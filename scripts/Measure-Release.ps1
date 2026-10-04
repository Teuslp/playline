[CmdletBinding()]
param(
    [int]$Runs = 3
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Runs -lt 1) {
    throw 'Runs deve ser maior que zero.'
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = Join-Path $repoRoot 'artifacts\publish\win-x64\Playline.exe'
$diagnosticsProject = Join-Path $repoRoot 'tools\Playline.Diagnostics\Playline.Diagnostics.csproj'
$dotnetCandidates = @(
    (Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'dotnet\dotnet.exe'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1

if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    throw "Publicação não encontrada: $executable"
}

if (-not $dotnetCandidates) {
    throw 'O .NET SDK não foi encontrado.'
}

$dotnet = $dotnetCandidates
$temporaryBase = [System.IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
$metricsRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $temporaryBase ('Playline-UIMetrics-' + [Guid]::NewGuid().ToString('N'))))

if (-not $metricsRoot.StartsWith($temporaryBase, [StringComparison]::OrdinalIgnoreCase) -or
    (Split-Path $metricsRoot -Leaf) -notlike 'Playline-UIMetrics-*') {
    throw "Diretório temporário inseguro: $metricsRoot"
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PlaylineProcessIo
{
    [StructLayout(LayoutKind.Sequential)]
    public struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetProcessIoCounters(IntPtr process, out IoCounters counters);
}
'@

function Start-MeasuredPlayline {
    param([Parameter(Mandatory)] [string]$DataRoot)

    $previousDataRoot = $env:PLAYLINE_DATA_DIRECTORY
    try {
        $env:PLAYLINE_DATA_DIRECTORY = $DataRoot
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $process = Start-Process -FilePath $executable -PassThru
    }
    finally {
        $env:PLAYLINE_DATA_DIRECTORY = $previousDataRoot
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 40
        $process.Refresh()
    } until ($process.HasExited -or $process.MainWindowHandle -ne 0 -or [DateTime]::UtcNow -ge $deadline)
    $stopwatch.Stop()

    if ($process.HasExited -or $process.MainWindowHandle -eq 0) {
        throw "A inicialização falhou para $DataRoot"
    }

    return [pscustomobject]@{
        Process = $process
        StartupMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
    }
}

function Stop-Playline {
    param([Parameter(Mandatory)] [System.Diagnostics.Process]$Process)

    if (-not $Process.CloseMainWindow() -or -not $Process.WaitForExit(7000)) {
        if (-not $Process.HasExited) {
            $Process.Kill()
            $Process.WaitForExit()
        }

        throw 'O Playline não encerrou normalmente durante a medição.'
    }
}

New-Item -ItemType Directory -Path $metricsRoot | Out-Null
$rows = @()
$activeProcess = $null

try {
    & $dotnet build $diagnosticsProject -c Release --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar a ferramenta de diagnóstico.' }

    foreach ($count in @(0, 1, 10, 50, 100, 250)) {
        $dataRoot = Join-Path $metricsRoot $count
        & $dotnet run --no-build --project $diagnosticsProject -c Release -- "--prepare-root=$dataRoot" "--games=$count" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Falha ao preparar $count jogos." }

        $startupSamples = @()
        $workingSetSamples = @()
        $privateSamples = @()
        $threadSamples = @()
        $renderedGameButtons = -1

        for ($run = 1; $run -le $Runs; $run++) {
            $started = Start-MeasuredPlayline -DataRoot $dataRoot
            $activeProcess = $started.Process
            $startupSamples += $started.StartupMilliseconds

            Start-Sleep -Seconds 3
            $activeProcess.Refresh()
            $workingSetSamples += $activeProcess.WorkingSet64 / 1MB
            $privateSamples += $activeProcess.PrivateMemorySize64 / 1MB
            $threadSamples += $activeProcess.Threads.Count

            if ($run -eq $Runs) {
                $window = [Windows.Automation.AutomationElement]::FromHandle($activeProcess.MainWindowHandle)
                $elements = $window.FindAll(
                    [Windows.Automation.TreeScope]::Descendants,
                    [Windows.Automation.Condition]::TrueCondition)
                $renderedGameButtons = @(
                    for ($index = 0; $index -lt $elements.Count; $index++) {
                        if ($elements.Item($index).Current.AutomationId -like 'Game-*') { $elements.Item($index) }
                    }
                ).Count
            }

            Stop-Playline -Process $activeProcess
            $activeProcess = $null
        }

        $rows += [pscustomobject]@{
            Games = $count
            StartupMeanMilliseconds = [Math]::Round(($startupSamples | Measure-Object -Average).Average, 1)
            StartupMinimumMilliseconds = [Math]::Round(($startupSamples | Measure-Object -Minimum).Minimum, 1)
            WorkingSetMeanMB = [Math]::Round(($workingSetSamples | Measure-Object -Average).Average, 1)
            PrivateMeanMB = [Math]::Round(($privateSamples | Measure-Object -Average).Average, 1)
            ThreadsMean = [Math]::Round(($threadSamples | Measure-Object -Average).Average, 1)
            RenderedGameButtons = $renderedGameButtons
        }
    }

    $idleRoot = Join-Path $metricsRoot 'idle'
    & $dotnet run --no-build --project $diagnosticsProject -c Release -- "--prepare-root=$idleRoot" '--games=10' | Out-Null
    $idleStarted = Start-MeasuredPlayline -DataRoot $idleRoot
    $activeProcess = $idleStarted.Process
    Start-Sleep -Seconds 20
    $activeProcess.Refresh()
    $cpuStart = $activeProcess.TotalProcessorTime
    [PlaylineProcessIo+IoCounters]$ioStart = New-Object 'PlaylineProcessIo+IoCounters'
    [void][PlaylineProcessIo]::GetProcessIoCounters($activeProcess.Handle, [ref]$ioStart)

    $idleStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Seconds 10
    $idleStopwatch.Stop()
    $activeProcess.Refresh()
    $cpuEnd = $activeProcess.TotalProcessorTime
    [PlaylineProcessIo+IoCounters]$ioEnd = New-Object 'PlaylineProcessIo+IoCounters'
    [void][PlaylineProcessIo]::GetProcessIoCounters($activeProcess.Handle, [ref]$ioEnd)

    $idleCpuMilliseconds = ($cpuEnd - $cpuStart).TotalMilliseconds
    $idle = [pscustomobject]@{
        SettleSeconds = 20
        SampleSeconds = [Math]::Round($idleStopwatch.Elapsed.TotalSeconds, 1)
        CpuMilliseconds = [Math]::Round($idleCpuMilliseconds, 2)
        NormalizedCpuPercent = [Math]::Round(
            ($idleCpuMilliseconds / ($idleStopwatch.Elapsed.TotalMilliseconds * [Environment]::ProcessorCount)) * 100,
            4)
        WriteBytes = $ioEnd.WriteTransferCount - $ioStart.WriteTransferCount
        Threads = $activeProcess.Threads.Count
        ChildProcesses = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$($activeProcess.Id)").Count
    }

    Stop-Playline -Process $activeProcess
    $activeProcess = $null

    [pscustomobject]@{
        TimestampUtc = [DateTimeOffset]::UtcNow
        RunsPerLibrarySize = $Runs
        Libraries = $rows
        Idle = $idle
    } | ConvertTo-Json -Depth 4
}
finally {
    if ($null -ne $activeProcess -and -not $activeProcess.HasExited) {
        $activeProcess.Kill()
        $activeProcess.WaitForExit()
    }

    if (Test-Path -LiteralPath $metricsRoot) {
        Remove-Item -LiteralPath $metricsRoot -Recurse -Force
    }
}
