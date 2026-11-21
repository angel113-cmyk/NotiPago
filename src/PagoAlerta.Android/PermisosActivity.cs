using Android.Graphics;
using Android.Net;
using Android.Provider;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil;

/// <summary>
/// HU-11: "Activar la deteccion" en 4 pasos: acceso a notificaciones, ajustes restringidos (Android 13+),
/// bateria sin restricciones (con la guia de cada marca) y un mensaje de prueba opcional.
/// </summary>
[Activity(Label = "Permisos", Exported = false, Theme = "@android:style/Theme.DeviceDefault.Light.NoActionBar")]
public class PermisosActivity : PantallaBase
{
    private sealed class Paso
    {
        public FrameLayout Circulo = null!;
        public TextView Numero = null!;
        public ImageView Check = null!;
        public TextView Chip = null!;
        public int Indice;
    }

    private readonly Paso[] _pasos = new Paso[4];
    private bool _guiaAbierta;
    private TextView _resultadoPrueba = null!;
    private Button _btnPrueba = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ConfigurarBarras(Colores.Fondo, iconosClaros: false);

        var raiz = new LinearLayout(this) { Orientation = Orientation.Vertical };
        raiz.SetBackgroundColor(Colores.Fondo);

        // ---- Barra superior con boton atras ----
        var barra = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        barra.SetGravity(GravityFlags.CenterVertical);
        var atras = new FrameLayout(this);
        atras.Background = Vistas.ConOnda(this, Colores.Tarjeta, 30, Color.ParseColor("#221F4FE0"), Colores.BordeSuave);
        atras.Elevation = Vistas.Px(this, 2);
        atras.AddView(Vistas.Icono(this, Resource.Drawable.ic_chevron_l, Colores.Primario, 24),
            new FrameLayout.LayoutParams(Vistas.Px(this, 24), Vistas.Px(this, 24), GravityFlags.Center));
        atras.Click += (_, _) => Finish();
        Vistas.EfectoPresion(atras, 0.92f);
        barra.AddView(atras, Vistas.Margen(this, 0, 0, 0, 14, Vistas.Px(this, 46), Vistas.Px(this, 46)));
        barra.AddView(Vistas.Etiqueta(this, "Activar la detección", 25, Colores.PrimarioOscuro, negrita: true));
        raiz.AddView(barra, Vistas.Margen(this));

        var scroll = new ScrollView(this) { FillViewport = true, VerticalScrollBarEnabled = false };
        var col = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var pad = Vistas.Px(this, 20);
        col.SetPadding(pad, Vistas.Px(this, 4), pad, Vistas.Px(this, 24));

        var intro = Vistas.Etiqueta(this, "Sigue estos 4 pasos una sola vez para que la app lea tus pagos aunque esté cerrada.", 16, Colores.TextoSecundario);
        intro.SetLineSpacing(0, 1.15f);
        col.AddView(intro, Vistas.Margen(this, 0, 16));

        // ---- Paso 1 ----
        var c1 = Vistas.Tarjeta(this, padDp: 18);
        var b1 = new LinearLayout(this) { Orientation = Orientation.Vertical };
        b1.AddView(Descripcion("Se abre Ajustes. Busca PagoAlerta y activa el permiso."));
        b1.AddView(Vistas.Boton(this, "Abrir ajustes", EstiloBoton.Primario, AbrirAjusteNotificaciones, compacto: true),
            Vistas.Margen(this, 12, 0, 0, 0, ViewGroup.LayoutParams.WrapContent));
        EncabezadoPaso(c1, 0, "Acceso a notificaciones", b1);
        col.AddView(c1, Vistas.Margen(this, 0, 14));

        // ---- Paso 2 ----
        var c2 = Vistas.Tarjeta(this, padDp: 18);
        var b2 = new LinearLayout(this) { Orientation = Orientation.Vertical };
        b2.AddView(Descripcion("Si Android bloquea el permiso (Android 13 o superior): Ajustes > Apps > PagoAlerta > menú de tres puntos > Permitir ajustes restringidos."));
        b2.AddView(Vistas.Boton(this, "Ver cómo", EstiloBoton.Primario, AbrirInfoApp, compacto: true),
            Vistas.Margen(this, 12, 0, 0, 0, ViewGroup.LayoutParams.WrapContent));
        EncabezadoPaso(c2, 1, "Permitir ajustes restringidos", b2);
        col.AddView(c2, Vistas.Margen(this, 0, 14));

        // ---- Paso 3 ----
        var c3 = Vistas.Tarjeta(this, padDp: 18);
        var b3 = new LinearLayout(this) { Orientation = Orientation.Vertical };
        b3.AddView(Descripcion("Para que Android no cierre la app en segundo plano. En algunas marcas hay que activar también el inicio automático."));
        b3.AddView(Vistas.Boton(this, "Excluir de la batería ahora", EstiloBoton.Primario, PedirExclusionBateria, compacto: true),
            Vistas.Margen(this, 12, 0, 0, 0, ViewGroup.LayoutParams.WrapContent));
        b3.AddView(Vistas.Boton(this, "Abrir ajustes de batería", EstiloBoton.Suave, AbrirAjusteBateria, compacto: true),
            Vistas.Margen(this, 8, 0, 0, 0, ViewGroup.LayoutParams.WrapContent));
        b3.AddView(ConstruirGuiaMarca(), Vistas.Margen(this, 12));
        EncabezadoPaso(c3, 2, "Batería sin restricciones", b3);
        col.AddView(c3, Vistas.Margen(this, 0, 14));

        // ---- Paso 4 ----
        var c4 = Vistas.Tarjeta(this, padDp: 18);
        var b4 = new LinearLayout(this) { Orientation = Orientation.Vertical };
        b4.AddView(Descripcion("Envía un pago de prueba y comprueba que aparece en la PC con la etiqueta PRUEBA."));
        _btnPrueba = Vistas.Boton(this, "Enviar mensaje de prueba", EstiloBoton.Primario,
            () => ElegirAppPrueba.Mostrar(this, async app => await ProbarAsync(app)), compacto: true);
        b4.AddView(_btnPrueba, Vistas.Margen(this, 12, 0, 0, 0, ViewGroup.LayoutParams.WrapContent));
        _resultadoPrueba = Vistas.Etiqueta(this, "", 14, Colores.TextoSecundario);
        _resultadoPrueba.Visibility = ViewStates.Gone;
        b4.AddView(_resultadoPrueba, Vistas.Margen(this, 10));
        EncabezadoPaso(c4, 3, "Probar con un mensaje", b4);
        col.AddView(c4, Vistas.Margen(this, 0, 8));

        scroll.AddView(col);
        raiz.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        AplicarInsets(raiz, (arriba, abajo) =>
        {
            barra.SetPadding(Vistas.Px(this, 20), arriba + Vistas.Px(this, 14), Vistas.Px(this, 20), Vistas.Px(this, 12));
            col.SetPadding(pad, Vistas.Px(this, 4), pad, Vistas.Px(this, 24) + abajo);
        });
        SetContentView(raiz);

        for (var i = 0; i < col.ChildCount; i++) Anim.Entrar(col.GetChildAt(i)!, i * 70, 18);
    }

    private TextView Descripcion(string texto)
    {
        var t = Vistas.Etiqueta(this, texto, 14, Colores.TextoSecundario);
        t.SetLineSpacing(0, 1.18f);
        return t;
    }

    /// <summary>Fila superior del paso (circulo numerado + titulo + chip de estado) y el cuerpo debajo.</summary>
    private void EncabezadoPaso(LinearLayout tarjeta, int i, string titulo, View cuerpo)
    {
        var fila = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        fila.SetGravity(GravityFlags.CenterVertical);

        var p = new Paso { Indice = i };
        p.Circulo = new FrameLayout(this);
        p.Numero = Vistas.Etiqueta(this, (i + 1).ToString(), 17, Colores.Ambar, negrita: true);
        p.Numero.Gravity = GravityFlags.Center;
        p.Circulo.AddView(p.Numero, new FrameLayout.LayoutParams(-1, -1));
        p.Check = Vistas.Icono(this, Resource.Drawable.ic_check, Colores.Verde, 22);
        p.Check.Visibility = ViewStates.Gone;
        p.Circulo.AddView(p.Check, new FrameLayout.LayoutParams(Vistas.Px(this, 22), Vistas.Px(this, 22), GravityFlags.Center));
        fila.AddView(p.Circulo, Vistas.Margen(this, 0, 0, 0, 14, Vistas.Px(this, 42), Vistas.Px(this, 42)));

        fila.AddView(Vistas.Etiqueta(this, titulo, 17, Colores.Texto, negrita: true), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        p.Chip = Vistas.Pildora(this, "PENDIENTE", Colores.AmbarClaro, Colores.Ambar, 11);
        fila.AddView(p.Chip);
        tarjeta.AddView(fila);

        var cont = new LinearLayout(this) { Orientation = Orientation.Vertical };
        cont.AddView(cuerpo);
        tarjeta.AddView(cont, Vistas.Margen(this, 12, 0, 56, 0));
        _pasos[i] = p;
    }

    // ---------------- Guia por marca (HT-08) ----------------
    private View ConstruirGuiaMarca()
    {
        var guia = GuiaBateria.ParaEsteEquipo();
        var caja = new LinearLayout(this) { Orientation = Orientation.Vertical };

        var cab = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        cab.SetGravity(GravityFlags.CenterVertical);
        cab.Background = Vistas.ConOnda(this, Colores.AzulClaro, 14, Color.ParseColor("#221F4FE0"));
        cab.SetPadding(Vistas.Px(this, 14), Vistas.Px(this, 12), Vistas.Px(this, 12), Vistas.Px(this, 12));
        cab.AddView(Vistas.Etiqueta(this, $"Ajustes de {guia.Marca}", 14, Colores.Primario, negrita: true),
            new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        var flecha = Vistas.Icono(this, Resource.Drawable.ic_chevron_r, Colores.Primario, 20);
        cab.AddView(flecha);
        caja.AddView(cab);

        var contenido = new LinearLayout(this) { Orientation = Orientation.Vertical };
        contenido.Visibility = ViewStates.Gone;
        contenido.SetPadding(Vistas.Px(this, 4), Vistas.Px(this, 12), Vistas.Px(this, 4), 0);
        for (var i = 0; i < guia.Pasos.Length; i++)
        {
            var t = Vistas.Etiqueta(this, $"{i + 1}. {guia.Pasos[i]}", 14, Colores.Texto);
            t.SetLineSpacing(0, 1.18f);
            contenido.AddView(t, Vistas.Margen(this, 0, 8));
        }
        if (guia.Pantallas.Length > 0)
            contenido.AddView(Vistas.Boton(this, "Abrir ajustes de la marca", EstiloBoton.Contorno, () => AbrirPantallasMarca(guia), compacto: true),
                Vistas.Margen(this, 4, 0, 0, 0, ViewGroup.LayoutParams.WrapContent));
        caja.AddView(contenido);

        cab.Click += (_, _) =>
        {
            _guiaAbierta = !_guiaAbierta;
            flecha.Animate()!.Rotation(_guiaAbierta ? 90f : 0f).SetDuration(220).Start();
            if (_guiaAbierta) { contenido.Visibility = ViewStates.Visible; Anim.Entrar(contenido, 0, 8); }
            else contenido.Visibility = ViewStates.Gone;
        };
        return caja;
    }

    // ---------------- Estado de cada paso ----------------
    protected override void OnResume()
    {
        base.OnResume();
        var notif = Servicios.ListenerHabilitado(this);
        var bateria = Servicios.IgnoraBateria(this);
        var android13 = Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu;

        Marcar(0, notif ? "LISTO" : "PENDIENTE", notif);
        // No se puede detectar si el ajuste restringido esta permitido: si el permiso 1 ya funciona, se da por resuelto.
        Marcar(1, !android13 ? "NO APLICA" : notif ? "LISTO" : "PENDIENTE", !android13 || notif);
        Marcar(2, bateria ? "LISTO" : "PENDIENTE", bateria);
        Marcar(3, "OPCIONAL", false, opcional: true);
    }

    private void Marcar(int i, string texto, bool ok, bool opcional = false)
    {
        var p = _pasos[i];
        p.Chip.Text = texto;
        var (fondoChip, textoChip, fondoCirculo) = opcional ? (Colores.AzulClaro, Colores.Primario, Colores.AzulClaro)
            : ok ? (Colores.VerdeClaro, Colores.Verde, Colores.VerdeClaro)
            : (Colores.AmbarClaro, Colores.Ambar, Colores.AmbarClaro);
        p.Chip.Background = Vistas.Forma(this, fondoChip, Vistas.RadioPildora);
        p.Chip.SetTextColor(textoChip);
        p.Circulo.Background = Vistas.Forma(this, fondoCirculo, 40);
        p.Numero.SetTextColor(opcional ? Colores.Primario : Colores.Ambar);

        var mostrarCheck = ok && !opcional;
        if (mostrarCheck && p.Check.Visibility != ViewStates.Visible)
        {
            p.Numero.Visibility = ViewStates.Gone;
            p.Check.Visibility = ViewStates.Visible;
            Anim.Rebotar(p.Check);
        }
        else if (!mostrarCheck)
        {
            p.Numero.Visibility = ViewStates.Visible;
            p.Check.Visibility = ViewStates.Gone;
        }
    }

    // ---------------- Acciones ----------------
    private async Task ProbarAsync(string? app)
    {
        _btnPrueba.Enabled = false;
        _resultadoPrueba.Visibility = ViewStates.Visible;
        _resultadoPrueba.SetTextColor(Colores.TextoSecundario);
        _resultadoPrueba.Text = "Enviando…";
        var r = await Servicios.EnviarPruebaAsync(app);
        _btnPrueba.Enabled = true;
        _resultadoPrueba.SetTextColor(r.Ok ? Colores.Verde : Colores.Error);
        _resultadoPrueba.Text = r.Ok ? "Prueba enviada. Revísala en la PC (etiqueta PRUEBA)." : r.Mensaje;
        if (r.Ok) Mensaje("Mensaje de prueba enviado"); else { Anim.Temblar(_btnPrueba); Mensaje(r.Mensaje, error: true, largo: true); }
    }

    private void AbrirAjusteNotificaciones() => Abrir(() => new Intent(Settings.ActionNotificationListenerSettings),
        "No se pudo abrir el ajuste. Ve a Ajustes > Notificaciones > Acceso a notificaciones.");

    private void AbrirAjusteBateria() => Abrir(() => new Intent(Settings.ActionIgnoreBatteryOptimizationSettings),
        "No se pudo abrir el ajuste. Ve a Ajustes > Batería.");

    /// <summary>Muestra el dialogo del sistema "¿Permitir que PagoAlerta ignore las optimizaciones de batería?".</summary>
    private void PedirExclusionBateria()
    {
        if (Servicios.IgnoraBateria(this)) { Mensaje("Ya está excluida de la optimización de batería"); return; }
        Abrir(() => new Intent(Settings.ActionRequestIgnoreBatteryOptimizations, Uri.FromParts("package", PackageName, null)),
            "No se pudo abrir el diálogo. Usa \"Abrir ajustes de batería\".");
    }

    private void AbrirPantallasMarca(GuiaBateria.Guia guia)
    {
        foreach (var (paquete, clase) in guia.Pantallas)
        {
            try
            {
                var i = new Intent();
                i.SetComponent(new ComponentName(paquete, clase));
                StartActivity(i);
                return;
            }
            catch (Exception) { /* esa pantalla no existe en esta version: se prueba la siguiente */ }
        }
        Mensaje("No se pudo abrir la pantalla de tu marca. Sigue los pasos escritos.", error: true, largo: true);
        AbrirInfoApp();
    }

    private void AbrirInfoApp() => Abrir(
        () => new Intent(Settings.ActionApplicationDetailsSettings, Uri.FromParts("package", PackageName, null)),
        "No se pudo abrir la información de la app.");

    private void Abrir(Func<Intent> crear, string errorMensaje)
    {
        try { StartActivity(crear()); }
        catch (ActivityNotFoundException) { Mensaje(errorMensaje, error: true, largo: true); }
    }
}
