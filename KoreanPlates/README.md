# KoreanPlates

Replaces the plates in My Summer Car with Korean-style (반사 방지식, matte white / black text) plates.
The player's Satsuma gets `360무2934`; other cars get a stable generated Korean number.

Install: build (or take `KoreanPlates.dll`) and put it in `Mods` (MSCLoader 1.x). Check the in-game console (F1) for
`KoreanPlates:` lines listing which plate objects were replaced.

Build: `MSCMANAGED=<game>/mysummercar_Data/Managed`, then `msbuild` / `mcs` (see csproj). `tools/make_atlas.py` regenerates `glyphs.png`.
