# Build and Package TaskSwitcher
# This script builds the application and creates the installer

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TaskSwitcher Build and Package Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get paths
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionDir = Split-Path -Parent $scriptPath
$projectPath = Join-Path $solutionDir "TaskSwitcher\TaskSwitcher.csproj"
$installerScript = Join-Path $scriptPath "Installer.iss"
$innoSetupCompiler = Join-Path $scriptPath "InnoSetup\ISCC.exe"

# Step 1: Clean previous builds
Write-Host "Step 1: Cleaning previous builds..." -ForegroundColor Yellow
$publishPath = Join-Path $solutionDir "TaskSwitcher\bin\$Configuration\net10.0-windows7.0\$Runtime\publish"
if (Test-Path $publishPath) {
    Remove-Item -Path $publishPath -Recurse -Force
    Write-Host "  Cleaned: $publishPath" -ForegroundColor Green
}

# Step 2: Build and publish the application
Write-Host ""
Write-Host "Step 2: Building and publishing application..." -ForegroundColor Yellow
Write-Host "  Configuration: $Configuration" -ForegroundColor Gray
Write-Host "  Runtime: $Runtime" -ForegroundColor Gray

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "  Build successful!" -ForegroundColor Green

# Step 3: Verify build output
Write-Host ""
Write-Host "Step 3: Verifying build output..." -ForegroundColor Yellow
$exePath = Join-Path $publishPath "TaskSwitcher.exe"
if (Test-Path $exePath) {
    $fileInfo = Get-Item $exePath
    Write-Host "  TaskSwitcher.exe found: $($fileInfo.Length) bytes" -ForegroundColor Green
} else {
    Write-Host "  ERROR: TaskSwitcher.exe not found!" -ForegroundColor Red
    exit 1
}

# Step 4: Compile installer
Write-Host ""
Write-Host "Step 4: Compiling installer..." -ForegroundColor Yellow

if (-not (Test-Path $innoSetupCompiler)) {
    Write-Host "  ERROR: Inno Setup compiler not found at: $innoSetupCompiler" -ForegroundColor Red
    Write-Host "  Please ensure Inno Setup is installed in the Installer\InnoSetup directory" -ForegroundColor Red
    exit 1
}

$outputDir = Join-Path $scriptPath "Output"
if (-not (Test-Path $outputDir)) {
    New-Item -Path $outputDir -ItemType Directory | Out-Null
}

& $innoSetupCompiler $installerScript

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Installer compilation failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "  Installer compiled successfully!" -ForegroundColor Green

# Step 5: Display results
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$installerPath = Get-ChildItem -Path $outputDir -Filter "TaskSwitcher-Setup-*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($installerPath) {
    Write-Host ""
    Write-Host "Installer created at:" -ForegroundColor Green
    Write-Host "  $($installerPath.FullName)" -ForegroundColor White
    Write-Host "  Size: $([math]::Round($installerPath.Length / 1MB, 2)) MB" -ForegroundColor Gray
} else {
    Write-Host ""
    Write-Host "WARNING: Installer file not found in output directory" -ForegroundColor Yellow
}

Write-Host ""
