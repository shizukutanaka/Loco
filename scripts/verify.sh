#!/usr/bin/env bash
#
# Everything this repository can verify without a network.
#
# One command, because the alternative is remembering five and running three.
# api.nuget.org is refused by proxy policy here, so there is no package restore
# and no `dotnet test` - but all 321 backend tests DO run, against the harness in
# scripts/offline-test-harness/, including the controller tests, which get a real
# API process on a loopback port. Between them these checks have caught a restore
# that failed before it reached a package, a test assembly that could not
# compile, 47,000 lines of unreachable code, a rename that missed a call site,
# and a condition node that never branched.
#
# Run: scripts/verify.sh
# Exit: 0 when everything passes, 1 on the first failure.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

step() { printf '\n\033[1m== %s\033[0m\n' "$1"; }

step "C# type-check (offline, no NuGet)"
scripts/typecheck-offline.sh

step "Structural checks"
python3 scripts/check-structure.py

step "Backend tests"
scripts/run-tests-offline.sh

EDITOR_DIR="src/Loco.VisualEditor"

if [[ ! -d "$EDITOR_DIR/node_modules" ]]; then
  echo
  echo "Skipping the editor: $EDITOR_DIR/node_modules is missing."
  echo "Run 'npm ci' in $EDITOR_DIR to include it."
  exit 0
fi

cd "$EDITOR_DIR"

step "Editor type-check"
npx tsc --noEmit

step "Editor tests"
npx vitest run

step "Editor build"
npm run build

step "Editor lint"
npm run lint

printf '\n\033[1mAll offline checks passed.\033[0m\n'
echo "Still unverified here: a real build against the real packages. The harness"
echo "hosts the API for the controller tests, but its JwtBearer plumbing is"
echo "hand-written and its Swashbuckle stubs are inert. docs/ci/ci.yml runs the"
echo "real suite."
