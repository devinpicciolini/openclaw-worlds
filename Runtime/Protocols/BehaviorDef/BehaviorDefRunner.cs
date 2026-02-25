using UnityEngine;
using System;
using System.Collections.Generic;

namespace OpenClawWorlds.Protocols
{
    // ═══════════════════════════════════════════════════════════════════
    //  BehaviorDef Engine — interprets BehaviorDef JSON blocks at runtime
    //
    //  CityDef builds the world. BehaviorDef changes the rules.
    //  Both are just JSON. Both are instant. Zero compilation.
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Interprets BehaviorDef JSON blocks and creates runtime effects instantly.
    /// No compilation. No domain reload. Same-frame execution.
    /// </summary>
    public static class BehaviorEngine
    {
        /// <summary>Fired when a behavior is created. Args: (name, type, summary).</summary>
        public static event Action<string, string, string> OnBehaviorCreated;

        /// <summary>
        /// Optional: assign a Transform for FollowPlayer to track.
        /// If null, FollowPlayer will search for a "Player"-tagged object.
        /// </summary>
        public static Transform PlayerTransform { get; set; }

        static readonly Dictionary<string, GameObject> activeBehaviors = new Dictionary<string, GameObject>();

        /// <summary>
        /// Check an AI response for ```behaviordef code blocks.
        /// If found, execute each one immediately. Returns summary or null.
        /// </summary>
        public static string ProcessResponse(string response)
        {
            var blocks = ExtractBehaviorDefBlocks(response);
            if (blocks == null || blocks.Length == 0)
                return null;

            string summary = "";
            foreach (var json in blocks)
            {
                try
                {
                    string result = Execute(json);
                    summary += (summary.Length > 0 ? "\n" : "") + result;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BehaviorEngine] Failed: {e.Message}");
                    summary += $"\n[Behavior failed: {e.Message}]";
                }
            }

            return summary;
        }

        /// <summary>Execute a single BehaviorDef JSON string.</summary>
        public static string Execute(string json)
        {
            var def = JsonUtility.FromJson<BehaviorDef>(json);
            if (def == null)
                throw new Exception("Invalid BehaviorDef JSON");

            string type = (def.type ?? "particle").ToLower();
            string name = def.name ?? $"behavior_{type}_{Time.frameCount}";

            // Remove existing behavior with same name (allows overwriting)
            if (activeBehaviors.TryGetValue(name, out var existing) && existing != null)
            {
                UnityEngine.Object.Destroy(existing);
                activeBehaviors.Remove(name);
            }

            switch (type)
            {
                case "particle": return CreateParticleEffect(def, name);
                case "light":    return CreateLightEffect(def, name);
                case "physics":  return ApplyPhysicsMod(def, name);
                case "fog":      return ApplyFog(def, name);
                case "timer":    return CreateTimer(def, name);
                case "remove":   return RemoveBehavior(name);
                default:         return $"Unknown behavior type: {type}";
            }
        }

        // ── Particle Effects ──

        static string CreateParticleEffect(BehaviorDef def, string name)
        {
            var p = def.particles ?? new ParticleDef();

            var go = new GameObject($"Behavior_{name}");
            if (def.followPlayer)
                go.AddComponent<FollowPlayer>();

            if (p.offset != null && p.offset.Length >= 3)
                go.transform.position = new Vector3(p.offset[0], p.offset[1], p.offset[2]);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = p.count;
            main.startLifetime = p.lifetime;
            main.startSpeed = p.speed;
            main.startSize = p.size;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = p.gravity;

            if (p.duration > 0)
            {
                main.duration = p.duration;
                main.loop = false;
            }

            if (p.color != null && p.color.Length >= 3)
            {
                float a = p.color.Length >= 4 ? p.color[3] : 1f;
                main.startColor = new Color(p.color[0], p.color[1], p.color[2], a);
            }

            var emission = ps.emission;
            emission.rateOverTime = p.rate;

            var shape = ps.shape;
            switch ((p.shape ?? "box").ToLower())
            {
                case "sphere":     shape.shapeType = ParticleSystemShapeType.Sphere; break;
                case "cone":       shape.shapeType = ParticleSystemShapeType.Cone; break;
                case "hemisphere": shape.shapeType = ParticleSystemShapeType.Hemisphere; break;
                default:           shape.shapeType = ParticleSystemShapeType.Box; break;
            }

            if (p.shapeScale != null && p.shapeScale.Length >= 3)
                shape.scale = new Vector3(p.shapeScale[0], p.shapeScale[1], p.shapeScale[2]);

            // Material
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Standard Unlit")
                      ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);

            if (p.color != null && p.color.Length >= 3)
            {
                float a = p.color.Length >= 4 ? p.color[3] : 1f;
                mat.color = new Color(p.color[0], p.color[1], p.color[2], a);
            }

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            if (p.additive)
            {
                mat.SetFloat("_Blend", 2f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }
            else
            {
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            renderer.material = mat;

            if (p.duration > 0)
                UnityEngine.Object.Destroy(go, p.duration + p.lifetime + 1f);

            activeBehaviors[name] = go;
            string summary = $"Created particle effect '{name}' ({p.count} particles, {p.rate}/sec)";
            Debug.Log($"[BehaviorEngine] {summary}");
            OnBehaviorCreated?.Invoke(name, "particle", summary);
            return summary;
        }

        // ── Light Effects ──

        static string CreateLightEffect(BehaviorDef def, string name)
        {
            var l = def.lighting ?? new LightDef();

            var go = new GameObject($"Behavior_{name}");
            if (def.followPlayer)
                go.AddComponent<FollowPlayer>();

            if (l.position != null && l.position.Length >= 3)
                go.transform.position = new Vector3(l.position[0], l.position[1], l.position[2]);

            string mode = (l.mode ?? "ambient").ToLower();

            if (mode == "ambient")
            {
                Color c = Color.white;
                if (l.color != null && l.color.Length >= 3)
                    c = new Color(l.color[0], l.color[1], l.color[2]);
                RenderSettings.ambientLight = c * l.intensity;
                RenderSettings.ambientIntensity = l.intensity;

                activeBehaviors[name] = go;
                string s = $"Set ambient light to ({c.r:F1}, {c.g:F1}, {c.b:F1}) intensity {l.intensity}";
                Debug.Log($"[BehaviorEngine] {s}");
                OnBehaviorCreated?.Invoke(name, "light", s);
                return s;
            }

            var light = go.AddComponent<Light>();
            light.type = mode == "directional" ? LightType.Directional : LightType.Point;
            light.intensity = l.intensity;
            light.range = l.range;

            if (l.color != null && l.color.Length >= 3)
                light.color = new Color(l.color[0], l.color[1], l.color[2]);

            if (l.pulseSpeed > 0)
                go.AddComponent<PulseLight>().Init(l.intensity, l.pulseSpeed);

            activeBehaviors[name] = go;
            string summary = $"Created {mode} light '{name}' (intensity {l.intensity})";
            Debug.Log($"[BehaviorEngine] {summary}");
            OnBehaviorCreated?.Invoke(name, "light", summary);
            return summary;
        }

        // ── Physics Modifiers ──

        static string ApplyPhysicsMod(BehaviorDef def, string name)
        {
            var p = def.physics ?? new PhysicsDef();

            Physics.gravity = new Vector3(0, p.gravity, 0);
            Time.timeScale = Mathf.Clamp(p.timescale, 0.1f, 3f);

            var go = new GameObject($"Behavior_{name}");
            go.AddComponent<PhysicsRestorer>();

            activeBehaviors[name] = go;
            string summary = $"Physics: gravity={p.gravity}, timescale={p.timescale}";
            Debug.Log($"[BehaviorEngine] {summary}");
            OnBehaviorCreated?.Invoke(name, "physics", summary);
            return summary;
        }

        // ── Fog ──

        static string ApplyFog(BehaviorDef def, string name)
        {
            var f = def.fog ?? new FogDef();

            RenderSettings.fog = f.enabled;
            RenderSettings.fogDensity = f.density;

            switch ((f.mode ?? "exponential").ToLower())
            {
                case "linear": RenderSettings.fogMode = FogMode.Linear; break;
                case "exponentialsquared": RenderSettings.fogMode = FogMode.ExponentialSquared; break;
                default: RenderSettings.fogMode = FogMode.Exponential; break;
            }

            if (f.color != null && f.color.Length >= 3)
                RenderSettings.fogColor = new Color(f.color[0], f.color[1], f.color[2]);

            var go = new GameObject($"Behavior_{name}");
            go.AddComponent<FogRestorer>();

            activeBehaviors[name] = go;
            string summary = f.enabled
                ? $"Fog enabled: density={f.density}, mode={f.mode}"
                : "Fog disabled";
            Debug.Log($"[BehaviorEngine] {summary}");
            OnBehaviorCreated?.Invoke(name, "fog", summary);
            return summary;
        }

        // ── Timer ──

        static string CreateTimer(BehaviorDef def, string name)
        {
            var t = def.timer ?? new TimerDef();

            var go = new GameObject($"Behavior_{name}");
            var timer = go.AddComponent<BehaviorTimer>();
            timer.Init(t.delay, t.removeBehavior);

            activeBehaviors[name] = go;
            string summary = $"Timer set: {t.delay}s, then remove '{t.removeBehavior}'";
            Debug.Log($"[BehaviorEngine] {summary}");
            return summary;
        }

        // ── Remove ──

        static string RemoveBehavior(string name)
        {
            if (activeBehaviors.TryGetValue(name, out var go) && go != null)
            {
                UnityEngine.Object.Destroy(go);
                activeBehaviors.Remove(name);
                Debug.Log($"[BehaviorEngine] Removed '{name}'");
                return $"Removed behavior '{name}'";
            }
            return $"No active behavior named '{name}'";
        }

        /// <summary>Remove all active behaviors (reset).</summary>
        public static void ClearAll()
        {
            foreach (var kvp in activeBehaviors)
            {
                if (kvp.Value != null)
                    UnityEngine.Object.Destroy(kvp.Value);
            }
            activeBehaviors.Clear();
            Physics.gravity = new Vector3(0, -9.81f, 0);
            Time.timeScale = 1f;
            Debug.Log("[BehaviorEngine] All behaviors cleared");
        }

        // ── Block Extraction ──

        static string[] ExtractBehaviorDefBlocks(string response)
        {
            var results = new List<string>();
            int searchFrom = 0;

            while (searchFrom < response.Length)
            {
                // Try ```behaviordef first (explicit), then ```json with content heuristic
                int fenceStart = response.IndexOf("```behaviordef", searchFrom, StringComparison.OrdinalIgnoreCase);
                bool isExplicit = fenceStart >= 0;
                if (fenceStart < 0)
                    fenceStart = response.IndexOf("```json", searchFrom, StringComparison.OrdinalIgnoreCase);
                if (fenceStart < 0) break;

                int codeStart = response.IndexOf('\n', fenceStart);
                if (codeStart < 0) break;
                codeStart++;

                int fenceEnd = response.IndexOf("```", codeStart);
                if (fenceEnd < 0) break;

                string json = response.Substring(codeStart, fenceEnd - codeStart).Trim();

                if (json.Length > 5)
                {
                    if (isExplicit)
                    {
                        results.Add(json);
                    }
                    else
                    {
                        // For ```json blocks, only treat as BehaviorDef if it has behavior markers
                        // but NOT CityDef markers (those are handled by CityDefSpawner)
                        bool hasBehaviorType = json.Contains("\"type\"") &&
                            (json.Contains("\"particle\"") || json.Contains("\"light\"") ||
                             json.Contains("\"physics\"") || json.Contains("\"fog\"") ||
                             json.Contains("\"timer\"") || json.Contains("\"remove\""));
                        bool isCityDef = json.Contains("\"streets\"") || json.Contains("\"buildings\"");
                        if (hasBehaviorType && !isCityDef)
                            results.Add(json);
                    }
                }

                searchFrom = fenceEnd + 3;
            }

            return results.Count > 0 ? results.ToArray() : null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Helper MonoBehaviours (attached to behavior GameObjects)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Makes a GameObject follow the player's position every frame.</summary>
    public class FollowPlayer : MonoBehaviour
    {
        Transform player;
        Vector3 offset;

        void Start()
        {
            offset = transform.position;
            // Use SDK-provided reference first, fall back to tag search
            player = BehaviorEngine.PlayerTransform;
            if (player == null)
                player = GameObject.FindWithTag("Player")?.transform;
        }

        void LateUpdate()
        {
            if (player != null)
                transform.position = player.position + offset;
        }
    }

    /// <summary>Pulses a Light component's intensity sinusoidally.</summary>
    public class PulseLight : MonoBehaviour
    {
        float baseIntensity;
        float speed;
        Light lightComponent;

        public void Init(float intensity, float pulseSpeed)
        {
            baseIntensity = intensity;
            speed = pulseSpeed;
        }

        void Start() { lightComponent = GetComponent<Light>(); }

        void Update()
        {
            if (lightComponent != null)
                lightComponent.intensity = baseIntensity * (0.5f + 0.5f * Mathf.Sin(Time.time * speed));
        }
    }

    /// <summary>Restores default physics when the behavior is destroyed.</summary>
    public class PhysicsRestorer : MonoBehaviour
    {
        void OnDestroy()
        {
            Physics.gravity = new Vector3(0, -9.81f, 0);
            Time.timeScale = 1f;
            Debug.Log("[BehaviorEngine] Physics restored to defaults");
        }
    }

    /// <summary>Restores fog settings when destroyed.</summary>
    public class FogRestorer : MonoBehaviour
    {
        void OnDestroy()
        {
            RenderSettings.fog = false;
            Debug.Log("[BehaviorEngine] Fog disabled");
        }
    }

    /// <summary>Removes a named behavior after a delay.</summary>
    public class BehaviorTimer : MonoBehaviour
    {
        float delay;
        string targetName;
        float startTime;

        public void Init(float d, string target)
        {
            delay = d;
            targetName = target;
            startTime = Time.time;
        }

        void Update()
        {
            if (Time.time - startTime >= delay)
            {
                if (!string.IsNullOrEmpty(targetName))
                {
                    string safeName = targetName.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    BehaviorEngine.Execute($"{{\"type\":\"remove\",\"name\":\"{safeName}\"}}");
                }
                Destroy(gameObject);
            }
        }
    }
}
