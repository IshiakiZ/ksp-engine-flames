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
        }

        void OnDestroy()
        {
            if (hooked) { try { Feed.Unhook(Asked); } catch (Exception) { } }
            foreach (Craft c in crafts.Values) if (c.Lamp != null) Destroy(c.Lamp.gameObject);
            crafts.Clear();
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
                    bool taken = Feed.Plasma(c.Id, face, along, v.srf_velocity, c.Radius, neck, length, running, fire, v.transform);
#if DEV
                    if (say) Fire.Log("re-entry: " + v.vesselName + " face " + c.FaceL + " radius " + c.Radius.ToString("F2") + " length " + c.Length.ToString("F1") + " wake " + length.ToString("F1") + " fire " + fire.ToString("F2") + " taken " + taken);
#endif
                    c.Strength = taken ? strength : 0f;
                    if (!taken) continue;
                    c.Seen = now;
                    glowing++;
                    if (v == FlightGlobals.ActiveVessel) drawnForActive = true;
                    Lamp(c, face, along, strength, glow);
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
        }

        /// <summary>The glow's own light on the craft: from just ahead of the face, the colour of the glow at its strength (pink-violet faint, orange and gold strong).</summary>
        void Lamp(Craft c, Vector3 face, Vector3 along, float strength, float glow)
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
