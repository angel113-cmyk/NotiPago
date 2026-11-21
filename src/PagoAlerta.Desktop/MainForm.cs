using System.Globalization;
using PagoAlerta.Core;
using PagoAlerta.Core.Exportacion;
using PagoAlerta.Core.Logging;
using PagoAlerta.Core.Models;
using PagoAlerta.Core.Services;
using PagoAlerta.Core.Time;
using PagoAlerta.Desktop.Services;
using PagoAlerta.Desktop.Ui;

namespace PagoAlerta.Desktop;

/// <summary>
/// Pantalla principal: barra superior, resumen, filtros en chips, tabla de pagos en tiempo real y panel de detalle.
/// HU-02, 03, 04, 05, 06, 07, 08 y HU-20 (parte PC). La logica es la de siempre; aqui solo cambio la presentacion.
/// </summary>
public class MainForm : Form
{
    private const int TamPagina = 50;
    private static readonly CultureInfo Es = new("es-PE");

    private readonly PagoService _servicio;
    private readonly PagoMonitor _monitor;
    private readonly Preferencias _prefs = Preferencias.Cargar();

    // Barra superior
    private readonly ChipEstado _chipEstado = new();
    private readonly Label _lblUsuario = new();
    private readonly BotonPildora _btnSalir = new("Cerrar sesión", BotonPildora.Estilo.ContornoBlanco);

    // Encabezado y resumen
    private readonly Label _lblFecha = new(), _lblTitulo = new();
    private readonly TarjetaStat _stTotal = new("Total cobrado", "No incluye mensajes de prueba", oscura: true);
    private readonly TarjetaStat _stCantidad = new("Pagos recibidos", "Según los filtros activos", oscura: false);
    private readonly TarjetaStat _stUltimo = new("Último pago", "Hora de Lima", oscura: false);

    // Filtros
    private readonly Chip _chipHoy = new("Hoy"), _chipAyer = new("Ayer"), _chipRango = new("Rango de fechas");
    private readonly Chip _chipTodas = new("Todas", Chip.Estilo.Contorno), _chipYape = new("Yape", Chip.Estilo.Contorno),
                          _chipPlin = new("Plin", Chip.Estilo.Contorno), _chipBbva = new("BBVA", Chip.Estilo.Contorno);
    private readonly DateTimePicker _dtpDesde = new(), _dtpHasta = new();
    private readonly CampoRedondeado _buscar = new("Buscar por nombre", radio: 19, alto: 38);
    private readonly CheckBox _chkPruebas = new();
    private readonly BotonPildora _btnExportar = new("Exportar a Excel", BotonPildora.Estilo.Verde, BotonPildora.IconoBoton.Descargar);
    private readonly BotonPildora _btnMas = new("", BotonPildora.Estilo.Contorno, BotonPildora.IconoBoton.Mas);
    private readonly ContextMenuStrip _menuMas = new();
    private readonly Label _lblAviso = new();
    private PanelRedondeado _tarjFiltros = null!;
    private FlowLayoutPanel _flujo = null!;

    private readonly TablaPagos _tabla = new() { Dock = DockStyle.Fill };
    private readonly PanelDetalle _detalle = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _debounceBusqueda = new() { Interval = 350 };
    private readonly System.Windows.Forms.Timer _reintento = new() { Interval = 10000 };

    private int _version, _desplazamiento, _periodo;
    private string? _appFiltro;
    private bool _hayMas, _cargando, _suspender, _enviandoPrueba;

    // Segundo plano: icono junto al reloj de Windows
    private readonly NotifyIcon _bandeja = new();
    private readonly bool _iniciarOculto;
    private bool _cierreReal, _avisoBandejaMostrado;

    public bool CerrarSesionSolicitado { get; private set; }

    /// <param name="iniciarOculto">Arranque con Windows: la ventana queda oculta en la bandeja.</param>
    public MainForm(PagoService servicio, bool iniciarOculto = false)
    {
        _servicio = servicio;
        _iniciarOculto = iniciarOculto;
        _monitor = new PagoMonitor(servicio);
        if (iniciarOculto) { Opacity = 0; ShowInTaskbar = false; }

        Text = AppInfo.Nombre;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente(10f);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        ClientSize = new Size(1280, Math.Min(900, Screen.PrimaryScreen!.WorkingArea.Height - 90));
        MinimumSize = new Size(1000, 600);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        ConstruirUi();

        Shown += async (_, _) =>
        {
            if (_iniciarOculto)
            {
                Hide();
                Opacity = 1;
                ShowInTaskbar = true;
                MostrarAvisoBandeja();
            }
            await IniciarAsync();
        };
        FormClosing += (_, e) =>
        {
            // La X no cierra la app: la oculta junto al reloj y sigue recibiendo pagos.
            if (!_cierreReal && !CerrarSesionSolicitado && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                OcultarABandeja();
                return;
            }
            _bandeja.Visible = false;
            _bandeja.Dispose();
            _ = _monitor.DisposeAsync().AsTask();
        };
        _debounceBusqueda.Tick += async (_, _) => { _debounceBusqueda.Stop(); await RecargarAsync(); };
        _reintento.Tick += async (_, _) => { if (_lblAviso.Visible) await RecargarAsync(); else _reintento.Stop(); };
    }

    // =====================================================================
    //  Interfaz
    // =====================================================================
    private void ConstruirUi()
    {
        // ---------- Barra superior ----------
        var barra = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Tema.PrimarioOscuro };
        var logo = new LogoCampana(42) { Location = new Point(48, 15) };
        var nombre = new Label
        {
            Text = AppInfo.Nombre, ForeColor = Color.White, Font = Tema.Fuente(16f, FontStyle.Bold), AutoSize = true,
            BackColor = Color.Transparent, Location = new Point(100, 20)
        };
        _lblUsuario.Text = _servicio.CorreoActual ?? "";
        _lblUsuario.ForeColor = Color.FromArgb(215, 255, 255, 255);
        _lblUsuario.Font = Tema.Fuente(10f);
        _lblUsuario.AutoSize = true;
        _lblUsuario.BackColor = Color.Transparent;
        _btnSalir.Click += async (_, _) => await CerrarSesionAsync();
        barra.Controls.AddRange(new Control[] { logo, nombre, _chipEstado, _lblUsuario, _btnSalir });
        barra.Resize += (_, _) => PosicionarBarra(barra);
        PosicionarBarra(barra);

        // ---------- Encabezado ----------
        _lblFecha.ForeColor = Tema.TextoSecundario;
        _lblFecha.Font = Tema.Fuente(10f);
        _lblFecha.AutoSize = true;
        _lblFecha.Location = new Point(0, 2);
        _lblTitulo.Text = "Pagos recibidos";
        _lblTitulo.ForeColor = Tema.PrimarioOscuro;
        _lblTitulo.Font = Tema.Fuente(22f, FontStyle.Bold);
        _lblTitulo.AutoSize = true;
        _lblTitulo.Location = new Point(-2, 24);
        var encabezado = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Tema.Fondo };
        encabezado.Controls.AddRange(new Control[] { _lblFecha, _lblTitulo });

        // ---------- Tarjetas de resumen ----------
        var stats = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 120, ColumnCount = 3, RowCount = 1, BackColor = Tema.Fondo,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        for (var i = 0; i < 3; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
        stats.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        foreach (var (card, col) in new[] { (_stTotal, 0), (_stCantidad, 1), (_stUltimo, 2) })
        {
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(col == 0 ? 0 : 8, 0, col == 2 ? 0 : 8, 0);
            stats.Controls.Add(card, col, 0);
        }
        _stTotal.MostrarTexto(Formato.Monto(0));
        _stCantidad.MostrarTexto("0");
        _stUltimo.MostrarTexto("—");

        // ---------- Filtros ----------
        _tarjFiltros = new PanelRedondeado { Dock = DockStyle.Top, Height = 74, Padding = new Padding(18, 14, 18, 8), BackColor = Color.White };
        _flujo = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, BackColor = Color.Transparent, Margin = Padding.Empty };
        _tarjFiltros.Controls.Add(_flujo);

        var hoy = ZonaLima.AhoraLima.Date;
        foreach (var d in new[] { _dtpDesde, _dtpHasta })
        {
            d.Format = DateTimePickerFormat.Short;
            d.Font = Tema.Fuente(10f);
            d.Value = hoy;
            d.MaxDate = hoy.AddDays(1);
            d.Width = 112;
            d.Visible = false;
            d.Margin = new Padding(0, 7, 10, 3);
            d.ValueChanged += async (_, _) => await RecargarSiNoSuspendidoAsync();
        }

        _buscar.Width = 188;
        _buscar.Margin = new Padding(2, 3, 10, 3);
        _buscar.Caja.TextChanged += (_, _) => { if (!_suspender) { _debounceBusqueda.Stop(); _debounceBusqueda.Start(); } };

        _chkPruebas.Text = "Ver pruebas";
        _chkPruebas.AutoSize = true;
        _chkPruebas.ForeColor = Tema.Texto;
        _chkPruebas.Cursor = Cursors.Hand;
        _chkPruebas.Margin = new Padding(4, 10, 10, 3);
        _chkPruebas.CheckedChanged += async (_, _) => await RecargarSiNoSuspendidoAsync();

        _btnExportar.Click += async (_, _) => await ExportarAsync();
        _btnMas.Click += (_, _) => _menuMas.Show(_btnMas, new Point(_btnMas.Width - 200, _btnMas.Height + 6));
        ConstruirMenuMas();

        foreach (var c in new Control[] { _chipHoy, _chipAyer, _chipRango, _chipTodas, _chipYape, _chipPlin, _chipBbva, _btnExportar, _btnMas })
            c.Margin = new Padding(0, 3, 8, 3);
        _chipTodas.Margin = new Padding(10, 3, 8, 3);

        _chipHoy.Click += async (_, _) => await ElegirPeriodoAsync(0);
        _chipAyer.Click += async (_, _) => await ElegirPeriodoAsync(1);
        _chipRango.Click += async (_, _) => await ElegirPeriodoAsync(2);
        _chipTodas.Click += async (_, _) => await ElegirAppAsync(null);
        _chipYape.Click += async (_, _) => await ElegirAppAsync("Yape");
        _chipPlin.Click += async (_, _) => await ElegirAppAsync("Plin");
        _chipBbva.Click += async (_, _) => await ElegirAppAsync("BBVA");
        _chipHoy.Seleccionado = true;
        _chipTodas.Seleccionado = true;

        _flujo.Controls.AddRange(new Control[]
        {
            _chipHoy, _chipAyer, _chipRango, _dtpDesde, _dtpHasta, _buscar,
            _chipTodas, _chipYape, _chipPlin, _chipBbva, _chkPruebas, _btnExportar, _btnMas
        });
        _tarjFiltros.Resize += (_, _) => AjustarAltoFiltros();
        _flujo.ControlAdded += (_, _) => AjustarAltoFiltros();
        _dtpDesde.VisibleChanged += (_, _) => AjustarAltoFiltros();

        // ---------- Aviso de errores ----------
        _lblAviso.Dock = DockStyle.Top;
        _lblAviso.Height = 36;
        _lblAviso.BackColor = Color.FromArgb(254, 226, 226);
        _lblAviso.ForeColor = Tema.Error;
        _lblAviso.TextAlign = ContentAlignment.MiddleCenter;
        _lblAviso.Font = Tema.Fuente(10f, FontStyle.Bold);
        _lblAviso.Visible = false;

        // ---------- Tabla + detalle ----------
        _tabla.SeleccionCambio += p => _detalle.Mostrar(p);
        _tabla.CercaDelFinal += async () => await CargarMasAsync();
        var colDetalle = new Panel { Dock = DockStyle.Right, Width = 412, Padding = new Padding(22, 0, 0, 0), BackColor = Tema.Fondo };
        colDetalle.Controls.Add(_detalle);
        var principal = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fondo, Padding = new Padding(0, 14, 0, 0) };
        principal.Controls.Add(_tabla);
        principal.Controls.Add(colDetalle);

        // ---------- Cuerpo ----------
        var cuerpo = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fondo, Padding = new Padding(48, 26, 48, 32) };
        var espacio1 = new Panel { Dock = DockStyle.Top, Height = 16, BackColor = Tema.Fondo };
        var espacio2 = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = Tema.Fondo };
        // El orden de adicion define el apilado: Fill primero, lo de arriba al final.
        cuerpo.Controls.Add(principal);
        cuerpo.Controls.Add(_tarjFiltros);
        cuerpo.Controls.Add(espacio2);
        cuerpo.Controls.Add(stats);
        cuerpo.Controls.Add(espacio1);
        cuerpo.Controls.Add(encabezado);

        Controls.Add(cuerpo);
        Controls.Add(_lblAviso);
        Controls.Add(barra);

        ActualizarEstadoRealtime(false);
        ActualizarEncabezado();
        _detalle.Mostrar(null);
        Load += (_, _) => AjustarAltoFiltros();
        ConstruirBandeja();
    }

    // =====================================================================
    //  Segundo plano (bandeja del sistema) y avisos
    // =====================================================================
    private void ConstruirBandeja()
    {
        var menu = new ContextMenuStrip { Font = Tema.Fuente(10f), ShowImageMargin = false };
        menu.Items.Add("Abrir PagoAlerta", null, (_, _) => MostrarDesdeBandeja());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => Salir());
        _bandeja.Icon = Icon;
        _bandeja.Text = $"{AppInfo.Nombre} — recibiendo pagos";
        _bandeja.ContextMenuStrip = menu;
        _bandeja.DoubleClick += (_, _) => MostrarDesdeBandeja();
        _bandeja.BalloonTipClicked += (_, _) => MostrarDesdeBandeja();
        _bandeja.Visible = true;
    }

    /// <summary>Muestra la ventana (desde el icono de la bandeja o al abrir una segunda copia de la app).</summary>
    public void MostrarDesdeBandeja()
    {
        ShowInTaskbar = true;
        Opacity = 1;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void OcultarABandeja()
    {
        Hide();
        MostrarAvisoBandeja();
    }

    private void MostrarAvisoBandeja()
    {
        if (_avisoBandejaMostrado) return;
        _avisoBandejaMostrado = true;
        _bandeja.ShowBalloonTip(4500, AppInfo.Nombre, "Sigue funcionando en segundo plano. Te avisaré cuando llegue un pago.", ToolTipIcon.Info);
    }

    /// <summary>Cierra la app de verdad (la X solo la oculta).</summary>
    private void Salir()
    {
        _cierreReal = true;
        Close();
    }

    /// <summary>Sonido y aviso de Windows al llegar un pago, aunque la ventana este oculta o con otros filtros.</summary>
    private void AvisarPago(Pago p)
    {
        if (!Avisos.DebeAvisar(p)) return;
        if (_prefs.Sonido) Sonido.Reproducir();

        var ventanaALaVista = Visible && WindowState != FormWindowState.Minimized && ReferenceEquals(ActiveForm, this);
        if (_prefs.AvisoPago && !ventanaALaVista)
        {
            var (titulo, cuerpo) = Avisos.DePago(p);
            _bandeja.ShowBalloonTip(6000, titulo, cuerpo, ToolTipIcon.Info);
        }
    }

    private void PosicionarBarra(Panel barra)
    {
        var derecha = barra.Width - 48;
        _btnSalir.Location = new Point(derecha - _btnSalir.Width, (barra.Height - _btnSalir.Height) / 2);
        _lblUsuario.Location = new Point(_btnSalir.Left - 20 - _lblUsuario.Width, (barra.Height - _lblUsuario.Height) / 2);
        _chipEstado.Location = new Point(_lblUsuario.Left - 22 - _chipEstado.Width, (barra.Height - _chipEstado.Height) / 2);
    }

    private void AjustarAltoFiltros()
    {
        if (_tarjFiltros is null || _flujo is null || _tarjFiltros.Width < 50) return;
        var disponible = _tarjFiltros.Width - _tarjFiltros.Padding.Horizontal;
        var h = _flujo.GetPreferredSize(new Size(disponible, 0)).Height;
        _tarjFiltros.Height = h + _tarjFiltros.Padding.Vertical + 4;
    }

    private void ConstruirMenuMas()
    {
        _menuMas.Font = Tema.Fuente(10f);
        _menuMas.ShowImageMargin = false;
        _menuMas.Items.Add("Limpiar filtros", null, async (_, _) => await LimpiarFiltrosAsync());
        _menuMas.Items.Add("Enviar prueba", null, async (_, _) => await EnviarPruebaAsync());
        _menuMas.Items.Add("Borrar pruebas", null, async (_, _) => await BorrarPruebasAsync());
        _menuMas.Items.Add(new ToolStripSeparator());
        _menuMas.Items.Add("Ajustes", null, async (_, _) => await AbrirAjustesAsync());
    }

    private void ActualizarEstadoRealtime(bool activo)
    {
        _chipEstado.Establecer(activo, activo ? "En vivo · conectado" : "Actualizando cada pocos segundos");
        if (_chipEstado.Parent is Panel barra) PosicionarBarra(barra);
    }

    private void ActualizarEncabezado()
    {
        var hoy = ZonaLima.AhoraLima.Date;
        string Larga(DateTime d)
        {
            var t = d.ToString("dddd d 'de' MMMM 'de' yyyy", Es);
            return char.ToUpper(t[0], Es) + t[1..];
        }
        _lblFecha.Text = _periodo switch
        {
            1 => Larga(hoy.AddDays(-1)),
            2 => $"Del {_dtpDesde.Value:dd/MM/yyyy} al {_dtpHasta.Value:dd/MM/yyyy}",
            _ => Larga(hoy)
        };
    }

    // =====================================================================
    //  Filtros
    // =====================================================================
    private async Task ElegirPeriodoAsync(int periodo)
    {
        if (_periodo == periodo && periodo != 2) return;
        _periodo = periodo;
        _chipHoy.Seleccionado = periodo == 0;
        _chipAyer.Seleccionado = periodo == 1;
        _chipRango.Seleccionado = periodo == 2;
        _dtpDesde.Visible = _dtpHasta.Visible = periodo == 2;
        ActualizarEncabezado();
        await RecargarSiNoSuspendidoAsync();
    }

    private async Task ElegirAppAsync(string? app)
    {
        if (_appFiltro == app) return;
        _appFiltro = app;
        _chipTodas.Seleccionado = app is null;
        _chipYape.Seleccionado = app == "Yape";
        _chipPlin.Seleccionado = app == "Plin";
        _chipBbva.Seleccionado = app == "BBVA";
        await RecargarSiNoSuspendidoAsync();
    }

    private (DateTime Desde, DateTime Hasta) RangoSeleccionado()
    {
        var hoy = ZonaLima.AhoraLima.Date;
        switch (_periodo)
        {
            case 1:
                return ZonaLima.RangoDia(hoy.AddDays(-1));
            case 2:
                var a = _dtpDesde.Value.Date; var b = _dtpHasta.Value.Date;
                if (a > b) (a, b) = (b, a);
                return (ZonaLima.RangoDia(a).DesdeUtc, ZonaLima.RangoDia(b).HastaUtc);
            default:
                return ZonaLima.RangoDia(hoy);
        }
    }

    private FiltroPagos FiltroActual()
    {
        var r = RangoSeleccionado();
        return new FiltroPagos
        {
            DesdeUtc = r.Desde,
            HastaUtc = r.Hasta,
            App = _appFiltro,
            Nombre = string.IsNullOrWhiteSpace(_buscar.Text) ? null : _buscar.Text.Trim(),
            IncluirPruebas = _chkPruebas.Checked,
            Limite = TamPagina
        };
    }

    private async Task RecargarSiNoSuspendidoAsync()
    {
        if (!_suspender) await RecargarAsync();
    }

    private async Task LimpiarFiltrosAsync()
    {
        _suspender = true;
        _periodo = 0; _appFiltro = null;
        _chipHoy.Seleccionado = true; _chipAyer.Seleccionado = false; _chipRango.Seleccionado = false;
        _chipTodas.Seleccionado = true; _chipYape.Seleccionado = false; _chipPlin.Seleccionado = false; _chipBbva.Seleccionado = false;
        _dtpDesde.Visible = _dtpHasta.Visible = false;
        _buscar.Caja.Clear();
        _chkPruebas.Checked = false;
        _dtpDesde.Value = _dtpHasta.Value = ZonaLima.AhoraLima.Date;
        _suspender = false;
        _debounceBusqueda.Stop();
        ActualizarEncabezado();
        await RecargarAsync();
    }

    // =====================================================================
    //  Carga de datos
    // =====================================================================
    private async Task IniciarAsync()
    {
        await RecargarAsync();
        _monitor.PagoNuevo += p => AlLlegarPago(p);
        _monitor.EstadoRealtimeCambio += activo => EnUi(() => ActualizarEstadoRealtime(activo));
        _monitor.Iniciar(DateTime.UtcNow.AddMinutes(-1));
    }

    private void EnUi(Action accion)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(accion); } catch (ObjectDisposedException) { }
    }

    private async Task RecargarAsync()
    {
        var version = ++_version;
        _cargando = true;
        _tabla.MensajeVacio = "Cargando pagos…";
        _tabla.Reemplazar(Array.Empty<Pago>());
        _detalle.Mostrar(null);
        _desplazamiento = 0;
        _hayMas = true;

        try
        {
            await LlenarAsync(version);
            if (version != _version) return;
            _tabla.MensajeVacio = "No hay pagos con estos filtros.";
            _tabla.Invalidate();
            ActualizarUltimoPago();
            OcultarAviso();
            _ = ActualizarResumenAsync(version);
        }
        catch (Exception ex) when (version == _version)
        {
            await ManejarErrorAsync(ex);
        }
        finally
        {
            if (version == _version) _cargando = false;
        }
    }

    /// <summary>Carga paginas hasta mostrar suficientes filas (la busqueda por nombre puede dejar paginas casi vacias).</summary>
    private async Task LlenarAsync(int version)
    {
        for (var intento = 0; intento < 6 && _hayMas && (intento == 0 || _tabla.Cantidad < 15); intento++)
        {
            var f = FiltroActual();
            f.Desplazamiento = _desplazamiento;
            var pagina = await _servicio.ListarPaginaAsync(f);
            if (version != _version) return;
            _desplazamiento += TamPagina;
            _hayMas = pagina.HayMas;
            foreach (var p in pagina.Items) _monitor.MarcarVisto(p.Id);
            _tabla.Agregar(pagina.Items);
        }
    }

    private async Task CargarMasAsync()
    {
        if (_cargando || !_hayMas) return;
        var version = _version;
        _cargando = true;
        try { await LlenarAsync(version); ActualizarUltimoPago(); }
        catch (Exception ex) when (version == _version) { await ManejarErrorAsync(ex); }
        finally { if (version == _version) _cargando = false; }
    }

    private async Task ActualizarResumenAsync(int version)
    {
        try
        {
            var resumen = await _servicio.ResumenAsync(FiltroActual());
            _stTotal.AnimarNumero(resumen.Total, v => Formato.Monto(v));
            _stCantidad.AnimarNumero(resumen.Cantidad, v => ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture));
        }
        catch { /* el listado ya muestra el error; el resumen se recalcula en el proximo ciclo */ }
    }

    /// <summary>Hora del pago recibido mas reciente de la lista (las pruebas no cuentan).</summary>
    private void ActualizarUltimoPago()
    {
        var ultimo = _tabla.Pagos.Where(p => p.Tipo == TipoPago.Recibido && !p.EsPrueba)
                                 .OrderByDescending(p => p.FechaHora).FirstOrDefault();
        _stUltimo.MostrarTexto(ultimo is null ? "—" : Formato.Hora24(ultimo.FechaHora));
    }

    // =====================================================================
    //  Tiempo real
    // =====================================================================
    private bool Coincide(Pago p)
    {
        if (p.EsPrueba && !_chkPruebas.Checked) return false;
        var f = FiltroActual();
        if (f.DesdeUtc is { } d && p.FechaHora < d) return false;
        if (f.HastaUtc is { } h && p.FechaHora >= h) return false;
        if (f.App is not null && !string.Equals(p.App, f.App, StringComparison.OrdinalIgnoreCase)) return false;
        return Texto.Contiene(p.Remitente, f.Nombre);
    }

    private void AlLlegarPago(Pago p) => EnUi(() =>
    {
        AvisarPago(p);                 // sonido + aviso de Windows, sin importar los filtros activos
        if (!Coincide(p)) return;
        _tabla.InsertarResaltado(p);
        if (!p.EsPrueba && p.Tipo == TipoPago.Recibido)
        {
            ActualizarUltimoPago();
            _ = ActualizarResumenAsync(_version);
        }
    });

    // =====================================================================
    //  Acciones
    // =====================================================================
    private async Task EnviarPruebaAsync()
    {
        if (_enviandoPrueba) return;
        _enviandoPrueba = true;
        try
        {
            if (!_chkPruebas.Checked) _chkPruebas.Checked = true;   // para que la prueba sea visible
            await _servicio.InsertarAsync(PagoFactory.CrearPrueba("Prueba (PC)"));
            OcultarAviso();
        }
        catch (Exception ex)
        {
            MostrarAviso($"No se pudo enviar la prueba: {Motivo(ex)}.");
        }
        finally { _enviandoPrueba = false; }
    }

    /// <summary>HU-07: exporta a Excel (.xlsx) o CSV respetando los filtros activos. Las pruebas no se exportan.</summary>
    private async Task ExportarAsync()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Exportar pagos",
            Filter = "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv",
            FileName = $"pagos_{ZonaLima.AhoraLima:yyyyMMdd}",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _btnExportar.Enabled = false;
        var textoOriginal = _btnExportar.Text;
        _btnExportar.Text = "Exportando…";
        try
        {
            var f = FiltroActual();
            f.IncluirPruebas = false;
            f.Limite = 1000;
            var todos = new List<Pago>();
            for (f.Desplazamiento = 0; ; f.Desplazamiento += f.Limite)
            {
                var pagina = await _servicio.ListarPaginaAsync(f);
                todos.AddRange(pagina.Items);
                if (!pagina.HayMas) break;
            }

            var xlsx = dlg.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);
            await Task.Run(() =>
            {
                using var fs = File.Create(dlg.FileName);
                if (xlsx) Exportadores.EscribirXlsx(todos, fs); else Exportadores.EscribirCsv(todos, fs);
            });

            Registro.Info($"Exportados {todos.Count} pagos ({(xlsx ? "xlsx" : "csv")})");
            MessageBox.Show(this, $"Se exportaron {todos.Count} pagos.", AppInfo.Nombre,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (IOException ex)
        {
            Registro.Error("No se pudo guardar el archivo exportado", ex);
            MessageBox.Show(this, "No se pudo guardar el archivo. Si está abierto en Excel, ciérralo e inténtalo de nuevo.",
                AppInfo.Nombre, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            Registro.Error("Fallo al exportar", ex);
            MostrarAviso($"No se pudo exportar: {Motivo(ex)}.");
        }
        finally
        {
            _btnExportar.Text = textoOriginal;
            _btnExportar.Enabled = true;
        }
    }

    private async Task BorrarPruebasAsync()
    {
        var r = MessageBox.Show(this, "¿Borrar todos los pagos de prueba? Los pagos reales no se tocan.",
            AppInfo.Nombre, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;
        try
        {
            await _servicio.BorrarPruebasAsync();
            _tabla.LimpiarPruebas();
            OcultarAviso();
            await RecargarAsync();
        }
        catch (Exception ex) { MostrarAviso($"No se pudieron borrar las pruebas: {Motivo(ex)}."); }
    }

    private async Task AbrirAjustesAsync()
    {
        using var f = new AjustesForm(_prefs, _servicio.CorreoActual);
        f.ShowDialog(this);
        if (f.CerrarSesionSolicitado) await CerrarSesionAsync();
    }

    /// <summary>HU-08: cierra sesion y vuelve al login.</summary>
    private async Task CerrarSesionAsync()
    {
        await _monitor.DisposeAsync();
        await _servicio.CerrarSesionAsync();
        CerrarSesionSolicitado = true;
        Close();
    }

    // =====================================================================
    //  Errores
    // =====================================================================
    private static bool EsSesionVencida(Exception ex) => Reintentos.EsSesionVencida(ex);

    private static string Motivo(Exception ex) =>
        EsSesionVencida(ex) ? "la sesión venció" :
        Reintentos.EsTransitorio(ex) ? "sin conexión a internet" :
        ex.Message;

    private async Task ManejarErrorAsync(Exception ex)
    {
        Registro.Error("Error al cargar pagos", ex);
        if (EsSesionVencida(ex) || !_servicio.TieneSesion)
        {
            MessageBox.Show(this, "Tu sesión venció. Inicia sesión de nuevo.", AppInfo.Nombre,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            await CerrarSesionAsync();
            return;
        }

        MostrarAviso(Reintentos.EsTransitorio(ex)
            ? "Sin conexión. Reintentando…"
            : $"No se pudieron cargar los pagos: {ex.Message}");
        _tabla.MensajeVacio = "No se pudieron cargar los pagos.";
        _tabla.Invalidate();
        _reintento.Start();
    }

    private void MostrarAviso(string texto) { _lblAviso.Text = texto; _lblAviso.Visible = true; }
    private void OcultarAviso() { _lblAviso.Visible = false; _reintento.Stop(); }
}
