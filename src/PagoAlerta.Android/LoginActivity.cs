using Android.Graphics;
using PagoAlerta.Core;
using PagoAlerta.Core.Services;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil;

/// <summary>HU-10: iniciar sesion (o crear la cuenta) en el celular. Guarda solo tokens, cifrados con Keystore.</summary>
[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true,
          Theme = "@android:style/Theme.DeviceDefault.Light.NoActionBar",
          WindowSoftInputMode = SoftInput.AdjustResize)]
public class LoginActivity : Activity
{
    private EditText _correo = null!, _clave = null!;
    private CheckBox _recordar = null!;
    private TextView _mensaje = null!;
    private Button _ingresar = null!, _crear = null!;
    private TextView _estado = null!;

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Window!.SetStatusBarColor(Colores.Fondo);
        Window.SetNavigationBarColor(Colores.Fondo);
        Window.DecorView.IconosEstado(claros: false);
        Construir();

        if (Servicios.ErrorConfiguracion is { } err) { Mostrar(err, true); return; }

        // Si ya hay una sesion guardada, entra directo.
        Ocupado(true, "Restaurando sesión…");
        var ok = await Servicios.AsegurarSesionAsync();
        Ocupado(false);
        if (ok) IrAEstado();
    }

    private void Construir()
    {
        var pad = Vistas.Px(this, 24);
        var scroll = new ScrollView(this) { FillViewport = true };
        scroll.SetBackgroundColor(Colores.Fondo);

        var col = new LinearLayout(this) { Orientation = Orientation.Vertical };
        col.SetGravity(GravityFlags.CenterVertical | GravityFlags.CenterHorizontal);
        col.SetPadding(pad, pad, pad, pad);

        var logo = new LogoView(this);
        col.AddView(logo, Vistas.Margen(this, 0, 14, ancho: ViewGroup.LayoutParams.WrapContent, alto: ViewGroup.LayoutParams.WrapContent));
        ((LinearLayout.LayoutParams)logo.LayoutParameters!).Gravity = GravityFlags.CenterHorizontal;

        var titulo = Vistas.Etiqueta(this, AppInfo.Nombre, 28, Colores.Texto, negrita: true);
        titulo.Gravity = GravityFlags.Center;
        col.AddView(titulo, Vistas.Margen(this));
        var sub = Vistas.Etiqueta(this, "Inicia sesión o crea tu cuenta", 15, Colores.TextoSecundario);
        sub.Gravity = GravityFlags.Center;
        col.AddView(sub, Vistas.Margen(this, 2, 22));

        var tarjeta = Vistas.Tarjeta(this);
        _correo = Vistas.Campo(this, "Correo electrónico");
        _clave = Vistas.Campo(this, "Contraseña (mínimo 8 caracteres)", clave: true);
        tarjeta.AddView(_correo, Vistas.Margen(this, 0, 12));
        tarjeta.AddView(_clave, Vistas.Margen(this, 0, 8));

        _recordar = new CheckBox(this) { Text = "Recordar sesión", Checked = true };
        _recordar.SetTextColor(Colores.TextoSecundario);
        tarjeta.AddView(_recordar, Vistas.Margen(this, 0, 6));

        _mensaje = Vistas.Etiqueta(this, "", 14, Colores.Error);
        _mensaje.Visibility = ViewStates.Gone;
        tarjeta.AddView(_mensaje, Vistas.Margen(this, 0, 10));

        _ingresar = Vistas.Boton(this, "Ingresar", true, async () => await IngresarAsync());
        tarjeta.AddView(_ingresar, Vistas.Margen(this, 4, 10));
        _crear = Vistas.Boton(this, "Crear cuenta", false, async () => await CrearAsync());
        tarjeta.AddView(_crear, Vistas.Margen(this));

        _estado = Vistas.Etiqueta(this, "", 13, Colores.TextoSecundario);
        _estado.Gravity = GravityFlags.Center;
        tarjeta.AddView(_estado, Vistas.Margen(this, 10));

        col.AddView(tarjeta, Vistas.Margen(this));
        scroll.AddView(col);
        SetContentView(scroll);

        scroll.SetOnApplyWindowInsetsListener(new InsetsListener((arriba, abajo) => col.SetPadding(pad, pad + arriba, pad, pad + abajo)));
    }

    private void Ocupado(bool si, string? texto = null)
    {
        _ingresar.Enabled = _crear.Enabled = _correo.Enabled = _clave.Enabled = !si;
        _estado.Text = texto ?? "";
    }

    private void Mostrar(string texto, bool error)
    {
        _mensaje.Text = texto;
        _mensaje.SetTextColor(error ? Colores.Error : Colores.Verde);
        _mensaje.Visibility = ViewStates.Visible;
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
            case ResultadoLogin.Ok: IrAEstado(); break;
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
            case ResultadoRegistro.Ok: IrAEstado(); break;
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

    private void IrAEstado()
    {
        StartActivity(new Intent(this, typeof(EstadoActivity)));
        Finish();
    }
}

