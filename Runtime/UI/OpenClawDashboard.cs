using System.Collections.Generic;
using UnityEngine;
using OpenClawWorlds.Gateway;
using OpenClawWorlds.Agents;

namespace OpenClawWorlds.UI
{
    /// <summary>
    /// Dashboard overlay showing active NPC agents, their tasks, available skills,
    /// and system status. Toggle with backtick (`) key.
    ///
    /// Sits on the LEFT side of screen (ChatUI is on the RIGHT).
    /// Both can be open simultaneously.
    ///
    /// Uses IMGUI — no prefabs, no canvas setup, just add the component and it works.
    /// </summary>
    public class OpenClawDashboard : MonoBehaviour
    {
        public static OpenClawDashboard Instance { get; private set; }
        public bool IsOpen => isOpen;

        [Tooltip("Key to toggle the dashboard")]
        public KeyCode toggleKey = KeyCode.BackQuote;

        bool isOpen;
        Vector2 agentsScroll;
        Vector2 skillsScroll;

        // Tabs: 0=Agents, 1=Skills, 2=System
        int activeTab;
        static readonly string[] TabNames = { "Agents", "Skills", "System" };

        // Skills (read from disk, cached)
        List<string> skillNames = new List<string>();
        bool skillsScanned;

        // Styles
        bool stylesInit;
        Texture2D panelBg, headerBg, rowBg, rowAltBg;
        GUIStyle titleStyle, closeStyle, hintStyle;
        GUIStyle tabActiveStyle, tabInactiveStyle;
        GUIStyle sectionStyle, labelStyle, valueStyle, statusStyle;
        GUIStyle agentNameStyle, agentTaskStyle, agentResponseStyle;
        GUIStyle skillItemStyle;

        void Awake() { Instance = this; }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (panelBg != null) Destroy(panelBg);
            if (headerBg != null) Destroy(headerBg);
            if (rowBg != null) Destroy(rowBg);
            if (rowAltBg != null) Destroy(rowAltBg);
        }

        void Open()
        {
            isOpen = true;
            if (!skillsScanned) ScanSkills();
        }

        void Close()
        {
            isOpen = false;
        }

        // ---- Skills — scan from disk ----

        void ScanSkills()
        {
            skillsScanned = true;
            skillNames.Clear();

            try
            {
                string home = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.UserProfile);
                string skillsDir = System.IO.Path.Combine(home, ".openclaw", "skills");

                if (System.IO.Directory.Exists(skillsDir))
                {
                    foreach (string dir in System.IO.Directory.GetDirectories(skillsDir))
                    {
                        string name = System.IO.Path.GetFileName(dir);
                        if (name.StartsWith("_")) continue; // skip _shared
                        skillNames.Add(name);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[OpenClawDashboard] Failed to scan skills: {ex.Message}");
            }
        }

        // ---- GUI ----

        Texture2D MakeTex(int w, int h, Color col)
        {
            var t = new Texture2D(w, h);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = col;
            t.SetPixels(px); t.Apply();
            return t;
        }

        void InitStyles()
        {
            if (stylesInit) return;
            stylesInit = true;

            panelBg  = MakeTex(1, 1, new Color(0.07f, 0.09f, 0.13f, 0.94f));
            headerBg = MakeTex(1, 1, new Color(0.11f, 0.13f, 0.19f, 0.96f));
            rowBg    = MakeTex(1, 1, new Color(0.10f, 0.12f, 0.17f, 0.6f));
            rowAltBg = MakeTex(1, 1, new Color(0.13f, 0.15f, 0.20f, 0.6f));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(0.85f, 0.78f, 0.6f);

            closeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            closeStyle.normal.background = MakeTex(1, 1, new Color(0.5f, 0.2f, 0.15f, 0.8f));
            closeStyle.hover.background  = MakeTex(1, 1, new Color(0.65f, 0.25f, 0.18f, 0.9f));
            closeStyle.normal.textColor  = new Color(0.9f, 0.85f, 0.8f);

            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, alignment = TextAnchor.MiddleLeft, richText = true
            };
            hintStyle.normal.textColor = new Color(0.6f, 0.6f, 0.5f, 0.5f);

            tabActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            tabActiveStyle.normal.background = MakeTex(1, 1, new Color(0.18f, 0.22f, 0.30f, 0.9f));
            tabActiveStyle.normal.textColor  = new Color(0.95f, 0.90f, 0.75f);

            tabInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter
            };
            tabInactiveStyle.normal.background = MakeTex(1, 1, new Color(0.10f, 0.12f, 0.17f, 0.7f));
            tabInactiveStyle.normal.textColor  = new Color(0.50f, 0.50f, 0.45f);
            tabInactiveStyle.hover.textColor   = new Color(0.70f, 0.68f, 0.60f);

            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, richText = true
            };
            sectionStyle.normal.textColor = new Color(0.75f, 0.70f, 0.55f);

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, richText = true
            };
            labelStyle.normal.textColor = new Color(0.65f, 0.65f, 0.60f);

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, richText = true
            };
            valueStyle.normal.textColor = new Color(0.85f, 0.85f, 0.80f);

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, richText = true, alignment = TextAnchor.MiddleRight
            };
            statusStyle.normal.textColor = new Color(0.65f, 0.65f, 0.60f);

            agentNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, richText = true
            };
            agentNameStyle.normal.textColor = new Color(0.90f, 0.85f, 0.70f);

            agentTaskStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, wordWrap = true, richText = true
            };
            agentTaskStyle.normal.textColor = new Color(0.70f, 0.70f, 0.65f);
            agentTaskStyle.padding = new RectOffset(16, 4, 0, 0);

            agentResponseStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, wordWrap = true, richText = true
            };
            agentResponseStyle.normal.textColor = new Color(0.60f, 0.75f, 0.60f);
            agentResponseStyle.padding = new RectOffset(16, 4, 0, 0);

            skillItemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, richText = true
            };
            skillItemStyle.normal.textColor = new Color(0.80f, 0.80f, 0.75f);
        }

        const float PanelW  = 380f;
        const float HeaderH = 36f;
        const float TabH    = 26f;
        const float Pad     = 8f;

        void OnGUI()
        {
            // Toggle key
            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == toggleKey)
            {
                if (isOpen) Close();
                else Open();
                Event.current.Use();
                return;
            }

            // ESC closes dashboard
            if (isOpen && Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
                return;
            }

            // Bottom-left hint when closed
            if (!isOpen)
            {
                InitStyles();
                var pool = AgentPool.Instance;
                int working = pool != null ? pool.WorkingCount : 0;
                string badge = working > 0 ? $"  <color=#FFD700>({working} working)</color>" : "";
                GUI.Label(new Rect(10, Screen.height - 30, 300, 25),
                    $"[`] Dashboard{badge}", hintStyle);
                return;
            }

            InitStyles();

            float pw = PanelW;
            float ph = Mathf.Min(Screen.height - 40f, 560f);
            float px = 10f;
            float py = (Screen.height - ph) / 2f;

            // Background
            GUI.DrawTexture(new Rect(px, py, pw, ph), panelBg);

            // Header
            GUI.DrawTexture(new Rect(px, py, pw, HeaderH), headerBg);
            GUI.Label(new Rect(px + Pad, py + 2, pw - 80, HeaderH - 4),
                "Agent Dashboard", titleStyle);
            if (GUI.Button(new Rect(px + pw - 54, py + 6, 46, HeaderH - 12), "X", closeStyle))
                Close();

            // Tab bar
            float tabY = py + HeaderH;
            float tabW = (pw - Pad * 2) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                GUIStyle ts = (i == activeTab) ? tabActiveStyle : tabInactiveStyle;
                string tabLabel = TabNames[i];
                if (i == 0)
                {
                    var pool = AgentPool.Instance;
                    int working = pool != null ? pool.WorkingCount : 0;
                    if (working > 0) tabLabel += $" ({working})";
                }
                if (GUI.Button(new Rect(px + Pad + i * tabW, tabY, tabW, TabH), tabLabel, ts))
                    activeTab = i;
            }

            // Content
            float contentY = tabY + TabH + 4f;
            float contentH = ph - HeaderH - TabH - Pad - 4f;

            switch (activeTab)
            {
                case 0: DrawAgentsTab(px + Pad, contentY, pw - Pad * 2, contentH); break;
                case 1: DrawSkillsTab(px + Pad, contentY, pw - Pad * 2, contentH); break;
                case 2: DrawSystemTab(px + Pad, contentY, pw - Pad * 2, contentH); break;
            }

            GUI.Label(new Rect(px, py + ph + 2, pw, 18f), "` to toggle   Esc to close", hintStyle);
        }

        // ---- AGENTS TAB ----

        void DrawAgentsTab(float cx, float cy, float cw, float ch)
        {
            var pool = AgentPool.Instance;
            if (pool == null || pool.Tasks.Count == 0)
            {
                GUI.Label(new Rect(cx + 12, cy + 20, cw - 24, 40),
                    "No agents active yet.\n\nTalk to an NPC and give them a task.",
                    agentTaskStyle);
                return;
            }

            float totalH = 8f;
            float rowW = cw - 20f;
            foreach (var kvp in pool.Tasks)
            {
                totalH += 24f;
                if (!string.IsNullOrEmpty(kvp.Value.lastMessage))
                {
                    string taskText = Truncate(kvp.Value.lastMessage, 120);
                    totalH += agentTaskStyle.CalcHeight(new GUIContent(taskText), rowW - 20f) + 2f;
                }
                if (!string.IsNullOrEmpty(kvp.Value.lastResponse))
                {
                    string respText = "> " + Truncate(kvp.Value.lastResponse, 150);
                    totalH += agentResponseStyle.CalcHeight(new GUIContent(respText), rowW - 20f) + 2f;
                }
                totalH += 10f;
            }
            if (totalH < ch) totalH = ch;

            agentsScroll = GUI.BeginScrollView(new Rect(cx, cy, cw, ch), agentsScroll,
                new Rect(0, 0, cw - 16f, totalH));

            float y = 4f;
            int idx = 0;
            foreach (var kvp in pool.Tasks)
            {
                var task = kvp.Value;

                float rowH = CalculateAgentRowHeight(task, rowW);
                GUI.DrawTexture(new Rect(0, y - 2, cw - 16f, rowH + 4),
                    idx % 2 == 0 ? rowBg : rowAltBg);

                string statusColor;
                string statusLabel;
                switch (task.status)
                {
                    case "working":
                        statusColor = "#FFD700";
                        statusLabel = "working...";
                        break;
                    case "done":
                        statusColor = "#66CC66";
                        statusLabel = "done";
                        break;
                    case "error":
                        statusColor = "#FF6644";
                        statusLabel = "error";
                        break;
                    default:
                        statusColor = "#888888";
                        statusLabel = "idle";
                        break;
                }

                string emoji = !string.IsNullOrEmpty(task.emoji) ? task.emoji + " " : "";
                GUI.Label(new Rect(4, y, rowW - 100f, 22f),
                    $"{emoji}<b>{task.npcName}</b>", agentNameStyle);
                GUI.Label(new Rect(rowW - 100f, y, 100f, 22f),
                    $"<color={statusColor}>{statusLabel}</color>", statusStyle);
                y += 22f;

                if (!string.IsNullOrEmpty(task.lastMessage))
                {
                    string taskText = Truncate(task.lastMessage, 120);
                    float th = agentTaskStyle.CalcHeight(new GUIContent(taskText), rowW - 20f);
                    GUI.Label(new Rect(4, y, rowW - 20f, th), taskText, agentTaskStyle);
                    y += th + 2f;
                }

                if (!string.IsNullOrEmpty(task.lastResponse))
                {
                    string respText = "> " + Truncate(task.lastResponse, 150);
                    float rh = agentResponseStyle.CalcHeight(new GUIContent(respText), rowW - 20f);
                    GUI.Label(new Rect(4, y, rowW - 20f, rh), respText, agentResponseStyle);
                    y += rh + 2f;
                }

                float elapsed = Time.time - task.timestamp;
                string timeAgo = elapsed < 60f  ? $"{elapsed:F0}s ago"
                    : elapsed < 3600f ? $"{elapsed / 60f:F0}m ago"
                    : $"{elapsed / 3600f:F1}h ago";
                GUI.Label(new Rect(4, y, rowW, 16f),
                    $"<color=#555555>{timeAgo}</color>", labelStyle);
                y += 18f;
                y += 6f;
                idx++;
            }

            GUI.EndScrollView();
        }

        float CalculateAgentRowHeight(AgentTask task, float rowW)
        {
            float h = 22f;
            if (!string.IsNullOrEmpty(task.lastMessage))
                h += agentTaskStyle.CalcHeight(
                    new GUIContent(Truncate(task.lastMessage, 120)), rowW - 20f) + 2f;
            if (!string.IsNullOrEmpty(task.lastResponse))
                h += agentResponseStyle.CalcHeight(
                    new GUIContent("> " + Truncate(task.lastResponse, 150)), rowW - 20f) + 2f;
            h += 18f;
            return h;
        }

        // ---- SKILLS TAB ----

        void DrawSkillsTab(float cx, float cy, float cw, float ch)
        {
            if (skillNames.Count == 0)
            {
                GUI.Label(new Rect(cx + 12, cy + 20, cw - 24, 30),
                    "No skills found in ~/.openclaw/skills/", agentTaskStyle);
                if (GUI.Button(new Rect(cx + 12, cy + 50, 100, 24), "Rescan", tabActiveStyle))
                    ScanSkills();
                return;
            }

            float totalH = 8f + skillNames.Count * 28f + 30f;
            if (totalH < ch) totalH = ch;

            skillsScroll = GUI.BeginScrollView(new Rect(cx, cy, cw, ch), skillsScroll,
                new Rect(0, 0, cw - 16f, totalH));

            float y = 4f;
            GUI.Label(new Rect(4, y, cw - 24f, 22f),
                $"<color=#FFD700>{skillNames.Count} skills available</color>", sectionStyle);
            y += 26f;

            foreach (string skill in skillNames)
            {
                string display = FormatSkillName(skill);
                string icon = SkillIcon(skill);
                GUI.Label(new Rect(8, y, cw - 32f, 24f), $"{icon}  {display}", skillItemStyle);
                y += 26f;
            }

            GUI.EndScrollView();
        }

        string FormatSkillName(string slug)
        {
            var parts = slug.Split('-');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        string SkillIcon(string name)
        {
            if (name.Contains("google"))   return "[G]";
            if (name.Contains("github"))   return "[GH]";
            if (name.Contains("vercel"))   return "[V]";
            if (name.Contains("research")) return "[R]";
            if (name.Contains("deploy"))   return "[D]";
            return "[S]";
        }

        // ---- SYSTEM TAB ----

        void DrawSystemTab(float cx, float cy, float cw, float ch)
        {
            float y = cy + 8f;
            float lw = 130f;
            float vw = cw - lw - 16f;

            // Gateway
            GUI.Label(new Rect(cx + 4, y, cw - 8f, 22f), "GATEWAY", sectionStyle);
            y += 26f;

            var client = OpenClawClient.Instance;
            bool connected = client != null && client.IsConnected;
            string connStatus = connected
                ? "<color=#66CC66>Connected</color>"
                : "<color=#FF6644>Disconnected</color>";

            GUI.Label(new Rect(cx + 8, y, lw, 20f), "Status:", labelStyle);
            GUI.Label(new Rect(cx + lw, y, vw, 20f), connStatus, valueStyle);
            y += 22f;

            var config = AIConfig.Instance;
            string wsUrl = config != null ? config.gatewayWsUrl : "ws://127.0.0.1:18789";
            GUI.Label(new Rect(cx + 8, y, lw, 20f), "Endpoint:", labelStyle);
            GUI.Label(new Rect(cx + lw, y, vw, 20f), wsUrl, valueStyle);
            y += 22f;

            string agentId = config != null ? config.agentId : "default";
            GUI.Label(new Rect(cx + 8, y, lw, 20f), "Main Agent:", labelStyle);
            GUI.Label(new Rect(cx + lw, y, vw, 20f), agentId, valueStyle);
            y += 30f;

            // Agent Pool
            GUI.Label(new Rect(cx + 4, y, cw - 8f, 22f), "AGENT POOL", sectionStyle);
            y += 26f;

            var pool = AgentPool.Instance;
            int total = pool != null ? pool.Tasks.Count : 0;
            int working = pool != null ? pool.WorkingCount : 0;

            GUI.Label(new Rect(cx + 8, y, lw, 20f), "Total agents:", labelStyle);
            GUI.Label(new Rect(cx + lw, y, vw, 20f), total.ToString(), valueStyle);
            y += 22f;

            GUI.Label(new Rect(cx + 8, y, lw, 20f), "Working:", labelStyle);
            string workColor = working > 0 ? "#FFD700" : "#888888";
            GUI.Label(new Rect(cx + lw, y, vw, 20f),
                $"<color={workColor}>{working}</color>", valueStyle);
            y += 22f;

            GUI.Label(new Rect(cx + 8, y, lw, 20f), "Architecture:", labelStyle);
            GUI.Label(new Rect(cx + lw, y, vw, 20f), "Single active slot", valueStyle);
            y += 30f;

            // Memory
            GUI.Label(new Rect(cx + 4, y, cw - 8f, 22f), "MEMORY", sectionStyle);
            y += 26f;

            GUI.Label(new Rect(cx + 8, y, lw, 20f), "Vault:", labelStyle);
            GUI.Label(new Rect(cx + lw, y, vw, 20f), "~/.openclaw/npc-memories/", valueStyle);
            y += 22f;

            int memFiles = 0;
            try
            {
                string home = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.UserProfile);
                string memDir = System.IO.Path.Combine(home, ".openclaw", "npc-memories");
                if (System.IO.Directory.Exists(memDir))
                    memFiles = System.IO.Directory.GetFiles(memDir, "*.md").Length;
            }
            catch { }

            GUI.Label(new Rect(cx + 8, y, lw, 20f), "NPC memories:", labelStyle);
            GUI.Label(new Rect(cx + lw, y, vw, 20f), $"{memFiles} files", valueStyle);
            y += 22f;

            GUI.Label(new Rect(cx + 8, y, lw, 20f), "Skills loaded:", labelStyle);
            GUI.Label(new Rect(cx + lw, y, vw, 20f), $"{skillNames.Count}", valueStyle);
        }

        // ---- HELPERS ----

        static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\n", " ").Replace("\r", "");
            if (text.Length <= max) return text;
            return text.Substring(0, max - 3) + "...";
        }
    }
}
