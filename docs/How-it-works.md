# How it works

## A flame is gas, and the gas is worked out

The first version of this mod drew each flame as a shape: a box along the jet, and a rule that said how
bright the gas was at each place in it. It was a careful shape, with shock diamonds and a torn edge, and it
looked drawn. A real flame looks as it does because of what the gas in it is doing, and no rule about
places has that in it.

So a flame is now what an explosion's fire is in [Volumetric Explosions](https://github.com/IshiakiZ/ksp-volumetric-explosions),
and is made by that mod. Every frame, each running nozzle puts out some dozens of bits of burning gas.
Each bit:

* leaves the nozzle's mouth along the jet, the ones in the middle of the jet fastest and hottest;
* keeps most of its speed all the way, and is carried off by the air as that goes by;
* widens as it goes, taking in the air beside it;
* is stirred by the air, its edge far more than its middle;
* cools: white at the nozzle, yellow down most of the flame's length, out through orange in the last third;
* is turned where it strikes the ground or a building, and runs on along it.

A few hundred of these are alive at once for each nozzle. They are put on a grid of cells, as a blast's
smoke and fire are, and the grid is drawn as one lit volume: for every pixel the line of sight is walked
through it and the light of the gas along it is added up. The flame's edge, its flicker, its reach and the
way it lies on a launch pad are whatever the gas did. Nothing of them is laid down beforehand.

This mod's own part in it is small. It says which engines are rocket engines, what each burns, where each
nozzle's mouth is and how wide, and how hard the engine is running; and it hides the flames the game had.

## The air that goes along with the ship

A rocket doing three hundred metres a second goes six metres between one frame and the next, and the
grid a flame is drawn from is a frame or two old. Worked out in air that stays where it is, as an
explosion's is, a flame would be left behind its nozzle and catch it up in jumps.

So the flames of a ship's engines are worked out in a patch of air that goes along with the ship. The
nozzles stand still in it. The air goes by at the ship's speed, which is what sweeps a flame back when a
ship flies aslant. The ground under it is looked for afresh every frame. And its grid is turned, every
time one is made, to lie along the jets as the ship then points, so that its cells are finest across
the flame, where a flame is narrow.

## Where the nozzle is

The game says where an engine pushes from, which is not where its bell ends: it can be half a metre up
inside the bell or below it. So the first time an engine runs, its model is measured: of everything solid
in it near the jet's line, the part furthest along the line is the rim, and the widest thing there is the
rim's radius. (Shrouds that are cast off are left out; by the time an engine runs they are gone anyway.)
Where a model cannot be read, or gives something a nozzle could not be, the radius is worked out from the
engine's thrust instead, and the flame begins where the engine pushes from.

Every size in the flame is a multiple of that radius: an Ant's flame is a Mainsail's seen from further off.

## What the air does

How thin the air is shapes the flame all the way up:

| | At the pad | 8 km up | 16 km | 26 km | Vacuum |
| --- | --- | --- | --- | --- | --- |
| Air (Kerbin), against sea level | 1 | about a quarter | about a twentieth | under a hundredth | none |
| How much wider the jet gets for each metre it goes | 0.035 m | 0.08 | 0.14 | 0.23 | 0.36 |
| How far it reaches, against its length at the pad | all of it | 0.93 | 0.82 | 0.67 | 0.45 |
| Its edge | torn | | less | nearly smooth | smooth |
| Its colour as it goes | white, yellow, orange | | | paler: it thins before it cools | white, fading |

In thick air the jet is held to a narrow column, the air tears its edge, and it cools through orange
before it is spent. With little air or none there is nothing to hold it in and nothing to tear it: it
opens into a wide bell, is spent in half the length, and stays pale because it has spread to nothing
before it has cooled.

## The ground

Gas that would go into the ground is turned along it instead, the way it was going if it was going any
way, and otherwise outward on every side: the flame of a rocket standing on its pad runs out over the pad
as a sheet of fire, and the smoke of [Rocket Smoke](https://github.com/IshiakiZ/ksp-rocket-smoke), where
that is installed, rolls up from it. The same is done for buildings and for other craft.

## What an engine burns

See [flames for other engines](Other-engines.md). What is burning sets the flame's colours, how bright it
is, and a little of its length and width:

| Fuel | Colours | How bright, against kerosene |
| --- | --- | --- |
| kerosene | white, yellow, orange | 1 |
| hydrogen | white to pale blue | 0.38 |
| methane | white to blue and violet | 0.6 |
| the fuels that keep | white to blue-white | 0.45 |
| solid fuel | white, yellow, orange, and whiter than kerosene | 1.7 |
| monopropellant | pale yellow | 0.14 |
| fuel heated by a reactor | a faint red | 0.3 |

## Starting and stopping

How hard an engine is running is taken from how much it burns of what it could, not from how hard it
pushes: a vacuum engine at sea level pushes little and burns as much as ever. A throttled engine's flame
is shorter and fainter. When an engine stops, the gas already out is gone in a quarter of a second, and
the flames the game had are given back a moment after.

## The other flames

While a flame is burning for an engine, everything on that engine's part that is drawn with one of
Waterfall's shaders is hidden, and so is every particle effect on it except smoke (smoke is what has
"smoke" in its name, or in the name of what it hangs from). They are hidden, not removed: switch the mod
off, or the setting "Hide the game's own flames", and they are back.

## What it costs

About what the flames it hides cost. One flight each way of a Kerbal X (a Mainsail and six Swivels) from
the pad, on an Apple M5 Pro at 1280 by 720 with Scatterer, EVE, TUFX, Waterfall and Natural Light
installed, the game drawing as fast as it could: 39 frames a second leaving the pad with the game's and
Waterfall's flames and 42 with these; 45 and 49 on the climb, seen from the chase camera; 50 and 49
standing on the pad before it was lit. At the end of the climb the seven flames were 3,400 bits of gas in
one patch of air. Not measured: the camera inside a flame, a ship of dozens of nozzles, a slower computer.

## What it does not do

* **Shock diamonds.** The first version drew them. Gas worked out in bits a third of a nozzle across does
  not form them, and none are drawn on top.
* Light on the ship and the ground. It gives off none: what lights the pad at lift-off in the pictures
  is the Natural Light mod's lamps at the engines, where that is installed, or nothing. (It does light the
  smoke of Rocket Smoke.)
* Smoke. That is Rocket Smoke's; without it the game's own smoke is left as it is.
* Windows. Volumetric Explosions draws its volumes with a shader made for the game's OpenGL (a Mac,
  Linux). Until there is one for Direct3D this mod does nothing on Windows but say so.
* More than four ships' flames at once, and no more than about 1,500 bits of gas a second from any one
  nozzle: a cluster of very many engines is drawn with fewer bits each as the scene fills up.
