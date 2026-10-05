#!/bin/sh
# Build against the game's own Managed folder (Unity 5 / .NET 2.0 runtime): ./build.sh <game>/mysummercar_Data/Managed
M="$1"; [ -d "$M" ] || { echo "usage: $0 <Managed dir>"; exit 1; }
mcs -noconfig -nostdlib -target:library -out:KoreanPlates.dll -resource:glyphs.png,KoreanPlates.glyphs.png \
  -r:"$M/mscorlib.dll" -r:"$M/System.dll" -r:"$M/System.Core.dll" -r:"$M/MSCLoader.dll" \
  -r:"$M/UnityEngine.dll" -r:"$M/Assembly-CSharp.dll" -r:"$M/PlayMaker.dll" \
  KoreanPlates.cs Properties/AssemblyInfo.cs
