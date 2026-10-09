# How it works

## A box of gas for each nozzle

Each nozzle of each rocket engine gets a box that begins at the nozzle's rim and lies along the jet. The
box is drawn by one shader (`tools/shaderpack/flame.glsl`), and nothing in it is a picture: for each pixel
the line of sight is walked through the box in thirty steps from the near side to the far, and at each
step the gas there gives off light and hides a little of what is behind it. What the gas is like at a
place is worked out from where the place is. So the flame has depth, looks right from any side and from
inside, and stops at whatever the game has already drawn in front of it (it reads the game's own record of
how far off every pixel is).

A sooty flame (kerosene, a solid booster) hides what is behind it, so what is seen of it is its near
surface. A clean one (hydrogen) hides nothing and is seen right through.

The steps are spent only where the jet is. The box is square and the jet is not, so the shader first
works out where the line of sight is inside a cone fitted round the jet, and walks only that stretch.

## Where the nozzle is

The game says where an engine pushes from, which is not where its bell ends: it can be half a metre up
inside the bell or below it. So the first time an engine runs, its model is measured: of everything solid
in it near the jet's line, the part furthest along the line is the rim, and the widest thing there is the
rim's radius. (Shrouds that are cast off are left out; by the time an engine runs they are gone anyway.)
Where a model cannot be read, or gives something a nozzle could not be, the radius is worked out from the
engine's thrust instead, and the flame begins where the engine pushes from.

Every size in the flame is a multiple of that radius: an Ant's flame is a Mainsail's seen from further off.

## What the air does

The air's pressure at the craft is the one number that shapes the flame:

| | At the pad | 8 km up | 16 km | 26 km | Vacuum |
| --- | --- | --- | --- | --- | --- |
| Pressure (Kerbin) | 1 atmosphere | 0.26 | 0.055 | 0.008 | 0 |
| The jet's radius at its end, in nozzle radii | 2.2 | 3.6 | 5.7 | 8.7 | 13 |
| How it opens | nearly a straight cone | | | | a bell: fast at first |
| Its edge | torn, ending in tongues | | less | nearly smooth | smooth |
| Shock diamonds | strong, a little over a nozzle's width apart | half as strong, longer | all but gone | none | none |
| Its fainter colours | the fuel's own | | | paler | pale |

In thick air the jet goes on burning in the air it draws in, so it stays bright along its length and ends
in a ragged point where it burns out. With no air it only spreads and cools: most of its light is a streak
down its middle near the nozzle, inside a wide faint skirt.

The diamonds are where the air has squeezed the jet and it has sprung back: its edge necks in and out, and
where the shocks cross on the axis the gas is pressed hot. Each diamond begins sharply, fades as the gas
spreads again, and is dimmer than the one before.

## What moves

The gas in a real jet goes by far too fast to be seen as lumps. What a camera sees of it is streaks along
the jet, and the slow heave of the whole flame. So there are two kinds of noise in the shader: fast
streaks, which streak its heat and the outer part of its edge, and a slow heave, which tears its edge
further down and breaks its end into tongues. Both travel downstream, in lumps as large as the jet is wide
where they are.

## The ground

A ray is cast from the rim along the jet each frame, against the ground and the buildings on it (and the
sea, which has no surface in the game's physics: there it is where the jet's line comes down to sea
level). If it meets something within the flame's length, the jet is cut off there and what was left of
its length runs out along the ground on every side as a thin sheet: thinner and cooler the further it has
run, in fingers of uneven reach, and further on the side the jet was going where it came down aslant. The
sheet is walked a second time in sixteen finer steps, because a thin thing seen edge-on is missed by
coarse ones.

Only the ground, buildings and sea turn a flame. Other craft and the craft's own parts do not.

## What an engine burns

See [flames for other engines](Other-engines.md).

## Starting and stopping

How hard an engine is running is taken from how much it burns of what it could, not from how hard it
pushes: a vacuum engine at sea level pushes little and burns as much as ever. The flame follows that with
a short lag. For most of a second after an engine lights, its flame is wider, cooler and more torn, with
no diamonds: the fuel that got there first burning as it can.

## The other flames

While a flame is drawn for an engine, everything on that engine's part that is drawn with one of
Waterfall's shaders is hidden, and so is every particle effect on it except smoke (smoke is what has
"smoke" in its name, or in the name of what it hangs from). They are hidden, not removed: switch the mod
off, or the setting "Hide the game's own flames", and they are back.

## What it does not do

* Light. It gives off none: what lights the pad at lift-off in the pictures is the Natural Light mod's
  lamps at the engines, where that is installed, or nothing.
* Smoke. The game's smoke is left as it is.
* Windows. The shader is written in the form the game's OpenGL takes and packed by hand into a file the
  game can load (`tools/shaderpack/make_bundle.py`). Direct3D needs it compiled by the Unity editor, which
  has not been possible here; until it is, the mod does nothing on Windows but say so.
* Heat haze, flame turned by other craft, flame inside a flame trench.
