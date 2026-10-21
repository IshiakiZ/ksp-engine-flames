using System;
using System.Collections.Generic;
using Keystone;
using UnityEngine;

#if !DEV
[assembly: KSPAssembly("EngineFlames", 0, 4)]
[assembly: KSPAssemblyDependency("Keystone", 0, 5)]
#endif

namespace EngineFlames
{
    /// <summary>
    /// Engine Flames: a rocket engine's exhaust as burning gas.
    ///
    /// Asked for as: "add better fire, more realistic, more beautiful, more cinematic", and, asked which
    /// fire: "engine flames". And of the first version, which drew each flame as a shape worked out from
    /// where a place was in it: "I want them to be super realistic, like the explosions are. They look
    /// cartoony."
    ///
    /// So a flame here is what an explosion's fire is in the Volumetric Explosions mod, and is made by it:
    /// gas, in thousands of bits, that leaves the nozzle fast and hot, is slowed by the air and stirred by
    /// it, widens, cools from white through yellow and orange to nothing, strikes the ground and runs out
    /// along it, and is drawn as one lit volume. Nothing of its shape is laid down beforehand. This mod
    /// says what is burning, where, how wide the nozzle is and how hard it is running; that one does the
    /// rest (see its Sources, and Site.Flame).
    ///
    /// What is burning: the game has one fuel for every liquid engine; its engines are taken as burning
    /// what the engines they were modelled on burn (that is Keystone's to say, for every mod that draws
    /// exhaust: see Keystone.Engines). Kerosene yellow-white; hydrogen pale blue and faint; the fuels of
    /// small upper-stage engines all but clear, salmon where it shows, with bright diamonds; a solid booster brilliant; hydrazine nearly without
    /// light; hydrogen heated by a reactor a faint red.
    ///
    /// Each nozzle of each rocket engine gets one. How wide a nozzle is and where its rim is are measured
    /// from the engine's own model, so the flame leaves the bell it belongs to. While it is drawn, the
    /// flames the game or Waterfall draws for that engine are hidden, and its smoke is left. Jets, ion
    /// engines and anything it does not know are left as they are.
    ///
    /// Without Volumetric Explosions, or where that cannot draw a volume (it needs its shaders: OpenGL's on a Mac or Linux, those
    /// built for Direct3D on Windows), this mod does nothing but say so.
    /// </summary>
    static class Fire
    {
        public const string Version = "0.5.0";

        public static Mod Page;
        public static Toggle On, Hide, Reentry, ReentryHideStock, Lighting, Vacuum, Moments, Bells;
        public static Slider Glow, Long, Wide, ReentryGlow;
        public static string Why = "";

        public static void Setup()
        {
            if (Page != null && Kit.Find("Engine Flames") == Page) return;
            Page = Kit.Register("Engine Flames", Version, "A rocket engine's exhaust as burning gas: it leaves the nozzle, is slowed and stirred by the air, widens, cools and strikes the ground, and is drawn as a true volume. It burns in the air of the Volumetric Explosions mod, which it needs.");
            On = Page.Toggle("on", "Engine flames", true, "Off: the flames the game (or the Waterfall mod) draws.");
            Glow = Page.Slider("glow", "How bright", 1f, 0.2f, 3f, "", 2, "How bright the flames are.");
            Long = Page.Slider("long", "How long", 1f, 0.4f, 2.5f, "", 2, "How long the flames are.");
            Wide = Page.Slider("wide", "How wide", 1f, 0.5f, 2f, "", 2, "How wide the flames are. A nozzle's width is measured from the engine's model (or, where that cannot be done, worked out from its thrust); this corrects it.");
            Hide = Page.Toggle("hide", "Hide the game's own flames", true, "While a flame of this mod's is drawn for an engine, the flames the game or Waterfall draws for it are hidden. Smoke is left.");
            Vacuum = Page.Toggle("vacuum", "Flames in a vacuum", true, "Off: where there is next to no air (high above the ground, or on a world with none), no flame of this mod's is drawn, and the game's own (or Waterfall's) is left as it is.");
            Moments = Page.Toggle("moments", "Engines start and stop as real ones do", true, "A flame builds over a second as an engine starts (a kerosene engine with the green flash of its igniter, a solid motor in a burst), falls back as it stops (a solid motor tails off in chuffs), and flickers as the tanks run dry. Off: the flame comes and goes at once, as the game's does.");
            Bells = Page.Toggle("bells", "Vacuum engines' bells glow with heat", true, "An engine made for vacuum has a long nozzle that nothing cools but its own light: through a long burn it heats until it glows dull red, then orange, and after the burn it cools within seconds, as the bells of real upper stages do.");
            Lighting = Page.Toggle("light", "Flames light what is round them", true, "A running engine's flame lights the craft and the ground it strikes, more strongly as the daylight goes. Where Natural Light or the Engine Lighting mod is installed, they light it instead.");
#if DEV
            // (the re-entry fire's, in the development build only since 0.5.0: see Reentry)
            Reentry = Page.Toggle("reentry", "Re-entry fire", true, "Coming into an atmosphere fast, a craft glows: a white-hot layer of air standing off the face that meets it, streaming back past its sides into a wake that cools from gold through orange to pink-violet. Drawn as a true volume where the game's own fire would be. Off: the game's own streaks.");
            ReentryGlow = Page.Slider("reentryGlow", "How bright the re-entry fire is", 1f, 0.2f, 3f, "", 2, "How bright the re-entry fire and its light are.");
            ReentryHideStock = Page.Toggle("reentryHide", "Hide the game's own re-entry streaks", true, "While this mod's re-entry fire burns for the craft being flown, the game's flat streaks and their light are kept off (its sound and shaking are left).");
#endif
        }

        public static void Log(string text) => Kit.Log("Engine Flames", text);
    }

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class FlamesAtStart : MonoBehaviour
    {
        void Awake() { Fire.Setup(); Destroy(gameObject); }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed partial class Flames : MonoBehaviour
    {
        /// <summary>
        /// What a kind of propellant burns like: which of the colours of fire that Volumetric Explosions knows
        /// (Tint: 0 fuel, 1 solid propellant, 2 monopropellant, 4 hydrogen, 5 methane, 6 the fuels that keep,
        /// 7 gas heated by a reactor), how bright against that kind's own usual, and how long and wide against the usual.
        /// (What each kind of fire is like, how thick, how bright, how torn, is the explosions mod's to know.)
        /// </summary>
        sealed class Kind
        {
            public Fuel Fuel;
            public string Name;
            public int Tint;
            public float Bright = 1f, Long = 1f, Wide = 1f;
            // (the light it throws round it: its colour, and how much for its thrust against kerosene's; see Light.cs)
            public Color Light = new Color(1f, 0.78f, 0.5f);
            public float Shines = 1f;
            // (How bright against kerosene's, for the explosions mod before its 0.6.0, which had to be told: since then it
            // knows what each kind of fire is like, and Bright is against that fuel's own usual.)
            public float Was = 1f;
        }

        static readonly Kind[] Kinds =
        {
            // Kerosene and oxygen: a blinding white-hot body, yellow and orange where it cools.
            new Kind { Fuel = Fuel.Kerosene, Name = "kerosene and oxygen", Tint = 0 },
            // Hydrogen and oxygen: nearly no light of its own, a clear pale blue with shock diamonds in it.
            new Kind { Fuel = Fuel.Hydrogen, Name = "hydrogen and oxygen", Tint = 4, Was = 0.38f, Long = 1.1f, Light = new Color(0.72f, 0.8f, 1f), Shines = 0.25f },
            // Methane and oxygen: blue to violet.
            new Kind { Fuel = Fuel.Methane, Name = "methane and oxygen", Tint = 5, Was = 0.6f, Light = new Color(0.78f, 0.74f, 1f), Shines = 0.5f },
            // Fuels that keep (the small engines of upper stages and landers: nitrogen tetroxide and a hydrazine, as Titan's and
            // Proton's burn): all but clear, salmon and peach where it shows, with bright shock diamonds standing in it.
            new Kind { Fuel = Fuel.Storable, Name = "fuels that keep", Tint = 6, Was = 0.45f, Long = 0.9f, Light = new Color(1f, 0.82f, 0.7f), Shines = 0.45f },
            // Solid fuel: the brightest of all, thick with burning metal, wide and long.
            new Kind { Fuel = Fuel.Solid, Name = "solid fuel", Tint = 1, Was = 1.7f, Wide = 1.1f, Light = new Color(1f, 0.88f, 0.7f), Shines = 1.6f },
            // Monopropellant: steam and nitrogen, all but without light.
            new Kind { Fuel = Fuel.Mono, Name = "monopropellant", Tint = 2, Was = 0.14f, Long = 0.7f, Light = new Color(0.9f, 0.9f, 1f), Shines = 0.08f },
            // Fuel heated by a reactor: hot hydrogen and nothing burning, a faint red.
            new Kind { Fuel = Fuel.Reactor, Name = "fuel heated by a reactor", Tint = 7, Was = 0.3f, Light = new Color(1f, 0.55f, 0.45f), Shines = 0.3f },
        };

        /// <summary>
        /// (Since 0.5.0.) A jet's afterburner (the explosions mod's fire of tint 9, from its 0.8.0). A jet's own exhaust is next to
        /// nothing to see, hot clear air (the game's shimmer is left for it); with its afterburner lit, fuel burnt again in the tail
        /// pipe, it is a long thin flame, blue-violet where it burns, with a row of yellow-white shock diamonds where the gas is
        /// squeezed and the fuel left in it lights again, and an orange tail: most of what is seen is the diamonds. The game's
        /// Panther in its wet mode, the Whiplash and the RAPIER breathing air, and any engine whose mode is named so.
        /// </summary>
        static readonly Kind Afterburner = new Kind { Fuel = Fuel.None, Name = "a jet's afterburner", Tint = 9, Was = 0.5f, Long = 1f, Light = new Color(1f, 0.72f, 0.48f), Shines = 0.4f };

        static bool Afterburns(ModuleEngines engine, string part)
        {
            if (engine == null || !Feed.KnowsJets) return false;
            bool breathes = engine.engineType == EngineType.Turbine;
            foreach (Propellant p in engine.propellants) if (p.name == "IntakeAir" || p.name == "IntakeAtm") breathes = true;
            if (!breathes) return false;
            string id = (engine.engineID ?? "").ToLowerInvariant();
            if (id.Contains("wet") || id.Contains("afterburn") || id.Contains("reheat") || id.Contains("ramjet") || id.Contains("airbreathing")) return true;
            return part == "turboFanEngine";               // (the Whiplash, a turbo-ramjet: one mode, burning as an afterburner does)
        }

        /// <summary>The flame of what an engine burns (which fuel each engine is taken as burning is Keystone's to say: see Keystone.Engines), or none.</summary>
        static Kind Burns(ModuleEngines engine, string part)
        {
            Fuel fuel = Engines.Burns(engine, part);
            foreach (Kind k in Kinds) if (k.Fuel == fuel) return k;
            return Afterburns(engine, part) ? Afterburner : null;
        }

        /// <summary>One nozzle of one engine.</summary>
        sealed class Plume
        {
            public ModuleEngines Engine;
            public Part Part;
            public Transform Nozzle;
            public Kind Kind;
            public int Id;
            public float Guess, Radius, Rim, Share, Length, Running, Seen = -100f, HidAt;
            public bool Measured, Burning;
            // (since 0.5.0: when it last lit, when it last stopped and how hard it was burning then; see Moment)
            public float LitAt = -100f, CutAt = -100f, CutFrom, Shown;
            public readonly List<Renderer> Hidden = new List<Renderer>();
        }

        readonly List<Plume> plumes = new List<Plume>();
        readonly HashSet<ModuleEngines> known = new HashSet<ModuleEngines>();
        float lookAt;
        bool hooked;
        public static string Report = "";

        // (in a method of its own: a Keystone from before 2026-10-18 has no such thing, and it is the method that names it that fails)
        static void AskAhead() => Engines.MeasureAhead = true;

        void Awake()
        {
            Fire.Setup();
            Fire.Page.Panel = Panel;
            if (Installed())
            {
                try { Feed.Hook(Asked); hooked = true; }
                catch (Exception ex) { Fire.Why = "Volumetric Explosions is installed but is not a version this works with (" + ex.GetType().Name + "): the game's own flames are left."; Fire.Log(Fire.Why); }
                // (the nozzles measured by Keystone ahead of the lift-off, not at it: see Keystone's Engines.MeasureAhead)
                if (hooked) try { AskAhead(); } catch (Exception) { }
            }
            else
            {
                Fire.Why = "The flames are made in the air of the Volumetric Explosions mod, which is not installed: the game's own flames are left.";
                Fire.Log(Fire.Why);
            }
        }

        /// <summary>Whether the mod this one stands on is there to be asked. (Looked for by name, so that this mod loads without it.)</summary>
        internal static bool Installed()
        {
            foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetType("VolumetricExplosions.Sources", false) != null) return true;
            return false;
        }

        void OnDestroy()
        {
            Lights.Gone();
            Glow.Gone();
            if (hooked) { try { Feed.Unhook(Asked); } catch (Exception) { } }
            foreach (Plume p in plumes) Show(p);
            plumes.Clear();
            if (Fire.Page != null && Fire.Page.Panel == Panel) Fire.Page.Panel = null;
        }

        void Panel()
        {
#if DEV
            GUILayout.Label(Fire.Why.Length > 0 ? Fire.Why : Report + (Reentry.Report.Length > 0 ? " " + Reentry.Report : ""), Host.Small);
#else
            GUILayout.Label(Fire.Why.Length > 0 ? Fire.Why : Report, Host.Small);
#endif
        }

        static void Show(Plume p)
        {
            foreach (Renderer r in p.Hidden) if (r != null) r.forceRenderingOff = false;
            p.Hidden.Clear();
            p.HidAt = 0f;
        }

        void Find()
        {
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v == null || !v.loaded) continue;
                foreach (Part part in v.parts)
                {
                    if (part == null || part.Modules == null) continue;
                    for (int m = 0; m < part.Modules.Count; m++)
                    {
                        ModuleEngines engine = part.Modules[m] as ModuleEngines;
                        if (engine == null || known.Contains(engine) || engine.thrustTransforms == null || engine.thrustTransforms.Count == 0) continue;
                        Kind kind = Burns(engine, part.partInfo != null ? part.partInfo.name : null);
                        // (a jet is looked at again until the explosions mod is known to draw its afterburner: see KnowsJets)
                        if (kind == null && engine.engineType == EngineType.Turbine && !Feed.KnowsJets) continue;
                        known.Add(engine);
                        if (kind == null) continue;
                        int n = engine.thrustTransforms.Count;
                        for (int i = 0; i < n; i++)
                        {
                            if (engine.thrustTransforms[i] == null || !engine.thrustTransforms[i].gameObject.activeInHierarchy) continue;
                            float share = engine.thrustTransformMultipliers != null && i < engine.thrustTransformMultipliers.Count ? engine.thrustTransformMultipliers[i] : 1f / n;
                            float guess = Engines.Guess(engine, share);
                            // (a number of its own for every nozzle, the same from frame to frame)
                            int id = unchecked(((int)part.flightID * 31 + m) * 31 + i + 0x2f1a);
                            plumes.Add(new Plume { Engine = engine, Part = part, Nozzle = engine.thrustTransforms[i], Kind = kind, Share = share, Guess = guess, Radius = guess, Id = id });
                        }
                    }
                }
            }
            plumes.RemoveAll(p =>
            {
                if (p.Engine != null && p.Part != null && p.Nozzle != null && p.Part.vessel != null && p.Part.vessel.loaded) return false;
                Show(p);
                if (p.Engine != null) known.Remove(p.Engine);
                return true;
            });
            known.RemoveWhere(e => e == null);
        }

        /// <summary>
        /// The flames the game or Waterfall draws for this part: hidden while ours is shown. Waterfall's are whatever
        /// is drawn with one of its shaders. The game's own are particles, and so is its smoke, which is left: smoke
        /// is what has "smoke" in its name or in the name of what it hangs from, and every other particle effect of
        /// an engine is flame, sparks or shock.
        /// </summary>
        static void HideOthers(Plume p)
        {
            foreach (Renderer r in p.Part.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.forceRenderingOff) continue;
                Material m = r.sharedMaterial;
                bool theirs = m != null && m.shader != null && m.shader.name.StartsWith("Waterfall", StringComparison.Ordinal);
                if (!theirs && r is ParticleSystemRenderer)
                {
                    theirs = true;
                    for (Transform t = r.transform; t != null && t != p.Part.transform; t = t.parent)
                        if (t.name.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0) { theirs = false; break; }
                }
                if (!theirs) continue;
                r.forceRenderingOff = true;
                p.Hidden.Add(r);
            }
        }

        /// <summary>
        /// (Since 0.5.0.) How hard an engine's flame shows this moment, against how hard the engine is running: the game lights an
        /// engine at full thrust at once and puts it out at once; a real one does neither (Fire.Moments switches this off).
        ///
        /// Starting, a liquid engine's thrust builds over a second or so: the flame grows out of the bell. A kerosene engine is lit
        /// by a squirt of triethylaluminium and triethylborane, which burn by themselves in air with the green of the boron: the
        /// flash at the nozzles of a Merlin or an F-1 as it starts, a quarter to half a second, before the kerosene takes over
        /// (`tint`: the explosions mod's green fire, 10, where it has one). A solid motor's igniter fires down its whole bore at
        /// once: the flame comes out in a burst, brighter than it burns on (`flash`).
        /// Stopping, the propellant in the lines and the chamber burns away: a liquid engine's flame falls back into the bell in a
        /// third of a second; a solid motor tails off over a second and a half, the last of its grain burning out unevenly, in
        /// chuffs. Starving, an engine whose tanks are running dry (the game's own measure, how much of what it asks for it gets)
        /// gulps gas with its propellant: the flame flickers and stutters before it goes out.
        /// </summary>
        float Moment(Plume p, float running, float now, out int tint, out float flash)
        {
            tint = -1; flash = 1f;
            if (Fire.Moments != null && !Fire.Moments) return running;
            Fuel fuel = p.Kind.Fuel;
            bool solid = fuel == Fuel.Solid, jet = p.Kind == Afterburner;
            if (running > 0.02f)
            {
                float since = now - p.LitAt;
                if (since < 2f && !jet)
                {
                    if (solid) flash = 1f + 0.9f * Mathf.Exp(-since / 0.18f);
                    else
                    {
                        // (the flame grows out of the bell over the first second)
                        running *= Mathf.Lerp(0.25f, 1f, Mathf.SmoothStep(0f, 1f, since / 0.9f));
                        if (fuel == Fuel.Kerosene && since < 0.45f && Feed.KnowsJets) { tint = 10; flash = 1.25f; }
                    }
                }
                // (starving: the game gives it less than it asks for)
                float met = p.Engine.propellantReqMet;
#if DEV
                if (testMet >= 0f) met = testMet;
#endif
                if (met > 0f && met < 98f && !jet)
                {
                    float gulp = Mathf.PerlinNoise(now * 9f, p.Id * 0.017f), stutter = Mathf.PerlinNoise(now * 23f, p.Id * 0.031f + 5f);
                    float starve = 1f - met / 98f;
                    running *= Mathf.Lerp(1f, 0.15f + 0.85f * gulp * stutter, Mathf.Clamp01(starve * 3f));
                }
                return running;
            }
            // (stopped: the tail of it)
            float after = now - p.CutAt, tail = solid ? 1.5f : jet ? 0.2f : 0.35f;
            if (p.CutAt < p.LitAt || after > tail || p.CutFrom <= 0.02f) return 0f;
            float left = 1f - after / tail;
            if (solid)
            {
                // (a solid's burnout comes in chuffs: the last of its grain burning out unevenly)
                float chuff = 0.55f + 0.45f * Mathf.PerlinNoise(now * 7f, p.Id * 0.023f + 11f);
                return p.CutFrom * left * left * chuff;
            }
            return p.CutFrom * left * left * left;
        }

#if DEV
        static float testMet = -1f;
        /// <summary>(Development build: every engine taken as getting so many percent of the propellant it asks for, to see it starve; "go": as it is.)</summary>
        public static string Starve(string v) { testMet = v == "go" ? -1f : float.Parse(v, System.Globalization.CultureInfo.InvariantCulture); return testMet < 0f ? "as the game has it" : "starving at " + testMet + "%"; }
#endif
        /// <summary>Once a frame, when the air can take it: every running nozzle says what it is putting out.</summary>
        void Asked()
        {
            float now = Time.time;
            bool wanted = Fire.On != null && Fire.On;
            if (wanted && !Feed.Ready())
            {
                wanted = false;
                Report = "Volumetric Explosions cannot draw a volume here: the game's own flames are left.";
            }
            if (!wanted)
            {
                foreach (Plume p in plumes) if (p.Hidden.Count > 0) Show(p);
                if (Fire.On != null && !Fire.On) Report = "";
                Dark();
                return;
            }
            if (Time.realtimeSinceStartup > lookAt) { Find(); lookAt = Time.realtimeSinceStartup + 1f; Glow.Tidy(); }
            float glow = (float)Fire.Glow, longer = (float)Fire.Long, wider = (float)Fire.Wide;
            // (vacuum engines' bells heating and cooling, running or not: see Glow)
            if (Fire.Bells == null || Fire.Bells)
                foreach (Plume p in plumes)
                    if (p.Measured && p.Engine != null && p.Part != null && p.Nozzle != null) Glow.Step(p.Engine, p.Part, p.Nozzle, p.Radius, p.Rim, Engines.Running(p.Engine), Time.deltaTime);
            int burning = 0;
            foreach (Plume p in plumes)
            {
                // (a part that has just been blown up is still on the list until the list is next gone through)
                p.Burning = false;
                if (p.Engine == null || p.Part == null || p.Nozzle == null) { p.Running = 0f; continue; }
                float running = Engines.Running(p.Engine);
                Vessel v = p.Part.vessel;
                // (how the engine starts and stops, as the eye sees it: see Moment)
                bool lit = running > 0.02f;
                // (an engine already burning when the scene begins, as after a load in flight, was not just lit)
                if (lit && p.Shown <= 0.02f && now - p.CutAt > 0.6f) p.LitAt = Time.timeSinceLevelLoad < 3f ? -100f : now;
                if (!lit && p.Shown > 0.02f && p.CutAt < p.LitAt) { p.CutAt = now; p.CutFrom = p.Shown; }
                float shown = Moment(p, running, now, out int tint, out float flash);
                p.Running = running;
                p.Shown = lit ? running : 0f;
                if (shown <= 0.02f || v == null || v.packed)
                {
                    // (the game's own are given back a moment after it stops: the last of ours is still in the air)
                    if (p.Hidden.Count > 0 && now - p.Seen > 0.3f) Show(p);
                    continue;
                }
                running = shown;
                // (In a vacuum, where its owner has said no flames there, the game's own or Waterfall's, as without this mod: asked for by a
                // player whose ship's flame in orbit "looks weird with some rockets", 2026-10-12. Where the air is under four thousandths of
                // the ground's, as the explosions mod has a vacuum: some 31 km up on Kerbin.)
                if (Fire.Vacuum != null && !Fire.Vacuum && (v.mainBody == null || !v.mainBody.atmosphere || v.atmDensity < 0.0049))
                {
                    if (p.Hidden.Count > 0) Show(p);
                    continue;
                }
                if (!p.Measured)
                {
                    // (now, and not when the engine was found: by now whatever shrouded it has been cast off)
                    p.Measured = true;
                    try { if (!Engines.Mouth(p.Part, p.Nozzle, p.Engine.thrustTransforms, p.Guess, out p.Radius, out p.Rim)) { p.Radius = p.Guess; p.Rim = 0f; } }
                    catch (Exception) { p.Radius = p.Guess; p.Rim = 0f; }
                }
                Kind k = p.Kind;
                float nozzle = p.Radius * wider * k.Wide;
                Vector3 along = p.Nozzle.forward, rim = p.Nozzle.position + along * p.Rim;
                // (an engine throttled down has a shorter flame, and a fainter one; an afterburner's is some seven nozzles long)
                float full = k == Afterburner ? 14f * p.Radius : Engines.Flame(k.Fuel, p.Radius);
                p.Length = full * k.Long * longer * (0.3f + 0.7f * Mathf.Sqrt(running));
                float fire = (Feed.KnowsFuels ? k.Bright : k.Was) * glow * (0.3f + 0.7f * running) * flash;
                bool taken = Feed.Flame(p.Id, rim, along, v.srf_velocity, nozzle, p.Length, running, tint >= 0 ? tint : k.Tint, fire, v.transform);
                if (taken)
                {
                    burning++;
                    p.Seen = now;
                    p.Burning = true;
                    // (looked for again now and then: an effect can be made anew while the engine runs)
                    if (Fire.Hide && Time.realtimeSinceStartup > p.HidAt) { HideOthers(p); p.HidAt = Time.realtimeSinceStartup + 2f; }
                    else if (!Fire.Hide && p.Hidden.Count > 0) Show(p);
                }
                else if (p.Hidden.Count > 0 && now - p.Seen > 0.3f) Show(p);
            }
            Report = burning > 0 ? burning + " flame" + (burning == 1 ? " is" : "s are") + " burning." : plumes.Count > 0 ? "No engine is running." : "No rocket engine here.";
            Shine();
        }

#if DEV
        /// <summary>(For the development build: what is being drawn, in words.)</summary>
        public static string Stats()
        {
            Flames it = FindObjectOfType<Flames>();
            if (it == null) return "not running";
            var say = new System.Text.StringBuilder(Report + " " + Fire.Why);
            foreach (Plume p in it.plumes)
                say.Append("\n").Append(p.Part.partInfo != null ? p.Part.partInfo.name : p.Part.name).Append(": ").Append(p.Kind.Name).Append(", nozzle ").Append(p.Radius.ToString("F2")).Append(" m (guessed ").Append(p.Guess.ToString("F2"))
                   .Append("), rim ").Append(p.Rim.ToString("F2")).Append(" m on, running ").Append(p.Running.ToString("F2")).Append(", flame ").Append(p.Length.ToString("F1")).Append(" m, hides ").Append(p.Hidden.Count);
            return say.ToString();
        }

        /// <summary>
        /// (The vessel being flown held where it is, whatever its engines and gravity do: a flame can then be
        /// looked at for as long as it takes, in the air of any height. It is kept as a thing in the scene, not
        /// in this build's own memory: a newer build swapped in goes on holding.)
        /// </summary>
        public static string Hold()
        {
            GameObject was = GameObject.Find(HoldName);
            if (was != null) { DestroyImmediate(was); return "let go"; }
            new GameObject(HoldName);
            return "held where it is";
        }
        const string HoldName = "Engine Flames hold";
        bool held, aloft;
        float heldAsked;

        /// <summary>(Hold, bearing the craft's weight near the ground too: for a craft hovering over the ground on its engine, as one landing does. Called again, as Hold.)</summary>
        public static string HoldAloft()
        {
            GameObject was = GameObject.Find(HoldName + " aloft");
            if (was != null) { DestroyImmediate(was); return "its weight left to it near the ground again"; }
            new GameObject(HoldName + " aloft");
            return "its weight borne near the ground too";
        }

        void FixedUpdate()
        {
            if (Time.realtimeSinceStartup > heldAsked) { heldAsked = Time.realtimeSinceStartup + 0.5f; held = GameObject.Find(HoldName) != null; aloft = GameObject.Find(HoldName + " aloft") != null; }
            if (!held) return;
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.packed || !v.loaded) return;
            Vector3d push = Vector3d.zero;
            foreach (Part part in v.parts)
                foreach (ModuleEngines e in part.FindModulesImplementing<ModuleEngines>())
                    if (e != null && e.finalThrust > 0f && e.thrustTransforms != null)
                        for (int i = 0; i < e.thrustTransforms.Count; i++)
                            push -= (Vector3d)e.thrustTransforms[i].forward * (e.finalThrust * (e.thrustTransformMultipliers != null && i < e.thrustTransformMultipliers.Count ? e.thrustTransformMultipliers[i] : 1f / e.thrustTransforms.Count));
            // (near the ground its weight is left to it, so that it stays standing where it stands; in the air that is taken off it too)
            Vector3d gain = -push / Math.Max(v.totalMass, 0.01) - v.srf_velocity * 1.5;
            if (aloft || v.heightFromTerrain > 60f || v.heightFromTerrain < 0f) gain -= FlightGlobals.getGeeForceAtPosition(v.CoMD);
            foreach (Part part in v.parts) if (part.rb != null) part.rb.AddForce((Vector3)gain, ForceMode.Acceleration);
        }

        /// <summary>(The nozzles of the engines of the vessel being flown: where each is on its part, whether it is switched on, and its share of the thrust.)</summary>
        public static string Nozzles()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return "no vessel";
            var say = new System.Text.StringBuilder();
            foreach (Part part in v.parts)
                foreach (ModuleEngines e in part.FindModulesImplementing<ModuleEngines>())
                {
                    say.Append(part.partInfo.name).Append(" (").Append(e.engineID).Append("): ").Append(e.thrustTransforms.Count).Append(" named ").Append(e.thrustVectorTransformName).Append("\n");
                    for (int i = 0; i < e.thrustTransforms.Count; i++)
                    {
                        Transform t = e.thrustTransforms[i];
                        say.Append("  ").Append(part.transform.InverseTransformPoint(t.position).ToString("F2")).Append(t.gameObject.activeInHierarchy ? " on" : " off").Append(", under ").Append(t.parent != null ? t.parent.name : "nothing")
                           .Append(", share ").Append(i < e.thrustTransformMultipliers.Count ? e.thrustTransformMultipliers[i].ToString("F2") : "?").Append("\n");
                    }
                }
            return say.ToString();
        }

        /// <summary>
        /// (The camera put where a flame is best seen from, in the flame's own measure: "across=12 up=2 along=0 at=0.4" is
        /// 12 m to the flame's side and 2 m above a point four tenths of the way along it; "down=3" in place of "at" is a
        /// point 3 m along the jet from where the engine pushes, which is the same place whether this mod's flame is
        /// drawn or not. Whatever way up the game's camera thinks it is.)
        /// </summary>
        public static string View(string where)
        {
            Flames it = FindObjectOfType<Flames>();
            Vessel v = FlightGlobals.ActiveVessel;
            if (it == null || FlightCamera.fetch == null || v == null) return "not running";
            float across = 12f, up = 1.5f, along = 0f, at = 0.5f, down = float.NaN;
            foreach (string word in (where ?? "").Split(' '))
            {
                string[] pair = word.Split('=');
                if (pair.Length != 2 || !float.TryParse(pair[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float n)) continue;
                if (pair[0] == "across") across = n; else if (pair[0] == "up") up = n; else if (pair[0] == "along") along = n; else if (pair[0] == "at") at = n; else if (pair[0] == "down") down = n;
            }
            Transform nozzle = null;
            foreach (Plume p in it.plumes)
                if (p.Running > 0.02f && p.Part.vessel == v) { nozzle = p.Nozzle; if (float.IsNaN(down)) down = p.Rim + at * p.Length; break; }
            if (nozzle == null)
                foreach (Part part in v.parts)
                {
                    ModuleEngines engine = part.FindModuleImplementing<ModuleEngines>();
                    if (engine == null || engine.thrustTransforms == null) continue;
                    foreach (Transform t in engine.thrustTransforms) if (nozzle == null && t != null && t.gameObject.activeInHierarchy) nozzle = t;
                    if (nozzle != null) break;
                }
            if (nozzle == null) return "the vessel has no engine";
            if (float.IsNaN(down)) down = 3f;
            // (A mark of its own, of no size: the camera measures its distance in the measure of what it watches, and a flame's box
            // is not a metre each way. And a mark that is never destroyed: the game hangs its camera ON what it watches, so that
            // destroying that destroys the camera, and the flight with it, past what loading a quicksave mends.)
            if (it.mark == null) it.mark = GameObject.Find("Engine Flames camera mark") ?? new GameObject("Engine Flames camera mark");
            it.markOn = nozzle; it.markDown = down;
            Vector3 jet = nozzle.forward, sky = (nozzle.position - (Vector3)v.mainBody.position).normalized;
            Vector3 side = Vector3.Cross(sky, jet);
            if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(Vector3.Cross(sky, Vector3.right).normalized, jet);
            side.Normalize();
            Vector3 over = Vector3.Cross(jet, side);
            it.mark.transform.position = nozzle.position + jet * down;
            FlightCamera.fetch.SetTargetTransform(it.mark.transform);
            FlightCamera.fetch.SetCamCoordsFromPosition(it.mark.transform.position + side * across + over * up + jet * along);
            return "the camera is " + across + " m aside, " + up + " m over and " + along + " m along from a point " + down.ToString("F1") + " m down the jet of " + nozzle.GetComponentInParent<Part>().partInfo.name;
        }
        GameObject mark;
        Transform markOn;
        float markDown;
        void Update()
        {
            if (mark != null && markOn != null) mark.transform.position = markOn.position + markOn.forward * markDown;
        }

        /// <summary>(The game's own smoke hidden, or shown again: on a vessel that is held still it gathers round the flame and hides it.)</summary>
        public static string Smoke()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return "no vessel";
            int n = 0; bool hide = true;
            foreach (Part part in v.parts)
                foreach (ParticleSystemRenderer r in part.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    if (r.gameObject.name.ToLowerInvariant().Contains("smoke")) { if (n == 0) hide = !r.forceRenderingOff; r.forceRenderingOff = hide; n++; }
            return n + " smoke effects " + (hide ? "hidden" : "shown");
        }

        /// <summary>(The game's own effect models whose names say flame, and what each is made of: to check what would be hidden where Waterfall is not installed.)</summary>
        public static string Effects()
        {
            var say = new System.Text.StringBuilder();
            foreach (GameObject model in GameDatabase.Instance.databaseModel)
            {
                if (model == null || model.name.IndexOf("/FX/", StringComparison.OrdinalIgnoreCase) < 0) continue;
                say.Append(model.name).Append(":");
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t == model.transform) continue;
                    say.Append(" ").Append(t.name);
                    foreach (Component c in t.GetComponents<Component>()) if (c != null && !(c is Transform)) say.Append("[").Append(c.GetType().Name).Append("]");
                }
                say.Append("\n");
            }
            return say.ToString();
        }

        /// <summary>(The engines this mod draws a flame for that Waterfall has nothing for: on those the game's own flames can be seen being hidden.)</summary>
        public static string Plain()
        {
            var say = new System.Text.StringBuilder();
            foreach (AvailablePart known in PartLoader.LoadedPartsList)
            {
                Part part = known != null ? known.partPrefab : null;
                if (part == null) continue;
                bool engine = false, waterfall = false;
                foreach (PartModule module in part.Modules)
                {
                    if (module is ModuleEngines e && Burns(e, known.name) != null) engine = true;
                    if (module != null && module.moduleName == "ModuleWaterfallFX") waterfall = true;
                }
                if (engine && !waterfall) say.Append(known.name).Append(" (").Append(known.title).Append(")\n");
            }
            return say.Length > 0 ? say.ToString() : "every one has Waterfall's";
        }

        /// <summary>(Everything drawn that hangs from the engines of the vessel being flown and is not plainly solid: what it is called, what it hangs from, what draws it, and whether it is hidden now.)</summary>
        public static string Under()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return "no vessel";
            var say = new System.Text.StringBuilder();
            foreach (Part part in v.parts)
            {
                if (part.FindModuleImplementing<ModuleEngines>() == null) continue;
                say.Append(part.partInfo.name).Append(":\n");
                foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true))
                {
                    Material m = r.sharedMaterial;
                    if (!(r is ParticleSystemRenderer) && (m == null || m.renderQueue < 2500)) continue;
                    say.Append("  ").Append(r.GetType().Name).Append(" ").Append(r.name).Append(" < ").Append(r.transform.parent != null ? r.transform.parent.name : "").Append(" < ").Append(r.transform.parent != null && r.transform.parent.parent != null ? r.transform.parent.parent.name : "")
                       .Append(", ").Append(m != null && m.shader != null ? m.shader.name : "no shader").Append(r.forceRenderingOff ? ", HIDDEN" : "").Append(r.gameObject.activeInHierarchy ? "" : ", off").Append("\n");
                }
            }
            return say.ToString();
        }

        public static string WatchShip()
        {
            if (FlightCamera.fetch == null || FlightGlobals.ActiveVessel == null) return "not flying";
            FlightCamera.fetch.SetTargetVessel(FlightGlobals.ActiveVessel);
            return "watching the vessel";
        }

        /// <summary>(Fuel that does not run out, for looking at a flame as long as it takes. It is the game's own cheat, and lasts until the game is closed or this is called again.)</summary>
        public static string Endless()
        {
            CheatOptions.InfinitePropellant = !CheatOptions.InfinitePropellant;
            return "fuel that does not run out: " + (CheatOptions.InfinitePropellant ? "on" : "off");
        }

        /// <summary>(Every engine the game has, with its nozzles as guessed from its thrust and as measured from its model.)</summary>
        public static string Survey()
        {
            var say = new System.Text.StringBuilder();
            foreach (AvailablePart known in PartLoader.LoadedPartsList)
            {
                Part part = known != null ? known.partPrefab : null;
                if (part == null) continue;
                foreach (ModuleEngines engine in part.FindModulesImplementing<ModuleEngines>())
                {
                    Kind kind = Burns(engine, known.name);
                    say.Append(known.name).Append(" (").Append(known.title).Append("): ");
                    if (kind == null) { say.Append("left to the game\n"); continue; }
                    // (a pattern's nozzles are not found yet: by name, as the game finds them)
                    var nozzles = new List<Transform>(part.FindModelTransforms(engine.thrustVectorTransformName));
                    if (nozzles.Count == 0) { say.Append("no nozzle found\n"); continue; }
                    float guess = Engines.Guess(engine, 1f / nozzles.Count);
                    bool ok = Engines.Mouth(part, nozzles[0], nozzles, guess, out float radius, out float rim, true);
                    say.Append(kind.Name).Append(", ").Append(engine.maxThrust.ToString("F0")).Append(" kN, ").Append(nozzles.Count).Append(nozzles.Count == 1 ? " nozzle" : " nozzles").Append(", guessed ").Append(guess.ToString("F2"))
                       .Append(ok ? ", measured " + radius.ToString("F2") + " m, rim " + rim.ToString("F2") + " m on, ratio " + (radius / guess).ToString("F2") : ", not measured").Append("\n");
                }
            }
            return say.ToString();
        }
#endif
    }

    /// <summary>
    /// Everything that names the Volumetric Explosions mod. Kept apart, and in the bodies of methods only, so that
    /// this mod loads where that one is missing: nothing here is looked at until it is called.
    /// </summary>
    static class Feed
    {
        public static void Hook(Action asked) { VolumetricExplosions.Sources.Asked += asked; }
        public static void Unhook(Action asked) { VolumetricExplosions.Sources.Asked -= asked; }
        public static bool Ready() => VolumetricExplosions.Sources.Ready;

        /// <summary>Whether the explosions mod installed knows for itself how bright each kind of fire is (0.6.0 and later).</summary>
        public static bool KnowsFuels
        {
            get
            {
                if (knows == 0) { try { knows = Level() >= 2 ? 1 : -1; } catch (Exception) { knows = -1; } }
                return knows > 0;
            }
        }
        static int knows;
        static int Level() => VolumetricExplosions.Sources.Level;

        /// <summary>(Since 0.5.0.) Whether the explosions mod installed knows a jet's afterburner and a kerosene engine's green igniter (its fires 9 and 10: Level 5, its 0.8.0).</summary>
        public static bool KnowsJets
        {
            get
            {
                // (asked again now and then: a copy of the explosions mod swapped in, as the development build does, may be newer)
                if (jets == 0 || (jets < 0 && Time.realtimeSinceStartup > jetsAsked)) { jetsAsked = Time.realtimeSinceStartup + 5f; try { jets = Level() >= 5 ? 1 : -1; } catch (Exception) { jets = -1; } }
                return jets > 0;
            }
        }
        static int jets;
        static float jetsAsked;

        /// <summary>Whether the explosions mod installed knows a craft's re-entry glow (ExhaustKind.Plasma: 0.7.0 and later).</summary>
        public static bool KnowsPlasma
        {
            get
            {
                if (plasma == 0) { try { plasma = Level() >= 3 ? 1 : -1; } catch (Exception) { plasma = -1; } }
                return plasma > 0;
            }
        }
        static int plasma;

        /// <summary>A craft's re-entry glow (see Reentry): its face, the way the air goes past, how wide it is across the air, how far behind the face the flow closes in behind it (its neck), how far its wake glows.</summary>
        public static bool Plasma(int id, Vector3 face, Vector3 along, Vector3d going, float radius, float neck, float length, float running, float fire, Transform ship)
        {
            var exhaust = new VolumetricExplosions.Exhaust
            {
                id = id, rim = (Vector3d)face, along = (Vector3d)along, going = going, nozzle = radius, flame = length, running = running, reach = neck,
                // (the explosions mod's own number for this kind of fire, Site.PlasmaKind, and for this kind of source)
                tint = 8, fire = fire, rides = ship, kind = VolumetricExplosions.ExhaustKind.Plasma,
            };
            return VolumetricExplosions.Sources.Burning(exhaust);
        }

        public static bool Flame(int id, Vector3 rim, Vector3 along, Vector3d going, float nozzle, float length, float running, int tint, float fire, Transform ship)
        {
            var exhaust = new VolumetricExplosions.Exhaust
            {
                id = id, rim = (Vector3d)rim, along = (Vector3d)along, going = going, nozzle = nozzle, flame = length, running = running,
                tint = tint, fire = fire, rides = ship,
            };
            return VolumetricExplosions.Sources.Burning(exhaust);
        }
    }
}
