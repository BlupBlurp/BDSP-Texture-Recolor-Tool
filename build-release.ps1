# BDSP Texture Recolor Tool - Release Build Script
# This script creates optimized release packages

Write-Host "Building BDSP Texture Recolor Tool Release Packages..." -ForegroundColor Green

# Clean previous releases
Write-Host "Cleaning previous releases..." -ForegroundColor Yellow
if (Test-Path "Release-Final") { Remove-Item "Release-Final" -Recurse -Force }
if (Test-Path "Release-Debug") { Remove-Item "Release-Debug" -Recurse -Force }

# Build Release (Single File, Self-Contained, No Debug Symbols)
# This is the recommended distribution version
Write-Host "Building Self-Contained Single File Release (Recommended)..." -ForegroundColor Yellow
Set-Location "BDSP-CSharp-Randomizer"
dotnet publish --configuration Release --self-contained true --runtime win-x64 --output "../Release-Final" -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false

# Build Debug Release (for development/troubleshooting)
Write-Host "Building Debug Release..." -ForegroundColor Yellow
dotnet publish --configuration Release --self-contained true --runtime win-x64 --output "../Release-Debug" -p:PublishSingleFile=true

Set-Location ".."

# Display results
Write-Host "`nBuild Results:" -ForegroundColor Green
Write-Host "=============="

if (Test-Path "Release-Final/BDSP-Texture-Recolor-Tool.exe") {
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

if (Test-Path "Release-Debug/BDSP-Texture-Recolor-Tool.exe") {
    $debugSize = [math]::Round((Get-Item "Release-Debug/BDSP-Texture-Recolor-Tool.exe").Length/1MB,2)
    Write-Host "✅ Release-Debug/BDSP-Texture-Recolor-Tool.exe - ${debugSize}MB (with debug symbols)" -ForegroundColor Cyan
}

Write-Host "`nDistribution Instructions:" -ForegroundColor Green
Write-Host "========================="
Write-Host "📦 Use Release-Final/BDSP-Texture-Recolor-Tool.exe for distribution"
Write-Host "📦 This single file contains everything needed to run on Windows x64"
Write-Host "📦 No .NET installation required on target machines"
Write-Host "📦 Simply copy the .exe file and run it"

Write-Host "`nUsage Examples:" -ForegroundColor Green
Write-Host "==============="
Write-Host "Export: BDSP-Texture-Recolor-Tool.exe --operation Export -i PokemonBundles -o ExportedTextures"
Write-Host "Import: BDSP-Texture-Recolor-Tool.exe --operation Import -i PokemonBundles --textures-path ExportedTextures -o ImportedBundles"
Write-Host "Process: BDSP-Texture-Recolor-Tool.exe --operation Process -i PokemonBundles -o ProcessedBundles"