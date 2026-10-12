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
* cools: white-hot for more than half the flame's length, then yellow, and out through orange at its end;
* is turned where it strikes the ground or a building, and runs on along it.

About thirteen hundred of these are alive at once for each nozzle. They are put on a grid of cells, as a blast's
smoke and fire are, and the grid is drawn as one lit volume: for every pixel the line of sight is walked
through it and the light of the gas along it is added up. The flame's reach, its flicker, the way it is swept
back when a ship flies aslant and the way it lies on a launch pad are whatever the gas did. (What is laid
down beforehand: the pattern that streaks it and tears its edge, and a clear flame's shock diamonds. See below.)

This mod's own part in it is small. It says which engines are rocket engines, what each burns, where each
nozzle's mouth is and how wide, and how hard the engine is running; and it hides the flames the game had.

## How it is drawn: bright first, then fire

The first flames made this way (0.2.0) were thin: the gas glowed faintly, cooled to orange a quarter of the
way down, and was torn into wisps along its whole length. Set beside the game's own flame, a long white-hot
bar, it looked weak, and the game's was the one that looked like a rocket. Since 0.3.0 (with Volumetric
Explosions 0.6.0) a kerosene or solid-fuel flame is what a real one is to a camera, blinding:

* **A solid white-hot body.** Enough gas, close enough together, that the jet hides what is behind it and
  burns out the picture: several times as bright as white paper in the sun, with the glow round it that the
  game's own picture gives anything that bright. It leaves the nozzle hotter than white-hot and so stays white
  for more than half its length.
* **Brightness goes with heat, steeply.** The white-hot part is ten times as bright as the cooled orange end.
  (Nearly as bright at every heat, as at first, the orange end was cut off to a flat yellow by the picture.)
* **A steady outline.** The bits of gas leave the nozzle's mouth in turn from a list that fills it evenly, not
  by chance, and there are three times as many: the jet's edge is a straight line at the nozzle, not a row of
  bulges that changed every frame.
* **Fire where there would be fire.** A thin cooler sheath, yellow and orange, where the jet meets the air;
  the air tears its edge more and more the further down it, hardly at all at the nozzle; and at its far end
  the flame cools out through yellow into ragged orange tongues with gaps between them.
* **A pattern that flows.** Held in the nozzle's frame (the gas's own, going a hundred metres a second and
  more, is smeared to nothing by the game's smoothing between frames, the TAA of TUFX: 0.2.0 was first "kind
  of just like blurred color"). It is carried down the jet fast at the nozzle, where that smoothing draws it
  out into streaks, as gas going that fast looks, and slower and less drawn out the further down, until at
  the flame's end it is the slow lumps of a fire; and it opens out with the jet.
* **Drawn whole.** A ship's flames are drawn at the screen's own size (the smoke round about at half size).

## Each fuel its own

| Fuel | At the ground |
| --- | --- |
| kerosene | white-hot, yellow, then orange tongues: as above |
| solid fuel | the same, thicker and brighter still |
| hydrogen | a clear pale veil one sees through, with four or five shock diamonds standing in it: a bright disc where each shock stands, narrowing to a point behind it. In the jet of every nozzle, up to eight |
| methane | bright, half clear, blue-white to violet, with diamonds |
| the fuels that keep | a bright clean flame, white to pale pink, thinning away |
| monopropellant, a reactor's | faint and clear |

A hydrogen flame is not made bright: it has next to no light of its own, and a thick white one looked like a
kerosene flame painted white. Its diamonds are what is seen of it.

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
| How much wider the jet gets for each metre it goes | 0.035 m | 0.08 | 0.14 | 0.23 | it fans out from the lip, 27 degrees each way |
| How far it reaches, against its length at the pad | all of it | 0.93 | 0.82 | 0.67 | a few nozzles' widths |
| Its edge | torn | | less | nearly smooth | smooth |
| What is seen | a white-hot body, orange at its end | | | a wide clear flame, white to yellow | a bright lobe at the nozzle |

In thick air the jet is held to a narrow column, the air tears its edge, and it cools through orange before
it is spent. As the air thins there is less to hold it in and less to tear it: it opens out, its soot and its
sharp edge go, and what is seen is the light of the hot gas alone. With none, the gas fans out from the
nozzle's lip and cools as it spreads, and all that shows is a bright clear lobe at the nozzle, most of it
along the jet's line, gone within a few nozzles' widths: whatever burns, though each in its own colour.
(How fast the gas cools is reckoned by the time gone, not by the frame: reckoned by the frame, as at first,
a vacuum plume was twice as long at thirty frames a second as at sixty.)

## The ground

Gas that would go into the ground is turned along it instead, the way it was going if it was going any
way, and otherwise outward on every side: the flame of a rocket standing on its pad runs out over the pad
as a sheet of fire, and the smoke of [Rocket Smoke](https://github.com/IshiakiZ/ksp-rocket-smoke), where
that is installed, rolls up from it. The same is done for buildings and for other craft.

## What an engine burns

See [flames for other engines](Other-engines.md). What is burning sets the flame's colours, how bright it
is, and a little of its length and width:

| Fuel | Colours | How thick its gas is, against kerosene's |
| --- | --- | --- |
| kerosene | white, yellow, orange | 1 |
| hydrogen | white to pale blue | 0.08 |
| methane | white to blue and violet | 0.25 |
| the fuels that keep | white to pale pink | 0.3 |
| solid fuel | white, yellow, orange, and whiter than kerosene | 1.5 |
| monopropellant | pale yellow | 0.12 |
| fuel heated by a reactor | a faint red | 0.15 |

(These are Volumetric Explosions' to know since its 0.6.0: this mod says only which fuel, and how hard the
engine is running.)

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

Less than the flames it hides, where it was measured: a Kerbal X (a Mainsail and six Swivels) from the pad,
with Rocket Smoke's smoke in both, on an Apple M5 Pro at 1280 by 720 with Scatterer, EVE, TUFX, Waterfall,
Natural Light and Natural Weather installed, the game drawing as fast as it could, two flights each way
(0.3.0, 2026-10-10):

| | Standing on the pad | Its first 4 s | 4 to 8 s | 8 to 12 s | 12 to 16 s |
| --- | --- | --- | --- | --- | --- |
| The game's and Waterfall's flames | 53 frames a second | 35, 38 | 40 | 40 | 42 |
| These | 53 | 41, 40 | 44 | 44 | 46 |

The seven flames were about 6,000 bits of gas in one patch of air, three times what they were in 0.2.0. Not
measured: the camera inside a flame, a ship of dozens of nozzles, a slower computer.

## What it does not do

* Shock diamonds that come of the gas itself: gas worked out in bits a third of a nozzle across does not
  form them. The ones in a hydrogen or methane flame are drawn where they would stand, in up to eight of a
  ship's jets; a kerosene or solid-fuel flame is too bright for any to show, as a real one's is to a camera.
* Light on the ship and the ground. It gives off none: what lights the pad at lift-off in the pictures
  is the Natural Light mod's lamps at the engines, where that is installed, or nothing. (It does light the
  smoke of Rocket Smoke.)
* Smoke. That is Rocket Smoke's; without it the game's own smoke is left as it is.
* Anything where Volumetric Explosions cannot draw a volume: there it says so and leaves the game's flames.
  (On Windows that mod's shaders are built for Direct3D, and the flames were seen there on 2026-10-10, by day
  and by night, liquid engines and solid boosters: as they were in 0.2.0. The bright flames of 0.3.0 were seen
  there on 2026-10-11, by day, with Volumetric Explosions 0.6.3's shaders built for Direct3D again that day:
  Kerbal X and Kerbal 1-5 from the pad, as on OpenGL on the same computer. Not at night, and no shock diamonds
  made out from 75 m.)
* More than four ships' flames at once, and no more than about 5,000 bits of gas a second from any one
  nozzle (some 1,300 alive at a time): a cluster of very many engines is drawn with fewer bits each as the
  scene fills up.
