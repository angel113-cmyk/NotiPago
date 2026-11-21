using Android.Graphics;
using Android.Graphics.Drawables;
using PagoAlerta.Core;
using PagoAlerta.Core.Services;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil;

/// <summary>
/// HU-10: iniciar sesion (o crear la cuenta) en el celular. Guarda solo tokens, cifrados con Keystore.
/// La logica no cambia; solo el diseño y los efectos.
/// </summary>
[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true,
          Theme = "@android:style/Theme.DeviceDefault.Light.NoActionBar",
          WindowSoftInputMode = SoftInput.AdjustResize)]
public class LoginActivity : PantallaBase
{
    private EditText _correo = null!, _clave = null!;
    private CheckBox _recordar = null!;
    private TextView _mensaje = null!, _estado = null!, _crear = null!;
    private Button _ingresar = null!;
    private LinearLayout _formulario = null!;

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ConfigurarBarras(Colores.PrimarioOscuro, iconosClaros: true);
        Construir();

        if (Servicios.ErrorConfiguracion is { } err) { Mostrar(err, true); return; }

        // Si ya hay una sesion guardada, entra directo.
        Ocupado(true, "Restaurando sesión…");
        var ok = await Servicios.AsegurarSesionAsync();
        Ocupado(false);
        if (ok) IrAPrincipal();
    }

    private void Construir()
    {
        var raiz = new ScrollView(this) { FillViewport = true, VerticalScrollBarEnabled = false };
        raiz.SetBackgroundColor(Colores.Fondo);
        var col = new LinearLayout(this) { Orientation = Orientation.Vertical };

        // ---------- Cabecera azul con curva ----------
        var cab = new LinearLayout(this) { Orientation = Orientation.Vertical };
        cab.SetGravity(GravityFlags.Center);
        var fondo = new GradientDrawable();
        fondo.SetColor(Colores.PrimarioOscuro);
        var r = Vistas.Px(this, 44);
        fondo.SetCornerRadii(new float[] { 0, 0, 0, 0, r, r, r, r });
        cab.Background = fondo;

        var logo = new FrameLayout(this);
        logo.Background = Vistas.Forma(this, Color.White, 28);
        logo.Elevation = Vistas.Px(this, 10);
        logo.AddView(Vistas.Icono(this, Resource.Drawable.ic_bell, Colores.Primario, 46),
            new FrameLayout.LayoutParams(Vistas.Px(this, 46), Vistas.Px(this, 46), GravityFlags.Center));
        cab.AddView(logo, new LinearLayout.LayoutParams(Vistas.Px(this, 92), Vistas.Px(this, 92)));

        var titulo = Vistas.Etiqueta(this, AppInfo.Nombre, 36, Color.White, negrita: true);
        titulo.Gravity = GravityFlags.Center;
        cab.AddView(titulo, Vistas.Margen(this, 20));
        var lema = Vistas.Etiqueta(this, "Tus pagos, siempre a la vista", 16, Color.ParseColor("#C7D5FF"));
        lema.Gravity = GravityFlags.Center;
        cab.AddView(lema, Vistas.Margen(this, 6));
        col.AddView(cab, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        // ---------- Formulario ----------
        _formulario = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var pad = Vistas.Px(this, 24);
        _formulario.SetPadding(pad, Vistas.Px(this, 28), pad, Vistas.Px(this, 24));

        _formulario.AddView(Vistas.Etiqueta(this, "Ingresar", 30, Colores.PrimarioOscuro, negrita: true), Vistas.Margen(this, 0, 20));

        _formulario.AddView(Vistas.EtiquetaCampo(this, "Correo electrónico"), Vistas.Margen(this, 0, 8));
        _correo = Vistas.Campo(this, "tucorreo@tienda.com");
        _formulario.AddView(_correo, Vistas.Margen(this, 0, 18));

        _formulario.AddView(Vistas.EtiquetaCampo(this, "Contraseña"), Vistas.Margen(this, 0, 8));
        _clave = Vistas.Campo(this, "Mínimo 8 caracteres", clave: true);
        _formulario.AddView(_clave, Vistas.Margen(this, 0, 10));

        _recordar = new CheckBox(this) { Text = "Recordar sesión", Checked = true };
        _recordar.SetTextColor(Colores.TextoSecundario);
        _recordar.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
        _formulario.AddView(_recordar, Vistas.Margen(this, 0, 6));

        _mensaje = Vistas.Etiqueta(this, "", 14, Colores.Error, negrita: true);
        _mensaje.Visibility = ViewStates.Gone;
        _formulario.AddView(_mensaje, Vistas.Margen(this, 0, 10));

        _ingresar = Vistas.Boton(this, "Ingresar", EstiloBoton.Primario, async () => await IngresarAsync());
        _ingresar.SetMinHeight(Vistas.Px(this, 58));
        _formulario.AddView(_ingresar, Vistas.Margen(this, 6, 8));

        _estado = Vistas.Etiqueta(this, "", 13, Colores.TextoSecundario);
        _estado.Gravity = GravityFlags.Center;
        _formulario.AddView(_estado, Vistas.Margen(this, 4, 6));

        // Enlace secundario (discreto): crear cuenta con los datos escritos
        _crear = Vistas.Etiqueta(this, "¿Primera vez? Crear cuenta con estos datos", 15, Colores.Primario, negrita: true);
        _crear.Gravity = GravityFlags.Center;
        _crear.SetPadding(0, Vistas.Px(this, 14), 0, Vistas.Px(this, 14));
        _crear.Background = Vistas.ConOnda(this, Color.Transparent, 14, Color.ParseColor("#221F4FE0"));
        _crear.Click += async (_, _) => await CrearAsync();
        Vistas.EfectoPresion(_crear, 0.98f);
        _formulario.AddView(_crear, Vistas.Margen(this, 4, 12));

        var nota = Vistas.Etiqueta(this,
            "Usa el mismo correo y contraseña con los que entras en la app de la PC. Tus datos viajan cifrados a la nube.",
            13, Colores.TextoSecundario);
        nota.Gravity = GravityFlags.Center;
        nota.SetLineSpacing(0, 1.15f);
        _formulario.AddView(nota, Vistas.Margen(this, 4));
        col.AddView(_formulario, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        raiz.AddView(col);
        SetContentView(raiz);

        AplicarInsets(raiz, (arriba, abajo) =>
        {
            cab.SetPadding(Vistas.Px(this, 24), arriba + Vistas.Px(this, 44), Vistas.Px(this, 24), Vistas.Px(this, 48));
            _formulario.SetPadding(pad, Vistas.Px(this, 28), pad, Vistas.Px(this, 24) + abajo);
        });

        // Entrada animada
        Anim.Rebotar(logo, 80);
        Anim.Entrar(titulo, 180, 14);
        Anim.Entrar(lema, 260, 14);
        for (var i = 0; i < _formulario.ChildCount; i++) Anim.Entrar(_formulario.GetChildAt(i)!, 220 + i * 45, 16);
    }

    private void Ocupado(bool si, string? texto = null)
    {
        _ingresar.Enabled = _crear.Enabled = _correo.Enabled = _clave.Enabled = !si;
        _crear.Alpha = si ? 0.5f : 1f;
        _estado.Text = texto ?? "";
    }

    private void Mostrar(string texto, bool error)
    {
        _mensaje.Text = texto;
        _mensaje.SetTextColor(error ? Colores.Error : Colores.Verde);
        _mensaje.Visibility = ViewStates.Visible;
        Anim.Entrar(_mensaje, 0, 6);
        if (error) Anim.Temblar(_formulario);
    }

    private bool Validar(out string correo, out string clave)
    {
        correo = _correo.Text?.Trim() ?? "";
        clave = _clave.Text ?? "";
        if (correo.Length == 0 || clave.Length == 0) { Mostrar("Ingresa tu correo y contraseña.", true); return false; }
        if (Servicios.Pago is null) { Mostrar(Servicios.ErrorConfiguracion ?? "Configuración no disponible.", true); return false; }
        _mensaje.Visibility = ViewStates.Gone;
        return true;
    }

    private async Task IngresarAsync()
    {
        if (!Validar(out var correo, out var clave)) return;
        Ocupado(true, "Ingresando…");
        var r = await Servicios.Pago!.LoginAsync(correo, clave, _recordar.Checked);
        _clave.Text = "";
        Ocupado(false);

        switch (r)
        {
            case ResultadoLogin.Ok: IrAPrincipal(); break;
            case ResultadoLogin.SinConexion: Mostrar("No se pudo conectar. Revisa tu internet.", true); break;
            default: Mostrar("Correo o contraseña incorrectos.", true); break;   // mensaje generico
        }
    }

    private async Task CrearAsync()
    {
        if (!Validar(out var correo, out var clave)) return;
        Ocupado(true, "Creando cuenta…");
        var r = await Servicios.Pago!.RegistrarAsync(correo, clave, _recordar.Checked);
        _clave.Text = "";
        Ocupado(false);

        switch (r)
        {
            case ResultadoRegistro.Ok: IrAPrincipal(); break;
            case ResultadoRegistro.RequiereConfirmacion:
                Mostrar("Cuenta creada. Confirma tu correo y luego inicia sesión.", false); break;
            case ResultadoRegistro.CorreoInvalido: Mostrar("Escribe un correo válido.", true); break;
            case ResultadoRegistro.ContrasenaDebil: Mostrar("La contraseña debe tener al menos 8 caracteres.", true); break;
            case ResultadoRegistro.CorreoYaRegistrado: Mostrar("Ese correo ya tiene cuenta. Usa \"Ingresar\".", true); break;
            case ResultadoRegistro.RegistroDeshabilitado: Mostrar("El registro está deshabilitado en Supabase.", true); break;
            case ResultadoRegistro.SinConexion: Mostrar("No se pudo conectar. Revisa tu internet.", true); break;
            default: Mostrar("No se pudo crear la cuenta. Inténtalo de nuevo.", true); break;
        }
    }

    private void IrAPrincipal()
    {
        StartActivity(new Intent(this, typeof(PrincipalActivity)));
        OverridePendingTransition(Android.Resource.Animation.FadeIn, Android.Resource.Animation.FadeOut);
        Finish();
    }
}
