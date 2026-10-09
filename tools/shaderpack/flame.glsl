#ifdef VERTEX
#version 150
uniform vec4 hlslcc_mtx4x4unity_ObjectToWorld[4];
uniform vec4 hlslcc_mtx4x4unity_MatrixVP[4];
in vec3 in_POSITION0;
out vec3 vs_TEXCOORD0;
out vec4 vs_TEXCOORD1;
void main()
{
    vec4 world = hlslcc_mtx4x4unity_ObjectToWorld[0] * in_POSITION0.x + hlslcc_mtx4x4unity_ObjectToWorld[1] * in_POSITION0.y + hlslcc_mtx4x4unity_ObjectToWorld[2] * in_POSITION0.z + hlslcc_mtx4x4unity_ObjectToWorld[3];
    vec4 clip = hlslcc_mtx4x4unity_MatrixVP[0] * world.x + hlslcc_mtx4x4unity_MatrixVP[1] * world.y + hlslcc_mtx4x4unity_MatrixVP[2] * world.z + hlslcc_mtx4x4unity_MatrixVP[3] * world.w;
    // (the far faces of the box are what is drawn, so that it is drawn with the camera inside it too; they are kept inside the camera's range)
    clip.z = min(clip.z, clip.w * 0.99999);
    gl_Position = clip;
    vs_TEXCOORD0 = world.xyz;
    vs_TEXCOORD1 = clip;
}
#endif
#ifdef FRAGMENT
#version 150
// An engine's flame, as a volume.
//
// The flame is a box that begins at the nozzle's rim and lies along the jet: z runs from the rim (-0.5) to
// the flame's end (+0.5), and x and y across it. For each pixel of the box the line of sight is walked
// through it in steps from the near side to the far. At each step the gas there gives off light, and hides
// a little of what is behind it: so a sooty flame shows its near surface, with the lumps and creases on
// it, and a clean one is seen right through. What the gas is like at a place is worked out from where the
// place is, not kept anywhere:
//
// * It is a jet: hottest on its axis and near the nozzle, cooler outwards and downstream, and wider the
//   further it has gone. How fast it widens is the air's doing: at sea level the air holds it to a narrow
//   column; with less air it opens into a cone, and in none at all into a wide bell. The same gas spread
//   over more room is thinner: a wide jet is a faint one.
// * Where the air is thick the jet is squeezed and springs back, again and again. Its edge necks in and
//   out, and where the shocks cross on the axis the gas is pressed hot: a row of bright diamonds down the
//   middle, each beginning sharply, fading as the gas spreads again, and dimmer than the one before.
// * Its edge is torn by the air it meets, more so the further down, it is hot in lumps with cooler creases
//   between them, and it ends in tongues: noise that travels with the gas, in lumps as large as the jet is
//   wide where they are.
// * Some flames are dark where they leave the nozzle and light a little way out, where the air has got in.
// * Where the jet meets the ground it does not go into it: it is turned, and runs out along the ground on
//   every side as a thin sheet of flame, thinner and cooler the further it has run, and further the way
//   the jet was going where it met the ground aslant.
//
// How hot the gas is at a place is one number, and its light is that number's colour: the colours of what
// is burning, from coolest to hottest, are given.
uniform vec3 _WorldSpaceCameraPos;
uniform vec4 _ProjectionParams;
uniform vec4 _ZBufferParams;
uniform vec4 hlslcc_mtx4x4unity_WorldToObject[4];
uniform vec4 hlslcc_mtx4x4unity_MatrixV[4];
uniform vec4 _FlameCamera;   // x: 1 if the camera drawing this has a depth picture of the scene
uniform vec4 _FlameSize;     // x: half the box's width (m), y: its length (m), z: the nozzle's radius (m), w: how bright
uniform vec4 _FlameShape;    // x: the jet's radius at its end (nozzle radii), y: how it opens along the flame (a power: under 1, a bell), z: how much of the gas keeps to the axis (0: the jet is all of a piece; 5: a streak down the middle of a faint bell), w: the length of one shock cell (m)
uniform vec4 _FlameAir;      // x: the air's pressure (atmospheres), y: the time (s), z: a number of this flame's own, w: how fast the lumps travel (jet radii a second)
uniform vec4 _FlameLook;     // x: how torn the edge is, y: how strong the shock diamonds are, z: how the light falls off along the flame (a power), w: how much it hides what is behind it
uniform vec4 _FlameMore;     // x: how fast the light thins as the jet widens (a power), y: how far out the flame lights (nozzle radii; 0: at the rim), z: how far the edge necks in between the diamonds (a part of its radius), w: how much the light comes in lumps (0 to 1)
uniform vec4 _FlameBox;      // x: the box's length (m): the flame's, or less where the ground cuts it short; y: where along the jet it begins (m from the rim: 0, or before the rim where flame on the ground stands higher than that); z: how far the flame runs along the ground (m); w: how much of a skirt it has (0 in thick air)
uniform vec4 _FlameGround;   // xyz: which way the ground faces where the jet meets it, in the box's own frame; w: how far along the jet that is (m), or under 0 where it meets none
uniform vec4 _FlameCone;     // x, y: the radius (m) at the rim and at the flame's end of a cone that the whole jet is inside of (so that the steps are spent where the jet is, not on the empty corners of the box)
uniform vec4 _FlameHot;      // rgb: the colour of the hottest gas, on the axis
uniform vec4 _FlameMid;      // rgb: of the body of the flame
uniform vec4 _FlameCool;     // rgb: of its edge and tail
uniform sampler2D _CameraDepthTexture;
in vec3 vs_TEXCOORD0;
in vec4 vs_TEXCOORD1;
out vec4 SV_Target0;

float hash(vec3 p)
{
    p = fract(p * 0.3183099 + 0.1);
    p *= 17.0;
    return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float noise(vec3 x)
{
    vec3 i = floor(x), f = fract(x);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(hash(i), hash(i + vec3(1.0, 0.0, 0.0)), f.x), mix(hash(i + vec3(0.0, 1.0, 0.0)), hash(i + vec3(1.0, 1.0, 0.0)), f.x), f.y),
               mix(mix(hash(i + vec3(0.0, 0.0, 1.0)), hash(i + vec3(1.0, 0.0, 1.0)), f.x), mix(hash(i + vec3(0.0, 1.0, 1.0)), hash(i + vec3(1.0, 1.0, 1.0)), f.x), f.y), f.z);
}

// The light of gas so hot (0: none; 1: as hot as this flame gets; over 1, in the diamonds: brighter still).
vec3 lightOf(float heat)
{
    vec3 c = _FlameCool.rgb * smoothstep(0.0, 0.3, heat);
    c = mix(c, _FlameMid.rgb, smoothstep(0.25, 0.6, heat));
    c = mix(c, _FlameHot.rgb, smoothstep(0.55, 1.0, heat));
    return c * (1.0 + max(heat - 1.0, 0.0));
}

void main()
{
    vec3 eye = _WorldSpaceCameraPos;
    vec3 look = normalize(vs_TEXCOORD0 - eye);
    float forward = max(-(hlslcc_mtx4x4unity_MatrixV[0].z * look.x + hlslcc_mtx4x4unity_MatrixV[1].z * look.y + hlslcc_mtx4x4unity_MatrixV[2].z * look.z), 1e-4);
    // ---- the line of sight in the box's own frame, and where it is inside the box
    vec3 from = hlslcc_mtx4x4unity_WorldToObject[0].xyz * eye.x + hlslcc_mtx4x4unity_WorldToObject[1].xyz * eye.y + hlslcc_mtx4x4unity_WorldToObject[2].xyz * eye.z + hlslcc_mtx4x4unity_WorldToObject[3].xyz;
    vec3 way = hlslcc_mtx4x4unity_WorldToObject[0].xyz * look.x + hlslcc_mtx4x4unity_WorldToObject[1].xyz * look.y + hlslcc_mtx4x4unity_WorldToObject[2].xyz * look.z;
    way += vec3(equal(way, vec3(0.0))) * 1e-6;
    vec3 a = (-0.5 - from) / way, b = (0.5 - from) / way;
    vec3 near = min(a, b), far = max(a, b);
    float t0 = max(max(near.x, near.y), near.z), t1 = min(min(far.x, far.y), far.z);
    // (each of the game's cameras draws its own range of distances, and adds up only what is within it; and nothing behind what is already drawn)
    t0 = max(t0, _ProjectionParams.y / forward);
    t1 = min(t1, _ProjectionParams.z / forward);
    vec2 uv = vs_TEXCOORD1.xy / vs_TEXCOORD1.w * 0.5 + 0.5;
    if (_FlameCamera.x > 0.5)
    {
        float depth = 1.0 / (_ZBufferParams.z * texture(_CameraDepthTexture, uv).x + _ZBufferParams.w);
        t1 = min(t1, depth / forward);
    }
    if (t1 <= t0) discard;

    float halfWide = _FlameSize.x, len = _FlameSize.y, nozzle = max(_FlameSize.z, 1e-3);
    float widest = max(_FlameShape.x, 1.0), cellLen = max(_FlameShape.w, 1e-3);
    float time = _FlameAir.y, own = _FlameAir.z;
    // (how fast it widens, for measuring along it in its own radii: lumps as big as the jet is wide keep their shape as it widens)
    float grow = max((widest - 1.0) * nozzle / len, 1e-3);
    // (each pixel starts its steps a little differently, so that the steps do not show as bands)
    float start = fract(52.9829189 * fract(dot(gl_FragCoord.xy, vec2(0.06711056, 0.00583715))) + fract(time * 61.803));
    vec3 lit = vec3(0.0);
    float clear = 1.0;                                          // how much of what is behind still comes through
    bool grounded = _FlameGround.w >= 0.0;
    float met = clamp(_FlameGround.w / len, 0.0, 1.0);
    float Rmet = nozzle * mix(1.06, widest, pow(met, _FlameShape.y));       // the jet's radius where it meets the ground

    // ---- the jet itself: walked only where the line of sight is inside the cone that holds it
    // (the line of sight in metres, from the middle of the rim)
    vec3 m0 = vec3(from.xy * (2.0 * halfWide), _FlameBox.y + (from.z + 0.5) * _FlameBox.x);
    vec3 mw = vec3(way.xy * (2.0 * halfWide), way.z * _FlameBox.x);
    float j0 = t0, j1 = t1;
    {
        float slope = (_FlameCone.y - _FlameCone.x) / len;
        float c0 = _FlameCone.x + slope * m0.z, c1 = slope * mw.z;
        float A = dot(mw.xy, mw.xy) - c1 * c1, B = 2.0 * (dot(m0.xy, mw.xy) - c0 * c1), C = dot(m0.xy, m0.xy) - c0 * c0;
        // (looked at along its length, the line of sight is inside it from end to end, and the whole of it is walked)
        if (A > 1e-4)
        {
            float disc = B * B - 4.0 * A * C;
            if (disc <= 0.0) j1 = j0;
            else
            {
                float root = sqrt(disc);
                j0 = max(t0, (-B - root) / (2.0 * A));
                j1 = min(t1, (-B + root) / (2.0 * A));
            }
        }
    }
    const int steps = 30;
    float dt = max(j1 - j0, 0.0) / float(steps);
    // (so much for each nozzle radius of gas looked through, whatever size the engine is)
    float each = dt / nozzle;
    for (int i = 0; i < steps; i++)
    {
        if (j1 <= j0) break;
        vec3 p = from + way * (j0 + (float(i) + start) * dt);
        float z = _FlameBox.y + (p.z + 0.5) * _FlameBox.x;      // along the flame from the rim, in metres
        float s = clamp(z / len, 0.0, 1.0);                     // and as a part of its whole length
        vec2 off = p.xy * (2.0 * halfWide);                     // across it, in metres from the axis
        float r = length(off);
        float cell = max(z, 0.0) / cellLen;
        // the jet's radius here: opening as it goes, and necking in and out with the shocks
        float R = nozzle * mix(1.06, widest, pow(s, _FlameShape.y));
        R *= 1.0 - _FlameMore.z * exp(-cell * 0.4) * (0.5 - 0.5 * cos(6.2831853 * cell));
        // What the air does to it, in the jet's own measure. The gas goes by far too fast to be seen as lumps: what is seen
        // of it is streaks along the jet, and they are what its edge and its heat are streaked with. What is seen to move
        // is the slow heave of the whole flame, and that is what tears its edge and breaks its end into tongues.
        float along = log(1.0 + grow * max(z, 0.0) / nozzle) / grow;
        vec3 q = vec3(off / R, along);
        float heave = noise(vec3(q.xy * 0.7, q.z * 0.45 - time * _FlameAir.w * 0.16) + own) * 0.62 + noise(vec3(q.xy * 1.6, q.z * 1.0 - time * _FlameAir.w * 0.3) + own * 1.7) * 0.38;
        float s1 = noise(vec3(q.xy * 2.6, q.z * 0.5 - time * _FlameAir.w) - own), s2 = noise(vec3(q.xy * 5.5, q.z * 1.1 - time * _FlameAir.w * 1.3) + own * 2.3);
        float streaks = s1 * 0.6 + s2 * 0.4;
        // (in thick air it does not go on widening to its end: it burns out, and what is left narrows to a ragged point)
        float point = mix(1.0, 1.0 - 0.6 * smoothstep(0.45, 1.0, s), min(_FlameLook.x * 3.0, 1.0));
        float x = r / (R * point) + ((heave - 0.5) * (0.15 + 1.7 * s) + (streaks - 0.5) * 0.3 * smoothstep(0.0, 0.25, s)) * _FlameLook.x;
        // how much gas: all of a piece where it leaves the nozzle, softer as the air gets into it; thinner as it widens; and ending in tongues
        float inner = mix(0.88, 0.25, smoothstep(0.0, 0.6, s));
        float body = (1.0 - smoothstep(inner, 1.0, x)) * exp(-x * x * _FlameShape.z);
        float spread = pow(nozzle / R, _FlameMore.x);
        float cut = mix(-0.3, 0.55, pow(s, 1.1));
        float tongues = mix(1.0, smoothstep(cut, cut + 0.25, heave), min(_FlameLook.x * 3.0, 1.0));
        float gas = body * spread * tongues * smoothstep(0.0, 0.012, s);
        // (in thin air, round that: the wide skirt of gas that has got away sideways, faint, and with the faintest of the flame's colours)
        float skirt = (1.0 - smoothstep(0.35, 1.0, x)) * pow(nozzle / R, 0.85) * pow(1.0 - s, 1.5) * smoothstep(0.0, 0.012, s) * _FlameBox.w;
        // how hot: hottest on the axis and at the rim, cooler outwards and along; streaked, most where the air is getting in; and where it has not lit yet, hot only at its heart
        float heat = (1.0 - 0.5 * smoothstep(0.0, 1.0, x)) * pow(1.0 - s, _FlameLook.z);
        heat *= mix(1.0, 0.55 + 0.9 * streaks, _FlameMore.w * smoothstep(0.15, 0.7, x + s));
        if (_FlameMore.y > 0.0) heat *= mix(mix(0.3, 1.0, 1.0 - smoothstep(0.25, 0.6, x)), 1.0, smoothstep(0.0, _FlameMore.y * nozzle, z));
        // the diamonds: each begins where the shocks cross, a little over half a cell out, widest there, and fades as the gas spreads again
        float c = cell - 0.62;
        float which = floor(c), part = c - which;
        float wide = 0.5 - 0.36 * part;
        heat += step(0.0, c) * smoothstep(0.0, 0.08, part) * pow(1.0 - part, 1.4) * exp(-which * 0.5) * (1.0 - smoothstep(wide * 0.45, wide, x)) * _FlameLook.y;
        heat = (heat * gas + 0.27 * skirt) / max(gas + skirt, 1e-5);
        gas += skirt;
        if (grounded)
        {
            // (the jet stops at the ground; and where it strikes, it is pressed hot)
            vec3 rel = vec3(off, z - _FlameGround.w);
            float high = dot(rel, _FlameGround.xyz);
            gas *= smoothstep(0.0, 0.04 + 0.1 * nozzle, high);
            heat += 0.3 * exp(-max(high, 0.0) / (0.4 * Rmet)) * (1.0 - smoothstep(0.5 * Rmet, 1.2 * Rmet, length(rel - _FlameGround.xyz * high)));
        }
        lit += lightOf(heat) * (gas * each * clear);
        // (what hides is the glowing soot: where the gas has cooled it hides nothing, and is the smoke's business)
        clear *= exp(-gas * each * _FlameLook.w * smoothstep(0.25, 0.7, heat));
    }

    // ---- the flame on the ground: a thin sheet, so the line of sight is walked again where it is within the sheet's height, in finer steps
    if (grounded)
    {
        vec3 faces = _FlameGround.xyz;
        // (the line of sight in metres, from where the jet meets the ground)
        vec3 g0 = m0 - vec3(0.0, 0.0, _FlameGround.w);
        float h0 = dot(g0, faces), hd = dot(mw, faces);
        float top = (Rmet * 0.4 + 0.1 * _FlameBox.z) * 2.4;               // (no sheet stands higher than this)
        float u0 = t0, u1 = t1;
        if (abs(hd) > 1e-5)
        {
            float ta = -h0 / hd, tb = (top - h0) / hd;
            u0 = max(t0, min(ta, tb)); u1 = min(t1, max(ta, tb));
        }
        else if (h0 < 0.0 || h0 > top) u1 = u0;
        if (u1 > u0)
        {
            const int more = 16;
            float du = (u1 - u0) / float(more);
            float eachW = du / nozzle;
            // (where the jet came down aslant, more of it runs on the way it was going)
            vec3 slide = vec3(0.0, 0.0, 1.0) - faces * faces.z;
            for (int i = 0; i < more; i++)
            {
                vec3 rel = g0 + mw * (u0 + (float(i) + start) * du);
                float high = dot(rel, faces);                           // how far over the ground this place is
                vec3 flat_ = rel - faces * high;
                float out_ = length(flat_);                             // and how far from where the jet meets it
                vec3 outward = flat_ / max(out_, 1e-3);
                float run = max(out_ - Rmet * 0.6, 0.0);                // how far the gas here has run along the ground
                float fan = Rmet / max(out_, Rmet) * max(1.0 + 1.6 * dot(outward, slide), 0.12);
                vec3 qw = (rel - outward * (time * _FlameAir.w * 0.2 * Rmet)) / (0.7 * Rmet);
                float heaveW = noise(qw * 0.7 + own) * 0.62 + noise(qw * 1.7 - own) * 0.38;
                float streaksW = noise(qw * 2.4 + own * 3.1 - outward * (time * _FlameAir.w * 0.5));
                // (how far the gas here has run, of as far as it runs at all: it runs out in fingers, so not the same all round)
                float fingers = noise(vec3(outward.xy * 1.9 + outward.z, run / (4.0 * Rmet) - time * _FlameAir.w * 0.06) + own * 0.7);
                float gone = clamp(run / _FlameBox.z + ((heaveW - 0.5) * 0.35 + (fingers - 0.5) * 0.9) * smoothstep(0.0, Rmet * 2.0, run), 0.0, 1.0);
                float thickness = (Rmet * 0.4 + 0.1 * run) * (0.45 + 1.1 * heaveW);
                float cutW = mix(-0.3, 0.55, pow(gone, 1.1));
                float sheet = exp(-(high * high) / (thickness * thickness)) * step(0.0, high) * smoothstep(Rmet * 0.3, Rmet * 0.9, out_);
                float tonguesW = smoothstep(cutW, cutW + 0.25, heaveW) * mix(1.0, smoothstep(0.3, 0.65, streaksW * 0.5 + heaveW * 0.5) * 1.8, smoothstep(0.1, 0.6, gone));
                float gasW = sheet * fan * pow(nozzle / Rmet, _FlameMore.x) * tonguesW * (1.0 - smoothstep(0.7, 1.0, gone)) * (1.0 - smoothstep(0.85, 1.0, run / _FlameBox.z)) * 2.0;
                // (it is cooler than the jet was where it struck: it has taken in air and given its heat to the ground)
                float heatW = 0.75 * pow(1.0 - met, _FlameLook.z) * pow(1.0 - gone, 0.6) * (0.75 + 0.5 * streaksW);
                lit += lightOf(heatW) * (gasW * eachW * clear);
                // (gas that has run along the ground has the dust and the soot of it: it hides more)
                clear *= exp(-gasW * 4.0 * eachW * _FlameLook.w * smoothstep(0.25, 0.7, heatW));
            }
        }
    }
    float flicker = 0.9 + 0.2 * noise(vec3(time * 13.0, own * 3.0, 0.5));
    lit *= _FlameSize.w * flicker;
    // (looked at along its length there is no end of gas in the line of sight: past four times white its light rises more and more slowly, so that it blinds without wiping out the picture)
    float most = max(lit.r, max(lit.g, lit.b));
    if (most > 4.0) lit *= (4.0 + (most - 4.0) / (1.0 + (most - 4.0) / 8.0)) / most;
    float hides = 1.0 - clear;
    if (lit.r + lit.g + lit.b + hides <= 0.0004) discard;
    SV_Target0 = vec4(lit, hides);
}
#endif
