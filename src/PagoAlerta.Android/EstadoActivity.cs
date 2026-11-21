using Android.Graphics;
using PagoAlerta.Core;
using PagoAlerta.Core.Logging;
using PagoAlerta.Core.Time;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil;

/// <summary>
/// Pantalla Estado: interruptor "Monitoreo activo", indicador, ultimo envio, pendientes y el boton
/// "Enviar mensaje de prueba" (HU-20).
/// </summary>
[Activity(Label = "@string/app_name", Exported = false, Theme = "@android:style/Theme.DeviceDefault.Light.NoActionBar")]
public class EstadoActivity : PantallaBase
{
    private Switch _interruptor = null!;
    private TextView _indicador = null!, _ultimoEnvio = null!, _pendientes = null!, _ultimoError = null!, _resultadoPrueba = null!;
    private View _puntoEstado = null!;
    private LinearLayout _avisoPermiso = null!;
    private Button _btnPrueba = null!;
    private Handler? _handler;
    private Java.Lang.IRunnable _tick = null!;
    private bool _construyendo, _servicioIniciado;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Construir("Estado", Servicios.Pago?.CorreoActual);
        var c = Contenido;

        // Aviso de permiso
        _avisoPermiso = Vistas.Tarjeta(this);
        _avisoPermiso.Background = Vistas.Forma(this, Color.ParseColor("#FFF7ED"), Vistas.RadioTarjeta, Colores.Ambar);
        _avisoPermiso.AddView(Vistas.Etiqueta(this, "Falta el permiso de notificaciones", 16, Colores.Ambar, negrita: true));
        _avisoPermiso.AddView(Vistas.Etiqueta(this,
            "Sin \"Acceso a notificaciones\" la app no puede detectar tus pagos.", 14, Colores.Texto), Vistas.Margen(this, 4, 10));
        _avisoPermiso.AddView(Vistas.Boton(this, "Activar permiso", true,
            () => StartActivity(new Intent(this, typeof(PermisosActivity)))));
        c.AddView(_avisoPermiso, Vistas.Margen(this, 0, 14));

        // Tarjeta de monitoreo
        var t1 = Vistas.Tarjeta(this);
        var cab = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        cab.SetGravity(GravityFlags.CenterVertical);
        var tit = Vistas.Etiqueta(this, "Monitoreo activo", 18, Colores.Texto, negrita: true);
        cab.AddView(tit, new LinearLayout.LayoutParams(0, -2, 1f));
        _interruptor = new Switch(this);
        _interruptor.CheckedChange += AlCambiarInterruptor;
        cab.AddView(_interruptor);
        t1.AddView(cab);

        var filaEstado = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        filaEstado.SetGravity(GravityFlags.CenterVertical);
        _puntoEstado = new View(this);
        filaEstado.AddView(_puntoEstado, new LinearLayout.LayoutParams(Vistas.Px(this, 12), Vistas.Px(this, 12)));
        _indicador = Vistas.Etiqueta(this, "", 15, Colores.Texto, negrita: true);
        filaEstado.AddView(_indicador, Vistas.Margen(this, 0, 0, 8, 0, -2, -2));
        t1.AddView(filaEstado, Vistas.Margen(this, 8, 8));
        t1.AddView(Vistas.Separador(this));

        var (f1, v1) = Vistas.FilaDato(this, "Último envío", "—"); _ultimoEnvio = v1; t1.AddView(f1);
        t1.AddView(Vistas.Separador(this));
        var (f2, v2) = Vistas.FilaDato(this, "Pagos pendientes de enviar", "0"); _pendientes = v2; t1.AddView(f2);

        _ultimoError = Vistas.Etiqueta(this, "", 13, Colores.Error);
        _ultimoError.Visibility = ViewStates.Gone;
        t1.AddView(_ultimoError, Vistas.Margen(this, 6));
        c.AddView(t1, Vistas.Margen(this, 0, 14));

        // Prueba
        var t2 = Vistas.Tarjeta(this);
        t2.AddView(Vistas.Etiqueta(this, "Mensaje de prueba", 18, Colores.Texto, negrita: true));
        t2.AddView(Vistas.Etiqueta(this,
            "Envía un pago simulado de S/ 1.00 por el mismo camino que un pago real. Debe aparecer en la PC con la etiqueta PRUEBA.",
            14, Colores.TextoSecundario), Vistas.Margen(this, 4, 12));
        _btnPrueba = Vistas.Boton(this, "Enviar mensaje de prueba", true, async () => await ProbarAsync());
        t2.AddView(_btnPrueba);
        _resultadoPrueba = Vistas.Etiqueta(this, "", 14, Colores.Texto);
        _resultadoPrueba.Visibility = ViewStates.Gone;
        t2.AddView(_resultadoPrueba, Vistas.Margen(this, 10));
        c.AddView(t2, Vistas.Margen(this, 0, 14));

        // Cola y registro
        var t3 = Vistas.Tarjeta(this);
        t3.AddView(Vistas.Etiqueta(this, "Envíos pendientes", 18, Colores.Texto, negrita: true));
        t3.AddView(Vistas.Etiqueta(this,
            "Si no hay internet, los pagos se guardan en el celular y se envían solos, en orden, al volver la conexión.",
            14, Colores.TextoSecundario), Vistas.Margen(this, 4, 12));
        t3.AddView(Vistas.Boton(this, "Reintentar envío ahora", false, async () => await ReintentarAsync()), Vistas.Margen(this, 0, 10));
        t3.AddView(Vistas.Boton(this, "Ver registro de errores", false, MostrarRegistro));
        c.AddView(t3, Vistas.Margen(this, 0, 14));

        // Accesos
        c.AddView(Vistas.Boton(this, "Apps a monitorear", false,
            () => StartActivity(new Intent(this, typeof(AppsActivity)))), Vistas.Margen(this, 0, 10));
        c.AddView(Vistas.Boton(this, "Permisos y batería", false,
            () => StartActivity(new Intent(this, typeof(PermisosActivity)))), Vistas.Margen(this, 0, 10));
        c.AddView(Vistas.Boton(this, "Cerrar sesión", false, async () => await CerrarSesionAsync()), Vistas.Margen(this));

        _handler = new Handler(Looper.MainLooper!);
        _tick = new Java.Lang.Runnable(() => { Refrescar(); _handler?.PostDelayed(_tick, 3000); });

        // Android 13+: permiso para mostrar la notificacion persistente del monitoreo.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            CheckSelfPermission("android.permission.POST_NOTIFICATIONS") != Permission.Granted)
            RequestPermissions(new[] { "android.permission.POST_NOTIFICATIONS" }, 10);
    }

    private async Task ReintentarAsync()
    {
        Aviso("Reintentando…", false);
        var r = await Servicios.ProcesarColaAsync(volvioLaRed: true);
        Refrescar();
        Aviso(r.Pendientes == 0 ? "Todo enviado." : $"Quedan {r.Pendientes} pendientes.", false);
    }

    private void MostrarRegistro()
    {
        var ruta = Registro.RutaActual;
        var texto = "Aún no hay registros.";
        try
        {
            if (ruta is not null && File.Exists(ruta))
            {
                var lineas = File.ReadAllLines(ruta);
                texto = string.Join("\n", lineas.Skip(Math.Max(0, lineas.Length - 40)));
            }
        }
        catch (Exception ex) { texto = "No se pudo leer el registro: " + ex.Message; }

        new AlertDialog.Builder(this)!
            .SetTitle("Registro de errores")!
            .SetMessage(texto)!
            .SetPositiveButton("Cerrar", (IDialogInterfaceOnClickListener?)null)!
            .Show();
    }

    protected override void OnResume()
    {
        base.OnResume();
        Refrescar();
        _handler?.PostDelayed(_tick, 3000);
    }

    protected override void OnPause()
    {
        base.OnPause();
        _handler?.RemoveCallbacks(_tick);
    }

    private void Refrescar()
    {
        var permiso = Servicios.ListenerHabilitado(this);
        var activo = Servicios.Prefs.MonitoreoActivo;

        _construyendo = true;
        _interruptor.Checked = activo;
        _construyendo = false;

        _avisoPermiso.Visibility = permiso ? ViewStates.Gone : ViewStates.Visible;

        // HT-08: el servicio en primer plano vive mientras el monitoreo este activo.
        var debeCorrer = activo && permiso;
        if (debeCorrer != _servicioIniciado)
        {
            _servicioIniciado = debeCorrer;
            if (debeCorrer) MonitorService.Iniciar(this); else MonitorService.Detener(this);
        }

        var pend = Servicios.Pendientes;
        _pendientes.Text = pend.ToString();
        _pendientes.SetTextColor(pend > 0 ? Colores.Ambar : Colores.Texto);

        (string texto, Color color) = !permiso ? ("Sin permiso de notificaciones", Colores.Ambar)
                                    : activo ? ("Activo: detectando pagos", Colores.Verde)
                                    : ("Inactivo", Colores.TextoSecundario);
        _indicador.Text = texto;
        _indicador.SetTextColor(color);
        var punto = new Android.Graphics.Drawables.GradientDrawable();
        punto.SetShape(Android.Graphics.Drawables.ShapeType.Oval);
        punto.SetColor(color);
        _puntoEstado.Background = punto;

        var ms = Servicios.Prefs.UltimoEnvioMs;
        _ultimoEnvio.Text = ms == 0 ? "—" :
            $"{Formato.Fecha(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime)} {Formato.Hora(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime)}";

        var err = Servicios.Prefs.UltimoError;
        _ultimoError.Text = err ?? "";
        _ultimoError.Visibility = string.IsNullOrEmpty(err) ? ViewStates.Gone : ViewStates.Visible;
    }

    private void AlCambiarInterruptor(object? s, CompoundButton.CheckedChangeEventArgs e)
    {
        if (_construyendo) return;
        Servicios.Prefs.MonitoreoActivo = e.IsChecked;
        Refrescar();
    }

    private async Task ProbarAsync()
    {
        _btnPrueba.Enabled = false;
        _resultadoPrueba.Visibility = ViewStates.Visible;
        _resultadoPrueba.SetTextColor(Colores.TextoSecundario);
        _resultadoPrueba.Text = "Enviando…";

        var r = await Servicios.EnviarPruebaAsync();
        _btnPrueba.Enabled = true;

        if (r.Ok)
        {
            var aviso = Servicios.ListenerHabilitado(this)
                ? ""
                : "\nOjo: el permiso de notificaciones está desactivado, así que los pagos reales no se detectarán.";
            _resultadoPrueba.SetTextColor(Servicios.ListenerHabilitado(this) ? Colores.Verde : Colores.Ambar);
            _resultadoPrueba.Text = "Prueba enviada. Revísala en la PC (etiqueta PRUEBA)." + aviso;
        }
        else
        {
            _resultadoPrueba.SetTextColor(Colores.Error);
            _resultadoPrueba.Text = r.Mensaje;
            if (r.Resultado == ResultadoEnvio.SesionVencida) await IrALoginAsync();
        }
        Refrescar();
    }

    private async Task CerrarSesionAsync()
    {
        MonitorService.Detener(this);
        if (Servicios.Pago is not null) await Servicios.Pago.CerrarSesionAsync();
        await IrALoginAsync(cerrar: false);
    }

    private Task IrALoginAsync(bool cerrar = true)
    {
        var i = new Intent(this, typeof(LoginActivity));
        i.AddFlags(ActivityFlags.ClearTask | ActivityFlags.NewTask);
        StartActivity(i);
        Finish();
        return Task.CompletedTask;
    }
}

