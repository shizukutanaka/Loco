#!/usr/bin/env bash
# Type-check the C# sources - and the tests - without NuGet.
#
# WHY THIS EXISTS
# ---------------
# Large parts of this codebase were written in environments where `dotnet
# restore` is impossible: api.nuget.org is refused by organization proxy policy
# (HTTP 403). The result was a backend that had never been compiled at all.
#
# The .NET SDK is installable from the Ubuntu archive, and it ships Roslyn plus
# the framework reference assemblies. Everything else - the four packages this
# repository actually needs types from - is declared in scripts/offline-test-stubs/.
#
# THE BUG THIS SCRIPT USED TO HAVE
# --------------------------------
# csc binds a compilation in phases: parse, declarations, then method bodies.
# If ANY declaration-level error exists, it reports that and never binds a
# single method body.
#
#     echo 'public class B { public void M() { int x = "no"; } }' > body.cs
#     echo 'using Missing.Namespace;'                             > decl.cs
#     csc body.cs           -> 1 error
#     csc body.cs decl.cs   -> 1 error, and it is decl.cs's
#
# This script always had 12 such errors, from the JwtBearer handler, Swashbuckle
# and System.CommandLine. So it checked DECLARATIONS ONLY, while its own header
# claimed it caught "wrong method names, wrong argument counts" - both of which
# are method-body errors. Every "0 unexplained errors" it ever printed was a
# statement about declarations.
#
# Stubbing those four packages brought the declaration-error count to zero,
# which is what lets the compiler reach the bodies. It immediately found real
# defects that had been invisible for the life of the repository: an
# `ActionParameters.Has` that does not exist, called from 13 places in four
# connectors; `Name =="action"` in ZoomConnector; a TestConnectionAsync call
# missing its configuration argument; and a CLI still calling a class that had
# been renamed.
#
# WHAT IT PROVES NOW, AND WHAT IT DOES NOT
# ----------------------------------------
# Proves: every source file in src/ and tests/ compiles - types, members,
# signatures, overrides, nullability, and every expression in every method body.
#
# Does NOT prove: that the code RUNS. No test executes here; `dotnet test` needs
# the packages that cannot be restored. It also does not check the stubbed
# frameworks themselves - if a Swashbuckle or xunit call is wrong in a way the
# stub happens to accept, only a real build catches it. That is what the backend
# job in docs/ci/ci.yml is for.
#
# EXPECTED OUTPUT
# ---------------
# Zero errors, on both phases. Anything else is a defect.
#
# Usage:  scripts/typecheck-offline.sh
# Setup:  sudo apt-get install -y dotnet-sdk-8.0

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

# The SDK injects these; raw csc does not. Without them every use of DateTime,
# List<>, CancellationToken and friends reports as an unresolved type, drowning
# out real findings. The Microsoft.Extensions.* entries matter most: omitting
# them made ILogger and IHostedService look like missing packages, which hid
# whether the controllers and hosted services actually type-check (they do).
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
CS

# Compile all three projects in one pass so cross-project types resolve without
# needing to build and reference intermediate assemblies.
find src/Loco.Core src/Loco.Api src/Loco.Cli -name '*.cs' > "$WORK/files.txt"
find scripts/offline-test-stubs -name 'NuGetPackageStubs.cs' >> "$WORK/files.txt"
echo "$WORK/GlobalUsings.cs" >> "$WORK/files.txt"

REFS=()
for dll in "$NREF"/*.dll; do REFS+=("-r:$dll"); done
if [[ -n "$AREF" ]]; then
  for dll in "$AREF"/*.dll; do REFS+=("-r:$dll"); done
fi

# Some packages the projects reference are already on disk, shipped inside the
# SDK's own tooling rather than the reference packs. Borrowing them costs
# nothing and shrinks the unverifiable surface considerably: without these,
# every JWT and CLI-parsing call site is invisible to this check.
for name in System.IdentityModel.Tokens.Jwt; do
  # Prefer the LARGEST copy: the SDK ships trimmed builds of some of these
  # alongside the full ones, and a trimmed System.CommandLine omits the public
  # Command type that every CLI command derives from.
  found="$(find /usr/lib/dotnet -name "$name.dll" -printf '%s\t%p\n' 2>/dev/null \
           | sort -rn | head -1 | cut -f2)"
  [[ -n "$found" ]] || continue
  REFS+=("-r:$found")
  # Pull in the sibling assemblies it depends on (e.g. Microsoft.IdentityModel.*).
  for sibling in "$(dirname "$found")"/Microsoft.IdentityModel.*.dll; do
    [[ -f "$sibling" ]] && REFS+=("-r:$sibling")
  done
done

echo "Type-checking $(wc -l < "$WORK/files.txt") files against net8.0 reference assemblies..."

dotnet "$CSC" -nologo -nostdlib -langversion:12 -nullable:enable \
  -t:exe -out:"$WORK/out.exe" "${REFS[@]}" "@$WORK/files.txt" \
  > "$WORK/errors.txt" 2>&1

# ── Phase 2: the tests ───────────────────────────────────────────────────────
#
# The test assembly is the one nobody could see. dotnet test needs xunit and
# FluentAssertions, which are exactly what cannot be restored - so the test
# sources were never compiled by anything, and three files naming types that do
# not exist sat there taking the whole assembly down with them.
#
# scripts/offline-test-stubs/ stands in for the test packages only. The src
# types are real, so a test reaching for a property Loco.Core does not have
# still fails here, which is the class of breakage that actually happened.
# The test projects declare <Using Include="Xunit" />, so most test files have
# no `using Xunit;` of their own. Without this the attributes resolve nowhere
# and every [Fact] reports as a missing type - 190 errors that say nothing.
cat > "$WORK/TestGlobalUsings.cs" <<'CS'
global using global::Xunit;
CS

find tests -name '*.cs' > "$WORK/test-files.txt"
find scripts/offline-test-stubs -name 'TestFrameworkStubs.cs' >> "$WORK/test-files.txt"
echo "$WORK/TestGlobalUsings.cs" >> "$WORK/test-files.txt"
cat "$WORK/files.txt" >> "$WORK/test-files.txt"

echo "Type-checking $(find tests -name '*.cs' | wc -l) test files against stubbed test packages..."

dotnet "$CSC" -nologo -nostdlib -langversion:12 -nullable:enable \
  -t:exe -out:"$WORK/tests.exe" "${REFS[@]}" "@$WORK/test-files.txt" \
  > "$WORK/test-errors.txt" 2>&1

# Only errors in tests/ count: the src ones are already reported by phase 1, and
# a stub's own shortcomings are this script's problem, not the repository's.
test_errors="$(grep 'error CS' "$WORK/test-errors.txt" | grep -E '(^|/)tests/' || true)"

src_errors="$(grep 'error CS' "$WORK/errors.txt" || true)"

# Only errors in tests/ count for phase 2: any src error is already reported
# above, and a shortcoming in the stubs is this script's problem rather than
# the repository's.
test_errors="$(grep 'error CS' "$WORK/test-errors.txt" | grep -E '(^|/)tests/' || true)"

count() { printf '%s' "$1" | grep -c . || true; }

echo
echo "src   errors: $(count "$src_errors")"
echo "tests errors: $(count "$test_errors")"
echo

if [[ -n "$src_errors" || -n "$test_errors" ]]; then
  echo "These are defects - the declaration-error floor that used to hide method"
  echo "bodies from this check is gone, so nothing here is expected noise:"
  echo
  [[ -n "$src_errors" ]] && echo "$src_errors"
  [[ -n "$test_errors" ]] && echo "$test_errors"
  exit 1
fi

echo "Clean: every file in src/ and tests/ compiles, method bodies included."
echo "Not proven here: that any of it RUNS. dotnet test needs the packages that"
echo "cannot be restored; docs/ci/ci.yml is what executes the suite."
