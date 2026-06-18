[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string]$Version = '1.0.3'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$installerDirectory = $PSScriptRoot
$repositoryDirectory = Split-Path -Parent $installerDirectory
$publishDirectory = Join-Path $installerDirectory 'Publish'
$outputDirectory = Join-Path $installerDirectory 'Output'
$innoScriptPath = Join-Path $installerDirectory 'ScheduleInstaller.iss'
$iconPath = Join-Path $installerDirectory 'Assets\ggpk.ico'

$projectPath = @(
    (Join-Path $repositoryDirectory 'test\test.csproj'),
    (Join-Path $repositoryDirectory 'test\test\test.csproj')
) | Where-Object {
    Test-Path -LiteralPath $_ -PathType Leaf
} | Select-Object -First 1

function Assert-FileExists {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description was not found: $Path"
    }
}

function Reset-BuildDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $fullInstallerPath = [System.IO.Path]::GetFullPath($installerDirectory)
    $fullTargetPath = [System.IO.Path]::GetFullPath($Path)

    if (-not $fullTargetPath.StartsWith(
        $fullInstallerPath + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a directory outside Installer: $fullTargetPath"
    }

    if (Test-Path -LiteralPath $fullTargetPath) {
        Remove-Item -LiteralPath $fullTargetPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $fullTargetPath | Out-Null
}

if (-not $projectPath) {
    throw 'Project file test.csproj was not found in the expected directories.'
}

Assert-FileExists -Path $innoScriptPath -Description 'Inno Setup script'
Assert-FileExists -Path $iconPath -Description 'Application icon'

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCommand) {
    throw 'The dotnet SDK was not found in PATH.'
}

$innoCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }

$innoCompiler = $innoCandidates | Select-Object -First 1
if (-not $innoCompiler) {
    throw 'Inno Setup 6 or 7 was not found.'
}

Reset-BuildDirectory -Path $publishDirectory
Reset-BuildDirectory -Path $outputDirectory

Write-Host "Publishing application version $Version..." -ForegroundColor Cyan

$publishArguments = @(
    'publish',
    $projectPath,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--output', $publishDirectory,
    '--nologo',
    '-p:AssemblyName=Raspisanie',
    '-p:OutputType=WinExe',
    "-p:ApplicationIcon=$iconPath",
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:PublishTrimmed=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

& $dotnetCommand.Source @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishedExe = Join-Path $publishDirectory 'Raspisanie.exe'
Assert-FileExists -Path $publishedExe -Description 'Published application'

Write-Host 'Compiling Inno Setup installer...' -ForegroundColor Cyan

& $innoCompiler "/DMyAppVersion=$Version" $innoScriptPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

$installer = Get-ChildItem -LiteralPath $outputDirectory -Filter '*.exe' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $installer) {
    throw 'The installer executable was not created.'
}

$hash = Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256

Write-Host ''
Write-Host 'Installer created successfully.' -ForegroundColor Green
[pscustomobject]@{
    File = $installer.FullName
    Version = $Version
    SizeMB = [Math]::Round($installer.Length / 1MB, 2)
    SHA256 = $hash.Hash
}
