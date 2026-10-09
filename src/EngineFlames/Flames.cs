using System;
using System.Collections.Generic;
using Keystone;
using UnityEngine;

#if !DEV
[assembly: KSPAssembly("EngineFlames", 0, 2)]
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
    /// small upper-stage engines a thin blue-white; a solid booster brilliant; hydrazine nearly without
    /// light; hydrogen heated by a reactor a faint red.
    ///
    /// Each nozzle of each rocket engine gets one. How wide a nozzle is and where its rim is are measured
    /// from the engine's own model, so the flame leaves the bell it belongs to. While it is drawn, the
    /// flames the game or Waterfall draws for that engine are hidden, and its smoke is left. Jets, ion
    /// engines and anything it does not know are left as they are.
    ///
    /// Without Volumetric Explosions, or where that cannot draw a volume (it needs OpenGL: a Mac, Linux),
    /// this mod does nothing but say so.
    /// </summary>
    static class Fire
    {
        public const string Version = "0.2.0";

        public static Mod Page;
        public static Toggle On, Hide;
        public static Slider Glow, Long, Wide;
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
        }

        public static void Log(string text) => Kit.Log("Engine Flames", text);
    }

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public sealed class FlamesAtStart : MonoBehaviour
    {
        void Awake() { Fire.Setup(); Destroy(gameObject); }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class Flames : MonoBehaviour
    {
        /// <summary>
        /// What a kind of propellant burns like: which of the colours of fire that Volumetric Explosions knows
        /// (Tint: 0 fuel, 1 solid propellant, 2 monopropellant, 4 hydrogen, 5 methane, 6 the fuels that keep,
        /// 7 gas heated by a reactor), how bright against a kerosene engine's, and how long and wide against the usual.
        /// </summary>
        sealed class Kind
        {
            public Fuel Fuel;
            public string Name;
            public int Tint;
            public float Bright = 1f, Long = 1f, Wide = 1f;
        }

        static readonly Kind[] Kinds =
        {
            // Kerosene and oxygen: yellow-white at the heart, orange outside, and plenty of it.
            new Kind { Fuel = Fuel.Kerosene, Name = "kerosene and oxygen", Tint = 0, Bright = 1f },
            // Hydrogen and oxygen: nearly no light of its own, pale blue.
            new Kind { Fuel = Fuel.Hydrogen, Name = "hydrogen and oxygen", Tint = 4, Bright = 0.38f, Long = 1.1f },
            // Methane and oxygen: blue to violet.
            new Kind { Fuel = Fuel.Methane, Name = "methane and oxygen", Tint = 5, Bright = 0.6f },
            // Fuels that keep (the small engines of upper stages and landers): a thin clean flame.
            new Kind { Fuel = Fuel.Storable, Name = "fuels that keep", Tint = 6, Bright = 0.45f, Long = 0.9f },
            // Solid fuel: brilliant, thick with burning metal, wide and long.
            new Kind { Fuel = Fuel.Solid, Name = "solid fuel", Tint = 1, Bright = 1.7f, Wide = 1.1f },
            // Monopropellant: steam and nitrogen, all but without light.
            new Kind { Fuel = Fuel.Mono, Name = "monopropellant", Tint = 2, Bright = 0.14f, Long = 0.7f },
            // Fuel heated by a reactor: hot hydrogen and nothing burning, a faint red.
            new Kind { Fuel = Fuel.Reactor, Name = "fuel heated by a reactor", Tint = 7, Bright = 0.3f },
        };

        /// <summary>The flame of what an engine burns (which fuel each engine is taken as burning is Keystone's to say: see Keystone.Engines), or none.</summary>
        static Kind Burns(ModuleEngines engine, string part)
        {
            Fuel fuel = Engines.Burns(engine, part);
            foreach (Kind k in Kinds) if (k.Fuel == fuel) return k;
            return null;
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
            public bool Measured;
            public readonly List<Renderer> Hidden = new List<Renderer>();
        }

        readonly List<Plume> plumes = new List<Plume>();
        readonly HashSet<ModuleEngines> known = new HashSet<ModuleEngines>();
        float lookAt;
        bool hooked;
        public static string Report = "";

        void Awake()
        {
            Fire.Setup();
            Fire.Page.Panel = Panel;
            if (Installed())
            {
                try { Feed.Hook(Asked); hooked = true; }
                catch (Exception ex) { Fire.Why = "Volumetric Explosions is installed but is not a version this works with (" + ex.GetType().Name + "): the game's own flames are left."; Fire.Log(Fire.Why); }
            }
            else
            {
                Fire.Why = "The flames are made in the air of the Volumetric Explosions mod, which is not installed: the game's own flames are left.";
                Fire.Log(Fire.Why);
            }
        }

        /// <summary>Whether the mod this one stands on is there to be asked. (Looked for by name, so that this mod loads without it.)</summary>
        static bool Installed()
        {
            foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetType("VolumetricExplosions.Sources", false) != null) return true;
            return false;
        }

        void OnDestroy()
        {
            if (hooked) { try { Feed.Unhook(Asked); } catch (Exception) { } }
            foreach (Plume p in plumes) Show(p);
            plumes.Clear();
            if (Fire.Page != null && Fire.Page.Panel == Panel) Fire.Page.Panel = null;
        }

        void Panel()
        {
            GUILayout.Label(Fire.Why.Length > 0 ? Fire.Why : Report, Host.Small);
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
                        known.Add(engine);
                        Kind kind = Burns(engine, part.partInfo != null ? part.partInfo.name : null);
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

        /// <summary>Once a frame, when the air can take it: every running nozzle says what it is putting out.</summary>
        void Asked()
        {
            float now = Time.time;
            bool wanted = Fire.On != null && Fire.On;
            if (wanted && !Feed.Ready())
            {
                wanted = false;
                Report = "Volumetric Explosions cannot draw a volume here (it needs OpenGL: a Mac, Linux): the game's own flames are left.";
            }
            if (!wanted)
            {
                foreach (Plume p in plumes) if (p.Hidden.Count > 0) Show(p);
                if (Fire.On != null && !Fire.On) Report = "";
                return;
            }
            if (Time.realtimeSinceStartup > lookAt) { Find(); lookAt = Time.realtimeSinceStartup + 1f; }
            float glow = (float)Fire.Glow, longer = (float)Fire.Long, wider = (float)Fire.Wide;
            int burning = 0;
            foreach (Plume p in plumes)
            {
                // (a part that has just been blown up is still on the list until the list is next gone through)
                if (p.Engine == null || p.Part == null || p.Nozzle == null) { p.Running = 0f; continue; }
                float running = Engines.Running(p.Engine);
                Vessel v = p.Part.vessel;
                p.Running = running;
                if (running <= 0.02f || v == null || v.packed)
                {
                    // (the game's own are given back a moment after it stops: the last of ours is still in the air)
                    if (p.Hidden.Count > 0 && now - p.Seen > 0.3f) Show(p);
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
                // (an engine throttled down has a shorter flame, and a fainter one)
                p.Length = Engines.Flame(k.Fuel, p.Radius) * k.Long * longer * (0.3f + 0.7f * Mathf.Sqrt(running));
                float fire = k.Bright * glow * (0.3f + 0.7f * running);
                bool taken = Feed.Flame(p.Id, rim, along, v.srf_velocity, nozzle, p.Length, running, k.Tint, fire, v.transform);
                if (taken)
                {
                    burning++;
                    p.Seen = now;
                    // (looked for again now and then: an effect can be made anew while the engine runs)
                    if (Fire.Hide && Time.realtimeSinceStartup > p.HidAt) { HideOthers(p); p.HidAt = Time.realtimeSinceStartup + 2f; }
                    else if (!Fire.Hide && p.Hidden.Count > 0) Show(p);
                }
                else if (p.Hidden.Count > 0 && now - p.Seen > 0.3f) Show(p);
            }
            Report = burning > 0 ? burning + " flame" + (burning == 1 ? " is" : "s are") + " burning." : plumes.Count > 0 ? "No engine is running." : "No rocket engine here.";
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
        bool held;
        float heldAsked;

        void FixedUpdate()
        {
            if (Time.realtimeSinceStartup > heldAsked) { heldAsked = Time.realtimeSinceStartup + 0.5f; held = GameObject.Find(HoldName) != null; }
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
            if (v.heightFromTerrain > 60f || v.heightFromTerrain < 0f) gain -= FlightGlobals.getGeeForceAtPosition(v.CoMD);
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
