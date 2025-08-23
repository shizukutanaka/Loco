param(
  [switch]$VerboseMode,
  [int]$PerCommandTimeoutSec = 90,
  [int]$IdleTimeoutSec = 30,
  [string[]]$Only = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ProgressPreference = 'SilentlyContinue'
${global:PSModuleAutoloadingPreference} = 'None'

# Provide a deterministic DOTNET_CLI_HOME for child processes to avoid first-run writes to user profile
$script:DotnetCliHome = Join-Path $env:TEMP 'dotnet-cli-home-loco'
try { New-Item -ItemType Directory -Force -Path $script:DotnetCliHome | Out-Null } catch {}

# Clamp timeout: minimum 1s and maximum 600s to avoid pathological values
if ($PerCommandTimeoutSec -lt 1) { $PerCommandTimeoutSec = 1 }
if ($PerCommandTimeoutSec -gt 600) { $PerCommandTimeoutSec = 600 }
if ($IdleTimeoutSec -lt 1) { $IdleTimeoutSec = 1 }
if ($IdleTimeoutSec -gt $PerCommandTimeoutSec) { $IdleTimeoutSec = $PerCommandTimeoutSec }

# Determine which cases to run and log when verbose
$allCases = @('r1','r2','r2b','r3','r3b','r4')
$selected = if ($Only -and $Only.Count -gt 0) { $Only | Where-Object { $_ -in $allCases } } else { $allCases }
if ($VerboseMode) {
  $list = [string]::Join(',', $selected)
  Write-Host "[INFO] Will run cases: $list (timeout=${PerCommandTimeoutSec}s)" -ForegroundColor DarkGray
  if ($Only -and $Only.Count -gt 0) {
    $unknown = $Only | Where-Object { $_ -notin $allCases }
    if ($unknown -and $unknown.Count -gt 0) {
      Write-Host ("[WARN] Unknown cases ignored: {0}" -f ([string]::Join(',', $unknown))) -ForegroundColor DarkYellow
    }
  }
}

function ShouldRun($name) {
  if (-not $Only -or $Only.Count -eq 0) { return $true }
  return $selected -contains $name
}

function Run($cmd, $envOverrides) {
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  # Support native invocation via hashtable: @{ FileName = 'exe'; Arguments = 'args' }
  if ($cmd -is [hashtable] -and $cmd.ContainsKey('FileName') -and $cmd.ContainsKey('Arguments')) {
    $psi.FileName  = [string]$cmd['FileName']
    $psi.Arguments = [string]$cmd['Arguments']
  } else {
    # Fallback: run through PowerShell (compat mode)
    $psCore = $null
    try { $psCore = (Get-Command pwsh -ErrorAction SilentlyContinue) } catch {}
    if ($psCore -and $psCore.Source) { $psi.FileName = $psCore.Source } else { $psi.FileName = 'powershell.exe' }
    $psi.Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command $cmd"
  }

  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError  = $true
  $psi.RedirectStandardInput  = $true
  # Ensure consistent UTF-8 output capture
  try { $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8 } catch {}
  try { $psi.StandardErrorEncoding  = [System.Text.Encoding]::UTF8 } catch {}
  $psi.UseShellExecute = $false
  $psi.CreateNoWindow = $true
  $psi.ErrorDialog = $false
  $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
  $psi.WorkingDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent

  if (-not $envOverrides) { $envOverrides = @{} }
  foreach ($k in $envOverrides.Keys) {
    $psi.Environment[$k] = [string]$envOverrides[$k]
  }

  # Apply safe default environment toggles to avoid interactive/telemetry/network on CI
  $defaults = @{
    'DOTNET_CLI_TELEMETRY_OPTOUT'    = '1'
    'DOTNET_NOLOGO'                  = '1'
    'DOTNET_SKIP_FIRST_TIME_EXPERIENCE' = '1'
    'POWERSHELL_TELEMETRY_OPTOUT'    = '1'
    'POWERSHELL_UPDATECHECK'         = 'Off'
    'GIT_TERMINAL_PROMPT'            = '0'
    'DOTNET_PRINT_TELEMETRY_MESSAGE' = 'false'
    'DOTNET_GENERATE_ASPNET_CERTIFICATE' = 'false'
    'DOTNET_MULTILEVEL_LOOKUP'       = '0'
    'MSBUILDDISABLENODEREUSE'        = '1'
    'NUGET_XMLDOC_MODE'              = 'skip'
    'DOTNET_SKIP_WORKLOAD_INSTALLATION' = 'true'
    'DOTNET_CLI_HOME'                = $script:DotnetCliHome
  }
  foreach ($dk in $defaults.Keys) {
    if (-not $psi.Environment.ContainsKey($dk)) {
      $psi.Environment[$dk] = $defaults[$dk]
      if ($VerboseMode) { Write-Host "[ENV-DEFAULT] $dk=$($defaults[$dk])" -ForegroundColor DarkGray }
    }
  }

  $p = New-Object System.Diagnostics.Process
  $p.StartInfo = $psi
  $p.EnableRaisingEvents = $true

  # StringBuilders to accumulate async output
  $outSb = New-Object System.Text.StringBuilder
  $errSb = New-Object System.Text.StringBuilder

  # Verbose diagnostics for environment and CWD
  if ($VerboseMode) {
    Write-Host "[CWD] $($psi.WorkingDirectory)" -ForegroundColor DarkGray
    if ($envOverrides -and $envOverrides.Keys.Count -gt 0) {
      foreach ($k in $envOverrides.Keys) {
        $v = [string]$envOverrides[$k]
        if ($v.Length -gt 200) { $v = $v.Substring(0,200) + '…' }
        Write-Host "[ENV] $k=$v" -ForegroundColor DarkGray
      }
    }
  }

  # Attach handlers before Start to avoid missing early output; also update last-activity timestamp
  $script:lastActivity = Get-Date
  $p.add_OutputDataReceived({ param($s,$e) if ($e.Data) { [void]$outSb.AppendLine($e.Data); $script:lastActivity = Get-Date } })
  $p.add_ErrorDataReceived({ param($s,$e) if ($e.Data) { [void]$errSb.AppendLine($e.Data); $script:lastActivity = Get-Date } })

  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  [void]$p.Start()
  try { $p.StandardInput.Close() } catch {}
  $p.BeginOutputReadLine()
  $p.BeginErrorReadLine()

  $display = ("{0} {1}" -f $p.StartInfo.FileName, $p.StartInfo.Arguments)
  if ($VerboseMode) { Write-Host "[RUN] pid=$($p.Id) $display" -ForegroundColor DarkCyan }

  # Wait with both total timeout and idle-output timeout to prevent indefinite hangs
  $deadline = (Get-Date).AddSeconds($PerCommandTimeoutSec)
  $timedOut = $false
  $timeoutReason = ''
  while (-not $p.HasExited) {
    $now = Get-Date
    if ($now -gt $deadline) { $timedOut = $true; $timeoutReason = 'TIMEOUT'; break }
    if ($IdleTimeoutSec -gt 0) {
      $last = $script:lastActivity
      if ($null -eq $last) { $last = $now }
      if ($now -gt $last.AddSeconds($IdleTimeoutSec)) { $timedOut = $true; $timeoutReason = 'IDLE TIMEOUT'; break }
    }
    try { $p.WaitForExit(500) } catch {}
  }
  if ($timedOut) {
    # Before killing, attempt to log immediate child processes for diagnostics (guarded by a short timeout)
    try {
      $job = Start-Job -ScriptBlock {
        param($ppid)
        $list = @()
        try {
          $cim = $null
          try { $cim = Get-CimInstance -ClassName Win32_Process -Filter ("ParentProcessId={0}" -f $ppid) -ErrorAction Stop } catch {
            try { $cim = Get-WmiObject Win32_Process -Filter ("ParentProcessId={0}" -f $ppid) -ErrorAction Stop } catch {}
          }
          if ($cim) {
            foreach ($c in $cim) {
              $cmd = ''
              try { $cmd = [string]$c.CommandLine } catch {}
              $list += [PSCustomObject]@{ ProcessId = $c.ProcessId; Name = $c.Name; CommandLine = $cmd }
            }
          }
        } catch {}
        return $list
      } -ArgumentList $p.Id -ErrorAction SilentlyContinue

      $childInfo = Receive-Job -Job $job -Wait -Timeout 2 -ErrorAction SilentlyContinue
      if ($childInfo) {
        foreach ($c in $childInfo) {
          $cmdl = [string]$c.CommandLine
          if ($cmdl.Length -gt 200) { $cmdl = $cmdl.Substring(0,200) + '…' }
          if ($VerboseMode) { Write-Host ("[CHILD] pid={0} name={1} cmd={2}" -f $c.ProcessId, $c.Name, $cmdl) -ForegroundColor DarkYellow }
        }
      }
      if ($job -and ($job.State -eq 'Running')) { try { Stop-Job -Job $job -Force -ErrorAction SilentlyContinue } catch {} }
      if ($job) { try { Remove-Job -Job $job -Force -ErrorAction SilentlyContinue } catch {} }
    } catch {}

    # Kill the entire process tree to avoid orphaned children (e.g., dotnet)
    $killed = $false
    try { $p.Kill($true); $killed = $true } catch { }
    if (-not $killed) { try { $p.Kill(); $killed = $true } catch { } }
    if (-not $killed) {
      # Last-resort: taskkill to ensure process tree termination
      try { Start-Process -FilePath 'taskkill' -ArgumentList "/PID $($p.Id) /T /F" -NoNewWindow -Wait } catch { }
    }
    $sw.Stop()
    $timeoutMsg = if ($timeoutReason -eq 'IDLE TIMEOUT') {
      "[IDLE TIMEOUT after ${IdleTimeoutSec}s idle] elapsed=${($sw.Elapsed.TotalSeconds.ToString('0.###'))}s $display"
    } else {
      "[TIMEOUT after ${PerCommandTimeoutSec}s] elapsed=${($sw.Elapsed.TotalSeconds.ToString('0.###'))}s $display"
    }
    if ($VerboseMode) { Write-Host $timeoutMsg -ForegroundColor Yellow }
    # Try to flush and cleanup after kill
    try { $p.WaitForExit(5000) } catch {}
    try { $p.CancelOutputRead() } catch {}
    try { $p.CancelErrorRead() } catch {}
    $toutOut = ("$timeoutMsg`n$($outSb.ToString())").Trim()
    $toutErr = $errSb.ToString().Trim()
    try { $p.Close() } catch {}
    try { $p.Dispose() } catch {}
    return @{ Code = -1; Out = $toutOut; Err = $toutErr }
  }
  # Ensure async readers flush after exit
  try { $p.WaitForExit() } catch {}
  try { $p.CancelOutputRead() } catch {}
  try { $p.CancelErrorRead() } catch {}
  $outText = $outSb.ToString().Trim()
  $errText = $errSb.ToString().Trim()
  $exitCode = $p.ExitCode
  try { $p.Close() } catch {}
  try { $p.Dispose() } catch {}
  $sw.Stop()
  if ($VerboseMode) { Write-Host "[DONE] exit=$exitCode elapsed=${($sw.Elapsed.TotalSeconds.ToString('0.###'))}s $display" -ForegroundColor DarkCyan }
  return @{ Code = $exitCode; Out = $outText; Err = $errText }
}

function Assert-Contains($text, $substr, $label) {
  if ($text -like "*${substr}*") {
    Write-Host "[PASS] $label" -ForegroundColor Green
  } else {
    Write-Host "[FAIL] $label" -ForegroundColor Red
    Write-Host " Output: $text"
    exit 1
  }
}

function Assert-Equal($a, $b, $label) {
  if ($a -eq $b) {
    Write-Host "[PASS] $label" -ForegroundColor Green
  } else {
    Write-Host "[FAIL] $label" -ForegroundColor Red
    Write-Host "  Left : $a"
    Write-Host "  Right: $b"
    exit 1
  }
}

function Assert-ExitZero($result, $label) {
  if ($result.Code -eq 0) {
    Write-Host "[PASS] $label" -ForegroundColor Green
  } else {
    Write-Host "[FAIL] $label (exit=$($result.Code))" -ForegroundColor Red
    if ($result.Out) { Write-Host "  Out: $($result.Out)" }
    if ($result.Err) { Write-Host "  Err: $($result.Err)" }
    exit 1
  }
}

function Assert-PathExists($path, $label) {
  if (Test-Path -LiteralPath $path) {
    Write-Host "[PASS] $label" -ForegroundColor Green
  } else {
    Write-Host "[FAIL] $label" -ForegroundColor Red
    Write-Host " Path not found: $path"
    exit 1
  }
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
$csproj   = Join-Path $repoRoot 'src\Loco.Cli\Loco.Cli.csproj'
if (-not (Test-Path $csproj)) { throw "Loco.Cli.csproj not found at $csproj" }

# Ensure dotnet CLI is available
$dotnetCmd = $null
try { $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue } catch {}
if (-not $dotnetCmd) {
  throw "dotnet CLI not found. Please install .NET SDK 8.0.x and ensure 'dotnet' is in PATH."
}

# Prefer executing the built DLL directly if present (fast, no build logic). Fallback to dotnet run.
$cliDll = Join-Path $repoRoot 'src\Loco.Cli\bin\Release\net8.0\Loco.Cli.dll'
if (Test-Path -LiteralPath $cliDll) {
  if ($VerboseMode) { Write-Host "[INFO] Using built CLI: $cliDll" -ForegroundColor DarkCyan }
  $cliFile = 'dotnet'
  $cliArgsBase = "`"$cliDll`" --"
} else {
  if ($VerboseMode) { Write-Host "[INFO] Built CLI not found, falling back to dotnet run" -ForegroundColor DarkCyan }
  # Use --no-build/--no-restore assuming prior build step
  $cliFile = 'dotnet'
  $cliArgsBase = "run --project `"$csproj`" -c Release --no-build --no-restore --"
}

# Prepare temp dirs
$defaultExpectedSource = 'source=default'
$envDir   = Join-Path $env:TEMP ('LocoPluginsEnv_' + [guid]::NewGuid())
$cliDir   = Join-Path $env:TEMP ('LocoPluginsCli_' + [guid]::NewGuid())
New-Item -ItemType Directory -Force -Path $envDir | Out-Null
New-Item -ItemType Directory -Force -Path $cliDir | Out-Null

# Fresh dirs to validate ensure-creation behavior (do not pre-create)
$envNewDir = Join-Path $env:TEMP ('LocoPluginsEnvEnsure_' + [guid]::NewGuid())
$cliNewDir = Join-Path $env:TEMP ('LocoPluginsCliEnsure_' + [guid]::NewGuid())

try {
  # 1) Default (no env, no explicit) - verbose vs non-verbose and path exists
  if (ShouldRun 'r1') {
    if ($VerboseMode) { Write-Host '[CASE] r1: default path checks' -ForegroundColor DarkYellow }
    $r1v  = Run @{ FileName = $cliFile; Arguments = "$cliArgsBase plugins-path -v" } @{}
    $r1nv = Run @{ FileName = $cliFile; Arguments = "$cliArgsBase plugins-path" } @{}
    if ($VerboseMode) { Write-Host "[DEBUG] r1v: $($r1v.Out)"; Write-Host "[DEBUG] r1nv: $($r1nv.Out)" }
    Assert-ExitZero $r1v 'plugins-path -v exit code'
    Assert-ExitZero $r1nv 'plugins-path exit code'
    Assert-Contains $r1v.Out $defaultExpectedSource 'plugins-path -v default source'
    $r1vPath = ($r1v.Out -replace ' \(source=.*\)$','')
    Assert-Equal $r1nv.Out $r1vPath 'plugins-path matches verbose path without source'
    Assert-PathExists $r1vPath 'default plugins directory exists after plugins-path'
  }

  # 2) Env override - verbose, and ensure creation when dir does not exist
  if (ShouldRun 'r2') {
    if ($VerboseMode) { Write-Host '[CASE] r2: env override checks' -ForegroundColor DarkYellow }
    $r2 = Run @{ FileName = $cliFile; Arguments = "$cliArgsBase plugins-path -v" } @{ 'LOCO_PLUGINS_PATH' = $envDir }
    if ($VerboseMode) { Write-Host "[DEBUG] r2: $($r2.Out)" }
    Assert-ExitZero $r2 'plugins-path -v env exit code'
    Assert-Contains $r2.Out "(source=env:LOCO_PLUGINS_PATH)" 'plugins-path -v env source'
    Assert-Contains $r2.Out $envDir 'plugins-path output is env dir'
  }

  # 2b) Env ensure: directory is created by command
  if (ShouldRun 'r2b') {
    if ($VerboseMode) { Write-Host '[CASE] r2b: env ensure directory creation' -ForegroundColor DarkYellow }
    $r2b = Run @{ FileName = $cliFile; Arguments = "$cliArgsBase plugins-path" } @{ 'LOCO_PLUGINS_PATH' = $envNewDir }
    if ($VerboseMode) { Write-Host "[DEBUG] r2b: $($r2b.Out)" }
    Assert-ExitZero $r2b 'plugins-path env ensure exit code'
    Assert-Equal $r2b.Out $envNewDir 'plugins-path prints env dir exactly (no -v)'
    Assert-PathExists $envNewDir 'plugins-path ensures env dir exists when missing'
  }

  # 3) Explicit path wins
  if (ShouldRun 'r3') {
    if ($VerboseMode) { Write-Host '[CASE] r3: explicit path wins' -ForegroundColor DarkYellow }
    $r3 = Run @{ FileName = $cliFile; Arguments = "$cliArgsBase plugins-path --plugins-path `"$cliDir`" -v" } @{ 'LOCO_PLUGINS_PATH' = $envDir }
    if ($VerboseMode) { Write-Host "[DEBUG] r3: $($r3.Out)" }
    Assert-ExitZero $r3 'plugins-path -v explicit exit code'
    Assert-Contains $r3.Out "(source=explicit)" 'plugins-path -v explicit source'
    Assert-Contains $r3.Out $cliDir 'plugins-path output is explicit dir'
  }

  # 3b) Explicit ensure: directory is created by command
  if (ShouldRun 'r3b') {
    if ($VerboseMode) { Write-Host '[CASE] r3b: explicit ensure directory creation' -ForegroundColor DarkYellow }
    $r3b = Run @{ FileName = $cliFile; Arguments = "$cliArgsBase plugins-path --plugins-path `"$cliNewDir`"" } @{ }
    if ($VerboseMode) { Write-Host "[DEBUG] r3b: $($r3b.Out)" }
    Assert-ExitZero $r3b 'plugins-path explicit ensure exit code'
    Assert-Equal $r3b.Out $cliNewDir 'plugins-path prints explicit dir exactly (no -v)'
    Assert-PathExists $cliNewDir 'plugins-path ensures explicit dir exists when missing'
  }

  # 4) test-plugin logs include source
  if (ShouldRun 'r4') {
    if ($VerboseMode) { Write-Host '[CASE] r4: test-plugin uses env dir' -ForegroundColor DarkYellow }
    $r4 = Run @{ FileName = $cliFile; Arguments = "$cliArgsBase test-plugin" } @{ 'LOCO_PLUGINS_PATH' = $envDir }
    if ($VerboseMode) { Write-Host "[DEBUG] r4: $($r4.Out)" }
    Assert-ExitZero $r4 'test-plugin exit code'
    Assert-Contains ($r4.Out + " `n " + $r4.Err) $envDir 'test-plugin uses env dir'
  }

  Write-Host "All plugin path verification checks passed." -ForegroundColor Green
}
finally {
  # Cleanup temp dirs (keep if verbose requested)
  if (-not $VerboseMode) {
    Remove-Item -Recurse -Force $envDir, $cliDir, $envNewDir, $cliNewDir -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $script:DotnetCliHome -ErrorAction SilentlyContinue
  }
}
