# Empaqueta la aplicación para Windows: .exe self-contained + instalador .msi  (RNF-04)
#
#   .\publicar-windows.ps1                 # x64
#   .\publicar-windows.ps1 -Arquitectura arm64
#
# Firma (opcional, requiere un certificado de code signing):
#   $env:HUELLA_CERTIFICADO = "AB12..."; .\publicar-windows.ps1
#
# Requisitos: .NET 10 SDK y WiX v5  ->  dotnet tool install --global wix
#
# IMPORTANTE: este paquete NO incluye Ghostscript, y no debe incluirlo (ADR-003, AGPL).

param(
    [ValidateSet('x64', 'arm64')]
    [string]$Arquitectura = 'x64'
)

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$csproj  = 'src/CompresorPdf.App/CompresorPdf.App.csproj'
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { $version = '1.0.0' }

$salida   = "artifacts/windows"
$publicar = "$salida/_app"
$msi      = "$salida/CompresorPdf-$version-$Arquitectura.msi"

Write-Host "== Compresor de PDFs $version - Windows/$Arquitectura ==" -ForegroundColor Cyan

# --- 1. Publicar -----------------------------------------------------------
Write-Host "-> Publicando win-$Arquitectura (self-contained)"
Remove-Item -Recurse -Force $salida -ErrorAction SilentlyContinue
dotnet publish $csproj -c Release -r "win-$Arquitectura" --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=none -o $publicar --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Falló dotnet publish" }

# --- 2. Firmar los binarios (opcional) -------------------------------------
if ($env:HUELLA_CERTIFICADO) {
    Write-Host "-> Firmando el ejecutable"
    & signtool sign /sha1 $env:HUELLA_CERTIFICADO /fd SHA256 `
        /tr http://timestamp.digicert.com /td SHA256 "$publicar/CompresorPdf.exe"
    if ($LASTEXITCODE -ne 0) { throw "Falló la firma del ejecutable" }
} else {
    Write-Host "-> Sin firmar (define HUELLA_CERTIFICADO para firmar)." -ForegroundColor Yellow
    Write-Host "   SmartScreen mostrará un aviso al instalar."
}

# --- 3. Construir el MSI ----------------------------------------------------
if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    throw "Falta WiX. Instálalo con: dotnet tool install --global wix"
}

Write-Host "-> Construyendo el instalador MSI"
$rutaAbsoluta = (Resolve-Path $publicar).Path
wix extension add --global WixToolset.UI.wixext | Out-Null
wix build build/instalador.wxs `
    -ext WixToolset.UI.wixext `
    -d "PublishDir=$rutaAbsoluta" `
    -d "Version=$version" `
    -arch $Arquitectura `
    -o $msi
if ($LASTEXITCODE -ne 0) { throw "Falló la construcción del MSI" }

if ($env:HUELLA_CERTIFICADO) {
    Write-Host "-> Firmando el instalador"
    & signtool sign /sha1 $env:HUELLA_CERTIFICADO /fd SHA256 `
        /tr http://timestamp.digicert.com /td SHA256 $msi
}

Write-Host ""
Write-Host "OK  EXE: $publicar/CompresorPdf.exe" -ForegroundColor Green
Write-Host "OK  MSI: $msi" -ForegroundColor Green
Write-Host ""
Write-Host "Recuerda: el usuario necesita Ghostscript instalado."
Write-Host "https://www.ghostscript.com/releases/gsdnld.html"
