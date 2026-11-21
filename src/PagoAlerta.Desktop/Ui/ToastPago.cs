using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PagoAlerta.Desktop.Ui;

/// <summary>
/// Recuadro emergente de pago (como las notificaciones de Chrome): aparece en la esquina inferior derecha SIN robar
/// el foco, con sombra suave y esquinas redondeadas (ventana con transparencia por pixel), se cierra solo y se puede tocar
/// para abrir la app. Con el mouse encima se detiene el tiempo.
/// </summary>
public sealed class ToastPago : Form
{
    // Tamaño logico (a 96 dpi). Incluye el margen para la sombra.
    public const int Ancho = 432, Alto = 144;
    public const int MargenX = 16, MargenY = 14, AnchoTarjeta = 400, AltoTarjeta = 108;
    private const double MsEntrada = 320, MsSalida = 220;

    private readonly string _titulo, _monto, _detalle;
    private readonly bool _esPrueba;
    private readonly Color _colorMonto;
    private readonly Action? _alClic;
    private readonly float _k;                      // escala por dpi
    private readonly System.Windows.Forms.Timer _reloj = new() { Interval = 15 };
    private readonly Stopwatch _cronometro = Stopwatch.StartNew();
    private readonly int _duracionMs;

    private double _msTranscurridos, _msUltimo, _msInicioSalida = -1;
    private bool _pausado, _hoverCerrar, _cerrando;
    private float _x, _y, _destX, _destY;
    private bool _posicionado;

    public event Action? Cerrado;

    /// <summary>True mientras se desvanece (ya no cuenta como visible para el limite).</summary>
    public bool Cerrando => _cerrando;

    public ToastPago(string titulo, string monto, string detalle, bool esPrueba, Color colorMonto, Action? alClic, int duracionMs = 8000)
    {
        _titulo = titulo; _monto = monto; _detalle = detalle; _esPrueba = esPrueba; _colorMonto = colorMonto;
        _alClic = alClic; _duracionMs = duracionMs;
        _k = Math.Max(1f, DeviceDpi / 96f);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Cursor = Cursors.Hand;
        Size = new Size((int)(Ancho * _k), (int)(Alto * _k));

        _reloj.Tick += (_, _) => Paso();
    }

    // ---------- Ventana "no activable" con transparencia por pixel ----------
    private const int WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000, WS_EX_TOPMOST = 0x8;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    /// <summary>Coloca la ventana. La primera vez entra deslizandose desde la derecha; despues, se mueve suavemente.</summary>
    public void IrA(int x, int y)
    {
        _destX = x; _destY = y;
        if (!_posicionado) { _x = x; _y = y; _posicionado = true; }
    }

    public void CerrarYa()
    {
        if (_cerrando) return;
        _cerrando = true;
        _msInicioSalida = _msTranscurridos;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _msUltimo = _cronometro.Elapsed.TotalMilliseconds;
        _reloj.Start();
        Paso();
    }

    // ---------- Animacion ----------
    private void Paso()
    {
        var ahora = _cronometro.Elapsed.TotalMilliseconds;
        var dt = ahora - _msUltimo; _msUltimo = ahora;
        _msTranscurridos += dt;                              // tiempo total (animaciones)
        if (!_pausado && !_cerrando) _msVisibles += dt;      // tiempo que cuenta para cerrarse

        _y += (_destY - _y) * 0.22f;
        if (Math.Abs(_destY - _y) < 0.5f) _y = _destY;

        float alfa;
        double deslizamiento;
        if (_cerrando)
        {
            var t = (_msTranscurridos - _msInicioSalida) / MsSalida;
            alfa = (float)(1 - Mezcla.EaseOutCubic((float)t));
            deslizamiento = Mezcla.EaseOutCubic((float)t) * 24;
            if (t >= 1) { Cerrar(); return; }
        }
        else
        {
            var t = Math.Min(1.0, _msTranscurridos / MsEntrada);
            alfa = (float)Mezcla.EaseOutCubic((float)t);
            deslizamiento = (1 - alfa) * 70;
            if (_msVisibles >= _duracionMs) CerrarYa();
        }

        Dibujar(alfa, (int)(_destX + deslizamiento * _k), (int)_y);
    }

    private double _msVisibles;

    private void Cerrar()
    {
        _reloj.Stop();
        Cerrado?.Invoke();
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _reloj.Dispose();
        base.OnFormClosed(e);
    }

    // ---------- Interaccion ----------
    private RectangleF RectCerrar => new((MargenX + AnchoTarjeta - 40) * _k, (MargenY + 10) * _k, 28 * _k, 28 * _k);

    protected override void OnMouseEnter(EventArgs e) { _pausado = true; base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _pausado = false; _hoverCerrar = false; base.OnMouseLeave(e); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        _pausado = true;
        _hoverCerrar = RectCerrar.Contains(e.Location);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || _cerrando) return;
        if (RectCerrar.Contains(e.Location)) { CerrarYa(); return; }
        try { _alClic?.Invoke(); } catch { }
        CerrarYa();
    }

    // ---------- Dibujo ----------
    private Bitmap Pintar()
    {
        var w = (int)(Ancho * _k); var h = (int)(Alto * _k);
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);
        g.ScaleTransform(_k, _k);

        var tarjeta = new Rectangle(MargenX, MargenY, AnchoTarjeta, AltoTarjeta);

        // Sombra suave: varias capas translucidas
        for (var i = 7; i >= 1; i--)
        {
            var r = new Rectangle(tarjeta.X - i + 1, tarjeta.Y + 4 - i + 1, tarjeta.Width + (i - 1) * 2, tarjeta.Height + (i - 1) * 2);
            using var p = Tema.Redondeado(r, Tema.RadioTarjeta + i);
            using var b = new SolidBrush(Color.FromArgb(7, 11, 42, 143));
            g.FillPath(b, p);
        }

        using var path = Tema.Redondeado(tarjeta, Tema.RadioTarjeta);
        using (var fondo = new SolidBrush(Color.White)) g.FillPath(fondo, path);
        using (var borde = new Pen(Color.FromArgb(0xE3, 0xE8, 0xF5))) g.DrawPath(borde, path);

        // Icono de la app
        IconoApp.Dibujar(g, tarjeta.X + 18, tarjeta.Y + 22, 52);

        var xTexto = tarjeta.X + 84;
        var anchoTexto = tarjeta.Right - xTexto - 18;
        using var fCab = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        using var fTit = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var fMonto = new Font("Segoe UI", 19f, FontStyle.Bold);
        using var fDet = new Font("Segoe UI", 10f);
        using var gris = new SolidBrush(Tema.TextoSecundario);
        using var oscuro = new SolidBrush(Tema.Texto);
        var fmt = new StringFormat(StringFormat.GenericTypographic) { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

        g.DrawString("PagoAlerta · ahora", fCab, gris, new PointF(xTexto, tarjeta.Y + 12), fmt);

        // Boton cerrar (X)
        var rc = new RectangleF(tarjeta.Right - 40, tarjeta.Y + 10, 28, 28);
        if (_hoverCerrar) using (var hb = new SolidBrush(Tema.AzulClaro)) g.FillEllipse(hb, rc);
        using (var xp = new Pen(_hoverCerrar ? Tema.Primario : Color.FromArgb(0x9C, 0xA3, 0xAF), 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            var cx = rc.X + rc.Width / 2; var cy = rc.Y + rc.Height / 2;
            g.DrawLine(xp, cx - 4.5f, cy - 4.5f, cx + 4.5f, cy + 4.5f);
            g.DrawLine(xp, cx - 4.5f, cy + 4.5f, cx + 4.5f, cy - 4.5f);
        }

        var yTit = tarjeta.Y + 32;
        g.DrawString(_titulo, fTit, oscuro, new RectangleF(xTexto, yTit, anchoTexto - 60, 22), fmt);

        if (_esPrueba)
        {
            var etiqueta = new Rectangle(tarjeta.Right - 40 - 62, yTit + 1, 58, 19);
            using var ep = Tema.Redondeado(etiqueta, 9);
            using (var eb = new SolidBrush(Tema.Acento)) g.FillPath(eb, ep);
            using var fe = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            var cf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("PRUEBA", fe, Brushes.White, etiqueta, cf);
        }

        if (!string.IsNullOrEmpty(_monto))
        {
            using var verde = new SolidBrush(_colorMonto);
            g.DrawString(_monto, fMonto, verde, new RectangleF(xTexto - 1, tarjeta.Y + 50, anchoTexto, 34), fmt);
            g.DrawString(_detalle, fDet, gris, new RectangleF(xTexto, tarjeta.Y + 82, anchoTexto, 20), fmt);
        }
        else
        {
            var ajuste = new StringFormat { Trimming = StringTrimming.EllipsisWord };
            g.DrawString(_detalle, fDet, gris, new RectangleF(xTexto, tarjeta.Y + 56, anchoTexto, 40), ajuste);
        }

        // Barra de tiempo (se vacia hasta cerrarse)
        var restante = (float)Math.Clamp(1 - _msVisibles / _duracionMs, 0, 1);
        var estado = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        using (var pista = new SolidBrush(Color.FromArgb(0xEE, 0xF1, 0xF8))) g.FillRectangle(pista, tarjeta.X, tarjeta.Bottom - 4, tarjeta.Width, 4);
        using (var barra = new LinearGradientBrush(new Rectangle(tarjeta.X, tarjeta.Bottom - 4, tarjeta.Width, 4), Tema.Primario, Tema.Acento, 0f))
            g.FillRectangle(barra, tarjeta.X, tarjeta.Bottom - 4, tarjeta.Width * restante, 4);
        g.Restore(estado);
        return bmp;
    }

    private void Dibujar(float alfa, int x, int y)
    {
        if (!IsHandleCreated) return;
        using var bmp = Pintar();
        var hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
        var pantalla = GetDC(IntPtr.Zero);
        var memoria = CreateCompatibleDC(pantalla);
        var anterior = SelectObject(memoria, hBitmap);
        try
        {
            var destino = new POINT { x = x, y = y };
            var tam = new SIZE { cx = bmp.Width, cy = bmp.Height };
            var origen = new POINT { x = 0, y = 0 };
            var mezcla = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = (byte)(Math.Clamp(alfa, 0f, 1f) * 255), AlphaFormat = 1 };
            UpdateLayeredWindow(Handle, pantalla, ref destino, ref tam, memoria, ref origen, 0, ref mezcla, 2);
        }
        finally
        {
            SelectObject(memoria, anterior);
            DeleteObject(hBitmap);
            DeleteDC(memoria);
            ReleaseDC(IntPtr.Zero, pantalla);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
}
