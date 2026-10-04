[CmdletBinding()]
param(
    [string]$ArtifactsDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    $ArtifactsDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts'
}

$artifacts = [System.IO.Path]::GetFullPath($ArtifactsDirectory)
$setup = Join-Path $artifacts 'Playline-Setup-x64.exe'
$portableArchive = Join-Path $artifacts 'Playline-Portable-x64.zip'
$checksums = Join-Path $artifacts 'checksums.txt'

foreach ($requiredFile in @($setup, $portableArchive, $checksums)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Artefato ausente: $requiredFile"
    }
}

$expectedHashes = @{}
foreach ($line in Get-Content -LiteralPath $checksums) {
    if ($line -notmatch '^SHA256\s+([a-fA-F0-9]{64})\s+(.+)$') {
        throw "Linha de checksum inválida: $line"
    }

    $expectedHashes[$matches[2]] = $matches[1].ToLowerInvariant()
}

foreach ($file in @($setup, $portableArchive)) {
    $name = [System.IO.Path]::GetFileName($file)
    $actual = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if (-not $expectedHashes.ContainsKey($name) -or $expectedHashes[$name] -ne $actual) {
        throw "Checksum divergente: $name"
    }
}

$temporaryBase = [System.IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
$validationRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $temporaryBase ('Playline-ReleaseValidation-' + [Guid]::NewGuid().ToString('N'))))

if (-not $validationRoot.StartsWith($temporaryBase, [StringComparison]::OrdinalIgnoreCase) -or
    (Split-Path $validationRoot -Leaf) -notlike 'Playline-ReleaseValidation-*') {
    throw "Diretório temporário inseguro: $validationRoot"
}

$installDirectory = Join-Path $validationRoot 'installed'
$dataDirectory = Join-Path $validationRoot 'data'
$portableDirectory = Join-Path $validationRoot 'portable'
$trackedProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$result = [ordered]@{
    ChecksumsValid = $true
    FirstInstall = $false
    FirstLaunch = $false
    Upgrade = $false
    LaunchAfterUpgrade = $false
    Uninstall = $false
    ProgramFilesRemoved = $false
    DataPreservedAfterUninstall = $false
    PortableLaunch = $false
    RemainingStartedProcesses = 0
}

function Invoke-HiddenProcess {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$ArgumentList
    )

    $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Processo falhou com código $($process.ExitCode): $FilePath"
    }
}

function Start-AndValidatePlayline {
    param(
        [Parameter(Mandatory)] [string]$Executable,
        [Parameter(Mandatory)] [string]$DataRoot
    )

    $previousDataRoot = $env:PLAYLINE_DATA_DIRECTORY
    $previousMultilevelLookup = $env:DOTNET_MULTILEVEL_LOOKUP
    try {
        $env:PLAYLINE_DATA_DIRECTORY = $DataRoot
        $env:DOTNET_MULTILEVEL_LOOKUP = '0'
        $process = Start-Process -FilePath $Executable -PassThru
        $trackedProcesses.Add($process)
    }
    finally {
        $env:PLAYLINE_DATA_DIRECTORY = $previousDataRoot
        $env:DOTNET_MULTILEVEL_LOOKUP = $previousMultilevelLookup
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 50
        $process.Refresh()
    } until ($process.HasExited -or $process.MainWindowHandle -ne 0 -or [DateTime]::UtcNow -ge $deadline)

    if ($process.HasExited) {
        throw "Playline encerrou durante a inicialização com código $($process.ExitCode): $Executable"
    }

    if ($process.MainWindowHandle -eq 0 -or $process.MainWindowTitle -ne 'Playline') {
        throw "A janela principal não foi encontrada: $Executable"
    }

    $runningPath = [System.IO.Path]::GetFullPath($process.MainModule.FileName)
    if (-not [string]::Equals($runningPath, [System.IO.Path]::GetFullPath($Executable), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Executável inesperado: $runningPath"
    }

    if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(7000)) {
        if (-not $process.HasExited) {
            $process.Kill()
            $process.WaitForExit()
        }

        throw "Playline não encerrou normalmente: $Executable"
    }
}

New-Item -ItemType Directory -Path $validationRoot, $dataDirectory | Out-Null

try {
    $installArguments = @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/NOICONS',
        '/LANG=brazilianportuguese',
        ('/DIR="' + $installDirectory + '"'),
        ('/LOG="' + (Join-Path $validationRoot 'install.log') + '"')
    )

    Invoke-HiddenProcess -FilePath $setup -ArgumentList $installArguments
    $installedExecutable = Join-Path $installDirectory 'Playline.exe'
    if (-not (Test-Path -LiteralPath $installedExecutable -PathType Leaf)) {
        throw 'O instalador não criou Playline.exe.'
    }
    $result.FirstInstall = $true

    Start-AndValidatePlayline -Executable $installedExecutable -DataRoot $dataDirectory
    foreach ($createdFile in @('games.json', 'settings.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $dataDirectory $createdFile) -PathType Leaf)) {
            throw "Arquivo de dados não criado: $createdFile"
        }
    }
    $result.FirstLaunch = $true

    $marker = Join-Path $dataDirectory 'upgrade-preservation-marker.txt'
    [System.IO.File]::WriteAllText($marker, 'playline-release-validation', [System.Text.UTF8Encoding]::new($false))

    $installArguments[-1] = '/LOG="' + (Join-Path $validationRoot 'upgrade.log') + '"'
    Invoke-HiddenProcess -FilePath $setup -ArgumentList $installArguments
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        throw 'A atualização não preservou os dados isolados.'
    }
    $result.Upgrade = $true

    Start-AndValidatePlayline -Executable $installedExecutable -DataRoot $dataDirectory
    $result.LaunchAfterUpgrade = $true

    $uninstaller = Join-Path $installDirectory 'unins000.exe'
    if (-not (Test-Path -LiteralPath $uninstaller -PathType Leaf)) {
        throw 'Desinstalador ausente.'
    }

    Invoke-HiddenProcess -FilePath $uninstaller -ArgumentList @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        ('/LOG="' + (Join-Path $validationRoot 'uninstall.log') + '"')
    )
    $result.Uninstall = $true
    $result.ProgramFilesRemoved = -not (Test-Path -LiteralPath $installedExecutable)
    $result.DataPreservedAfterUninstall = Test-Path -LiteralPath $marker -PathType Leaf

    if (-not $result.ProgramFilesRemoved -or -not $result.DataPreservedAfterUninstall) {
        throw 'A remoção de arquivos ou a preservação de dados falhou.'
    }

    Expand-Archive -LiteralPath $portableArchive -DestinationPath $portableDirectory
    Start-AndValidatePlayline -Executable (Join-Path $portableDirectory 'Playline.exe') -DataRoot (Join-Path $validationRoot 'portable-data')
    $result.PortableLaunch = $true
}
finally {
    foreach ($process in $trackedProcesses) {
        if (-not $process.HasExited) {
            $process.Kill()
            $process.WaitForExit()
        }
    }

    $result.RemainingStartedProcesses = @($trackedProcesses | Where-Object { -not $_.HasExited }).Count

    $remainingUninstaller = Join-Path $installDirectory 'unins000.exe'
    if (Test-Path -LiteralPath $remainingUninstaller -PathType Leaf) {
        Invoke-HiddenProcess -FilePath $remainingUninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')
    }

    if (Test-Path -LiteralPath $validationRoot) {
        Remove-Item -LiteralPath $validationRoot -Recurse -Force
    }
}

[pscustomobject]$result | ConvertTo-Json
