#!/usr/bin/env python3
"""
Structural checks that need no network, no NuGet restore and no build.

Each of the three checks here was written because it caught a real defect that
had been sitting in the repository unnoticed - and each of those defects was
invisible to the compiler, to the tests, and to review:

  packages  Two projects carried an inline Version= on a PackageReference,
            which is NU1008 under central package management. A repo-wide
            `dotnet restore` failed before it reached a single package - a
            build break that had nothing to do with network access, and that
            no amount of fixing the network would have revealed.

  tests     Two test files named classes that have never existed in this
            repository. A test project is all-or-nothing, so those two files
            took the whole assembly down and every other test in it: the
            secrets, connection-store and mapper tests could not have run even
            with a working network. Every "the first CI run executes these"
            note was resting on an assembly that does not build.

  reachable 44,000 lines across 36 namespaces - Billing, OCR, MachineLearning,
            DisasterRecovery, Governance - had no path to them from anything a
            user can do. Dead code does not announce itself; it accumulates
            quietly and makes every subsequent search noisier.

  sdks      Both client SDKs polled /api/v1/workflows/{id}/executions/{id},
            a route that does not exist - executions are addressed globally.
            Nothing connects an SDK to the controllers, so the two drift in
            silence and the only symptom is a 404 in someone else's program.

Run: python3 scripts/check-structure.py
Exit: 0 when every check passes, 1 otherwise.
"""

import os
import re
import sys
import collections

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SKIP_DIRS = ("node_modules", "/bin", "/obj", "/.git", "/dist")

DECL = re.compile(r"\b(?:class|interface|record|struct|enum)\s+([A-Z][A-Za-z0-9_]*)")
IDENT = re.compile(r"\b([A-Z][A-Za-z0-9_]{2,})\b")
USING_LOCO = re.compile(r"^\s*using\s+(Loco\.[A-Za-z0-9_.]+)\s*;", re.M)
NAMESPACE = re.compile(r"^\s*namespace\s+([A-Za-z0-9_.]+)", re.M)
# A connector is an entry point: ConnectorStartupService finds it by reflection.
CONNECTOR = re.compile(r":\s*(?:ConnectorBase|IConnector)\b")

PKG_REF = re.compile(r'PackageReference\s+Include="([^"]+)"([^>]*)')
PKG_VER = re.compile(r'PackageVersion\s+Include="([^"]+)"')
INLINE_VERSION = re.compile(r'Version\s*=')


def read_sources():
    """Every .cs file in the repo, by path, skipping build output."""
    sources = {}
    for root, dirs, names in os.walk(REPO):
        if any(skip in root.replace(os.sep, "/") for skip in SKIP_DIRS):
            continue
        for name in names:
            if name.endswith(".cs"):
                path = os.path.join(root, name)
                rel = os.path.relpath(path, REPO).replace(os.sep, "/")
                sources[rel] = open(path, encoding="utf-8", errors="ignore").read()
    return sources


def read_projects():
    projects = {}
    for root, dirs, names in os.walk(REPO):
        if any(skip in root.replace(os.sep, "/") for skip in SKIP_DIRS):
            continue
        for name in names:
            if name.endswith(".csproj"):
                path = os.path.join(root, name)
                rel = os.path.relpath(path, REPO).replace(os.sep, "/")
                projects[rel] = open(path, encoding="utf-8", errors="ignore").read()
    return projects


def check_packages():
    """
    Central package management has to be consistent or restore never starts.

    Three ways it goes wrong, all of which produce a failure long before any
    code is compiled: a version declared inline (NU1008), a reference with no
    version anywhere (NU1010), and a version declared for a package nothing
    references - which is not inert, because it still pins the package
    transitively while reading as a dependency the product has.
    """
    props_path = os.path.join(REPO, "Directory.Packages.props")
    if not os.path.exists(props_path):
        return []

    props = open(props_path, encoding="utf-8").read()
    declared = set(PKG_VER.findall(props))

    referenced = set()
    problems = []

    for path, text in read_projects().items():
        for name, attrs in PKG_REF.findall(text):
            referenced.add(name)
            if INLINE_VERSION.search(attrs):
                problems.append(
                    f"NU1008: {path} pins {name} inline; "
                    f"move the version to Directory.Packages.props"
                )

    for name in sorted(referenced - declared):
        problems.append(f"NU1010: {name} is referenced but has no PackageVersion")

    for name in sorted(declared - referenced):
        problems.append(f"orphan: {name} has a PackageVersion but no project references it")

    return problems


def check_test_references(sources):
    """
    Every Loco namespace and type a test names must exist in src.

    The compiler would catch this - but only if the test assembly can be
    built, and building it needs the very packages that cannot be restored
    here. So the check that matters most is the one the compiler cannot run.
    """
    src = {p: s for p, s in sources.items() if p.startswith("src/")}
    tests = {p: s for p, s in sources.items() if p.startswith("tests/")}

    namespaces = set()
    for text in src.values():
        namespaces.update(NAMESPACE.findall(text))

    declared = set()
    for text in src.values():
        declared.update(DECL.findall(text))

    problems = []

    for path, text in sorted(tests.items()):
        for ns in sorted(set(USING_LOCO.findall(text))):
            if ns not in namespaces:
                problems.append(f"{path}: imports {ns}, which no file in src declares")

        # Types the test constructs. `new Foo(` is the strongest signal that a
        # test depends on a concrete type, and it is what both broken files
        # did with classes that were never written.
        local = set(DECL.findall(text))
        for typename in sorted(set(re.findall(r"\bnew\s+([A-Z][A-Za-z0-9_]{2,})\s*[(<{]", text))):
            if typename in declared or typename in local:
                continue
            if typename in BCL:
                continue
            problems.append(f"{path}: constructs {typename}, which no file in src declares")

    return problems


# Types from the base class library and the test packages. Anything here is
# expected not to live in src; everything else that a test constructs should.
BCL = {
    "Action", "Array", "AuthenticationHeaderValue", "AutoResetEvent", "Barrier",
    "Boolean", "CancellationTokenSource", "ClaimsIdentity", "ClaimsPrincipal",
    "Comparer", "ConcurrentBag", "ConcurrentDictionary", "ConcurrentQueue",
    "CountdownEvent", "DateTime", "DateTimeOffset", "Decimal", "DefaultHttpContext",
    "Dictionary", "DirectoryInfo", "Double", "EqualityComparer", "Exception",
    "FileInfo", "FileStream", "Func", "Guid", "HashSet", "HttpClient",
    "HttpRequestMessage", "HttpResponseMessage", "InvalidOperationException",
    "ArgumentException", "ArgumentNullException", "ArgumentOutOfRangeException",
    "NotSupportedException", "NotImplementedException", "OperationCanceledException",
    "TaskCanceledException", "TimeoutException", "IOException", "FormatException",
    "Int32", "Int64", "JsonSerializerOptions", "KeyValuePair", "Lazy", "List",
    "ManualResetEventSlim", "MemoryStream", "Mock", "Monitor", "Mutex",
    "Nullable", "Object", "Predicate", "Queue", "Random", "ReaderWriterLockSlim",
    "Regex", "SemaphoreSlim", "ServiceCollection", "SpinLock", "Stack",
    "Stopwatch", "StreamReader", "StreamWriter", "String", "StringBuilder",
    "StringContent", "Task", "Thread", "TimeSpan", "Tuple", "Uri", "Version",
    "WebApplicationFactory", "System",
}


def check_reachable(sources):
    """
    Every file in Loco.Core must be reachable from something a user can do.

    Reachability starts at the real entry points - the API's controllers and
    hosted services, the CLI's commands, the tests - plus every IConnector,
    because ConnectorStartupService discovers those by reflection and no
    static analysis would find them otherwise. Seeding the connectors is not
    optional: without them all 28 look unreachable, which would make this
    check demand the deletion of the entire product.

    This over-approximates - it follows every capitalized identifier, so it
    reports reachable more readily than unreachable. That is the safe
    direction: what it does flag is very likely genuinely dead.
    """
    owner = collections.defaultdict(set)
    for path, text in sources.items():
        for typename in DECL.findall(text):
            owner[typename].add(path)

    seeds = [p for p in sources if not p.startswith("src/Loco.Core/")]
    seeds += [p for p, s in sources.items() if CONNECTOR.search(s)]

    reached = set(seeds)
    queue = list(seeds)
    while queue:
        path = queue.pop()
        for typename in set(IDENT.findall(sources[path])):
            for declaring in owner.get(typename, ()):
                if declaring not in reached:
                    reached.add(declaring)
                    queue.append(declaring)

    problems = []
    for path in sorted(p for p in sources if p.startswith("src/Loco.Core/")):
        if path not in reached:
            lines = sources[path].count("\n")
            problems.append(f"{path}: {lines} lines, reachable from no entry point")

    return problems


ROUTE_ATTR = re.compile(r'\[Route\("([^"]+)"\)\]')
HTTP_ATTR = re.compile(r'\[Http(?:Get|Post|Put|Delete|Patch)(?:\("([^"]*)"\))?\]')
CONTROLLER_NAME = re.compile(r"class\s+([A-Za-z0-9_]+)Controller\b")
# Any /api/... path literal in an SDK, however it is quoted or interpolated.
SDK_PATH = re.compile(r"""["'`](/api/[^"'`\s]*)["'`]""")


def normalize_route(path):
    """Reduce a route to its shape: /api/v1/workflows/{}/execute."""
    path = re.sub(r"\{[^}]*\}", "{}", path)          # {id}, {workflowId}
    path = re.sub(r"\$\{[^}]*\}", "{}", path)        # ${workflowId}
    path = path.split("?")[0].rstrip("/")
    return path


def check_sdks(sources):
    """
    Every /api path an SDK calls must be a route the API exposes.

    Nothing links the two: the SDKs are hand-written against a remembered
    spec, so a controller can be renamed or a route re-nested and the SDKs
    keep compiling, keep passing their own type-check, and keep 404ing in
    somebody else's program. That is how both of them ended up polling
    /api/v1/workflows/{id}/executions/{id}, which has never existed.
    """
    routes = set()

    for path, text in sources.items():
        if "/Controllers/" not in path:
            continue

        route_attr = ROUTE_ATTR.search(text)
        name_match = CONTROLLER_NAME.search(text)
        if not route_attr or not name_match:
            continue

        base = route_attr.group(1).replace("[controller]", name_match.group(1).lower())

        for suffix in HTTP_ATTR.findall(text):
            full = f"/{base}/{suffix}" if suffix else f"/{base}"
            routes.add(normalize_route(full))

    if not routes:
        return ["no controller routes found - the route parser needs updating"]

    problems = []

    # The SDKs and the documentation are checked together on purpose: the
    # nested executions route both clients used is exactly what README.md
    # documented, so a wrong example is where a wrong client comes from.
    targets = []
    for scope in ("sdks", "docs"):
        scope_dir = os.path.join(REPO, scope)
        if not os.path.isdir(scope_dir):
            continue
        for root, dirs, names in os.walk(scope_dir):
            if any(skip in root.replace(os.sep, "/") for skip in SKIP_DIRS):
                continue
            targets += [os.path.join(root, n) for n in names]

    readme = os.path.join(REPO, "README.md")
    if os.path.exists(readme):
        targets.append(readme)

    for path in sorted(targets):
        if not path.endswith((".py", ".ts", ".js", ".md")):
            continue
        rel = os.path.relpath(path, REPO).replace(os.sep, "/")
        text = open(path, encoding="utf-8", errors="ignore").read()

        for called in sorted(set(SDK_PATH.findall(text))):
            shape = normalize_route(called)
            if shape in routes:
                continue
            # A base URL like "/api/v1" is a prefix of real routes, not a call.
            if any(route.startswith(shape + "/") for route in routes):
                continue
            problems.append(f"{rel}: calls {called}, which the API does not route")

    return problems


CITATION = re.compile(r"\b((?:src|tests|benchmarks|tools|scripts|sdks)/[A-Za-z0-9_./-]+\.[A-Za-z]{1,6})\b")
# A citation is allowed to name a file that is gone when the same line says so.
CITATION_EXEMPT = re.compile(
    r"削除済み|削除|存在しない|since removed|no longer|does not exist|never existed"
    r"|has been deleted|removed|missing",
    re.I,
)


def check_docs(sources):
    """
    Every source file a document points at must exist.

    Documentation rots differently from code: nothing recompiles it, so a
    guide can describe a framework that was never written and read as
    authoritative forever. This found five such documents at once - a Serilog
    guide for a library this project has never used, an error-handling guide
    for classes that were never written, and three reports claiming completed
    work whose every cited file was absent, including one headed
    "Core Features Complete" that inventoried an AI framework that does not
    exist.

    A line that says outright the file is gone is not a rot - that is a
    document being accurate about history - so those are exempt.
    """
    problems = []

    for root, dirs, names in os.walk(REPO):
        if any(skip in root.replace(os.sep, "/") for skip in SKIP_DIRS):
            continue
        for name in sorted(names):
            if not name.endswith(".md"):
                continue

            path = os.path.join(root, name)
            rel = os.path.relpath(path, REPO).replace(os.sep, "/")
            seen = set()

            for line in open(path, encoding="utf-8", errors="ignore"):
                if CITATION_EXEMPT.search(line):
                    continue
                for cited in CITATION.findall(line):
                    if cited in seen:
                        continue
                    seen.add(cited)
                    if not os.path.exists(os.path.join(REPO, cited)):
                        problems.append(f"{rel}: cites {cited}, which does not exist")

    return problems


def main():
    sources = read_sources()

    checks = [
        ("packages", "central package management is consistent", check_packages()),
        ("tests", "every type a test names exists in src", check_test_references(sources)),
        ("reachable", "every Loco.Core file has a path from an entry point", check_reachable(sources)),
        ("sdks", "every API path an SDK calls is a route the API exposes", check_sdks(sources)),
        ("docs", "every source file a document cites exists", check_docs(sources)),
    ]

    failed = False
    for name, description, problems in checks:
        if problems:
            failed = True
            print(f"FAIL  {name}: {description}")
            for problem in problems:
                print(f"        {problem}")
        else:
            print(f"ok    {name}: {description}")

    if failed:
        print("\nEach of these was a real defect at some point in this repository's")
        print("history, and none of them was visible to the compiler or the tests.")
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
