# HT-16: genera el APK firmado (Release) de PagoAlerta.
# La keystore y su clave viven FUERA del repositorio: %USERPROFILE%\PagoAlerta-keys\
# Uso:  powershell -ExecutionPolicy Bypass -File installer\crear-apk.ps1
param(
    # 'todas' = celulares ARM + emuladores (un solo APK, el definitivo). 'x64' = solo emuladores como BlueStacks (mas liviano).
    [ValidateSet('todas', 'x64')] [string]$Abi = 'todas'
)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$proyecto = Join-Path $raiz 'src\PagoAlerta.Android\PagoAlerta.Android.csproj'
$carpetaClaves = Join-Path $env:USERPROFILE 'PagoAlerta-keys'
$keystore = Join-Path $carpetaClaves 'pagoalerta.keystore'
$propiedades = Join-Path $carpetaClaves 'keystore.properties'
$alias = 'pagoalerta'

if (-not (Test-Path (Join-Path $raiz 'src\PagoAlerta.Android\Assets\appsettings.json'))) {
    throw "Falta src\PagoAlerta.Android\Assets\appsettings.json (copia appsettings.example.json y pon tu URL y clave anon)."
}

# --- 1. Keystore (se crea una sola vez; si ya existe se reutiliza) ---
if (-not (Test-Path $keystore)) {
    $keytool = @("$env:LOCALAPPDATA\Android\jdk-21\bin\keytool.exe", "$env:JAVA_HOME\bin\keytool.exe") |
        Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $keytool) { throw 'No se encontro keytool (JDK).' }

    New-Item -ItemType Directory -Force $carpetaClaves | Out-Null
    $clave = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 28 | ForEach-Object { [char]$_ })
    & $keytool -genkeypair -v -keystore $keystore -alias $alias -keyalg RSA -keysize 2048 -validity 10000 `
        -storepass $clave -keypass $clave -dname 'CN=PagoAlerta, O=PagoAlerta, C=PE' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'keytool fallo.' }
    @("alias=$alias", "password=$clave", "keystore=$keystore") | Set-Content $propiedades -Encoding ascii
    Write-Host ''
    Write-Host '==============================================================' -ForegroundColor Yellow
    Write-Host ' KEYSTORE NUEVA CREADA. HAZ UNA COPIA DE ESTA CARPETA AHORA:'      -ForegroundColor Yellow
    Write-Host "   $carpetaClaves"                                                 -ForegroundColor Yellow
    Write-Host ' Si la pierdes NO podras actualizar la app instalada.'             -ForegroundColor Yellow
    Write-Host '==============================================================' -ForegroundColor Yellow
}

$cfg = @{}
Get-Content $propiedades | ForEach-Object { if ($_ -match '^(\w+)=(.*)$') { $cfg[$matches[1]] = $matches[2] } }
$env:PA_STORE_PASS = $cfg['password']; $env:PA_KEY_PASS = $cfg['password']

# --- 2. Version ---
[xml]$props = Get-Content (Join-Path $raiz 'Directory.Build.props')
$version = $props.Project.PropertyGroup.VersionPrefix | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { $version = '1.0.0' }

# --- 3. Compilar en Release y firmar ---
Write-Host "Compilando PagoAlerta $version (Release, firmado)..."
$extra = @()
if ($Abi -eq 'x64') { $extra += '-p:RuntimeIdentifier=android-x64' }
dotnet publish $proyecto -c Release -f net11.0-android @extra `
    -p:AndroidPackageFormat=apk `
    -p:AndroidKeyStore=true `
    "-p:AndroidSigningKeyStore=$keystore" `
    "-p:AndroidSigningKeyAlias=$alias" `
    "-p:AndroidSigningStorePass=env:PA_STORE_PASS" `
    "-p:AndroidSigningKeyPass=env:PA_KEY_PASS"
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish fallo.' }
Remove-Item Env:PA_STORE_PASS, Env:PA_KEY_PASS -ErrorAction SilentlyContinue

$apk = Get-ChildItem (Join-Path $raiz 'src\PagoAlerta.Android\bin\Release') -Recurse -Filter '*-Signed.apk' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) { throw 'No se encontro el APK firmado.' }

$dist = Join-Path $raiz 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$sufijo = if ($Abi -eq 'x64') { '-emulador-x64' } else { '' }
$final = Join-Path $dist "PagoAlerta-$version$sufijo.apk"
Copy-Item $apk.FullName $final -Force
Write-Host ''
Write-Host "APK listo: $final  ($([math]::Round((Get-Item $final).Length/1MB,1)) MB)"

