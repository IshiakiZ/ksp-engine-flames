using System;
using System.Collections.Generic;
using Keystone;
using UnityEngine;

namespace EngineFlames
{
    /// <summary>
    /// (Since 0.6.0.) The puffs of RCS thrusters, as they look.
    ///
    /// A monopropellant thruster (hydrazine broken down over a hot catalyst, which is what the game's monopropellant stands for)
    /// throws out gas with nothing in it that glows. In the films of spacecraft manoeuvring, what shows is the plume catching the
    /// sun: a brief white puff of propellant frozen as it left unburnt, most of it as each pulse begins, spreading wide from the
    /// nozzle; bright seen towards the sun, as a cloud's edge is with the sun behind it, faint with the sun behind the camera,
    /// and nothing at all in a planet's shadow. A bipropellant one (the game's Vernor, on fuel and oxidiser) glows faintly at the
    /// nozzle as well, salmon, as small upper-stage engines do. In air the jet is stopped within a short way and shows as a short
    /// whitish puff.
    ///
    /// The game draws every RCS thruster as one white jet whatever the light, night or day; Waterfall's stock templates as wide
    /// beige fans, as bright in a planet's shadow as in the sun. The explosions' air, where this mod's flames burn, is far too
    /// coarse for a jet a few centimetres across, so here each thruster that fires gets a drawn plume of its own along the way its
    /// gas leaves: a soft cone, bright at the nozzle and fading as one over the way out, as a jet let out into a vacuum does (a
    /// picture of it, on a square turned to face the camera round the jet's line), and seen end on, a round puff. Its light is the
    /// sun's, by the angle it is seen at (Henyey and Greenstein's rule for light thrown mostly forward, as by ice, with a fifth all
    /// round), times how hard the thruster fires, and four times that as a pulse begins. While it is drawn the game's own jets on
    /// that part are hidden, and Waterfall's plumes where it draws them, as for this mod's flames.
    ///
    /// Tried (2026-10-24, development build, a craft with four RV-105s in an 80 km orbit): in the sun looking towards it, bright
    /// cones with a flash at each pulse; side on, faint wisps; with the sun behind the camera, fainter still against the lit
    /// planet; in Kerbin's shadow, nothing. A film of a turn under SAS, a push sideways and taps (DEV SunAt, tools/motion): no
    /// flicker, jumps or jitter.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class Puffs : MonoBehaviour
    {
        sealed class Thrusters
        {
            public Part Part;
            public ModuleRCS Rcs;
            public bool Burns;                          // fuel and oxidiser: a faint glow at the nozzle
            public float Long;                          // the plume's length in a vacuum (m)
            public float[] Shown, Flash;
            public bool[] Firing;
            public readonly List<Renderer> Hidden = new List<Renderer>();
        }

        // (each jet to be drawn this frame: its thruster block, which of its jets, how hard it fires as seen; where it is drawn is
        // reckoned as the flight's cameras draw, from where the craft is drawn then)
        struct Jet { public Thrusters Of; public int K; public float Strength, Shown, Air; public bool Lit; }

        const int Most = 96;                            // jets drawn at most
        const int Layer = 1;                            // (drawn by the near and far cameras, and the visors' cube)
        readonly List<Thrusters> all = new List<Thrusters>();
        readonly Jet[] jets = new Jet[Most];
        int jetCount, builtFrame = -1;
        float lookedAt = -10f;
        Mesh mesh;
        Material ink;
        Texture2D picture;
        GameObject shown;
        Vector3[] spots = new Vector3[Most * 8];
        Color32[] tints = new Color32[Most * 8];
        int usedWas;
        public static string Report = "";
        static Puffs running;
        int drawn;

        void Awake()
        {
            running = this;
            Camera.onPreCull += BeforeDrawing;
        }

        void OnDestroy()
        {
            Camera.onPreCull -= BeforeDrawing;
            if (running == this) running = null;
            foreach (Thrusters t in all) Unhide(t);
            if (shown != null) Destroy(shown);
            if (mesh != null) Destroy(mesh);
            if (ink != null) Destroy(ink);
            if (picture != null) Destroy(picture);
        }

        static void Unhide(Thrusters t)
        {
            foreach (Renderer r in t.Hidden) if (r != null) r.forceRenderingOff = false;
            t.Hidden.Clear();
        }

        void LateUpdate()
        {
            bool wanted = Fire.On != null && Fire.On && Fire.Rcs != null && Fire.Rcs && Fire.FlightReady && Sun.Instance != null && Sun.Instance.sunLight != null;
            if (!wanted)
            {
                if (all.Count > 0) { foreach (Thrusters t in all) Unhide(t); all.Clear(); }
                jetCount = 0;
                if (shown != null && shown.activeSelf) shown.SetActive(false);
                Report = "";
#if DEV
                why = "off: on " + (Fire.On != null && Fire.On) + ", rcs " + (Fire.Rcs != null && Fire.Rcs) + ", ready " + Fire.FlightReady + ", sun " + (Sun.Instance != null);
#endif
                return;
            }
#if DEV
            why = "";
#endif
            if (Time.unscaledTime - lookedAt > 1f) { lookedAt = Time.unscaledTime; Find(); }
            if (ink == null) Make();
            if (ink == null) return;

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            int n = 0;
            for (int i = 0; i < all.Count; i++)
            {
                Thrusters t = all[i];
                if (t.Part == null || t.Rcs == null || t.Rcs.thrusterTransforms == null || t.Rcs.thrustForces == null) continue;
                Vessel v = t.Part.vessel;
                bool lit = v != null && v.directSunlight;
                // (how thick the air is round it, 1 at Kerbin's sea level: it stops the jet short and mixes it into a puff)
                float air = v != null ? Mathf.Clamp01((float)(v.atmDensity / 1.225)) : 0f;
                int count = Mathf.Min(t.Rcs.thrusterTransforms.Count, t.Rcs.thrustForces.Length);
                if (t.Shown == null || t.Shown.Length != count) { t.Shown = new float[count]; t.Flash = new float[count]; t.Firing = new bool[count]; }
                for (int k = 0; k < count; k++)
                {
                    float power = t.Rcs.thrusterPower > 0f ? Mathf.Clamp01(t.Rcs.thrustForces[k] / t.Rcs.thrusterPower) : 0f;
                    bool firing = power > 0.001f && t.Rcs.isEnabled && t.Rcs.rcsEnabled;
                    // (a pulse begins: the frost of what left unburnt, four times as bright, for a tenth of a second and more)
                    if (firing && !t.Firing[k]) t.Flash[k] = 1f;
                    t.Firing[k] = firing;
                    t.Flash[k] = Mathf.Max(0f, t.Flash[k] - dt / 0.15f);
                    // (it comes at once and goes within a few hundredths of a second, as gas does from a valve that shuts)
                    t.Shown[k] = firing ? Mathf.Max(power, t.Shown[k]) : t.Shown[k] * Mathf.Exp(-dt / 0.04f);
                    float strength = t.Shown[k] * (1f + 3f * t.Flash[k]);
                    if (strength < 0.01f || n >= Most) continue;
                    Transform at = t.Rcs.thrusterTransforms[k];
                    // (a model's other variants have jets of their own, not shown: the RV-105 lists nine, four of them its own)
                    if (at == null || !at.gameObject.activeInHierarchy) continue;
                    jets[n++] = new Jet { Of = t, K = k, Strength = strength, Shown = t.Shown[k], Air = air, Lit = lit };
                }
            }
            jetCount = n;
            if (shown.activeSelf != (n > 0)) shown.SetActive(n > 0);
            if (n == 0) drawn = 0;
            Report = "";
        }

        /// <summary>
        /// The puffs placed where the jets are drawn, before the first of the flight's own cameras draws the frame (the far one, then the
        /// near one), and faced to it. Not in the frame's scripts: where the Smooth Running mod is on, the craft are carried on between
        /// the game's physics steps just before the cameras draw (by up to forty metres a frame in orbit, where the game moves the world
        /// past the craft), and puffs placed earlier stood that far behind their nozzles.
        /// </summary>
        void BeforeDrawing(Camera c)
        {
            if (builtFrame == Time.frameCount || shown == null || !shown.activeSelf || jetCount == 0) return;
            FlightCamera fc = FlightCamera.fetch;
            if (fc == null || fc.cameras == null) return;
            bool ours = false;
            for (int i = 0; i < fc.cameras.Length; i++) if (fc.cameras[i] == c) { ours = true; break; }
            if (!ours) return;
            builtFrame = Time.frameCount;
            Build(c);
        }

        void Build(Camera eye)
        {
            Light sun = Sun.Instance != null ? Sun.Instance.sunLight : null;
            if (sun == null) return;
            Color sunColour = sun.color * sun.intensity;
            Vector3 toSun = -sun.transform.forward;
            Vector3 camAt = eye.transform.position, camUp = eye.transform.up;
            int n = 0;
            for (int i = 0; i < jetCount; i++)
            {
                Jet j = jets[i];
                Thrusters t = j.Of;
                if (t.Part == null || t.Rcs == null || t.Rcs.thrusterTransforms == null || j.K >= t.Rcs.thrusterTransforms.Count) continue;
                Transform at = t.Rcs.thrusterTransforms[j.K];
                if (at == null) continue;
                Vector3 axis = (t.Rcs.useZaxis ? at.forward : at.up).normalized, nozzle = at.position;
                float length = t.Long * Mathf.Lerp(1f, 0.3f, j.Air), wide = length * Mathf.Lerp(0.9f, 0.7f, j.Air);
                Vector3 toCam = camAt - nozzle;
                float far = toCam.magnitude;
                if (far < 0.05f || far > 2500f) continue;
                toCam /= far;
                // The light it throws this way: the sun's, scattered forward mostly (Henyey and Greenstein, g = 0.5), as ice does;
                // in air a little more all round, as a puff of vapour.
                float cos = Vector3.Dot(-toSun, toCam);
                const float G = 0.5f;
                // (with a fifth of it all round, as ice of every size throws some light every way: forward six times as much as side
                // on, back two fifths; drawn as frost thrown back alone, the puffs were lost against a sunlit planet with the sun
                // behind the camera)
                float phase = 0.8f * (1f - G * G) / Mathf.Pow(1f + G * G - 2f * G * cos, 1.5f) + 0.2f;
                Color light = j.Lit ? sunColour * (0.4f * j.Strength * Mathf.Lerp(phase, 0.6f + 0.4f * phase, j.Air)) : Color.black;
                // (a bipropellant one's own faint glow, sun or none)
                if (t.Burns) light += new Color(1f, 0.5f, 0.35f) * (0.08f * j.Shown);
                // (and the sky's light on it in air by day: a share of the sun's)
                if (j.Air > 0f && j.Lit) light += new Color(0.55f, 0.65f, 0.8f) * (0.05f * j.Air * j.Strength);
                // In air the jet is stopped within a short way and mixes into the air round it, and what it mixes with turns to vapour
                // for a moment where it cools (as the white puffs of the cold-gas thrusters of real boosters in the films of their
                // flights): a white ball, lit by the sun and the sky as a cloud is, more of it the thicker the air. (Drawn as frost
                // alone, in thick air by day a pod's jets were too faint and too small to be seen at all, with the game's own hidden.)
                Color vapour = j.Air > 0f ? (j.Lit ? sunColour * 0.4f + new Color(0.55f, 0.65f, 0.8f) * 0.06f : new Color(0.2f, 0.22f, 0.26f) * 0.15f) * (j.Air * j.Strength) : Color.black;
                float side = Mathf.Sqrt(Mathf.Clamp01(1f - Vector3.Dot(axis, toCam) * Vector3.Dot(axis, toCam)));
#if DEV
                if (n == 0) lastJet = "lit " + j.Lit + ", air " + j.Air.ToString("F3") + ", strength " + j.Strength.ToString("F2") + ", phase " + phase.ToString("F2") + ", light " + light.ToString("F3") + ", side " + side.ToString("F2") + ", " + length.ToString("F2") + " m from " + nozzle.ToString("F1") + " along " + axis.ToString("F2") + ", " + far.ToString("F1") + " m off, drawn for " + eye.name;
#endif
                // (a cone along the jet, faced to the camera round its line; and seen end on, a round puff)
                Vector3 across = Vector3.Cross(axis, toCam);
                across = across.sqrMagnitude > 1e-8f ? across.normalized : Vector3.Cross(axis, Vector3.up).normalized;
                Quad(n * 8, nozzle - across * (wide * 0.5f), nozzle + across * (wide * 0.5f), nozzle + axis * length + across * (wide * 0.5f), nozzle + axis * length - across * (wide * 0.5f), light * side);
                Vector3 middle = nozzle + axis * (length * 0.2f), right = Vector3.Cross(toCam, camUp).normalized * (wide * 0.35f), up = Vector3.Cross(right, toCam).normalized * (wide * 0.35f);
                // (the round puff: end on, the jet seen through its length; in air, the vapour ball it mixes into, from every side, a little
                // further out and wider)
                if (j.Air > 0f) { middle = nozzle + axis * (length * Mathf.Lerp(0.2f, 0.55f, j.Air)); right *= 1f + 1.4f * j.Air; up *= 1f + 1.4f * j.Air; }
                Quad(n * 8 + 4, middle - right - up, middle + right - up, middle + right + up, middle - right + up, light * (1f - side) * 0.6f + vapour);
                n++;
            }
            for (int i = n * 8; i < usedWas * 8; i++) { spots[i] = Vector3.zero; tints[i] = new Color32(0, 0, 0, 0); }
            usedWas = n;
            drawn = n;
            // (an object of its own at the scene's origin, drawn by every camera of the scene, the visors' cube among them)
            shown.transform.position = Vector3.zero; shown.transform.rotation = Quaternion.identity;
            mesh.vertices = spots;
            mesh.colors32 = tints;
            // (bounds round the camera, wide enough for every jet drawn)
            mesh.bounds = new Bounds(camAt, Vector3.one * 6000f);
        }

        /// <summary>One square: its four corners and its light (which half of the picture each square shows is set once: the cone,
        /// then the round puff).</summary>
        void Quad(int at, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color light)
        {
#if DEV
            if (loud) light = loudWhite ? Color.white : new Color(1f, 0f, 1f);
#endif
            spots[at] = a; spots[at + 1] = b; spots[at + 2] = c; spots[at + 3] = d;
            var t = new Color32(Shown(light.r), Shown(light.g), Shown(light.b), 255);
            tints[at] = t; tints[at + 1] = t; tints[at + 2] = t; tints[at + 3] = t;
        }

        static byte Shown(float light) => (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(Mathf.Max(0f, light), 1f / 2.2f) * 127.5f), 0, 255);

        /// <summary>Every part with RCS thrusters on the craft near enough to be drawn; and the game's own jets on them, hidden.</summary>
        void Find()
        {
            var keep = new HashSet<Part>();
            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v == null) continue;
                foreach (Part p in v.parts)
                {
                    if (p == null) continue;
                    ModuleRCS rcs = p.FindModuleImplementing<ModuleRCS>();
                    if (rcs == null || rcs.isJustForShow) continue;
                    keep.Add(p);
                    if (all.Exists(t => t.Part == p)) continue;
                    var made = new Thrusters { Part = p, Rcs = rcs };
                    if (rcs.propellants != null) foreach (Propellant prop in rcs.propellants) if (prop != null && prop.name == "Oxidizer") made.Burns = true;
                    // (its length in a vacuum by its thrust: two metres for a kilonewton, by the square root of it beyond: the frost of
                    // a pulse in sunlight, as films of real ones show it, reaches a few metres from a thruster of that size)
                    made.Long = 2f * Mathf.Sqrt(Mathf.Max(0.2f, rcs.thrusterPower));
                    // (the game's own jets, and Waterfall's plumes where it draws them, as for this mod's flames: its particle effects on
                    // the part, which for an RCS part are its jets, and whatever is drawn with one of Waterfall's shaders)
                    if (Fire.Hide)
                        foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r == null || r.forceRenderingOff) continue;
                            Material m = r.sharedMaterial;
                            bool theirs = r is ParticleSystemRenderer || (m != null && m.shader != null && m.shader.name.StartsWith("Waterfall", StringComparison.Ordinal));
                            if (theirs) { r.forceRenderingOff = true; made.Hidden.Add(r); }
                        }
                    all.Add(made);
                }
            }
            for (int i = all.Count - 1; i >= 0; i--) if (all[i].Part == null || !keep.Contains(all[i].Part)) { Unhide(all[i]); all.RemoveAt(i); }
        }

        void Make()
        {
            // (the shader the game's own RCS jets are drawn with)
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("KSP/Particles/Additive");
            if (shader == null) { Fire.Log("no shader to draw RCS puffs with"); return; }
            picture = Picture();
            ink = new Material(shader) { mainTexture = picture };
            // (the shader doubles what it is given: the colours are kept at half, so a pulse's bright start looking towards the sun has
            // room above white, as the near camera's light has)
            ink.SetColor("_TintColor", new Color(1f, 1f, 1f, 0.5f));
            mesh = new Mesh { name = "Engine Flames RCS puffs" };
            mesh.MarkDynamic();
            shown = new GameObject("Engine Flames RCS puffs") { layer = Layer };
            shown.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = shown.AddComponent<MeshRenderer>();
            r.sharedMaterial = ink;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            shown.SetActive(false);
            var places = new Vector2[Most * 8];
            for (int q = 0; q < Most * 2; q++)
            {
                float half = (q & 1) == 0 ? 0f : 0.5f;
                places[q * 4] = new Vector2(half, 0f); places[q * 4 + 1] = new Vector2(half + 0.5f, 0f); places[q * 4 + 2] = new Vector2(half + 0.5f, 1f); places[q * 4 + 3] = new Vector2(half, 1f);
            }
            mesh.vertices = spots; mesh.colors32 = tints; mesh.uv = places;
            var corners = new int[Most * 12];
            for (int q = 0; q < Most * 2; q++) { int a = q * 4, i = q * 6; corners[i] = a; corners[i + 1] = a + 1; corners[i + 2] = a + 2; corners[i + 3] = a; corners[i + 4] = a + 2; corners[i + 5] = a + 3; }
            mesh.triangles = corners;
        }

        /// <summary>
        /// The picture: on the left a plume seen from the side (across, then along from the nozzle), on the right as seen end on. As
        /// the screen shows light. A jet let out into a vacuum spreads in a cone from its nozzle and its gas goes on at one speed, so
        /// as much of it passes each step of the way: seen from the side, as much light comes from each length of it, spread across
        /// a width that grows with the way out; so it is brightest at the nozzle and fades as one over the way out, not all in a bulb
        /// there (as one over its square, tried first: four lit bulbs at the thruster blocks, not jets). End on, the same cone seen
        /// through its length: as bright at the middle, falling as one over the way from it. Both fade out at the end, where its
        /// frost has thinned to nothing to be seen.
        /// </summary>
        static Texture2D Picture()
        {
            const int W = 128, H = 128;
            // (how wide the jet is at the nozzle and at its end, against the square's half width: its spread from the line, a Gaussian
            // in the angle out from it; well inside the square at the end, and windowed to nothing at its edges, or the square showed
            // round a pulse's bright start)
            const float Nozzle = 0.03f, Spread = 0.32f;
            var t = new Texture2D(W * 2, H, TextureFormat.RGBA32, false) { name = "Engine Flames RCS puff", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[W * 2 * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float along = (y + 0.5f) / H, across = ((x + 0.5f) / W - 0.5f) * 2f;
                    float sigma = Nozzle + (Spread - Nozzle) * along;
                    float end = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, along));
                    float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, Mathf.Abs(across)));
                    float lit = Mathf.Clamp01(Nozzle / sigma * Mathf.Exp(-0.5f * across * across / (sigma * sigma)) * end * edge);
                    byte v = (byte)(Mathf.Pow(lit, 1f / 2.2f) * 255f);
                    px[x + y * W * 2] = new Color32(v, v, v, 255);
                    float dx = ((x + 0.5f) / W - 0.5f) * 2f, dy = ((y + 0.5f) / H - 0.5f) * 2f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float round = Mathf.Clamp01(Nozzle / (Nozzle + r) * Mathf.Exp(-2f * r * r) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, r))));
                    byte w = (byte)(Mathf.Pow(round, 1f / 2.2f) * 255f);
                    px[W + x + y * W * 2] = new Color32(w, w, w, 255);
                }
            t.SetPixels32(px);
#if DEV
            t.Apply(false, false);
#else
            t.Apply(false, true);
#endif
            return t;
        }

#if DEV
        /// <summary>(Development build: the picture the puffs are drawn with, written to a file.)</summary>
        public static string Dump(string path)
        {
            if (running == null || running.picture == null) return "no picture";
            System.IO.File.WriteAllBytes(path, running.picture.EncodeToPNG());
            return "written to " + path;
        }

        static string why = "", lastJet = "";
        static bool loud;
        /// <summary>(Development build: every puff drawn magenta at full strength, to see where they are: "on", "off".)</summary>
        public static string Loud(string v) { loud = v == "on" || v == "white"; loudWhite = v == "white"; return loud ? "puffs " + (loudWhite ? "white" : "magenta") : "puffs as they are"; }
        static bool loudWhite;
        /// <summary>(Development build: each jet of the first RCS part: where it is from the part, which way it points, whether its
        /// transform is shown, and how hard it fires.)</summary>
        public static string Jets()
        {
            if (running == null || running.all.Count == 0) return "none";
            var o = new System.Text.StringBuilder();
            foreach (Thrusters t in running.all)
            {
                if (t.Part == null || t.Rcs.thrusterTransforms == null) continue;
                o.Append(t.Part.partInfo.title + " at " + t.Part.transform.position.ToString("F2") + ":\n");
                for (int k = 0; k < t.Rcs.thrusterTransforms.Count; k++)
                {
                    Transform at = t.Rcs.thrusterTransforms[k];
                    // (a model's other variants have jets of their own, not shown: the RV-105 lists nine, four of them its own)
                    if (at == null || !at.gameObject.activeInHierarchy) continue;
                    o.Append("  " + k + " " + at.name + " under " + (at.parent != null ? at.parent.name : "-") + ": " + (at.position - t.Part.transform.position).ToString("F2") + " pointing " + at.up.ToString("F2") + (at.gameObject.activeInHierarchy ? "" : " (not shown)") + ", force " + (k < t.Rcs.thrustForces.Length ? t.Rcs.thrustForces[k].ToString("F2") : "?") + "\n");
                }
                break;
            }
            return o.ToString();
        }

        /// <summary>(Development build: the flight camera turned to look at the jet firing hardest from square to its line, so many
        /// metres off: "5". The jet's nozzle in the middle of the view.)</summary>
        public static string Square(string v)
        {
            if (running == null || FlightCamera.fetch == null) return "no camera";
            float off = float.Parse(v, System.Globalization.CultureInfo.InvariantCulture), best = 0f;
            Transform found = null; ModuleRCS from = null;
            foreach (Thrusters t in running.all)
            {
                if (t.Rcs == null || t.Rcs.thrusterTransforms == null || t.Shown == null) continue;
                for (int k = 0; k < t.Shown.Length && k < t.Rcs.thrusterTransforms.Count; k++)
                    if (t.Shown[k] > best && t.Rcs.thrusterTransforms[k] != null && t.Rcs.thrusterTransforms[k].gameObject.activeInHierarchy) { best = t.Shown[k]; found = t.Rcs.thrusterTransforms[k]; from = t.Rcs; }
            }
            if (found == null) return "no jet firing";
            Vector3 axis = (from.useZaxis ? found.forward : found.up).normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.Cross(axis, FlightCamera.fetch.transform.forward)).normalized;
            if (side.sqrMagnitude < 0.5f) side = Vector3.Cross(axis, Vector3.up).normalized;
            FlightCamera cam = FlightCamera.fetch;
            cam.SetCamCoordsFromPosition(found.position + axis * 0.5f - side * off);
            return "looking at " + found.name + " firing " + best.ToString("F2") + " from " + off + " m square to it";
        }

        /// <summary>(Development build: the flight camera turned about its craft to look so many degrees from the sun: "0" into it, "90"
        /// with the sun to the side, "180" with the sun behind.)</summary>
        public static string SunAt(string v)
        {
            FlightCamera cam = FlightCamera.fetch;
            if (cam == null || Sun.Instance == null || Sun.Instance.sunLight == null) return "no camera";
            float deg = float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
            Vector3 toSun = -Sun.Instance.sunLight.transform.forward;
            Vector3 side = Vector3.Cross(toSun, cam.transform.up);
            side = side.sqrMagnitude > 1e-4f ? side.normalized : cam.transform.right;
            Vector3 look = Quaternion.AngleAxis(deg, side) * toSun;
            cam.SetCamCoordsFromPosition(cam.GetPivot().position - look * cam.Distance);
            return "looking " + deg + " degrees from the sun";
        }

        /// <summary>(Development build: the game's two lists of craft, the loaded ones and all: each one's name, whether it is loaded,
        /// packed, its state and its parts.)</summary>
        public static string Craft()
        {
            var o = new System.Text.StringBuilder("loaded:");
            foreach (Vessel v in FlightGlobals.VesselsLoaded) if (v != null) o.Append(" " + v.vesselName + " (" + v.state + ", " + (v.parts != null ? v.parts.Count : -1) + " parts, id " + v.GetInstanceID() + ")");
            o.Append("\nall that are loaded:");
            foreach (Vessel v in FlightGlobals.Vessels) if (v != null && v.loaded) o.Append(" " + v.vesselName + " (" + v.state + (v.packed ? ", packed" : "") + ", " + (v.parts != null ? v.parts.Count : -1) + " parts, id " + v.GetInstanceID() + (v == FlightGlobals.ActiveVessel ? ", flown" : "") + ")");
            return o.ToString();
        }

        /// <summary>(Development build: the RCS parts found, each one's jets and how hard each fires, and how many are drawn.)</summary>
        public static string Says()
        {
            if (running == null) return "not running";
            var o = new System.Text.StringBuilder(why + " " + running.all.Count + " RCS parts, " + running.drawn + " jets drawn; the first: " + lastJet + "; its corners " + running.spots[0].ToString("F2") + running.spots[1].ToString("F2") + running.spots[2].ToString("F2") + running.spots[3].ToString("F2") + " colour " + running.tints[0] + ", its puff " + running.spots[4].ToString("F2") + running.spots[6].ToString("F2") + " colour " + running.tints[4] + "; the camera at " + (FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera.transform.position.ToString("F2") : "?") + "\n");
            foreach (Thrusters t in running.all)
            {
                if (t.Part == null) continue;
                o.Append(t.Part.partInfo.title + (t.Burns ? " (fuel and oxidiser)" : "") + ": " + (t.Rcs.thrusterTransforms != null ? t.Rcs.thrusterTransforms.Count : 0) + " jets, " + t.Rcs.thrusterPower.ToString("F1") + " kN each, " + t.Long.ToString("F2") + " m long, " + t.Hidden.Count + " of the game's own hidden; now");
                if (t.Shown != null) foreach (float s in t.Shown) o.Append(" " + s.ToString("F2"));
                o.Append("\n");
            }
            return o.ToString();
        }
#endif
    }
}
