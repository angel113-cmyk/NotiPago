# Publica la app de escritorio (win-x64, self-contained) y genera el Setup.exe con Inno Setup.
# Uso:  powershell -ExecutionPolicy Bypass -File installer\crear-instalador.ps1
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$proyecto = Join-Path $raiz 'src\PagoAlerta.Desktop\PagoAlerta.Desktop.csproj'
$salida = Join-Path $PSScriptRoot 'publish'
$config = Join-Path $raiz 'src\PagoAlerta.Desktop\appsettings.json'

if (-not (Test-Path $config)) {
    throw "Falta src\PagoAlerta.Desktop\appsettings.json (copia appsettings.example.json y pon tu URL y clave anon)."
}

# Version: se lee de Directory.Build.props
[xml]$props = Get-Content (Join-Path $raiz 'Directory.Build.props')
$version = $props.Project.PropertyGroup.VersionPrefix | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { $version = '1.0.0' }

if (Test-Path $salida) { Remove-Item $salida -Recurse -Force }
Write-Host "Publicando PagoAlerta $version (win-x64, self-contained)..."
dotnet publish $proyecto -c Release -r win-x64 --self-contained true -o $salida `
    -p:PublishSingleFile=false -p:DebugType=none -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish fallo.' }

Copy-Item $config (Join-Path $salida 'appsettings.json') -Force

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'No se encontro Inno Setup 6. Instalalo con: winget install JRSoftware.InnoSetup' }

& $iscc "/DVersionApp=$version" (Join-Path $PSScriptRoot 'PagoAlerta.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup fallo.' }

$setup = Get-ChildItem (Join-Path $PSScriptRoot 'Output') -Filter '*.exe' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ''
Write-Host "Instalador listo: $($setup.FullName)  ($([math]::Round($setup.Length/1MB,1)) MB)"
