using UnityEngine;

namespace OpenClawWorlds.Gateway
{
    /// <summary>
    /// Configuration for the OpenClaw gateway connection.
    /// Singleton — add to any GameObject in your scene.
    ///
    /// Token is loaded at runtime from StreamingAssets/ai_config.json:
    ///   { "gatewayToken": "your-token-here" }
    /// Falls back to inspector values if the file is missing.
    /// </summary>
    public class AIConfig : MonoBehaviour
    {
        public static AIConfig Instance { get; private set; }

        [Header("OpenClaw Gateway")]
        [Tooltip("WebSocket URL of the OpenClaw gateway")]
        public string gatewayWsUrl = "ws://127.0.0.1:18789";

        [Tooltip("Auth token — set in StreamingAssets/ai_config.json or the inspector")]
        public string gatewayToken = "";

        [Tooltip("Agent ID to talk to (e.g. 'my-agent')")]
        public string agentId = "";

        [Header("Assistant Identity")]
        [Tooltip("Name shown in chat UI")]
        public string assistantName = "Agent";

        [TextArea(3, 8)]
        [Tooltip("Personality description injected into agent identity")]
        public string personality = "";

        [TextArea(3, 8)]
        [Tooltip("System prompt for the main agent (legacy — use personality instead)")]
        public string systemPrompt =
            "You are an AI agent living inside a Unity game world. " +
            "You can build entire towns, change weather, spawn NPCs, and modify the world in real-time. " +
            "Keep responses concise unless asked for detail.";

        void Awake()
        {
            Instance = this;
            LoadConfigFile();
            Debug.Log($"[OpenClaw] Agent: {agentId}, gateway: {gatewayWsUrl}");
        }

        void LoadConfigFile()
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, "ai_config.json");
            if (!System.IO.File.Exists(path)) return;

            try
            {
                string json = System.IO.File.ReadAllText(path);
                var cfg = JsonUtility.FromJson<AIConfigFile>(json);
                if (!string.IsNullOrEmpty(cfg.gatewayToken))
                    gatewayToken = cfg.gatewayToken;
                if (!string.IsNullOrEmpty(cfg.gatewayWsUrl))
                    gatewayWsUrl = cfg.gatewayWsUrl;
                if (!string.IsNullOrEmpty(cfg.agentId))
                    agentId = cfg.agentId;
                if (!string.IsNullOrEmpty(cfg.assistantName))
                    assistantName = cfg.assistantName;
                if (!string.IsNullOrEmpty(cfg.personality))
                    personality = cfg.personality;
                if (!string.IsNullOrEmpty(cfg.systemPrompt))
                    systemPrompt = cfg.systemPrompt;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[OpenClaw] Failed to load ai_config.json: {e.Message}");
            }
        }

        [System.Serializable]
        struct AIConfigFile
        {
            public string gatewayToken;
            public string gatewayWsUrl;
            public string agentId;
            public string assistantName;
            public string personality;
            public string systemPrompt;
        }
    }
}
