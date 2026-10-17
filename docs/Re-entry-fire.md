# The re-entry fire (0.4.0)

Asked for on 2026-10-14: "make the heat fire when entering an atmosphere better". It needs
[Volumetric Explosions](https://github.com/IshiakiZ/ksp-volumetric-explosions) 0.7.0 or later; with an older one it does
nothing and the game's own effect is left as it was.

## What it is meant to look like

Film of real capsules coming in shows a bright layer standing a little off the heat shield, orange-yellow to white at
its hottest, the glow wrapping the body and a long wake behind it cooling to orange, red and the pink-violet of hot air's
own light. The game draws something else: flat streaks pulled back from the craft's outline by a second camera, with a
light.

## How it is made

* **When:** by the game's own measure, the one its streaks go by (`AerodynamicsFX`): half the air's density (as the game
  weighs it) times the speed to the power 3.5, and how far past the speed of sound the craft is going. So it burns where
  and when the game's own would, for every craft loaded near by, not only the one being flown.
* **Where:** the craft's meshes are measured twice a second (and at once when it turns): the face is the front of the
  craft as the air meets it; the width is that of its front two fifths, so a heat shield sets it and a solar panel further
  back does not.
* **What:** hot gas in Volumetric Explosions' air, a new kind of source there (`ExhaustKind.Plasma`) with a ninth kind of
  fire of its own colours (white at its hottest, then gold, orange, and pink to violet as it cools). Three parts: a thin
  shock layer standing off the face and flowing out to its rim; a sheath streaming back past the body's sides; a wake
  behind, from about the craft's own length behind the face, longer the hotter it is (a few of the body's widths at first,
  a dozen at the height of it). The gas is held in the craft's own frame: the air rushing past holds it there.
* **Light:** a light of the glow's colour shines on the craft from just ahead of its face (pink-violet when faint, orange
  and gold when strong), and fades when the glow is gone.
* **The game's own:** while the glow burns for the craft being flown, the game's streaks and their light are kept off.
  Its sound and its shaking are left as they are.

## Settings (Engine Flames' Keystone page)

On or off; how bright; whether the game's own streaks are hidden.

## What was tried

On the Mac, in the owner's game, on a development build: a capsule with a 2.5 m heat shield put on a pass through
Kerbin's air at 42 km and 2.1 km/s, shield first, filmed beside the game's own effect in the same pass. The face and width
were found right (radius 1.29 m). With the game's streaks the pass ran at 34 frames a second; with the glow at 24 at
first, about 29 after its tuning (about what an engine's flame costs).

## Limits

* A first version, tuned by eye on one capsule at one speed. Not yet tried on a spaceplane, a long stage falling
  sideways, a craft breaking up, or another world's air (Eve, Duna, Laythe): the colours are the same in every air.
* It costs a few milliseconds a frame while it burns.
* No shader was changed for it, so on Windows it uses the shaders already built there; it has not been run on Windows.
