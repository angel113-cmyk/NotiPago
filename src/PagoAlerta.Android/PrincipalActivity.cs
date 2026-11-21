using Android.Graphics;
using PagoAlerta.Core.Logging;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Tabs;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil;

/// <summary>Pantalla principal con tres pestañas (Inicio · Estado · Cuenta) y barra inferior animada.</summary>
[Activity(Label = "@string/app_name", Exported = false, Theme = "@android:style/Theme.DeviceDefault.Light.NoActionBar")]
public class PrincipalActivity : PantallaBase
{
    private ITab[] _tabs = null!;
    private FrameLayout _contenedor = null!;
    private LinearLayout _nav = null!;
    private readonly List<(ImageView Icono, TextView Texto, View Indicador)> _items = new();
    private int _actual = -1;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ConfigurarBarras(Colores.Fondo, iconosClaros: false);

        _tabs = new ITab[] { new InicioTab(this), new EstadoTab(this), new CuentaTab(this) };

        var raiz = new LinearLayout(this) { Orientation = Orientation.Vertical };
        raiz.SetBackgroundColor(Colores.Fondo);

        _contenedor = new FrameLayout(this);
        raiz.AddView(_contenedor, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        _nav = ConstruirNav();
        raiz.AddView(_nav, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        AplicarInsets(raiz, (arriba, abajo) =>
        {
            raiz.SetPadding(0, arriba, 0, 0);
            _nav.SetPadding(Vistas.Px(this, 8), Vistas.Px(this, 6), Vistas.Px(this, 8), Vistas.Px(this, 8) + abajo);
        });
        SetContentView(raiz);

        // Android 13+: permiso para mostrar la notificacion persistente del monitoreo.
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            CheckSelfPermission("android.permission.POST_NOTIFICATIONS") != Permission.Granted)
            RequestPermissions(new[] { "android.permission.POST_NOTIFICATIONS" }, 10);

        Mostrar(0);
    }

    protected override void OnResume()
    {
        base.OnResume();
        if (_actual >= 0) _tabs[_actual].AlMostrar();
    }

    protected override void OnPause()
    {
        base.OnPause();
        if (_actual >= 0) _tabs[_actual].AlOcultar();
    }

    public override void OnBackPressed()
    {
        if (_actual != 0) Mostrar(0);   // atras desde Estado/Cuenta vuelve a Inicio
        else base.OnBackPressed();
    }

    // ---------------- Barra inferior ----------------
    private LinearLayout ConstruirNav()
    {
        var nav = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        nav.Background = Vistas.Forma(this, Colores.Tarjeta, 0, Colores.BordeSuave);
        nav.Elevation = Vistas.Px(this, 10);

        var def = new (string Texto, int Icono)[]
        {
            ("Inicio", Resource.Drawable.ic_home), ("Estado", Resource.Drawable.ic_pulse), ("Cuenta", Resource.Drawable.ic_person)
        };
        for (var i = 0; i < def.Length; i++)
        {
            var item = new LinearLayout(this) { Orientation = Orientation.Vertical };
            item.SetGravity(GravityFlags.Center);
            item.SetPadding(0, Vistas.Px(this, 6), 0, Vistas.Px(this, 4));
            item.Background = Vistas.ConOnda(this, Color.Transparent, 18, Color.ParseColor("#1A1F4FE0"));

            var indicador = new View(this);
            indicador.Background = Vistas.Forma(this, Colores.Primario, 4);
            indicador.Alpha = 0f;
            item.AddView(indicador, new LinearLayout.LayoutParams(Vistas.Px(this, 26), Vistas.Px(this, 4)) { BottomMargin = Vistas.Px(this, 6) });

            var ico = Vistas.Icono(this, def[i].Icono, Colores.TextoSecundario, 26);
            item.AddView(ico, new LinearLayout.LayoutParams(Vistas.Px(this, 26), Vistas.Px(this, 26)));
            var txt = Vistas.Etiqueta(this, def[i].Texto, 13, Colores.TextoSecundario, negrita: true);
            item.AddView(txt, Vistas.Margen(this, 4, 0, 0, 0, ViewGroup.LayoutParams.WrapContent));

            var idx = i;
            item.Click += (_, _) => Mostrar(idx);
            nav.AddView(item, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            _items.Add((ico, txt, indicador));
        }
        return nav;
    }

    private void MarcarNav(int seleccionado)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var (ico, txt, ind) = _items[i];
            var activo = i == seleccionado;
            ico.SetColorFilter(activo ? Colores.Primario : Colores.TextoSecundario);
            txt.SetTextColor(activo ? Colores.Primario : Colores.TextoSecundario);
            ind.Animate()!.Alpha(activo ? 1f : 0f).SetDuration(200).Start();
            if (activo)
            {
                ico.ScaleX = ico.ScaleY = 0.8f;
                ico.Animate()!.ScaleX(1f).ScaleY(1f).SetDuration(320)
                    .SetInterpolator(new Android.Views.Animations.OvershootInterpolator(3f)).Start();
            }
        }
    }

    /// <summary>Cambia de pestaña con un desvanecimiento y deslizamiento suave.</summary>
    public void Mostrar(int indice)
    {
        if (indice == _actual) return;
        var direccion = indice > _actual ? 1 : -1;
        if (_actual >= 0) _tabs[_actual].AlOcultar();

        _contenedor.RemoveAllViews();
        var vista = _tabs[indice].Vista;
        (vista.Parent as ViewGroup)?.RemoveView(vista);
        _contenedor.AddView(vista, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        vista.Alpha = 0f;
        vista.TranslationX = direccion * Vistas.Px(this, 28);
        vista.Animate()!.Alpha(1f).TranslationX(0f).SetDuration(260)
            .SetInterpolator(new Android.Views.Animations.DecelerateInterpolator()).Start();

        _actual = indice;
        MarcarNav(indice);
        _tabs[indice].AlMostrar();
    }

    // ---------------- Sesion ----------------
    /// <summary>Vuelve al login. Si <paramref name="cerrarSesion"/>, antes detiene el servicio y borra la sesion guardada.</summary>
    public async void IrALogin(bool cerrarSesion)
    {
        try
        {
            if (cerrarSesion)
            {
                MonitorService.Detener(this);
                if (Servicios.Pago is not null) await Servicios.Pago.CerrarSesionAsync();
                Registro.Info("Sesión cerrada por el usuario");
            }
        }
        catch (Exception ex) { Registro.Error("Error al cerrar sesión", ex); }

        var i = new Intent(this, typeof(LoginActivity));
        i.AddFlags(ActivityFlags.ClearTask | ActivityFlags.NewTask);
        StartActivity(i);
        Finish();
    }
}
