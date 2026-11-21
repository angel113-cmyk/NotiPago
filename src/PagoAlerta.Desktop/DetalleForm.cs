using PagoAlerta.Core;
using PagoAlerta.Core.Models;
using PagoAlerta.Core.Time;
using PagoAlerta.Desktop.Ui;

namespace PagoAlerta.Desktop;

/// <summary>HU-06: detalle de un pago (fecha y hora, nombre, monto, app, tipo, paquete y texto original).</summary>
public class DetalleForm : Form
{
    public DetalleForm(Pago p)
    {
        Text = "Detalle del pago";
        BackColor = Tema.Fondo;
        Font = Tema.Fuente(10f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 640);

        var cab = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Tema.Primario };
        var colorMonto = Color.White;
        cab.Controls.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(p.Remitente) ? "(sin nombre)" : p.Remitente,
            ForeColor = Color.FromArgb(220, 255, 255, 255), Font = Tema.Fuente(12f),
            AutoSize = false, Bounds = new Rectangle(28, 22, 464, 26), BackColor = Color.Transparent,
            AutoEllipsis = true
        });
        cab.Controls.Add(new Label
        {
            Text = Formato.Monto(p.Monto, p.Moneda), ForeColor = colorMonto, Font = Tema.Fuente(30f, FontStyle.Bold),
            AutoSize = false, Bounds = new Rectangle(24, 50, 470, 56), BackColor = Color.Transparent
        });
        if (p.EsPrueba)
        {
            var etiqueta = new Label
            {
                Text = "PRUEBA", ForeColor = Color.White, BackColor = Tema.Acento, TextAlign = ContentAlignment.MiddleCenter,
                Font = Tema.Fuente(8.5f, FontStyle.Bold), Bounds = new Rectangle(412, 24, 80, 24)
            };
            cab.Controls.Add(etiqueta);
        }

        var tarjeta = new PanelRedondeado { Sombra = true, Bounds = new Rectangle(24, 150, 472, 380) };
        var y = 18;
        void Fila(string titulo, string valor, int alto = 30)
        {
            tarjeta.Controls.Add(new Label
            {
                Text = titulo, ForeColor = Tema.TextoSecundario, Font = Tema.Fuente(9f),
                AutoSize = false, Bounds = new Rectangle(20, y, 120, 22), BackColor = Color.Transparent
            });
            tarjeta.Controls.Add(new Label
            {
                Text = valor, ForeColor = Tema.Texto, Font = Tema.Fuente(10f, FontStyle.Bold),
                AutoSize = false, Bounds = new Rectangle(140, y, 310, alto), BackColor = Color.Transparent,
                AutoEllipsis = true
            });
            y += alto + 8;
        }

        var lima = ZonaLima.DeUtc(p.FechaHora);
        Fila("Fecha", lima.ToString("dddd dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-PE")));
        Fila("Hora (Lima)", lima.ToString("hh:mm:ss tt", System.Globalization.CultureInfo.InvariantCulture));
        Fila("Nombre", p.Remitente ?? "—");
        Fila("App", p.App ?? "—");
        Fila("Tipo", p.Tipo);
        Fila("Paquete", p.Paquete ?? "—");
        Fila("Título original", p.TituloOriginal ?? "—");

        tarjeta.Controls.Add(new Label
        {
            Text = "Texto original de la notificación", ForeColor = Tema.TextoSecundario, Font = Tema.Fuente(9f),
            AutoSize = false, Bounds = new Rectangle(20, y, 400, 22), BackColor = Color.Transparent
        });
        y += 26;
        var texto = new TextBox
        {
            Text = p.TextoOriginal ?? "", ReadOnly = true, Multiline = true, BorderStyle = BorderStyle.None,
            BackColor = Tema.AzulClaro, ForeColor = Tema.Texto, Font = Tema.Fuente(10f),
            Bounds = new Rectangle(20, y, 432, tarjeta.Height - y - 18)
        };
        tarjeta.Controls.Add(texto);

        var cerrar = new BotonPrimario { Text = "Cerrar", Bounds = new Rectangle(24, 560, 472, 48) };
        cerrar.Click += (_, _) => Close();
        AcceptButton = cerrar;
        CancelButton = cerrar;

        Controls.Add(tarjeta);
        Controls.Add(cerrar);
        Controls.Add(cab);
    }
}
