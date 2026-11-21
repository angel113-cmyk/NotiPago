using PagoAlerta.Core;
using PagoAlerta.Desktop.Services;
using PagoAlerta.Desktop.Ui;

namespace PagoAlerta.Desktop;

/// <summary>Ajustes: sonido de pago nuevo y cerrar sesion (HU-08).</summary>
public class AjustesForm : Form
{
    public bool CerrarSesionSolicitado { get; private set; }

    public AjustesForm(Preferencias prefs, string? correo)
    {
        Text = "Ajustes";
        BackColor = Tema.Fondo;
        Font = Tema.Fuente(10f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(440, 330);

        var tarjeta = new PanelRedondeado { Sombra = true, BackColor = Color.White, Bounds = new Rectangle(24, 24, 392, 150) };
        tarjeta.Controls.Add(new Label
        {
            Text = "Sesión iniciada como", ForeColor = Tema.TextoSecundario, Font = Tema.Fuente(9f),
            AutoSize = false, Bounds = new Rectangle(20, 16, 340, 20), BackColor = Color.Transparent
        });
        tarjeta.Controls.Add(new Label
        {
            Text = correo ?? "—", ForeColor = Tema.Texto, Font = Tema.Fuente(11f, FontStyle.Bold),
            AutoSize = false, Bounds = new Rectangle(20, 38, 350, 26), BackColor = Color.Transparent, AutoEllipsis = true
        });
        var sonido = new CheckBox
        {
            Text = "Sonido cuando llegue un pago nuevo", Checked = prefs.Sonido, AutoSize = true,
            ForeColor = Tema.Texto, BackColor = Color.Transparent, Location = new Point(20, 88)
        };
        sonido.CheckedChanged += (_, _) => { prefs.Sonido = sonido.Checked; prefs.Guardar(); };
        tarjeta.Controls.Add(sonido);

        var salir = new BotonPildora("Cerrar sesión", BotonPildora.Estilo.Contorno, alto: 48) { Bounds = new Rectangle(24, 200, 392, 48) };
        salir.Click += (_, _) =>
        {
            CerrarSesionSolicitado = true;
            DialogResult = DialogResult.OK;
            Close();
        };

        var version = new Label
        {
            Text = $"{AppInfo.Nombre} v{typeof(AjustesForm).Assembly.GetName().Version?.ToString(3)}",
            ForeColor = Tema.TextoSecundario, Font = Tema.Fuente(9f), TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(24, 268, 392, 24), BackColor = Color.Transparent
        };

        Controls.AddRange(new Control[] { tarjeta, salir, version });
    }
}
