#!/bin/zsh
# Compile-checks the game's C# with Unity's own Roslyn, WITHOUT opening Unity (no Library lock, ~10 s).
# Runtime scripts (Assembly-CSharp) then editor scripts (Assembly-CSharp-Editor), against the DLLs Unity last built.
# It cannot catch serialisation or asset problems — only whether the code compiles. Usage: ./tools/csc-check.sh
set -e
cd "$(dirname "$0")/.."
S=/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/Resources/Scripting
OUT=$(mktemp -d "${TMPDIR:-/tmp}/kartcup-csc.XXXXXX")   # per run, so parallel checks never share outputs

# Sources follow Unity's rules: everything under Assets/ outside an asmdef's folder; Editor folders go to the editor assembly.
ASMDEF_DIRS=("${(@f)$(find Assets -name '*.asmdef' -exec dirname {} \;)}")
sources() {  # $1 = editor|runtime
  find Assets -name '*.cs' | while read -r f; do
    skip=0; for d in $ASMDEF_DIRS; do [[ "$f" == "$d"/* ]] && skip=1; done; (( skip )) && continue
    if [[ "$f" == */Editor/* ]]; then [[ $1 == editor ]] && echo "\"$f\""; else [[ $1 == runtime ]] && echo "\"$f\""; fi
  done
}

build() {  # $1 = name, $2 = editor|runtime, $3.. = extra references
  local name=$1 kind=$2; shift 2
  local rsp="$OUT/$name.rsp"
  {
    echo "-nologo -target:library -out:\"$OUT/$name.dll\" -langversion:9.0 -nowarn:0169,0649,0414,0108,0618,CS1701,CS1702,USG0001 -noconfig -nostdlib+"
    sed -n 's|.*<DefineConstants>\(.*\)</DefineConstants>.*|\1|p' Assembly-CSharp.csproj | tr ';' '\n' | sed 's|^|-define:|'
    sed -n 's|.*<HintPath>\(.*\)</HintPath>.*|-reference:"\1"|p' Assembly-CSharp.csproj
    for dll in Library/ScriptAssemblies/*.dll; do
      [[ "$dll" == *Assembly-CSharp*.dll ]] && continue      # we are rebuilding these two
      [[ "$dll" == *.Tests.* ]] && continue
      echo "-reference:\"$dll\""
    done
    for r in "$@"; do echo "-reference:\"$r\""; done
    sources $kind
  } > "$rsp"
  "$S/NetCoreRuntime/dotnet" "$S/DotNetSdkRoslyn/csc.dll" @"$rsp" 2>&1 | grep -E "error|warning CS8|Build" | grep -v "warning" || true
  [[ -f "$OUT/$name.dll" && "$OUT/$name.dll" -nt "$rsp" ]] || { echo "COMPILE FAILED: $name"; return 1; }
  echo "OK: $name"
}

build Assembly-CSharp runtime
build Assembly-CSharp-Editor editor "$OUT/Assembly-CSharp.dll"
