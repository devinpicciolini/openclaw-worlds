using UnityEngine;

namespace OpenClawWorlds
{
    /// <summary>
    /// Shared material factory for procedurally generated town geometry.
    /// Auto-detects URP vs Built-in render pipeline.
    /// </summary>
    public class TownMaterials
    {
        public Material WoodDark { get; private set; }
        public Material WoodMed { get; private set; }
        public Material WoodLight { get; private set; }
        public Material Metal { get; private set; }
        public Material Brass { get; private set; }
        public Material Stone { get; private set; }
        public Material FabricRed { get; private set; }
        public Material FabricGreen { get; private set; }
        public Material LanternGlow { get; private set; }

        static Shader _cachedShader;

        /// <summary>
        /// Returns the correct lit shader for the current render pipeline.
        /// Falls through: URP/Lit → URP/Simple Lit → Standard → Unlit/Color.
        /// Cached after first lookup.
        /// </summary>
        public static Shader LitShader
        {
            get
            {
                if (_cachedShader != null) return _cachedShader;
                _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
                if (_cachedShader == null) _cachedShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (_cachedShader == null) _cachedShader = Shader.Find("Standard");
                if (_cachedShader == null)
                {
                    _cachedShader = Shader.Find("Unlit/Color");
                    if (_cachedShader != null)
                        Debug.LogWarning("[TownMaterials] No lit shader found — using Unlit/Color fallback. Buildings will appear flat-shaded.");
                }
                return _cachedShader;
            }
        }

        /// <summary>Quick helper: create a solid-color material without a TownMaterials instance.</summary>
        public static Material QuickMat(Color color, float smoothness = 0.15f)
        {
            var shader = LitShader;
            if (shader == null)
            {
                Debug.LogError("[TownMaterials] No shader available at all. Check that your render pipeline shaders are included in the build.");
                return new Material(Shader.Find("Hidden/InternalErrorShader")) { color = color };
            }
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        public TownMaterials()
        {
            WoodDark     = MakeMat(new Color(0.25f, 0.15f, 0.08f), 0.3f);
            WoodMed      = MakeMat(new Color(0.40f, 0.28f, 0.14f), 0.25f);
            WoodLight    = MakeMat(new Color(0.55f, 0.42f, 0.25f), 0.2f);
            Metal        = MakeMat(new Color(0.3f, 0.3f, 0.32f), 0.6f, 0.7f);
            Brass        = MakeMat(new Color(0.7f, 0.6f, 0.2f), 0.5f, 0.5f);
            Stone        = MakeMat(new Color(0.5f, 0.48f, 0.44f), 0.1f);
            FabricRed    = MakeMat(new Color(0.6f, 0.15f, 0.1f), 0.05f);
            FabricGreen  = MakeMat(new Color(0.15f, 0.35f, 0.15f), 0.05f);
            LanternGlow  = MakeMat(new Color(1f, 0.8f, 0.3f), 0.1f, 0f, new Color(1f, 0.7f, 0.2f) * 2f);
        }

        public Material MakeMat(Color color, float smoothness = 0.15f, float metallic = 0f, Color? emission = null)
        {
            var mat = QuickMat(color, smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            return mat;
        }
    }
}
