# Building

`./build.sh` builds the mod and installs it into the game; `./build.sh check` only compiles; `./build.sh dist`
builds into `GameData/` here without touching the game. It needs the .NET SDK, a copy of the game (`KSP_DIR=/path`
if it is not where Steam puts it), and `Keystone.dll` from [Keystone](https://github.com/IshiakiZ/ksp-keystone):
the copy installed in the game, a built copy of that repository beside this one, or `KEYSTONE=/path/to/Keystone.dll`.

The shader is `tools/shaderpack/flame.glsl`, packed into `src/EngineFlames/Shaders/flame.bundle` by
`python3 tools/shaderpack/make_bundle.py "<KSP folder>" src/EngineFlames/Shaders/flame.bundle flame` (it takes the
game's own shader bundle as its pattern). The bundle is kept in the repository, so this is needed only if the
shader is changed.

This repository is made from a workspace that holds several mods side by side; changes arrive here from there.
