using System;
using System.Collections.Generic;
using Keystone;
using UnityEngine;

namespace EngineFlames
{
    /// <summary>
    /// (Since 0.5.0.) An upper stage's bell glowing with heat.
    ///
    /// An engine that works in thick air is cooled by its own fuel running through the walls of its chamber and nozzle, and its
    /// bell stays as dark as it was. An engine made for vacuum carries a long nozzle extension that nothing cools but its own
    /// light: through a burn it heats until it gives off as much as the gas gives it, some 1,400 to 1,500 K at full thrust, and
    /// glows, dull red, cherry, then orange (the Merlin Vacuum's niobium skirt seen from the second stage in every Falcon 9 film,
    /// the Apollo service engine's, an RL-10's); after the burn it cools within seconds, red again before it goes dark.
    /// The game's own glow of hot parts does not show it (only a few of its older engines have a heat animation at all).
    ///
    /// Which engines: those made for vacuum, told by how much better they push there than at the ground (more than a third:
    /// the Terrier, the Poodle, the Wolfhound, the Rhino, the Cheetah; not the Swivel or the Mainsail, whose bells are cooled).
    /// Which meshes: the engine's own that lie about its thrust's axis between its throat and its mouth (the bell as Keystone
    /// measures it), given an emissive colour of their own. How hot: towards a heat set by how hard the engine runs, within a few
    /// seconds; cooling as hot things in vacuum do, by their own light (fast while hot, slow once dull: dark again in ten to fifteen). The colour is a
    /// black body's at that heat, as bright as it really is against the sunlit metal round it: invisible under 800 K.
    /// </summary>
    static class Glow
    {
        sealed class Bell
        {
            public ModuleEngines Engine;
            public Part Part;
            public readonly List<Material> Materials = new List<Material>();
            public readonly List<Color> Was = new List<Color>();
            public float Kelvin = 290f;
            public bool Found, Cooled;
            public int Frame = -1;
        }

        static readonly Dictionary<ModuleEngines, Bell> bells = new Dictionary<ModuleEngines, Bell>();
        static readonly List<ModuleEngines> gone = new List<ModuleEngines>();
        static readonly int emissive = Shader.PropertyToID("_EmissiveColor");
        public static string Said = "";

        /// <summary>Whether an engine is made for vacuum, with a bell nothing cools but its own light.</summary>
        static bool ForVacuum(ModuleEngines e)
        {
            if (e == null || e.atmosphereCurve == null || e.engineType == EngineType.Turbine) return false;
            float vac = e.atmosphereCurve.Evaluate(0f), ground = e.atmosphereCurve.Evaluate(1f);
            return vac > 1f && ground > 0f && vac / ground > 1.33f;
        }

        /// <summary>Once a frame, for every engine that has a flame of this mod's (running or not): its bell's heat, and its glow.</summary>
        public static void Step(ModuleEngines engine, Part part, Transform nozzle, float radius, float rim, float running, float dt)
        {
            if (engine == null || part == null || nozzle == null || dt <= 0f) return;
            if (!bells.TryGetValue(engine, out Bell b))
            {
                b = new Bell { Engine = engine, Part = part, Cooled = !ForVacuum(engine) };
                bells[engine] = b;
            }
            if (b.Cooled || b.Frame == Time.frameCount) return;
            b.Frame = Time.frameCount;
            if (!b.Found) { b.Found = true; Find(b, nozzle, radius, rim); }
            if (b.Materials.Count == 0) return;
            // (heating towards what the gas gives it at this thrust; cooling by its own light, as T^4)
            float target = 290f + 1160f * Mathf.Pow(Mathf.Clamp01(running), 0.6f);
            // (A skirt a millimetre thick holds little heat for what it gives off at these heats: some 1,600 J a square metre for
            // each degree, against 2.4 x 10^5 W off each side at 1,400 K. It comes to its heat within a few seconds of the gas
            // reaching it, and, the gas gone, loses some 240 degrees a second at first, sixty at a thousand: the glow is gone ten
            // to fifteen seconds after the engine stops, as in films of the Merlin Vacuum's.)
            if (target > b.Kelvin) b.Kelvin += (target - b.Kelvin) * (1f - Mathf.Exp(-dt / 4f));
            else
            {
                float t = b.Kelvin / 1450f;
                b.Kelvin = Mathf.Max(target, b.Kelvin - dt * 240f * t * t * t * t);
            }
#if DEV
            if (!float.IsNaN(held)) b.Kelvin = held;
#endif
            Color c = Body(b.Kelvin);
            for (int i = 0; i < b.Materials.Count; i++) if (b.Materials[i] != null) b.Materials[i].SetColor(emissive, b.Was[i] + c);
        }

        /// <summary>
        /// A black body's light at a heat, as an emissive colour against things lit by the sun: nothing to see under about 800 K, a
        /// dull red at 900, cherry at 1,100, orange at 1,300, yellow-orange at 1,500. (Its brightness rises steeply, by Planck's law in
        /// the red: about seven times for every hundred degrees at these heats, levelling off as it nears white.)
        /// </summary>
        static Color Body(float k)
        {
            if (k < 780f) return Color.black;
            float x = Mathf.Clamp01((k - 780f) / 820f);
            float bright = Mathf.Clamp01((k - 780f) / 720f);
            bright = bright * bright * (0.4f + 0.6f * bright) * 1.15f;
            var hue = new Color(1f, Mathf.Lerp(0.06f, 0.62f, x * x), Mathf.Lerp(0.0f, 0.26f, x * x * x));
            return hue * bright;
        }

        /// <summary>The engine's own meshes about its axis between its throat and its mouth, and each one's material made its own, its own emissive kept.</summary>
        static void Find(Bell b, Transform nozzle, float radius, float rim)
        {
            Vector3 axis = nozzle.forward, mouth = nozzle.position + axis * rim;
            float r = Mathf.Max(radius, 0.05f);
            int looked = 0;
            foreach (Renderer rend in b.Part.GetComponentsInChildren<Renderer>(false))
            {
                if (rend == null || !(rend is MeshRenderer) || !rend.enabled) continue;
                looked++;
                Bounds box = rend.bounds;
                Vector3 c = box.center - mouth;
                float along = Vector3.Dot(c, axis), off = (c - axis * along).magnitude;
                float half = Mathf.Max(box.extents.x, Mathf.Max(box.extents.y, box.extents.z));
                // (a bell: about the axis, ending at the mouth, no wider than the mouth and not much longer than three of its radii)
                if (off > 0.35f * r || along > 0.3f * r || along < -2.6f * r || half > 1.9f * r || half < 0.25f * r) continue;
                Material m = rend.material;
                if (m == null || !m.HasProperty(emissive)) continue;
                b.Materials.Add(m);
                b.Was.Add(m.GetColor(emissive));
            }
            Said = b.Part.partInfo.name + ": " + b.Materials.Count + " of its " + looked + " meshes taken for its bell";
        }

        /// <summary>Every bell's own emissive back as it was (the mod switched off, or the scene left).</summary>
        public static void Gone()
        {
            foreach (Bell b in bells.Values)
                for (int i = 0; i < b.Materials.Count; i++) if (b.Materials[i] != null) b.Materials[i].SetColor(emissive, b.Was[i]);
            bells.Clear();
        }

        /// <summary>Bells of engines that are no more forgotten.</summary>
        public static void Tidy()
        {
            gone.Clear();
            foreach (var pair in bells) if (pair.Key == null || pair.Value.Part == null) gone.Add(pair.Key);
            foreach (ModuleEngines e in gone) bells.Remove(e);
        }

#if DEV
        /// <summary>(Development build: every bell known, how hot, and what was found of it.)</summary>
        public static string Says()
        {
            var say = new System.Text.StringBuilder(Said);
            foreach (Bell b in bells.Values)
                if (b.Part != null) say.Append("\n").Append(b.Part.partInfo.name).Append(": ").Append(b.Cooled ? "cooled by its fuel" : b.Materials.Count + " meshes, " + b.Kelvin.ToString("F0") + " K");
            return say.ToString();
        }

        /// <summary>(Development build: the bells held at so many kelvin, to look at; "go" lets them be.)</summary>
        public static string Hold(string k)
        {
            if (k == "go") { held = float.NaN; return "as they are"; }
            held = float.Parse(k, System.Globalization.CultureInfo.InvariantCulture);
            foreach (Bell b in bells.Values) b.Kelvin = held;
            return "held at " + held + " K";
        }
        static float held = float.NaN;
#endif
    }
}
