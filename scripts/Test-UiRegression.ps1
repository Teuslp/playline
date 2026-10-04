[CmdletBinding()]
param(
    [int]$Cycles = 10
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Cycles -lt 1) {
    throw 'Cycles deve ser maior que zero.'
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executable = Join-Path $repoRoot 'artifacts\publish\win-x64\Playline.exe'
$diagnosticsProject = Join-Path $repoRoot 'tools\Playline.Diagnostics\Playline.Diagnostics.csproj'
$dotnet = @(
    (Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'dotnet\dotnet.exe'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1

if (-not (Test-Path -LiteralPath $executable -PathType Leaf) -or -not $dotnet) {
    throw 'A publicação ou o .NET SDK não foi encontrado.'
}

$temporaryBase = [System.IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
$testRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $temporaryBase ('Playline-UIRegression-' + [Guid]::NewGuid().ToString('N'))))
if (-not $testRoot.StartsWith($temporaryBase, [StringComparison]::OrdinalIgnoreCase) -or
    (Split-Path $testRoot -Leaf) -notlike 'Playline-UIRegression-*') {
    throw "Diretório temporário inseguro: $testRoot"
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PlaylineForegroundWindow
{
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);
}
'@

function Wait-AutomationElement {
    param(
        [Parameter(Mandatory)] [Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)] [Windows.Automation.AutomationProperty]$Property,
        [Parameter(Mandatory)] [object]$Value,
        [int]$TargetProcessId = 0,
        [int]$TimeoutSeconds = 10
    )

    [Windows.Automation.Condition]$condition = [Windows.Automation.PropertyCondition]::new($Property, $Value)
    if ($TargetProcessId -gt 0) {
        $condition = [Windows.Automation.AndCondition]::new(
            $condition,
            [Windows.Automation.PropertyCondition]::new(
                [Windows.Automation.AutomationElement]::ProcessIdProperty,
                $TargetProcessId))
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $element = $Root.FindFirst([Windows.Automation.TreeScope]::Descendants, $condition)
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 50
    } until ([DateTime]::UtcNow -ge $deadline)

    throw "Elemento de automação não encontrado: $Value"
}

function Invoke-AutomationElement {
    param([Parameter(Mandatory)] [Windows.Automation.AutomationElement]$Element)

    $pattern = $Element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
    ([Windows.Automation.InvokePattern]$pattern).Invoke()
}

function Get-ContainingWindow {
    param([Parameter(Mandatory)] [Windows.Automation.AutomationElement]$Element)

    $current = $Element
    while ($null -ne $current -and $current.Current.ControlType -ne [Windows.Automation.ControlType]::Window) {
        $current = [Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($current)
    }

    if ($null -eq $current) { throw 'A janela contêiner não foi encontrada.' }
    return $current
}

function Open-OverflowItem {
    param(
        [Parameter(Mandatory)] [Windows.Automation.AutomationElement]$MainWindow,
        [Parameter(Mandatory)] [string]$AutomationId
    )

    $button = Wait-AutomationElement -Root $MainWindow `
        -Property ([Windows.Automation.AutomationElement]::AutomationIdProperty) `
        -Value 'OverflowMenuButton'
    Invoke-AutomationElement $button

    $item = Wait-AutomationElement -Root ([Windows.Automation.AutomationElement]::RootElement) `
        -Property ([Windows.Automation.AutomationElement]::AutomationIdProperty) `
        -Value $AutomationId `
        -TargetProcessId $MainWindow.Current.ProcessId
    Start-Sleep -Milliseconds 500
    Write-Verbose "Invoking $AutomationId ($($item.Current.Name)), process $($item.Current.ProcessId), offscreen $($item.Current.IsOffscreen)."
    Invoke-AutomationElement $item
}

function Close-SettingsDialog {
    param(
        [Parameter(Mandatory)] [Windows.Automation.AutomationElement]$MainWindow,
        [switch]$CaptureAccessibility
    )

    Open-OverflowItem -MainWindow $MainWindow -AutomationId 'SettingsMenuItem'
    $save = Wait-AutomationElement -Root ([Windows.Automation.AutomationElement]::RootElement) `
        -Property ([Windows.Automation.AutomationElement]::AutomationIdProperty) `
        -Value 'SaveSettingsButton' `
        -TargetProcessId $MainWindow.Current.ProcessId
    $dialog = Get-ContainingWindow $save

    $namedFields = 0
    if ($CaptureAccessibility) {
        foreach ($id in @('SettingsDisplayMode', 'SettingsItemSize', 'SettingsBarOrientation', 'SettingsAfterLaunch')) {
            $field = Wait-AutomationElement -Root $dialog `
                -Property ([Windows.Automation.AutomationElement]::AutomationIdProperty) `
                -Value $id
            if (-not [string]::IsNullOrWhiteSpace($field.Current.Name)) { $namedFields++ }
        }
    }

    Invoke-AutomationElement $save
    Start-Sleep -Milliseconds 300
    return $namedFields
}

function Close-DiscoveryDialog {
    param([Parameter(Mandatory)] [Windows.Automation.AutomationElement]$MainWindow)

    Open-OverflowItem -MainWindow $MainWindow -AutomationId 'DiscoverGamesMenuItem'
    $results = Wait-AutomationElement -Root ([Windows.Automation.AutomationElement]::RootElement) `
        -Property ([Windows.Automation.AutomationElement]::AutomationIdProperty) `
        -Value 'DiscoveryResults' `
        -TargetProcessId $MainWindow.Current.ProcessId
    $dialog = Get-ContainingWindow $results
    Start-Sleep -Milliseconds 350
    $cancel = Wait-AutomationElement -Root $dialog `
        -Property ([Windows.Automation.AutomationElement]::NameProperty) `
        -Value 'Cancelar'
    if (-not $cancel.Current.IsEnabled) { throw 'O botão Cancelar da descoberta está desabilitado.' }
    $windowPattern = [Windows.Automation.WindowPattern]$dialog.GetCurrentPattern(
        [Windows.Automation.WindowPattern]::Pattern)
    $windowPattern.Close()
    Start-Sleep -Milliseconds 300
}

function Close-EditDialog {
    param(
        [Parameter(Mandatory)] [Windows.Automation.AutomationElement]$MainWindow,
        [Parameter(Mandatory)] [System.Diagnostics.Process]$Process,
        [switch]$CaptureAccessibility
    )

    $game = Wait-AutomationElement -Root $MainWindow `
        -Property ([Windows.Automation.AutomationElement]::AutomationIdProperty) `
        -Value 'Game-diagnostic-0'
    [void][PlaylineForegroundWindow]::SetForegroundWindow($Process.MainWindowHandle)
    $game.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait('+{F10}')

    $editItem = Wait-AutomationElement -Root ([Windows.Automation.AutomationElement]::RootElement) `
        -Property ([Windows.Automation.AutomationElement]::AutomationIdProperty) `
        -Value 'EditGameMenuItem' `
        -TargetProcessId $Process.Id
    Invoke-AutomationElement $editItem

    $save = Wait-AutomationElement -Root ([Windows.Automation.AutomationElement]::RootElement) `
        -Property ([Windows.Automation.AutomationElement]::AutomationIdProperty) `
        -Value 'SaveGameButton' `
        -TargetProcessId $Process.Id
    $dialog = Get-ContainingWindow $save

    $namedFields = 0
    if ($CaptureAccessibility) {
        foreach ($id in @('EditGameName', 'EditGameExecutable', 'EditGameArguments', 'EditGameWorkingDirectory', 'EditGameIcon')) {
            $field = Wait-AutomationElement -Root $dialog `
                -Property ([Windows.Automation.AutomationElement]::AutomationIdProperty) `
                -Value $id
            if (-not [string]::IsNullOrWhiteSpace($field.Current.Name)) { $namedFields++ }
        }
    }

    Invoke-AutomationElement $save
    Start-Sleep -Milliseconds 300
    return $namedFields
}

function Invoke-UiCycle {
    param(
        [Parameter(Mandatory)] [Windows.Automation.AutomationElement]$MainWindow,
        [Parameter(Mandatory)] [System.Diagnostics.Process]$Process,
        [switch]$CaptureAccessibility
    )

    $currentMainWindow = [Windows.Automation.AutomationElement]::FromHandle($Process.MainWindowHandle)
    $settingsFields = Close-SettingsDialog -MainWindow $currentMainWindow -CaptureAccessibility:$CaptureAccessibility
    $currentMainWindow = [Windows.Automation.AutomationElement]::FromHandle($Process.MainWindowHandle)
    Close-DiscoveryDialog -MainWindow $currentMainWindow
    $currentMainWindow = [Windows.Automation.AutomationElement]::FromHandle($Process.MainWindowHandle)
    $editFields = Close-EditDialog -MainWindow $currentMainWindow -Process $Process -CaptureAccessibility:$CaptureAccessibility
    return [pscustomobject]@{ SettingsNamedFields = $settingsFields; EditNamedFields = $editFields }
}

New-Item -ItemType Directory -Path $testRoot | Out-Null
$process = $null

try {
    & $dotnet build $diagnosticsProject -c Release --nologo | Out-Null
    & $dotnet run --no-build --project $diagnosticsProject -c Release -- "--prepare-root=$testRoot" '--games=10' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao preparar os dados de regressão.' }

    $previousDataRoot = $env:PLAYLINE_DATA_DIRECTORY
    try {
        $env:PLAYLINE_DATA_DIRECTORY = $testRoot
        $process = Start-Process -FilePath $executable -PassThru
    }
    finally {
        $env:PLAYLINE_DATA_DIRECTORY = $previousDataRoot
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 50
        $process.Refresh()
    } until ($process.HasExited -or $process.MainWindowHandle -ne 0 -or [DateTime]::UtcNow -ge $deadline)
    if ($process.HasExited -or $process.MainWindowHandle -eq 0) { throw 'A inicialização da UI falhou.' }

    $mainWindow = [Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    $accessibility = Invoke-UiCycle -MainWindow $mainWindow -Process $process -CaptureAccessibility
    Start-Sleep -Seconds 5
    $process.Refresh()
    $baselinePrivateMB = $process.PrivateMemorySize64 / 1MB
    $samples = @()

    for ($cycle = 1; $cycle -le $Cycles; $cycle++) {
        [void](Invoke-UiCycle -MainWindow $mainWindow -Process $process)
        Start-Sleep -Milliseconds 500
        $process.Refresh()
        $samples += [pscustomobject]@{
            Cycle = $cycle
            PrivateMB = [Math]::Round($process.PrivateMemorySize64 / 1MB, 1)
            WorkingSetMB = [Math]::Round($process.WorkingSet64 / 1MB, 1)
            Threads = $process.Threads.Count
        }
    }

    Start-Sleep -Seconds 10
    $process.Refresh()
    $topLevelWindows = [Windows.Automation.AutomationElement]::RootElement.FindAll(
        [Windows.Automation.TreeScope]::Children,
        [Windows.Automation.PropertyCondition]::new(
            [Windows.Automation.AutomationElement]::ProcessIdProperty,
            $process.Id)).Count

    $gamesDocument = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'games.json') | ConvertFrom-Json
    $settingsDocument = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'settings.json') | ConvertFrom-Json
    $logFiles = @(Get-ChildItem -LiteralPath (Join-Path $testRoot 'logs') -File -ErrorAction SilentlyContinue)

    [pscustomobject]@{
        WarmupSettingsNamedFields = $accessibility.SettingsNamedFields
        WarmupEditNamedFields = $accessibility.EditNamedFields
        Cycles = $Cycles
        BaselinePrivateMB = [Math]::Round($baselinePrivateMB, 1)
        FinalPrivateMB = [Math]::Round($process.PrivateMemorySize64 / 1MB, 1)
        FinalWorkingSetMB = [Math]::Round($process.WorkingSet64 / 1MB, 1)
        FinalThreads = $process.Threads.Count
        RemainingTopLevelWindows = $topLevelWindows
        PersistedGames = @($gamesDocument.games).Count
        SettingsJsonValid = $null -ne $settingsDocument
        CriticalLogFiles = $logFiles.Count
        Samples = $samples
    } | ConvertTo-Json -Depth 4

    if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(7000)) {
        throw 'O encerramento normal falhou.'
    }
    $process = $null
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }

    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
