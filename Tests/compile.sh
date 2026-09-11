#!/usr/bin/env bash
# Compile the package the way Unity would, without opening Unity.
#
# Opening the editor needs an activated licence, which a CI box and a fresh
# clone do not have. The compiler and the assemblies do not: both ship inside
# the editor, so the package can be built and type-checked by pointing Roslyn at
# them. That covers everything except the parts only a device can answer — for
# those, see verify_jni.py, which checks the Java names this C# pronounces.
#
# Usage: Tests/compile.sh [/path/to/Unity/Hub/Editor/<version>]
set -euo pipefail

EDITOR="${1:-$(ls -d /Applications/Unity/Hub/Editor/*/ 2>/dev/null | sort -V | tail -1)}"
[ -n "$EDITOR" ] && [ -d "$EDITOR" ] || { echo "No Unity editor found. Pass one as \$1."; exit 1; }

U="$EDITOR/Unity.app/Contents"
M="$U/Managed/UnityEngine"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT

csc() { "$U/NetCoreRuntime/dotnet" "$U/DotNetSdkRoslyn/csc.dll" "$@"; }

# -warnaserror because a warning in an SDK becomes a warning in every publisher's
# console, and a publisher cannot tell ours from their own.
csc -target:library -nostdlib+ -langversion:9.0 -warnaserror+ \
    -out:"$OUT/Sanbuk.Runtime.dll" \
    -r:"$U/NetStandard/ref/2.1.0/netstandard.dll" \
    -r:"$M/UnityEngine.CoreModule.dll" \
    -r:"$M/UnityEngine.AndroidJNIModule.dll" \
    "$ROOT"/Runtime/*.cs "$ROOT"/Runtime/Internal/*.cs > /dev/null
echo "OK  Runtime"

UI="$(find "$EDITOR" -name UnityEngine.UI.dll | head -1)"

# The sample is compiled too: it is what a publisher copies, so it is part of
# the package's surface whether or not we treat it that way.
# -nowarn:0649 for [SerializeField] fields, which the inspector assigns and the
# compiler cannot see being assigned. Unity suppresses the same warning.
csc -target:library -nostdlib+ -langversion:9.0 -warnaserror+ -nowarn:0649 \
    -out:"$OUT/SanbukSample.dll" \
    -r:"$U/NetStandard/ref/2.1.0/netstandard.dll" \
    -r:"$M/UnityEngine.CoreModule.dll" \
    -r:"$M/UnityEngine.AndroidJNIModule.dll" \
    -r:"$M/UnityEngine.UnityWebRequestModule.dll" \
    -r:"$M/UnityEngine.UnityWebRequestTextureModule.dll" \
    -r:"$M/UnityEngine.IMGUIModule.dll" \
    -r:"$M/UnityEngine.TextRenderingModule.dll" \
    -r:"$UI" -r:"$OUT/Sanbuk.Runtime.dll" \
    "$ROOT/Samples~/BasicIntegration/SanbukDemo.cs" > /dev/null
echo "OK  Samples~/BasicIntegration"
