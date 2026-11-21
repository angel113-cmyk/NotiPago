using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;

namespace PagoAlerta.Movil.Ui;

/// <summary>Paleta azul (la misma del escritorio y del prototipo).</summary>
public static class Colores
{
    public static readonly Color Primario = Color.ParseColor("#1F4FE0");
    public static readonly Color PrimarioOscuro = Color.ParseColor("#0B2A8F");
    public static readonly Color AzulClaro = Color.ParseColor("#EAF0FF");
    public static readonly Color Acento = Color.ParseColor("#19C6E0");
    public static readonly Color AcentoClaro = Color.ParseColor("#D5F5FA");
    public static readonly Color Fondo = Color.ParseColor("#F5F7FB");
    public static readonly Color Tarjeta = Color.White;
    public static readonly Color Texto = Color.ParseColor("#1B1F2A");
    public static readonly Color TextoSecundario = Color.ParseColor("#6B7280");
    public static readonly Color Verde = Color.ParseColor("#16A34A");
    public static readonly Color VerdeClaro = Color.ParseColor("#DCF5E6");
    public static readonly Color Ambar = Color.ParseColor("#B45309");
    public static readonly Color AmbarClaro = Color.ParseColor("#FFF1D6");
    public static readonly Color AmbarPunto = Color.ParseColor("#F59E0B");
    public static readonly Color Error = Color.ParseColor("#DC2626");
    public static readonly Color Borde = Color.ParseColor("#D9DFEC");
    public static readonly Color BordeSuave = Color.ParseColor("#E8ECF6");
}

public enum EstiloBoton { Primario, Contorno, ContornoRojo, Suave, Blanco }

public static class Vistas
{
    public const float RadioTarjeta = 22, RadioCampo = 16, RadioPildora = 60;

    public static int Px(Context c, float dp) =>
        (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, c.Resources!.DisplayMetrics);

    public static GradientDrawable Forma(Context c, Color fondo, float radioDp, Color? borde = null, float bordeDp = 1f)
    {
        var g = new GradientDrawable();
        g.SetColor(fondo);
        g.SetCornerRadius(Px(c, radioDp));
        if (borde is { } b) g.SetStroke(Px(c, bordeDp), b);
        return g;
    }

    /// <summary>Fondo con efecto "onda" (ripple) al tocar, recortado a la forma.</summary>
    public static Drawable ConOnda(Context c, Color fondo, float radioDp, Color onda, Color? borde = null)
    {
        var contenido = Forma(c, fondo, radioDp, borde);
        var mascara = Forma(c, Color.White, radioDp);
        return new RippleDrawable(ColorStateList.ValueOf(onda), contenido, mascara);
    }

    public static TextView Etiqueta(Context c, string texto, float sp, Color color, bool negrita = false)
    {
        var t = new TextView(c) { Text = texto };
        t.SetTextSize(ComplexUnitType.Sp, sp);
        t.SetTextColor(color);
        if (negrita) t.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Bold), TypefaceStyle.Bold);
        t.SetIncludeFontPadding(false);
        return t;
    }

    public static LinearLayout.LayoutParams Margen(Context c, float arriba = 0, float abajo = 0, float izq = 0, float der = 0,
        int ancho = ViewGroup.LayoutParams.MatchParent, int alto = ViewGroup.LayoutParams.WrapContent)
    {
        var p = new LinearLayout.LayoutParams(ancho, alto);
        p.SetMargins(Px(c, izq), Px(c, arriba), Px(c, der), Px(c, abajo));
        return p;
    }

    /// <summary>Tarjeta blanca con esquinas muy redondeadas y sombra suave. Oscura = azul marino.</summary>
    public static LinearLayout Tarjeta(Context c, bool oscura = false, float padDp = 18)
    {
        var l = new LinearLayout(c) { Orientation = Orientation.Vertical };
        l.Background = oscura
            ? Forma(c, Colores.PrimarioOscuro, RadioTarjeta)
            : Forma(c, Colores.Tarjeta, RadioTarjeta, Colores.BordeSuave);
        var p = Px(c, padDp);
        l.SetPadding(p, p, p, p);
        l.Elevation = Px(c, oscura ? 6 : 2);
        return l;
    }

    // ---------------- Iconos ----------------
    public static ImageView Icono(Context c, int recurso, Color color, float dp = 24)
    {
        var i = new ImageView(c);
        i.SetImageResource(recurso);
        i.SetColorFilter(color);
        i.LayoutParameters = new ViewGroup.LayoutParams(Px(c, dp), Px(c, dp));
        return i;
    }

    // ---------------- Botones ----------------
    public static Button Boton(Context c, string texto, EstiloBoton estilo, Action alClic, bool compacto = false)
    {
        Color fondo, textoC, onda;
        Color? borde = null;
        switch (estilo)
        {
            case EstiloBoton.Contorno: fondo = Colores.Tarjeta; textoC = Colores.Primario; onda = Color.ParseColor("#331F4FE0"); borde = Colores.Primario; break;
            case EstiloBoton.ContornoRojo: fondo = Colores.Tarjeta; textoC = Color.ParseColor("#B91C1C"); onda = Color.ParseColor("#33DC2626"); borde = Colores.Error; break;
            case EstiloBoton.Suave: fondo = Colores.AzulClaro; textoC = Colores.Primario; onda = Color.ParseColor("#331F4FE0"); break;
            case EstiloBoton.Blanco: fondo = Color.White; textoC = Colores.Primario; onda = Color.ParseColor("#331F4FE0"); break;
            default: fondo = Colores.Primario; textoC = Color.White; onda = Color.ParseColor("#44FFFFFF"); break;
        }

        var b = new Button(c) { Text = texto };
        b.SetAllCaps(false);
        b.SetTextSize(ComplexUnitType.Sp, compacto ? 14 : 16);
        b.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Bold), TypefaceStyle.Bold);
        b.SetTextColor(textoC);
        b.StateListAnimator = null;
        b.SetMinHeight(Px(c, compacto ? 42 : 54));
        b.SetMinimumHeight(Px(c, compacto ? 42 : 54));
        b.SetPadding(Px(c, compacto ? 18 : 24), 0, Px(c, compacto ? 18 : 24), 0);
        b.Background = ConOnda(c, fondo, RadioPildora, onda, borde);
        if (estilo == EstiloBoton.Primario) b.Elevation = Px(c, 3);
        b.Click += (_, _) => alClic();
        EfectoPresion(b);
        return b;
    }

    /// <summary>Al presionar, la vista se encoge un poco y rebota al soltar.</summary>
    public static void EfectoPresion(View v, float escala = 0.97f)
    {
        v.Touch += (_, e) =>
        {
            switch (e.Event?.Action)
            {
                case MotionEventActions.Down:
                    v.Animate()!.ScaleX(escala).ScaleY(escala).SetDuration(90).Start();
                    break;
                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    v.Animate()!.ScaleX(1f).ScaleY(1f).SetDuration(160)
                        .SetInterpolator(new Android.Views.Animations.OvershootInterpolator(2.2f)).Start();
                    break;
            }
            e.Handled = false;
        };
    }

    // ---------------- Campos y controles ----------------
    public static TextView EtiquetaCampo(Context c, string texto) =>
        Etiqueta(c, texto, 13, Colores.TextoSecundario, negrita: true);

    public static EditText Campo(Context c, string pista, bool clave = false)
    {
        var e = new EditText(c) { Hint = pista };
        e.SetTextSize(ComplexUnitType.Sp, 16);
        e.SetTextColor(Colores.Texto);
        e.SetHintTextColor(Color.ParseColor("#9CA3AF"));
        e.SetPadding(Px(c, 18), Px(c, 15), Px(c, 18), Px(c, 15));
        e.SetSingleLine(true);
        e.InputType = clave
            ? InputTypes.ClassText | InputTypes.TextVariationPassword
            : InputTypes.ClassText | InputTypes.TextVariationEmailAddress;
        e.Background = Forma(c, Colores.Tarjeta, RadioCampo, Colores.Borde);
        // El borde se vuelve azul y mas grueso al enfocar.
        e.FocusChange += (_, ev) =>
            e.Background = ev.HasFocus
                ? Forma(c, Colores.Tarjeta, RadioCampo, Colores.Primario, 2f)
                : Forma(c, Colores.Tarjeta, RadioCampo, Colores.Borde);
        return e;
    }

    public static Switch Interruptor(Context c, bool marcado, Color pistaOn)
    {
        var s = new Switch(c) { Checked = marcado };
        var estados = new[] { new[] { Android.Resource.Attribute.StateChecked }, new int[0] };
        s.ThumbTintList = new ColorStateList(estados, new[] { (int)Color.White, (int)Color.White });
        s.TrackTintList = new ColorStateList(estados, new[] { (int)pistaOn, (int)Color.ParseColor("#C7CEDD") });
        return s;
    }

    public static TextView Pildora(Context c, string texto, Color fondo, Color textoColor, float sp = 11)
    {
        var t = Etiqueta(c, texto, sp, textoColor, negrita: true);
        t.SetPadding(Px(c, 10), Px(c, 4), Px(c, 10), Px(c, 4));
        t.Background = Forma(c, fondo, RadioPildora);
        t.Gravity = GravityFlags.Center;
        return t;
    }

    public static TextView Circulo(Context c, string texto, Color fondo, Color textoColor, float dp = 48, float sp = 16, bool cuadrado = false)
    {
        var t = Etiqueta(c, texto, sp, textoColor, negrita: true);
        t.Gravity = GravityFlags.Center;
        t.Background = Forma(c, fondo, cuadrado ? 14 : dp);
        t.LayoutParameters = new LinearLayout.LayoutParams(Px(c, dp), Px(c, dp));
        return t;
    }

    public static View Separador(Context c)
    {
        var v = new View(c);
        v.SetBackgroundColor(Colores.BordeSuave);
        v.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Px(c, 1));
        return v;
    }

    public static string Iniciales(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "?";
        var partes = nombre.Split(new[] { ' ', '.', '@', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var letras = partes.Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpperInvariant(p[0]));
        var r = string.Concat(letras);
        return r.Length == 0 ? "?" : r;
    }
}

/// <summary>Animaciones reutilizables: entrada escalonada, conteo de numeros, temblor de error.</summary>
public static class Anim
{
    public static void Entrar(View v, int retardoMs = 0, float desplazaDp = 18)
    {
        v.Alpha = 0f;
        v.TranslationY = Vistas.Px(v.Context!, desplazaDp);
        v.Animate()!.Alpha(1f).TranslationY(0f).SetStartDelay(retardoMs).SetDuration(320)
            .SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.6f)).Start();
    }

    public static void EntrarLista(ViewGroup lista, int pasoMs = 45, int maximo = 10)
    {
        for (var i = 0; i < lista.ChildCount; i++) Entrar(lista.GetChildAt(i)!, Math.Min(i, maximo) * pasoMs, 14);
    }

    /// <summary>El numero "cuenta" desde su valor anterior hasta el nuevo.</summary>
    public static void ContarNumero(TextView t, decimal desde, decimal hasta, Func<decimal, string> formato, int ms = 700)
    {
        if (desde == hasta) { t.Text = formato(hasta); return; }
        var a = ValueAnimator.OfFloat(0f, 1f)!;
        a.SetDuration(ms);
        a.SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.8f));
        a.Update += (_, e) =>
        {
            var f = (decimal)e.Animation!.AnimatedFraction;
            t.Text = formato(f >= 0.999m ? hasta : desde + (hasta - desde) * f);
        };
        a.Start();
    }

    public static void Temblar(View v)
    {
        var d = Vistas.Px(v.Context!, 10);
        var a = ObjectAnimator.OfFloat(v, "translationX", 0f, -d, d, -d * 0.7f, d * 0.7f, -d * 0.35f, d * 0.35f, 0f)!;
        a.SetDuration(420);
        a.Start();
    }

    /// <summary>Aparicion con rebote (logo, iconos de estado).</summary>
    public static void Rebotar(View v, int retardoMs = 0)
    {
        v.ScaleX = v.ScaleY = 0.6f; v.Alpha = 0f;
        v.Animate()!.ScaleX(1f).ScaleY(1f).Alpha(1f).SetStartDelay(retardoMs).SetDuration(420)
            .SetInterpolator(new Android.Views.Animations.OvershootInterpolator(2f)).Start();
    }
}
