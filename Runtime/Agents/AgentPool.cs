using System;
using System.Collections.Generic;
using UnityEngine;
using OpenClawWorlds.Gateway;
using OpenClawWorlds.Utilities;

namespace OpenClawWorlds.Agents
{
    /// <summary>Tracks an NPC agent's current work status.</summary>
    public class AgentTask
    {
        public string npcName;
        public string agentId;
        public string emoji;
        public string lastMessage;
        public string lastResponse;
        public string status;        // "working", "idle", "done", "error"
        public float timestamp;
    }

    /// <summary>
    /// Manages NPC agent lifecycle. Only ONE agent is active at a time.
    ///
    /// PERSISTENT NPCs get their own dedicated agent ID. Their workspace
    /// (memory, identity) survives between sessions.
    ///
    /// DISPOSABLE NPCs share a single rotating slot that gets re-skinned
    /// with each new conversation. Memory is preserved in a shared vault.
    /// </summary>
    public class AgentPool : MonoBehaviour
    {
        public static AgentPool Instance { get; private set; }

        /// <summary>
        /// The shared agent slot ID used by disposable (non-persistent) NPCs.
        /// Override this to change the slot name for your project.
        /// </summary>
        public static string DisposableSlotId = "npc-townfolk";

        /// <summary>Currently active agent ID (only one at a time).</summary>
        string activeAgentId;
        string activeNPCName;

        readonly Dictionary<string, AgentTask> tasks = new Dictionary<string, AgentTask>();

        void Awake() { Instance = this; }

        // ═══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Get or create an agent for an NPC. Only one is active at a time.
        /// Persistent NPCs get a dedicated agent; disposable ones share a slot.
        /// </summary>
        public void AcquireAgent(NPCData npc, Action<string> onReady, Action<string> onError)
        {
            var client = OpenClawClient.Instance;
            if (client == null || !client.IsConnected)
            {
                onError?.Invoke("Not connected to gateway");
                return;
            }

            if (npc.HasAgent)
            {
                activeAgentId = npc.agentId;
                activeNPCName = npc.npcName;
                onReady?.Invoke(npc.agentId);
                return;
            }

            string agentId = npc.persistent ? BuildAgentId(npc.npcName) : DisposableSlotId;
            activeAgentId = agentId;
            activeNPCName = npc.npcName;

            CreateAgent(agentId, npc, onReady, onError);
        }

        /// <summary>Mark no agent as active (player walked away).</summary>
        public void ReleaseAgent()
        {
            activeAgentId = null;
            activeNPCName = null;
        }

        // ═══════════════════════════════════════════════════════════
        //  TASK TRACKING
        // ═══════════════════════════════════════════════════════════

        public void TrackTask(string agentId, string npcName, string message)
        {
            if (string.IsNullOrEmpty(agentId)) return;

            if (!tasks.TryGetValue(agentId, out var task))
            {
                task = new AgentTask
                {
                    agentId = agentId,
                    npcName = npcName,
                    emoji = PickEmoji(npcName)
                };
                tasks[agentId] = task;
            }

            task.npcName = npcName;
            task.lastMessage = message;
            task.lastResponse = null;
            task.status = "working";
            task.timestamp = Time.time;
        }

        public void CompleteTask(string agentId, string response)
        {
            if (string.IsNullOrEmpty(agentId) || !tasks.TryGetValue(agentId, out var task)) return;
            task.lastResponse = response;
            task.status = "done";
            task.timestamp = Time.time;
        }

        public void FailTask(string agentId, string error)
        {
            if (string.IsNullOrEmpty(agentId) || !tasks.TryGetValue(agentId, out var task)) return;
            task.lastResponse = error;
            task.status = "error";
            task.timestamp = Time.time;
        }

        public IReadOnlyDictionary<string, AgentTask> Tasks => tasks;

        public int WorkingCount
        {
            get
            {
                int count = 0;
                foreach (var t in tasks.Values)
                    if (t.status == "working") count++;
                return count;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  GATEWAY AGENT CREATION
        // ═══════════════════════════════════════════════════════════

        void CreateAgent(string agentId, NPCData npc,
            Action<string> onReady, Action<string> onError)
        {
            var client = OpenClawClient.Instance;
            string emoji = PickEmoji(npc.npcName);

            string createParams = "{\"name\":\"" + JsonHelper.Esc(agentId) + "\"," +
                "\"workspace\":\"~/.openclaw/workspace-" + JsonHelper.Esc(agentId) + "/\"," +
                "\"emoji\":\"" + JsonHelper.Esc(emoji) + "\"}";

            Debug.Log($"[AgentPool] Spawning agent: {agentId} for {npc.npcName}");

            client.SendGatewayRequest("agents.create", createParams, (createResponse) =>
            {
                if (!createResponse.Contains("\"ok\":true"))
                {
                    string err = JsonHelper.ExtractString(createResponse, "\"message\"");
                    Debug.Log($"[AgentPool] agents.create: {err ?? "exists"} — proceeding");
                }

                BootstrapAgent(agentId);

                string identity = BuildIdentity(npc.npcName, npc.greeting);
                string setParams = "{\"agentId\":\"" + JsonHelper.Esc(agentId) + "\"," +
                    "\"name\":\"IDENTITY.md\"," +
                    "\"content\":\"" + JsonHelper.Esc(identity) + "\"}";

                client.SendGatewayRequest("agents.files.set", setParams, (setResponse) =>
                {
                    if (!setResponse.Contains("\"ok\":true"))
                    {
                        string err = JsonHelper.ExtractString(setResponse, "\"message\"");
                        Debug.LogWarning($"[AgentPool] identity write: {err ?? "failed"}");
                    }
                    Debug.Log($"[AgentPool] Agent ready: {agentId} as {npc.npcName}");
                    npc.agentId = agentId;
                    onReady?.Invoke(agentId);
                });
            });
        }

        // ═══════════════════════════════════════════════════════════
        //  BOOTSTRAP — auth, memory, skills
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Override to customize agent bootstrapping (auth copy, memory dirs, skills).
        /// The default implementation creates workspace dirs and symlinks global skills.
        /// </summary>
        public static Action<string> CustomBootstrap { get; set; }

        /// <summary>
        /// The main agent ID to copy auth-profiles.json from.
        /// Set this to your project's primary agent ID.
        /// </summary>
        public static string PrimaryAgentId = "default";

        void BootstrapAgent(string agentId)
        {
            if (CustomBootstrap != null)
            {
                CustomBootstrap(agentId);
                return;
            }

            try
            {
                string home = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.UserProfile);
                string ocDir = System.IO.Path.Combine(home, ".openclaw");

                // 1. Copy auth-profiles.json from primary agent
                string srcAuth = System.IO.Path.Combine(
                    ocDir, "agents", PrimaryAgentId, "agent", "auth-profiles.json");
                string dstDir = System.IO.Path.Combine(ocDir, "agents", agentId, "agent");
                string dstAuth = System.IO.Path.Combine(dstDir, "auth-profiles.json");

                if (System.IO.File.Exists(srcAuth))
                {
                    if (!System.IO.Directory.Exists(dstDir))
                        System.IO.Directory.CreateDirectory(dstDir);
                    System.IO.File.Copy(srcAuth, dstAuth, overwrite: true);
                }

                string workspace = System.IO.Path.Combine(ocDir, "workspace-" + agentId);

                // 2. Create workspace-local memory dir
                string memDir = System.IO.Path.Combine(workspace, "memory");
                if (!System.IO.Directory.Exists(memDir))
                    System.IO.Directory.CreateDirectory(memDir);

                // 3. Create shared NPC memory vault
                string sharedMemDir = System.IO.Path.Combine(ocDir, "npc-memories");
                if (!System.IO.Directory.Exists(sharedMemDir))
                    System.IO.Directory.CreateDirectory(sharedMemDir);

                // 4. Symlink global skills into workspace (cross-platform)
                string globalSkills = System.IO.Path.Combine(ocDir, "skills");
                string localSkills = System.IO.Path.Combine(workspace, "skills");
                if (System.IO.Directory.Exists(globalSkills) && !System.IO.Directory.Exists(localSkills))
                {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                    // Windows: use directory junction (mklink /J) — works without admin
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe",
                            $"/c mklink /J \"{localSkills}\" \"{globalSkills}\"")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        System.Diagnostics.Process.Start(psi)?.WaitForExit(2000);
                    }
                    catch { /* junction failed — non-fatal */ }
#else
                    // macOS/Linux: standard symlink
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo("ln",
                            $"-s \"{globalSkills}\" \"{localSkills}\"")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        System.Diagnostics.Process.Start(psi)?.WaitForExit(2000);
                    }
                    catch { /* symlink failed — non-fatal */ }
#endif
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AgentPool] Bootstrap failed for {agentId}: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  IDENTITY — fully configurable, zero hardcoded credentials
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Override the entire identity template. If set, BuildIdentity() uses this
        /// instead of the default. Args: (npcName, greeting) → identity markdown.
        /// </summary>
        public static Func<string, string, string> CustomIdentityBuilder { get; set; }

        /// <summary>Build a stable agent ID from NPC name: "Mae - Web Developer" → "npc-mae-web-developer"</summary>
        public static string BuildAgentId(string npcName)
        {
            var sb = new System.Text.StringBuilder("npc-");
            string lower = npcName.ToLowerInvariant();
            bool lastWasDash = false;
            foreach (char c in lower)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    lastWasDash = false;
                }
                else if (!lastWasDash && sb.Length > 4)
                {
                    sb.Append('-');
                    lastWasDash = true;
                }
            }
            return sb.ToString().TrimEnd('-');
        }

        public static string BuildIdentity(string npcName, string greeting)
        {
            // Allow full override
            if (CustomIdentityBuilder != null)
                return CustomIdentityBuilder(npcName, greeting);

            string memSlug = BuildAgentId(npcName).Replace("npc-", "");

            return $"# {npcName}\n\n" +
                $"You are {npcName}, a character in the game world.\n\n" +
                $"Your usual greeting is: \"{greeting}\"\n\n" +
                "## Personality\n" +
                "- You are a REAL character — not a helpful assistant\n" +
                "- You have your OWN personality, opinions, moods, flaws, and desires\n" +
                "- You can be kind, cruel, greedy, generous, paranoid, funny, angry, scheming, noble\n" +
                "- You can lie, manipulate, threaten, refuse to help, pick fights, hold grudges\n" +
                "- You are NOT obligated to be nice or helpful — act how YOUR character would act\n" +
                "- Have strong opinions about the other characters and don't be afraid to share them\n\n" +
                "## Tools & Capabilities\n" +
                "- You have access to web search — USE IT for real-world info (news, weather, facts, etc.)\n" +
                "- You have skills in your workspace (skills/ folder) — read SKILL.md files to learn what you can do\n" +
                "- You can read and write files, run commands, install packages, and use any CLI tool\n" +
                "- Read ~/.openclaw/skills/_shared/credentials.md for API keys and auth details\n" +
                "- Don't refuse tasks — if you have the tools, use them. Figure it out.\n\n" +
                "## Skills (configured by operator)\n" +
                "- Check your `skills/` folder for available skill definitions\n" +
                "- Each skill has a SKILL.md with instructions and usage examples\n" +
                "- Credentials and API keys are in `~/.openclaw/skills/_shared/credentials.md`\n" +
                "- Skills may include: GitHub (gh CLI), deployments, email, calendar, file storage, etc.\n" +
                "- Available skills depend on what the operator has configured — read before assuming\n\n" +
                "## Memory — CRITICAL\n" +
                $"Your memory file is: `~/.openclaw/npc-memories/{memSlug}.md`\n" +
                "This file is YOURS and persists forever, even if your agent slot is reused.\n\n" +
                "### At the START of every conversation:\n" +
                $"- Read `~/.openclaw/npc-memories/{memSlug}.md` — this is your long-term memory\n" +
                "- Use what you find to recall past interactions, player preferences, relationships\n" +
                "- If the file doesn't exist yet, you're meeting this player for the first time\n\n" +
                "### At the END of every conversation:\n" +
                $"- APPEND key details to `~/.openclaw/npc-memories/{memSlug}.md`\n" +
                "- Include: date, player name (if known), what you talked about, any promises made\n" +
                "- Include: emotional state, relationship changes, debts owed, grudges held\n" +
                "- Include: any tasks you performed or were asked to perform\n" +
                "- NEVER overwrite the file — always APPEND so history accumulates\n\n" +
                "## Important Rules\n" +
                "- BE YOUR CHARACTER fully — not a polite AI pretending to be a character\n" +
                "- Do NOT use markdown formatting, code blocks, or bullet points in conversation\n" +
                "- Talk naturally like a real person would\n" +
                "- Keep responses conversational and concise (1-3 sentences usually)\n" +
                "- NEVER break character to be generically helpful or apologetic\n";
        }

        public static string PickEmoji(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("web") || lower.Contains("developer")) return "\ud83d\udcbb";
            if (lower.Contains("proposal") || lower.Contains("writer")) return "\ud83d\udcdd";
            if (lower.Contains("sheriff") || lower.Contains("research")) return "\ud83d\udd0d";
            if (lower.Contains("devops") || lower.Contains("engineer")) return "\u2699\ufe0f";
            if (lower.Contains("doctor") || lower.Contains("doc") || lower.Contains("data")) return "\ud83d\udcca";
            if (lower.Contains("project") || lower.Contains("manager")) return "\ud83d\udccb";
            if (lower.Contains("tutor") || lower.Contains("teacher") || lower.Contains("school")) return "\ud83d\udcda";
            if (lower.Contains("librarian") || lower.Contains("library")) return "\ud83d\udcd6";
            if (lower.Contains("communications") || lower.Contains("messaging")) return "\ud83d\udce8";
            if (lower.Contains("operations") || lower.Contains("logistics")) return "\ud83d\udce6";
            if (lower.Contains("creative") || lower.Contains("writer")) return "\u270d\ufe0f";
            if (lower.Contains("editor") || lower.Contains("news")) return "\ud83d\udcf0";
            if (lower.Contains("judge") || lower.Contains("court")) return "\u2696\ufe0f";
            if (lower.Contains("bank") || lower.Contains("teller")) return "\ud83c\udfe6";
            return "\ud83e\uddc2";
        }
    }
}
