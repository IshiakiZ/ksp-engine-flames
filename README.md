# Engine Flames

A rocket engine's exhaust as burning gas: a blinding white-hot jet that leaves the nozzle, is stirred by the
air, widens, cools out through yellow into orange tongues, strikes the ground and runs out over it. Worked
out bit by bit and drawn as a true volume, the way [Volumetric Explosions](https://github.com/IshiakiZ/ksp-volumetric-explosions)
makes its fire, in whose air it burns.

![Six flames: lift-off by day and by night, kerosene, hydrogen, thin air, a vacuum](src/EngineFlames/img/engine-flames.jpg)

For Kerbal Space Program 1.12.x. Needs [Volumetric Explosions](https://github.com/IshiakiZ/ksp-volumetric-explosions) 0.6.0 or
later for the look described here (it runs with 0.4 and 0.5, as dimmer and thinner flames) and
[Keystone](https://github.com/IshiakiZ/ksp-keystone); both come in the download. It works beside the Waterfall mod
or without it: while one of its flames burns for an engine, the flame the game or Waterfall draws for that
engine is hidden, and the smoke is left.

**It works wherever Volumetric Explosions draws a volume**: on the game's OpenGL (a Mac, Linux), and on
Windows (Direct3D) with that mod's shaders built for Direct3D. Where it cannot, the mod says so in its
settings and leaves the game's flames as they are.

## What it does

* **Bright, as a rocket's flame is** (0.3.0). A kerosene or solid-fuel engine's jet is a solid body of
  white-hot gas, several times as bright as white paper in the sun, that burns out the picture and glows:
  white for more than half its length, then yellow, and ending in ragged orange tongues. (The first flames
  made this way were thin and orange beside the game's own; in the words of the one who asked for this,
  the game's are "super bright and powerful", and these are now as bright.)
* **Worked out, not drawn.** A flame is some thirteen hundred bits of gas for each nozzle, each moved,
  widened, stirred and cooled frame by frame, and all of them drawn as one lit volume. Its reach, its
  flicker and the way it meets the ground are what the gas did.
* **Fire, not a glow.** A thin yellow and orange sheath where the jet meets the air, torn more the further
  down it; streaks that flow down it; tongues with gaps between them at its far end.
* **What burns shows.** Kerosene and solid fuel blinding; hydrogen a clear pale veil with shock diamonds
  standing in it, in the jet of every nozzle; the small upper-stage engines a clean white-to-pink flame;
  a nuclear engine a faint red.
* **The air shapes it.** A narrow column at the pad; wider and clearer as the air thins; in a vacuum the
  gas fans out from the nozzle's lip and what shows is a bright clear lobe. It changes all the way up.
* **It strikes the ground.** The flame of a rocket on its pad is turned by the pad and runs out over it
  as a sheet of fire; it does the same on a building, the ground or another craft.
* **It stays on its nozzle.** The air it burns in goes along with the ship, so the flame is as steady at
  nine hundred metres a second as on the pad, and is swept back when the ship flies aslant.

![The same craft at the same place, before and with Engine Flames](src/EngineFlames/img/engine-flames-beside.jpg)

*Each pair is one craft at one place, each picture taken in a scene loaded afresh, with the camera's
exposure held still. "Before" is this install as it was: the game's own flames on the solid boosters and
the Waterfall mod's on the liquid engines. The smoke in all of them is the game's.*

## Install

Copy the `Keystone`, `VolumetricExplosions` and `EngineFlames` folders from the download into
your KSP `GameData`.

## Settings

In the Keystone window (the keystone button on the game's toolbar, or Option-K on a Mac, Alt-K elsewhere).

| Setting | Installed as | |
| --- | --- | --- |
| Engine flames | on | off: the flames the game (or Waterfall) draws |
| How bright | 1 | |
| How long | 1 | |
| How wide | 1 | a nozzle's width is measured from the engine's model; this corrects it |
| Hide the game's own flames | on | off: both are drawn |

## Good to know

* **It does not yet move as fire does** (found by playing, 2026-10-11): the flame keeps its line when the ship
  turns, like a rod, where a real one's cooler end would trail; and nothing in it is seen to stream, so it looks
  slow. Being reworked: the gas is to slow along the jet as a real one does, and to be left behind by a nozzle
  that swings away from it.
* Rocket engines only. Jets, ion engines and engines that burn something it does not know keep the flames
  they had.
* The game has one fuel for every liquid engine. Which real fuel each of the game's engines is taken as
  burning is the mod's own choice, by the engine each was modelled on; a config can say otherwise for any
  part, and say it for other mods' engines. See [flames for other engines](docs/Other-engines.md).
* Shock diamonds are drawn where they would stand in a hydrogen or methane flame, in up to eight of a
  ship's jets; they do not come of the gas itself. A kerosene or solid-fuel flame is too bright for any to
  show.
* How bright a flame is to the eye also depends on the camera: where [Natural Light](https://github.com/IshiakiZ/ksp-natural-light)
  is installed its exposure closes a little for a flame by day and a good deal by night, as a real camera's does.
* It gives off no light of its own on the ship or the ground (the [Natural Light](https://github.com/IshiakiZ/ksp-natural-light)
  mod's engine lamps do that); it does light the smoke of [Rocket Smoke](https://github.com/IshiakiZ/ksp-rocket-smoke), which
  is made to go with it.
* It costs less than the flames it hides, where it was measured. A Kerbal X (seven engines) left the pad
  at 35 to 38 frames a second with the game's and Waterfall's flames and at 40 with these, and climbed at 40
  to 42 and at 44 to 46 (two flights each way, on an Apple M5 Pro at 1280 by 720, with Scatterer, EVE, TUFX,
  Natural Light, Natural Weather and Rocket Smoke).
* Made on 2026-10-09, made bright on 2026-10-10, and looked at in pictures and frame by frame: on a test
  stand from the pad to a vacuum (a Mainsail, a Swivel, a Kickback, a Vector, a Mammoth, a Poodle, a Spark,
  a Nerv), at lift-off by day and night, and on a climb. Nobody has played with it yet.
* Made and tried on a Mac; tried on Windows too (see below).

## Where it works

| System | |
| --- | --- |
| Mac | Made and tried here (KSP 1.12.5, OpenGL). |
| Windows | Tried on Windows 11 (KSP 1.12.5, Direct3D 11), with Volumetric Explosions' shaders built for Direct3D: Kerbal X from the pad to thin air, its flames drawn into the explosions' volume ("1 flame is burning" on its page) and widening into a soft plume high up. Then at night (the sun 23 degrees down): Kerbal X's flames a bright plume climbing into the dark, 72 frames a second at lift-off (the game's cap not lifted); and solid boosters at night (Kerbal 1-5's two Hammers): two orange flames climbing on their trail, 102 frames a second. All of that was 0.2.0. Then 0.3.0's bright flames (2026-10-11, by day, with Volumetric Explosions 0.6.3 and its shaders built for Direct3D again that day): Kerbal X from the pad, filmed from 75 m: at ignition a white flame under the rocket and a ring of orange fire on the pad's grate; as it left, a flame longer than the rocket, white at the nozzles and yellow to orange behind, with a glow round it, and the pad lit white by it; followed up to 700 m, white at the nozzles and running into its brown smoke. Kerbal 1-5's two solid boosters: a flame each, as bright. Both launches looked the same filmed on OpenGL on the same computer (the game started with `-force-glcore`); the first was seen again with TUFX taken out (less glow round the flame), and on the release builds. Shock diamonds could not be made out from that far. Not looked at there: 0.3.0 at night, Waterfall. |
| Linux | **Never run.** It uses the same OpenGL shader as the Mac. |

More: [how it works](docs/How-it-works.md).

## Licence

[CC BY 4.0](LICENSE): use it, change it and pass it on as you like, as long as you credit Ishiaki and link back here.
