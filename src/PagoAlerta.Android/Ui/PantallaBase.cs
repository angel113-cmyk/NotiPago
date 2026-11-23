using Android.Graphics;
using PagoAlerta.Core;
using PagoAlerta.Core.Models;

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

/// <summary>Base de todas las pantallas: barra de estado, insets y mensajes flotantes.</summary>
public abstract class PantallaBase : Activity
{
    /// <summary>Configura la barra de estado. <paramref name="iconosClaros"/> = iconos blancos (sobre fondo azul).</summary>
    protected void ConfigurarBarras(Color barraEstado, bool iconosClaros)
    {
#pragma warning disable CA1422
        Window!.SetStatusBarColor(barraEstado);
        Window.SetNavigationBarColor(Colores.Tarjeta);
        var decor = Window.DecorView;
        var f = decor.SystemUiFlags;
        f = iconosClaros ? f & ~SystemUiFlags.LightStatusBar : f | SystemUiFlags.LightStatusBar;
        f |= SystemUiFlags.LightNavigationBar;
        decor.SystemUiFlags = f;
#pragma warning restore CA1422
    }

    protected static void AplicarInsets(View raiz, Action<int, int> aplicar) =>
        raiz.SetOnApplyWindowInsetsListener(new InsetsListener(aplicar));

    /// <summary>
    /// Pantalla secundaria: boton circular de atras + titulo, y un contenido con scroll.
    /// Devuelve la raiz ya con los márgenes del sistema aplicados.
    /// </summary>
    protected LinearLayout PantallaConAtras(string titulo, out LinearLayout contenido, out ScrollView scroll)
    {
        ConfigurarBarras(Colores.Fondo, iconosClaros: false);
        var raiz = new LinearLayout(this) { Orientation = Orientation.Vertical };
        raiz.SetBackgroundColor(Colores.Fondo);

        var barra = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        barra.SetGravity(GravityFlags.CenterVertical);
        var atras = new FrameLayout(this);
        atras.Background = Vistas.ConOnda(this, Colores.Tarjeta, 30, Color.ParseColor("#221F4FE0"), Colores.BordeSuave);
        atras.Elevation = Vistas.Px(this, 2);
        atras.AddView(Vistas.Icono(this, Resource.Drawable.ic_chevron_l, Colores.Primario, 24),
            new FrameLayout.LayoutParams(Vistas.Px(this, 24), Vistas.Px(this, 24), GravityFlags.Center));
        atras.Click += (_, _) => Finish();
        Vistas.EfectoPresion(atras, 0.92f);
        barra.AddView(atras, Vistas.Margen(this, 0, 0, 0, 14, Vistas.Px(this, 46), Vistas.Px(this, 46)));
        barra.AddView(Vistas.Etiqueta(this, titulo, 25, Colores.PrimarioOscuro, negrita: true));
        raiz.AddView(barra, Vistas.Margen(this));

        scroll = new ScrollView(this) { FillViewport = true, VerticalScrollBarEnabled = false };
        var col = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var pad = Vistas.Px(this, 20);
        col.SetPadding(pad, Vistas.Px(this, 6), pad, Vistas.Px(this, 24));
        scroll.AddView(col);
        raiz.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        AplicarInsets(raiz, (arriba, abajo) =>
        {
            barra.SetPadding(Vistas.Px(this, 20), arriba + Vistas.Px(this, 14), Vistas.Px(this, 20), Vistas.Px(this, 12));
            col.SetPadding(pad, Vistas.Px(this, 6), pad, Vistas.Px(this, 24) + abajo);
        });
        contenido = col;
        return raiz;
    }

    /// <summary>Mensaje flotante (pildora) que aparece desde abajo y se oculta solo.</summary>
    public void Mensaje(string texto, bool error = false, bool largo = false)
    {
        var contenedor = FindViewById<FrameLayout>(Android.Resource.Id.Content);
        if (contenedor is null) return;

        var t = Vistas.Etiqueta(this, texto, 14, Color.White, negrita: true);
        t.SetPadding(Vistas.Px(this, 20), Vistas.Px(this, 13), Vistas.Px(this, 20), Vistas.Px(this, 13));
        t.Background = Vistas.Forma(this, error ? Color.ParseColor("#B91C1C") : Colores.PrimarioOscuro, 30);
        t.Elevation = Vistas.Px(this, 8);
        t.Gravity = GravityFlags.Center;
        var lp = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent,
            GravityFlags.Bottom | GravityFlags.CenterHorizontal);
        lp.SetMargins(Vistas.Px(this, 24), 0, Vistas.Px(this, 24), Vistas.Px(this, 96));
        contenedor.AddView(t, lp);

        t.Alpha = 0f; t.TranslationY = Vistas.Px(this, 24);
        t.Animate()!.Alpha(1f).TranslationY(0f).SetDuration(260).Start();
        t.PostDelayed(() =>
        {
            t.Animate()!.Alpha(0f).TranslationY(Vistas.Px(this, 24)).SetDuration(260)
                .WithEndAction(new Java.Lang.Runnable(() => contenedor.RemoveView(t))).Start();
        }, largo ? 4200 : 2600);
    }

    /// <summary>Compatibilidad con el codigo anterior (Toast -> mensaje flotante).</summary>
    protected void Aviso(string mensaje, bool largo = true) => Mensaje(mensaje, error: false, largo: largo);
}

/// <summary>Tarjetas compartidas: movimiento (pago) del listado.</summary>
public static class Tarjetas
{
    /// <summary>Tarjeta de un pago: iniciales, nombre, chips (app / PRUEBA), hora y monto a la derecha.</summary>
    public static LinearLayout Movimiento(Context c, Pago p)
    {
        var fila = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        fila.SetGravity(GravityFlags.CenterVertical);
        fila.Background = Vistas.ConOnda(c, Colores.Tarjeta, 20, Color.ParseColor("#221F4FE0"), Colores.BordeSuave);
        fila.Elevation = Vistas.Px(c, 2);
        var pad = Vistas.Px(c, 16);
        fila.SetPadding(pad, pad, pad, pad);
        Vistas.EfectoPresion(fila, 0.985f);

        var nombre = string.IsNullOrWhiteSpace(p.Remitente) ? "(sin nombre)" : p.Remitente!;
        fila.AddView(Vistas.Circulo(c, Formato.Iniciales(p.Remitente), Colores.AzulClaro, Colores.Primario, 52, 17));

        var medio = new LinearLayout(c) { Orientation = Orientation.Vertical };
        var n = Vistas.Etiqueta(c, nombre, 16, Colores.Texto, negrita: true);
        n.SetSingleLine(true);
        n.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        medio.AddView(n);

        var etiquetas = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        if (!string.IsNullOrEmpty(p.App))
            etiquetas.AddView(Vistas.Pildora(c, p.App!.ToUpperInvariant(), Colores.AzulClaro, Colores.Primario, 11),
                Vistas.Margen(c, 5, 0, 0, 6, ViewGroup.LayoutParams.WrapContent));
        if (p.EsPrueba)
            etiquetas.AddView(Vistas.Pildora(c, "PRUEBA", Colores.AcentoClaro, Color.ParseColor("#0E7490"), 11),
                Vistas.Margen(c, 5, 0, 0, 6, ViewGroup.LayoutParams.WrapContent));
        if (p.EsRevisar())
            etiquetas.AddView(Vistas.Pildora(c, "REVISAR", Color.ParseColor("#FEF3C7"), Color.ParseColor("#B45309"), 11),
                Vistas.Margen(c, 5, 0, 0, 6, ViewGroup.LayoutParams.WrapContent));
        if (etiquetas.ChildCount > 0) medio.AddView(etiquetas);

        var detalle = p.Tipo == TipoPago.Recibido ? "Pago confirmado" : p.Tipo == TipoPago.Enviado ? "Pago enviado" : p.EsRevisar() ? "No reconocido" : "Movimiento";
        medio.AddView(Vistas.Etiqueta(c, $"{Formato.Hora24(p.FechaHora)} · {detalle}", 13, Colores.TextoSecundario),
            Vistas.Margen(c, 5));
        var lpMedio = Vistas.Margen(c, 0, 0, 14, 8, 0, ViewGroup.LayoutParams.WrapContent);
        lpMedio.Weight = 1f;
        fila.AddView(medio, lpMedio);

        var derecha = new LinearLayout(c) { Orientation = Orientation.Vertical };
        derecha.SetGravity(GravityFlags.End);
        var color = p.Tipo == TipoPago.Recibido ? Colores.Verde : p.Tipo == TipoPago.Enviado ? Color.ParseColor("#374151") : Colores.TextoSecundario;
        var signo = p.Tipo == TipoPago.Recibido ? "+" : p.Tipo == TipoPago.Enviado ? "−" : "";
        var monto = Vistas.Etiqueta(c, p.EsRevisar() ? "—" : signo + Formato.Monto(p.Monto, p.Moneda), 19, color, negrita: true);
        monto.Gravity = GravityFlags.End;
        derecha.AddView(monto);
        var tipo = p.Tipo.Length > 0 ? char.ToUpperInvariant(p.Tipo[0]) + p.Tipo[1..] : "";
        var t2 = Vistas.Etiqueta(c, tipo, 13, Colores.TextoSecundario);
        t2.Gravity = GravityFlags.End;
        derecha.AddView(t2, Vistas.Margen(c, 3));
        fila.AddView(derecha, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
        return fila;
    }
}
