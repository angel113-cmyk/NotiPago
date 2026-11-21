using PagoAlerta.Core;
using PagoAlerta.Core.Services;
using PagoAlerta.Desktop.Ui;

namespace PagoAlerta.Desktop;

/// <summary>
/// HU-01: login del admin. Pantalla dividida (panel azul + tarjeta). Error generico, "Recordar sesion",
/// sin guardar contrasena. La logica no cambia; solo la presentacion y los efectos.
/// </summary>
public class LoginForm : Form
{
    private readonly PagoService _servicio;
    private readonly Panel _izquierda = new();
    private readonly Panel _derecha = new();
    private readonly PanelRedondeado _tarjeta = new() { Sombra = true, Size = new Size(470, 500) };
    private readonly CampoRedondeado _correo = new("correo@tienda.com", radio: 14, alto: 50);
    private readonly CampoRedondeado _clave = new("Contraseña", clave: true, radio: 14, alto: 50);
    private readonly CheckBox _recordar = new();
    private readonly Label _error = new();
    private readonly Label _estado = new();
    private readonly BotonPildora _ingresar = new("Ingresar", BotonPildora.Estilo.Primario, alto: 52);
    private readonly Suave _entrada = new(0f, 0.12f);
    private readonly System.Windows.Forms.Timer _temblor = new() { Interval = 16 };
    private int _pasoTemblor;
    private readonly bool _silencioso;

    /// <param name="silencioso">Arranque con Windows: la ventana no se ve hasta saber si hay una sesion guardada.</param>
    public LoginForm(PagoService servicio, bool silencioso = false)
    {
        _servicio = servicio;
        _silencioso = silencioso;
        if (silencioso) { Opacity = 0; ShowInTaskbar = false; }
        Text = AppInfo.Nombre;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente(10f);
        ClientSize = new Size(1120, 680);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon; } catch { }

        Construir();
        Resize += (_, _) => CentrarTarjeta();
        KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && _ingresar.Enabled) { e.SuppressKeyPress = true; await IngresarAsync(); }
        };
        _entrada.Cambio += CentrarTarjeta;
        _temblor.Tick += (_, _) => Temblar();
        Shown += async (_, _) => { _entrada.Hacia(1f); await IntentarRestaurarAsync(); };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _entrada.Dispose(); _temblor.Dispose(); }
        base.Dispose(disposing);
    }

    private void Construir()
    {
        // ---------- Panel izquierdo (azul) ----------
        _izquierda.Dock = DockStyle.Left;
        _izquierda.Width = 500;
        _izquierda.BackColor = Tema.PrimarioOscuro;
        var logo = new LogoCampana(76) { Location = new Point(70, 0), Claro = true };
        var titulo = new Label
        {
            Text = AppInfo.Nombre, ForeColor = Color.White, Font = Tema.Fuente(34f, FontStyle.Bold),
            AutoSize = true, BackColor = Color.Transparent
        };
        var lema = new Label
        {
            Text = "Mira en tu computadora los pagos que llegan al celular de la tienda: fecha, hora, nombre completo y monto.",
            ForeColor = Color.FromArgb(215, 255, 255, 255), Font = Tema.Fuente(13f), BackColor = Color.Transparent,
            AutoSize = false, Size = new Size(360, 110)
        };
        _izquierda.Controls.AddRange(new Control[] { logo, titulo, lema });
        _izquierda.Resize += (_, _) =>
        {
            var bloque = 76 + 24 + titulo.Height + 14 + lema.Height;
            var y = Math.Max(40, (_izquierda.Height - bloque) / 2);
            logo.Location = new Point(70, y);
            titulo.Location = new Point(66, y + 76 + 20);
            lema.Location = new Point(70, titulo.Bottom + 12);
            lema.Width = Math.Max(200, _izquierda.Width - 140);
        };

        // ---------- Panel derecho ----------
        _derecha.Dock = DockStyle.Fill;
        _derecha.BackColor = Tema.Fondo;

        const int margen = 38;
        var ancho = _tarjeta.Width - margen * 2 - 8;
        var y0 = 36;

        _tarjeta.BackColor = Color.White;      // los controles de dentro pintan su fondo con este color
        _tarjeta.ColorBorde = Color.Transparent;
        var tit = new Label
        {
            Text = "Ingresar como administrador", ForeColor = Tema.PrimarioOscuro, Font = Tema.Fuente(17f, FontStyle.Bold),
            AutoSize = false, Bounds = new Rectangle(margen, y0, ancho, 36), BackColor = Color.Transparent
        };
        y0 += 52;
        var lblCorreo = Etiqueta("Correo", margen, y0, ancho); y0 += 24;
        _correo.Bounds = new Rectangle(margen, y0, ancho, 50); y0 += 50 + 16;
        var lblClave = Etiqueta("Contraseña", margen, y0, ancho); y0 += 24;
        _clave.Bounds = new Rectangle(margen, y0, ancho, 50); y0 += 50 + 14;

        _recordar.Text = "Recordar sesión en esta PC";
        _recordar.ForeColor = Tema.Texto;
        _recordar.BackColor = Color.Transparent;
        _recordar.AutoSize = true;
        _recordar.Checked = true;
        _recordar.Cursor = Cursors.Hand;
        _recordar.Location = new Point(margen, y0);
        y0 += 34;

        _error.ForeColor = Tema.Error;
        _error.BackColor = Color.Transparent;
        _error.Font = Tema.Fuente(9.5f, FontStyle.Bold);
        _error.AutoSize = false;
        _error.Bounds = new Rectangle(margen, y0, ancho, 24);
        _error.Visible = false;
        y0 += 28;

        _ingresar.Bounds = new Rectangle(margen, y0, ancho, 52);
        _ingresar.Click += async (_, _) => await IngresarAsync();
        y0 += 52 + 14;

        _estado.ForeColor = Tema.TextoSecundario;
        _estado.BackColor = Color.Transparent;
        _estado.AutoSize = false;
        _estado.TextAlign = ContentAlignment.MiddleCenter;
        _estado.Font = Tema.Fuente(9.5f);
        _estado.Bounds = new Rectangle(margen, y0, ancho, 20);
        y0 += 24;

        var nota = new Label
        {
            Text = "Es el mismo usuario que usas en el celular. Solo el administrador puede ver los pagos.",
            ForeColor = Tema.TextoSecundario, Font = Tema.Fuente(9f), BackColor = Color.Transparent,
            AutoSize = false, TextAlign = ContentAlignment.TopCenter, Bounds = new Rectangle(margen, y0, ancho, 40)
        };

        _tarjeta.Controls.AddRange(new Control[] { tit, lblCorreo, _correo, lblClave, _clave, _recordar, _error, _ingresar, _estado, nota });
        _derecha.Controls.Add(_tarjeta);

        Controls.Add(_derecha);
        Controls.Add(_izquierda);
        CentrarTarjeta();
    }

    private static Label Etiqueta(string texto, int x, int y, int ancho) => new()
    {
        Text = texto, ForeColor = Tema.TextoSecundario, Font = Tema.Fuente(9.5f, FontStyle.Bold),
        AutoSize = false, Bounds = new Rectangle(x, y, ancho, 22), BackColor = Color.Transparent
    };

    private int _desplazamientoX;

    private void CentrarTarjeta()
    {
        // Entrada: la tarjeta sube suavemente; error: se sacude lateralmente.
        var subida = (int)((1f - Mezcla.EaseOutCubic(_entrada.Valor)) * 34);
        _tarjeta.Location = new Point(
            Math.Max(0, (_derecha.ClientSize.Width - _tarjeta.Width) / 2) + _desplazamientoX,
            Math.Max(0, (_derecha.ClientSize.Height - _tarjeta.Height) / 2) + subida);
    }

    private void Temblar()
    {
        _pasoTemblor++;
        _desplazamientoX = (int)(Math.Sin(_pasoTemblor * 1.3) * 12 * Math.Pow(0.86, _pasoTemblor));
        if (_pasoTemblor > 26) { _temblor.Stop(); _desplazamientoX = 0; }
        CentrarTarjeta();
    }

    private void Ocupado(bool si, string? mensaje = null)
    {
        _ingresar.Enabled = !si;
        _correo.Enabled = !si;
        _clave.Enabled = !si;
        _estado.Text = mensaje ?? "";
        UseWaitCursor = si;
    }

    private async Task IntentarRestaurarAsync()
    {
        Ocupado(true, "Restaurando sesión…");
        var ok = await _servicio.RestaurarSesionAsync();
        Ocupado(false);
        if (ok) { DialogResult = DialogResult.OK; Close(); return; }

        if (_silencioso)   // no hay sesion guardada: ahora si se muestra el login
        {
            Opacity = 1; ShowInTaskbar = true; Activate();
        }
        _correo.Caja.Focus();
    }

    private async Task IngresarAsync()
    {
        _error.Visible = false;
        if (string.IsNullOrWhiteSpace(_correo.Text) || string.IsNullOrEmpty(_clave.Text))
        {
            MostrarError("Ingresa tu correo y contraseña.");
            return;
        }

        Ocupado(true, "Ingresando…");
        var r = await _servicio.LoginAsync(_correo.Text, _clave.Text, _recordar.Checked);
        _clave.Caja.Clear(); // la contrasena no se conserva en memoria de la UI
        Ocupado(false);

        switch (r)
        {
            case ResultadoLogin.Ok:
                DialogResult = DialogResult.OK;
                Close();
                break;
            case ResultadoLogin.SinConexion:
                MostrarError("No se pudo conectar. Revisa tu internet.");
                break;
            default: // credenciales invalidas u otro error: mensaje generico
                MostrarError("Correo o contraseña incorrectos.");
                break;
        }
    }

    private void MostrarError(string mensaje)
    {
        _error.Text = mensaje;
        _error.Visible = true;
        _pasoTemblor = 0;
        _temblor.Start();
    }
}
