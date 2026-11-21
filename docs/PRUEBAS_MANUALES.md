# Pruebas manuales (HT-18)

Marca cada caso con ✅/❌ y anota la fecha. Antes de empezar: PC y celular con la **misma cuenta**, permiso de
notificaciones activo en el celular y la app de escritorio abierta.

## A. Flujo completo

| # | Caso | Pasos | Resultado esperado |
|---|---|---|---|
| A1 | Mensaje de prueba (HU-20) | Celular → Estado → **Enviar mensaje de prueba** → elegir **Yape**, **Plin**, **BBVA** o al azar | En la PC aparece en pocos segundos con la app elegida y etiqueta **PRUEBA**, y no suma al "Total de hoy" |
| A1b | Prueba desde la PC | PC → **⋯ Más → Enviar prueba → Como Yape / Plin / BBVA** | La fila aparece con esa app y la etiqueta PRUEBA |
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

## E. Diseño (sprint 5)

| # | Caso | Resultado esperado |
|---|---|---|
| E1 | Android: abrir la app con sesión guardada | Entra directo a **Inicio** (resumen del día y movimientos recientes) |
| E2 | Android: cambiar entre **Inicio · Estado · Cuenta** | Transición suave; la pestaña activa se marca en azul |
| E3 | Android: **Inicio** con un pago nuevo | El total cuenta hasta el nuevo valor y aparece el aviso "Nuevo movimiento" |
| E4 | Android: **Ver todos** | Lista completa con "Cargar más"; el interruptor oculta/muestra las pruebas |
| E5 | Android: **Cuenta → interruptores de apps** | Desmarcar una app deja de leer sus notificaciones |
| E6 | Android: **Permisos y batería** | Los 4 pasos cambian de PENDIENTE a LISTO al activar cada permiso |
| E7 | Android: login con datos incorrectos | El formulario se sacude y muestra el error |
| E8 | PC: llega un pago en vivo | Fila nueva con fundido y etiqueta **NUEVO**; el punto "En vivo" pulsa |
| E9 | PC: filtros en chips, buscador y "Ver pruebas" | Igual que antes; "⋯ Más" contiene Limpiar, Enviar prueba, Borrar pruebas y Ajustes |
| E10 | PC: seleccionar una fila (clic o flechas ↑↓) | El panel de detalle de la derecha cambia con un fundido |
## F. Segundo plano y avisos (sprint 6)

| # | Caso | Resultado esperado |
|---|---|---|
| F1 | PC: cerrar la ventana con la **X** | La ventana se oculta, aparece el globo "Sigue funcionando en segundo plano" y el ícono queda junto al reloj |
| F2 | PC: llega un pago con la ventana oculta | Suena y aparece un **recuadro en la esquina inferior derecha** ("Pago recibido", monto en verde, nombre y app); no roba el foco, se cierra solo y al tocarlo se abre la app |
| F3 | PC: llega un pago con la ventana a la vista | Suena (si el sonido está activo) y la fila nueva sale resaltada, sin globo |
| F4 | PC: abrir PagoAlerta otra vez (acceso directo) | No abre una segunda copia: muestra la que ya está corriendo |
| F5 | PC: ícono de la bandeja → **Salir** | La app se cierra del todo y deja de avisar |
| F6 | PC: Ajustes → apagar "Sonido" / "Aviso de Windows" | Deja de sonar / de mostrar el recuadro |
| F6b | PC: llegan 4 pagos seguidos | Se apilan hasta 3 recuadros (el más nuevo abajo); el más antiguo se desvanece |
| F7 | PC: Ajustes → "Iniciar PagoAlerta con Windows" y reiniciar | Arranca oculta junto al reloj (sin pedir login si había sesión guardada) |
| F8 | Celular: llega un pago real | Notificación **"Pago recibido · S/ …"** con sonido y vibración; al tocarla abre la app |
| F9 | Celular: la misma notificación repetida por Android | Avisa **una sola vez** |
| F10 | Celular: Cuenta → apagar "Avisar cuando llegue un pago" | Se sigue enviando a la nube, pero ya no suena ni vibra |
| F11 | Celular: Cuenta → "Probar aviso" y "Enviar mensaje de prueba" | Ambos muestran la notificación de prueba con sonido |
| F12 | Celular (Android 13+): permiso de notificaciones denegado | La app avisa en pantalla; los avisos no se muestran hasta permitirlo |