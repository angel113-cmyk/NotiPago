using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Util;

namespace PagoAlerta.Movil.Ui;

/// <summary>Paleta azul (la misma del escritorio) y constructores de vistas con estilo.</summary>
public static class Colores
{
    public static readonly Color Primario = Color.ParseColor("#1F4FE0");
    public static readonly Color PrimarioOscuro = Color.ParseColor("#0B2A8F");
    public static readonly Color AzulClaro = Color.ParseColor("#EAF0FF");
    public static readonly Color Acento = Color.ParseColor("#19C6E0");
    public static readonly Color Fondo = Color.ParseColor("#F5F7FB");
    public static readonly Color Tarjeta = Color.White;
    public static readonly Color Texto = Color.ParseColor("#1B1F2A");
    public static readonly Color TextoSecundario = Color.ParseColor("#6B7280");
    public static readonly Color Verde = Color.ParseColor("#16A34A");
    public static readonly Color Ambar = Color.ParseColor("#D97706");
    public static readonly Color Error = Color.ParseColor("#DC2626");
    public static readonly Color Borde = Color.ParseColor("#D9DFEC");
}

public static class Vistas
{
    public const float RadioTarjeta = 16, RadioCampo = 12;

    public static int Px(Context c, float dp) =>
        (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, c.Resources!.DisplayMetrics);

    public static GradientDrawable Forma(Context c, Color fondo, float radioDp, Color? borde = null)
    {
        var g = new GradientDrawable();
        g.SetColor(fondo);
        g.SetCornerRadius(Px(c, radioDp));
        if (borde is { } b) g.SetStroke(Px(c, 1), b);
        return g;
    }

    public static TextView Etiqueta(Context c, string texto, float sp, Color color, bool negrita = false)
    {
        var t = new TextView(c) { Text = texto };
        t.SetTextSize(ComplexUnitType.Sp, sp);
        t.SetTextColor(color);
        if (negrita) t.SetTypeface(Typeface.DefaultBold, TypefaceStyle.Bold);
        return t;
    }

    public static LinearLayout.LayoutParams Margen(Context c, float arriba = 0, float abajo = 0, float izq = 0, float der = 0,
        int ancho = ViewGroup.LayoutParams.MatchParent, int alto = ViewGroup.LayoutParams.WrapContent)
    {
        var p = new LinearLayout.LayoutParams(ancho, alto);
        p.SetMargins(Px(c, izq), Px(c, arriba), Px(c, der), Px(c, abajo));
        return p;
    }

    /// <summary>Tarjeta blanca, esquinas redondeadas (16) y sombra suave.</summary>
    public static LinearLayout Tarjeta(Context c)
    {
        var l = new LinearLayout(c) { Orientation = Orientation.Vertical };
        l.Background = Forma(c, Colores.Tarjeta, RadioTarjeta, Colores.Borde);
        l.SetPadding(Px(c, 18), Px(c, 16), Px(c, 18), Px(c, 16));
        l.Elevation = Px(c, 2);
        return l;
    }

    public static Button Boton(Context c, string texto, bool primario, Action alClic)
    {
        var fondo = primario ? Colores.Primario : Colores.Tarjeta;
        var presionado = primario ? Colores.PrimarioOscuro : Colores.AzulClaro;
        var b = new Button(c) { Text = texto };
        b.SetAllCaps(false);
        b.SetTextSize(ComplexUnitType.Sp, 16);
        b.SetTypeface(Typeface.DefaultBold, TypefaceStyle.Bold);
        b.SetTextColor(primario ? Color.White : Colores.Primario);
        b.StateListAnimator = null;
        b.SetMinHeight(Px(c, 52));

        var estados = new StateListDrawable();
        estados.AddState(new[] { Android.Resource.Attribute.StatePressed },
            Forma(c, presionado, RadioCampo, primario ? null : Colores.Primario));
        estados.AddState(new[] { -Android.Resource.Attribute.StateEnabled },
            Forma(c, primario ? Color.ParseColor("#9DB2F0") : Colores.Fondo, RadioCampo, primario ? null : Colores.Borde));
        estados.AddState(new int[0], Forma(c, fondo, RadioCampo, primario ? null : Colores.Primario));
        b.Background = estados;
        b.Click += (_, _) => alClic();
        return b;
    }

    public static EditText Campo(Context c, string pista, bool clave = false)
    {
        var e = new EditText(c) { Hint = pista };
        e.SetTextSize(ComplexUnitType.Sp, 16);
        e.SetTextColor(Colores.Texto);
        e.SetHintTextColor(Colores.TextoSecundario);
        e.Background = Forma(c, Colores.Tarjeta, RadioCampo, Colores.Borde);
        e.SetPadding(Px(c, 16), Px(c, 14), Px(c, 16), Px(c, 14));
        e.SetSingleLine(true);
        e.InputType = clave
            ? InputTypes.ClassText | InputTypes.TextVariationPassword
            : InputTypes.ClassText | InputTypes.TextVariationEmailAddress;
        return e;
    }

    public static View Separador(Context c)
    {
        var v = new View(c);
        v.SetBackgroundColor(Colores.Borde);
        v.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Px(c, 1));
        return v;
    }

    /// <summary>Fila "titulo ........ valor" para las tarjetas de datos.</summary>
    public static (LinearLayout Fila, TextView Valor) FilaDato(Context c, string titulo, string valor)
    {
        var fila = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        fila.SetPadding(0, Px(c, 8), 0, Px(c, 8));
        var t = Etiqueta(c, titulo, 14, Colores.TextoSecundario);
        t.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f);
        var v = Etiqueta(c, valor, 14, Colores.Texto, negrita: true);
        v.Gravity = GravityFlags.End;
        v.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1.4f);
        fila.AddView(t);
        fila.AddView(v);
        return (fila, v);
    }
}

/// <summary>Logo propio (campana + S/): no usa marcas de terceros.</summary>
public class LogoView : View
{
    private readonly Paint _p = new(PaintFlags.AntiAlias);

    public LogoView(Context c) : base(c) { }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        float s = Math.Min(Width, Height);
        _p.SetStyle(Paint.Style.Fill);
        _p.Color = Colores.Primario;
        canvas.DrawRoundRect(0, 0, s, s, s * 0.26f, s * 0.26f, _p);

        float cx = s / 2f, top = s * 0.14f;
        _p.Color = Color.White;
        using (var campana = new Android.Graphics.Path())
        {
            campana.AddArc(cx - s * 0.19f, top, cx + s * 0.19f, top + s * 0.38f, 180, 180);
            campana.LineTo(cx + s * 0.23f, top + s * 0.40f);
            campana.LineTo(cx - s * 0.23f, top + s * 0.40f);
            campana.Close();
            canvas.DrawPath(campana, _p);
        }
        canvas.DrawOval(cx - s * 0.05f, top + s * 0.42f, cx + s * 0.05f, top + s * 0.49f, _p);

        _p.Color = Colores.Acento;
        canvas.DrawCircle(cx + s * 0.18f, top + s * 0.04f, s * 0.065f, _p);

        _p.Color = Color.White;
        _p.TextAlign = Paint.Align.Center;
        _p.TextSize = s * 0.24f;
        _p.SetTypeface(Typeface.DefaultBold);
        canvas.DrawText("S/", cx, s * 0.90f, _p);
    }

    protected override void OnMeasure(int w, int h)
    {
        var d = Vistas.Px(Context!, 76);
        SetMeasuredDimension(d, d);
    }
}

