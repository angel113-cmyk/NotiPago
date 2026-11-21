using Android.Graphics;
using PagoAlerta.Core.Logging;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil;

/// <summary>HT-17: registro de errores del celular (solo eventos y tipos de error, sin datos de pagos).</summary>
[Activity(Label = "Registro de errores", Exported = false, Theme = "@android:style/Theme.DeviceDefault.Light.NoActionBar")]
public class RegistroActivity : PantallaBase
{
    private TextView _texto = null!;
    private TextView _vacio = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var raiz = PantallaConAtras("Registro de errores", out var c, out _);
        SetContentView(raiz);

        var intro = Vistas.Etiqueta(this, "Aquí se guardan los eventos y errores de la app. No incluyen nombres ni montos.", 15, Colores.TextoSecundario);
        intro.SetLineSpacing(0, 1.15f);
        c.AddView(intro, Vistas.Margen(this, 0, 16));

        var tarjeta = Vistas.Tarjeta(this, padDp: 16);
        _vacio = Vistas.Etiqueta(this, "Aún no hay registros.", 15, Colores.TextoSecundario);
        _vacio.Gravity = GravityFlags.Center;
        tarjeta.AddView(_vacio, Vistas.Margen(this, 12, 12));
        _texto = Vistas.Etiqueta(this, "", 12, Colores.Texto);
        _texto.Typeface = Typeface.Monospace;
        _texto.SetTextIsSelectable(true);
        _texto.SetLineSpacing(0, 1.2f);
        tarjeta.AddView(_texto);
        c.AddView(tarjeta, Vistas.Margen(this, 0, 16));

        var fila = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        var recargar = Vistas.Boton(this, "Actualizar", EstiloBoton.Contorno, Cargar, compacto: true);
        var lp = Vistas.Margen(this, 0, 0, 0, 0, 0); lp.Weight = 1f;
        fila.AddView(recargar, lp);
        c.AddView(fila, Vistas.Margen(this));

        for (var i = 0; i < c.ChildCount; i++) Anim.Entrar(c.GetChildAt(i)!, i * 70, 16);
    }

    protected override void OnResume()
    {
        base.OnResume();
        Cargar();
    }

    private void Cargar()
    {
        var ruta = Registro.RutaActual;
        var contenido = "";
        try
        {
            if (ruta is not null && File.Exists(ruta))
            {
                var lineas = File.ReadAllLines(ruta);
                // Lo mas reciente primero (ultimas 120 lineas).
                contenido = string.Join("\n", lineas.Skip(Math.Max(0, lineas.Length - 120)).Reverse());
            }
        }
        catch (Exception ex) { contenido = "No se pudo leer el registro: " + ex.Message; }

        _texto.Text = contenido;
        _vacio.Visibility = contenido.Length == 0 ? ViewStates.Visible : ViewStates.Gone;
    }
}
