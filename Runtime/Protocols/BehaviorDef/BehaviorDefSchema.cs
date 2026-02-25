using System;

namespace OpenClawWorlds.Protocols
{
    // ═══════════════════════════════════════════════════════════════════
    //  BehaviorDef Schema — JSON types for runtime behavior modification
    //
    //  CityDef builds the world. BehaviorDef changes the rules.
    //  Both are just JSON. Both are instant. Zero compilation.
    // ═══════════════════════════════════════════════════════════════════

    [Serializable]
    public class BehaviorDef
    {
        public string type;            // particle, light, physics, fog, timer, remove
        public string name;            // unique name for this behavior (for removal)
        public bool followPlayer;      // attach to player position
        public ParticleDef particles;
        public LightDef lighting;
        public PhysicsDef physics;
        public TimerDef timer;
        public FogDef fog;
    }

    [Serializable]
    public class ParticleDef
    {
        public int count = 500;
        public float lifetime = 2f;
        public float speed = 5f;
        public float size = 0.1f;
        public float[] color;          // [r, g, b, a]
        public float gravity;
        public string shape = "box";   // box, sphere, cone, hemisphere
        public float[] shapeScale;     // [x, y, z]
        public float[] offset;         // [x, y, z] from origin
        public float rate = 100f;
        public float duration;         // 0 = forever
        public bool additive;          // additive blending (fire, sparks)
    }

    [Serializable]
    public class LightDef
    {
        public string mode = "ambient"; // ambient, point, directional, pulse
        public float[] color;           // [r, g, b]
        public float intensity = 1f;
        public float range = 50f;
        public float pulseSpeed;
        public float[] position;        // [x, y, z]
    }

    [Serializable]
    public class PhysicsDef
    {
        public float gravity = -9.81f;
        public float timescale = 1f;
    }

    [Serializable]
    public class TimerDef
    {
        public float delay;
        public string removeBehavior;
    }

    [Serializable]
    public class FogDef
    {
        public bool enabled = true;
        public float[] color;           // [r, g, b]
        public float density = 0.02f;
        public string mode = "exponential";
    }
}
