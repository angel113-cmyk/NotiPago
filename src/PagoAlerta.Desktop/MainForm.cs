using PagoAlerta.Core;
using PagoAlerta.Core.Services;
using PagoAlerta.Desktop.Ui;

namespace PagoAlerta.Desktop;

/// <summary>Pantalla principal (Sprint 1: vacia). Encabezado azul con "Total de hoy".</summary>
public class MainForm : Form
{
    private readonly PagoService _servicio;

    public bool CerrarSesionSolicitado { get; private set; }

    public MainForm(PagoService servicio)
    {
        _servicio = servicio;
        Text = AppInfo.Nombre;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente(10f);
        ClientSize = new Size(960, 640);
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.CenterScreen;

        var encabezado = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = Tema.Primario };

        var total = new Label
        {
            Text = "Total de hoy", ForeColor = Color.FromArgb(210, 255, 255, 255),
            Font = Tema.Fuente(11f), AutoSize = true, Location = new Point(32, 28), BackColor = Color.Transparent
        };
        var monto = new Label
        {
            Text = Formato.Monto(0), ForeColor = Color.White,
            Font = Tema.Fuente(32f, FontStyle.Bold), AutoSize = true, Location = new Point(28, 54), BackColor = Color.Transparent
        };
        var cantidad = new Label
        {
            Text = "0 pagos", ForeColor = Color.FromArgb(210, 255, 255, 255),
            Font = Tema.Fuente(11f), AutoSize = true, Location = new Point(32, 112), BackColor = Color.Transparent
        };
        var usuario = new Label
        {
            Text = _servicio.CorreoActual ?? "", ForeColor = Color.FromArgb(210, 255, 255, 255),
            Font = Tema.Fuente(9.5f), AutoSize = true, BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        var salir = new Button
        {
            Text = "Cerrar sesión", FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
            BackColor = Tema.PrimarioOscuro, Cursor = Cursors.Hand, Size = new Size(130, 36),
            Font = Tema.Fuente(9.5f, FontStyle.Bold), Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        salir.FlatAppearance.BorderSize = 0;
        salir.Click += async (_, _) =>
        {
            await _servicio.CerrarSesionAsync();
            CerrarSesionSolicitado = true;
            Close();
        };
        encabezado.Controls.AddRange(new Control[] { total, monto, cantidad, usuario, salir });
        encabezado.Resize += (_, _) =>
        {
            salir.Location = new Point(encabezado.Width - salir.Width - 32, 24);
            usuario.Location = new Point(encabezado.Width - usuario.Width - 32, 70);
        };

        var vacio = new Label
        {
            Text = "Aún no hay pagos.\nCuando el celular detecte uno, aparecerá aquí.",
            ForeColor = Tema.TextoSecundario, Font = Tema.Fuente(12f),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
        };

        Controls.Add(vacio);
        Controls.Add(encabezado);
    }
}
