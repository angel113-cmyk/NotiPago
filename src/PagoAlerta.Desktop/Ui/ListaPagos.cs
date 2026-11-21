using System.ComponentModel;
using System.Drawing.Drawing2D;
using PagoAlerta.Core;
using PagoAlerta.Core.Models;

namespace PagoAlerta.Desktop.Ui;

/// <summary>
/// Lista de pagos como tarjetas (dibujadas a mano y virtualizadas: solo se pintan las visibles).
/// Cada tarjeta: circulo con iniciales, nombre, app + fecha + hora, y el monto a la derecha.
/// </summary>
public class ListaPagos : Control
{
    private const int AltoTarjeta = 76, Espacio = 10, Margen = 24;
    private static readonly TimeSpan DuracionResalte = TimeSpan.FromSeconds(8);

    private sealed class Item
    {
        public Pago Pago = null!;
        public DateTime ResaltadoHasta;
    }

    private readonly List<Item> _items = new();
    private readonly VScrollBar _barra = new() { Dock = DockStyle.Right, Visible = false };
    private readonly System.Windows.Forms.Timer _timerResalte = new() { Interval = 500 };
    private int _hover = -1;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string MensajeVacio { get; set; } = "Aún no hay pagos.";

    public int Cantidad => _items.Count;
    public IEnumerable<Guid> Ids => _items.Select(i => i.Pago.Id);

    public event Action<Pago>? PagoClick;
    /// <summary>Se dispara al acercarse al final del scroll (carga progresiva).</summary>
    public event Action? CercaDelFinal;

    public ListaPagos()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Tema.Fondo;
        Controls.Add(_barra);
        _barra.Scroll += (_, _) => { Invalidate(); VerificarFinal(); };
        _timerResalte.Tick += (_, _) =>
        {
            if (_items.Any(i => i.ResaltadoHasta > DateTime.UtcNow.AddMilliseconds(-600))) Invalidate();
        };
        _timerResalte.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timerResalte.Dispose();
        base.Dispose(disposing);
    }

    // ---------- Datos ----------
    public void Reemplazar(IEnumerable<Pago> pagos)
    {
        _items.Clear();
        _items.AddRange(pagos.Select(p => new Item { Pago = p }));
        _barra.Value = 0;
        _hover = -1;
        ActualizarBarra();
        Invalidate();
    }

    public void Agregar(IEnumerable<Pago> pagos)
    {
        var existentes = _items.Select(i => i.Pago.Id).ToHashSet();
        _items.AddRange(pagos.Where(p => !existentes.Contains(p.Id)).Select(p => new Item { Pago = p }));
        ActualizarBarra();
        Invalidate();
    }

    /// <summary>Inserta un pago nuevo en su posicion (mas reciente primero) y lo resalta.</summary>
    public void InsertarResaltado(Pago pago)
    {
        if (_items.Any(i => i.Pago.Id == pago.Id)) return;
        var idx = _items.FindIndex(i => i.Pago.FechaHora < pago.FechaHora);
        if (idx < 0) idx = _items.Count;
        _items.Insert(idx, new Item { Pago = pago, ResaltadoHasta = DateTime.UtcNow + DuracionResalte });

        // Si el usuario esta mirando mas abajo, se mantiene su posicion.
        var desplazado = _barra.Value > 0;
        ActualizarBarra();
        if (desplazado) _barra.Value = Math.Min(MaxScroll(), _barra.Value + AltoTarjeta + Espacio);
        Invalidate();
    }

    public void LimpiarPruebas()
    {
        _items.RemoveAll(i => i.Pago.EsPrueba);
        ActualizarBarra();
        Invalidate();
    }

    // ---------- Scroll ----------
    private int AltoTotal => Margen + _items.Count * (AltoTarjeta + Espacio);
    private int MaxScroll() => Math.Max(0, AltoTotal - Height);

    private void ActualizarBarra()
    {
        var max = MaxScroll();
        _barra.Visible = max > 0;
        if (max > 0)
        {
            _barra.Minimum = 0;
            _barra.Maximum = AltoTotal;
            _barra.LargeChange = Math.Max(1, Height);
            _barra.SmallChange = AltoTarjeta;
            _barra.Value = Math.Min(_barra.Value, Math.Max(0, AltoTotal - _barra.LargeChange));
        }
        else _barra.Value = 0;
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); ActualizarBarra(); }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (!_barra.Visible) return;
        var nuevo = _barra.Value - Math.Sign(e.Delta) * (AltoTarjeta + Espacio);
        _barra.Value = Math.Clamp(nuevo, 0, Math.Max(0, AltoTotal - _barra.LargeChange));
        Invalidate();
        VerificarFinal();
    }

    private void VerificarFinal()
    {
        if (_items.Count > 0 && _barra.Value + Height >= AltoTotal - 2 * (AltoTarjeta + Espacio))
            CercaDelFinal?.Invoke();
    }

    // ---------- Raton ----------
    private int IndiceEn(Point p)
    {
        var y = p.Y + _barra.Value - Margen;
        if (y < 0) return -1;
        var i = y / (AltoTarjeta + Espacio);
        if (y % (AltoTarjeta + Espacio) >= AltoTarjeta) return -1;      // en el espacio entre tarjetas
        var anchoUtil = Width - (_barra.Visible ? _barra.Width : 0);
        if (p.X < Margen || p.X > anchoUtil - Margen) return -1;
        return i >= 0 && i < _items.Count ? i : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var i = IndiceEn(e.Location);
        if (i != _hover) { _hover = i; Cursor = i >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        var i = IndiceEn(e.Location);
        if (i >= 0) PagoClick?.Invoke(_items[i].Pago);
        base.OnMouseClick(e);
    }

    // ---------- Pintado ----------
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_items.Count == 0)
        {
            TextRenderer.DrawText(g, MensajeVacio, Tema.Fuente(12f), ClientRectangle, Tema.TextoSecundario,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        var ancho = Width - (_barra.Visible ? _barra.Width : 0) - Margen * 2;
        var primero = Math.Max(0, (_barra.Value - Margen) / (AltoTarjeta + Espacio));
        for (var i = primero; i < _items.Count; i++)
        {
            var y = Margen + i * (AltoTarjeta + Espacio) - _barra.Value;
            if (y > Height) break;
            DibujarTarjeta(g, _items[i], new Rectangle(Margen, y, ancho, AltoTarjeta), i == _hover);
        }
    }

    private static void DibujarTarjeta(Graphics g, Item item, Rectangle r, bool hover)
    {
        var p = item.Pago;
        var resaltado = item.ResaltadoHasta > DateTime.UtcNow;

        // Sombra suave
        using (var sp = Tema.Redondeado(new Rectangle(r.X + 1, r.Y + 3, r.Width - 2, r.Height - 1), Tema.RadioTarjeta))
        using (var sb = new SolidBrush(Color.FromArgb(14, 11, 42, 143)))
            g.FillPath(sb, sp);

        using var path = Tema.Redondeado(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), Tema.RadioTarjeta);
        using (var fill = new SolidBrush(resaltado ? Tema.AzulClaro : Tema.Tarjeta)) g.FillPath(fill, path);
        using (var pen = new Pen(resaltado ? Tema.Primario : hover ? Tema.Primario : Tema.Borde, resaltado ? 2f : 1f))
            g.DrawPath(pen, path);

        // Circulo de iniciales
        var d = 44;
        var circ = new Rectangle(r.X + 16, r.Y + (r.Height - d) / 2, d, d);
        using (var b = new SolidBrush(resaltado ? Color.White : Tema.AzulClaro)) g.FillEllipse(b, circ);
        TextRenderer.DrawText(g, Formato.Iniciales(p.Remitente), Tema.Fuente(11f, FontStyle.Bold), circ, Tema.Primario,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        // Monto a la derecha
        var colorMonto = p.Tipo == TipoPago.Recibido ? Tema.Recibido
                       : p.Tipo == TipoPago.Enviado ? Tema.Enviado : Tema.TextoSecundario;
        var textoMonto = Formato.Monto(p.Monto, p.Moneda);
        var fMonto = Tema.Fuente(15f, FontStyle.Bold);
        var anchoMonto = TextRenderer.MeasureText(g, textoMonto, fMonto).Width;
        var rMonto = new Rectangle(r.Right - anchoMonto - 22, r.Y, anchoMonto + 8, r.Height);
        TextRenderer.DrawText(g, textoMonto, fMonto, rMonto, colorMonto,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        // Nombre (+ etiqueta PRUEBA) y subtitulo
        var xTexto = circ.Right + 14;
        var anchoTexto = Math.Max(40, rMonto.Left - xTexto - 12);
        var nombre = string.IsNullOrWhiteSpace(p.Remitente) ? "(sin nombre)" : p.Remitente!;
        var fNombre = Tema.Fuente(11.5f, FontStyle.Bold);
        var etiquetaAncho = p.EsPrueba ? 66 : 0;

        TextRenderer.DrawText(g, nombre, fNombre,
            new Rectangle(xTexto, r.Y + 13, anchoTexto - etiquetaAncho, 24), Tema.Texto,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.Left);

        if (p.EsPrueba)
        {
            var w = Math.Min(TextRenderer.MeasureText(g, nombre, fNombre).Width, anchoTexto - etiquetaAncho);
            var tag = new Rectangle(xTexto + w + 8, r.Y + 14, 58, 20);
            using var tp = Tema.Redondeado(tag, 10);
            using (var tb = new SolidBrush(Tema.Acento)) g.FillPath(tb, tp);
            TextRenderer.DrawText(g, "PRUEBA", Tema.Fuente(8f, FontStyle.Bold), tag, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        var sub = $"{p.App ?? "—"}  ·  {Formato.Fecha(p.FechaHora)}  ·  {Formato.Hora(p.FechaHora)}";
        TextRenderer.DrawText(g, sub, Tema.Fuente(9.5f), new Rectangle(xTexto, r.Y + 42, anchoTexto, 20),
            Tema.TextoSecundario, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.Left);
    }
}
