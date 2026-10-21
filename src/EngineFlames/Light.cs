using System;
using System.Collections.Generic;
using UnityEngine;

namespace EngineFlames
{
    /// <summary>
    /// A running engine's flame lights what is round it: the craft it hangs from, and the ground it strikes.
    ///
    /// The game's flame lights nothing: a rocket leaving the pad at night is a bright plume under a black rocket on
    /// black ground. Here the two strongest flames of the craft round about each give a lamp of the costly kind
    /// (drawn for every dot it reaches, highlights and all) just below the bell, which lights the engine, the tanks
    /// above it and whatever is near; and where a flame reaches the ground, a second at the place it lands: the
    /// ground there, white-hot under the flame, is what lights a real rocket's sides at night, and the smoke round
    /// it. How bright goes with the thrust and with what burns (kerosene's light, a solid booster's half as much
    /// again, hydrogen's a quarter: see Kind.Shines), and with the air: in thin air a flame spreads out faint. Its
    /// colour is the fuel's. And it is made stronger as the daylight goes, as the explosions' fires are: the game's
    /// lights are not reckoned in real amounts, and beside the sun what a flame adds is little, beside the space
    /// centre's floodlights at night a great deal.
    ///
    /// Natural Light lights every running engine in the same way, whoever draws its flame, and so does the Engine
    /// Lighting mod: where either is installed this does nothing, and leaves it to them.
    /// </summary>
    public sealed partial class Flames
    {
        sealed class Group { public Vessel Vessel; public Vector3 At, Along; public float Power, Length, Radius, Seed; public Color Tint; }
        readonly List<Group> groups = new List<Group>();
        static readonly int weatherSun = Shader.PropertyToID("_WeatherShadowSun"), weatherFade = Shader.PropertyToID("_WeatherShadowFade");

        /// <summary>Once a frame, after the flames have been fed: the lamps put where the strongest flames are.</summary>
        void Shine()
        {
            if (!Lights.Wanted || Lights.Others()) { Dark(); return; }
            groups.Clear();
            foreach (Plume p in plumes)
            {
                if (!p.Burning || p.Engine == null || p.Nozzle == null || p.Part == null || p.Part.vessel == null) continue;
                float power = p.Engine.finalThrust * p.Share * p.Kind.Shines;
                if (power < 0.5f) continue;
                Vector3 along = p.Nozzle.forward, rim = p.Nozzle.position + along * p.Rim;
                // (nozzles close together are one flame for their light: a cluster's light is one light)
                Group g = null;
                foreach (Group h in groups)
                {
                    float near = Mathf.Max(3f, 2.5f * (h.Radius + p.Radius));
                    if (h.Vessel == p.Part.vessel && (h.At - rim).sqrMagnitude < near * near) { g = h; break; }
                }
                if (g == null) { groups.Add(new Group { Vessel = p.Part.vessel, At = rim, Along = along, Power = power, Length = p.Length, Radius = p.Radius, Seed = (p.Id & 1023) * 0.37f, Tint = p.Kind.Light }); continue; }
                float w = power / (g.Power + power);
                g.At = Vector3.Lerp(g.At, rim, w);
                g.Along = Vector3.Lerp(g.Along, along, w).normalized;
                g.Tint = Color.Lerp(g.Tint, p.Kind.Light, w);
                g.Length = Mathf.Max(g.Length, p.Length);
                g.Radius = Mathf.Max(g.Radius, p.Radius);
                g.Power += power;
            }
            groups.Sort((a, b) => b.Power.CompareTo(a.Power));
            float glow = (float)Fire.Glow, time = Time.time;
            for (int n = 0; n < Lights.Most; n++)
            {
                if (n >= groups.Count) { Lights.Off(2 * n); Lights.Off(2 * n + 1); continue; }
                Group g = groups[n];
                Vessel v = g.Vessel;
                // (thin air: the flame spreads out wide and faint, and lights the craft far less)
                float air = 0.35f + 0.65f * Mathf.Clamp01((float)(v.staticPressurekPa / 101.325) * 2f);
                float bright = 0.55f * Mathf.Sqrt(g.Power / 50f) * glow * air * Gain(v);
                float flicker = 1f + 0.08f * (Mathf.PerlinNoise(time * 9f, g.Seed) - 0.5f);
                // Just below the bell, a little way down the flame: the engine, the tanks over it, and what is near.
                Lights.Set(2 * n, g.At + g.Along * (1f + 0.08f * g.Length), g.Tint, Mathf.Min(8f, bright * flicker), Mathf.Max(25f, 1.6f * g.Length + 10f), n == 0);
                // Where the flame reaches the ground: the ground's own glow under it, and brighter the closer the engine is.
                float reach = 1.15f * g.Length;
                if (v.altitude < 25000.0 && Physics.Raycast(g.At, g.Along, out RaycastHit hit, reach, 1 << 15, QueryTriggerInteraction.Ignore))
                {
                    float close = Mathf.Sqrt(Mathf.Clamp01(1f - hit.distance / reach));
                    float stamp = 1f + 0.16f * (Mathf.PerlinNoise(time * 6f, g.Seed + 11.3f) - 0.5f);
                    Lights.Set(2 * n + 1, hit.point + hit.normal * Mathf.Max(2f, 1.5f * g.Radius), g.Tint, Mathf.Min(8f, 1.1f * bright * close * stamp), Mathf.Max(20f, 0.9f * g.Length + 12f), false);
                }
                else Lights.Off(2 * n + 1);
            }
        }

        void Dark()
        {
            for (int k = 0; k < 2 * Lights.Most; k++) Lights.Off(k);
        }

        /// <summary>How much stronger than by day a flame's light is made where it is: 2.5 times by night, as by day in full sun (after the weather's clouds).</summary>
        static float Gain(Vessel v)
        {
            CelestialBody body = v.mainBody;
            if (body == null || Planetarium.fetch == null || Planetarium.fetch.Sun == null) return 1f;
            Vector3d up = (v.CoMD - body.position).normalized, sun = (Planetarium.fetch.Sun.position - v.CoMD).normalized;
            float daylight = body.atmosphere && v.altitude < body.atmosphereDepth ? Mathf.Clamp01((float)Vector3d.Dot(up, sun) * 4f + 0.25f) : 0.7f;
            Vector4 shading = Shader.GetGlobalVector(weatherSun), fade = Shader.GetGlobalVector(weatherFade);
            if (shading.w > 0.5f) daylight *= Mathf.Clamp01(fade.x);
            return Mathf.Lerp(2.5f, 1f, Mathf.Clamp01(daylight * 1.6f));
        }

        /// <summary>The lamps themselves: kept for the life of the flight scene, two to a flame (on the flame, and on the ground under it).</summary>
        static class Lights
        {
            public const int Most = 2;
            static readonly Light[] lamps = new Light[2 * Most];
            static bool looked, others;

            public static bool Wanted => Fire.On != null && Fire.On && Fire.Lighting != null && Fire.Lighting;

            /// <summary>Whether another mod lights engines: Natural Light (every engine, whoever draws its flame), or the Engine Lighting mod.</summary>
            public static bool Others()
            {
#if DEV
                if (TestAlways) return false;
#endif
                if (looked) return others;
                looked = true;
                foreach (AssemblyLoader.LoadedAssembly a in AssemblyLoader.loadedAssemblies)
                    if (a.name != null && (a.name.Equals("NaturalLight", StringComparison.OrdinalIgnoreCase) || a.name.Equals("EngineLight", StringComparison.OrdinalIgnoreCase))) others = true;
                if (others) Fire.Log("the flames' light on the craft and the ground is left to " + (Has("NaturalLight") ? "Natural Light" : "the Engine Lighting mod") + ", which is installed");
                return others;
            }

            static bool Has(string name)
            {
                foreach (AssemblyLoader.LoadedAssembly a in AssemblyLoader.loadedAssemblies) if (a.name != null && a.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }

            public static void Set(int k, Vector3 at, Color tint, float intensity, float range, bool costly)
            {
                if (intensity < 0.03f) { Off(k); return; }
                Light l = lamps[k];
                if (l == null)
                {
                    var holder = new GameObject("Engine Flames light " + k);
                    l = lamps[k] = holder.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.shadows = LightShadows.None;
                }
                l.renderMode = costly ? LightRenderMode.ForcePixel : LightRenderMode.Auto;
                l.transform.position = at;
                l.color = tint;
                l.intensity = intensity;
                l.range = range;
                if (!l.enabled) l.enabled = true;
            }

            public static void Off(int k)
            {
                if (lamps[k] != null && lamps[k].enabled) lamps[k].enabled = false;
            }

            public static void Gone()
            {
                for (int k = 0; k < lamps.Length; k++) if (lamps[k] != null) { UnityEngine.Object.Destroy(lamps[k].gameObject); lamps[k] = null; }
            }

#if DEV
            public static bool TestAlways;      // (for trying: light even where another mod would)
            public static string Said()
            {
                var say = new System.Text.StringBuilder(others ? "left to another mod; " : "");
                for (int k = 0; k < lamps.Length; k++)
                {
                    Light l = lamps[k];
                    say.Append("lamp ").Append(k).Append(l == null ? ": none" : (l.enabled ? " on" : " off") + ", intensity " + l.intensity.ToString("F2") + ", range " + l.range.ToString("F0") + ", " + l.renderMode + ", colour " + l.color).Append("; ");
                }
                return say.ToString();
            }
#endif
        }

#if DEV
        /// <summary>(The flames' own lamps: on or off, how strong, how far.)</summary>
        public static string LightsNow() => Lights.Said();
        /// <summary>(For trying: "on" lights even where Natural Light is installed, "off" leaves it to it again.)</summary>
        public static string LightTest(string arg) { Lights.TestAlways = arg == "on"; return "the flames light " + (Lights.TestAlways ? "always" : "only without another mod that does"); }
#endif
    }
}
