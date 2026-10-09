# Flames for other engines

## What an engine is drawn as burning

| Flame | Looks | Drawn for |
| --- | --- | --- |
| `kerosene` | yellow-white at the heart, orange and sooty outside; dark for a little way out of the nozzle | Reliant, Swivel, Mainsail, Twin-Boar, Mastodon, Kodiak, Cub |
| `hydrogen` | pale blue, nearly clear, its diamonds sharp | Vector, Mammoth, Rhino, Skipper, Skiff, Dart, the R.A.P.I.E.R. as a rocket |
| `storable` | thin and clean, blue-white to violet | Terrier, Poodle, Spark, Ant, Spider, Twitch, Thud, Wolfhound, Cheetah, Bobcat |
| `methane` | blue to violet, pink where hottest | engines that take `LqdMethane` |
| `solid` | brilliant, thick, wide and long, torn | every engine that burns `SolidFuel` |
| `mono` | all but without light | engines that burn `MonoPropellant` alone |
| `reactor` | a faint red | engines that take `LiquidFuel` alone (the Nerv), or liquid hydrogen without oxygen |

The game has one fuel for every liquid engine, so the first three rows are the mod's own choice, by the
real engine each of the game's was modelled on. A liquid engine it has no entry for is drawn as `storable`
if it is plainly made for a vacuum (its sea-level specific impulse under six tenths of its vacuum one),
and as `kerosene` otherwise.

Other mods' fuels are known by name: `LqdHydrogen` with an oxidizer is `hydrogen`, `LqdMethane` is
`methane`, `Kerosene` or `RP-1` is `kerosene`, `Aerozine50`, `MMH`, `UDMH`, `NTO` are `storable`.

An engine that takes anything else (air, xenon, a fuel it does not know) gets no flame from this mod and
keeps the one it had.

## Saying otherwise

Any config in `GameData` can name the flame for a part:

```
ENGINE_FLAMES
{
    ENGINE
    {
        part = liquidEngine2_v2     // the part's name, as its own config has it
        flame = hydrogen            // kerosene, hydrogen, methane, storable, solid, mono, reactor, or none
    }
}
```

`none` leaves that engine to the game (or to Waterfall). What a config says goes before everything in the
table above. The configs are read once, the first time a flight is entered.

Only which of the seven flames an engine gets can be set this way. Their colours and shapes are in the
mod's code (`src/EngineFlames/Flames.cs`, the `Kind`s).
