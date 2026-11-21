using Android.Graphics;
using Android.Graphics.Drawables;

namespace PagoAlerta.Movil.Ui;

/// <summary>Dialogo para elegir de que app se simula el mensaje de prueba (Yape, Plin, BBVA o al azar).</summary>
public static class ElegirAppPrueba
{
    private record Opcion(string Siglas, string Titulo, string Detalle, string? App);

    private static readonly Opcion[] Opciones =
    {
        new("YP", "Yape", "Confirmación de pago", "Yape"),
        new("PL", "Plin", "¡Recibiste un Plin! (dentro de la app de BBVA)", "Plin"),
        new("BB", "BBVA", "Abono a tu cuenta", "BBVA"),
        new("?", "Al azar", "Cualquiera de las tres", null),
    };

    /// <summary>Llama a <paramref name="alElegir"/> con "Yape", "Plin", "BBVA" o null (al azar).</summary>
    public static void Mostrar(Context ctx, Action<string?> alElegir)
    {
        var dialogo = new Dialog(ctx);
        dialogo.RequestWindowFeature((int)WindowFeatures.NoTitle);

        var col = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
        col.Background = Vistas.Forma(ctx, Colores.Tarjeta, 28);
        var pad = Vistas.Px(ctx, 20);
        col.SetPadding(pad, pad, pad, Vistas.Px(ctx, 12));

        col.AddView(Vistas.Etiqueta(ctx, "¿De qué app es la prueba?", 20, Colores.PrimarioOscuro, negrita: true));
        var sub = Vistas.Etiqueta(ctx, "Se verá como un pago real de esa app, en el celular y en la PC.", 14, Colores.TextoSecundario);
        sub.SetLineSpacing(0, 1.15f);
        col.AddView(sub, Vistas.Margen(ctx, 6, 12));

        foreach (var o in Opciones)
        {
            var fila = new LinearLayout(ctx) { Orientation = Orientation.Horizontal };
            fila.SetGravity(GravityFlags.CenterVertical);
            fila.SetPadding(Vistas.Px(ctx, 8), Vistas.Px(ctx, 10), Vistas.Px(ctx, 8), Vistas.Px(ctx, 10));
            fila.Background = Vistas.ConOnda(ctx, Color.Transparent, 16, Color.ParseColor("#221F4FE0"));
            fila.AddView(Vistas.Circulo(ctx, o.Siglas, Colores.AzulClaro, Colores.Primario, 46, 15, cuadrado: true),
                Vistas.Margen(ctx, 0, 0, 0, 14, Vistas.Px(ctx, 46), Vistas.Px(ctx, 46)));

            var tx = new LinearLayout(ctx) { Orientation = Orientation.Vertical };
            tx.AddView(Vistas.Etiqueta(ctx, o.Titulo, 17, Colores.Texto, negrita: true));
            var d = Vistas.Etiqueta(ctx, o.Detalle, 13, Colores.TextoSecundario);
            d.SetLineSpacing(0, 1.1f);
            tx.AddView(d, Vistas.Margen(ctx, 2));
            fila.AddView(tx, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            fila.AddView(Vistas.Icono(ctx, Resource.Drawable.ic_chevron_r, Colores.TextoSecundario, 20));

            var app = o.App;
            fila.Click += (_, _) => { dialogo.Dismiss(); alElegir(app); };
            col.AddView(fila);
        }

        var cancelar = Vistas.Etiqueta(ctx, "Cancelar", 16, Colores.TextoSecundario, negrita: true);
        cancelar.Gravity = GravityFlags.Center;
        cancelar.SetPadding(0, Vistas.Px(ctx, 14), 0, Vistas.Px(ctx, 10));
        cancelar.Click += (_, _) => dialogo.Dismiss();
        col.AddView(cancelar, Vistas.Margen(ctx, 4));

        dialogo.SetContentView(col);
        dialogo.Window!.SetBackgroundDrawable(new ColorDrawable(Color.Transparent));
        dialogo.Show();
        dialogo.Window.SetLayout((int)(ctx.Resources!.DisplayMetrics!.WidthPixels * 0.92), ViewGroup.LayoutParams.WrapContent);
    }
}
