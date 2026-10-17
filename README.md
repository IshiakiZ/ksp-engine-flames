# Engine Flames

A rocket engine's exhaust as burning gas: a blinding white-hot jet that leaves the nozzle, widens, cools out through
yellow into orange tongues, strikes the ground and runs out over it. Worked out bit by bit and drawn as a true
volume, not painted.

![Six flames: day and night lift-off, kerosene, hydrogen, thin air, vacuum](src/EngineFlames/img/engine-flames.jpg)

![A big engine's flame on a test stand, its tongues breaking up as they cool](src/EngineFlames/img/engine-flames-stand.gif)

![A rocket climbing at 4 km over the sea, its flame and trail](src/EngineFlames/img/engine-flames-climb.gif)

For Kerbal Space Program 1.12.x. **Needs two other mods**, each a separate download, and does not load without both:
[Keystone](https://github.com/IshiakiZ/ksp-keystone) ([on SpaceDock](https://spacedock.info/mod/4665/Keystone)) and
[Volumetric Explosions](https://github.com/IshiakiZ/ksp-volumetric-explosions) 0.6.4 or later
([on SpaceDock](https://spacedock.info/mod/4669/Volumetric%20Explosions)), in whose air the flame burns.

## What it does

* **Bright, as a rocket's flame is:** white-hot at the nozzle, then yellow, ending in ragged orange tongues.
* **What burns shows:** kerosene and solid fuel blinding, hydrogen a pale veil with shock diamonds in it, a nuclear
  engine a faint red.
* **The air shapes it:** a narrow column at the pad, wider as the air thins, a clear lobe in a vacuum.
* **It moves as fire does:** it wavers, trails when the ship turns, and is turned by the pad, the ground or another
  craft.
* With or without Waterfall: the game's or Waterfall's flame is hidden while this one burns.
* **Re-entry fire** (with Volumetric Explosions 0.7.0): white-hot air standing off a craft's heat shield, streaming
  back past its sides into a wake cooling from gold to pink-violet, where the game's flat streaks would be.

![A capsule coming into the air at 2 km/s, wrapped in its own fire](src/EngineFlames/img/engine-flames-reentry.gif)

## Install

Copy the `EngineFlames` folder from the download into your KSP's `GameData`, beside `Keystone` and
`VolumetricExplosions`.

## Settings

In the Keystone window (the keystone button on the game's toolbar, or Alt-K; Option-K on a Mac): how bright, long
and wide the flame is, the re-entry fire, and whether the game's own flames and streaks are hidden.

## Good to know

* Rocket engines only. Jets, ion engines and engines that burn something it does not know keep the flames they had.
  [Flames for other mods' engines](docs/Other-engines.md).
* It lights the smoke of [Rocket Smoke](https://github.com/IshiakiZ/ksp-rocket-smoke); the light on ship and ground is
  [Natural Light](https://github.com/IshiakiZ/ksp-natural-light)'s.
* The flame costs less than the flames it hides, where it was measured.

## Works on

Mac: tried. Windows 11 (Direct3D 11 and OpenGL): tried before the flame's rework and the re-entry fire. Linux: never
run.

**More:** [the long description](docs/The-long-description.md) (everything in full, and what
was tried on each system) and the rest of the [wiki](docs/README.md).

## Licence

[CC BY 4.0](LICENSE): use it, change it and pass it on as you like, as long as you credit Ishiaki and link back here.
