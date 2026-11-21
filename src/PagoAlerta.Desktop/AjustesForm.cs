using PagoAlerta.Core;
using PagoAlerta.Desktop.Services;
using PagoAlerta.Desktop.Ui;

namespace PagoAlerta.Desktop;

/// <summary>Ajustes: sonido y aviso de pago nuevo, inicio con Windows y cerrar sesion (HU-08).</summary>
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
        ClientSize = new Size(440, 400);

        var tarjeta = new PanelRedondeado { Sombra = true, BackColor = Color.White, Bounds = new Rectangle(24, 24, 392, 232) };
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

        CheckBox Casilla(string texto, bool marcada, int y, Action<bool> alCambiar)
        {
            var c = new CheckBox
            {
                Text = texto, Checked = marcada, AutoSize = true, Cursor = Cursors.Hand,
                ForeColor = Tema.Texto, BackColor = Color.Transparent, Location = new Point(20, y)
            };
            c.CheckedChanged += (_, _) => alCambiar(c.Checked);
            tarjeta.Controls.Add(c);
            return c;
        }

        Casilla("Sonido cuando llegue un pago nuevo", prefs.Sonido, 84, v => { prefs.Sonido = v; prefs.Guardar(); if (v) Sonido.Reproducir(); });
        Casilla("Recuadro emergente cuando llegue un pago", prefs.AvisoPago, 120, v => { prefs.AvisoPago = v; prefs.Guardar(); });
        Casilla("Iniciar PagoAlerta con Windows", InicioConWindows.Activo, 156, v =>
        {
            if (!InicioConWindows.Establecer(v))
                MessageBox.Show(this, "No se pudo cambiar el inicio con Windows.", AppInfo.Nombre, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        });
        tarjeta.Controls.Add(new Label
        {
            Text = "Al cerrar la ventana, PagoAlerta sigue funcionando junto al reloj de Windows.",
            ForeColor = Tema.TextoSecundario, Font = Tema.Fuente(8.5f), BackColor = Color.Transparent,
            AutoSize = false, Bounds = new Rectangle(20, 190, 350, 32)
        });

        var salir = new BotonPildora("Cerrar sesión", BotonPildora.Estilo.Contorno, alto: 48) { Bounds = new Rectangle(24, 280, 392, 48) };
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
            Bounds = new Rectangle(24, 348, 392, 24), BackColor = Color.Transparent
        };

        Controls.AddRange(new Control[] { tarjeta, salir, version });
    }
}
