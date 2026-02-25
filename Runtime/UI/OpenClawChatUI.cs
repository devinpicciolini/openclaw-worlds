using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using OpenClawWorlds.Gateway;
using OpenClawWorlds.Agents;
using OpenClawWorlds.Player;
using OpenClawWorlds.Protocols;
using OpenClawWorlds.World;
using OpenClawWorlds.Utilities;

namespace OpenClawWorlds.UI
{
    /// <summary>
    /// Built-in chat UI for OpenClaw agents. Press Tab to open/close.
    ///
    /// Features:
    /// - Chat tab: talk to the main agent or nearest NPC agent
    /// - Skills tab: live list of available skills from the gateway
    /// - Crons tab: scheduled cron jobs from the gateway
    /// - Quick action presets: one-tap buttons for common requests
    /// - CityDef audit loop: retries JSON until it passes validation
    ///
    /// Uses IMGUI — no prefabs, no canvas setup, just add the component and it works.
    /// Replace this with your own UI for production games.
    /// </summary>
    public class OpenClawChatUI : MonoBehaviour
    {
        [Tooltip("Key to toggle the chat window")]
        public KeyCode toggleKey = KeyCode.Tab;

        [Tooltip("Key to interact with nearby NPCs")]
        public KeyCode interactKey = KeyCode.E;

        [Tooltip("Max distance to detect NPCs for interaction prompt")]
        public float npcInteractRange = 5f;

        // Nearby NPC tracking (for E-key interaction prompt)
        NPCData nearbyNPC;
        float nearbyNPCDist;

        // Quick action presets — shown as buttons in the chat tab
        static readonly string[][] QuickActions = new string[][]
        {
            new[] { "Small Town",     "Build me a small western town with 1 street, a saloon, hotel, sheriff office, and general store. Add 3 wandering NPCs." },
            new[] { "Medium Town",    "Build me a medium town with 2 streets and 8 buildings on each side — saloon, hotel, bank, sheriff, church, blacksmith, doctor, general store, stables, and a courthouse. Add 5 NPCs." },
            new[] { "Single Building","Build me a single large saloon building with detailed interior." },
            new[] { "2 Streets",      "Build a town with 2 parallel streets. Put 3 buildings on each side of each street — 12 buildings total. Include a church at the end of the main street." },
            new[] { "Make it Rain",   "Make it rain with dark clouds and fog." },
            new[] { "Sunset",         "Set the lighting to a warm sunset with orange sky and long shadows." },
        };

        bool chatOpen;
        string inputText = "";
        string statusText = "";
        bool waiting;
        Vector2 chatScrollPos;
        Vector2 skillsScrollPos;
        Vector2 cronsScrollPos;
        NPCData currentNPC;
        string currentAgentId;
        bool agentReady;
        bool shouldScrollToBottom;
        int lastLineCount;

        // Tabs
        int activeTab; // 0 = Chat, 1 = Skills, 2 = Crons
        static readonly string[] TabNames = { "Chat", "Skills", "Crons" };

        // Timeout: if no response after this many seconds, stop waiting
        const float ResponseTimeout = 90f;
        float lastSendTime;

        struct ChatLine
        {
            public string speaker;
            public string text;
            public Color color;
        }

        readonly List<ChatLine> lines = new List<ChatLine>();

        // Skills (fetched from gateway)
        struct SkillInfo
        {
            public string name;
            public string emoji;
            public string description;
            public string source;
            public bool ready;
        }
        readonly List<SkillInfo> skills = new List<SkillInfo>();
        bool skillsLoaded;
        bool skillsLoading;
        string skillsError;

        // Crons (fetched from gateway)
        struct CronInfo
        {
            public string id;
            public string name;
            public string schedule;
            public string status;
            public string next;
            public string last;
        }
        readonly List<CronInfo> crons = new List<CronInfo>();
        bool cronsLoaded;
        bool cronsLoading;
        string cronsError;

        // Styling
        GUIStyle boxStyle;
        GUIStyle labelStyle;
        GUIStyle inputStyle;
        GUIStyle buttonStyle;
        GUIStyle headerStyle;
        GUIStyle statusStyle;
        GUIStyle hudStyle;
        GUIStyle closeHintStyle;
        GUIStyle tabActiveStyle;
        GUIStyle tabInactiveStyle;
        GUIStyle skillNameStyle;
        GUIStyle skillDescStyle;
        GUIStyle cronNameStyle;
        GUIStyle cronDetailStyle;
        GUIStyle quickActionStyle;
        bool stylesReady;
        Texture2D boxBgTex;
        Texture2D tabActiveTex;
        Texture2D tabInactiveTex;
        Texture2D quickActionTex;

        void OnDestroy()
        {
            CancelInvoke();
            if (boxBgTex != null) Destroy(boxBgTex);
            if (tabActiveTex != null) Destroy(tabActiveTex);
            if (tabInactiveTex != null) Destroy(tabInactiveTex);
            if (quickActionTex != null) Destroy(quickActionTex);
        }

        void Update()
        {
            // Enter to send when chat is open and on Chat tab
            if (chatOpen && activeTab == 0 && Input.GetKeyDown(KeyCode.Return) && !waiting)
            {
                string msg = inputText != null ? inputText.Trim() : "";
                if (!string.IsNullOrEmpty(msg))
                {
                    SendChat(msg);
                    inputText = "";
                }
            }

            // ── Timeout: don't hang forever ──
            if (waiting && Time.time - lastSendTime > ResponseTimeout)
            {
                waiting = false;
                statusText = "";
                lines.Add(new ChatLine
                {
                    speaker = "System",
                    text = "Response timed out — try again.",
                    color = new Color(1f, 0.6f, 0.3f)
                });
                shouldScrollToBottom = true;
            }

            // ── NPC proximity detection (E to talk) ──
            if (!chatOpen)
            {
                // Scan for nearby NPCs every frame — lightweight since NPC count is small
                float dist;
                nearbyNPC = FindNearestNPC(npcInteractRange, out dist);
                nearbyNPCDist = dist;

                // Press E to open chat with nearby NPC
                if (nearbyNPC != null && Input.GetKeyDown(interactKey))
                {
                    OpenChatWithNPC(nearbyNPC);
                }
            }
            else
            {
                nearbyNPC = null;
            }
        }

        /// <summary>
        /// Opens the chat panel targeting a specific NPC.
        /// Acquires the NPC's agent automatically.
        /// </summary>
        void OpenChatWithNPC(NPCData npc)
        {
            chatOpen = true;
            SimplePlayerController.InputBlocked = true;

            currentNPC = npc;
            lines.Clear();
            lines.Add(new ChatLine
            {
                speaker = npc.npcName,
                text = npc.greeting,
                color = new Color(0.4f, 0.8f, 1f)
            });
            AcquireNPCAgent();

            // Fetch skills & crons when chat opens
            if (!skillsLoaded && !skillsLoading)
                FetchSkillsFromGateway();
            if (!cronsLoaded && !cronsLoading)
                FetchCronsFromGateway();

            shouldScrollToBottom = true;
            activeTab = 0;
        }

        NPCData FindNearestNPC(float maxRange, out float distance)
        {
            distance = float.MaxValue;

            // Use actual player body position, NOT the camera (which orbits behind in 3rd person)
            Vector3 playerPos = transform.position;
            var pc = SimplePlayerController.FindFirstInstance();
            if (pc != null)
                playerPos = pc.transform.position;
            else
            {
                // Fallback: find any CharacterController
#if UNITY_2023_1_OR_NEWER
                var cc = FindAnyObjectByType<CharacterController>();
#else
                var cc = FindObjectOfType<CharacterController>();
#endif
                if (cc != null) playerPos = cc.transform.position;
            }

#if UNITY_2023_1_OR_NEWER
            var allNPCs = FindObjectsByType<NPCData>(FindObjectsSortMode.None);
#else
            var allNPCs = FindObjectsOfType<NPCData>();
#endif
            NPCData nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var npc in allNPCs)
            {
                float dist = Vector3.Distance(playerPos, npc.transform.position);
                if (dist < nearestDist && dist <= maxRange)
                {
                    nearestDist = dist;
                    nearest = npc;
                }
            }
            distance = nearestDist;
            return nearest;
        }

        void AcquireNPCAgent()
        {
            if (currentNPC == null) return;

            var pool = AgentPool.Instance;
            if (pool == null)
            {
                statusText = "AgentPool not found — talking to main agent.";
                agentReady = true;
                return;
            }

            var client = OpenClawClient.Instance;
            if (client == null || !client.IsConnected)
            {
                statusText = "Connecting to gateway...";
                Invoke(nameof(AcquireNPCAgent), 1f);
                return;
            }

            statusText = "Acquiring agent...";
            pool.AcquireAgent(currentNPC,
                onReady: (agentId) =>
                {
                    currentAgentId = agentId;
                    agentReady = true;
                    statusText = "";
                    Debug.Log($"[OpenClawChat] Agent acquired: {agentId} for {currentNPC.npcName}");
                },
                onError: (err) =>
                {
                    Debug.LogWarning($"[OpenClawChat] Agent acquire failed: {err} — using main agent");
                    agentReady = true;
                    statusText = "";
                }
            );
        }

        void SendChat(string message)
        {
            var client = OpenClawClient.Instance;
            if (client == null || !client.IsConnected)
            {
                statusText = "Not connected — waiting for gateway...";
                return;
            }

            lines.Add(new ChatLine { speaker = "You", text = message, color = Color.white });
            waiting = true;
            lastSendTime = Time.time;
            statusText = "Thinking...";
            shouldScrollToBottom = true;

            if (!string.IsNullOrEmpty(currentAgentId))
            {
                if (AgentPool.Instance != null)
                    AgentPool.Instance.TrackTask(currentAgentId, currentNPC?.npcName ?? "NPC", message);

                client.SendNPCMessage(currentAgentId, message,
                    onComplete: (response) => HandleResponse(response),
                    onError: (err) => HandleError(err)
                );
            }
            else
            {
                var config = AIConfig.Instance;
                string prompt = config != null ? config.systemPrompt : "";
                var messages = new List<ChatMessage> { new ChatMessage("user", message) };

                client.SendMessage(prompt, messages,
                    onComplete: (response) => HandleResponse(response),
                    onError: (err) => HandleError(err)
                );
            }
        }

        void HandleResponse(string response)
        {
            string speaker = currentNPC != null ? currentNPC.npcName
                : (AIConfig.Instance != null ? AIConfig.Instance.assistantName : "Agent");

            // Strip code blocks from display
            string displayText = StripAllCodeFences(response);
            if (string.IsNullOrWhiteSpace(displayText))
                displayText = response;

            lines.Add(new ChatLine
            {
                speaker = speaker,
                text = displayText.Trim(),
                color = new Color(0.4f, 0.8f, 1f)
            });
            waiting = false;
            statusText = "";
            shouldScrollToBottom = true;

            if (!string.IsNullOrEmpty(currentAgentId) && AgentPool.Instance != null)
                AgentPool.Instance.CompleteTask(currentAgentId, response);

            // ── Process protocols: CityDef, BehaviorDef, HotReload ──
            ProcessProtocols(response);
        }

        static string StripAllCodeFences(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            while (true)
            {
                int open = text.IndexOf("```");
                if (open < 0) break;
                int close = text.IndexOf("```", open + 3);
                if (close < 0) { text = text.Substring(0, open); break; }
                text = text.Substring(0, open) + text.Substring(close + 3);
            }
            return text.Trim();
        }

        void ProcessProtocols(string response)
        {
            // CityDef — spawn towns from ```citydef or ```json blocks
            try
            {
                Vector3 spawnOrigin = Camera.main != null
                    ? Camera.main.transform.position + Camera.main.transform.forward * 30f
                    : Vector3.zero;
                spawnOrigin.y = 0;

                string citySummary = CityDefSpawner.ProcessResponse(response, spawnOrigin);
                if (citySummary != null)
                {
                    lines.Add(new ChatLine
                    {
                        speaker = "System",
                        text = citySummary,
                        color = new Color(0.4f, 1f, 0.4f)
                    });
                    shouldScrollToBottom = true;
                    Debug.Log($"[OpenClawChat] CityDef: {citySummary}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenClawChat] CityDef processing failed: {e.Message}");
            }

            // BehaviorDef — weather, lighting, physics, particles
            try
            {
                string behaviorSummary = BehaviorEngine.ProcessResponse(response);
                if (behaviorSummary != null)
                {
                    lines.Add(new ChatLine
                    {
                        speaker = "System",
                        text = behaviorSummary,
                        color = new Color(0.4f, 1f, 0.4f)
                    });
                    shouldScrollToBottom = true;
                    Debug.Log($"[OpenClawChat] BehaviorDef: {behaviorSummary}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenClawChat] BehaviorDef processing failed: {e.Message}");
            }

            // HotReload — C# code blocks compile live in the editor
            try
            {
                string hotReloadSummary = HotReloadBridge.ProcessResponse(response);
                if (hotReloadSummary != null)
                {
                    lines.Add(new ChatLine
                    {
                        speaker = "System",
                        text = hotReloadSummary,
                        color = new Color(1f, 0.9f, 0.4f)
                    });
                    shouldScrollToBottom = true;
                    Debug.Log($"[OpenClawChat] HotReload: {hotReloadSummary}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenClawChat] HotReload processing failed: {e.Message}");
            }
        }

        void HandleError(string err)
        {
            lines.Add(new ChatLine
            {
                speaker = "System",
                text = $"Error: {err}",
                color = new Color(1f, 0.4f, 0.4f)
            });
            waiting = false;
            statusText = "";
            shouldScrollToBottom = true;

            if (!string.IsNullOrEmpty(currentAgentId) && AgentPool.Instance != null)
                AgentPool.Instance.FailTask(currentAgentId, err);
        }

        // ─── Gateway data fetching ──────────────────────────────────

        void FetchSkillsFromGateway()
        {
            var client = OpenClawClient.Instance;
            if (client == null || !client.IsConnected) return;

            skillsLoading = true;
            skillsError = null;
            client.SendGatewayRequest("skills.status", "{}", (response) =>
            {
                skillsLoading = false;
                if (response.Contains("\"ok\":true"))
                {
                    ParseSkills(response);
                    skillsLoaded = true;
                }
                else
                {
                    skillsError = "Failed to fetch skills from gateway";
                }
            });
        }

        void ParseSkills(string raw)
        {
            skills.Clear();
            int searchPos = 0;
            while (true)
            {
                int nameIdx = raw.IndexOf("\"name\"", searchPos);
                if (nameIdx < 0) break;

                string name = JsonHelper.ExtractString(raw.Substring(nameIdx - 2), "\"name\"");
                if (string.IsNullOrEmpty(name)) { searchPos = nameIdx + 10; continue; }

                string section = raw.Substring(nameIdx, Math.Min(600, raw.Length - nameIdx));
                string desc = JsonHelper.ExtractString(section, "\"description\"");
                string emoji = JsonHelper.ExtractString(section, "\"emoji\"");
                string source = JsonHelper.ExtractString(section, "\"source\"");
                bool eligible = section.Contains("\"eligible\":true");
                bool disabled = section.Contains("\"disabled\":true");
                bool ready = section.Contains("\"ready\":true") || section.Contains("\"status\":\"ready\"");

                if (!string.IsNullOrEmpty(desc) && !disabled)
                {
                    skills.Add(new SkillInfo
                    {
                        name = name,
                        emoji = emoji ?? "",
                        description = desc.Length > 120 ? desc.Substring(0, 117) + "..." : desc,
                        source = source ?? "",
                        ready = ready || eligible
                    });
                }
                searchPos = nameIdx + 10;
            }
        }

        void FetchCronsFromGateway()
        {
            var client = OpenClawClient.Instance;
            if (client == null || !client.IsConnected) return;

            cronsLoading = true;
            cronsError = null;
            client.SendGatewayRequest("cron.list", "{}", (response) =>
            {
                cronsLoading = false;
                if (response.Contains("\"ok\":true"))
                {
                    ParseCrons(response);
                    cronsLoaded = true;
                }
                else
                {
                    cronsError = "Failed to fetch crons from gateway";
                }
            });
        }

        void ParseCrons(string raw)
        {
            crons.Clear();
            int searchPos = 0;
            while (true)
            {
                int nameIdx = raw.IndexOf("\"name\"", searchPos);
                if (nameIdx < 0) break;

                string name = JsonHelper.ExtractString(raw.Substring(nameIdx - 2), "\"name\"");
                if (string.IsNullOrEmpty(name)) { searchPos = nameIdx + 10; continue; }

                string section = raw.Substring(nameIdx, Math.Min(600, raw.Length - nameIdx));
                string schedule = JsonHelper.ExtractString(section, "\"schedule\"");
                string status = JsonHelper.ExtractString(section, "\"status\"");
                string id = JsonHelper.ExtractString(section, "\"id\"");
                string next = JsonHelper.ExtractString(section, "\"next\"");
                string last = JsonHelper.ExtractString(section, "\"last\"");

                if (!string.IsNullOrEmpty(name))
                {
                    crons.Add(new CronInfo
                    {
                        id = id ?? "",
                        name = name,
                        schedule = schedule ?? "",
                        status = status ?? "unknown",
                        next = next ?? "",
                        last = last ?? ""
                    });
                }
                searchPos = nameIdx + 10;
            }
        }

        // ─── IMGUI Rendering ────────────────────────────────────────

        void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            boxBgTex = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.15f, 0.95f));
            tabActiveTex = MakeTex(2, 2, new Color(0.18f, 0.22f, 0.30f, 0.95f));
            tabInactiveTex = MakeTex(2, 2, new Color(0.10f, 0.12f, 0.17f, 0.7f));

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = boxBgTex;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.wordWrap = true;
            labelStyle.richText = true;
            labelStyle.fontSize = 14;
            labelStyle.normal.textColor = Color.white;

            inputStyle = new GUIStyle(GUI.skin.textField);
            inputStyle.fontSize = 14;
            inputStyle.normal.textColor = Color.white;
            inputStyle.padding = new RectOffset(8, 8, 6, 6);

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 14;
            buttonStyle.fontStyle = FontStyle.Bold;

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 18;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0.4f, 0.8f, 1f);
            headerStyle.alignment = TextAnchor.MiddleCenter;

            statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.fontSize = 12;
            statusStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            statusStyle.alignment = TextAnchor.MiddleCenter;

            hudStyle = new GUIStyle(GUI.skin.label);
            hudStyle.fontSize = 16;
            hudStyle.richText = true;
            hudStyle.normal.textColor = Color.white;

            closeHintStyle = new GUIStyle(GUI.skin.label);
            closeHintStyle.fontSize = 11;
            closeHintStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            closeHintStyle.alignment = TextAnchor.MiddleRight;

            tabActiveStyle = new GUIStyle(GUI.skin.button);
            tabActiveStyle.fontSize = 13;
            tabActiveStyle.fontStyle = FontStyle.Bold;
            tabActiveStyle.alignment = TextAnchor.MiddleCenter;
            tabActiveStyle.normal.background = tabActiveTex;
            tabActiveStyle.normal.textColor = new Color(0.95f, 0.90f, 0.75f);

            tabInactiveStyle = new GUIStyle(GUI.skin.button);
            tabInactiveStyle.fontSize = 13;
            tabInactiveStyle.alignment = TextAnchor.MiddleCenter;
            tabInactiveStyle.normal.background = tabInactiveTex;
            tabInactiveStyle.normal.textColor = new Color(0.50f, 0.50f, 0.45f);

            skillNameStyle = new GUIStyle(GUI.skin.label);
            skillNameStyle.fontSize = 13;
            skillNameStyle.fontStyle = FontStyle.Bold;
            skillNameStyle.richText = true;
            skillNameStyle.normal.textColor = new Color(0.90f, 0.85f, 0.70f);

            skillDescStyle = new GUIStyle(GUI.skin.label);
            skillDescStyle.fontSize = 11;
            skillDescStyle.wordWrap = true;
            skillDescStyle.normal.textColor = new Color(0.72f, 0.72f, 0.68f);

            cronNameStyle = new GUIStyle(GUI.skin.label);
            cronNameStyle.fontSize = 13;
            cronNameStyle.fontStyle = FontStyle.Bold;
            cronNameStyle.richText = true;
            cronNameStyle.normal.textColor = new Color(0.70f, 0.85f, 0.95f);

            cronDetailStyle = new GUIStyle(GUI.skin.label);
            cronDetailStyle.fontSize = 11;
            cronDetailStyle.wordWrap = true;
            cronDetailStyle.richText = true;
            cronDetailStyle.normal.textColor = new Color(0.65f, 0.65f, 0.62f);

            quickActionTex = MakeTex(2, 2, new Color(0.15f, 0.25f, 0.35f, 0.9f));
            quickActionStyle = new GUIStyle(GUI.skin.button);
            quickActionStyle.fontSize = 12;
            quickActionStyle.wordWrap = true;
            quickActionStyle.alignment = TextAnchor.MiddleCenter;
            quickActionStyle.normal.background = quickActionTex;
            quickActionStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);
            quickActionStyle.hover.textColor = Color.white;
            quickActionStyle.padding = new RectOffset(8, 8, 6, 6);
        }

        Texture2D MakeTex(int w, int h, Color col)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        void OnGUI()
        {
            // Intercept Tab key in OnGUI before TextField eats it
            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == toggleKey)
            {
                chatOpen = !chatOpen;
                SimplePlayerController.InputBlocked = chatOpen;
                Event.current.Use();

                if (!chatOpen)
                {
                    // Release current NPC agent when chat closes
                    if (currentNPC != null && AgentPool.Instance != null)
                        AgentPool.Instance.ReleaseAgent();
                    currentNPC = null;
                    currentAgentId = null;
                    agentReady = false;
                    CancelInvoke(nameof(AcquireNPCAgent));
                    return;
                }

                // Re-detect nearest NPC every time chat opens (no range limit for Tab)
                float _d;
                currentNPC = FindNearestNPC(float.MaxValue, out _d);
                if (currentNPC != null)
                {
                    lines.Clear();
                    lines.Add(new ChatLine
                    {
                        speaker = currentNPC.npcName,
                        text = currentNPC.greeting,
                        color = new Color(0.4f, 0.8f, 1f)
                    });
                    AcquireNPCAgent();
                }
                else
                {
                    var config = AIConfig.Instance;
                    string name = config != null ? config.assistantName : "Agent";
                    if (lines.Count == 0)
                    {
                        lines.Add(new ChatLine
                        {
                            speaker = name,
                            text = "Hello! How can I help you?",
                            color = new Color(0.4f, 0.8f, 1f)
                        });
                    }
                    agentReady = true;
                }

                // Fetch skills & crons when chat opens
                if (!skillsLoaded && !skillsLoading)
                    FetchSkillsFromGateway();
                if (!cronsLoaded && !cronsLoading)
                    FetchCronsFromGateway();

                shouldScrollToBottom = true;
                return;
            }

            // HUD when chat is closed
            if (!chatOpen)
            {
                InitStyles();
                var client = OpenClawClient.Instance;
                string connStatus;
                if (client != null && client.IsConnected)
                    connStatus = "<color=#66ff66>Connected to OpenClaw</color>";
                else if (client != null && client.AuthTimedOut)
                    connStatus = "<color=#ff6666>Auth failed — run 'openclaw doctor' in terminal</color>";
                else
                    connStatus = "<color=#ff6666>Connecting...</color>";

                GUI.Label(new Rect(15, 15, 500, 30), connStatus, hudStyle);
                GUI.Label(new Rect(15, 40, 400, 30),
                    $"Press <b>{toggleKey}</b> to chat", hudStyle);

                // Dashboard hint — lighter text under the Tab hint
                {
                    var dashHintStyle = new GUIStyle(hudStyle);
                    dashHintStyle.fontSize = 12;
                    dashHintStyle.normal.textColor = new Color(0.6f, 0.6f, 0.55f, 0.5f);

                    var dashboard = OpenClawDashboard.Instance;
                    var pool = AgentPool.Instance;
                    int working = pool != null ? pool.WorkingCount : 0;
                    string badge = working > 0 ? $"  <color=#FFD700>({working} working)</color>" : "";
                    GUI.Label(new Rect(15, 62, 400, 24),
                        $"Press <b>`</b> for dashboard{badge}", dashHintStyle);
                }

                // NPC interaction prompt — centered on screen
                if (nearbyNPC != null)
                {
                    string npcPrompt = $"Press <b>{interactKey}</b> to talk to <b>{nearbyNPC.npcName}</b>";
                    float promptW = 400f;
                    float promptH = 40f;
                    float promptX = (Screen.width - promptW) / 2f;
                    float promptY = Screen.height * 0.65f;

                    // Semi-transparent background for readability
                    GUI.color = new Color(0, 0, 0, 0.6f);
                    GUI.DrawTexture(new Rect(promptX - 10, promptY - 5, promptW + 20, promptH + 10), Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    var npcPromptStyle = new GUIStyle(hudStyle)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 18
                    };
                    GUI.Label(new Rect(promptX, promptY, promptW, promptH), npcPrompt, npcPromptStyle);
                }

                return;
            }

            InitStyles();

            // Chat window — docked to the right so the scene is visible
            float w = 400f;
            float h = Screen.height - 40f;
            float x = Screen.width - w - 15f;
            float y = 20f;

            GUI.Box(new Rect(x, y, w, h), "", boxStyle);

            // Header
            string agentName = currentNPC != null ? currentNPC.npcName
                : (AIConfig.Instance != null ? AIConfig.Instance.assistantName : "Agent");
            string title = $"Chat with {agentName}";
            GUI.Label(new Rect(x, y + 8, w, 30), title, headerStyle);

            // Close hint
            GUI.Label(new Rect(x, y + 8, w - 15, 30),
                $"{toggleKey} to close", closeHintStyle);

            // Status
            if (!string.IsNullOrEmpty(statusText))
                GUI.Label(new Rect(x, y + 35, w, 20), statusText, statusStyle);

            // Tab bar
            float tabY = y + 38;
            float tabW = (w - 20) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                GUIStyle ts = (i == activeTab) ? tabActiveStyle : tabInactiveStyle;
                string tabLabel = TabNames[i];
                // Show counts in tab labels
                if (i == 1 && skillsLoaded) tabLabel = $"Skills ({skills.Count})";
                if (i == 2 && cronsLoaded) tabLabel = $"Crons ({crons.Count})";
                if (GUI.Button(new Rect(x + 10 + i * tabW, tabY, tabW, 26), tabLabel, ts))
                {
                    activeTab = i;
                    // Lazy fetch on tab switch
                    if (i == 1 && !skillsLoaded && !skillsLoading) FetchSkillsFromGateway();
                    if (i == 2 && !cronsLoaded && !cronsLoading) FetchCronsFromGateway();
                }
            }

            // Content area
            float contentY = tabY + 30;
            float contentH = h - 130;

            switch (activeTab)
            {
                case 0: DrawChatTab(x, contentY, w, contentH, y + h); break;
                case 1: DrawSkillsTab(x, contentY, w, contentH); break;
                case 2: DrawCronsTab(x, contentY, w, contentH); break;
            }
        }

        void DrawChatTab(float x, float contentY, float w, float contentH, float panelBottom)
        {
            // Messages
            Rect scrollViewRect = new Rect(x + 10, contentY, w - 20, contentH);

            float contentHeight = 0;
            foreach (var line in lines)
            {
                string formatted = $"<b>{line.speaker}:</b> {line.text}";
                float lineH = labelStyle.CalcHeight(new GUIContent(formatted), w - 50);
                contentHeight += lineH + 6;
            }
            if (waiting) contentHeight += 25;
            contentHeight += 20; // bottom padding

            Rect contentRect = new Rect(0, 0, w - 40,
                Mathf.Max(contentHeight, contentH));

            // Auto-scroll only when new messages arrive
            if (shouldScrollToBottom || lines.Count != lastLineCount)
            {
                if (contentHeight > contentH)
                    chatScrollPos.y = contentHeight - contentH + 20;
                shouldScrollToBottom = false;
                lastLineCount = lines.Count;
            }

            chatScrollPos = GUI.BeginScrollView(scrollViewRect, chatScrollPos, contentRect);

            float cy = 0;
            foreach (var line in lines)
            {
                string colorHex = ColorUtility.ToHtmlStringRGB(line.color);
                string formatted = $"<color=#{colorHex}><b>{line.speaker}:</b></color> {line.text}";
                float lineH = labelStyle.CalcHeight(new GUIContent(formatted), w - 50);
                GUI.Label(new Rect(5, cy, w - 50, lineH), formatted, labelStyle);
                cy += lineH + 6;
            }

            if (waiting)
            {
                float dots = Time.time % 1.5f;
                string dotStr = dots < 0.5f ? "." : dots < 1f ? ".." : "...";
                GUI.Label(new Rect(5, cy, w - 50, 25),
                    "<color=#999999><i>Thinking" + dotStr + "</i></color>", labelStyle);
            }

            GUI.EndScrollView();

            // Quick action buttons — show when chat is fresh (few messages)
            if (lines.Count <= 2 && !waiting)
            {
                float qaY = contentY + contentH + 4;
                float qaW = (w - 30) / 2f;
                float qaH = 30f;
                int col = 0;
                float qaStartY = qaY;

                for (int i = 0; i < QuickActions.Length && qaStartY < panelBottom - 100; i++)
                {
                    float qaX = x + 10 + col * (qaW + 5);
                    if (GUI.Button(new Rect(qaX, qaStartY, qaW, qaH), QuickActions[i][0], quickActionStyle))
                    {
                        SendChat(QuickActions[i][1]);
                        inputText = "";
                    }
                    col++;
                    if (col >= 2) { col = 0; qaStartY += qaH + 4; }
                }
            }

            // Input
            float inputY = panelBottom - 55;
            GUI.SetNextControlName("ChatInput");
            inputText = GUI.TextField(
                new Rect(x + 10, inputY, w - 90, 30), inputText ?? "", inputStyle);

            bool canSend = !waiting && !string.IsNullOrEmpty(inputText?.Trim());
            GUI.enabled = canSend;
            if (GUI.Button(new Rect(x + w - 75, inputY, 65, 30), "Send", buttonStyle))
            {
                string msg = inputText.Trim();
                if (!string.IsNullOrEmpty(msg))
                {
                    SendChat(msg);
                    inputText = "";
                }
            }
            GUI.enabled = true;

            // Focus input on chat tab
            if (GUI.GetNameOfFocusedControl() != "ChatInput")
                GUI.FocusControl("ChatInput");
        }

        void DrawSkillsTab(float x, float contentY, float w, float contentH)
        {
            Rect area = new Rect(x + 10, contentY, w - 20, contentH);

            if (skillsLoading)
            {
                GUI.Label(new Rect(x + 20, contentY + 20, w - 40, 30),
                    "<color=#999999><i>Loading skills from gateway...</i></color>", labelStyle);
                return;
            }
            if (!string.IsNullOrEmpty(skillsError))
            {
                GUI.Label(new Rect(x + 20, contentY + 20, w - 40, 30),
                    $"<color=#ff6666>{skillsError}</color>", labelStyle);
                if (GUI.Button(new Rect(x + 20, contentY + 55, 100, 26), "Retry", buttonStyle))
                    FetchSkillsFromGateway();
                return;
            }
            if (skills.Count == 0)
            {
                GUI.Label(new Rect(x + 20, contentY + 20, w - 40, 30),
                    "<color=#999999>No skills loaded. Check gateway connection.</color>", labelStyle);
                if (GUI.Button(new Rect(x + 20, contentY + 55, 100, 26), "Retry", buttonStyle))
                    FetchSkillsFromGateway();
                return;
            }

            // Calculate content height
            float totalH = 30f; // header
            float skillW = w - 50;
            foreach (var sk in skills)
            {
                totalH += 22f;
                totalH += skillDescStyle.CalcHeight(new GUIContent(sk.description), skillW - 20f) + 10f;
            }
            if (totalH < contentH) totalH = contentH;

            skillsScrollPos = GUI.BeginScrollView(area, skillsScrollPos,
                new Rect(0, 0, w - 40, totalH));

            float sy = 4f;
            int readyCount = 0;
            foreach (var sk in skills) if (sk.ready) readyCount++;
            GUI.Label(new Rect(4, sy, skillW, 20),
                $"<color=#FFD700>{readyCount} ready</color> / {skills.Count} total skills", skillNameStyle);
            sy += 26f;

            foreach (var sk in skills)
            {
                string dot = sk.ready ? "<color=#4f4>\u25cf</color>" : "<color=#888>\u25cb</color>";
                string src = !string.IsNullOrEmpty(sk.source)
                    ? $" <color=#555>[{sk.source}]</color>" : "";
                GUI.Label(new Rect(4, sy, skillW, 20),
                    $"{dot} {sk.emoji} <b>{sk.name}</b>{src}", skillNameStyle);
                sy += 20f;
                float dh = skillDescStyle.CalcHeight(new GUIContent(sk.description), skillW - 20f);
                GUI.Label(new Rect(20, sy, skillW - 20f, dh), sk.description, skillDescStyle);
                sy += dh + 8f;
            }

            GUI.EndScrollView();
        }

        void DrawCronsTab(float x, float contentY, float w, float contentH)
        {
            Rect area = new Rect(x + 10, contentY, w - 20, contentH);

            if (cronsLoading)
            {
                GUI.Label(new Rect(x + 20, contentY + 20, w - 40, 30),
                    "<color=#999999><i>Loading crons from gateway...</i></color>", labelStyle);
                return;
            }
            if (!string.IsNullOrEmpty(cronsError))
            {
                GUI.Label(new Rect(x + 20, contentY + 20, w - 40, 30),
                    $"<color=#ff6666>{cronsError}</color>", labelStyle);
                if (GUI.Button(new Rect(x + 20, contentY + 55, 100, 26), "Retry", buttonStyle))
                    FetchCronsFromGateway();
                return;
            }
            if (crons.Count == 0)
            {
                GUI.Label(new Rect(x + 20, contentY + 20, w - 40, 30),
                    "<color=#999999>No crons scheduled.</color>", labelStyle);
                if (GUI.Button(new Rect(x + 20, contentY + 55, 100, 26), "Refresh", buttonStyle))
                    FetchCronsFromGateway();
                return;
            }

            float totalH = 30f;
            foreach (var c in crons)
                totalH += 70f; // approx per cron
            if (totalH < contentH) totalH = contentH;

            cronsScrollPos = GUI.BeginScrollView(area, cronsScrollPos,
                new Rect(0, 0, w - 40, totalH));

            float cy = 4f;
            GUI.Label(new Rect(4, cy, w - 50, 20),
                $"<color=#70D8F0>{crons.Count} scheduled cron(s)</color>", cronNameStyle);
            cy += 26f;

            foreach (var c in crons)
            {
                string statusColor = c.status == "ok" || c.status == "active" ? "#4f4"
                    : c.status == "error" ? "#f44" : "#888";
                string dot = $"<color={statusColor}>\u25cf</color>";
                GUI.Label(new Rect(4, cy, w - 50, 20),
                    $"{dot} <b>{c.name}</b>", cronNameStyle);
                cy += 20f;

                string details = $"<color=#888>Schedule:</color> {c.schedule}";
                if (!string.IsNullOrEmpty(c.status))
                    details += $"  <color=#888>Status:</color> <color={statusColor}>{c.status}</color>";
                GUI.Label(new Rect(20, cy, w - 60, 18), details, cronDetailStyle);
                cy += 18f;

                string times = "";
                if (!string.IsNullOrEmpty(c.next))
                    times += $"<color=#888>Next:</color> {c.next}  ";
                if (!string.IsNullOrEmpty(c.last))
                    times += $"<color=#888>Last:</color> {c.last}";
                if (!string.IsNullOrEmpty(times))
                {
                    GUI.Label(new Rect(20, cy, w - 60, 18), times, cronDetailStyle);
                    cy += 18f;
                }
                cy += 8f;
            }

            GUI.EndScrollView();
        }
    }
}
