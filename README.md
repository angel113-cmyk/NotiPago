# PagoAlerta

Notificador de pagos para una tienda. El celular Android lee las notificaciones de pagos (Yape, Plin, BBVA, etc.)
y las envía a la nube (Supabase); la app de Windows muestra los pagos en tiempo real. No necesitan estar en la
misma red Wi-Fi.

```
Celular Android  ──►  Supabase (PostgreSQL + Auth + Realtime)  ──►  App de escritorio (Windows)
 lee notificaciones     guarda y avisa                              lista, filtra y exporta
```

| Carpeta | Contenido |
|---|---|
| `src/PagoAlerta.Core` | Modelos, parser de notificaciones, cliente Supabase, cola de envío, exportación |
| `src/PagoAlerta.Desktop` | App de escritorio (Windows Forms, .NET 11) |
| `src/PagoAlerta.Android` | App Android (.NET for Android, sin MAUI) |
| `tests/PagoAlerta.Core.Tests` | Pruebas unitarias |
| `supabase/` | Scripts SQL y guía de Auth |
| `installer/` | Scripts del instalador de Windows y del APK firmado |
| `docs/` | Lista de pruebas manuales |
| `assets/` | Ícono de la app (`icono.svg`). Para regenerar los íconos de Windows y Android: `powershell -ExecutionPolicy Bypass -File installer\crear-iconos.ps1` |

## 1. Configurar Supabase (una sola vez)

1. Crea un proyecto en <https://supabase.com>.
2. **SQL Editor** → ejecuta, en este orden, el contenido de:
   `supabase/01_tabla_pagos.sql`, `supabase/02_rls_realtime.sql`, `supabase/04_es_prueba.sql` y, para verificar la
   seguridad, `supabase/99_pruebas_rls.sql`.
3. **Authentication → Sign In / Providers**: deja **Allow new users to sign up** encendido y apaga **Confirm email**
   (ver `supabase/03_admin_y_auth.md`). La cuenta se crea desde la app Android con "Crear cuenta".
4. **Project Settings → API Keys**: copia la **URL** y la clave **publishable/anon**.
   **Nunca** uses la `service_role`/`secret` en ninguna app.

> La PC y el celular deben iniciar sesión con **la misma cuenta**: cada cuenta solo ve sus propios pagos.

**Si "Crear cuenta" no funciona**, revisa en *Authentication → Sign In / Providers*:
- el proveedor **Email** debe estar **habilitado** (*Enable email provider*);
- **Allow new users to sign up** debe estar encendido;
- **Confirm email** debe estar apagado (si no, la cuenta se crea pero no puede entrar hasta confirmar un correo).

Alternativa: crea el usuario a mano en *Authentication → Users → Add user* marcando *Auto Confirm User*.

## 2. Configuración de las apps

Copia `appsettings.example.json` como `appsettings.json` y completa la URL y la clave anon en **ambos** sitios
(estos archivos no se suben a git):

- `src/PagoAlerta.Desktop/appsettings.json`
- `src/PagoAlerta.Android/Assets/appsettings.json`

## 3. Compilar

Requisitos: SDK de .NET 11, workload `android` (`dotnet workload install android`), JDK 21 y Android SDK API 37
(`dotnet build src/PagoAlerta.Android -t:InstallAndroidDependencies -f net11.0-android -p:AcceptAndroidSDKLicenses=True`).
Si tus rutas de SDK/JDK no son las predeterminadas, crea `src/PagoAlerta.Android/Android.local.props`
(ignorado por git) con `AndroidSdkDirectory` y `JavaSdkDirectory`.

```powershell
dotnet test  tests/PagoAlerta.Core.Tests          # pruebas unitarias
dotnet run   --project src/PagoAlerta.Desktop     # app de escritorio
dotnet build src/PagoAlerta.Android               # app Android (depuración)
```

## 4. Instalador de Windows (Setup.exe)

Requiere [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
powershell -ExecutionPolicy Bypass -File installer\crear-instalador.ps1
```

Resultado: `installer\Output\PagoAlerta-Setup-<versión>.exe`. Instala sin pedir .NET, crea acceso directo en el menú
Inicio (y opcionalmente en el escritorio) y se desinstala desde "Aplicaciones" borrando también sus datos locales.

## 5. APK firmado para el celular

```powershell
powershell -ExecutionPolicy Bypass -File installer\crear-apk.ps1            # celulares ARM + emuladores
powershell -ExecutionPolicy Bypass -File installer\crear-apk.ps1 -Abi x64   # solo emuladores (BlueStacks), más liviano
```

Resultado en `dist\`. La primera vez se crea la **keystore** en `%USERPROFILE%\PagoAlerta-keys\`
(fuera del repositorio). **Respalda esa carpeta**: si se pierde no podrás actualizar la app ya instalada.

### Instalar el APK en el celular
1. Copia `PagoAlerta-<versión>.apk` al celular (cable, WhatsApp, Drive…).
2. Ábrelo y permite "Instalar apps desconocidas" para la app desde la que lo abres.
3. Abre **PagoAlerta** → **Crear cuenta** (o Ingresar).
4. Entra en **Permisos y batería** y sigue los 3 pasos: *Acceso a notificaciones*, *Ahorro de batería* y los ajustes
   de tu marca. En Android 13+ si el interruptor aparece bloqueado: Info. de la app → ⋮ → *Permitir ajustes restringidos*.
5. Toca **Enviar mensaje de prueba** y elige la app (**Yape**, **Plin**, **BBVA** o al azar): debe aparecer en la PC con esa app y la etiqueta PRUEBA. La prueba pasa por el mismo parser que un pago real, así que también sirve para comprobar las reglas de cada app.

En BlueStacks arrastra el APK a la ventana (o `Ctrl+Shift+B`).

## 6. Agregar una app nueva o una regla del parser

Las reglas están en `src/PagoAlerta.Core/Parsing/parser-rules.json` (no hay que tocar código):

```json
{
  "app": "NombreApp",
  "paquetes": [ "com.empresa.app" ],
  "tipo": "recibido",
  "patron": "(?<nombre>.+?)\\s+te envió\\s+(?<monto>S/\\s*[\\d.,]+)"
}
```

- `patron` es una expresión regular (.NET, sin distinguir mayúsculas). Obligatorio el grupo `(?<monto>…)`;
  opcional `(?<nombre>…)`.
- `tipo`: `recibido`, `enviado` u `otro`. `buscarEn`: `texto` (por defecto) o `ambos` (título + texto).
- `provisional: true` marca reglas aún sin un texto real de la tienda.
- Para saber el **nombre de paquete** real de una app, abre en el celular *Apps a monitorear → Notificaciones
  recientes* y toca el paquete para agregarlo.
- Después de editar, ejecuta `dotnet test` (hay pruebas con los textos reales de Yape y BBVA) y agrega la tuya en
  `tests/PagoAlerta.Core.Tests/NotificationParserTests.cs`.

## 6b. Segundo plano y avisos

- **PC:** al cerrar la ventana con la X, PagoAlerta **sigue funcionando junto al reloj de Windows** y te avisa de cada pago con
  un **recuadro emergente** en la esquina inferior derecha (como las notificaciones de Chrome: monto, nombre, app y barra de tiempo) y un sonido. El recuadro no roba el foco, se cierra solo a los 8 s (con el mouse encima se detiene) y al tocarlo abre la app. Para cerrarla del todo: clic derecho en el ícono → **Salir**. Solo se permite una copia; abrirla
  otra vez muestra la que ya corre. En *Ajustes* puedes apagar el sonido y el recuadro, o activar **Iniciar con Windows**.
- **Celular:** al detectar un pago muestra una notificación **"Pago recibido · S/ … · Nombre (App)"** con sonido y vibración.
  Se puede apagar en *Cuenta → Avisar cuando llegue un pago*. En Android 13+ hay que permitir las notificaciones de la app.
  Si prefieres no duplicar el aviso de Yape/BBVA, apágalo ahí.

## 7. Registros de errores

- Windows: `%LocalAppData%\PagoAlerta\logs\pagoalerta.log`
- Android: pantalla *Estado → Ver registro de errores*

Nunca guardan nombres, montos ni texto de las notificaciones; solo eventos y tipos de error.

## 8. Seguridad

- Las apps solo llevan la URL y la clave anon. Cada cuenta solo puede leer e insertar **sus** filas (RLS).
- Las apps no pueden modificar pagos ni borrar pagos reales; solo se pueden borrar los de prueba (`es_prueba = true`).
- La sesión se guarda cifrada (DPAPI en Windows, Keystore en Android). La contraseña nunca se guarda.
- La `service_role`, la contraseña de la base de datos y la keystore **no** van en el repositorio.
