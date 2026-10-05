[CmdletBinding()]
param(
    [string]$InnoCompiler,
    [switch]$ZipOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsDirectory = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$publishDirectory = Join-Path $artifactsDirectory 'publish\win-x64'
$project = Join-Path $repoRoot 'src\Playline.App\Playline.App.csproj'
$solution = Join-Path $repoRoot 'Playline.sln'
$installerScript = Join-Path $repoRoot 'installer\Playline.iss'
$releaseNotes = Join-Path $repoRoot 'docs\playline-release-phases7-9\RELEASE_NOTES.md'

if ((Split-Path $artifactsDirectory -Parent) -ne $repoRoot -or
    (Split-Path $artifactsDirectory -Leaf) -ne 'artifacts') {
    throw "Diretório de artefatos inseguro: $artifactsDirectory"
}

$dotnetCandidates = @(
    (Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'dotnet\dotnet.exe'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1

if (-not $dotnetCandidates) {
    throw 'O .NET SDK não foi encontrado.'
}

$dotnet = $dotnetCandidates

if (-not $ZipOnly) {
    if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
        $innoCandidates = @(
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
        ) | Where-Object { Test-Path -LiteralPath $_ }

        $InnoCompiler = $innoCandidates | Select-Object -First 1
    }

    if ([string]::IsNullOrWhiteSpace($InnoCompiler) -or -not (Test-Path -LiteralPath $InnoCompiler)) {
        throw 'ISCC.exe não encontrado. Instale o Inno Setup 6 ou informe -InnoCompiler.'
    }
}

if (Test-Path -LiteralPath $artifactsDirectory) {
    Remove-Item -LiteralPath $artifactsDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

& $dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'Falha no restore.' }

& $dotnet test $solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes.' }

& $dotnet publish $project -c Release --no-restore -p:PublishProfile=win-x64 --output $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Falha na publicação.' }

$symbolFiles = Get-ChildItem -LiteralPath $publishDirectory -Filter '*.pdb' -File -Recurse
if ($symbolFiles) {
    $symbolFiles | Remove-Item -Force
}

$portableArchive = Join-Path $artifactsDirectory 'Playline-Portable-x64.zip'
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $portableArchive -CompressionLevel Optimal

if (-not $ZipOnly) {
    & $InnoCompiler $installerScript
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o instalador.' }
}

Copy-Item -LiteralPath $releaseNotes -Destination (Join-Path $artifactsDirectory 'release-notes.md')

$deliverables = @($portableArchive)
if (-not $ZipOnly) {
    $deliverables = @(
        (Join-Path $artifactsDirectory 'Playline-Setup-x64.exe'),
        $portableArchive
    )
}

$checksumLines = foreach ($deliverable in $deliverables) {
    $hash = Get-FileHash -LiteralPath $deliverable -Algorithm SHA256
    "SHA256  $($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($deliverable))"
}

[System.IO.File]::WriteAllLines(
    (Join-Path $artifactsDirectory 'checksums.txt'),
    $checksumLines,
    [System.Text.UTF8Encoding]::new($false))

$summary = foreach ($deliverable in $deliverables) {
    $file = Get-Item -LiteralPath $deliverable
    [pscustomobject]@{
        File = $file.Name
        Megabytes = [Math]::Round($file.Length / 1MB, 2)
    }
}

$summary | Format-Table -AutoSize
