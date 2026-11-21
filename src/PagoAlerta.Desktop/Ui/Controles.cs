using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace PagoAlerta.Desktop.Ui;

/// <summary>Panel con esquinas redondeadas, fondo propio y borde/sombra suave.</summary>
public class PanelRedondeado : Panel
{
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radio { get; set; } = Tema.RadioTarjeta;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ColorFondo { get; set; } = Tema.Tarjeta;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ColorBorde { get; set; } = Tema.Borde;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Sombra { get; set; }

    public PanelRedondeado()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Pinta el fondo del padre para que las esquinas queden limpias.
        using var b = new SolidBrush(Parent?.BackColor ?? Tema.Fondo);
        e.Graphics.FillRectangle(b, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (Sombra)
        {
            var s = new Rectangle(2, 3, Width - 5, Height - 5);
            using var sp = Tema.Redondeado(s, Radio);
            using var sb = new SolidBrush(Color.FromArgb(18, 11, 42, 143));
            g.FillPath(sb, sp);
            rect = new Rectangle(0, 0, Width - 5, Height - 6);
        }
        using var path = Tema.Redondeado(rect, Radio);
        using var fill = new SolidBrush(ColorFondo);
        g.FillPath(fill, path);
        if (ColorBorde != Color.Transparent)
        {
            using var pen = new Pen(ColorBorde);
            g.DrawPath(pen, path);
        }
    }
}

/// <summary>Boton principal: azul, texto blanco, ancho y redondeado; mas oscuro al presionar.</summary>
public class BotonPrimario : Button
{
    private bool _encima, _presionado;

    public BotonPrimario()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = Tema.Fuente(11f, FontStyle.Bold);
        Height = 48;
    }

    protected override void OnMouseEnter(EventArgs e) { _encima = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _encima = false; _presionado = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _presionado = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _presionado = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new SolidBrush(Parent?.BackColor ?? Tema.Fondo)) g.FillRectangle(bg, ClientRectangle);

        var color = !Enabled ? Color.FromArgb(150, Tema.Primario)
                  : _presionado ? Tema.PrimarioOscuro
                  : _encima ? ControlPaint.Light(Tema.Primario, 0.05f) : Tema.Primario;

        using var path = Tema.Redondeado(new Rectangle(0, 0, Width - 1, Height - 1), Tema.RadioCampo);
        using var fill = new SolidBrush(color);
        g.FillPath(fill, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>Campo de texto con borde redondeado y etiqueta flotante arriba.</summary>
public class CampoRedondeado : PanelRedondeado
{
    public TextBox Caja { get; } = new();

    public CampoRedondeado(string placeholder, bool clave = false)
    {
        Radio = Tema.RadioCampo;
        Height = 48;
        Padding = new Padding(14, 0, 14, 0);
        Caja.BorderStyle = BorderStyle.None;
        Caja.Font = Tema.Fuente(11f);
        Caja.ForeColor = Tema.Texto;
        Caja.BackColor = Tema.Tarjeta;
        Caja.PlaceholderText = placeholder;
        Caja.UseSystemPasswordChar = clave;
        Caja.Left = 14;
        Caja.Width = Width - 28;
        Caja.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(Caja);
        Caja.GotFocus += (_, _) => { ColorBorde = Tema.Primario; Invalidate(); };
        Caja.LostFocus += (_, _) => { ColorBorde = Tema.Borde; Invalidate(); };
        Click += (_, _) => Caja.Focus();
        Resize += (_, _) => Centrar();
        Centrar();
    }

    private void Centrar()
    {
        Caja.Width = Math.Max(10, Width - 28);
        Caja.Top = Math.Max(0, (Height - Caja.Height) / 2);
    }

    [AllowNull]
    public override string Text { get => Caja.Text; set => Caja.Text = value ?? ""; }
}

/// <summary>Logo propio: cuadrado azul redondeado con campana y "S/" (no usa marcas de terceros).</summary>
public class LogoPagoAlerta : Control
{
    public LogoPagoAlerta()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Size = new Size(72, 72);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new SolidBrush(Parent?.BackColor ?? Tema.Fondo)) g.FillRectangle(bg, ClientRectangle);

        var s = Math.Min(Width, Height) - 1;
        using var caja = Tema.Redondeado(new Rectangle(0, 0, s, s), (int)(s * 0.26));
        using (var b = new SolidBrush(Tema.Primario)) g.FillPath(b, caja);

        // Campana (blanca) en la parte superior.
        float cx = s / 2f, top = s * 0.14f;
        using var campana = new GraphicsPath();
        campana.AddArc(cx - s * 0.19f, top, s * 0.38f, s * 0.38f, 180, 180);
        campana.AddLine(cx + s * 0.19f, top + s * 0.19f, cx + s * 0.23f, top + s * 0.40f);
        campana.AddLine(cx + s * 0.23f, top + s * 0.40f, cx - s * 0.23f, top + s * 0.40f);
        campana.AddLine(cx - s * 0.23f, top + s * 0.40f, cx - s * 0.19f, top + s * 0.19f);
        campana.CloseFigure();
        using (var w = new SolidBrush(Color.White)) g.FillPath(w, campana);
        using (var w = new SolidBrush(Color.White))
            g.FillEllipse(w, cx - s * 0.05f, top + s * 0.42f, s * 0.10f, s * 0.07f);

        // Punto de notificacion cian.
        using (var c = new SolidBrush(Tema.Acento))
            g.FillEllipse(c, cx + s * 0.12f, top - s * 0.02f, s * 0.13f, s * 0.13f);

        // "S/" debajo.
        using var f = Tema.Fuente(s * 0.20f, FontStyle.Bold);
        var rect = new Rectangle(0, (int)(s * 0.62f), s, (int)(s * 0.32f));
        TextRenderer.DrawText(g, "S/", f, rect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

