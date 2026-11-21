using System.Globalization;
using Android.Graphics;
using PagoAlerta.Core;
using PagoAlerta.Core.Logging;
using PagoAlerta.Core.Models;
using PagoAlerta.Core.Services;
using PagoAlerta.Core.Time;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil.Tabs;

public interface ITab
{
    View Vista { get; }
    void AlMostrar();
    void AlOcultar();
}

/// <summary>
/// Inicio: saludo, resumen del dia (total cobrado y cantidad, sin pruebas), aviso de permiso y movimientos recientes.
/// Lee de la nube con las mismas consultas del Core que usa la app de escritorio.
/// </summary>
public class InicioTab : ITab
{
    private static readonly CultureInfo Es = new("es-PE");

    private readonly PrincipalActivity _a;
    private readonly ScrollView _vista;
    private TextView _tituloResumen = null!, _total = null!, _cantidad = null!;
    private LinearLayout _lista = null!;
    private View _avisoPermiso = null!;
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly Java.Lang.IRunnable _tick;
    private decimal _totalAnterior; private int _cantidadAnterior;
    private string _firmaLista = "";
    private bool _cargando, _visible;

    public View Vista => _vista;

    public InicioTab(PrincipalActivity a)
    {
        _a = a;
        _vista = new ScrollView(a) { FillViewport = true, VerticalScrollBarEnabled = false };
        _tick = new Java.Lang.Runnable(async () =>
        {
            if (!_visible) return;
            await CargarAsync();
            _handler.PostDelayed(_tick, 15000);
        });
        Construir();
    }

    private static string Nombre(string? correo)
    {
        var local = (correo ?? "").Split('@')[0].Replace('.', ' ').Replace('_', ' ').Trim();
        if (local.Length == 0) return "Mi tienda";
        return string.Join(' ', local.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => char.ToUpper(p[0], Es) + p[1..]));
    }

    private void Construir()
    {
        var c = _a;
        var col = new LinearLayout(c) { Orientation = Orientation.Vertical };
        var pad = Vistas.Px(c, 20);
        col.SetPadding(pad, Vistas.Px(c, 14), pad, Vistas.Px(c, 20));

        // ---- Encabezado: saludo + ADMIN + ayuda ----
        var cab = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        cab.SetGravity(GravityFlags.CenterVertical);
        var saludo = new LinearLayout(c) { Orientation = Orientation.Vertical };
        saludo.AddView(Vistas.Etiqueta(c, "Bienvenido,", 16, Colores.TextoSecundario));
        saludo.AddView(Vistas.Etiqueta(c, Nombre(Servicios.Pago?.CorreoActual), 30, Colores.PrimarioOscuro, negrita: true),
            Vistas.Margen(c, 2));
        cab.AddView(saludo, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        cab.AddView(Vistas.Pildora(c, "ADMIN", Colores.Primario, Color.White, 12), Vistas.Margen(c, 0, 0, 0, 10, ViewGroup.LayoutParams.WrapContent));
        var ayuda = new FrameLayout(c);
        ayuda.Background = Vistas.ConOnda(c, Colores.Tarjeta, 30, Color.ParseColor("#221F4FE0"), Colores.BordeSuave);
        ayuda.Elevation = Vistas.Px(c, 2);
        var ico = Vistas.Icono(c, Resource.Drawable.ic_help, Colores.Primario, 24);
        ayuda.AddView(ico, new FrameLayout.LayoutParams(Vistas.Px(c, 24), Vistas.Px(c, 24), GravityFlags.Center));
        ayuda.Click += (_, _) => MostrarAyuda();
        Vistas.EfectoPresion(ayuda, 0.92f);
        cab.AddView(ayuda, new LinearLayout.LayoutParams(Vistas.Px(c, 48), Vistas.Px(c, 48)));
        col.AddView(cab, Vistas.Margen(c, 0, 18));

        // ---- Resumen del dia ----
        var resumen = Vistas.Tarjeta(c, oscura: true, padDp: 20);
        var fila = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        fila.SetGravity(GravityFlags.CenterVertical);
        _tituloResumen = Vistas.Etiqueta(c, "", 17, Color.White, negrita: true);
        fila.AddView(_tituloResumen, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        fila.AddView(Vistas.Icono(c, Resource.Drawable.ic_calendar, Color.ParseColor("#9DB4FF"), 26));
        resumen.AddView(fila);

        var interior = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        interior.Background = Vistas.Forma(c, Color.White, 18);
        interior.SetPadding(Vistas.Px(c, 12), Vistas.Px(c, 16), Vistas.Px(c, 12), Vistas.Px(c, 16));
        _total = Vistas.Etiqueta(c, Formato.Monto(0), 28, Colores.PrimarioOscuro, negrita: true);
        _cantidad = Vistas.Etiqueta(c, "0", 28, Colores.PrimarioOscuro, negrita: true);
        interior.AddView(Metrica(c, "Total cobrado", _total), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1.35f));
        var divisor = new View(c); divisor.SetBackgroundColor(Colores.BordeSuave);
        interior.AddView(divisor, new LinearLayout.LayoutParams(Vistas.Px(c, 1), ViewGroup.LayoutParams.MatchParent));
        interior.AddView(Metrica(c, "Pagos recibidos", _cantidad), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        resumen.AddView(interior, Vistas.Margen(c, 16));
        var nota = Vistas.Etiqueta(c, "No incluye mensajes de prueba", 13, Color.ParseColor("#C7D5FF"));
        nota.Gravity = GravityFlags.Center;
        resumen.AddView(nota, Vistas.Margen(c, 12));
        col.AddView(resumen, Vistas.Margen(c, 0, 16));

        // ---- Aviso de permiso ----
        var aviso = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        aviso.SetGravity(GravityFlags.CenterVertical);
        aviso.Background = Vistas.Forma(c, Colores.AmbarClaro, 20, Color.ParseColor("#F2D28A"));
        var p2 = Vistas.Px(c, 16);
        aviso.SetPadding(p2, p2, p2, p2);
        aviso.AddView(Vistas.Icono(c, Resource.Drawable.ic_bell, Colores.Ambar, 28), Vistas.Margen(c, 0, 0, 0, 12, Vistas.Px(c, 28), Vistas.Px(c, 28)));
        var textos = new LinearLayout(c) { Orientation = Orientation.Vertical };
        textos.AddView(Vistas.Etiqueta(c, "Falta el permiso de notificaciones", 15, Color.ParseColor("#7C2D12"), negrita: true));
        textos.AddView(Vistas.Etiqueta(c, "Sin él, la app no detecta tus pagos.", 13, Colores.Ambar), Vistas.Margen(c, 2));
        aviso.AddView(textos, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        aviso.AddView(Vistas.Boton(c, "Activar", EstiloBoton.Primario,
            () => _a.StartActivity(new Intent(_a, typeof(PermisosActivity))), compacto: true),
            Vistas.Margen(c, 0, 0, 10, 0, ViewGroup.LayoutParams.WrapContent));
        _avisoPermiso = aviso;
        col.AddView(aviso, Vistas.Margen(c, 0, 20));

        // ---- Movimientos recientes ----
        var titulo = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        titulo.SetGravity(GravityFlags.CenterVertical);
        titulo.AddView(Vistas.Etiqueta(c, "Movimientos recientes", 20, Colores.Texto, negrita: true),
            new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        var verTodos = Vistas.Etiqueta(c, "Ver todos", 15, Colores.Primario, negrita: true);
        verTodos.SetPadding(Vistas.Px(c, 10), Vistas.Px(c, 8), 0, Vistas.Px(c, 8));
        verTodos.Click += (_, _) => _a.StartActivity(new Intent(_a, typeof(MovimientosActivity)));
        titulo.AddView(verTodos);
        col.AddView(titulo, Vistas.Margen(c, 0, 10));

        _lista = new LinearLayout(c) { Orientation = Orientation.Vertical };
        col.AddView(_lista, Vistas.Margen(c));
        PonerMensajeLista("Cargando movimientos…");

        _vista.AddView(col);
        ActualizarTituloFecha();
    }

    private static LinearLayout Metrica(Context c, string titulo, TextView valor)
    {
        var l = new LinearLayout(c) { Orientation = Orientation.Vertical };
        l.SetGravity(GravityFlags.Center);
        var t = Vistas.Etiqueta(c, titulo, 14, Colores.TextoSecundario);
        t.Gravity = GravityFlags.Center;
        l.AddView(t);
        valor.Gravity = GravityFlags.Center;
        l.AddView(valor, Vistas.Margen(c, 4));
        return l;
    }

    private void ActualizarTituloFecha()
    {
        var f = ZonaLima.AhoraLima.ToString("dddd d 'de' MMMM", Es);
        _tituloResumen.Text = $"Resumen del {f}";
    }

    private void MostrarAyuda()
    {
        new AlertDialog.Builder(_a)!
            .SetTitle("¿Cómo funciona?")!
            .SetMessage("PagoAlerta lee las notificaciones de tus apps de pago (Yape, Plin, BBVA…) y las envía a la nube. " +
                        "Tu computadora muestra los pagos en tiempo real.\n\n" +
                        "• El total y la cantidad no incluyen los mensajes de prueba.\n" +
                        "• Si no hay internet, los pagos se guardan en el celular y se envían solos al volver la red.\n" +
                        "• Activa el permiso de notificaciones y desactiva el ahorro de batería en \"Permisos y batería\".")!
            .SetPositiveButton("Entendido", (IDialogInterfaceOnClickListener?)null)!
            .Show();
    }

    // ---------------- Ciclo de vida ----------------
    public void AlMostrar()
    {
        _visible = true;
        ActualizarTituloFecha();
        ActualizarAvisoPermiso();
        _handler.RemoveCallbacks(_tick);
        _handler.Post(_tick);   // carga ya y luego cada 15 s
    }

    public void AlOcultar()
    {
        _visible = false;
        _handler.RemoveCallbacks(_tick);
    }

    private void ActualizarAvisoPermiso()
    {
        var falta = !Servicios.ListenerHabilitado(_a);
        _avisoPermiso.Visibility = falta ? ViewStates.Visible : ViewStates.Gone;
    }

    // ---------------- Datos ----------------
    private async Task CargarAsync()
    {
        if (_cargando) return;
        _cargando = true;
        try
        {
            ActualizarAvisoPermiso();
            if (!await Servicios.AsegurarSesionAsync())
            {
                PonerMensajeLista("Tu sesión venció. Cierra sesión desde la pestaña Cuenta e ingresa de nuevo.");
                return;
            }

            var (desde, hasta) = ZonaLima.RangoDia(ZonaLima.AhoraLima.Date);
            var tResumen = Servicios.Pago!.ResumenAsync(new FiltroPagos { DesdeUtc = desde, HastaUtc = hasta });
            var tLista = Servicios.Pago.ListarPaginaAsync(new FiltroPagos { IncluirPruebas = true, Limite = 12 });
            await Task.WhenAll(tResumen, tLista);

            PintarResumen(tResumen.Result);
            PintarLista(tLista.Result.Items);
        }
        catch (Exception ex)
        {
            Registro.Error("No se pudieron cargar los movimientos", ex);
            if (_lista.ChildCount == 0 || _firmaLista == "")
                PonerMensajeLista(Reintentos.EsTransitorio(ex)
                    ? "Sin conexión. Se reintentará automáticamente."
                    : "No se pudieron cargar los movimientos.");
        }
        finally { _cargando = false; }
    }

    private void PintarResumen(ResumenPagos r)
    {
        Anim.ContarNumero(_total, _totalAnterior, r.Total, v => Formato.Monto(v));
        Anim.ContarNumero(_cantidad, _cantidadAnterior, r.Cantidad, v => ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture), 500);
        _totalAnterior = r.Total; _cantidadAnterior = r.Cantidad;
    }

    private void PintarLista(List<Pago> pagos)
    {
        var firma = string.Join(",", pagos.Select(p => p.Id));
        if (firma == _firmaLista && _lista.ChildCount > 0) return;   // sin cambios: no se vuelve a animar
        var hayNuevos = _firmaLista != "" && firma != _firmaLista;
        _firmaLista = firma;

        _lista.RemoveAllViews();
        if (pagos.Count == 0)
        {
            PonerMensajeLista("Aún no hay movimientos.\nCuando llegue un pago aparecerá aquí.");
            _firmaLista = "";
            return;
        }
        foreach (var p in pagos)
            _lista.AddView(Tarjetas.Movimiento(_a, p), Vistas.Margen(_a, 0, 12));
        Anim.EntrarLista(_lista);
        if (hayNuevos) _a.Mensaje("Nuevo movimiento");
    }

    private void PonerMensajeLista(string texto)
    {
        _lista.RemoveAllViews();
        var t = Vistas.Etiqueta(_a, texto, 15, Colores.TextoSecundario);
        t.Gravity = GravityFlags.Center;
        t.SetLineSpacing(0, 1.15f);
        var tarjeta = Vistas.Tarjeta(_a, padDp: 24);
        tarjeta.AddView(t, Vistas.Margen(_a));
        _lista.AddView(tarjeta, Vistas.Margen(_a));
    }
}
