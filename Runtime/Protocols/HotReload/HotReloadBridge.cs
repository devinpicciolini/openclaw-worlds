using UnityEngine;
using System;
using System.IO;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OpenClawWorlds.Protocols
{
    /// <summary>
    /// Hot-reload bridge: detects ```csharp code blocks in AI responses,
    /// writes them to the project's Assets/Scripts/Generated/ folder,
    /// and triggers Unity's domain reload so the new code takes effect immediately.
    ///
    /// Pipeline:
    ///   1. Player asks an agent for something ("add double-jump", "create a weather system")
    ///   2. Agent responds with a ```csharp code block
    ///   3. This bridge writes the C# file to disk
    ///   4. Unity detects the change → recompiles → new behavior appears
    ///
    /// Works in Unity Editor (auto domain reload).
    /// In builds, code blocks are written but not compiled (would need Roslyn runtime).
    /// </summary>
    public static class HotReloadBridge
    {
        /// <summary>Fired when a script is written. Args: (fileName, code, summary).</summary>
        public static event Action<string, string, string> OnScriptWritten;

        /// <summary>Fired when reload is triggered.</summary>
        public static event Action OnReloadTriggered;

        /// <summary>
        /// Override where generated scripts are written. Defaults to Assets/Scripts/Generated/.
        /// </summary>
        public static string GeneratedFolderOverride { get; set; }

        static string GeneratedFolder =>
            !string.IsNullOrEmpty(GeneratedFolderOverride)
                ? GeneratedFolderOverride
                : Path.Combine(Application.dataPath, "Scripts", "Generated");

        /// <summary>
        /// Check an AI response for ```csharp code blocks.
        /// If found, extract each one, write to disk, and trigger reload.
        /// Returns a display-friendly summary, or null if no code blocks found.
        /// </summary>
        public static string ProcessResponse(string response)
        {
            var blocks = ExtractCSharpBlocks(response);
            if (blocks == null || blocks.Length == 0)
                return null;

#if !UNITY_EDITOR
            // In builds, code blocks cannot be compiled — skip file writes entirely
            Debug.Log("[HotReload] C# code blocks detected but cannot compile in builds.");
            return $"Detected {blocks.Length} code block(s) — live reload only works in the Unity Editor.";
#else
            if (!Directory.Exists(GeneratedFolder))
            {
                Directory.CreateDirectory(GeneratedFolder);
                Debug.Log($"[HotReload] Created generated scripts folder: {GeneratedFolder}");
            }

            string summary = "";
            foreach (var block in blocks)
            {
                string fileName = InferFileName(block);
                string filePath = Path.Combine(GeneratedFolder, fileName);

                try
                {
                    File.WriteAllText(filePath, block);
                    Debug.Log($"[HotReload] Wrote {fileName} ({block.Length} chars) to {filePath}");

                    string shortSummary = $"Wrote {fileName} ({block.Split('\n').Length} lines)";
                    summary += (summary.Length > 0 ? "\n" : "") + shortSummary;

                    OnScriptWritten?.Invoke(fileName, block, shortSummary);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[HotReload] Failed to write {fileName}: {e.Message}");
                    summary += $"\n[Error writing {fileName}: {e.Message}]";
                }
            }

            TriggerReload();
            return summary;
#endif
        }

        static string[] ExtractCSharpBlocks(string response)
        {
            var results = new System.Collections.Generic.List<string>();

            int searchFrom = 0;
            while (searchFrom < response.Length)
            {
                int fenceStart = response.IndexOf("```csharp", searchFrom, StringComparison.OrdinalIgnoreCase);
                if (fenceStart < 0)
                    fenceStart = response.IndexOf("```cs\n", searchFrom, StringComparison.OrdinalIgnoreCase);
                if (fenceStart < 0)
                    break;

                int codeStart = response.IndexOf('\n', fenceStart);
                if (codeStart < 0) break;
                codeStart++;

                int fenceEnd = response.IndexOf("```", codeStart);
                if (fenceEnd < 0) break;

                string code = response.Substring(codeStart, fenceEnd - codeStart).Trim();
                if (code.Length > 10)
                    results.Add(code);

                searchFrom = fenceEnd + 3;
            }

            return results.Count > 0 ? results.ToArray() : null;
        }

        static string InferFileName(string code)
        {
            var match = Regex.Match(code, @"(?:class|struct|enum)\s+(\w+)");
            if (match.Success)
                return match.Groups[1].Value + ".cs";

            var nsMatch = Regex.Match(code, @"namespace\s+[\w.]+\s*\{[^}]*?(?:class|struct)\s+(\w+)");
            if (nsMatch.Success)
                return nsMatch.Groups[1].Value + ".cs";

            return $"Generated_{DateTime.Now:yyyyMMdd_HHmmss}.cs";
        }

        static void TriggerReload()
        {
#if UNITY_EDITOR
            Debug.Log("[HotReload] Triggering AssetDatabase.Refresh()...");
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                Debug.Log("[HotReload] Domain reload triggered — Unity is recompiling...");
            };
            OnReloadTriggered?.Invoke();
#else
            Debug.Log("[HotReload] Not in Editor — code was written but won't auto-compile.");
            Debug.Log("[HotReload] In a production build, you'd need Roslyn runtime compilation.");
#endif
        }

        /// <summary>List all generated scripts currently on disk.</summary>
        public static string[] GetGeneratedScripts()
        {
            if (!Directory.Exists(GeneratedFolder))
                return Array.Empty<string>();
            return Directory.GetFiles(GeneratedFolder, "*.cs");
        }

        /// <summary>Delete all generated scripts (reset to clean state).</summary>
        public static void ClearGenerated()
        {
            if (!Directory.Exists(GeneratedFolder)) return;

            foreach (var file in Directory.GetFiles(GeneratedFolder, "*.cs"))
            {
                try { File.Delete(file); }
                catch (Exception e) { Debug.LogWarning($"[HotReload] Could not delete {file}: {e.Message}"); }
            }

            Debug.Log("[HotReload] Cleared all generated scripts.");
            TriggerReload();
        }
    }
}
