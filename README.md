# Engine Flames

A rocket engine's exhaust drawn as a true volume of glowing gas: shaped by the air it is in, coloured by what
it burns, and spreading over the ground where it strikes it.

![Six flames: lift-off by day and by night, kerosene, hydrogen, thin air, a vacuum](src/EngineFlames/img/engine-flames.jpg)

For Kerbal Space Program 1.12.x. Needs [Keystone](https://github.com/IshiakiZ/ksp-keystone), which comes in the download.
It works beside the Waterfall mod or without it: while one of its flames is drawn for an engine, the flame
the game or Waterfall draws for that engine is hidden, and the smoke is left.

**It is made for the game's OpenGL (a Mac, Linux).** On Windows (Direct3D) there is no flame shader yet:
the mod says so in its settings and leaves the game's flames as they are.

## What it does

* **A volume, not pictures.** Every pixel's line of sight is walked through the flame, so it has depth,
  looks right from any side and from inside, and stops at whatever stands in front of it.
* **The air shapes it.** A narrow column with shock diamonds at the pad; wider and softer as the air thins;
  a pale plume with a faint wide skirt in a vacuum. It changes all the way up, not in steps.
* **It runs out over the ground.** Where a flame strikes the pad, the ground or the sea it is turned and
  spreads along it.
* **What burns shows.** Kerosene yellow-white and orange, hydrogen pale blue and all diamonds, the small
  upper-stage engines blue-white, solid boosters brilliant and thick, a nuclear engine a faint red.
* **It starts and stops.** An engine just lit flares wide and ragged for most of a second before it
  settles; one shut down or throttled back shrinks away.

![The same craft at the same place, before and with Engine Flames](src/EngineFlames/img/engine-flames-beside.jpg)

*Each pair is one craft at one place, each picture taken in a scene loaded afresh, with the camera's
exposure held still. "Before" is this install as it was: the game's own flames on the solid boosters and
the Waterfall mod's on the liquid engines.*

## Install

Copy the `Keystone` and `EngineFlames` folders from the download's `GameData` into your KSP `GameData`.

## Settings

In the Keystone window (the keystone button on the game's toolbar, or Option-K on a Mac, Alt-K elsewhere).

| Setting | Installed as | |
| --- | --- | --- |
| Engine flames | on | off: the flames the game (or Waterfall) draws |
| How bright | 1 | |
| How long | 1 | |
| How wide | 1 | a nozzle's width is measured from the engine's model; this corrects it |
| How torn | 1 | how much the air tears the flame's edge. 0: smooth |
| Shock diamonds | on | the row of bright diamonds down a flame in thick air |
| Flame along the ground | on | off: a flame stops where it strikes the ground |
| Hide the game's own flames | on | off: both are drawn |

## Good to know

* Rocket engines only. Jets, ion engines and engines that burn something it does not know keep the flames
  they had.
* The game has one fuel for every liquid engine. Which real fuel each of the game's engines is drawn as
  burning is the mod's own choice, by the engine each was modelled on; a config can say otherwise for any
  part, and say it for other mods' engines. See [flames for other engines](docs/Other-engines.md).
* It costs about what Waterfall's flames cost: with three or four engines burning in view the frame rate
  was the same with either. With the camera right beside four large flames, so that they filled the
  picture, it was 61 frames a second against 82 (an Apple-silicon Mac, 1280 by 720).
* Made on 2026-10-09 and looked at in pictures and frame by frame: on a test stand at every height, on
  the pad by day and night, and on one flight to orbit. Nobody has played with it yet.
* Made and tried on a Mac. Linux uses the same OpenGL shader and should work, but it has **not been run**
  there. On Windows it does nothing yet (see above).

More: [how it works](docs/How-it-works.md).
