#!/bin/bash
# Godot 전환(ADR 0004): 옮긴 로직과 테스트를 **유니티에 들어 있는 Mono 런타임**으로 돌려 본다.
# .NET 8 과 유니티(Mono)는 실수 계산이 미세하게 다르다(Mono 는 float 연산을 배정밀도로 한다). 보류한 테스트가
# "유니티에서는 통과하던 것(런타임 차이)" 인지 "유니티에서도 실패하던 것" 인지 가르는 데 쓴다.
#   tools/godot_port/mono_check.sh <작업 폴더> [NUnit --where 식]
# godot/core, godot/tests 를 작업 폴더에 복사해 .NET Framework 4.8 용으로 빌드하고(보류 표시 [Ignore] 는 떼어 낸다)
# Unity 의 mono.exe 로 NUnitLite 를 돌린다. 저장소는 건드리지 않는다.
set -e
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
WORK="$1"; WHERE="$2"
MONO="${MONO:-/c/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Data/MonoBleedingEdge/bin/mono.exe}"
rm -rf "$WORK"; mkdir -p "$WORK/core" "$WORK/tests"
cp -r "$ROOT/godot/core/." "$WORK/core/"; rm -rf "$WORK/core/obj" "$WORK/core/bin" "$WORK/core/Samkuk.Core.csproj"
cp "$ROOT"/godot/tests/*.cs "$WORK/tests/"
cp "$ROOT/godot/project.godot" "$WORK/"; cp -r "$ROOT/godot/data" "$WORK/data"; cp -r "$ROOT/godot/sprites" "$WORK/sprites"
# net48 에 없는 API 두 가지를 같은 뜻의 코드로
sed -i 's/Array\.Fill(head, -1)/ArrayFillCompat.Fill(head, -1)/' "$WORK/core/Enemy/EnemyManager.cs"
printf 'public static class ArrayFillCompat { public static void Fill(int[] a, int v) { for (int i = 0; i < a.Length; i++) a[i] = v; } }
' > "$WORK/core/ArrayFillCompat.cs"
sed -i 's/System\.IO\.Path\.GetRelativePath(root, file)/file.Substring(root.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).Length + 1)/' "$WORK/core/Data/GameData.cs"
# 보류 표시를 떼어 전부 돌린다
sed -i '/^\s*\[Ignore("/d' "$WORK"/tests/*.cs
cat > "$WORK/core/Samkuk.Core.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <RootNamespace>Samkuk</RootNamespace>
    <LangVersion>12</LangVersion>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Samkuk.Tests" />
    <PackageReference Include="Google.Protobuf" Version="3.27.3" />
    <PackageReference Include="System.Text.Json" Version="8.0.5" />
    <PackageReference Include="Microsoft.Bcl.HashCode" Version="1.1.1" />
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
  </ItemGroup>
</Project>
EOF
cat > "$WORK/tests/Samkuk.Tests.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net48</TargetFramework>
    <RootNamespace>Samkuk.Tests</RootNamespace>
    <LangVersion>12</LangVersion>
    <Nullable>disable</Nullable>
    <GenerateProgramFile>false</GenerateProgramFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnitLite" Version="3.14.0" />
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
    <ProjectReference Include="../core/Samkuk.Core.csproj" />
  </ItemGroup>
</Project>
EOF
cat > "$WORK/tests/MonoMain.cs" <<'EOF'
// NUnitLite 로 이 어셈블리의 테스트를 돌린다 (mono.exe Samkuk.Tests.exe [--where ...])
public static class MonoMain
{
    public static int Main(string[] args) => new NUnitLite.AutoRun(typeof(MonoMain).Assembly).Execute(args);
}
EOF
cd "$WORK/tests"
dotnet build -nologo -v q 2>&1 | grep -E ' error ' | sed -E 's/ \[.*//' | sort -u | head -20
EXE="$WORK/tests/bin/Debug/net48/Samkuk.Tests.exe"
[ -f "$EXE" ] || { echo "빌드 실패"; exit 1; }
if [ -n "$WHERE" ]; then "$MONO" "$EXE" --noheader --noresult --where "$WHERE" 2>&1 | tail -60
else "$MONO" "$EXE" --noheader --noresult 2>&1 | tail -60; fi
