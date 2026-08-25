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
# The controller tests need a live ASP.NET host, and they get one: this builds
# the API as a real executable and the harness's WebApplicationFactory launches
# it on a loopback port, so those tests talk to actual Kestrel over actual HTTP.
# The ASP.NET Core shared runtime is installed, and the real JWT libraries ship
# inside the SDK's own dotnet-user-jwts tool - so tokens are signed and
# validated by Microsoft's code, not by anything in this directory.
#
# WHAT IT DOES NOT DO
# -------------------
# It is not a replacement for `dotnet test`: the real xunit and FluentAssertions
# are cleverer than this harness, the JwtBearer *plumbing* (not the validation)
# is written here, and the Swashbuckle stubs are inert. Only a real build
# exercises the actual packages. docs/ci/ci.yml runs the real suite.
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

# Program.cs stays out of the test assembly: its top-level statements are an
# entry point, and csc refuses -main: in any compilation that has one. The empty
# partial Program it also declares - the type LocoApiFactory names as its type
# parameter - is supplied by the harness instead. That parameter is only a
# marker here; the API is launched by path, not by type.
find src/Loco.Core src/Loco.Api src/Loco.Cli -name '*.cs' \
  ! -path 'src/Loco.Api/Program.cs' > "$WORK/files.txt"
find tests -name '*.cs' >> "$WORK/files.txt"
find scripts/offline-test-harness -name '*.cs' >> "$WORK/files.txt"
echo "$WORK/GlobalUsings.cs" >> "$WORK/files.txt"

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
write_runtimeconfig() {
  cat > "$1" <<'JSON'
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
}

write_runtimeconfig "$WORK/tests.runtimeconfig.json"

# The API as a real executable, for the controller tests to talk to over HTTP.
# Same sources, same stubs; the only difference from tests.dll is that this one
# keeps Program.cs's own entry point and carries no test code.
mkdir -p "$WORK/api"
find src/Loco.Core src/Loco.Api -name '*.cs' > "$WORK/api-files.txt"
find scripts/offline-test-harness -name 'NuGetPackageStubs.cs' >> "$WORK/api-files.txt"

# The same implicit usings the SDK would inject, minus Xunit - the API host
# carries no test code and there is no Xunit namespace in its compilation.
grep -v 'global::Xunit' "$WORK/GlobalUsings.cs" > "$WORK/ApiGlobalUsings.cs"
echo "$WORK/ApiGlobalUsings.cs" >> "$WORK/api-files.txt"

echo "Compiling the API host ($(wc -l < "$WORK/api-files.txt") files)..."
dotnet "$CSC" -nologo -nostdlib -langversion:12 -nullable:enable \
  -t:exe -out:"$WORK/api/api.dll" \
  "${REFS[@]}" "@$WORK/api-files.txt" > "$WORK/api-build.txt" 2>&1

if grep -q 'error CS' "$WORK/api-build.txt"; then
  echo "API host compilation failed:"
  grep 'error CS' "$WORK/api-build.txt" | head -30
  exit 1
fi

write_runtimeconfig "$WORK/api/api.runtimeconfig.json"

# Microsoft.IdentityModel.* and System.IdentityModel.Tokens.Jwt are NOT in the
# shared framework - they were borrowed from the SDK's dotnet-user-jwts tool -
# so the runtime can only find them next to the assembly that needs them.
for ref in "${REFS[@]}"; do
  dll="${ref#-r:}"
  case "$(basename "$dll")" in
    Microsoft.IdentityModel.*.dll|System.IdentityModel.Tokens.Jwt.dll)
      cp -f "$dll" "$WORK/api/" ;;
  esac
done

echo "Running..."
echo
LOCO_TEST_API_DLL="$WORK/api/api.dll" dotnet "$WORK/tests.dll" "$@"
status=$?

echo
echo "This is not dotnet test. The real xunit and FluentAssertions are cleverer"
echo "than scripts/offline-test-harness/, the JwtBearer plumbing there is hand"
echo "-written (the validation itself is Microsoft's), and the Swashbuckle stubs"
echo "are inert - docs/ci/ci.yml runs the real suite against the real packages."

exit $status
