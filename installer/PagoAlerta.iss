; HT-15 / HU-09: instalador de Windows (Inno Setup 6).
; Se compila con installer\crear-instalador.ps1, que antes publica la app self-contained (no requiere instalar .NET).

#define NombreApp "PagoAlerta"
#ifndef VersionApp
  #define VersionApp "1.0.0"
#endif
#define Editor "PagoAlerta"
#define Ejecutable "PagoAlerta.exe"

[Setup]
AppId={{6B0F7E52-3C1A-4E0B-9A71-5D2C8F4A9E31}
AppName={#NombreApp}
AppVersion={#VersionApp}
AppPublisher={#Editor}
DefaultDirName={autopf}\{#NombreApp}
DefaultGroupName={#NombreApp}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#Ejecutable}
UninstallDisplayName={#NombreApp}
SetupIconFile=..\src\PagoAlerta.Desktop\pagoalerta.ico
OutputDir=Output
OutputBaseFilename=PagoAlerta-Setup-{#VersionApp}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el &Escritorio"; GroupDescription: "Accesos directos:"
Name: "autostart"; Description: "Iniciar {#NombreApp} al encender Windows (queda junto al reloj, recibiendo pagos)"; GroupDescription: "Segundo plano:"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#NombreApp}"; Filename: "{app}\{#Ejecutable}"
Name: "{group}\Desinstalar {#NombreApp}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#NombreApp}"; Filename: "{app}\{#Ejecutable}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#NombreApp}"; ValueData: """{app}\{#Ejecutable}"" --segundo-plano"; Tasks: autostart

[Run]
Filename: "{app}\{#Ejecutable}"; Description: "Abrir {#NombreApp}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Desinstalacion limpia: se borran tambien los datos locales (sesion cifrada, preferencias y registros).
Type: filesandordirs; Name: "{localappdata}\{#NombreApp}"

[Code]
// La app puede estar oculta junto al reloj de Windows: se cierra antes de instalar o desinstalar.
procedure CerrarAplicacion();
var
  CodigoSalida: Integer;
begin
  Exec('taskkill.exe', '/F /IM {#Ejecutable}', '', SW_HIDE, ewWaitUntilTerminated, CodigoSalida);
end;

function InitializeSetup(): Boolean;
begin
  CerrarAplicacion();
  Result := True;
end;

function InitializeUninstall(): Boolean;
begin
  CerrarAplicacion();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Desinstalacion limpia: tambien se quita el inicio con Windows (lo pudo activar la app desde Ajustes).
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#NombreApp}');
end;