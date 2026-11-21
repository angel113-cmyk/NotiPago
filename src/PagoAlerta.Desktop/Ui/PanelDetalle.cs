using System.Drawing.Drawing2D;
using PagoAlerta.Core;
using PagoAlerta.Core.Models;
using PagoAlerta.Core.Time;

namespace PagoAlerta.Desktop.Ui;

/// <summary>
/// HU-06: detalle del pago seleccionado, en un panel a la derecha de la tabla
/// (fecha y hora, nombre, monto, app, tipo, paquete, titulo y texto original).
/// </summary>
public class PanelDetalle : ControlDibujado
{
    private readonly TextBox _texto = new()
    {
        ReadOnly = true, Multiline = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical,
        BackColor = Color.FromArgb(0xF5, 0xF7, 0xFB), ForeColor = Tema.Texto, Font = Tema.Fuente(10f)
    };
    private readonly Suave _aparecer = new(1f, 0.2f);
    private const int YTexto = 322;   // donde empieza el titulo "TEXTO ORIGINAL"
    private Pago? _pago;

    public PanelDetalle()
    {
        Controls.Add(_texto);
        _texto.Visible = false;
        _aparecer.Cambio += Invalidate;
        Resize += (_, _) => Distribuir();
    }

    public void Mostrar(Pago? pago)
    {
        _pago = pago;
        _texto.Visible = pago is not null;
        _texto.Text = pago?.TextoOriginal ?? "";
        _aparecer.Fijar(0f);
        _aparecer.Hacia(1f);   // pequeño fundido al cambiar de pago
        Distribuir();
        Invalidate();
    }

    private void Distribuir()
    {
        if (_pago is null) return;
        _texto.SetBounds(26, YTexto + 24, Width - 58, Math.Max(36, Height - YTexto - 24 - 26));
        // La barra de desplazamiento solo aparece si el texto no cabe.
        var alto = TextRenderer.MeasureText(_texto.Text, _texto.Font, new Size(_texto.Width - 8, int.MaxValue), TextFormatFlags.WordBreak).Height;
        _texto.ScrollBars = alto > _texto.Height ? ScrollBars.Vertical : ScrollBars.None;
    }

    protected override void Dispose(bool disposing) { if (disposing) _aparecer.Dispose(); base.Dispose(disposing); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        PintarFondoPadre(g);

        using (var sp = Tema.Redondeado(new Rectangle(1, 3, Width - 5, Height - 6), Tema.RadioTarjeta))
        using (var sb = new SolidBrush(Color.FromArgb(14, 11, 42, 143)))
            g.FillPath(sb, sp);
        var r = new Rectangle(0, 0, Width - 4, Height - 5);
        using var path = Tema.Redondeado(r, Tema.RadioTarjeta);
        using (var fill = new SolidBrush(Color.White)) g.FillPath(fill, path);
        using (var pen = new Pen(Tema.Borde)) g.DrawPath(pen, path);

        TextRenderer.DrawText(g, "Detalle del pago", Tema.Fuente(13f, FontStyle.Bold), new Rectangle(26, 22, Width - 60, 26),
            Tema.PrimarioOscuro, TextFormatFlags.Left | TextFormatFlags.NoPadding);

        if (_pago is null)
        {
            TextRenderer.DrawText(g, "Selecciona un pago de la lista\npara ver su detalle.", Tema.Fuente(10.5f),
                new Rectangle(20, 80, Width - 44, Height - 120), Tema.TextoSecundario,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        var p = _pago;
        var t = Mezcla.EaseOutCubic(_aparecer.Valor);
        Color Fade(Color c) => Mezcla.De(Color.White, c, t);
        var desplaza = (int)((1 - t) * 8);

        // Circulo de iniciales + nombre + monto
        var circ = new Rectangle(26, 62 + desplaza, 56, 56);
        using (var b = new SolidBrush(Fade(Tema.AzulClaro))) g.FillEllipse(b, circ);
        TextRenderer.DrawText(g, Formato.Iniciales(p.Remitente), Tema.Fuente(14f, FontStyle.Bold), circ, Fade(Tema.Primario),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        var nombre = string.IsNullOrWhiteSpace(p.Remitente) ? "(sin nombre)" : p.Remitente!;
        TextRenderer.DrawText(g, nombre, Tema.Fuente(12f, FontStyle.Bold), new Rectangle(96, 60 + desplaza, Width - 130, 28),
            Fade(Tema.Texto), TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        var colorMonto = p.Tipo == TipoPago.Recibido ? Tema.Recibido : p.Tipo == TipoPago.Enviado ? Tema.Enviado : Tema.TextoSecundario;
        var signo = p.Tipo == TipoPago.Recibido ? "+" : p.Tipo == TipoPago.Enviado ? "−" : "";
        TextRenderer.DrawText(g, signo + Formato.Monto(p.Monto, p.Moneda), Tema.Fuente(18f, FontStyle.Bold),
            new Rectangle(96, 86 + desplaza, Width - 130, 34), Fade(colorMonto), TextFormatFlags.Left | TextFormatFlags.NoPadding);

        if (p.EsPrueba)
        {
            var tag = new Rectangle(Width - 100, 24, 66, 22);
            using var tp = Tema.Redondeado(tag, 11);
            using (var tb = new SolidBrush(Tema.Acento)) g.FillPath(tb, tp);
            TextRenderer.DrawText(g, "PRUEBA", Tema.Fuente(8f, FontStyle.Bold), tag, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        // Filas de datos
        var lima = ZonaLima.DeUtc(p.FechaHora);
        var filas = new (string, string)[]
        {
            ("Fecha y hora", $"{lima:dd/MM/yyyy} · {lima:HH:mm:ss}"),
            ("App", (p.App ?? "—").ToUpperInvariant()),
            ("Tipo", p.Tipo.Length > 0 ? char.ToUpperInvariant(p.Tipo[0]) + p.Tipo[1..] : "—"),
            ("Paquete", p.Paquete ?? "—"),
            ("Título", p.TituloOriginal ?? "—")
        };
        var y = 132;
        foreach (var (k, v) in filas)
        {
            TextRenderer.DrawText(g, k, Tema.Fuente(9.5f), new Rectangle(26, y, 100, 24), Fade(Tema.TextoSecundario),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, v, Tema.Fuente(9.5f, FontStyle.Bold), new Rectangle(128, y, Width - 160, 24), Fade(Tema.Texto),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            using (var pen = new Pen(Color.FromArgb(0xEE, 0xF1, 0xF8))) g.DrawLine(pen, 26, y + 32, Width - 34, y + 32);
            y += 36;
        }

        TextRenderer.DrawText(g, "TEXTO ORIGINAL DE LA NOTIFICACIÓN", Tema.Fuente(8f, FontStyle.Bold),
            new Rectangle(26, YTexto, Width - 60, 20), Tema.TextoSecundario, TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }
}
