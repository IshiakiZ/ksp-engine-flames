// The re-entry fire is in the development build only since 0.5.0, kept for the work on it (the owner, 2026-10-21: "Reentry fire:
// we're going to need to work on it a lot, so don't get rid of it, but save it for later. Don't release it"). A release build
// has none of it: no switch on the page, nothing drawn, and the game's own re-entry streaks as the game has them.
#if DEV
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EngineFlames
{
    /// <summary>
    /// The fire of coming into an atmosphere fast (the owner, 2026-10-14: "add to the rocket fire mod to make the heat fire
    /// when entering an atmosphere better"). The game draws it as flat streaks pulled back from the craft's outline. Here it
    /// is what it is: air in front of the craft brought almost to rest and heated past white heat, a thin bright layer
    /// standing off the face that meets the air, streaming back past the body's sides and on behind it as a glowing wake
    /// that cools as it goes, from white and gold through orange to the pink-violet of nitrogen's own light. Like the
    /// engines' flames it is made in the air of the Volumetric Explosions mod and drawn as one lit volume (see its
    /// Site.Plasma); this says where, how wide and how strong.
    ///
    /// How strong goes by the game's own measure, the one its streaks go by (AerodynamicsFX): half the air's density times
    /// the speed to the power 3.5, and how far past the speed of sound it is going. So it burns where and when the game's
    /// own fire would, for every craft in the air near by, not only the one being flown; and while it burns for the craft
    /// being flown the game's streaks and their light are kept off (its sound and shaking are left).
    ///
    /// Where the face is and how wide the craft is across the air are measured from the craft's own meshes, twice a second:
    /// the face is the front of the craft as the air meets it, the width that of its front part (a capsule's heat shield,
    /// a stage's nose), not of a solar panel sticking out further back. A light of the glow's colour shines on the craft.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    [DefaultExecutionOrder(10000)]
    public sealed class Reentry : MonoBehaviour
    {
        sealed class Craft
        {
            public Vessel V;
            public int Id;
            public Vector3 FaceL;           // the middle of the face, in the craft's own frame
            public Vector3 AlongL;          // the way the air was going past when the face was found, in the craft's own frame
            public float Radius, Length, Strength, SizedAt = -1f, Seen;
            public Light Lamp;
            public Vector3 Along;           // (for its look from far off, see BeforeDrawing: the way the air goes past, as the world lies this frame)
            public float Fire, Wake;        // (and how bright its glow is and how far its wake glows, as told the air)
        }

        readonly Dictionary<Vessel, Craft> crafts = new Dictionary<Vessel, Craft>();
        bool hooked, drawnForActive;
        AerodynamicsFX stock;
        float lookedForStock;
        public static string Report = "";
#if DEV
        float saidAt;
#endif

        void Awake()
        {
            Fire.Setup();
            if (!Flames.Installed()) return;
            try { Feed.Hook(Asked); hooked = true; }
            catch (Exception ex) { Fire.Log("the re-entry fire cannot be made with this Volumetric Explosions (" + ex.GetType().Name + ")"); }
            if (hooked) Camera.onPreCull += BeforeDrawing;
        }

        void OnDestroy()
        {
            if (hooked) { try { Feed.Unhook(Asked); } catch (Exception) { } }
            Camera.onPreCull -= BeforeDrawing;
            foreach (Craft c in crafts.Values) if (c.Lamp != null) Destroy(c.Lamp.gameObject);
            crafts.Clear();
            if (farShown != null) Destroy(farShown);
            if (farMesh != null) Destroy(farMesh);
            if (farInk != null) Destroy(farInk);
            if (farPicture != null) Destroy(farPicture);
        }

        /// <summary>The game's own measure of re-entry heat (AerodynamicsFX), 0 to 1: its streaks' strength times how far it is from a shock's condensation to fire.</summary>
        static float Strength(Vessel v)
        {
            double density = v.atmDensity, speed = v.srfSpeed;
            if (density <= 0.0 || speed <= 1.0) return 0f;
            double fadeStart = PhysicsGlobals.AeroFXDensityFadeStart;
            if (fadeStart > 0.0 && density < fadeStart) density *= density * Math.Ceiling(1.0 / fadeStart);
            double factor = Math.Pow(density, PhysicsGlobals.AeroFXDensityExponent1) * PhysicsGlobals.AeroFXDensityScalar1
                            + Math.Pow(density, PhysicsGlobals.AeroFXDensityExponent2) * PhysicsGlobals.AeroFXDensityScalar2;
            double heat = 0.5 * factor * Math.Pow(speed, PhysicsGlobals.AeroFXVelocityExponent);
            float fx = (float)Math.Min(1.0, (heat - 8000000.0) / 40000000.0);
            float state = Mathf.InverseLerp(PhysicsGlobals.AeroFXStartThermalFX, PhysicsGlobals.AeroFXFullThermalFX, (float)v.mach);
            return Mathf.Clamp01(fx) * state;
        }

        /// <summary>
        /// The face and the width, from the craft's meshes: every corner of every mesh's box, laid along the way the air goes
        /// past and across it. The face is the middle of the corners in the front tenth of the craft; the width the furthest
        /// any corner in its front two fifths lies from the line through the face.
        /// </summary>
        static void Size(Craft c, Vector3 along)
        {
            Vessel v = c.V;
            Vector3 com = v.CoMD, ahead = -along;
            var corners = new List<Vector3>(256);
            foreach (Part part in v.parts)
            {
                if (part == null) continue;
                foreach (MeshFilter mf in part.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    Renderer r = mf.GetComponent<Renderer>();
                    if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                    Bounds b = mf.sharedMesh.bounds;
                    Transform t = mf.transform;
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 local = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                        corners.Add(t.TransformPoint(local) - com);
                    }
                }
            }
            if (corners.Count == 0) { c.Radius = 1f; c.Length = 2f; c.FaceL = Vector3.zero; c.AlongL = v.transform.InverseTransformDirection(along); return; }
            float front = float.MinValue, back = float.MaxValue;
            foreach (Vector3 p in corners) { float d = Vector3.Dot(p, ahead); if (d > front) front = d; if (d < back) back = d; }
            float length = Mathf.Max(0.5f, front - back);
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (Vector3 p in corners) if (Vector3.Dot(p, ahead) > front - 0.1f * length) { sum += p; n++; }
            Vector3 face = n > 0 ? sum / n : ahead * front;
            face += ahead * (front - Vector3.Dot(face, ahead));                 // (on the front itself)
            float radius = 0.25f;
            foreach (Vector3 p in corners)
            {
                if (Vector3.Dot(p, ahead) < front - 0.4f * length) continue;
                Vector3 off = p - face;
                // (a round part's box has its corners half as far again out as the part: most parts are round)
                float across = 0.72f * (off - ahead * Vector3.Dot(off, ahead)).magnitude;
                if (across > radius) radius = across;
            }
            c.Radius = Mathf.Min(radius, 0.5f * length + 2f);
            c.Length = length;
            c.FaceL = v.transform.InverseTransformPoint(face + com);
            c.AlongL = v.transform.InverseTransformDirection(along);
        }

        /// <summary>Once a frame, when the air can take it: each craft that is coming into the air fast says where its glow is.</summary>
        void Asked()
        {
            drawnForActive = false;
            bool wanted = Fire.Reentry != null && Fire.Reentry && Feed.Ready() && Feed.KnowsPlasma;
            float now = Time.time;
            int glowing = 0;
#if DEV
            bool say = Time.realtimeSinceStartup > saidAt;
            if (say)
            {
                saidAt = Time.realtimeSinceStartup + 10f;
                Vessel a = FlightGlobals.ActiveVessel;
                Fire.Log("re-entry: wanted " + wanted + " (switch " + (Fire.Reentry != null && Fire.Reentry) + ", ready " + Feed.Ready() + ", knows " + Feed.KnowsPlasma + ")" +
                         (a != null ? ", " + a.vesselName + " strength " + Strength(a).ToString("F3") + " density " + a.atmDensity.ToString("G3") + " speed " + a.srfSpeed.ToString("F0") + " mach " + a.mach.ToString("F1") : ""));
            }
#endif
            if (wanted)
            {
                float glow = Fire.ReentryGlow != null ? (float)Fire.ReentryGlow : 1f;
                foreach (Vessel v in FlightGlobals.VesselsLoaded)
                {
                    if (v == null || !v.loaded || v.packed || v.mainBody == null || !v.mainBody.atmosphere) continue;
                    float strength = Strength(v);
                    if (strength <= 0.01f) continue;
                    Vector3 along = -(Vector3)v.srf_velocity.normalized;
                    if (!crafts.TryGetValue(v, out Craft c))
                    {
                        c = new Craft { V = v, Id = unchecked((int)v.persistentId * 31 + 0x51ce) };
                        crafts[v] = c;
                    }
                    // (measured again twice a second, and at once if the craft has turned its face from the air)
                    Vector3 alongNow = v.transform.InverseTransformDirection(along);
                    if (now > c.SizedAt || Vector3.Dot(alongNow, c.AlongL) < 0.97f) { Size(c, along); c.SizedAt = now + 0.5f; }
                    Vector3 face = v.transform.TransformPoint(c.FaceL);
                    // (the wake glows the further behind the hotter it is: a few of the body's widths at first, a dozen at its height)
                    float length = c.Length + c.Radius * (5f + 22f * strength);
                    float running = Mathf.Clamp01(strength * 3f);
                    float fire = glow * (0.3f + 1.6f * strength);
                    // (the flow from every side closes in just behind the craft: its own length, and a little more)
                    float neck = c.Length + 0.5f * c.Radius;
                    bool taken = Feed.Plasma(c.Id, face, along, v.srf_velocity, c.Radius, neck, length, running, fire, strength, v.transform);
#if DEV
                    if (say) Fire.Log("re-entry: " + v.vesselName + " face " + c.FaceL + " radius " + c.Radius.ToString("F2") + " length " + c.Length.ToString("F1") + " wake " + length.ToString("F1") + " fire " + fire.ToString("F2") + " taken " + taken);
#endif
                    c.Strength = taken ? strength : 0f;
                    c.Along = along; c.Fire = fire; c.Wake = length;
                    if (!taken) continue;
                    c.Seen = now;
                    glowing++;
                    if (v == FlightGlobals.ActiveVessel) drawnForActive = true;
                    Lamp(c, face, along, strength, glow, v.rootPart != null ? v.rootPart.transform.Find("model") : null);
                }
            }
            // (each craft's light fades out once its glow is gone, and is let go when the craft is)
            var gone = new List<Vessel>();
            foreach (KeyValuePair<Vessel, Craft> kv in crafts)
            {
                Craft c = kv.Value;
                if (kv.Key == null || !kv.Key.loaded) { if (c.Lamp != null) Destroy(c.Lamp.gameObject); gone.Add(kv.Key); continue; }
                if (now - c.Seen > 0.05f && c.Lamp != null)
                {
                    c.Lamp.intensity *= 0.85f;
                    if (c.Lamp.intensity < 0.02f) c.Lamp.enabled = false;
                }
            }
            foreach (Vessel v in gone) crafts.Remove(v);
            Report = !wanted ? "" : glowing > 0 ? glowing + (glowing == 1 ? " craft is" : " crafts are") + " glowing as it comes into the air." : "";
#if DEV
            if (say && glowing > 0) Fire.Log("re-entry from far off: " + (FarSaid.Length > 0 ? FarSaid : "none drawn"));
#endif
        }

        // ================================================================== seen from far off (since 2026-11-20)

        /// <summary>
        /// The glow seen from far off. The glow itself is a volume in the explosions mod's air, and from a few hundred metres its
        /// sheath and wake are thinner than a pixel and its head a pixel or two: a picture drawn by sampling a point in each pixel
        /// misses what is thinner than a pixel, where a camera gathers all of a pixel's light, and the glow was gone. Filmed
        /// beside the Win Capsule at 41 km by night it was one orange dot of six pixels from 300 m, nothing at all from 1.5 and
        /// 5 km (`far20_now_n300`, `_n1500`, `_n5000`). A real one is photographed from afar as a small head, white and burnt out
        /// with a coloured fringe, blooming as a bright light does in a camera, on a long thin trail of its own colour: Progress
        /// 42 from the station (NASA, iss029e034092), Cygnus (iss040e098545, iss040e098572), Stardust's capsule from the DC-8
        /// (PIA03668). So, as the glow comes to fewer pixels across than FarFrom and FarTo say: a sharp core, a halo round it as
        /// wide as the head is bright (its light at the lens taken in tenfolds, as a camera's bloom grows), and a streak along the
        /// path behind, StreakLong times as long as the glowing wake (some 160 m at its strongest, under a tenth of a second of
        /// flight), a body's width across but never under two pixels.
        /// Light at the lens goes as the glow's strength and its face's area over the distance squared: burnt out within some
        /// three kilometres, fading to nothing by thirty. Its colour the glow's own, pink-violet while faint, gold when strong.
        /// Placed where the craft is drawn, just before the flight's cameras draw (see BeforeDrawing). Close by, where the glow
        /// is more than FarTo pixels across, none of it: the glow as it is. Fire.ReentryFar: off.
        /// </summary>
        const float FarFrom = 4f, FarTo = 16f, StreakLong = 10f;
        const int FarMost = 8;                          // crafts drawn so at most
        const int FarLayer = 1;                         // (drawn by the near and far cameras, as the RCS puffs are)
        Mesh farMesh;
        Material farInk;
        Texture2D farPicture;
        GameObject farShown;
        readonly Vector3[] farSpots = new Vector3[FarMost * 12];
        readonly Color32[] farTints = new Color32[FarMost * 12];
        int farBuilt = -1, farUsed;
        public static string FarSaid = "";

        /// <summary>(Development: how the first glowing craft was last drawn from far off, for the bridge's dev_call.)</summary>
        public static string Far(string arg) => FarSaid;

        void FarMake()
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("KSP/Particles/Additive");
            if (shader == null) { Fire.Log("no shader to draw the re-entry fire from far off with"); return; }
            farPicture = FarPicture();
            // (after the glow's own volume, which the explosions mod draws at 3001 for a patch that rides a craft: drawn before it, at the
            // shader's own 3000, the head and the first fourteen metres of the streak were under the volume's thin wake, `far20_far1_n300`)
            farInk = new Material(shader) { mainTexture = farPicture, renderQueue = 3002 };
            // (the shader doubles what it is given: the colours are kept at half, so the head has room above white to burn out)
            farInk.SetColor("_TintColor", new Color(1f, 1f, 1f, 0.5f));
            farMesh = new Mesh { name = "Engine Flames re-entry fire from far off" };
            farMesh.MarkDynamic();
            farShown = new GameObject("Engine Flames re-entry fire from far off") { layer = FarLayer };
            farShown.AddComponent<MeshFilter>().sharedMesh = farMesh;
            MeshRenderer r = farShown.AddComponent<MeshRenderer>();
            r.sharedMaterial = farInk;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            // (each craft three squares: the core, the halo and the streak, each its third of the picture)
            var places = new Vector2[FarMost * 12];
            for (int q = 0; q < FarMost * 3; q++)
            {
                float a = (q % 3) / 3f, b = a + 1f / 3f;
                places[q * 4] = new Vector2(a, 0f); places[q * 4 + 1] = new Vector2(b, 0f); places[q * 4 + 2] = new Vector2(b, 1f); places[q * 4 + 3] = new Vector2(a, 1f);
            }
            farMesh.vertices = farSpots; farMesh.colors32 = farTints; farMesh.uv = places;
            var corners = new int[FarMost * 18];
            for (int q = 0; q < FarMost * 3; q++) { int a = q * 4, i = q * 6; corners[i] = a; corners[i + 1] = a + 1; corners[i + 2] = a + 2; corners[i + 3] = a; corners[i + 4] = a + 2; corners[i + 5] = a + 3; }
            farMesh.triangles = corners;
        }

        /// <summary>
        /// The picture, as the screen shows light: on the left the core (a round spot, a Gaussian), in the middle the halo (a camera's
        /// bloom round a bright light: falling as one over the square of the distance, past a small middle, windowed to nothing at the
        /// square's edge), on the right the streak (across, a Gaussian; along it, from the head at the bottom to nothing at the top).
        /// </summary>
        static Texture2D FarPicture()
        {
            const int W = 64, H = 64;
            var t = new Texture2D(W * 3, H, TextureFormat.RGBA32, false) { name = "Engine Flames re-entry fire from far off", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[W * 3 * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = ((x + 0.5f) / W - 0.5f) * 2f, dy = ((y + 0.5f) / H - 0.5f) * 2f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float window = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, r));
                    float core = Mathf.Exp(-r * r / (2f * 0.45f * 0.45f)) * window;
                    float halo = 1f / (1f + r * r / (0.2f * 0.2f)) * window;
                    float along = (y + 0.5f) / H, across = dx;
                    float streak = Mathf.Exp(-across * across / (2f * 0.35f * 0.35f)) * Mathf.Pow(1f - along, 1.2f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, Mathf.Abs(across))));
                    byte a = (byte)(Mathf.Pow(Mathf.Clamp01(core), 1f / 2.2f) * 255f), b = (byte)(Mathf.Pow(Mathf.Clamp01(halo), 1f / 2.2f) * 255f), c = (byte)(Mathf.Pow(Mathf.Clamp01(streak), 1f / 2.2f) * 255f);
                    px[x + y * W * 3] = new Color32(a, a, a, 255);
                    px[W + x + y * W * 3] = new Color32(b, b, b, 255);
                    px[2 * W + x + y * W * 3] = new Color32(c, c, c, 255);
                }
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }

        /// <summary>
        /// The look from far off placed where each craft is drawn, before the first of the flight's own cameras draws the frame, and
        /// faced to it: where the Smooth Running mod is on, the craft are carried on between the game's physics steps just before the
        /// cameras draw (forty metres a frame at the speed of a craft coming in from orbit), and what was placed earlier stood that
        /// far behind (as for the RCS puffs: see Puffs.BeforeDrawing). They carry the craft's models, not the craft's own transform,
        /// which the face is read from: it is carried on here by what that mod hands on (Keystone.Motion.CarryWith).
        /// </summary>
        void BeforeDrawing(Camera eye)
        {
            if (farBuilt == Time.frameCount) return;
            FlightCamera fc = FlightCamera.fetch;
            if (fc == null || fc.cameras == null) return;
            bool ours = false;
            for (int i = 0; i < fc.cameras.Length; i++) if (fc.cameras[i] == eye) { ours = true; break; }
            if (!ours) return;
            farBuilt = Time.frameCount;
            // (said afresh each frame: kept from the last frame it was drawn, the log had a capsule 25 m off "300 m off, far 1.00")
            FarSaid = "";
            bool wanted = Fire.ReentryFar != null && Fire.ReentryFar && Fire.Reentry != null && Fire.Reentry;
            int n = 0;
            float now = Time.time;
            if (wanted)
            {
                Vector3 camAt = eye.transform.position, camUp = eye.transform.up;
                float lines = Mathf.Max(eye.pixelHeight, 1), scale = lines / 720f, tan = Mathf.Tan(0.5f * eye.fieldOfView * Mathf.Deg2Rad);
                foreach (Craft c in crafts.Values)
                {
                    if (n >= FarMost) break;
                    if (c.V == null || !c.V.loaded || c.Strength <= 0.01f || now - c.Seen > 0.1f) continue;
                    // (the craft's own transform is where its last physics step left it: its models, and the glow's volume with them,
                    // are drawn carried on from there, by Keystone.Motion: 18 to 38 m on at 2 km/s, and the look stood that far behind)
                    Vector3 face = Keystone.Motion.CarryWith(c.V, c.V.transform.TransformPoint(c.FaceL));
                    Vector3 toCam = camAt - face;
                    float d = toCam.magnitude;
                    if (d < 1f) continue;
                    toCam /= d;
                    // (how many pixels a metre is there, and how many the glow's head is across: none of this while it is more than FarTo)
                    float perMetre = lines / (2f * d * tan);
                    float headPx = 2f * c.Radius * perMetre;
                    float far = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FarFrom, FarTo, headPx));
                    if (far <= 0.002f) continue;
                    // (its light at the lens: one for the Win Capsule's glow at three quarters of its strength a kilometre off, by its face's
                    // area and over the distance squared; in tenfolds)
                    float area = c.Radius / 0.65f;
                    float lens = c.Fire / 1.5f * area * area * 1e6f / (d * d);
                    float tens = Mathf.Log10(Mathf.Max(lens, 1e-6f));
                    float bright = Mathf.Clamp01(1f + 0.5f * (tens + 1f)) * far * Mathf.Clamp01(c.Strength * 3f);
                    if (bright <= 0.002f) continue;
                    // (and as much of it as comes through the weather's clouds between, since 2026-11-20: see ThroughClouds)
                    float through = TestFarClouds == 1f ? 1f : ThroughClouds(camAt, face);
                    bright *= through;
                    if (bright <= 0.002f)
                    {
                        // (said all the same: hidden by the clouds, where the log had "none drawn" as for a craft too faint to be seen)
                        if (n == 0 && FarSaid.Length == 0) FarSaid = c.V.vesselName + " " + d.ToString("F0") + " m off: hidden by the clouds, " + through.ToString("F3") + " of its light through them (bright " + (bright / Mathf.Max(through, 1e-6f)).ToString("F2") + " without)";
                        continue;
                    }
                    float warm = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, c.Strength));
                    Color tint = Color.Lerp(new Color(1f, 0.5f, 0.78f), new Color(1f, 0.7f, 0.42f), warm);
                    // (the streak deeper than the head's colour, since 2026-11-20: the Cygnus and Progress photographs from the station
                    // have orange trails, 151 to 160,115 to 121,89 to 90 (blue under three fifths of red), where ours in the head's colour
                    // came out a pale gold, 161 to 245,132 to 219,100 to 181. TestFarClouds 2: in the head's colour, as until then.)
                    Color trail = TestFarClouds == 2f ? tint : Color.Lerp(new Color(1f, 0.42f, 0.66f), new Color(1f, 0.5f, 0.27f), warm);
                    Vector3 right = Vector3.Cross(toCam, camUp);
                    right = right.sqrMagnitude > 1e-8f ? right.normalized : eye.transform.right;
                    Vector3 up = Vector3.Cross(right, toCam).normalized;
                    // (the core and the halo stand two radii towards the camera from the face: at the face the craft's own body, a few pixels
                    // across from a few hundred metres, hid their middles, and the head showed as a ring with a dark hole in it)
                    Vector3 front = face + toCam * Mathf.Min(2f * c.Radius, 0.25f * d);
                    // the core: four and a half pixels across (or the head's own size, where that is more), white with the glow's colour in it, burnt out
                    float corePx = Mathf.Max(4.5f * scale, 1.2f * headPx), coreHalf = 0.5f * corePx / perMetre;
                    FarQuad(n * 12, front - right * coreHalf - up * coreHalf, front + right * coreHalf - up * coreHalf, front + right * coreHalf + up * coreHalf, front - right * coreHalf + up * coreHalf,
                            Color.Lerp(tint, Color.white, 0.7f) * (2f * bright));
                    // the halo: as wide as the head is bright
                    float haloPx = Mathf.Max((3.5f + 3f * Mathf.Max(0f, tens + 1f)) * scale, 1.5f * headPx), haloHalf = haloPx / perMetre;
                    FarQuad(n * 12 + 4, front - right * haloHalf - up * haloHalf, front + right * haloHalf - up * haloHalf, front + right * haloHalf + up * haloHalf, front - right * haloHalf + up * haloHalf,
                            tint * (0.8f * bright));
                    // the streak: along the path behind, a body's width across but never under two pixels
                    Vector3 along = c.Along.sqrMagnitude > 0.5f ? c.Along.normalized : -toCam;
                    Vector3 across = Vector3.Cross(along, toCam);
                    across = across.sqrMagnitude > 1e-6f ? across.normalized : right;
                    float wide = 0.5f * Mathf.Max(2f * c.Radius, 2f * scale / perMetre), longs = StreakLong * Mathf.Max(c.Wake, 2f * c.Radius);
                    Vector3 from = face + along * (0.5f * c.Radius), to = face + along * longs;
                    FarQuad(n * 12 + 8, from - across * wide, from + across * wide, to + across * wide, to - across * wide, trail * (0.75f * bright));
                    if (n == 0) FarSaid = c.V.vesselName + " " + d.ToString("F0") + " m off: head " + headPx.ToString("F1") + " px, far " + far.ToString("F2") + ", light at the lens " + lens.ToString("G3") + ", bright " + bright.ToString("F2") + ", through the clouds " + through.ToString("F2") + ", halo " + haloPx.ToString("F1") + " px, streak " + longs.ToString("F0") + " m";
                    n++;
                }
            }
            if (n == 0 && farUsed == 0) { if (farShown != null && farShown.activeSelf) farShown.SetActive(false); return; }
            if (farMesh == null) FarMake();
            if (farMesh == null) return;
            for (int i = n * 12; i < farUsed * 12; i++) { farSpots[i] = Vector3.zero; farTints[i] = new Color32(0, 0, 0, 0); }
            farUsed = n;
            if (farShown.activeSelf != (n > 0)) farShown.SetActive(n > 0);
            if (n == 0) return;
            farShown.transform.position = Vector3.zero; farShown.transform.rotation = Quaternion.identity;
            farMesh.vertices = farSpots;
            farMesh.colors32 = farTints;
            farMesh.RecalculateBounds();
        }

        /// <summary>(For setting one against the other, development build: 1 draws the far look through the weather's clouds as if
        /// they were not there, as until 2026-11-20; 2 that, and its streak in the head's colour, as until then.)</summary>
        public static float TestFarClouds;
        /// <summary>(For the development build's call: TestFarClouds set, "0", "1" or "2".)</summary>
        public static string TestClouds(string value) { float.TryParse(value, out TestFarClouds); return "TestFarClouds " + TestFarClouds; }
        static bool cloudsMissing;

        /// <summary>
        /// How much of a far re-entry's light comes through the weather's clouds between it and the camera, 0 to 1, since 2026-11-20.
        /// The weather's clouds keep no depth and the far look is drawn after them, so by its order of drawing a re-entry behind a
        /// cloud bank showed through it. Each layer the line of sight crosses (Keystone.Weather, about halfway along the line, as
        /// near as the weather's map has it) takes so much as its cover says of what a path through so much cloud would: a broken
        /// layer has gaps the light comes through. Through cirrus and altostratus, the ground glass the sun shows through, a third
        /// at most. Without the weather mod (or with a Keystone older than its weather), 1: as until then.
        /// </summary>
        static float ThroughClouds(Vector3 camAt, Vector3 at)
        {
            if (cloudsMissing) return 1f;
            try { return AskClouds(camAt, at); }
            catch (Exception) { cloudsMissing = true; return 1f; }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static float AskClouds(Vector3 camAt, Vector3 at)
        {
            if (!Keystone.Weather.There) return 1f;
            CelestialBody body = FlightGlobals.currentMainBody;
            if (body == null || !body.atmosphere) return 1f;
            float a = (float)body.GetAltitude((Vector3d)camAt), b = (float)body.GetAltitude((Vector3d)at);
            float along = Vector3.Distance(camAt, at);
            Keystone.WeatherHere h = Keystone.Weather.At(body, (Vector3d)Vector3.Lerp(camAt, at, 0.5f));
            return Leaves(h.Low, a, b, along) * Leaves(h.Middle, a, b, along) * Leaves(h.High, a, b, along);
        }

        /// <summary>What one layer leaves of the light along a line between heights a and b, so many metres long: its cover's share
        /// of the line's way through it (as so much of the line as lies between its base and top) taken as a cloud takes light,
        /// a tenth left for every 120 m of a heap's or a deck's cloud, every 2 km of the thin ones'.</summary>
        static float Leaves(Keystone.CloudLayer l, float a, float b, float along)
        {
            if (l.Cover <= 0.02f || l.Kind == Keystone.CloudKind.Fog || l.Kind == Keystone.CloudKind.Dust) return 1f;
            float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b), top = Mathf.Max(l.Top, l.Base + 1f);
            float inside = hi - lo < 1f ? (lo >= l.Base && lo <= top ? 1f : 0f) : Mathf.Clamp01((Mathf.Min(hi, top) - Mathf.Max(lo, l.Base)) / (hi - lo));
            if (inside <= 0f) return 1f;
            bool thin = l.Kind == Keystone.CloudKind.Cirrus || l.Kind == Keystone.CloudKind.Altostratus;
            float path = inside * along, takes = 1f - Mathf.Pow(0.1f, path / (thin ? 2000f : 120f));
            return 1f - Mathf.Clamp01(l.Cover) * (thin ? 0.35f : 1f) * takes;
        }

        /// <summary>One square: its four corners and its light (kept at half: the shader doubles it).</summary>
        void FarQuad(int at, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color light)
        {
            farSpots[at] = a; farSpots[at + 1] = b; farSpots[at + 2] = c; farSpots[at + 3] = d;
            Color32 t = new Color32((byte)(Mathf.Clamp01(0.5f * light.r) * 255f), (byte)(Mathf.Clamp01(0.5f * light.g) * 255f), (byte)(Mathf.Clamp01(0.5f * light.b) * 255f), 255);
            farTints[at] = t; farTints[at + 1] = t; farTints[at + 2] = t; farTints[at + 3] = t;
        }

        /// <summary>The glow's own light on the craft: from just ahead of the face, the colour of the glow at its strength (pink-violet faint, orange and gold strong).</summary>
        void Lamp(Craft c, Vector3 face, Vector3 along, float strength, float glow, Transform ride)
        {
            if (c.Lamp == null)
            {
                var go = new GameObject("Engine Flames re-entry light");
                c.Lamp = go.AddComponent<Light>();
                c.Lamp.type = LightType.Point;
                c.Lamp.shadows = LightShadows.None;
                c.Lamp.renderMode = LightRenderMode.ForcePixel;
                // (the craft and what is near it, as the game's own lights of a craft do: not the far terrain or the sky)
                c.Lamp.cullingMask = (1 << 0) | (1 << 15);
            }
            c.Lamp.enabled = true;
            // (hung from the craft's model, so that it goes wherever the craft is drawn: the Smooth Running mod carries the models on
            // between the game's physics steps, and a light left loose would stand behind the craft by up to a step, forty metres at
            // the speed of a craft coming in from orbit)
            if (ride != null && c.Lamp.transform.parent != ride) c.Lamp.transform.SetParent(ride, true);
            c.Lamp.transform.position = face - along * (0.4f * c.Radius);
            c.Lamp.range = 3f * c.Radius + 1.5f * c.Length + 6f;
            c.Lamp.color = Color.Lerp(new Color(0.85f, 0.45f, 0.75f), new Color(1f, 0.62f, 0.32f), Mathf.Clamp01(strength * 2f));
            c.Lamp.intensity = Mathf.Lerp(c.Lamp.intensity, glow * (0.4f + 3.6f * strength), 0.3f);
        }

        /// <summary>After the game's own effect has had its turn this frame: while ours burns for the craft being flown, its streaks and their light stay off.</summary>
        void LateUpdate()
        {
            if (!drawnForActive || Fire.ReentryHideStock == null || !Fire.ReentryHideStock) return;
            if (stock == null && Time.realtimeSinceStartup > lookedForStock) { stock = FindObjectOfType<AerodynamicsFX>(); lookedForStock = Time.realtimeSinceStartup + 2f; }
            if (stock == null || stock.state < 0.05f) return;
            if (stock.fxCamera != null)
            {
                Camera cam = stock.fxCamera.GetComponent<Camera>();
                if (cam != null && cam.enabled) cam.enabled = false;
            }
            if (stock.fxDepthCamera != null && stock.fxDepthCamera.depthCamera != null && stock.fxDepthCamera.depthCamera.enabled) stock.fxDepthCamera.depthCamera.enabled = false;
            if (stock.fxLight != null && stock.fxLight.enabled) stock.fxLight.enabled = false;
        }
    }
}
#endif
