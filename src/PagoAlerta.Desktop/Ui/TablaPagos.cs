using System.ComponentModel;
using System.Drawing.Drawing2D;
using PagoAlerta.Core;
using PagoAlerta.Core.Models;

namespace PagoAlerta.Desktop.Ui;

/// <summary>
/// Tabla de pagos dibujada a mano y virtualizada (solo pinta lo visible):
/// FECHA · HORA · NOMBRE COMPLETO · APP · MONTO. Seleccion, hover, scroll suave y animacion de filas nuevas.
/// </summary>
public class TablaPagos : Control
{
    private const int AltoCabecera = 50, AltoFila = 58, Relleno = 24;
    private static readonly TimeSpan DuracionNuevo = TimeSpan.FromSeconds(10);
    private const double MsEntrada = 450;

    private sealed class Fila
    {
        public Pago Pago = null!;
        public DateTime NuevoHasta;      // mientras sea futuro: muestra "NUEVO" y fondo resaltado
        public DateTime CreadaEn = DateTime.MinValue; // para el fundido de entrada
    }

    private readonly List<Fila> _filas = new();
    private readonly System.Windows.Forms.Timer _reloj = new() { Interval = 16 };
    private float _scroll, _scrollDestino;
    private int _hover = -1, _seleccion = -1;
    private bool _arrastrando;
    private int _arrastreOffsetY;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string MensajeVacio { get; set; } = "Aún no hay pagos.";

    public int Cantidad => _filas.Count;
    public IEnumerable<Pago> Pagos => _filas.Select(f => f.Pago);
    public Pago? Seleccionado => _seleccion >= 0 && _seleccion < _filas.Count ? _filas[_seleccion].Pago : null;

    public event Action<Pago?>? SeleccionCambio;
    /// <summary>Se dispara al acercarse al final (carga progresiva).</summary>
    public event Action? CercaDelFinal;

    public TablaPagos()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Tema.Fondo;
        _reloj.Tick += (_, _) => AlTick();
        _reloj.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _reloj.Dispose();
        base.Dispose(disposing);
    }

    // ---------------- Datos ----------------
    public void Reemplazar(IEnumerable<Pago> pagos)
    {
        var anterior = Seleccionado?.Id;
        _filas.Clear();
        _filas.AddRange(pagos.Select(p => new Fila { Pago = p }));
        _scroll = _scrollDestino = 0;
        _hover = -1;
        _seleccion = anterior is { } id ? _filas.FindIndex(f => f.Pago.Id == id) : -1;
        Invalidate();
    }

    public void Agregar(IEnumerable<Pago> pagos)
    {
        var existentes = _filas.Select(f => f.Pago.Id).ToHashSet();
        _filas.AddRange(pagos.Where(p => !existentes.Contains(p.Id)).Select(p => new Fila { Pago = p }));
        Invalidate();
    }

    /// <summary>Inserta el pago en su lugar (mas reciente primero), con fundido y etiqueta NUEVO.</summary>
    public void InsertarResaltado(Pago pago)
    {
        if (_filas.Any(f => f.Pago.Id == pago.Id)) return;
        var idx = _filas.FindIndex(f => f.Pago.FechaHora < pago.FechaHora);
        if (idx < 0) idx = _filas.Count;
        _filas.Insert(idx, new Fila { Pago = pago, NuevoHasta = DateTime.UtcNow + DuracionNuevo, CreadaEn = DateTime.UtcNow });
        if (_seleccion >= idx) _seleccion++;
        if (_scrollDestino > 0) { _scrollDestino += AltoFila; _scroll += AltoFila; }   // no mover lo que el usuario mira
        Invalidate();
    }

    public void LimpiarPruebas()
    {
        var sel = Seleccionado?.Id;
        _filas.RemoveAll(f => f.Pago.EsPrueba);
        _seleccion = sel is { } id ? _filas.FindIndex(f => f.Pago.Id == id) : -1;
        if (_seleccion < 0) SeleccionCambio?.Invoke(null);
        Invalidate();
    }

    public void Seleccionar(int indice)
    {
        indice = Math.Clamp(indice, -1, _filas.Count - 1);
        if (indice == _seleccion) return;
        _seleccion = indice;
        if (indice >= 0) AsegurarVisible(indice);
        SeleccionCambio?.Invoke(Seleccionado);
        Invalidate();
    }

    // ---------------- Geometria y scroll ----------------
    private int AltoVista => Math.Max(0, Height - AltoCabecera - 2);
    private int AltoContenido => _filas.Count * AltoFila;
    private int ScrollMax => Math.Max(0, AltoContenido - AltoVista);
    private bool HayScroll => ScrollMax > 0;

    private void AsegurarVisible(int i)
    {
        var arriba = i * AltoFila; var abajo = arriba + AltoFila;
        if (arriba < _scrollDestino) _scrollDestino = arriba;
        else if (abajo > _scrollDestino + AltoVista) _scrollDestino = abajo - AltoVista;
        _scrollDestino = Math.Clamp(_scrollDestino, 0, ScrollMax);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!HayScroll) return;
        _scrollDestino = Math.Clamp(_scrollDestino - Math.Sign(e.Delta) * AltoFila * 1.6f, 0, ScrollMax);
    }

    private void AlTick()
    {
        var movio = false;
        if (Math.Abs(_scrollDestino - _scroll) > 0.3f)
        {
            _scroll += (_scrollDestino - _scroll) * 0.25f;
            movio = true;
        }
        else if (_scroll != _scrollDestino) { _scroll = _scrollDestino; movio = true; }

        var ahora = DateTime.UtcNow;
        var animando = _filas.Any(f => f.NuevoHasta > ahora.AddMilliseconds(-800) ||
                                       (ahora - f.CreadaEn).TotalMilliseconds < MsEntrada + 50);
        if (movio || animando) { Invalidate(); if (movio) VerificarFinal(); }
    }

    private void VerificarFinal()
    {
        if (_filas.Count > 0 && _scroll + AltoVista >= AltoContenido - 3 * AltoFila) CercaDelFinal?.Invoke();
    }

    // ---------------- Barra de scroll fina ----------------
    private Rectangle RectPulgar()
    {
        if (!HayScroll) return Rectangle.Empty;
        var pista = AltoVista - 12;
        var alto = Math.Max(36, (int)(pista * (AltoVista / (float)AltoContenido)));
        var y = AltoCabecera + 6 + (int)((pista - alto) * (_scroll / ScrollMax));
        return new Rectangle(Width - 11, y, 6, alto);
    }

    // ---------------- Raton y teclado ----------------
    private int IndiceEn(Point p)
    {
        if (p.Y < AltoCabecera || p.Y > Height - 2) return -1;
        var i = (int)((p.Y - AltoCabecera + _scroll) / AltoFila);
        return i >= 0 && i < _filas.Count ? i : -1;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        var pulgar = RectPulgar();
        if (HayScroll && new Rectangle(Width - 18, AltoCabecera, 18, AltoVista).Contains(e.Location))
        {
            _arrastrando = true;
            _arrastreOffsetY = pulgar.Contains(e.Location) ? e.Y - pulgar.Y : pulgar.Height / 2;
            MoverPulgar(e.Y);
            return;
        }
        var i = IndiceEn(e.Location);
        if (i >= 0) Seleccionar(i);
        base.OnMouseDown(e);
    }

    private void MoverPulgar(int y)
    {
        var pista = AltoVista - 12;
        var alto = Math.Max(36, (int)(pista * (AltoVista / (float)AltoContenido)));
        var rel = (y - _arrastreOffsetY - AltoCabecera - 6) / (float)Math.Max(1, pista - alto);
        _scrollDestino = _scroll = Math.Clamp(rel, 0f, 1f) * ScrollMax;
        Invalidate();
        VerificarFinal();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_arrastrando) { MoverPulgar(e.Y); return; }
        var i = IndiceEn(e.Location);
        if (i != _hover) { _hover = i; Cursor = i >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) { _arrastrando = false; base.OnMouseUp(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var paso = Math.Max(1, AltoVista / AltoFila - 1);
        switch (e.KeyCode)
        {
            case Keys.Down: Seleccionar(_seleccion < 0 ? 0 : _seleccion + 1); e.Handled = true; break;
            case Keys.Up: Seleccionar(_seleccion < 0 ? 0 : _seleccion - 1); e.Handled = true; break;
            case Keys.PageDown: Seleccionar(Math.Max(_seleccion, 0) + paso); e.Handled = true; break;
            case Keys.PageUp: Seleccionar(Math.Max(_seleccion, 0) - paso); e.Handled = true; break;
            case Keys.Home: Seleccionar(0); e.Handled = true; break;
            case Keys.End: Seleccionar(_filas.Count - 1); e.Handled = true; break;
        }
        VerificarFinal();
    }

    // ---------------- Pintado ----------------
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var tarjeta = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Tema.Redondeado(tarjeta, Tema.RadioTarjeta);
        using (var sp = Tema.Redondeado(new Rectangle(1, 3, Width - 4, Height - 5), Tema.RadioTarjeta))
        using (var sb = new SolidBrush(Color.FromArgb(14, 11, 42, 143)))
            g.FillPath(sb, sp);
        using (var fill = new SolidBrush(Color.White)) g.FillPath(fill, path);

        var estado = g.Save();
        g.SetClip(path, CombineMode.Intersect);

        // Cabecera
        using (var cab = new SolidBrush(Tema.AzulClaro)) g.FillRectangle(cab, 0, 0, Width, AltoCabecera);
        var c = Columnas();
        EncabezadoColumna(g, "FECHA", c.fecha, TextFormatFlags.Left);
        EncabezadoColumna(g, "HORA", c.hora, TextFormatFlags.Left);
        EncabezadoColumna(g, "NOMBRE COMPLETO", c.nombre, TextFormatFlags.Left);
        EncabezadoColumna(g, "APP", c.app, TextFormatFlags.Left);
        EncabezadoColumna(g, "MONTO", c.monto, TextFormatFlags.Right);

        if (_filas.Count == 0)
        {
            TextRenderer.DrawText(g, MensajeVacio, Tema.Fuente(12f),
                new Rectangle(0, AltoCabecera, Width, Height - AltoCabecera), Tema.TextoSecundario,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            g.Restore(estado);
            using var penB = new Pen(Tema.Borde); g.DrawPath(penB, path);
            return;
        }

        g.SetClip(new Rectangle(0, AltoCabecera, Width, Height - AltoCabecera), CombineMode.Intersect);
        var primero = Math.Max(0, (int)(_scroll / AltoFila));
        for (var i = primero; i < _filas.Count; i++)
        {
            var y = AltoCabecera + i * AltoFila - (int)_scroll;
            if (y > Height) break;
            DibujarFila(g, _filas[i], new Rectangle(0, y, Width, AltoFila), i == _hover, i == _seleccion, i < _filas.Count - 1, c);
        }

        // Barra de scroll fina
        if (HayScroll)
        {
            var t = RectPulgar();
            using var tp = Tema.Redondeado(t, 3);
            using var tb = new SolidBrush(Color.FromArgb(_arrastrando ? 170 : 90, 11, 42, 143));
            g.FillPath(tb, tp);
        }

        g.Restore(estado);
        using var pen = new Pen(Tema.Borde);
        g.DrawPath(pen, path);
    }

    private (Rectangle fecha, Rectangle hora, Rectangle nombre, Rectangle app, Rectangle monto) Columnas()
    {
        var ancho = Width - (HayScroll ? 14 : 0);
        int wFecha = 112, wHora = 92, wApp = 108, wMonto = 140;
        var x = Relleno;
        var fecha = new Rectangle(x, 0, wFecha, 0); x += wFecha;
        var hora = new Rectangle(x, 0, wHora, 0); x += wHora;
        var nombreW = Math.Max(160, ancho - Relleno - x - wApp - wMonto - 8);
        var nombre = new Rectangle(x, 0, nombreW, 0); x += nombreW;
        var app = new Rectangle(x, 0, wApp, 0); x += wApp;
        var monto = new Rectangle(x, 0, Math.Max(wMonto, ancho - Relleno - x), 0);
        return (fecha, hora, nombre, app, monto);
    }

    private void EncabezadoColumna(Graphics g, string texto, Rectangle col, TextFormatFlags alin) =>
        TextRenderer.DrawText(g, texto, Tema.Fuente(8.5f, FontStyle.Bold), new Rectangle(col.X, 0, col.Width, AltoCabecera),
            Tema.PrimarioOscuro, alin | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

    private void DibujarFila(Graphics g, Fila f, Rectangle r, bool hover, bool sel, bool separador,
        (Rectangle fecha, Rectangle hora, Rectangle nombre, Rectangle app, Rectangle monto) c)
    {
        var p = f.Pago;
        var ahora = DateTime.UtcNow;
        var esNuevo = f.NuevoHasta > ahora;

        // Fondo: seleccionada > nueva (se desvanece) > hover
        Color fondo = Color.White;
        if (hover) fondo = Color.FromArgb(0xF7, 0xF9, 0xFF);
        if (esNuevo) fondo = Mezcla.De(Color.White, Tema.AzulClaro, Math.Min(1f, (float)(f.NuevoHasta - ahora).TotalMilliseconds / 2500f));
        if (sel) fondo = Tema.AzulClaro;
        using (var b = new SolidBrush(fondo)) g.FillRectangle(b, r);
        if (sel) using (var acento = new SolidBrush(Tema.Primario)) g.FillRectangle(acento, 0, r.Y + 10, 3, r.Height - 20);

        if (separador && !sel)
            using (var pen = new Pen(Color.FromArgb(0xEE, 0xF1, 0xF8))) g.DrawLine(pen, Relleno, r.Bottom - 1, r.Right - Relleno, r.Bottom - 1);

        var normal = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.Left;
        var fila = r with { };
        TextRenderer.DrawText(g, Formato.Fecha(p.FechaHora), Tema.Fuente(10f), Rc(c.fecha, fila), Tema.Texto, normal);
        TextRenderer.DrawText(g, Formato.Hora24(p.FechaHora), Tema.Fuente(10f), Rc(c.hora, fila), Tema.Texto, normal);

        // Nombre + etiquetas
        var nombre = string.IsNullOrWhiteSpace(p.Remitente) ? "(sin nombre)" : p.Remitente!;
        var fN = Tema.Fuente(10.5f, FontStyle.Bold);
        var etiquetas = new List<(string t, Color fondo, Color txt)>();
        if (p.EsPrueba) etiquetas.Add(("PRUEBA", Color.FromArgb(0xD5, 0xF5, 0xFA), Color.FromArgb(0x0E, 0x74, 0x90)));
        if (p.EsRevisar()) etiquetas.Add(("REVISAR", Color.FromArgb(0xFE, 0xF3, 0xC7), Color.FromArgb(0xB4, 0x53, 0x09)));
        if (esNuevo) etiquetas.Add(("NUEVO", Tema.Primario, Color.White));
        var anchoEtq = etiquetas.Sum(t => TextRenderer.MeasureText(t.t, Tema.Fuente(7.5f, FontStyle.Bold)).Width + 18 + 6);
        var rn = Rc(c.nombre, fila);
        var anchoNombre = Math.Min(TextRenderer.MeasureText(g, nombre, fN, new Size(int.MaxValue, 40), TextFormatFlags.NoPadding).Width,
            Math.Max(40, rn.Width - anchoEtq));
        TextRenderer.DrawText(g, nombre, fN, new Rectangle(rn.X, rn.Y, Math.Max(40, rn.Width - anchoEtq), rn.Height), Tema.Texto, normal);
        var ex = rn.X + anchoNombre + 8;
        foreach (var (t, bg, tx) in etiquetas)
        {
            var w = TextRenderer.MeasureText(t, Tema.Fuente(7.5f, FontStyle.Bold)).Width + 12;
            var rr = new Rectangle(ex, r.Y + (r.Height - 20) / 2, w, 20);
            using var pp = Tema.Redondeado(rr, 10);
            using (var bb = new SolidBrush(bg)) g.FillPath(bb, pp);
            TextRenderer.DrawText(g, t, Tema.Fuente(7.5f, FontStyle.Bold), rr, tx,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            ex += w + 6;
        }

        // App (chip pequeno)
        var app = (p.App ?? "—").ToUpperInvariant();
        var fa = Tema.Fuente(8f, FontStyle.Bold);
        var aw = TextRenderer.MeasureText(app, fa).Width + 18;
        var ra = new Rectangle(c.app.X, r.Y + (r.Height - 24) / 2, Math.Min(aw, c.app.Width - 6), 24);
        using (var pa = Tema.Redondeado(ra, 12))
        using (var ba = new SolidBrush(sel || esNuevo ? Color.White : Tema.AzulClaro)) g.FillPath(ba, pa);
        TextRenderer.DrawText(g, app, fa, ra, Tema.Primario,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        // Monto
        var color = p.Tipo == TipoPago.Recibido ? Tema.Recibido : p.Tipo == TipoPago.Enviado ? Tema.Enviado : Tema.TextoSecundario;
        var signo = p.Tipo == TipoPago.Recibido ? "+" : p.Tipo == TipoPago.Enviado ? "−" : "";
        TextRenderer.DrawText(g, p.EsRevisar() ? "—" : signo + Formato.Monto(p.Monto, p.Moneda), Tema.Fuente(11.5f, FontStyle.Bold), Rc(c.monto, fila), color,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        // Fundido de entrada: una capa blanca que desaparece
        var t0 = (ahora - f.CreadaEn).TotalMilliseconds;
        if (f.CreadaEn != DateTime.MinValue && t0 < MsEntrada)
        {
            var alfa = (int)(255 * (1 - Mezcla.EaseOutCubic((float)(t0 / MsEntrada))));
            using var capa = new SolidBrush(Color.FromArgb(alfa, 255, 255, 255));
            g.FillRectangle(capa, r);
        }
    }

    private static Rectangle Rc(Rectangle col, Rectangle fila) => new(col.X, fila.Y, col.Width, fila.Height);
}
