using Android.Graphics;
using PagoAlerta.Core;

namespace PagoAlerta.Movil.Ui;

/// <summary>Insets (barras del sistema): en Android 15+ la app dibuja de borde a borde.</summary>
internal sealed class InsetsListener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
{
    private readonly Action<int, int> _aplicar;
    public InsetsListener(Action<int, int> aplicar) => _aplicar = aplicar;

    public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
    {
#pragma warning disable CA1422
        _aplicar(insets.SystemWindowInsetTop, insets.SystemWindowInsetBottom);
#pragma warning restore CA1422
        return insets;
    }
}

/// <summary>Pantalla con encabezado azul y contenido con scroll. Todas las pantallas internas la heredan.</summary>
public abstract class PantallaBase : Activity
{
    protected LinearLayout Contenido { get; private set; } = null!;

    protected void Construir(string titulo, string? subtitulo = null)
    {
        Window!.SetStatusBarColor(Colores.Primario);
        Window.SetNavigationBarColor(Colores.Fondo);
        Window.DecorView.IconosEstado(claros: true);

        var raiz = new LinearLayout(this) { Orientation = Orientation.Vertical };
        raiz.SetBackgroundColor(Colores.Fondo);

        var cab = new LinearLayout(this) { Orientation = Orientation.Vertical };
        cab.SetBackgroundColor(Colores.Primario);
        var padCab = Vistas.Px(this, 22);
        cab.SetPadding(padCab, padCab, padCab, Vistas.Px(this, 20));
        cab.AddView(Vistas.Etiqueta(this, titulo, 24, Color.White, negrita: true));
        if (subtitulo is not null)
            cab.AddView(Vistas.Etiqueta(this, subtitulo, 14, Color.ParseColor("#D6E0FF")));
        raiz.AddView(cab);

        Contenido = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var pad = Vistas.Px(this, 16);
        Contenido.SetPadding(pad, pad, pad, pad);
        var scroll = new ScrollView(this) { FillViewport = true };
        scroll.AddView(Contenido);
        raiz.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1f));

        raiz.SetOnApplyWindowInsetsListener(new InsetsListener((arriba, abajo) =>
        {
            cab.SetPadding(padCab, padCab + arriba, padCab, Vistas.Px(this, 20));
            Contenido.SetPadding(pad, pad, pad, pad + abajo);
        }));

        SetContentView(raiz);
    }

    protected void Aviso(string mensaje, bool largo = true) =>
        Toast.MakeText(this, mensaje, largo ? ToastLength.Long : ToastLength.Short)?.Show();
}

internal static class DecorExtensiones
{
    /// <summary>Iconos de la barra de estado: claros (sobre fondo azul) u oscuros (sobre fondo claro).</summary>
    public static void IconosEstado(this View decor, bool claros)
    {
        var f = decor.SystemUiFlags;
        decor.SystemUiFlags = claros ? f & ~SystemUiFlags.LightStatusBar : f | SystemUiFlags.LightStatusBar;
    }
}

