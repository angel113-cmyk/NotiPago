using System.Media;
using PagoAlerta.Core;
using PagoAlerta.Core.Models;
using PagoAlerta.Core.Services;
using PagoAlerta.Core.Time;
using PagoAlerta.Desktop.Services;
using PagoAlerta.Desktop.Ui;

namespace PagoAlerta.Desktop;

/// <summary>
/// Pantalla principal: resumen del dia, filtros, lista de pagos en tiempo real, detalle y ajustes.
/// HU-02, 03, 04, 05, 06, 08 y HU-20 (parte PC).
/// </summary>
public class MainForm : Form
{
    private const int TamPagina = 50;

    private readonly PagoService _servicio;
    private readonly PagoMonitor _monitor;
    private readonly Preferencias _prefs = Preferencias.Cargar();

    // Encabezado
    private readonly Label _lblTitulo = new(), _lblTotal = new(), _lblCantidad = new(), _lblUsuario = new();
    private readonly Button _btnAjustes = new();

    // Filtros
    private readonly ComboBox _cmbPeriodo = new(), _cmbApp = new();
    private readonly DateTimePicker _dtpDesde = new(), _dtpHasta = new();
    private readonly TextBox _txtBuscar = new();
    private readonly Button _btnLimpiar = new(), _btnEnviarPrueba = new(), _btnBorrarPruebas = new();
    private readonly CheckBox _chkPruebas = new();
    private readonly Label _lblEstado = new(), _lblAviso = new();

    private readonly ListaPagos _lista = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _debounceBusqueda = new() { Interval = 350 };
    private readonly System.Windows.Forms.Timer _reintento = new() { Interval = 10000 };

    private int _version, _desplazamiento;
    private bool _hayMas, _cargando, _suspender;

    public bool CerrarSesionSolicitado { get; private set; }

    public MainForm(PagoService servicio)
    {
        _servicio = servicio;
        _monitor = new PagoMonitor(servicio);

        Text = AppInfo.Nombre;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente(10f);
        ClientSize = new Size(1000, 700);
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterScreen;

        ConstruirUi();

        Shown += async (_, _) => await IniciarAsync();
        FormClosing += (_, _) => _ = _monitor.DisposeAsync().AsTask();
        _debounceBusqueda.Tick += async (_, _) => { _debounceBusqueda.Stop(); await RecargarAsync(); };
        _reintento.Tick += async (_, _) => { if (_lblAviso.Visible) await RecargarAsync(); else _reintento.Stop(); };
    }

    // =====================================================================
    //  UI
    // =====================================================================
    private void ConstruirUi()
    {
        // ---- Encabezado azul ----
        var encabezado = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = Tema.Primario };
        _lblTitulo.Text = "Total de hoy";
        _lblTitulo.ForeColor = Color.FromArgb(210, 255, 255, 255);
        _lblTitulo.Font = Tema.Fuente(11f);
        _lblTitulo.AutoSize = true;
        _lblTitulo.Location = new Point(32, 26);
        _lblTotal.Text = Formato.Monto(0);
        _lblTotal.ForeColor = Color.White;
        _lblTotal.Font = Tema.Fuente(32f, FontStyle.Bold);
        _lblTotal.AutoSize = true;
        _lblTotal.Location = new Point(28, 50);
        _lblCantidad.Text = "0 pagos";
        _lblCantidad.ForeColor = Color.FromArgb(210, 255, 255, 255);
        _lblCantidad.Font = Tema.Fuente(11f);
        _lblCantidad.AutoSize = true;
        _lblCantidad.Location = new Point(32, 110);
        _lblUsuario.Text = _servicio.CorreoActual ?? "";
        _lblUsuario.ForeColor = Color.FromArgb(210, 255, 255, 255);
        _lblUsuario.Font = Tema.Fuente(9.5f);
        _lblUsuario.AutoSize = true;
        foreach (var l in new[] { _lblTitulo, _lblTotal, _lblCantidad, _lblUsuario }) l.BackColor = Color.Transparent;

        _btnAjustes.Text = "Ajustes";
        _btnAjustes.FlatStyle = FlatStyle.Flat;
        _btnAjustes.FlatAppearance.BorderSize = 0;
        _btnAjustes.BackColor = Tema.PrimarioOscuro;
        _btnAjustes.ForeColor = Color.White;
        _btnAjustes.Cursor = Cursors.Hand;
        _btnAjustes.Font = Tema.Fuente(9.5f, FontStyle.Bold);
        _btnAjustes.Size = new Size(110, 36);
        _btnAjustes.Click += async (_, _) => await AbrirAjustesAsync();

        encabezado.Controls.AddRange(new Control[] { _lblTitulo, _lblTotal, _lblCantidad, _lblUsuario, _btnAjustes });
        encabezado.Resize += (_, _) =>
        {
            _btnAjustes.Location = new Point(encabezado.Width - _btnAjustes.Width - 32, 24);
            _lblUsuario.Location = new Point(encabezado.Width - _lblUsuario.Width - 32, 70);
        };

        // ---- Filtros ----
        var filtros = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Tema.Fondo, Padding = new Padding(24, 12, 24, 0) };
        var fila1 = NuevaFila();
        var fila2 = NuevaFila();
        fila2.Top = 48;

        _cmbPeriodo.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbPeriodo.Items.AddRange(new object[] { "Hoy", "Ayer", "Rango de fechas" });
        _cmbPeriodo.SelectedIndex = 0;
        _cmbPeriodo.Width = 140;

        var hoy = ZonaLima.AhoraLima.Date;
        foreach (var d in new[] { _dtpDesde, _dtpHasta })
        {
            d.Format = DateTimePickerFormat.Short;
            d.Value = hoy;
            d.MaxDate = hoy.AddDays(1);
            d.Width = 110;
            d.Visible = false;
        }

        _cmbApp.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbApp.Items.AddRange(new object[] { "Todas las apps", "Yape", "Plin", "BBVA" });
        _cmbApp.SelectedIndex = 0;
        _cmbApp.Width = 140;

        _txtBuscar.PlaceholderText = "Buscar por nombre…";
        _txtBuscar.Width = 240;

        Configurar(_btnLimpiar, "Limpiar", 90);
        _btnLimpiar.Click += async (_, _) => await LimpiarFiltrosAsync();

        _chkPruebas.Text = "Incluir pruebas";
        _chkPruebas.AutoSize = true;
        _chkPruebas.ForeColor = Tema.TextoSecundario;
        _chkPruebas.Margin = new Padding(3, 8, 12, 0);

        Configurar(_btnEnviarPrueba, "Enviar prueba", 120);
        Configurar(_btnBorrarPruebas, "Borrar pruebas", 120);
        _btnEnviarPrueba.Click += async (_, _) => await EnviarPruebaAsync();
        _btnBorrarPruebas.Click += async (_, _) => await BorrarPruebasAsync();

        _lblEstado.AutoSize = true;
        _lblEstado.Margin = new Padding(18, 8, 0, 0);
        ActualizarEstadoRealtime(false);

        fila1.Controls.AddRange(new Control[] { _cmbPeriodo, _dtpDesde, _dtpHasta, _cmbApp, _txtBuscar, _btnLimpiar });
        fila2.Controls.AddRange(new Control[] { _chkPruebas, _btnEnviarPrueba, _btnBorrarPruebas, _lblEstado });
        filtros.Controls.Add(fila2);
        filtros.Controls.Add(fila1);

        // Eventos de filtros
        _cmbPeriodo.SelectedIndexChanged += async (_, _) =>
        {
            var rango = _cmbPeriodo.SelectedIndex == 2;
            _dtpDesde.Visible = _dtpHasta.Visible = rango;
            await RecargarSiNoSuspendidoAsync();
        };
        _dtpDesde.ValueChanged += async (_, _) => await RecargarSiNoSuspendidoAsync();
        _dtpHasta.ValueChanged += async (_, _) => await RecargarSiNoSuspendidoAsync();
        _cmbApp.SelectedIndexChanged += async (_, _) => await RecargarSiNoSuspendidoAsync();
        _chkPruebas.CheckedChanged += async (_, _) => await RecargarSiNoSuspendidoAsync();
        _txtBuscar.TextChanged += (_, _) => { if (!_suspender) { _debounceBusqueda.Stop(); _debounceBusqueda.Start(); } };

        // ---- Aviso de errores ----
        _lblAviso.Dock = DockStyle.Top;
        _lblAviso.Height = 34;
        _lblAviso.BackColor = Color.FromArgb(254, 226, 226);
        _lblAviso.ForeColor = Tema.Error;
        _lblAviso.TextAlign = ContentAlignment.MiddleCenter;
        _lblAviso.Font = Tema.Fuente(10f, FontStyle.Bold);
        _lblAviso.Visible = false;

        _lista.PagoClick += p => { using var f = new DetalleForm(p); f.ShowDialog(this); };
        _lista.CercaDelFinal += async () => await CargarMasAsync();

        // El orden de adicion define el apilado: Fill primero, encabezado al final (queda arriba).
        Controls.Add(_lista);
        Controls.Add(_lblAviso);
        Controls.Add(filtros);
        Controls.Add(encabezado);
    }

    private static FlowLayoutPanel NuevaFila() => new()
    {
        Left = 24, Top = 12, Height = 36, Width = 4000, WrapContents = false, AutoSize = false,
        BackColor = Color.Transparent
    };

    private static void Configurar(Button b, string texto, int ancho)
    {
        b.Text = texto;
        b.Width = ancho;
        b.Height = 30;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Tema.Borde;
        b.BackColor = Tema.Tarjeta;
        b.ForeColor = Tema.Primario;
        b.Cursor = Cursors.Hand;
        b.Font = Tema.Fuente(9.5f, FontStyle.Bold);
    }

    private void ActualizarEstadoRealtime(bool activo)
    {
        _lblEstado.Text = activo ? "● Tiempo real activo" : "● Actualizando cada pocos segundos";
        _lblEstado.ForeColor = activo ? Tema.Recibido : Tema.TextoSecundario;
    }

    // =====================================================================
    //  Filtros
    // =====================================================================
    private (DateTime Desde, DateTime Hasta, string Titulo) RangoSeleccionado()
    {
        var hoy = ZonaLima.AhoraLima.Date;
        switch (_cmbPeriodo.SelectedIndex)
        {
            case 1:
                var (d1, h1) = ZonaLima.RangoDia(hoy.AddDays(-1));
                return (d1, h1, "Total de ayer");
            case 2:
                var a = _dtpDesde.Value.Date; var b = _dtpHasta.Value.Date;
                if (a > b) (a, b) = (b, a);
                return (ZonaLima.RangoDia(a).DesdeUtc, ZonaLima.RangoDia(b).HastaUtc, "Total del rango");
            default:
                var (d0, h0) = ZonaLima.RangoDia(hoy);
                return (d0, h0, "Total de hoy");
        }
    }

    private FiltroPagos FiltroActual()
    {
        var r = RangoSeleccionado();
        return new FiltroPagos
        {
            DesdeUtc = r.Desde,
            HastaUtc = r.Hasta,
            App = _cmbApp.SelectedIndex > 0 ? _cmbApp.SelectedItem!.ToString() : null,
            Nombre = string.IsNullOrWhiteSpace(_txtBuscar.Text) ? null : _txtBuscar.Text.Trim(),
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
        _cmbPeriodo.SelectedIndex = 0;
        _cmbApp.SelectedIndex = 0;
        _txtBuscar.Clear();
        _chkPruebas.Checked = false;
        _dtpDesde.Value = _dtpHasta.Value = ZonaLima.AhoraLima.Date;
        _suspender = false;
        _debounceBusqueda.Stop();
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
        _lista.MensajeVacio = "Cargando pagos…";
        _lista.Reemplazar(Array.Empty<Pago>());
        _desplazamiento = 0;
        _hayMas = true;

        try
        {
            await LlenarAsync(version);
            if (version != _version) return;
            _lista.MensajeVacio = "No hay pagos con estos filtros.";
            _lista.Invalidate();
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

    /// <summary>Carga paginas hasta mostrar suficientes tarjetas (la busqueda por nombre puede dejar paginas casi vacias).</summary>
    private async Task LlenarAsync(int version)
    {
        for (var intento = 0; intento < 6 && _hayMas && (intento == 0 || _lista.Cantidad < 15); intento++)
        {
            var f = FiltroActual();
            f.Desplazamiento = _desplazamiento;
            var pagina = await _servicio.ListarPaginaAsync(f);
            if (version != _version) return;
            _desplazamiento += TamPagina;
            _hayMas = pagina.HayMas;
            foreach (var p in pagina.Items) _monitor.MarcarVisto(p.Id);
            _lista.Agregar(pagina.Items);
        }
    }

    private async Task CargarMasAsync()
    {
        if (_cargando || !_hayMas) return;
        var version = _version;
        _cargando = true;
        try { await LlenarAsync(version); }
        catch (Exception ex) when (version == _version) { await ManejarErrorAsync(ex); }
        finally { if (version == _version) _cargando = false; }
    }

    private async Task ActualizarResumenAsync(int version)
    {
        try
        {
            var r = RangoSeleccionado();
            var f = FiltroActual();
            var resumen = await _servicio.ResumenAsync(f);
            if (version != _version && version != 0) { /* un cambio de filtro lo recalcula */ }
            _lblTitulo.Text = r.Titulo;
            _lblTotal.Text = Formato.Monto(resumen.Total);
            _lblCantidad.Text = resumen.Cantidad == 1 ? "1 pago" : $"{resumen.Cantidad} pagos";
        }
        catch { /* el listado ya muestra el error; el resumen se recalcula en el proximo ciclo */ }
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
        if (!Coincide(p)) return;
        _lista.InsertarResaltado(p);
        if (_prefs.Sonido) SystemSounds.Asterisk.Play();
        if (!p.EsPrueba && p.Tipo == TipoPago.Recibido) _ = ActualizarResumenAsync(_version);
    });

    // =====================================================================
    //  Acciones
    // =====================================================================
    private async Task EnviarPruebaAsync()
    {
        _btnEnviarPrueba.Enabled = false;
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
        finally { _btnEnviarPrueba.Enabled = true; }
    }

    private async Task BorrarPruebasAsync()
    {
        var r = MessageBox.Show(this, "¿Borrar todos los pagos de prueba? Los pagos reales no se tocan.",
            AppInfo.Nombre, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;
        try
        {
            await _servicio.BorrarPruebasAsync();
            _lista.LimpiarPruebas();
            OcultarAviso();
            await RecargarAsync();
        }
        catch (Exception ex) { MostrarAviso($"No se pudieron borrar las pruebas: {Motivo(ex)}."); }
    }

    private async Task AbrirAjustesAsync()
    {
        using var f = new AjustesForm(_prefs, _servicio.CorreoActual);
        f.ShowDialog(this);
        if (!f.CerrarSesionSolicitado) return;

        await _monitor.DisposeAsync();
        await _servicio.CerrarSesionAsync();
        CerrarSesionSolicitado = true;
        Close();
    }

    // =====================================================================
    //  Errores
    // =====================================================================
    private static bool EsSesionVencida(Exception ex)
    {
        var m = ex.ToString();
        return m.Contains("JWT", StringComparison.OrdinalIgnoreCase)
            || m.Contains("401") || m.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase);
    }

    private static string Motivo(Exception ex) =>
        EsSesionVencida(ex) ? "la sesión venció" :
        Reintentos.EsTransitorio(ex) ? "sin conexión a internet" :
        ex.Message;

    private async Task ManejarErrorAsync(Exception ex)
    {
        if (EsSesionVencida(ex) || !_servicio.TieneSesion)
        {
            MessageBox.Show(this, "Tu sesión venció. Inicia sesión de nuevo.", AppInfo.Nombre,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            await _monitor.DisposeAsync();
            await _servicio.CerrarSesionAsync();
            CerrarSesionSolicitado = true;
            Close();
            return;
        }

        MostrarAviso(Reintentos.EsTransitorio(ex)
            ? "Sin conexión. Reintentando…"
            : $"No se pudieron cargar los pagos: {ex.Message}");
        _lista.MensajeVacio = "No se pudieron cargar los pagos.";
        _lista.Invalidate();
        _reintento.Start();
    }

    private void MostrarAviso(string texto) { _lblAviso.Text = texto; _lblAviso.Visible = true; }
    private void OcultarAviso() { _lblAviso.Visible = false; _reintento.Stop(); }
}
