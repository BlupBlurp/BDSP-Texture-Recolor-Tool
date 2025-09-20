# BDSP Texture Recolor Tool - Build Script
# This script creates optimized build packages

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Release", "Debug", "Both")]
    [string]$Configuration = "Both",
    
    [Parameter(Mandatory=$false)]
    [switch]$Help
)

# Show help information
if ($Help) {
    Write-Host "BDSP Texture Recolor Tool Build Script" -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage: .\build-release.ps1 [-Configuration <Release|Debug|Both>] [-Help]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Parameters:" -ForegroundColor Cyan
    Write-Host "  -Configuration   Choose build configuration (default: Both)" -ForegroundColor White
    Write-Host "    Release        Build only optimized release version (no debug symbols)" -ForegroundColor Gray
    Write-Host "    Debug          Build only debug version (with debug symbols)" -ForegroundColor Gray
    Write-Host "    Both           Build both release and debug versions" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  -Help            Show this help message" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Cyan
    Write-Host "  .\build-release.ps1                    # Build both versions" -ForegroundColor Gray
    Write-Host "  .\build-release.ps1 -Configuration Release   # Build only release" -ForegroundColor Gray
    Write-Host "  .\build-release.ps1 -Configuration Debug     # Build only debug" -ForegroundColor Gray
    exit 0
}

Write-Host "Building BDSP Texture Recolor Tool ($Configuration)..." -ForegroundColor Green

# Clean previous releases
Write-Host "Cleaning previous releases..." -ForegroundColor Yellow
if ($Configuration -eq "Release" -or $Configuration -eq "Both") {
    if (Test-Path "Release-Final") { Remove-Item "Release-Final" -Recurse -Force }
}
if ($Configuration -eq "Debug" -or $Configuration -eq "Both") {
    if (Test-Path "Release-Debug") { Remove-Item "Release-Debug" -Recurse -Force }
}

Set-Location "BDSP-Texture-Recolor-Tool"

# Build Release (Single File, Self-Contained, No Debug Symbols)
if ($Configuration -eq "Release" -or $Configuration -eq "Both") {
    Write-Host "Building Self-Contained Single File Release..." -ForegroundColor Yellow
    dotnet publish --configuration Release --self-contained true --runtime win-x64 --output "../Release-Final" -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false
}

# Build Debug Release (for development/troubleshooting)
if ($Configuration -eq "Debug" -or $Configuration -eq "Both") {
    Write-Host "Building Debug Release..." -ForegroundColor Yellow
    dotnet publish --configuration Release --self-contained true --runtime win-x64 --output "../Release-Debug" -p:PublishSingleFile=true
}

Set-Location ".."

# Display results
Write-Host "`nBuild Results:" -ForegroundColor Green
Write-Host "=============="

if (($Configuration -eq "Release" -or $Configuration -eq "Both") -and (Test-Path "Release-Final/BDSP-Texture-Recolor-Tool.exe")) {
    $finalSize = [math]::Round((Get-Item "Release-Final/BDSP-Texture-Recolor-Tool.exe").Length/1MB,2)
    Write-Host "✅ Release-Final/BDSP-Texture-Recolor-Tool.exe - ${finalSize}MB (RECOMMENDED FOR DISTRIBUTION)" -ForegroundColor Green
    
    # Test the executable
    Write-Host "Testing release executable..." -ForegroundColor Yellow
    $testResult = & "Release-Final/BDSP-Texture-Recolor-Tool.exe" --version 2>&1
    if ($LASTEXITCODE -eq 0 -or $testResult -match "BDSP Texture Recolor Tool") {
        Write-Host "✅ Release executable test passed" -ForegroundColor Green
    } else {
        Write-Host "❌ Release executable test failed" -ForegroundColor Red
    }
}

if (($Configuration -eq "Debug" -or $Configuration -eq "Both") -and (Test-Path "Release-Debug/BDSP-Texture-Recolor-Tool.exe")) {
    $debugSize = [math]::Round((Get-Item "Release-Debug/BDSP-Texture-Recolor-Tool.exe").Length/1MB,2)
    Write-Host "✅ Release-Debug/BDSP-Texture-Recolor-Tool.exe - ${debugSize}MB (with debug symbols)" -ForegroundColor Cyan
}

Write-Host "`nDistribution Instructions:" -ForegroundColor Green
Write-Host "========================="
if ($Configuration -eq "Release" -or $Configuration -eq "Both") {
    Write-Host "📦 Use Release-Final/BDSP-Texture-Recolor-Tool.exe for distribution"
    Write-Host "📦 This single file contains everything needed to run on Windows x64"
    Write-Host "📦 No .NET installation required on target machines"
    Write-Host "📦 Simply copy the .exe file and run it"
}
if ($Configuration -eq "Debug") {
    Write-Host "🐛 Use Release-Debug/BDSP-Texture-Recolor-Tool.exe for debugging"
    Write-Host "🐛 This version includes debug symbols for troubleshooting"
}

Write-Host "`nUsage Examples:" -ForegroundColor Green
Write-Host "==============="
$exeName = if ($Configuration -eq "Debug") { "Release-Debug/BDSP-Texture-Recolor-Tool.exe" } else { "BDSP-Texture-Recolor-Tool.exe" }
Write-Host "Export: $exeName --operation Export -i PokemonBundles -o ExportedTextures"
Write-Host "Import: $exeName --operation Import -i PokemonBundles --textures-path ExportedTextures -o ImportedBundles"
Write-Host "Process: $exeName --operation Process -i PokemonBundles -o ProcessedBundles"