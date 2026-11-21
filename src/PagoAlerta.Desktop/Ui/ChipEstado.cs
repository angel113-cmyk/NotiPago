using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace PagoAlerta.Desktop.Ui;

/// <summary>Pildora de estado de la barra superior: punto de color (pulsa cuando esta en vivo) + texto.</summary>
public class ChipEstado : ControlDibujado
{
    private readonly System.Windows.Forms.Timer _pulso = new() { Interval = 40 };
    private double _fase;
    private bool _activo;
    private string _texto = "";

    public ChipEstado()
    {
        Height = 34;
        Font = Tema.Fuente(9.5f, FontStyle.Bold);
        _pulso.Tick += (_, _) => { _fase += 0.18; Invalidate(); };
        Establecer(false, "Conectando…");
    }

    /// <summary>activo = Realtime conectado (verde y pulsando); si no, ambar fijo.</summary>
    public void Establecer(bool activo, string texto)
    {
        _activo = activo; _texto = texto;
        if (activo) _pulso.Start(); else _pulso.Stop();
        Width = TextRenderer.MeasureText(texto, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + 52;
        Invalidate();
    }

    protected override void Dispose(bool disposing) { if (disposing) _pulso.Dispose(); base.Dispose(disposing); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        PintarFondoPadre(g);

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Pildora(r);
        using (var b = new SolidBrush(Color.FromArgb(Tema.PrimarioOscuro.R + 28, Tema.PrimarioOscuro.G + 32, Tema.PrimarioOscuro.B + 60))) g.FillPath(b, path);

        var color = _activo ? Color.FromArgb(0x34, 0xD3, 0x99) : Color.FromArgb(0xFB, 0xBF, 0x24);
        var cy = Height / 2f;
        if (_activo)
        {
            var onda = (float)((Math.Sin(_fase) + 1) / 2);            // 0..1
            var radio = 5f + onda * 5f;
            using var halo = new SolidBrush(Color.FromArgb((int)(90 * (1 - onda)), color));
            g.FillEllipse(halo, 18 - radio, cy - radio, radio * 2, radio * 2);
        }
        using (var p = new SolidBrush(color)) g.FillEllipse(p, 14f, cy - 4f, 8f, 8f);
        TextRenderer.DrawText(g, _texto, Font, new Rectangle(30, 0, Width - 38, Height), Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
