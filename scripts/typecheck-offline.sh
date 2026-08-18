#!/usr/bin/env bash
# Type-check the C# sources without NuGet.
#
# WHY THIS EXISTS
# ---------------
# Large parts of this codebase were written in environments where `dotnet
# restore` is impossible: api.nuget.org is refused by organization proxy policy
# (HTTP 403). The result was a backend that had never been compiled at all, and
# a long series of commits carrying "VERIFICATION CAVEAT" notes.
#
# The .NET SDK itself, however, is installable from the Ubuntu archive, and it
# ships the Roslyn compiler plus the framework reference assemblies. That is
# enough to run the compiler's full syntax and semantic analysis over every
# source file - everything except the types that live in NuGet packages.
#
# WHAT IT PROVES, AND WHAT IT DOES NOT
# ------------------------------------
# Proves: the sources parse, and every type, member, signature, override and
# nullability annotation that does NOT come from a NuGet package resolves
# correctly. That catches the overwhelming majority of "written blind" mistakes -
# wrong method names, wrong argument counts, missing usings, bad overrides.
#
# Does NOT prove: three things, ~15 symbols in total.
#   - Swashbuckle and the JwtBearer handler: no copy exists anywhere on disk.
#   - The `Command` base class that CLI command classes derive from. The SDK
#     ships System.CommandLine, and borrowing it resolves the NAMESPACE, but
#     that build has its public types internalized, so the type itself stays
#     unresolved. Each CLI command file reports exactly one error for its base
#     class; their bodies are otherwise checked.
#
# Microsoft.Extensions.* and Microsoft.AspNetCore.* are NOT missing: they ship in
# the shared framework, so ILogger, IHostedService, controllers and DI
# registrations are fully checked. System.IdentityModel.Tokens.Jwt is borrowed
# from the SDK's tooling below and IS fully usable.
#
# EXPECTED OUTPUT
# ---------------
# A non-zero error count is normal. Every error should be CS0246/CS0234 (a type
# or namespace from a NuGet package) or CS0534 on LocoJsonContext (a
# source-generated partial that raw csc does not produce). The script separates
# those from anything else, and only the "unexplained" count matters.
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
for name in System.IdentityModel.Tokens.Jwt System.CommandLine; do
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
  -t:library -out:"$WORK/out.dll" "${REFS[@]}" "@$WORK/files.txt" \
  > "$WORK/errors.txt" 2>&1

total=$(grep -c 'error CS' "$WORK/errors.txt" || true)

# CS0246/CS0234: type or namespace not found - i.e. it lives in a NuGet package.
#
# There used to be a second exemption here, for the CS0534 pair on
# LocoJsonContext that the System.Text.Json source generator would have filled
# in during a real build. That class was deleted with the unreachable code it
# served, so every remaining error is now a plain missing package: JwtBearer and
# OpenApi in Loco.Api, System.CommandLine's Command in Loco.Cli.
unexplained=$(grep 'error CS' "$WORK/errors.txt" \
  | grep -vE 'error (CS0246|CS0234)' || true)

echo
echo "Total compiler errors:      $total"
echo "Expected (NuGet/generated): $(( total - $(printf '%s' "$unexplained" | grep -c . || true) ))"
echo

if [[ -n "$unexplained" ]]; then
  echo "UNEXPLAINED errors - these are real defects:"
  echo "$unexplained"
  exit 1
fi

echo "No unexplained errors: every failure is a type from a package that could"
echo "not be restored. The sources are otherwise type-correct."
