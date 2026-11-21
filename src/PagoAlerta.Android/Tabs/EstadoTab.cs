using Android.Graphics;
using Android.Graphics.Drawables;
using PagoAlerta.Core;
using PagoAlerta.Core.Logging;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil.Tabs;

/// <summary>
/// Estado del servicio: interruptor "Monitoreo activo", ultimo envio, pendientes, lista de verificacion
/// (permiso, nube, bateria) y el mensaje de prueba (HU-20). Misma logica de siempre, nuevo diseno.
/// </summary>
public class EstadoTab : ITab
{
    private readonly PrincipalActivity _a;
    private readonly ScrollView _vista;
    private Switch _interruptor = null!;
    private TextView _estadoTexto = null!, _ultimoEnvio = null!, _pendientes = null!, _ultimoError = null!, _resultado = null!;
    private View _puntoEstado = null!;
    private LinearLayout _checklist = null!;
    private Button _btnPrueba = null!;
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly Java.Lang.IRunnable _tick;
    private bool _construyendo, _visible, _servicioIniciado;

    public View Vista => _vista;

    public EstadoTab(PrincipalActivity a)
    {
        _a = a;
        _vista = new ScrollView(a) { FillViewport = true, VerticalScrollBarEnabled = false };
        _tick = new Java.Lang.Runnable(() => { if (!_visible) return; Refrescar(); _handler.PostDelayed(_tick, 3000); });
        Construir();
    }

    private void Construir()
    {
        var c = _a;
        var col = new LinearLayout(c) { Orientation = Orientation.Vertical };
        var pad = Vistas.Px(c, 20);
        col.SetPadding(pad, Vistas.Px(c, 14), pad, Vistas.Px(c, 20));

        col.AddView(Vistas.Etiqueta(c, "Detección de pagos", 16, Colores.TextoSecundario));
        col.AddView(Vistas.Etiqueta(c, "Estado del servicio", 30, Colores.PrimarioOscuro, negrita: true), Vistas.Margen(c, 2, 18));

        // ---- Tarjeta azul: monitoreo ----
        var t1 = Vistas.Tarjeta(c, oscura: true, padDp: 20);
        var cab = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        cab.SetGravity(GravityFlags.CenterVertical);
        cab.AddView(Vistas.Etiqueta(c, "Monitoreo activo", 22, Color.White, negrita: true),
            new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        _interruptor = Vistas.Interruptor(c, true, Colores.Acento);
        _interruptor.ScaleX = _interruptor.ScaleY = 1.25f;
        _interruptor.CheckedChange += AlCambiarInterruptor;
        cab.AddView(_interruptor);
        t1.AddView(cab);

        var filaEstado = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        filaEstado.SetGravity(GravityFlags.CenterVertical);
        _puntoEstado = new View(c);
        filaEstado.AddView(_puntoEstado, new LinearLayout.LayoutParams(Vistas.Px(c, 11), Vistas.Px(c, 11)));
        _estadoTexto = Vistas.Etiqueta(c, "", 15, Color.ParseColor("#FCD9A0"));
        filaEstado.AddView(_estadoTexto, Vistas.Margen(c, 0, 0, 10, 0, ViewGroup.LayoutParams.WrapContent));
        t1.AddView(filaEstado, Vistas.Margen(c, 8, 16));

        var interior = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        interior.Background = Vistas.Forma(c, Color.White, 18);
        interior.SetPadding(Vistas.Px(c, 12), Vistas.Px(c, 16), Vistas.Px(c, 12), Vistas.Px(c, 16));
        _ultimoEnvio = Vistas.Etiqueta(c, "—", 22, Colores.PrimarioOscuro, negrita: true);
        _pendientes = Vistas.Etiqueta(c, "0", 22, Colores.PrimarioOscuro, negrita: true);
        interior.AddView(Metrica(c, "Último envío", _ultimoEnvio), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        var div = new View(c); div.SetBackgroundColor(Colores.BordeSuave);
        interior.AddView(div, new LinearLayout.LayoutParams(Vistas.Px(c, 1), ViewGroup.LayoutParams.MatchParent));
        interior.AddView(Metrica(c, "Pendientes de enviar", _pendientes), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        t1.AddView(interior);

        _ultimoError = Vistas.Etiqueta(c, "", 13, Color.ParseColor("#FFB4B4"));
        _ultimoError.Visibility = ViewStates.Gone;
        t1.AddView(_ultimoError, Vistas.Margen(c, 12));
        col.AddView(t1, Vistas.Margen(c, 0, 16));

        // ---- Lista de verificacion ----
        var t2 = Vistas.Tarjeta(c, padDp: 20);
        t2.AddView(Vistas.Etiqueta(c, "Lista de verificación", 20, Colores.Texto, negrita: true), Vistas.Margen(c, 0, 6));
        _checklist = new LinearLayout(c) { Orientation = Orientation.Vertical };
        t2.AddView(_checklist);
        col.AddView(t2, Vistas.Margen(c, 0, 16));

        // ---- Mensaje de prueba ----
        var t3 = Vistas.Tarjeta(c, padDp: 20);
        t3.AddView(Vistas.Etiqueta(c, "Mensaje de prueba", 20, Colores.Texto, negrita: true));
        var desc = Vistas.Etiqueta(c,
            "Elige una app (Yape, Plin o BBVA) y envía un pago simulado de S/ 1.00 con su aspecto real. Aparecerá en la PC con la etiqueta PRUEBA.",
            14, Colores.TextoSecundario);
        desc.SetLineSpacing(0, 1.15f);
        t3.AddView(desc, Vistas.Margen(c, 6, 16));
        _btnPrueba = Vistas.Boton(c, "Enviar mensaje de prueba", EstiloBoton.Primario,
            () => ElegirAppPrueba.Mostrar(_a, async app => await ProbarAsync(app)));
        ConIcono(_btnPrueba, Resource.Drawable.ic_send, Color.White);
        t3.AddView(_btnPrueba);
        _resultado = Vistas.Etiqueta(c, "", 14, Colores.TextoSecundario);
        _resultado.SetLineSpacing(0, 1.15f);
        _resultado.Visibility = ViewStates.Gone;
        t3.AddView(_resultado, Vistas.Margen(c, 12));

        var botones = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        var b1 = Vistas.Boton(c, "Reintentar envío", EstiloBoton.Contorno, async () => await ReintentarAsync(), compacto: true);
        var b2 = Vistas.Boton(c, "Ver errores", EstiloBoton.Contorno,
            () => _a.StartActivity(new Intent(_a, typeof(RegistroActivity))), compacto: true);
        var lp1 = Vistas.Margen(c, 0, 0, 0, 6, 0); lp1.Weight = 1f;
        var lp2 = Vistas.Margen(c, 0, 0, 6, 0, 0); lp2.Weight = 1f;
        botones.AddView(b1, lp1); botones.AddView(b2, lp2);
        t3.AddView(botones, Vistas.Margen(c, 14));
        col.AddView(t3, Vistas.Margen(c, 0, 8));

        _vista.AddView(col);
    }

    private static void ConIcono(Button b, int recurso, Color color)
    {
        var d = b.Context!.GetDrawable(recurso)!.Mutate();
        d.SetTint(color);
        var px = Vistas.Px(b.Context, 22);
        d.SetBounds(0, 0, px, px);
        b.SetCompoundDrawables(d, null, null, null);
        b.CompoundDrawablePadding = Vistas.Px(b.Context, 10);
    }

    private static LinearLayout Metrica(Context c, string titulo, TextView valor)
    {
        var l = new LinearLayout(c) { Orientation = Orientation.Vertical };
        l.SetGravity(GravityFlags.Center);
        var t = Vistas.Etiqueta(c, titulo, 13, Colores.TextoSecundario);
        t.Gravity = GravityFlags.Center;
        l.AddView(t);
        valor.Gravity = GravityFlags.Center;
        l.AddView(valor, Vistas.Margen(c, 4));
        return l;
    }

    // ---------------- Ciclo de vida ----------------
    public void AlMostrar()
    {
        _visible = true;
        Refrescar();
        Anim.Entrar(_vista.GetChildAt(0)!, 0, 10);
        _handler.RemoveCallbacks(_tick);
        _handler.PostDelayed(_tick, 3000);
    }

    public void AlOcultar()
    {
        _visible = false;
        _handler.RemoveCallbacks(_tick);
    }

    // ---------------- Refresco ----------------
    private void Refrescar()
    {
        var permiso = Servicios.ListenerHabilitado(_a);
        var activo = Servicios.Prefs.MonitoreoActivo;

        _construyendo = true;
        _interruptor.Checked = activo;
        _construyendo = false;

        // HT-08: el servicio en primer plano vive mientras el monitoreo este activo.
        var debeCorrer = activo && permiso;
        if (debeCorrer != _servicioIniciado)
        {
            _servicioIniciado = debeCorrer;
            if (debeCorrer) MonitorService.Iniciar(_a); else MonitorService.Detener(_a);
        }

        (string texto, Color color) = !permiso ? ("Esperando permiso de notificaciones", Colores.AmbarPunto)
                                    : activo ? ("Activo: detectando pagos", Color.ParseColor("#34D399"))
                                    : ("Inactivo", Color.ParseColor("#9CA3AF"));
        _estadoTexto.Text = texto;
        var punto = new GradientDrawable();
        punto.SetShape(ShapeType.Oval);
        punto.SetColor(color);
        _puntoEstado.Background = punto;

        var ms = Servicios.Prefs.UltimoEnvioMs;
        _ultimoEnvio.Text = ms == 0 ? "—" : Formato.Hora(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime);

        var pend = Servicios.Pendientes;
        _pendientes.Text = pend.ToString();
        _pendientes.SetTextColor(pend > 0 ? Colores.Ambar : Colores.PrimarioOscuro);

        var err = Servicios.Prefs.UltimoError;
        _ultimoError.Text = err ?? "";
        _ultimoError.Visibility = string.IsNullOrEmpty(err) ? ViewStates.Gone : ViewStates.Visible;

        PintarChecklist(permiso);
    }

    private string _firmaChecklist = "";

    private void PintarChecklist(bool permiso)
    {
        var nube = Servicios.Pago?.TieneSesion == true;
        var bateria = Servicios.IgnoraBateria(_a);
        var correo = Servicios.Pago?.CorreoActual;
        var firma = $"{permiso}|{nube}|{bateria}|{correo}";
        if (firma == _firmaChecklist) return;
        _firmaChecklist = firma;

        _checklist.RemoveAllViews();
        FilaCheck(permiso, "Acceso a notificaciones",
            permiso ? "Activado: la app puede leer tus pagos" : "Pendiente: la app no puede leer tus pagos",
            permiso ? null : "Activar", EstiloBoton.Primario, () => _a.StartActivity(new Intent(_a, typeof(PermisosActivity))));
        _checklist.AddView(Vistas.Separador(_a));
        FilaCheck(nube, "Conexión con la nube",
            nube ? $"Sesión iniciada como {Nombre(correo)}" : "Sin sesión activa", null, EstiloBoton.Suave, () => { });
        _checklist.AddView(Vistas.Separador(_a));
        FilaCheck(bateria, "Batería sin restricciones",
            bateria ? "Android no cerrará la app" : "Evita que Android cierre la app",
            bateria ? null : "Ver", EstiloBoton.Suave, () => _a.StartActivity(new Intent(_a, typeof(PermisosActivity))));
    }

    private static string Nombre(string? correo) => string.IsNullOrWhiteSpace(correo) ? "admin" : correo.Split('@')[0];

    private void FilaCheck(bool ok, string titulo, string subtitulo, string? accion, EstiloBoton estilo, Action alClic)
    {
        var c = _a;
        var fila = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        fila.SetGravity(GravityFlags.CenterVertical);
        fila.SetPadding(0, Vistas.Px(c, 14), 0, Vistas.Px(c, 14));

        var circ = new FrameLayout(c);
        circ.Background = Vistas.Forma(c, ok ? Colores.VerdeClaro : Colores.AmbarClaro, 40);
        var ico = Vistas.Icono(c, ok ? Resource.Drawable.ic_check : Resource.Drawable.ic_alert, ok ? Colores.Verde : Colores.Ambar, 20);
        circ.AddView(ico, new FrameLayout.LayoutParams(Vistas.Px(c, 20), Vistas.Px(c, 20), GravityFlags.Center));
        fila.AddView(circ, Vistas.Margen(c, 0, 0, 0, 14, Vistas.Px(c, 42), Vistas.Px(c, 42)));

        var textos = new LinearLayout(c) { Orientation = Orientation.Vertical };
        textos.AddView(Vistas.Etiqueta(c, titulo, 16, Colores.Texto, negrita: true));
        var s = Vistas.Etiqueta(c, subtitulo, 13, Colores.TextoSecundario);
        s.SetLineSpacing(0, 1.1f);
        textos.AddView(s, Vistas.Margen(c, 2));
        fila.AddView(textos, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

        if (accion is not null)
            fila.AddView(Vistas.Boton(c, accion, estilo, alClic, compacto: true), Vistas.Margen(c, 0, 0, 10, 0, ViewGroup.LayoutParams.WrapContent));
        _checklist.AddView(fila);
        Anim.Rebotar(circ, 40);
    }

    // ---------------- Acciones ----------------
    private void AlCambiarInterruptor(object? s, CompoundButton.CheckedChangeEventArgs e)
    {
        if (_construyendo) return;
        Servicios.Prefs.MonitoreoActivo = e.IsChecked;
        Registro.Info($"Monitoreo {(e.IsChecked ? "activado" : "desactivado")}");
        Refrescar();
    }

    private async Task ProbarAsync(string? app)
    {
        _btnPrueba.Enabled = false;
        _resultado.Visibility = ViewStates.Visible;
        _resultado.SetTextColor(Colores.TextoSecundario);
        _resultado.Text = "Enviando…";

        var r = await Servicios.EnviarPruebaAsync(app);
        _btnPrueba.Enabled = true;

        if (r.Ok)
        {
            var permiso = Servicios.ListenerHabilitado(_a);
            _resultado.SetTextColor(permiso ? Colores.Verde : Colores.Ambar);
            _resultado.Text = "Prueba enviada. Revísala en la PC (etiqueta PRUEBA)." +
                              (permiso ? "" : "\nOjo: el permiso de notificaciones está desactivado, así que los pagos reales no se detectarán.");
            _a.Mensaje("Mensaje de prueba enviado");
        }
        else
        {
            _resultado.SetTextColor(Colores.Error);
            _resultado.Text = r.Mensaje;
            Anim.Temblar(_btnPrueba);
            _a.Mensaje(r.Mensaje, error: true, largo: true);
            if (r.Resultado == ResultadoEnvio.SesionVencida) _a.IrALogin(cerrarSesion: false);
        }
        Refrescar();
    }

    private async Task ReintentarAsync()
    {
        _a.Mensaje("Reintentando…");
        var r = await Servicios.ProcesarColaAsync(volvioLaRed: true);
        Refrescar();
        _a.Mensaje(r.Pendientes == 0 ? "Todo enviado" : $"Quedan {r.Pendientes} pendientes", error: r.Pendientes > 0);
    }
}
