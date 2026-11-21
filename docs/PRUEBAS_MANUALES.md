# Pruebas manuales (HT-18)

Marca cada caso con ✅/❌ y anota la fecha. Antes de empezar: PC y celular con la **misma cuenta**, permiso de
notificaciones activo en el celular y la app de escritorio abierta.

## A. Flujo completo

| # | Caso | Pasos | Resultado esperado |
|---|---|---|---|
| A1 | Mensaje de prueba (HU-20) | Celular → Estado → **Enviar mensaje de prueba** | En la PC aparece en pocos segundos con etiqueta **PRUEBA** y no suma al "Total de hoy" |
| A2 | Prueba sin permiso | Desactiva "Acceso a notificaciones" y envía la prueba | Se envía y la app avisa que el permiso está desactivado |
| A3 | Pago real | Recibe un pago real (Yape/Plin/BBVA) | Aparece en la PC con nombre, monto, app y hora de Lima; suma al total |
| A4 | Notificación duplicada | Android repite/actualiza la misma notificación | Solo hay **una** fila en la tabla |
| A5 | App no monitoreada | Desmarca una app en *Apps a monitorear* y recibe su notificación | Se ignora (no llega a la PC) |
| A6 | Notificación que no es pago | Recibe una promoción de Yape | Se ignora |

## B. Robustez (HU-14, HU-17)

| # | Caso | Pasos | Resultado esperado |
|---|---|---|---|
| B1 | App cerrada | Cierra la app deslizándola de recientes y recibe un pago | Llega a la PC (la notificación persistente sigue visible) |
| B2 | **Sin internet** | Activa modo avión → recibe/envía un pago → desactiva modo avión | *Pendientes* sube a 1; al volver la red llega **una sola vez** y *Pendientes* vuelve a 0 |
| B3 | Varios pagos sin internet | Modo avión → 3 pagos → volver la red | Llegan los 3, **en orden**, sin duplicados |
| B4 | **Celular reiniciado** | Reinicia el celular sin abrir PagoAlerta → recibe un pago | La notificación persistente reaparece sola y el pago llega a la PC |
| B5 | **PC apagada y luego encendida** | Apaga la PC, recibe pagos en el celular, enciende y abre la app | Los pagos se ven en la lista (consulta de respaldo / carga inicial) |
| B6 | Sesión vencida | Cierra sesión en la PC y vuelve a entrar | Pide iniciar sesión; no pierde pagos |
| B7 | Ahorro de batería | Sigue los 3 pasos de *Permisos y batería* y deja el celular 1 h con pantalla apagada | Un pago nuevo sigue llegando |

## C. Escritorio

| # | Caso | Resultado esperado |
|---|---|---|
| C1 | Instalar `Setup.exe` en una PC **limpia** (sin .NET) | Instala, crea acceso directo y abre |
| C2 | Iniciar sesión / "Recordar sesión" | Cierra y abre: entra directo; sin recordar pide de nuevo |
| C3 | Filtros (hoy, ayer, rango, app, nombre con y sin tildes) | Listado y total correctos; "Limpiar" restablece |
| C4 | **Exportar** a Excel y a CSV con filtros activos | Columnas Fecha, Hora, Nombre, Monto, App; tildes correctas; **sin pruebas** |
| C5 | Borrar pruebas | Solo desaparecen las de prueba; los pagos reales quedan |
| C6 | Desinstalar desde *Aplicaciones* | Se elimina el programa y `%LocalAppData%\PagoAlerta` |

## D. Seguridad

| # | Caso | Resultado esperado |
|---|---|---|
| D1 | Ejecutar `supabase/99_pruebas_rls.sql` | `anon` no puede leer ni insertar |
| D2 | Iniciar sesión con otra cuenta | No ve pagos de la primera cuenta |
| D3 | Revisar `git log -p` y el APK/instalador | No hay `service_role` ni contraseñas |
