# PagoAlerta — Notificador de Pagos (PC + Android)

## Contexto
App para UNA tienda. El celular Android lee notificaciones de pagos (Yape, Plin, BBVA...) y las envía a Supabase. La app de escritorio (Windows) inicia sesión con el mismo admin y muestra los pagos (fecha, hora, nombre completo, monto). No depende de la misma red Wi-Fi.

## Decisiones
- C#, .NET 11 (C# 15). Si el workload de Android no soporta .NET 11, usar .NET 10 solo para ese proyecto y avisar. NO usar .NET 8 ni 9.
- Solución: Core (modelos, parser, cliente Supabase), Desktop (Windows Forms), Android (.NET for Android, sin MAUI).
- Backend: Supabase (PostgreSQL + Auth + Realtime). Registro ABIERTO por decisión del dueño: la cuenta se crea desde la app Android; PC y celular deben usar la MISMA cuenta (RLS: cada cuenta solo ve sus pagos).
- Pagos de prueba (`es_prueba`): no suman en totales ni se exportan; se pueden borrar (única excepción al "sin DELETE").
- Android: cola local SQLite + servicio en primer plano + receptor de arranque. Escritorio: Setup.exe con Inno Setup. APK firmado con keystore fuera del repo (`%USERPROFILE%\PagoAlerta-keys`).
- Zona horaria de visualización: America/Lima. Moneda por defecto: PEN (S/). BD en UTC (timestamptz).
- En las apps SOLO URL del proyecto y clave anon/publishable. NUNCA service_role.
- Nombre de la app ("PagoAlerta") en una sola constante/recurso. No copiar logos ni marca de Yape ni de bancos.

## Diseño (azul, estilo Yape)
Azul primario #1F4FE0 · oscuro #0B2A8F · claro #EAF0FF · acento cian #19C6E0 · fondo #F5F7FB · tarjetas #FFFFFF · texto #1B1F2A / secundario #6B7280 · recibido verde #16A34A · enviado gris oscuro · error #DC2626.
Segoe UI (Windows), Roboto (Android). Radio 16 tarjetas, 12 botones/campos. Montos en negrita y grandes. Cada pago: círculo de iniciales (fondo azul claro), nombre, app + hora, monto a la derecha.

## Modelo de datos
Tabla `pagos` con RLS: solo SELECT e INSERT del dueño (user_id = auth.uid()); sin UPDATE/DELETE; UNIQUE(user_id, hash); índice (user_id, fecha_hora DESC); Realtime habilitado. Ver `supabase/`.

## Flujo de trabajo
- Un sprint = una rama (`sprint-1` ... `sprint-4`), cada una parte de la anterior.
- Antes de escribir código de cada sprint: mostrar el plan y esperar confirmación.
- Al terminar cada sprint: explicar exactamente cómo probarlo.
- No hacer push sin que el usuario lo pida.

## Ejemplos de notificaciones (provisionales hasta tener textos reales de la tienda)
- Yape: "Confirmación de pago" / "Amanda Rod* te envió un pago por S/25. El cód. de seguridad es:004"
- BBVA (ejemplo de México): "Abono a tu cuenta *29400 de $1,324.00" / "Cargo a tu cuenta *9400 de $1,324.00" (sin nombre)
- Plin: sin texto real aún.

---









<!-- cloude-code-toolbox:mcp-skills-awareness-begin -->

### MCP & Skills awareness (Claude Code ToolBox)

_Last synced: 2026-11-22T04:33:25.850Z._

- **Full report:** `.claude/cloude-code-toolbox-mcp-skills-awareness.md` in this workspace (auto-overwritten on each scan). Use it as ground truth for configured servers and skill folders.
- **MCP:** For **live tools** in Claude Code, enable the matching server via `/mcp`. Servers are configured in `~/.claude.json` (user) and `.mcp.json` (project).
- **When the user’s task matches a server** (e.g. Confluence work and a **Confluence** / **Atlassian** MCP is listed), **prefer that server id** and plan on tool use—not only file search.
- **Skills:** Folders below contain `SKILL.md`; attach or cite paths in chat when relevant.

#### Workspace MCP

- `c:\Users\User\Downloads\NotiPagos\.mcp.json` _(workspace: NotiPagos)_ — _file missing_

_No active workspace servers in mcp.json._

#### User MCP

- `C:\Users\User\.claude.json` — _no servers defined_

_No active user-scoped servers in mcp.json._

#### Project skills

_None found (or no workspace open)._

#### User skills

_None found._

<!-- cloude-code-toolbox:mcp-skills-awareness-end -->
