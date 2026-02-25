using System.Collections.Generic;
using UnityEngine;
using OpenClawWorlds.Gateway;
using OpenClawWorlds.Agents;
using OpenClawWorlds.World;

namespace OpenClawWorlds.UI
{
    /// <summary>
    /// Built-in chat UI for OpenClaw agents. Press Tab to open/close.
    ///
    /// Automatically finds the nearest NPC, acquires an agent via AgentPool,
    /// and lets you chat through the gateway. Uses IMGUI — no prefabs, no
    /// canvas setup, just add the component and it works.
    ///
    /// Replace this with your own UI for production games.
    /// </summary>
    public class OpenClawChatUI : MonoBehaviour
    {
        [Tooltip("Key to toggle the chat window")]
        public KeyCode toggleKey = KeyCode.Tab;

        bool chatOpen;
        string inputText = "";
        string statusText = "";
        bool waiting;
        Vector2 scrollPos;
        NPCData currentNPC;
        string currentAgentId;
        bool agentReady;

        struct ChatLine
        {
            public string speaker;
            public string text;
            public Color color;
        }

        readonly List<ChatLine> lines = new List<ChatLine>();

        // Styling
        GUIStyle boxStyle;
        GUIStyle labelStyle;
        GUIStyle inputStyle;
        GUIStyle buttonStyle;
        GUIStyle headerStyle;
        GUIStyle statusStyle;
        bool stylesReady;

        void Update()
        {
            // Enter to send when chat is open
            if (chatOpen && Input.GetKeyDown(KeyCode.Return) && !waiting)
            {
                string msg = inputText != null ? inputText.Trim() : "";
                if (!string.IsNullOrEmpty(msg))
                {
                    SendChat(msg);
                    inputText = "";
                }
            }
        }

        NPCData FindFirstNPC()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<NPCData>();
#else
            return FindObjectOfType<NPCData>();
#endif
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
                    // Fallback to main agent
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
            statusText = "Thinking...";

            if (!string.IsNullOrEmpty(currentAgentId))
            {
                // Send via NPC agent
                if (AgentPool.Instance != null)
                    AgentPool.Instance.TrackTask(currentAgentId, currentNPC?.npcName ?? "NPC", message);

                client.SendNPCMessage(currentAgentId, message,
                    onComplete: (response) => HandleResponse(response),
                    onError: (err) => HandleError(err)
                );
            }
            else
            {
                // Send via main agent
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

            lines.Add(new ChatLine
            {
                speaker = speaker,
                text = response,
                color = new Color(0.4f, 0.8f, 1f)
            });
            waiting = false;
            statusText = "";

            if (!string.IsNullOrEmpty(currentAgentId) && AgentPool.Instance != null)
                AgentPool.Instance.CompleteTask(currentAgentId, response);
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

            if (!string.IsNullOrEmpty(currentAgentId) && AgentPool.Instance != null)
                AgentPool.Instance.FailTask(currentAgentId, err);
        }

        // ─── IMGUI Rendering ────────────────────────────────────────

        void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.15f, 0.95f));

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
                Event.current.Use(); // consume the event

                if (chatOpen && currentNPC == null && !agentReady)
                {
                    currentNPC = FindFirstNPC();
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
                        lines.Add(new ChatLine
                        {
                            speaker = name,
                            text = "Hello! How can I help you?",
                            color = new Color(0.4f, 0.8f, 1f)
                        });
                        agentReady = true;
                    }
                }
                return;
            }

            // HUD when chat is closed
            if (!chatOpen)
            {
                var client = OpenClawClient.Instance;
                string connStatus = client != null && client.IsConnected
                    ? "<color=#66ff66>Connected to OpenClaw</color>"
                    : "<color=#ff6666>Connecting...</color>";

                var hintStyle = new GUIStyle(GUI.skin.label);
                hintStyle.fontSize = 16;
                hintStyle.richText = true;
                hintStyle.normal.textColor = Color.white;

                GUI.Label(new Rect(15, 15, 400, 30), connStatus, hintStyle);
                GUI.Label(new Rect(15, 40, 400, 30),
                    $"Press <b>{toggleKey}</b> to chat", hintStyle);
                return;
            }

            InitStyles();

            // Chat window
            float w = 520f;
            float h = 500f;
            float x = (Screen.width - w) / 2f;
            float y = (Screen.height - h) / 2f;

            GUI.Box(new Rect(x, y, w, h), "", boxStyle);

            // Header
            string agentName = currentNPC != null ? currentNPC.npcName
                : (AIConfig.Instance != null ? AIConfig.Instance.assistantName : "Agent");
            string title = $"Chat with {agentName}";
            GUI.Label(new Rect(x, y + 8, w, 30), title, headerStyle);

            // Close hint
            var closeStyle = new GUIStyle(GUI.skin.label);
            closeStyle.fontSize = 11;
            closeStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            closeStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(x, y + 8, w - 15, 30),
                $"{toggleKey} to close", closeStyle);

            // Status
            if (!string.IsNullOrEmpty(statusText))
                GUI.Label(new Rect(x, y + 35, w, 20), statusText, statusStyle);

            // Messages
            float msgY = y + 55;
            float msgH = h - 110;
            Rect scrollViewRect = new Rect(x + 10, msgY, w - 20, msgH);

            float contentHeight = 0;
            foreach (var line in lines)
            {
                string formatted = $"<b>{line.speaker}:</b> {line.text}";
                float lineH = labelStyle.CalcHeight(new GUIContent(formatted), w - 50);
                contentHeight += lineH + 6;
            }
            if (waiting) contentHeight += 25;

            Rect contentRect = new Rect(0, 0, w - 40,
                Mathf.Max(contentHeight, msgH));

            scrollPos = GUI.BeginScrollView(scrollViewRect, scrollPos, contentRect);

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

            // Auto-scroll
            if (contentHeight > msgH)
                scrollPos.y = contentHeight - msgH + 20;

            // Input
            float inputY = y + h - 45;
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

            GUI.FocusControl("ChatInput");
        }
    }
}
