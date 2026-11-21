using System.ComponentModel;
using System.Drawing.Drawing2D;
using PagoAlerta.Core;

namespace PagoAlerta.Desktop.Ui;

/// <summary>Base: control dibujado a mano, sin parpadeo, que pinta primero el fondo de su contenedor.</summary>
public abstract class ControlDibujado : Control
{
    protected ControlDibujado()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
    }

    protected void PintarFondoPadre(Graphics g)
    {
        // El fondo real es el del primer contenedor con color opaco (los paneles transparentes se saltan).
        Control? p = Parent;
        while (p is not null && p.BackColor.A == 0) p = p.Parent;
        using var b = new SolidBrush(p?.BackColor ?? Tema.Fondo);
        g.FillRectangle(b, ClientRectangle);
    }

    protected static GraphicsPath Pildora(Rectangle r)
    {
        var d = Math.Min(r.Height, r.Width);
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 90, 180);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        p.CloseFigure();
        return p;
    }
}

/// <summary>Chip (pildora) seleccionable: filtros de periodo y de app. Hover y seleccion animados.</summary>
public class Chip : ControlDibujado
{
    public enum Estilo { Relleno, Contorno }

    private readonly Suave _hover = new(), _sel = new(), _press = new(factor: 0.4f);
    private bool _seleccionado;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Estilo Variante { get; set; } = Estilo.Relleno;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Seleccionado
    {
        get => _seleccionado;
        set { if (_seleccionado == value) return; _seleccionado = value; _sel.Hacia(value ? 1f : 0f); }
    }

    public Chip(string texto, Estilo variante = Estilo.Relleno)
    {
        Text = texto;
        Variante = variante;
        Font = Tema.Fuente(10f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        Height = 38;
        _hover.Cambio += Invalidate; _sel.Cambio += Invalidate; _press.Cambio += Invalidate;
        AjustarAncho();
    }

    private void AjustarAncho() =>
        Width = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + 28;

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); AjustarAncho(); }
    protected override void OnMouseEnter(EventArgs e) { _hover.Hacia(1f); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover.Hacia(0f); _press.Hacia(0f); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _press.Hacia(1f); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _press.Hacia(0f); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        PintarFondoPadre(g);

        var h = _hover.Valor; var s = _sel.Valor; var p = _press.Valor;
        Color fondoNo, bordeNo, textoNo = Tema.Primario;
        if (Variante == Estilo.Relleno)
        {
            fondoNo = Mezcla.De(Tema.AzulClaro, Color.FromArgb(0xD6, 0xE2, 0xFF), h);
            bordeNo = fondoNo;
        }
        else
        {
            fondoNo = Mezcla.De(Color.White, Tema.AzulClaro, h);
            bordeNo = Tema.Primario;
        }
        var fondoSi = Mezcla.De(Tema.Primario, Tema.PrimarioOscuro, Math.Max(h * 0.35f, p * 0.6f));
        var fondo = Mezcla.De(fondoNo, fondoSi, s);
        var borde = Mezcla.De(bordeNo, fondoSi, s);
        var texto = Mezcla.De(textoNo, Color.White, s);

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        // Efecto "presionado": el chip se encoge un poco.
        var enc = (int)(p * 2);
        r.Inflate(-enc, -enc);
        using var path = Pildora(r);
        using (var b = new SolidBrush(fondo)) g.FillPath(b, path);
        using (var pen = new Pen(borde)) g.DrawPath(pen, path);
        TextRenderer.DrawText(g, Text, Font, r, texto,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _hover.Dispose(); _sel.Dispose(); _press.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>Boton en forma de pildora con hover y presionado animados.</summary>
public class BotonPildora : ControlDibujado
{
    public enum Estilo { Primario, Verde, Contorno, ContornoBlanco }
    public enum IconoBoton { Ninguno, Descargar, Mas }

    private readonly Suave _hover = new(), _press = new(factor: 0.45f);

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Estilo Variante { get; set; } = Estilo.Primario;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconoBoton Icono { get; set; } = IconoBoton.Ninguno;

    public BotonPildora(string texto, Estilo variante = Estilo.Primario, IconoBoton icono = IconoBoton.Ninguno, int alto = 40)
    {
        Text = texto; Variante = variante; Icono = icono;
        Font = Tema.Fuente(10f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        Height = alto;
        _hover.Cambio += Invalidate; _press.Cambio += Invalidate;
        AjustarAncho();
    }

    public void AjustarAncho()
    {
        var t = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
        Width = t + 36 + (Icono == IconoBoton.Descargar ? 24 : 0) + (string.IsNullOrEmpty(Text) ? -20 : 0);
        if (Icono == IconoBoton.Mas && string.IsNullOrEmpty(Text)) Width = Height;
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); AjustarAncho(); }
    protected override void OnMouseEnter(EventArgs e) { _hover.Hacia(1f); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover.Hacia(0f); _press.Hacia(0f); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _press.Hacia(1f); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _press.Hacia(0f); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        PintarFondoPadre(g);
        float h = _hover.Valor, p = _press.Valor;

        Color baseC, hoverC, texto, borde;
        switch (Variante)
        {
            case Estilo.Verde:
                baseC = Tema.Recibido; hoverC = Color.FromArgb(0x12, 0x84, 0x3C); texto = Color.White; borde = baseC; break;
            case Estilo.Contorno:
                baseC = Color.White; hoverC = Tema.AzulClaro; texto = Tema.Primario; borde = Tema.Primario; break;
            case Estilo.ContornoBlanco:
                baseC = Color.FromArgb(0, 255, 255, 255); hoverC = Color.FromArgb(40, 255, 255, 255); texto = Color.White;
                borde = Color.FromArgb(200, 255, 255, 255); break;
            default:
                baseC = Tema.Primario; hoverC = Tema.PrimarioOscuro; texto = Color.White; borde = baseC; break;
        }
        var fondo = Mezcla.De(baseC, hoverC, Math.Max(h, p));
        if (!Enabled) { fondo = Mezcla.De(fondo, Color.White, 0.55f); texto = Mezcla.De(texto, Color.Gray, 0.4f); }

        var enc = (int)(p * 1.5f);
        var r = new Rectangle(enc, enc, Width - 1 - enc * 2, Height - 1 - enc * 2);
        using var path = Pildora(r);
        // Sombra suave solo en botones rellenos
        if (Variante is Estilo.Primario or Estilo.Verde && Enabled)
        {
            var sr = new Rectangle(r.X + 1, r.Y + 2, r.Width - 2, r.Height - 1);
            using var sp = Pildora(sr);
            using var sb = new SolidBrush(Color.FromArgb((int)(28 + h * 18), baseC));
            g.FillPath(sb, sp);
        }
        using (var b = new SolidBrush(fondo)) g.FillPath(b, path);
        using (var pen = new Pen(borde)) g.DrawPath(pen, path);

        var areaTexto = r;
        if (Icono == IconoBoton.Descargar)
        {
            var ix = r.X + 18; var iy = r.Y + (r.Height - 14) / 2;
            using var pen = new Pen(texto, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLine(pen, ix + 7, iy, ix + 7, iy + 9);
            g.DrawLines(pen, new[] { new Point(ix + 3, iy + 6), new Point(ix + 7, iy + 10), new Point(ix + 11, iy + 6) });
            g.DrawLine(pen, ix, iy + 13, ix + 14, iy + 13);
            areaTexto = new Rectangle(r.X + 30, r.Y, r.Width - 30, r.Height);
        }
        if (Icono == IconoBoton.Mas)
        {
            using var b = new SolidBrush(texto);
            var cx = r.X + r.Width / 2f; var cy = r.Y + r.Height / 2f;
            for (var i = -1; i <= 1; i++) g.FillEllipse(b, cx + i * 7 - 2f, cy - 2f, 4f, 4f);
            return;
        }
        TextRenderer.DrawText(g, Text, Font, areaTexto, texto,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _hover.Dispose(); _press.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>Tarjeta de resumen: titulo, valor grande (con conteo animado) y nota.</summary>
public class TarjetaStat : ControlDibujado
{
    private readonly Suave _anim = new(factor: 0.12f);
    private decimal _desde, _hasta;
    private Func<decimal, string> _formato = v => v.ToString("0");
    private string _valorTexto = "—";

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Oscura { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Titulo { get; set; } = "";
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Nota { get; set; } = "";

    public TarjetaStat(string titulo, string nota, bool oscura)
    {
        Titulo = titulo; Nota = nota; Oscura = oscura;
        Height = 112;
        _anim.Cambio += () =>
        {
            var v = _desde + (_hasta - _desde) * (decimal)_anim.Valor;
            _valorTexto = _formato(_anim.Valor >= 0.999f ? _hasta : v);
            Invalidate();
        };
    }

    /// <summary>El numero "cuenta" desde el valor anterior hasta el nuevo.</summary>
    public void AnimarNumero(decimal destino, Func<decimal, string> formato)
    {
        _formato = formato;
        _desde = _anim.Valor >= 0.999f || _valorTexto == "—" ? (_valorTexto == "—" ? 0 : _hasta) : _desde + (_hasta - _desde) * (decimal)_anim.Valor;
        _hasta = destino;
        _anim.Fijar(0f);
        _anim.Hacia(1f);
    }

    public void MostrarTexto(string valor) { _anim.Fijar(1f); _valorTexto = valor; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        PintarFondoPadre(g);

        var r = new Rectangle(0, 0, Width - 6, Height - 8);
        using (var sp = Tema.Redondeado(new Rectangle(2, 4, r.Width - 3, r.Height - 2), Tema.RadioTarjeta))
        using (var sb = new SolidBrush(Color.FromArgb(Oscura ? 40 : 16, 11, 42, 143)))
            g.FillPath(sb, sp);

        using var path = Tema.Redondeado(r, Tema.RadioTarjeta);
        using (var fill = new SolidBrush(Oscura ? Tema.PrimarioOscuro : Color.White)) g.FillPath(fill, path);
        if (!Oscura) using (var pen = new Pen(Tema.Borde)) g.DrawPath(pen, path);

        var claro = Oscura ? Color.FromArgb(215, 255, 255, 255) : Tema.TextoSecundario;
        var valor = Oscura ? Color.White : Tema.PrimarioOscuro;
        TextRenderer.DrawText(g, Titulo, Tema.Fuente(10.5f), new Rectangle(22, 16, r.Width - 30, 22), claro,
            TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, _valorTexto, Tema.Fuente(24f, FontStyle.Bold), new Rectangle(20, 38, r.Width - 28, 40), valor,
            TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, Nota, Tema.Fuente(9f), new Rectangle(22, r.Height - 28, r.Width - 30, 18), claro,
            TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    protected override void Dispose(bool disposing) { if (disposing) _anim.Dispose(); base.Dispose(disposing); }
}

/// <summary>Logo propio: cuadrado redondeado con campana. Claro = fondo blanco y campana azul (barra superior y login).</summary>
public class LogoCampana : ControlDibujado
{
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Claro { get; set; } = true;

    public LogoCampana(int tam = 44) { Size = new Size(tam, tam); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        PintarFondoPadre(g);
        float s = Math.Min(Width, Height) - 1;
        using var caja = Tema.Redondeado(new Rectangle(0, 0, (int)s, (int)s), (int)(s * 0.28));
        using (var b = new SolidBrush(Claro ? Color.White : Tema.Primario)) g.FillPath(b, caja);

        var trazo = Claro ? Tema.Primario : Color.White;
        using var pen = new Pen(trazo, Math.Max(2f, s * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        float cx = s / 2f;
        using var campana = new GraphicsPath();
        campana.AddArc(cx - s * 0.20f, s * 0.20f, s * 0.40f, s * 0.40f, 180, 180);
        campana.AddLine(cx + s * 0.20f, s * 0.40f, cx + s * 0.25f, s * 0.60f);
        campana.AddLine(cx + s * 0.25f, s * 0.60f, cx - s * 0.25f, s * 0.60f);
        campana.AddLine(cx - s * 0.25f, s * 0.60f, cx - s * 0.20f, s * 0.40f);
        g.DrawPath(pen, campana);
        g.DrawArc(pen, cx - s * 0.07f, s * 0.64f, s * 0.14f, s * 0.12f, 0, 180);
    }
}
