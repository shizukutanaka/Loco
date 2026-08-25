#!/usr/bin/env bash
# Run the backend tests without NuGet.
#
# WHY
# ---
# api.nuget.org is refused by organization proxy policy (HTTP 403) in the
# environment this repository is developed in, and there is no package cache on
# disk. So `dotnet test` cannot run, and 271 backend tests had never been
# executed even once - every commit touching them carried a note saying the
# first CI run would be their first.
#
# The requirement was never "restore NuGet" though. It was "run the tests and
# see whether the assertions hold". scripts/offline-test-harness/ is the smaller
# thing that does that: a working subset of xunit and FluentAssertions, plus a
# reflection runner. Those assertions really compare and really throw - an
# assertion library that quietly returns `this` would turn a green run into a
# lie, which is worse than not running at all.
#
# WHAT IT DOES NOT DO
# -------------------
# It cannot host an ASP.NET application. The four controller test classes that
# need WebApplicationFactory are excluded and named below, rather than being
# faked into passing. It is also not a replacement for `dotnet test`: the real
# xunit and FluentAssertions are cleverer than this harness, and only a real
# build exercises the actual packages. docs/ci/ci.yml runs the real suite.
#
# Usage:  scripts/run-tests-offline.sh [--verbose]
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

CSC="$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)"
NREF="$(ls -d /usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 2>/dev/null | head -1)"
AREF="$(ls -d /usr/lib/dotnet/packs/Microsoft.AspNetCore.App.Ref/*/ref/net8.0 2>/dev/null | head -1)"

if [[ -z "$CSC" || -z "$NREF" ]]; then
  echo "error: .NET SDK 8 not found. Install it with:" >&2
  echo "  sudo apt-get install -y dotnet-sdk-8.0" >&2
  exit 2
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Tests that need a live ASP.NET host. Excluded rather than faked; reported at
# the end so their absence is stated rather than implied.
SKIPPED=(
  "tests/Loco.Api.Tests/LocoApiFactory.cs"
  "tests/Loco.Api.Tests/AuthenticationControllerTests.cs"
  "tests/Loco.Api.Tests/ConnectionsControllerTests.cs"
  "tests/Loco.Api.Tests/ConnectorsControllerTests.cs"
  "tests/Loco.Api.Tests/WorkflowsControllerTests.cs"
)

cat > "$WORK/GlobalUsings.cs" <<'CS'
global using global::System;
global using global::System.Collections.Generic;
global using global::System.IO;
global using global::System.Linq;
global using global::System.Net.Http;
global using global::System.Threading;
global using global::System.Threading.Tasks;
global using global::System.Net.Http.Json;
global using global::Microsoft.AspNetCore.Builder;
global using global::Microsoft.AspNetCore.Http;
global using global::Microsoft.AspNetCore.Routing;
global using global::Microsoft.Extensions.Configuration;
global using global::Microsoft.Extensions.DependencyInjection;
global using global::Microsoft.Extensions.Hosting;
global using global::Microsoft.Extensions.Logging;
global using global::Xunit;
CS

# Loco.Api/Program.cs is top-level statements, which claims the entry point the
# runner needs. Nothing else references it: it declares only the empty partial
# Program that WebApplicationFactory binds to, and those tests are excluded too.
find src/Loco.Core src/Loco.Api src/Loco.Cli -name '*.cs' \
  ! -path 'src/Loco.Api/Program.cs' > "$WORK/files.txt"
find tests -name '*.cs' >> "$WORK/files.txt"
find scripts/offline-test-harness -name '*.cs' \
  ! -name 'WebApplicationFactory.cs' >> "$WORK/files.txt"
echo "$WORK/GlobalUsings.cs" >> "$WORK/files.txt"

for excluded in "${SKIPPED[@]}"; do
  grep -vxF "$excluded" "$WORK/files.txt" > "$WORK/files.tmp" && mv "$WORK/files.tmp" "$WORK/files.txt"
done

REFS=()
for dll in "$NREF"/*.dll; do REFS+=("-r:$dll"); done
[[ -n "$AREF" ]] && for dll in "$AREF"/*.dll; do REFS+=("-r:$dll"); done

for name in System.IdentityModel.Tokens.Jwt; do
  found="$(find /usr/lib/dotnet -name "$name.dll" -printf '%s\t%p\n' 2>/dev/null \
           | sort -rn | head -1 | cut -f2)"
  [[ -n "$found" ]] || continue
  REFS+=("-r:$found")
  for sibling in "$(dirname "$found")"/Microsoft.IdentityModel.*.dll; do
    [[ -f "$sibling" ]] && REFS+=("-r:$sibling")
  done
done

echo "Compiling $(wc -l < "$WORK/files.txt") files..."
dotnet "$CSC" -nologo -nostdlib -langversion:12 -nullable:enable \
  -t:exe -main:Loco.OfflineTestRunner.Runner -out:"$WORK/tests.dll" \
  "${REFS[@]}" "@$WORK/files.txt" > "$WORK/build.txt" 2>&1

if grep -q 'error CS' "$WORK/build.txt"; then
  echo "Compilation failed:"
  grep 'error CS' "$WORK/build.txt" | head -30
  exit 1
fi

# A framework-dependent app needs a runtimeconfig telling it which shared
# framework to load; csc emits the assembly but not that.
cat > "$WORK/tests.runtimeconfig.json" <<'JSON'
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "frameworks": [
      { "name": "Microsoft.NETCore.App", "version": "8.0.0" },
      { "name": "Microsoft.AspNetCore.App", "version": "8.0.0" }
    ]
  }
}
JSON

echo "Running..."
echo
dotnet "$WORK/tests.dll" "$@"
status=$?

echo
echo "Skipped (need a live ASP.NET host, which this harness cannot provide):"
for excluded in "${SKIPPED[@]}"; do
  [[ "$excluded" == *LocoApiFactory.cs ]] && continue
  echo "  $excluded"
done
echo
echo "This is not dotnet test. The real xunit and FluentAssertions are cleverer"
echo "than scripts/offline-test-harness/, and only a real build exercises the"
echo "actual packages - docs/ci/ci.yml runs the real suite."

exit $status
