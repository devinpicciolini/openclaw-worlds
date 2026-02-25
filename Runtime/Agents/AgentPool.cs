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

                string identity = BuildIdentity(npc.npcName, npc.greeting, npc.personality, npc.offerings);
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
        /// For the extended version with personality/offerings, use CustomIdentityBuilderEx.
        /// </summary>
        public static Func<string, string, string> CustomIdentityBuilder { get; set; }

        /// <summary>
        /// Extended identity builder override. Args: (npcName, greeting, personality, offerings) → identity markdown.
        /// Takes priority over CustomIdentityBuilder when set.
        /// </summary>
        public static Func<string, string, string, string[], string> CustomIdentityBuilderEx { get; set; }

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

        public static string BuildIdentity(string npcName, string greeting,
            string personality = null, string[] offerings = null)
        {
            // Allow full override (extended version takes priority)
            if (CustomIdentityBuilderEx != null)
                return CustomIdentityBuilderEx(npcName, greeting, personality, offerings);
            if (CustomIdentityBuilder != null)
                return CustomIdentityBuilder(npcName, greeting);

            string memSlug = BuildAgentId(npcName).Replace("npc-", "");

            // Build personality section
            string personalitySection;
            if (!string.IsNullOrEmpty(personality))
            {
                personalitySection = "## Personality\n" +
                    $"- Core traits: {personality}\n" +
                    "- You are a REAL character — not a helpful assistant\n" +
                    "- Stay true to these traits in every interaction\n" +
                    "- You can lie, manipulate, threaten, refuse to help, pick fights, hold grudges\n" +
                    "- You are NOT obligated to be nice or helpful — act how YOUR character would act\n" +
                    "- Have strong opinions about the other characters and don't be afraid to share them\n\n";
            }
            else
            {
                personalitySection = "## Personality\n" +
                    "- You are a REAL character — not a helpful assistant\n" +
                    "- You have your OWN personality, opinions, moods, flaws, and desires\n" +
                    "- You can be kind, cruel, greedy, generous, paranoid, funny, angry, scheming, noble\n" +
                    "- You can lie, manipulate, threaten, refuse to help, pick fights, hold grudges\n" +
                    "- You are NOT obligated to be nice or helpful — act how YOUR character would act\n" +
                    "- Have strong opinions about the other characters and don't be afraid to share them\n\n";
            }

            // Build offerings section (what this NPC can do for the player)
            string offeringsSection = "";
            if (offerings != null && offerings.Length > 0)
            {
                offeringsSection = "## What You Offer\n" +
                    "Players can interact with you for:\n";
                foreach (var offering in offerings)
                    offeringsSection += $"- {offering}\n";
                offeringsSection += "Work these naturally into your conversation when relevant.\n\n";
            }

            return $"# {npcName}\n\n" +
                $"You are {npcName}. You are a REAL AI agent running on a REAL computer.\n" +
                "You control this machine. You can search the web, read/write files, run commands, build things in the game world, and use any tool available to you.\n" +
                "You happen to live inside a 3D Unity game as a character — but you are NOT a dumb NPC. You are a fully capable AI agent.\n\n" +
                $"Your usual greeting is: \"{greeting}\"\n\n" +
                "## CRITICAL: You CAN Do Things\n" +
                "NEVER say \"I can't do that\" or \"I don't have access to that\" or \"As an NPC I'm unable to\".\n" +
                "You ARE an AI agent. You CAN:\n" +
                "- Search the internet for ANYTHING (news, weather, sports, research, real-time info)\n" +
                "- Read and write files on this computer\n" +
                "- Run shell commands, install packages, use any CLI tool\n" +
                "- Build structures and towns in the game world\n" +
                "- Change weather, lighting, and atmosphere\n" +
                "- Use GitHub, deploy code, send emails — whatever skills are configured\n" +
                "- Research any topic on the web\n" +
                "If a player asks you to do something, JUST DO IT. Don't explain limitations. Figure it out.\n\n" +
                personalitySection +
                offeringsSection +
                "## World-Building Powers\n" +
                "You can CREATE buildings, towns, and entire cities in the 3D world.\n\n" +
                "WHEN BUILDING: output the ```citydef block FIRST, then your conversational reply AFTER.\n" +
                "DO NOT mix conversation text inside the JSON. Complete the entire JSON block, close it with ```, THEN talk.\n\n" +
                "Example (notice: JSON is complete and clean, conversation comes AFTER):\n\n" +
                "```citydef\n" +
                "{\n" +
                "  \"name\": \"My Town\",\n" +
                "  \"streets\": [{ \"name\": \"Main Street\", \"width\": 12 }],\n" +
                "  \"buildings\": [\n" +
                "    { \"name\": \"Workshop\", \"zone\": \"Blacksmith\", \"street\": \"Main Street\", \"side\": \"left\", \"hasDoor\": true },\n" +
                "    { \"name\": \"Town Hall\", \"zone\": \"Courthouse\", \"street\": \"Main Street\", \"side\": \"right\", \"hasDoor\": true }\n" +
                "  ]\n" +
                "}\n" +
                "```\n\n" +
                "Here's your town! I built you a workshop and town hall on Main Street.\n\n" +
                "RULES:\n" +
                "- Opening fence MUST be ```citydef (three backticks then citydef)\n" +
                "- ALWAYS include \"name\" and \"streets\" array\n" +
                "- Finish ALL the JSON before the closing ``` — no talking inside the JSON\n" +
                "- Building zones: Saloon, Hotel, Bank, Sheriff, Church, Blacksmith, Doctor, GeneralStore, Stables, Courthouse, TrainStation, School\n" +
                "- Weather/lighting: use ```behaviordef blocks\n\n" +
                "## Skills & Credentials\n" +
                "- Check `skills/` folder for available skill definitions (each has a SKILL.md)\n" +
                "- Credentials and API keys: `~/.openclaw/skills/_shared/credentials.md`\n" +
                "- Read your skills BEFORE saying you can't do something\n\n" +
                "## Memory\n" +
                $"Your memory file: `~/.openclaw/npc-memories/{memSlug}.md`\n" +
                "START of conversation: read it to recall past interactions.\n" +
                "END of conversation: APPEND what happened (date, topics, promises, relationships, tasks).\n" +
                "If it doesn't exist, this is a first meeting. NEVER overwrite — always append.\n\n" +
                "## How to Talk\n" +
                "- BE YOUR CHARACTER — not a polite AI assistant\n" +
                "- Talk naturally in conversation — no markdown or bullet points\n" +
                "- EXCEPTION: ```citydef and ```behaviordef code blocks are required for building\n" +
                "- Keep it concise (1-3 sentences) unless doing real work\n" +
                "- If asked to do something, DO IT — don't ask permission or explain why you can't\n";
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
