using System.Text;

namespace OpenClawWorlds.Utilities
{
    /// <summary>
    /// Lightweight hand-rolled JSON helpers for the gateway protocol.
    /// Avoids pulling in a JSON library for a handful of field lookups.
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>Extract the first string value for a given key (e.g. "\"type\"").</summary>
        public static string ExtractString(string json, string key)
        {
            int idx = json.IndexOf(key);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + key.Length);
            if (colon < 0) return null;
            int quote1 = json.IndexOf('"', colon + 1);
            if (quote1 < 0) return null;
            int quote2 = FindClosingQuote(json, quote1 + 1);
            if (quote2 < 0) return null;
            return json.Substring(quote1 + 1, quote2 - quote1 - 1);
        }

        /// <summary>Extract nested "data" → "text" from agent stream events.</summary>
        public static string ExtractDataText(string raw)
        {
            int dataIdx = raw.IndexOf("\"data\"");
            if (dataIdx < 0) return null;
            int textIdx = raw.IndexOf("\"text\"", dataIdx);
            if (textIdx < 0) return null;
            int colonIdx = raw.IndexOf(':', textIdx + 6);
            if (colonIdx < 0) return null;
            int pos = colonIdx + 1;
            while (pos < raw.Length && char.IsWhiteSpace(raw[pos])) pos++;
            if (pos >= raw.Length || raw[pos] != '"') return null;
            pos++;
            return ReadJsonString(raw, ref pos);
        }

        /// <summary>
        /// Extract "message" → "content" which may be a string or a content-block array.
        /// </summary>
        public static string ExtractMessageText(string raw)
        {
            int msgIdx = raw.IndexOf("\"message\"");
            if (msgIdx < 0) return null;
            int contentIdx = raw.IndexOf("\"content\"", msgIdx);
            if (contentIdx < 0) return null;
            int colonIdx = raw.IndexOf(':', contentIdx + 9);
            if (colonIdx < 0) return null;
            int pos = colonIdx + 1;
            while (pos < raw.Length && char.IsWhiteSpace(raw[pos])) pos++;
            if (pos >= raw.Length) return null;

            if (raw[pos] == '"')
            {
                pos++;
                return ReadJsonString(raw, ref pos);
            }

            if (raw[pos] == '[')
            {
                var result = new StringBuilder();
                int searchPos = pos;
                int closeBracket = FindMatchingBracket(raw, pos);
                while (searchPos < closeBracket)
                {
                    int typeIdx = raw.IndexOf("\"type\"", searchPos);
                    if (typeIdx < 0 || typeIdx > closeBracket) break;
                    string blockType = ExtractString(raw.Substring(typeIdx - 2), "\"type\"");
                    if (blockType == "text")
                    {
                        int textKeyIdx = raw.IndexOf("\"text\"", typeIdx + 6);
                        if (textKeyIdx >= 0 && textKeyIdx < closeBracket)
                        {
                            int tColon = raw.IndexOf(':', textKeyIdx + 6);
                            if (tColon >= 0)
                            {
                                int tPos = tColon + 1;
                                while (tPos < raw.Length && char.IsWhiteSpace(raw[tPos])) tPos++;
                                if (tPos < raw.Length && raw[tPos] == '"')
                                {
                                    tPos++;
                                    result.Append(ReadJsonString(raw, ref tPos));
                                }
                                searchPos = tPos;
                                continue;
                            }
                        }
                    }
                    searchPos = typeIdx + 10;
                }
                return result.Length > 0 ? result.ToString() : null;
            }

            return null;
        }

        /// <summary>Escape a string for embedding inside a JSON value.</summary>
        public static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        // ─── Internal helpers ────────────────────────────────────────

        public static string ReadJsonString(string json, ref int pos)
        {
            var sb = new StringBuilder();
            while (pos < json.Length)
            {
                char c = json[pos];
                if (c == '\\' && pos + 1 < json.Length)
                {
                    char n = json[pos + 1];
                    switch (n)
                    {
                        case '"': sb.Append('"'); pos += 2; continue;
                        case '\\': sb.Append('\\'); pos += 2; continue;
                        case 'n': sb.Append('\n'); pos += 2; continue;
                        case 'r': sb.Append('\r'); pos += 2; continue;
                        case 't': sb.Append('\t'); pos += 2; continue;
                        case '/': sb.Append('/'); pos += 2; continue;
                        case 'u':
                            if (pos + 5 < json.Length)
                            {
                                string hex = json.Substring(pos + 2, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int cp))
                                    sb.Append((char)cp);
                                pos += 6;
                                continue;
                            }
                            break;
                        default: sb.Append(c); pos++; continue;
                    }
                }
                if (c == '"') { pos++; break; }
                sb.Append(c);
                pos++;
            }
            return sb.ToString();
        }

        static int FindClosingQuote(string json, int start)
        {
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '\\') { i++; continue; }
                if (json[i] == '"') return i;
            }
            return -1;
        }

        static int FindMatchingBracket(string json, int openPos)
        {
            int depth = 0;
            for (int i = openPos; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']') { depth--; if (depth == 0) return i; }
                else if (json[i] == '"') { i = FindClosingQuote(json, i + 1); if (i < 0) return json.Length; }
            }
            return json.Length;
        }
    }
}
