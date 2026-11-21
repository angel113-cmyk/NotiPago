using PagoAlerta.Core;
using PagoAlerta.Core.Services;
using PagoAlerta.Desktop.Ui;

namespace PagoAlerta.Desktop;

/// <summary>HU-01: login del admin. Error generico, "Recordar sesion", sin guardar contrasena.</summary>
public class LoginForm : Form
{
    private readonly PagoService _servicio;
    private readonly PanelRedondeado _tarjeta = new() { Sombra = true, Size = new Size(420, 520) };
    private readonly CampoRedondeado _correo = new("Correo electrónico");
    private readonly CampoRedondeado _clave = new("Contraseña", clave: true);
    private readonly CheckBox _recordar = new();
    private readonly Label _error = new();
    private readonly Label _estado = new();
    private readonly BotonPrimario _ingresar = new() { Text = "Ingresar" };

    public LoginForm(PagoService servicio)
    {
        _servicio = servicio;
        Text = AppInfo.Nombre;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente(10f);
        ClientSize = new Size(520, 640);
        MinimumSize = new Size(480, 620);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        Construir();
        Resize += (_, _) => CentrarTarjeta();
        Shown += async (_, _) => await IntentarRestaurarAsync();
    }

    private void Construir()
    {
        const int margen = 32;
        var ancho = _tarjeta.Width - margen * 2;
        var y = 36;

        var logo = new LogoPagoAlerta { Location = new Point((_tarjeta.Width - 72) / 2, y) };
        y += 72 + 14;

        var titulo = new Label
        {
            Text = AppInfo.Nombre, Font = Tema.Fuente(20f, FontStyle.Bold), ForeColor = Tema.Texto,
            AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(margen, y, ancho, 38), BackColor = Color.Transparent
        };
        y += 38;
        var sub = new Label
        {
            Text = "Inicia sesión para ver tus pagos", Font = Tema.Fuente(10f), ForeColor = Tema.TextoSecundario,
            AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(margen, y, ancho, 24), BackColor = Color.Transparent
        };
        y += 24 + 22;

        _correo.Bounds = new Rectangle(margen, y, ancho, 48); y += 48 + 12;
        _clave.Bounds = new Rectangle(margen, y, ancho, 48); y += 48 + 10;

        _recordar.Text = "Recordar sesión";
        _recordar.ForeColor = Tema.TextoSecundario;
        _recordar.BackColor = Color.Transparent;
        _recordar.AutoSize = true;
        _recordar.Checked = true;
        _recordar.Location = new Point(margen, y);
        y += 30;

        _error.ForeColor = Tema.Error;
        _error.BackColor = Color.Transparent;
        _error.Font = Tema.Fuente(9.5f);
        _error.AutoSize = false;
        _error.Bounds = new Rectangle(margen, y, ancho, 24);
        _error.Visible = false;
        y += 28;

        _ingresar.Bounds = new Rectangle(margen, y, ancho, 48);
        _ingresar.Click += async (_, _) => await IngresarAsync();
        y += 48 + 10;

        _estado.ForeColor = Tema.TextoSecundario;
        _estado.BackColor = Color.Transparent;
        _estado.AutoSize = false;
        _estado.TextAlign = ContentAlignment.MiddleCenter;
        _estado.Bounds = new Rectangle(margen, y, ancho, 22);

        _tarjeta.Controls.AddRange(new Control[] { logo, titulo, sub, _correo, _clave, _recordar, _error, _ingresar, _estado });
        _tarjeta.ColorBorde = Color.Transparent;
        Controls.Add(_tarjeta);
        AcceptButton = _ingresar;
        CentrarTarjeta();
    }

    private void CentrarTarjeta() =>
        _tarjeta.Location = new Point(Math.Max(0, (ClientSize.Width - _tarjeta.Width) / 2),
                                      Math.Max(0, (ClientSize.Height - _tarjeta.Height) / 2));

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
        if (ok) { DialogResult = DialogResult.OK; Close(); }
        else _correo.Caja.Focus();
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
    }
}
