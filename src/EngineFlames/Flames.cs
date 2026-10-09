using System;
using System.Collections.Generic;
using System.IO;
using Keystone;
using UnityEngine;
using UnityEngine.Rendering;

#if !DEV
[assembly: KSPAssembly("EngineFlames", 0, 1)]
[assembly: KSPAssemblyDependency("Keystone", 0, 4)]
#endif

namespace EngineFlames
{
    /// <summary>
    /// Engine Flames: a rocket engine's exhaust drawn as a true volume of glowing gas.
    ///
    /// Asked for as: "add better fire, more realistic, more beautiful, more cinematic", and, asked which
    /// fire: "engine flames".
    ///
    /// The game draws a flame as flat pictures of fire flung out of the nozzle (or, with the Waterfall mod,
    /// as a few thin shells with a moving picture on them). Here the flame is a volume: for every pixel the
    /// line of sight is walked through it, and the light of the gas along it is added up and what the gas
    /// hides is taken off (see tools/shaderpack/flame.glsl). So it has depth, looks right from any side and
    /// from inside, is brightest where the sight line goes through most of it, and stops at whatever is in
    /// front. Nothing of it is a picture: what the gas is like at a place is worked out from where the
    /// place is.
    ///
    /// What makes a real flame look as it does is mostly the air:
    /// * At sea level the air holds the jet to a narrow column, and squeezes it: it springs out and is
    ///   squeezed again, which shows as a row of bright diamonds down its middle. It goes on burning in
    ///   the air it draws in, its edge is torn, and it ends in tongues.
    /// * As the air thins the jet opens and pales: by twenty kilometres it is a cone, and in a vacuum a
    ///   bright streak inside a wide faint skirt, smooth, with nothing to tear it.
    /// * Where it strikes the ground it is turned, and runs out along the ground as a sheet of flame.
    /// and what is burning. The game has one fuel for every liquid engine; its engines are drawn as burning
    /// what the engines they were modelled on burn (see Stock): kerosene yellow-white with an orange,
    /// sooty edge; hydrogen pale blue and all diamonds; the small upper-stage engines' fuels that keep, a
    /// thin pale orange; a solid booster brilliant and thick; hydrazine alone nearly without light;
    /// hydrogen heated by a reactor a faint red. A config can say otherwise for any part (see Told).
    ///
    /// Each nozzle of each rocket engine gets one. How wide a nozzle is and where its rim is are measured
    /// from the engine's own model (see Measure), so the flame leaves the bell it belongs to. While it is
    /// drawn, the flames the game or Waterfall draws for that engine are hidden, and its smoke is left.
    /// Jets, ion engines and anything it does not know are left as they are.
    ///
    /// The shader is packed for the game's OpenGL (a Mac, Linux). On Direct3D (Windows) there is none yet,
    /// and the mod does nothing there but say so.
    /// </summary>
    static class Fire
    {
        public const string Version = "0.1.0";

        public static Mod Page;
        public static Toggle On, Hide, Diamonds, Ground;
        public static Slider Glow, Long, Wide, Rough;
        public static Shader Flame;
        public static string Why = "";
        static bool looked;

        public static void Setup()
        {
            if (Page != null && Kit.Find("Engine Flames") == Page) return;
            Page = Kit.Register("Engine Flames", Version, "A rocket engine's exhaust as a true volume of glowing gas: shaped by the air it is in, coloured by what it burns, and spreading over the ground it strikes.");
            On = Page.Toggle("on", "Engine flames", true, "Off: the flames the game (or the Waterfall mod) draws.");
            Glow = Page.Slider("glow", "How bright", 1f, 0.2f, 3f, "", 2, "How bright the flames are.");
            Long = Page.Slider("long", "How long", 1f, 0.4f, 2.5f, "", 2, "How long the flames are.");
            Wide = Page.Slider("wide", "How wide", 1f, 0.5f, 2f, "", 2, "How wide the flames are. A nozzle's width is measured from the engine's model (or, where that cannot be done, worked out from its thrust); this corrects it.");
            Rough = Page.Slider("rough", "How torn", 1f, 0f, 2.5f, "", 2, "How much the edge of a flame is torn by the air. 0: smooth.");
            Diamonds = Page.Toggle("diamonds", "Shock diamonds", true, "The row of bright diamonds down the middle of a flame in thick air.");
            Ground = Page.Toggle("ground", "Flame along the ground", true, "Where a flame strikes the ground or the sea it runs out along it. Off: it stops there.");
            Hide = Page.Toggle("hide", "Hide the game's own flames", true, "While a flame of this mod's is drawn for an engine, the flames the game or Waterfall draws for it are hidden. Smoke is left.");
        }

        public static void Log(string text) => Kit.Log("Engine Flames", text);

        /// <summary>The flame's shader: from the mod's own file on OpenGL; none elsewhere yet.</summary>
        public static Shader Find()
        {
            if (looked) return Flame;
            looked = true;
            try
            {
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLCore) { Why = "There is no flame shader for " + SystemInfo.graphicsDeviceType + " yet (it is made for OpenGL: a Mac, Linux): the game's own flames are left."; return null; }
                string path = KSPUtil.ApplicationRootPath + "GameData/EngineFlames/PluginData/flame.bundle";
                if (!File.Exists(path)) { Why = "The flame shader (GameData/EngineFlames/PluginData/flame.bundle) is missing."; return null; }
                AssetBundle bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null) { Why = "The flame shader did not load."; return null; }
                Shader[] found = bundle.LoadAllAssets<Shader>();
                bundle.Unload(false);
                foreach (Shader one in found) if (one != null && one.name == "EngineFlames/Flame" && one.isSupported) Flame = one;
                if (Flame == null) Why = "This graphics card cannot run the flame shader.";
            }
            catch (Exception ex) { Why = "The flame shader did not load (" + ex.Message + ")."; }
            if (Why.Length > 0) Log(Why);
            return Flame;
        }
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
        /// What a kind of propellant burns like: its colours from hottest to coolest (as light: 1 is white paper in
        /// the sun), and what is its own about its shape.
        /// </summary>
        sealed class Kind
        {
            public string Key, Name;
            public Color Hot, Mid, Cool;
            /// <summary>What its fainter light goes over to as the air thins: with no air to burn in or to hold it together, the wide skirt of the jet is only thin gas glowing.</summary>
            public Color Thin = new Color(0.7f, 0.62f, 0.72f);
            /// <summary>Wide, Long: against the usual. Rough: how torn its edge is. Lumpy: how much its light comes in lumps. Hides: how much it hides what is behind it.</summary>
            public float Wide = 1f, Long = 1f, Rough = 1f, Lumpy = 0.5f, Hides;
            /// <summary>Diamonds: how strong its shock diamonds are. Pinch: how far its edge necks in between them. Tail: how its light falls off along it (a power). Thins: how fast its light thins as it widens (a power). Dark: how far out it lights, in nozzle radii.</summary>
            public float Diamonds = 1f, Pinch = 0.1f, Tail = 1.0f, Thins = 1.3f, Dark;
        }

        // Kerosene and oxygen: yellow-white at the heart, orange and sooty outside; it leaves the nozzle dark and lights where the air gets in.
        static readonly Kind Kerosene = new Kind { Key = "kerosene", Name = "kerosene and oxygen", Hot = new Color(7.5f, 6.3f, 4.2f), Mid = new Color(4.6f, 2.3f, 0.62f), Cool = new Color(2.0f, 0.62f, 0.12f), Thin = new Color(0.9f, 0.7f, 0.58f), Hides = 0.3f, Lumpy = 0.75f, Dark = 1.1f, Diamonds = 0.6f };
        // Hydrogen and oxygen: nearly no light of its own, pale blue, its diamonds sharp and plain to see.
        static readonly Kind Hydrogen = new Kind { Key = "hydrogen", Name = "hydrogen and oxygen", Hot = new Color(1.7f, 2.3f, 3.7f), Mid = new Color(0.42f, 0.7f, 1.5f), Cool = new Color(0.08f, 0.17f, 0.5f), Thin = new Color(0.3f, 0.5f, 1.0f), Rough = 0.6f, Lumpy = 0.2f, Diamonds = 1.7f, Pinch = 0.14f };
        // Methane and oxygen: blue to violet, pink where it is hottest.
        static readonly Kind Methane = new Kind { Key = "methane", Name = "methane and oxygen", Hot = new Color(3.6f, 3.0f, 4.4f), Mid = new Color(1.0f, 0.55f, 1.6f), Cool = new Color(0.3f, 0.14f, 0.6f), Thin = new Color(0.5f, 0.4f, 1.0f), Rough = 0.7f, Lumpy = 0.3f, Diamonds = 1.5f, Pinch = 0.13f };
        // Fuels that keep (the small engines of upper stages and landers): a thin clean flame, blue-white to violet, all but nothing in a vacuum.
        static readonly Kind Storable = new Kind { Key = "storable", Name = "fuels that keep", Hot = new Color(2.5f, 2.7f, 3.4f), Mid = new Color(0.75f, 0.72f, 1.35f), Cool = new Color(0.26f, 0.2f, 0.55f), Thin = new Color(0.5f, 0.5f, 0.85f), Rough = 0.7f, Lumpy = 0.4f, Hides = 0.04f, Diamonds = 0.8f };
        // Solid fuel: brilliant, thick with burning metal, wide and long and torn; no diamonds to be seen in it.
        static readonly Kind Solid = new Kind { Key = "solid", Name = "solid fuel", Hot = new Color(9.5f, 8.4f, 6.2f), Mid = new Color(4.6f, 2.7f, 0.9f), Cool = new Color(1.8f, 0.6f, 0.14f), Thin = new Color(1.6f, 1.2f, 0.8f), Wide = 1.3f, Long = 1.15f, Rough = 1.7f, Lumpy = 0.9f, Hides = 1.4f, Diamonds = 0.12f, Pinch = 0.03f, Tail = 0.85f, Thins = 1.1f };
        // Monopropellant: steam and nitrogen, all but without light.
        static readonly Kind Mono = new Kind { Key = "mono", Name = "monopropellant", Hot = new Color(1.3f, 1.4f, 1.5f), Mid = new Color(0.45f, 0.5f, 0.6f), Cool = new Color(0.14f, 0.16f, 0.2f), Thin = new Color(0.5f, 0.55f, 0.65f), Long = 0.7f, Rough = 0.6f, Lumpy = 0.2f, Diamonds = 0.5f };
        // Fuel heated by a reactor: hot hydrogen and nothing burning, a faint red.
        static readonly Kind Reactor = new Kind { Key = "reactor", Name = "fuel heated by a reactor", Hot = new Color(1.9f, 1.2f, 0.9f), Mid = new Color(0.8f, 0.3f, 0.2f), Cool = new Color(0.3f, 0.07f, 0.05f), Thin = new Color(0.8f, 0.3f, 0.35f), Long = 1.1f, Rough = 0.6f, Lumpy = 0.2f, Diamonds = 0.6f };
        static readonly Kind[] Kinds = { Kerosene, Hydrogen, Methane, Storable, Solid, Mono, Reactor };

        /// <summary>
        /// The game has one fuel for every rocket engine, and its engines are plainly not all alike: so which
        /// of the real fuels each of the game's own engines is drawn as burning is set down here, by the
        /// engine it was modelled on. (Anyone's config can say otherwise, or say it for other engines: see Told.)
        /// </summary>
        static readonly Dictionary<string, Kind> Stock = new Dictionary<string, Kind>
        {
            { "liquidEngine", Kerosene }, { "liquidEngine.v2", Kerosene }, { "liquidEngine2", Kerosene }, { "liquidEngine2.v2", Kerosene }, { "liquidEngineMainsail.v2", Kerosene },
            { "Size2LFB", Kerosene }, { "Size2LFB.v2", Kerosene }, { "LiquidEngineKE-1", Kerosene }, { "LiquidEngineRK-7", Kerosene }, { "LiquidEngineRV-1", Kerosene },
            { "SSME", Hydrogen }, { "Size3EngineCluster", Hydrogen }, { "Size3AdvancedEngine", Hydrogen }, { "engineLargeSkipper.v2", Hydrogen }, { "LiquidEngineRE-I2", Hydrogen },
            { "toroidalAerospike", Hydrogen }, { "RAPIER", Hydrogen },
            { "liquidEngine3.v2", Storable }, { "liquidEngine2-2.v2", Storable }, { "liquidEngineMini.v2", Storable }, { "microEngine.v2", Storable }, { "radialEngineMini.v2", Storable },
            { "smallRadialEngine", Storable }, { "smallRadialEngine.v2", Storable }, { "radialLiquidEngine1-2", Storable }, { "LiquidEngineRE-J10", Storable }, { "LiquidEngineLV-T91", Storable },
            { "LiquidEngineLV-TX87", Storable },
        };

        /// <summary>What configs say: ENGINE_FLAMES { ENGINE { part = the part's name; flame = kerosene, hydrogen, methane, storable, solid, mono, reactor, or none } }.</summary>
        static Dictionary<string, Kind> told;
        static Dictionary<string, Kind> Told()
        {
            if (told != null) return told;
            told = new Dictionary<string, Kind>();
            try
            {
                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("ENGINE_FLAMES"))
                    foreach (ConfigNode one in node.GetNodes("ENGINE"))
                    {
                        string part = (one.GetValue("part") ?? "").Trim().Replace('_', '.'), flame = (one.GetValue("flame") ?? "").Trim().ToLowerInvariant();
                        if (part.Length == 0) continue;
                        Kind kind = null;
                        foreach (Kind k in Kinds) if (k.Key == flame) kind = k;
                        if (kind == null && flame != "none") { Fire.Log("A config names a flame \"" + flame + "\" for " + part + ": there is none of that name."); continue; }
                        told[part] = kind;
                    }
            }
            catch (Exception ex) { Fire.Log("The configs' ENGINE_FLAMES could not be read: " + ex.Message); }
            return told;
        }

        sealed class Plume
        {
            public ModuleEngines Engine;
            public Part Part;
            public Transform Nozzle;
            public Kind Kind;
            public float Guess, Radius, Rim, Share, Own, Shown, LitAt = -100f, HidAt;
            public bool Measured;
            public GameObject Box;
            public MeshRenderer Draws;
            public readonly List<Renderer> Hidden = new List<Renderer>();
        }

        readonly List<Plume> plumes = new List<Plume>();
        readonly HashSet<ModuleEngines> known = new HashSet<ModuleEngines>();
        static readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        static readonly float[] edges = new float[13];
        static readonly int size = Shader.PropertyToID("_FlameSize"), shape = Shader.PropertyToID("_FlameShape"), air = Shader.PropertyToID("_FlameAir"), look = Shader.PropertyToID("_FlameLook"), more = Shader.PropertyToID("_FlameMore"), boxed = Shader.PropertyToID("_FlameBox"), ground = Shader.PropertyToID("_FlameGround"), cone = Shader.PropertyToID("_FlameCone"),
                            hot = Shader.PropertyToID("_FlameHot"), mid = Shader.PropertyToID("_FlameMid"), cool = Shader.PropertyToID("_FlameCool"), cameraNote = Shader.PropertyToID("_FlameCamera");
        Material paint;
        Mesh cube;
        float lookAt;
        int drawn;
        public static string Report = "";

        void Awake()
        {
            Fire.Setup();
            Fire.Page.Panel = Panel;
            Camera.onPreRender += ForCamera;
        }

        void OnDestroy()
        {
            Camera.onPreRender -= ForCamera;
            foreach (Plume p in plumes) Drop(p);
            plumes.Clear();
            if (paint != null) Destroy(paint);
            if (cube != null) Destroy(cube);
            if (Fire.Page != null && Fire.Page.Panel == Panel) Fire.Page.Panel = null;
        }

        void Panel()
        {
            GUILayout.Label(Fire.Why.Length > 0 ? Fire.Why : Report, Host.Small);
        }

        /// <summary>Each camera is told whether it has a picture of how far off everything is; and while flames are drawn, the game's two flight cameras are asked to keep one.</summary>
        void ForCamera(Camera camera)
        {
            if (drawn > 0 && (camera.depthTextureMode & DepthTextureMode.Depth) == 0 && (camera.name == "Camera 00" || camera.name == "Camera 01")) camera.depthTextureMode |= DepthTextureMode.Depth;
            Shader.SetGlobalVector(cameraNote, new Vector4((camera.depthTextureMode & DepthTextureMode.Depth) != 0 ? 1f : 0f, 0f, 0f, 0f));
        }

        static Mesh Cube()
        {
            GameObject made = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh mesh = Instantiate(made.GetComponent<MeshFilter>().sharedMesh);
            Destroy(made);
            mesh.name = "Engine Flames box";
            return mesh;
        }

        void Drop(Plume p)
        {
            foreach (Renderer r in p.Hidden) if (r != null) r.forceRenderingOff = false;
            p.Hidden.Clear();
            if (p.Box != null) Destroy(p.Box);
            p.Box = null;
        }

        /// <summary>
        /// What an engine is drawn as burning: what a config says of its part; or by what it takes in, and for
        /// the game's one rocket fuel by which engine it is; or nothing, for the kinds left to the game (jets,
        /// ion engines, anything unknown).
        /// </summary>
        static Kind Burns(ModuleEngines engine, string part)
        {
            if (part != null && Told().TryGetValue(part, out Kind said)) return said;
            bool fuel = false, oxidizer = false, solid = false, mono = false, hydrogen = false, methane = false, kerosene = false, keeps = false, other = false;
            foreach (Propellant p in engine.propellants)
            {
                switch (p.name)
                {
                    case "LiquidFuel": fuel = true; break;
                    case "Oxidizer": case "LqdOxygen": case "LOX": oxidizer = true; break;
                    case "SolidFuel": case "HTPB": case "PBAN": solid = true; break;
                    case "MonoPropellant": case "Hydrazine": case "HTP": mono = true; break;
                    case "LqdHydrogen": case "LH2": hydrogen = true; break;
                    case "LqdMethane": case "Methane": case "Methalox": methane = true; break;
                    case "Kerosene": case "RP-1": kerosene = true; break;
                    case "Aerozine50": case "MMH": case "UDMH": case "NTO": case "MON3": keeps = true; break;
                    case "ElectricCharge": break;
                    default: other = true; break;                    // (air for a jet, xenon, anything unknown)
                }
            }
            if (other) return null;
            if (solid) return Solid;
            if (hydrogen) return oxidizer ? Hydrogen : Reactor;
            if (methane) return Methane;
            if (kerosene) return Kerosene;
            if (keeps) return Storable;
            if (fuel && oxidizer)
            {
                if (part != null && Stock.TryGetValue(part, out Kind known)) return known;
                // (an engine nobody has named: one made for a vacuum is taken for a small upper-stage engine)
                float vacuum = engine.atmosphereCurve != null ? engine.atmosphereCurve.Evaluate(0f) : 300f, sea = engine.atmosphereCurve != null ? engine.atmosphereCurve.Evaluate(1f) : 250f;
                return sea < vacuum * 0.6f ? Storable : Kerosene;
            }
            if (mono && !fuel) return Mono;
            if (fuel) return Reactor;
            return null;
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
                        // A nozzle's radius, from how hard it pushes: the push is the pressure at its mouth times the mouth's area, more
                        // or less, so the radius goes as the root of the thrust; and an engine made for a vacuum (much better there than
                        // at sea level) has a wider mouth for the same push.
                        float vacuum = engine.atmosphereCurve != null ? engine.atmosphereCurve.Evaluate(0f) : 300f, sea = engine.atmosphereCurve != null ? Mathf.Max(engine.atmosphereCurve.Evaluate(1f), 1f) : 250f;
                        float bell = Mathf.Clamp(Mathf.Sqrt(vacuum / sea) / 1.06f, 1f, 2.2f);
                        for (int i = 0; i < n; i++)
                        {
                            if (engine.thrustTransforms[i] == null || !engine.thrustTransforms[i].gameObject.activeInHierarchy) continue;
                            float share = engine.thrustTransformMultipliers != null && i < engine.thrustTransformMultipliers.Count ? engine.thrustTransformMultipliers[i] : 1f / n;
                            float guess = Mathf.Clamp(Rule * Mathf.Sqrt(Mathf.Max(engine.maxThrust * share, 0.05f)) * bell, 0.03f, 4f);
                            plumes.Add(new Plume { Engine = engine, Part = part, Nozzle = engine.thrustTransforms[i], Kind = kind, Share = share, Guess = guess, Radius = guess, Own = UnityEngine.Random.value * 40f });
                        }
                    }
                }
            }
            plumes.RemoveAll(p =>
            {
                if (p.Engine != null && p.Part != null && p.Nozzle != null && p.Part.vessel != null && p.Part.vessel.loaded) return false;
                Drop(p);
                if (p.Engine != null) known.Remove(p.Engine);
                return true;
            });
            known.RemoveWhere(e => e == null);
        }

        /// <summary>The ground, the buildings on it and the sea: what a jet is turned by. (Not other craft, nor the craft's own parts.)</summary>
        const int GroundLayers = 1 << 15;

        /// <summary>Where a jet meets the ground within its length, if it does: how far along it, and which way the ground faces there.</summary>
        static bool Meets(Vessel v, Vector3 from, Vector3 along, float length, out float far, out Vector3 faces)
        {
            far = 0f; faces = -along;
            if (v == null || v.mainBody == null) return false;
            // (only near the ground is there any to meet)
            if (v.heightFromTerrain > length + 60f && v.altitude > length + 60.0) return false;
            bool met = false;
            if (Physics.Raycast(from, along, out RaycastHit hit, length, GroundLayers, QueryTriggerInteraction.Ignore)) { met = true; far = hit.distance; faces = hit.normal; }
            if (v.mainBody.ocean)
            {
                // (the sea has no surface to strike in the game's physics: it is where the jet's line comes down to sea level)
                Vector3 up = (from - (Vector3)v.mainBody.position).normalized;
                double high = FlightGlobals.getAltitudeAtPos((Vector3d)from, v.mainBody);
                float down = -Vector3.Dot(along, up);
                if (high > 0.0 && down > 0.05f)
                {
                    float sea = (float)(high / down);
                    if (sea < length && (!met || sea < far)) { met = true; far = sea; faces = up; }
                }
            }
            return met;
        }

        /// <summary>A nozzle's radius in metres for each root of a kilonewton of thrust, where the model cannot be measured.</summary>
        const float Rule = 0.0245f;
        static readonly List<Vector3> corners = new List<Vector3>();

        /// <summary>
        /// The mouth of a nozzle, measured from the engine's own model: how far along the jet's line the model
        /// reaches near that line (the rim of the bell), and how wide it is there. The flame begins at the rim
        /// and is as wide as it. Where the model cannot be read, or gives something a nozzle could not be,
        /// the guess from the thrust stands and the flame begins where the engine pushes from.
        /// </summary>
        static bool Measure(Part part, Transform nozzle, IList<Transform> all, float guess, out float radius, out float rim, bool whole = false)
        {
            radius = guess; rim = 0f;
            Vector3 from = nozzle.position, along = nozzle.forward;
            // (what is this nozzle's and not its neighbour's: within reach of its line)
            float reach = guess * 3.4f;
            if (all != null)
                foreach (Transform other in all)
                {
                    // (a nozzle that is switched off belongs to another look of the engine, and is not there)
                    if (other == null || other == nozzle || (!whole && !other.gameObject.activeInHierarchy)) continue;
                    Vector3 to = other.position - from;
                    float apart = (to - along * Vector3.Dot(to, along)).magnitude;
                    if (apart > 0.05f) reach = Mathf.Min(reach, apart * 0.5f);
                }
            var cast = new List<Transform>();
            foreach (ModuleJettison shroud in part.FindModulesImplementing<ModuleJettison>())
            {
                if (shroud == null) continue;
                if (shroud.jettisonTransform != null) cast.Add(shroud.jettisonTransform);
                foreach (string name in (shroud.jettisonName ?? "").Split(','))
                    if (name.Trim().Length > 0) cast.AddRange(part.FindModelTransforms(name.Trim()));
            }
            float far = float.NegativeInfinity;
            var seen = new List<Vector2>();
            foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>(whole))
            {
                Mesh mesh = filter.sharedMesh;
                Renderer drawn = filter.GetComponent<Renderer>();
                if (mesh == null || !mesh.isReadable || drawn == null || !drawn.enabled) continue;
                // (the engine itself is solid: flames, glows and such that others have hung on it are not)
                Material m = drawn.sharedMaterial;
                if (m == null || m.renderQueue >= 2500 || (m.shader != null && m.shader.name.StartsWith("Waterfall", StringComparison.Ordinal))) continue;
                bool shed = false;
                foreach (Transform t in cast) if (filter.transform.IsChildOf(t)) shed = true;
                if (shed) continue;
                if (whole)
                {
                    // (a part that is only a pattern, not in the world: what is switched off in it is found by hand)
                    bool off = false;
                    for (Transform t = filter.transform; t != null && t != part.transform; t = t.parent) if (!t.gameObject.activeSelf) off = true;
                    if (off) continue;
                }
                Matrix4x4 place = filter.transform.localToWorldMatrix;
                mesh.GetVertices(corners);
                for (int i = 0; i < corners.Count; i++)
                {
                    Vector3 at = place.MultiplyPoint3x4(corners[i]) - from;
                    float z = Vector3.Dot(at, along), r = (at - along * z).magnitude;
                    if (r > reach) continue;
                    seen.Add(new Vector2(z, r));
                    if (z > far) far = z;
                }
            }
            corners.Clear();
            if (seen.Count < 8) return false;
            float slab = Mathf.Max(0.02f, guess * 0.1f), widest = 0f;
            foreach (Vector2 one in seen) if (one.x > far - slab && one.y > widest) widest = one.y;
            if (widest < guess * 0.2f || widest > guess * 2.2f || far < -guess * 4f || far > guess * 10f) return false;
            radius = widest; rim = far;
            return true;
        }

        /// <summary>
        /// The flames the game or Waterfall draws for this part: hidden while ours is shown. Waterfall's are whatever
        /// is drawn with one of its shaders. The game's own are particles, and so is its smoke, which is left: smoke
        /// is what has "smoke" in its name or in the name of what it hangs from, and every other particle effect of
        /// an engine is flame, sparks or shock.
        /// </summary>
        void HideOthers(Plume p)
        {
            foreach (Renderer r in p.Part.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r == p.Draws || r.forceRenderingOff) continue;
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

        void LateUpdate()
        {
            bool wanted = Fire.On != null && Fire.On && Fire.Find() != null;
            if (!wanted)
            {
                if (plumes.Count > 0) { foreach (Plume p in plumes) Drop(p); plumes.Clear(); known.Clear(); }
                drawn = 0;
                Report = "";
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (now > lookAt) { Find(); lookAt = now + 1.0f; }
            if (paint == null) paint = new Material(Fire.Flame) { renderQueue = 3050, name = "Engine Flames" };
            if (cube == null) cube = Cube();

            float glow = (float)Fire.Glow, longer = (float)Fire.Long, wider = (float)Fire.Wide, rough = (float)Fire.Rough;
            float ease = 1f - Mathf.Exp(-Time.deltaTime * 9f);
            // (The shader's clock is begun again every ten minutes. It multiplies it by the speed of the gas, and after some hours
            // of play the product would be too large a number to move smoothly: the flames would begin to jerk. Beginning again
            // changes every lump at once, for one frame in ten minutes.)
            float clock = Time.time % 600f;
            int showing = 0;
            foreach (Plume p in plumes)
            {
                ModuleEngines e = p.Engine;
                // (how hard it runs is how much it burns of what it could: a vacuum engine in thick air pushes little, and burns as much as ever)
                float running = 0f;
                if (e.EngineIgnited && !e.flameout && e.finalThrust > 0f && e.maxThrust > 0f)
                {
                    float best = e.atmosphereCurve != null ? e.atmosphereCurve.Evaluate(0f) : 0f;
                    running = Mathf.Clamp01(e.finalThrust / e.maxThrust * (best > 0f && e.realIsp > 0f ? best / e.realIsp : 1f));
                }
                // (a flame does not go out in an instant, nor come on in one; and when it comes on, that is remembered: see below)
                if (running > 0.05f && p.Shown < 0.012f) p.LitAt = Time.time;
                p.Shown = Mathf.Lerp(p.Shown, running, ease);
                if (p.Shown < 0.012f)
                {
                    if (p.Box != null && p.Box.activeSelf) { p.Box.SetActive(false); foreach (Renderer r in p.Hidden) if (r != null) r.forceRenderingOff = false; p.Hidden.Clear(); p.HidAt = 0f; }
                    continue;
                }
                if (p.Box == null)
                {
                    p.Box = new GameObject("Engine Flames flame") { layer = 1 };
                    p.Box.AddComponent<MeshFilter>().sharedMesh = cube;
                    p.Draws = p.Box.AddComponent<MeshRenderer>();
                    p.Draws.sharedMaterial = paint;
                    p.Draws.shadowCastingMode = ShadowCastingMode.Off;
                    p.Draws.receiveShadows = false;
                    p.Draws.lightProbeUsage = LightProbeUsage.Off;
                    p.Draws.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                if (!p.Measured)
                {
                    // (now, and not when the engine was found: by now whatever shrouded it has been cast off)
                    p.Measured = true;
                    try { if (!Measure(p.Part, p.Nozzle, e.thrustTransforms, p.Guess, out p.Radius, out p.Rim)) { p.Radius = p.Guess; p.Rim = 0f; } }
                    catch (Exception) { p.Radius = p.Guess; p.Rim = 0f; }
                }
                if (!p.Box.activeSelf) p.Box.SetActive(true);
                // (looked for again now and then: an effect can be made anew while the engine runs)
                if (Fire.Hide && now > p.HidAt) { HideOthers(p); p.HidAt = now + 2f; }
                else if (!Fire.Hide && p.Hidden.Count > 0) { foreach (Renderer r in p.Hidden) if (r != null) r.forceRenderingOff = false; p.Hidden.Clear(); }
                showing++;

                // ---- its shape, from the air
                Vessel v = p.Part.vessel;
                float pressure = v != null ? Mathf.Max((float)(v.staticPressurekPa / 101.325), 0f) : 0f;
#if DEV
                if (airAs >= 0f) pressure = airAs;
#endif
                // (how thin the air is, as the flame feels it: 0 at sea level, a quarter at a tenth of an atmosphere, 1 with none)
                float thin = Mathf.Pow(Mathf.Clamp01(-Mathf.Log10(Mathf.Max(pressure, 0.0001f)) / 3.2f), 1.2f);
                Kind k = p.Kind;
                float nozzle = p.Radius * wider;
                // (An engine just lit has not settled: for most of a second its flame is a wider, cooler, more ragged thing, the
                // fuel that got there first burning as it can. And one throttled right down or going out shrinks away to nothing.)
                float fresh = Mathf.Clamp01(1f - (Time.time - p.LitAt) / 0.9f);
                fresh *= fresh;
                float low = Mathf.Clamp01(p.Shown / 0.15f);
                // (how wide it gets, in nozzle radii: a column at sea level, a wide bell with no air; and how long)
                float widest = Mathf.Lerp(2.2f, 13f, thin) * k.Wide * (1f + 0.45f * fresh);
                float length = nozzle * 2f * 22f * k.Long * longer * (0.3f + 0.7f * Mathf.Sqrt(p.Shown)) * Mathf.Sqrt(low);
                float opens = Mathf.Lerp(0.75f, 0.45f, thin);
                // (A cone that holds the whole jet, torn edge and all: the shader walks the line of sight only inside it. The jet
                // opens fastest where it begins, so the cone is fitted to it at a few places along it, not drawn from end to end.)
                float torn = 0.6f * rough * k.Rough * Mathf.Lerp(1f, 0.12f, thin) * (1f + 1.6f * fresh);
                float narrows = Mathf.Min(torn * 3f, 1f), coneEnd = 0f, coneRim = nozzle * 1.2f;
                for (int n = 0; n <= 12; n++)
                {
                    float s = n / 12f;
                    float reach = nozzle * Mathf.Lerp(1.06f, widest, Mathf.Pow(s, opens)) * Mathf.Lerp(1f, 1f - 0.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, s)), narrows) * (1f + (0.4f * (0.15f + 1.7f * s) + 0.12f) * torn);
                    edges[n] = reach;
                    if (reach > coneEnd) coneEnd = reach;
                }
                for (int n = 0; n < 12; n++) coneRim = Mathf.Max(coneRim, (edges[n] - coneEnd * (n / 12f)) / (1f - n / 12f));
                coneRim = Mathf.Min(coneRim, coneEnd);
                float half = coneEnd * 1.03f;
                Vector3 along = p.Nozzle.forward, rim = p.Nozzle.position + along * p.Rim;
                // (turned about the jet as the engine is: the flame's lumps then go round with the engine, and never jump as the craft turns)
                Quaternion turn = Quaternion.LookRotation(along, p.Nozzle.up);

                // ---- the ground, where the jet meets it within its length: the flame stops there and runs out along it
                float boxLength = length, boxFrom = 0f, runsFor = 1f;
                Vector4 met = new Vector4(0f, 0f, -1f, -1f);
                if (Fire.Ground && Meets(v, rim, along, length, out float far, out Vector3 faces))
                {
                    Vector3 own = Quaternion.Inverse(turn) * faces;
                    // (how far it runs along the ground: what was left of its length, and not all of that; and the box is widened to hold it)
                    float aslant = Mathf.Sqrt(Mathf.Max(0f, 1f - own.z * own.z));
                    float runs = nozzle * 1.2f + Mathf.Min((length - far) * 0.4f, nozzle * 13f);
                    half = Mathf.Max(half, runs);
                    runsFor = runs;
                    boxLength = Mathf.Min(length, far + runs * aslant + nozzle * 0.5f);
                    // (the sheet stands higher the further it has run: where the ground is very near, higher than the nozzle, and the box begins before the rim)
                    float jetThere = nozzle * Mathf.Lerp(1.06f, widest, Mathf.Pow(Mathf.Clamp01(far / length), opens));
                    boxFrom = Mathf.Min(0f, far - (jetThere * 0.4f + 0.1f * runs) * 2.4f - runs * aslant);
                    met = new Vector4(own.x, own.y, own.z, far);
                }
                p.Box.transform.SetPositionAndRotation(rim + along * ((boxFrom + boxLength) * 0.5f), turn);
                p.Box.transform.localScale = new Vector3(half * 2f, half * 2f, boxLength - boxFrom);

                // (the shock cells are a little longer than the nozzle is wide, and longer as the air thins; in very thin air there are none to see)
                float thick = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.5f, pressure));
                float cells = nozzle * 2f * Mathf.Lerp(1.3f, 3f, thin);
                float knots = (Fire.Diamonds ? 1f : 0f) * k.Diamonds * thick * 1.6f;
                float bright = glow * (0.25f + 0.75f * p.Shown) * low * Mathf.Lerp(1f, 1.15f, thin) * 0.3f;
                p.Draws.GetPropertyBlock(block);
                block.SetVector(cone, new Vector4(coneRim, coneEnd, 0f, 0f));
                block.SetVector(size, new Vector4(half, length, nozzle, bright));
                block.SetVector(shape, new Vector4(widest, opens, 4f * thin, cells));
                block.SetVector(air, new Vector4(pressure, clock, p.Own, 14f));
                block.SetVector(look, new Vector4(torn, knots * (1f - fresh), k.Tail, k.Hides * Mathf.Clamp01(pressure * 2f)));
                // (in thick air the jet goes on burning in the air it draws in, and thins slowly; with none it only spreads and cools, and thins as fast as it spreads)
                block.SetVector(more, new Vector4(Mathf.Lerp(k.Thins, 1.25f, thin), k.Dark * (1f - fresh) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.8f, pressure)), k.Pinch * thick * (1f - fresh), k.Lumpy * Mathf.Lerp(1f, 0.2f, thin)));
                float pale = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.9f, thin));
                block.SetVector(hot, Color.Lerp(k.Hot, k.Mid * 1.25f, 0.75f * fresh)); block.SetVector(mid, Color.Lerp(k.Mid, k.Thin * 1.6f, pale)); block.SetVector(cool, Color.Lerp(k.Cool, k.Thin, pale));
                block.SetVector(boxed, new Vector4(boxLength - boxFrom, boxFrom, runsFor, 0.24f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.85f, thin))));
                block.SetVector(ground, met);
                p.Draws.SetPropertyBlock(block);
            }
            drawn = showing;
            Report = showing > 0 ? showing + " flame" + (showing == 1 ? " is" : "s are") + " drawn as a volume." : plumes.Count > 0 ? "No engine is running." : "No rocket engine here.";
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
                   .Append("), rim ").Append(p.Rim.ToString("F2")).Append(" m on, running ").Append(p.Shown.ToString("F2"))
                   .Append(p.Box != null && p.Box.activeSelf ? ", box " + p.Box.transform.localScale.ToString("F1") : ", not shown").Append(", hides ").Append(p.Hidden.Count);
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
                if (p.Box != null && p.Box.activeSelf && p.Part.vessel == v) { nozzle = p.Nozzle; if (float.IsNaN(down)) down = p.Rim + at * p.Box.transform.localScale.z; break; }
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

        static float airAs = -1f;
        /// <summary>(The air the flames are shaped for, whatever air there is: to see every shape from the pad.)</summary>
        public static string AirReal() { airAs = -1f; return "the air as it is"; }
        public static string AirFull() { airAs = 1f; return "as at sea level"; }
        public static string AirHalf() { airAs = 0.5f; return "half an atmosphere"; }
        public static string AirFifth() { airAs = 0.2f; return "a fifth of an atmosphere"; }
        public static string AirTwentieth() { airAs = 0.05f; return "a twentieth of an atmosphere"; }
        public static string AirNone() { airAs = 0f; return "a vacuum"; }

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
                    float vacuum = engine.atmosphereCurve.Evaluate(0f), sea = Mathf.Max(engine.atmosphereCurve.Evaluate(1f), 1f);
                    float bell = Mathf.Clamp(Mathf.Sqrt(vacuum / sea) / 1.06f, 1f, 2.2f);
                    float guess = Mathf.Clamp(Rule * Mathf.Sqrt(Mathf.Max(engine.maxThrust / nozzles.Count, 0.05f)) * bell, 0.03f, 4f);
                    bool ok = Measure(part, nozzles[0], nozzles, guess, out float radius, out float rim, true);
                    say.Append(kind.Name).Append(", ").Append(engine.maxThrust.ToString("F0")).Append(" kN, ").Append(nozzles.Count).Append(nozzles.Count == 1 ? " nozzle" : " nozzles").Append(", guessed ").Append(guess.ToString("F2"))
                       .Append(ok ? ", measured " + radius.ToString("F2") + " m, rim " + rim.ToString("F2") + " m on, ratio " + (radius / guess).ToString("F2") : ", not measured").Append("\n");
                }
            }
            return say.ToString();
        }
#endif
    }
}
