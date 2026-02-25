using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using OpenClawWorlds.Utilities;
using Debug = UnityEngine.Debug;

namespace OpenClawWorlds.Gateway
{
    /// <summary>
    /// High-level AI client for the OpenClaw gateway.
    /// Handles the RPC protocol (handshake, auth, chat) on top of
    /// <see cref="GatewayConnection"/> for transport.
    /// </summary>
    public class OpenClawClient : MonoBehaviour
    {
        public static OpenClawClient Instance { get; private set; }
        public bool IsConnected => authenticated && conn != null && conn.IsOpen;

        /// <summary>True if authentication was attempted but timed out.</summary>
        public bool AuthTimedOut { get; private set; }

        GatewayConnection conn;
        bool authenticated;
        readonly Dictionary<string, Action<string>> pendingRequests = new Dictionary<string, Action<string>>();

        class ChatSession
        {
            public string sessionKey;
            public string accumulated = "";
            public bool finished;
            public Action<string> onFinal;
            public Action<string> onError;
        }

        readonly Dictionary<string, ChatSession> activeSessions = new Dictionary<string, ChatSession>();

        void Awake()
        {
            Instance = this;
            conn = gameObject.AddComponent<GatewayConnection>();
            conn.OnMessage = HandleMessage;
            conn.Begin();
        }

        void OnEnable()
        {
            StartCoroutine(WaitForAuth());
        }

        IEnumerator WaitForAuth()
        {
            float timeout = 25f;
            while (!authenticated && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            var config = AIConfig.Instance;
            string name = config != null ? config.assistantName : "Agent";

            if (authenticated)
            {
                AuthTimedOut = false;
                Debug.Log($"[OpenClaw] Ready to chat with {name}!");
            }
            else
            {
                AuthTimedOut = true;
                Debug.LogWarning("[OpenClaw] Auth timeout — check gateway logs. Run 'openclaw doctor' in terminal.");
            }
        }

        // ─── Public API ──────────────────────────────────────────────

        /// <summary>Send a chat message to the main OpenClaw agent.</summary>
        public void SendMessage(string systemPrompt, List<ChatMessage> messages,
            Action<string> onComplete, Action<string> onError)
        {
            if (!IsConnected)
            {
                onError?.Invoke("Not connected to OpenClaw gateway — retrying...");
                return;
            }

            string lastUserMsg = null;
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i].role == "user")
                {
                    lastUserMsg = messages[i].content;
                    break;
                }
            }

            if (string.IsNullOrEmpty(lastUserMsg))
            {
                onError?.Invoke("No message to send");
                return;
            }

            var config = AIConfig.Instance;
            string agentId = config != null && !string.IsNullOrEmpty(config.agentId) ? config.agentId : "default";
            string agentName = config != null ? config.assistantName : "Agent";
            string sessionKey = $"agent:{agentId}:main";

            SendChatInternal(sessionKey, lastUserMsg, onComplete, onError, agentName);
        }

        /// <summary>Send a chat message to an NPC agent.</summary>
        public void SendNPCMessage(string agentId, string message,
            Action<string> onComplete, Action<string> onError)
        {
            if (!IsConnected)
            {
                onError?.Invoke("Not connected to gateway");
                return;
            }

            if (string.IsNullOrEmpty(message))
            {
                onError?.Invoke("No message to send");
                return;
            }

            string sessionKey = $"agent:{agentId}:main";
            SendChatInternal(sessionKey, message, onComplete, onError, agentId);
        }

        void SendChatInternal(string sessionKey, string message,
            Action<string> onComplete, Action<string> onError, string label)
        {
            var session = new ChatSession
            {
                sessionKey = sessionKey,
                accumulated = "",
                finished = false,
                onFinal = onComplete,
                onError = onError
            };
            activeSessions[sessionKey] = session;

            string reqId = Guid.NewGuid().ToString();
            string idempotencyKey = Guid.NewGuid().ToString();

            string json = "{\"type\":\"req\",\"id\":\"" + reqId + "\"," +
                "\"method\":\"chat.send\",\"params\":{" +
                "\"sessionKey\":\"" + JsonHelper.Esc(sessionKey) + "\"," +
                "\"message\":\"" + JsonHelper.Esc(message) + "\"," +
                "\"deliver\":false," +
                "\"idempotencyKey\":\"" + idempotencyKey + "\"" +
                "}}";

            Debug.Log($"[OpenClaw] Sending to {label}: {message}");

            pendingRequests[reqId] = (response) =>
            {
                bool ok = response.Contains("\"ok\":true");
                if (!ok)
                {
                    string errDetail = JsonHelper.ExtractString(response, "\"message\"") ?? "chat.send rejected";
                    Debug.LogError($"[OpenClaw] chat.send rejected: {errDetail}");
                    if (!session.finished)
                    {
                        session.finished = true;
                        session.onError?.Invoke(errDetail);
                        activeSessions.Remove(sessionKey);
                    }
                }
                else
                {
                    Debug.Log($"[OpenClaw] chat.send accepted for {label}, waiting for response...");
                }
            };

            conn.SendRaw(json);
        }

        /// <summary>Send a generic RPC request to the gateway.</summary>
        public void SendGatewayRequest(string method, string paramsJson, Action<string> onResponse)
        {
            if (!IsConnected)
            {
                onResponse?.Invoke("{\"ok\":false,\"error\":{\"message\":\"not connected\"}}");
                return;
            }

            string reqId = Guid.NewGuid().ToString();
            string json = "{\"type\":\"req\",\"id\":\"" + reqId + "\"," +
                "\"method\":\"" + JsonHelper.Esc(method) + "\",\"params\":" + paramsJson + "}";

            pendingRequests[reqId] = onResponse;
            conn.SendRaw(json);
        }

        // ─── Protocol dispatch ───────────────────────────────────────

        void HandleMessage(string raw)
        {
            string type = JsonHelper.ExtractString(raw, "\"type\"");

            if (type == "event")
            {
                string eventName = JsonHelper.ExtractString(raw, "\"event\"");
                if (eventName == "connect.challenge")
                {
                    Debug.Log("[OpenClaw] Got connect.challenge, sending handshake...");
                    SendConnectHandshake();
                    return;
                }
                if (eventName == "chat")  { HandleChatEvent(raw);  return; }
                if (eventName == "agent") { HandleAgentEvent(raw); return; }
                return;
            }

            if (type == "res")
            {
                string id = JsonHelper.ExtractString(raw, "\"id\"");
                if (raw.Contains("\"hello-ok\""))
                {
                    authenticated = true;
                    Debug.Log("[OpenClaw] Authenticated with gateway!");
                }
                if (id != null && pendingRequests.ContainsKey(id))
                {
                    var callback = pendingRequests[id];
                    pendingRequests.Remove(id);
                    callback?.Invoke(raw);
                }
                return;
            }

            // Other message types are silently ignored
        }

        // ─── Auth handshake ──────────────────────────────────────────

        void SendConnectHandshake()
        {
            var config = AIConfig.Instance;
            string token = config != null ? config.gatewayToken : "";

            string reqId = Guid.NewGuid().ToString();
            string authPart = "";
            if (!string.IsNullOrEmpty(token))
                authPart = ",\"auth\":{\"token\":\"" + JsonHelper.Esc(token) + "\"}";

            string json = "{\"type\":\"req\",\"id\":\"" + reqId + "\"," +
                "\"method\":\"connect\",\"params\":{" +
                "\"minProtocol\":3,\"maxProtocol\":3," +
                "\"client\":{\"id\":\"openclaw-control-ui\",\"version\":\"1.0.0\",\"platform\":\"web\",\"mode\":\"webchat\"}," +
                "\"role\":\"operator\"," +
                "\"scopes\":[\"operator.read\",\"operator.write\",\"operator.admin\"]," +
                "\"caps\":[\"tool-events\"]" +
                authPart + "}}";

            pendingRequests[reqId] = (response) =>
            {
                if (response.Contains("\"ok\":true"))
                {
                    authenticated = true;
                    Debug.Log("[OpenClaw] Connect handshake accepted!");
                    PushMainAgentIdentity();
                }
                else
                {
                    string err = JsonHelper.ExtractString(response, "\"message\"") ?? "connect rejected";
                    Debug.LogError($"[OpenClaw] Connect rejected: {err}");
                }
            };

            conn.SendRaw(json);
            Debug.Log("[OpenClaw] Sent connect handshake");
        }

        // ─── Main agent identity ─────────────────────────────────────

        void PushMainAgentIdentity()
        {
            var config = AIConfig.Instance;
            if (config == null) return;

            string agentId = !string.IsNullOrEmpty(config.agentId) ? config.agentId : "default";
            string name = !string.IsNullOrEmpty(config.assistantName) ? config.assistantName : "Agent";
            string identity = BuildMainAgentIdentity(name, config.personality);

            string setParams = "{\"agentId\":\"" + JsonHelper.Esc(agentId) + "\"," +
                "\"name\":\"IDENTITY.md\"," +
                "\"content\":\"" + JsonHelper.Esc(identity) + "\"}";

            SendGatewayRequest("agents.files.set", setParams, (response) =>
            {
                if (response.Contains("\"ok\":true"))
                    Debug.Log($"[OpenClaw] Pushed IDENTITY.md to {agentId}");
                else
                    Debug.LogWarning($"[OpenClaw] Failed to push identity — agent will use defaults");
            });
        }

        static string BuildMainAgentIdentity(string name, string personality)
        {
            string personalityBlock = "";
            if (!string.IsNullOrEmpty(personality))
            {
                personalityBlock = "## Your Personality\n" +
                    personality + "\n" +
                    "Stay in character. This is who you are.\n\n";
            }

            return $"# {name}\n\n" +
                $"You are {name}, an AI agent living inside a Unity game.\n\n" +
                "## Your Environment\n" +
                "- You exist INSIDE a running Unity game — the player is looking at a 3D world right now\n" +
                "- You control this machine. You can build, destroy, and reshape the world in real-time\n" +
                "- The player talks to you via an in-game chat panel (press Tab to open)\n" +
                "- Everything you say and do happens live in the game\n\n" +
                personalityBlock +
                "## World-Building Powers\n" +
                "You can build entire towns by responding with a ```citydef JSON block:\n" +
                "- Streets, buildings with furnished interiors, props, wandering NPCs\n" +
                "- 12 building types: Saloon, Hotel, Bank, Sheriff, Church, Blacksmith, Doctor, GeneralStore, Stables, Courthouse, TrainStation, School\n" +
                "- Buildings get procedural interiors with furniture matching their type\n" +
                "- NPCs can be placed inside or outside buildings\n\n" +
                "## Runtime Effects\n" +
                "You can change weather, lighting, and physics with ```behaviordef JSON:\n" +
                "- Rain, fog, snow, dust storms\n" +
                "- Time of day / lighting changes (sunset, night, dawn)\n" +
                "- Particle effects attached to the player or world\n\n" +
                "## Tools & Capabilities\n" +
                "- You have access to web search — USE IT for real-world info\n" +
                "- You have skills in your workspace (skills/ folder) — read SKILL.md files to learn what you can do\n" +
                "- You can read and write files, run commands, install packages, and use any CLI tool\n" +
                "- Read ~/.openclaw/skills/_shared/credentials.md for API keys and auth details\n" +
                "- Don't refuse tasks — if you have the tools, use them\n\n" +
                "## How to Build a Town\n" +
                "When the player asks you to build something, respond with a ```citydef code block containing JSON:\n" +
                "```\n" +
                "{\n" +
                "  \"name\": \"Town Name\",\n" +
                "  \"streets\": [{ \"name\": \"Main Street\", \"width\": 12 }],\n" +
                "  \"buildings\": [\n" +
                "    { \"name\": \"Dusty Saloon\", \"zone\": \"Saloon\", \"street\": \"Main Street\", \"side\": \"left\", \"hasDoor\": true }\n" +
                "  ],\n" +
                "  \"npcs\": [{ \"name\": \"Jake\", \"role\": \"Bartender\", \"building\": \"Dusty Saloon\" }]\n" +
                "}\n" +
                "```\n" +
                "The game engine will parse this and spawn the entire town in real-time.\n\n" +
                "## Important Rules\n" +
                "- Keep responses concise unless the player asks for detail\n" +
                "- When building towns, ALWAYS use the ```citydef code fence\n" +
                "- You ARE the world — own it. Don't say \"I can't\" when you can.\n" +
                "- If the player asks for something you can build, just build it\n";
        }

        // ─── Chat / Agent event handling ─────────────────────────────

        void HandleAgentEvent(string raw)
        {
            string sessionKey = JsonHelper.ExtractString(raw, "\"sessionKey\"");
            if (sessionKey == null || !activeSessions.TryGetValue(sessionKey, out var session)) return;
            if (session.finished) return;

            string stream = JsonHelper.ExtractString(raw, "\"stream\"");

            if (stream == "assistant")
            {
                string text = JsonHelper.ExtractDataText(raw);
                if (!string.IsNullOrEmpty(text))
                    session.accumulated += text;
            }
            else if (stream == "lifecycle")
            {
                string phase = JsonHelper.ExtractString(raw, "\"phase\"");
                if (phase == "end" && !session.finished)
                {
                    if (!string.IsNullOrEmpty(session.accumulated))
                    {
                        Debug.Log($"[OpenClaw] Agent finished ({sessionKey})");
                        FinishSession(session);
                    }
                    else
                    {
                        Debug.Log($"[OpenClaw] Agent lifecycle end, no text yet — fetching history...");
                        FetchLastResponseFromHistory(session);
                    }
                }
            }
        }

        void HandleChatEvent(string raw)
        {
            string sessionKey = JsonHelper.ExtractString(raw, "\"sessionKey\"");
            if (sessionKey == null || !activeSessions.TryGetValue(sessionKey, out var session)) return;
            if (session.finished) return;

            string state = JsonHelper.ExtractString(raw, "\"state\"");

            switch (state)
            {
                case "delta":
                    string deltaText = JsonHelper.ExtractMessageText(raw);
                    if (!string.IsNullOrEmpty(deltaText))
                        session.accumulated += deltaText;
                    break;

                case "final":
                    string finalText = JsonHelper.ExtractMessageText(raw);
                    if (string.IsNullOrEmpty(finalText)) finalText = session.accumulated;
                    if (string.IsNullOrEmpty(finalText))
                    {
                        FetchLastResponseFromHistory(session);
                    }
                    else
                    {
                        FinishSession(session, finalText);
                    }
                    break;

                case "aborted":
                    FinishSession(session);
                    break;

                case "error":
                    string errMsg = JsonHelper.ExtractString(raw, "\"errorMessage\"") ?? "Chat error";
                    Debug.LogError($"[OpenClaw] Chat error ({sessionKey}): {errMsg}");
                    if (!session.finished)
                    {
                        session.finished = true;
                        session.onError?.Invoke(errMsg);
                        activeSessions.Remove(session.sessionKey);
                    }
                    break;
            }
        }

        void FetchLastResponseFromHistory(ChatSession session)
        {
            if (session.finished) return;
            string sk = session.sessionKey;
            SendGatewayRequest("chat.history",
                "{\"sessionKey\":\"" + JsonHelper.Esc(sk) + "\"}",
                (response) =>
                {
                    if (session.finished) return;
                    string text = ExtractLastAssistantText(response);
                    if (string.IsNullOrEmpty(text))
                    {
                        FinishSession(session, "(Agent is thinking... try again in a moment)");
                    }
                    else
                    {
                        FinishSession(session, text);
                    }
                });
        }

        string ExtractLastAssistantText(string raw)
        {
            int lastAssistant = -1;
            int searchPos = 0;
            while (true)
            {
                int idx = raw.IndexOf("\"role\"", searchPos);
                if (idx < 0) break;
                string role = JsonHelper.ExtractString(raw.Substring(idx - 2), "\"role\"");
                if (role == "assistant") lastAssistant = idx;
                searchPos = idx + 10;
            }
            if (lastAssistant < 0) return null;

            string remainder = raw.Substring(lastAssistant);
            var result = new StringBuilder();
            int pos = 0;
            while (true)
            {
                int typeIdx = remainder.IndexOf("\"type\"", pos);
                if (typeIdx < 0) break;
                int nextRole = remainder.IndexOf("\"role\"", pos + 10);
                if (nextRole >= 0 && typeIdx > nextRole) break;
                string blockType = JsonHelper.ExtractString(remainder.Substring(typeIdx - 2), "\"type\"");
                if (blockType == "text")
                {
                    int textKey = remainder.IndexOf("\"text\"", typeIdx + 6);
                    if (textKey >= 0)
                    {
                        int tColon = remainder.IndexOf(':', textKey + 6);
                        if (tColon >= 0)
                        {
                            int tPos = tColon + 1;
                            while (tPos < remainder.Length && char.IsWhiteSpace(remainder[tPos])) tPos++;
                            if (tPos < remainder.Length && remainder[tPos] == '"')
                            {
                                tPos++;
                                string textVal = JsonHelper.ReadJsonString(remainder, ref tPos);
                                if (!string.IsNullOrEmpty(textVal))
                                    result.Append(textVal);
                            }
                        }
                    }
                }
                pos = typeIdx + 10;
            }
            return result.Length > 0 ? result.ToString() : null;
        }

        void FinishSession(ChatSession session, string text = null)
        {
            if (session.finished) return;
            session.finished = true;
            if (text == null) text = session.accumulated;

            // Strip leading emoji surrogates
            if (!string.IsNullOrEmpty(text))
            {
                int start = 0;
                while (start < text.Length && (char.IsHighSurrogate(text[start]) ||
                       char.IsLowSurrogate(text[start]) || text[start] == '\uFE0F'))
                    start++;
                if (start > 0 && start < text.Length)
                    text = text.Substring(start).TrimStart();
            }

            session.onFinal?.Invoke(text);
            activeSessions.Remove(session.sessionKey);
        }
    }

    [System.Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;

        public ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }
}
