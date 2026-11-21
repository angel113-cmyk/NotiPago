using Android.Graphics;
using PagoAlerta.Core.Logging;
using PagoAlerta.Core.Models;
using PagoAlerta.Core.Services;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil;

/// <summary>"Ver todos": todos los movimientos, del mas reciente al mas antiguo, con carga progresiva.</summary>
[Activity(Label = "Movimientos", Exported = false, Theme = "@android:style/Theme.DeviceDefault.Light.NoActionBar")]
public class MovimientosActivity : PantallaBase
{
    private const int Pagina = 30;
    private LinearLayout _lista = null!, _contenido = null!;
    private TextView _estado = null!;
    private Button _mas = null!;
    private int _desplazamiento;
    private bool _cargando;
    private bool _verPruebas = true;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var raiz = PantallaConAtras("Movimientos", out _contenido, out _);
        SetContentView(raiz);

        // Filtro de pruebas
        var fila = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        fila.SetGravity(GravityFlags.CenterVertical);
        var txt = Vistas.Etiqueta(this, "Mostrar mensajes de prueba", 15, Colores.Texto);
        fila.AddView(txt, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        var sw = Vistas.Interruptor(this, _verPruebas, Colores.Primario);
        sw.CheckedChange += async (_, e) => { _verPruebas = e.IsChecked; await RecargarAsync(); };
        fila.AddView(sw);
        _contenido.AddView(fila, Vistas.Margen(this, 4, 14));

        _lista = new LinearLayout(this) { Orientation = Orientation.Vertical };
        _contenido.AddView(_lista, Vistas.Margen(this));

        _estado = Vistas.Etiqueta(this, "Cargando…", 15, Colores.TextoSecundario);
        _estado.Gravity = GravityFlags.Center;
        _contenido.AddView(_estado, Vistas.Margen(this, 12, 12));

        _mas = Vistas.Boton(this, "Cargar más", EstiloBoton.Contorno, async () => await CargarAsync(), compacto: true);
        _mas.Visibility = ViewStates.Gone;
        _contenido.AddView(_mas, Vistas.Margen(this, 4));
    }

    protected override async void OnResume()
    {
        base.OnResume();
        if (_lista.ChildCount == 0) await RecargarAsync();
    }

    private async Task RecargarAsync()
    {
        _lista.RemoveAllViews();
        _desplazamiento = 0;
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        if (_cargando) return;
        _cargando = true;
        _mas.Visibility = ViewStates.Gone;
        _estado.Visibility = ViewStates.Visible;
        _estado.Text = "Cargando…";
        try
        {
            if (!await Servicios.AsegurarSesionAsync()) { _estado.Text = "Tu sesión venció. Inicia sesión de nuevo."; return; }
            var pag = await Servicios.Pago!.ListarPaginaAsync(new FiltroPagos
            {
                IncluirPruebas = _verPruebas, Limite = Pagina, Desplazamiento = _desplazamiento
            });
            var inicio = _lista.ChildCount;
            foreach (var p in pag.Items) _lista.AddView(Tarjetas.Movimiento(this, p), Vistas.Margen(this, 0, 12));
            _desplazamiento += Pagina;

            // Entrada escalonada solo para lo recien agregado
            for (var i = inicio; i < _lista.ChildCount; i++) Anim.Entrar(_lista.GetChildAt(i)!, Math.Min(i - inicio, 8) * 40, 12);

            _estado.Visibility = _lista.ChildCount == 0 ? ViewStates.Visible : ViewStates.Gone;
            if (_lista.ChildCount == 0) _estado.Text = "Aún no hay movimientos.";
            _mas.Visibility = pag.HayMas ? ViewStates.Visible : ViewStates.Gone;
        }
        catch (Exception ex)
        {
            Registro.Error("No se pudieron cargar los movimientos", ex);
            _estado.Text = Reintentos.EsTransitorio(ex) ? "Sin conexión. Inténtalo de nuevo." : "No se pudieron cargar los movimientos.";
            _mas.Visibility = ViewStates.Visible;
        }
        finally { _cargando = false; }
    }
}
