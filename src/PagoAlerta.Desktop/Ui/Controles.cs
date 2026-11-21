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
        Control? p = Parent;
        while (p is not null && p.BackColor.A == 0) p = p.Parent;
        using var b = new SolidBrush(p?.BackColor ?? Tema.Fondo);
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

    private readonly Suave _foco = new(0f, 0.3f);

    public CampoRedondeado(string placeholder, bool clave = false, int radio = Tema.RadioCampo, int alto = 48)
    {
        Radio = radio;
        Height = alto;
        Padding = new Padding(14, 0, 14, 0);
        _foco.Cambio += () => { ColorBorde = Mezcla.De(Tema.Borde, Tema.Primario, _foco.Valor); Invalidate(); };
        Caja.BorderStyle = BorderStyle.None;
        Caja.Font = Tema.Fuente(alto <= 40 ? 10f : 11f);
        Caja.ForeColor = Tema.Texto;
        Caja.BackColor = Tema.Tarjeta;
        Caja.PlaceholderText = placeholder;
        Caja.UseSystemPasswordChar = clave;
        Caja.Left = 14;
        Caja.Width = Width - 28;
        Caja.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(Caja);
        Caja.GotFocus += (_, _) => _foco.Hacia(1f);
        Caja.LostFocus += (_, _) => _foco.Hacia(0f);
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
