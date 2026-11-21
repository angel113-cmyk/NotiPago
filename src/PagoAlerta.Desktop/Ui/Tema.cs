using System.Drawing.Drawing2D;

namespace PagoAlerta.Desktop.Ui;

/// <summary>Paleta y fuentes (estilo azul). Colores definidos una sola vez.</summary>
public static class Tema
{
    public static readonly Color Primario = ColorTranslator.FromHtml("#1F4FE0");
    public static readonly Color PrimarioOscuro = ColorTranslator.FromHtml("#0B2A8F");
    public static readonly Color AzulClaro = ColorTranslator.FromHtml("#EAF0FF");
    public static readonly Color Acento = ColorTranslator.FromHtml("#19C6E0");
    public static readonly Color Fondo = ColorTranslator.FromHtml("#F5F7FB");
    public static readonly Color Tarjeta = Color.White;
    public static readonly Color Texto = ColorTranslator.FromHtml("#1B1F2A");
    public static readonly Color TextoSecundario = ColorTranslator.FromHtml("#6B7280");
    public static readonly Color Recibido = ColorTranslator.FromHtml("#16A34A");
    public static readonly Color Enviado = ColorTranslator.FromHtml("#374151");
    public static readonly Color Error = ColorTranslator.FromHtml("#DC2626");
    public static readonly Color Borde = ColorTranslator.FromHtml("#D9DFEC");

    public const int RadioTarjeta = 16;
    public const int RadioCampo = 12;

    public static Font Fuente(float tam, FontStyle estilo = FontStyle.Regular) => new("Segoe UI", tam, estilo);

    public static GraphicsPath Redondeado(Rectangle r, int radio)
    {
        var d = radio * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
