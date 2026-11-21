using Android.Graphics;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil;

/// <summary>
/// HU-15: agregar otras apps por nombre de paquete. Incluye "Notificaciones recientes" para descubrir
/// el nombre de paquete real de cada app (toca uno para agregarlo).
/// </summary>
[Activity(Label = "Agregar otra app", Exported = false, Theme = "@android:style/Theme.DeviceDefault.Light.NoActionBar")]
public class AppsActivity : PantallaBase
{
    private LinearLayout _listaExtra = null!, _listaRecientes = null!;
    private EditText _nuevoPaquete = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var raiz = PantallaConAtras("Agregar otra app", out var c, out _);
        SetContentView(raiz);

        var intro = Vistas.Etiqueta(this, "Solo se leen las notificaciones de las apps elegidas.", 16, Colores.TextoSecundario);
        c.AddView(intro, Vistas.Margen(this, 0, 16));

        // ---- Agregar por paquete ----
        var t1 = Vistas.Tarjeta(this, padDp: 20);
        t1.AddView(Vistas.Etiqueta(this, "Otras apps (por nombre de paquete)", 18, Colores.Texto, negrita: true));
        _nuevoPaquete = Vistas.Campo(this, "ej. com.empresa.app");
        _nuevoPaquete.InputType = InputTypes.ClassText | InputTypes.TextVariationUri;
        t1.AddView(_nuevoPaquete, Vistas.Margen(this, 12, 10));
        t1.AddView(Vistas.Boton(this, "Agregar", EstiloBoton.Primario, () => Agregar(_nuevoPaquete.Text)));
        _listaExtra = new LinearLayout(this) { Orientation = Orientation.Vertical };
        t1.AddView(_listaExtra, Vistas.Margen(this, 12));
        c.AddView(t1, Vistas.Margen(this, 0, 16));

        // ---- Recientes ----
        var t2 = Vistas.Tarjeta(this, padDp: 20);
        t2.AddView(Vistas.Etiqueta(this, "Notificaciones recientes", 18, Colores.Texto, negrita: true));
        var ayuda = Vistas.Etiqueta(this,
            "Aquí se ven los paquetes que mandaron notificaciones (sin su contenido). Toca uno para agregarlo.", 14, Colores.TextoSecundario);
        ayuda.SetLineSpacing(0, 1.15f);
        t2.AddView(ayuda, Vistas.Margen(this, 4, 10));
        _listaRecientes = new LinearLayout(this) { Orientation = Orientation.Vertical };
        t2.AddView(_listaRecientes);
        t2.AddView(Vistas.Boton(this, "Actualizar", EstiloBoton.Contorno, PintarRecientes, compacto: true), Vistas.Margen(this, 12));
        c.AddView(t2, Vistas.Margen(this));

        for (var i = 0; i < c.ChildCount; i++) Anim.Entrar(c.GetChildAt(i)!, i * 70, 16);
    }

    protected override void OnResume()
    {
        base.OnResume();
        PintarExtra();
        PintarRecientes();
    }

    private void Agregar(string? texto)
    {
        var p = texto?.Trim() ?? "";
        if (p.Length < 3 || !p.Contains('.') || p.Contains(' ')) { Mensaje("Escribe un nombre de paquete válido, por ejemplo com.empresa.app.", error: true, largo: true); return; }
        var set = Servicios.Prefs.PaquetesExtra;
        set.Add(p);
        Servicios.Prefs.PaquetesExtra = set;
        _nuevoPaquete.Text = "";
        Mensaje($"Agregada: {p}");
        PintarExtra();
        PintarRecientes();
    }

    private void Quitar(string paquete)
    {
        var set = Servicios.Prefs.PaquetesExtra;
        set.Remove(paquete);
        Servicios.Prefs.PaquetesExtra = set;
        PintarExtra();
        PintarRecientes();
    }

    private void PintarExtra()
    {
        _listaExtra.RemoveAllViews();
        var extra = Servicios.Prefs.PaquetesExtra.OrderBy(x => x).ToList();
        if (extra.Count == 0)
            _listaExtra.AddView(Vistas.Etiqueta(this, "Ninguna agregada todavía.", 14, Colores.TextoSecundario));
        foreach (var p in extra)
        {
            var fila = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            fila.SetGravity(GravityFlags.CenterVertical);
            var nombre = Vistas.Etiqueta(this, p, 14, Colores.Texto);
            nombre.SetSingleLine(true);
            nombre.Ellipsize = Android.Text.TextUtils.TruncateAt.Middle;
            fila.AddView(nombre, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
            var quitar = Vistas.Etiqueta(this, "Quitar", 14, Colores.Error, negrita: true);
            quitar.SetPadding(Vistas.Px(this, 14), Vistas.Px(this, 12), 0, Vistas.Px(this, 12));
            var cp = p;
            quitar.Click += (_, _) => Quitar(cp);
            fila.AddView(quitar);
            _listaExtra.AddView(fila);
        }
    }

    private void PintarRecientes()
    {
        _listaRecientes.RemoveAllViews();
        var monitoreados = Servicios.Prefs.PaquetesMonitoreados(Servicios.Parser);
        var vistos = Servicios.Recientes().GroupBy(r => r.Paquete).Select(g => g.First()).Take(15).ToList();

        if (vistos.Count == 0)
        {
            var vacio = Vistas.Etiqueta(this, "Aún no se ha visto ninguna notificación.\nActiva el permiso y espera a que llegue una.", 14, Colores.TextoSecundario);
            vacio.SetLineSpacing(0, 1.15f);
            _listaRecientes.AddView(vacio);
            return;
        }

        foreach (var r in vistos)
        {
            var esta = monitoreados.Contains(r.Paquete);
            var fila = new LinearLayout(this) { Orientation = Orientation.Vertical };
            fila.SetPadding(0, Vistas.Px(this, 11), 0, Vistas.Px(this, 11));
            fila.AddView(Vistas.Etiqueta(this, r.Paquete, 15, Colores.Texto, negrita: true));
            var estado = esta ? (r.EraPago ? "Monitoreada · pago detectado" : "Monitoreada") : "No monitoreada · toca para agregar";
            fila.AddView(Vistas.Etiqueta(this, $"{r.HoraLocal:HH:mm:ss}  ·  {estado}", 12, esta ? Colores.Verde : Colores.TextoSecundario),
                Vistas.Margen(this, 2));
            var paquete = r.Paquete;
            if (!esta)
            {
                fila.Background = Vistas.ConOnda(this, Color.Transparent, 8, Color.ParseColor("#221F4FE0"));
                fila.Click += (_, _) => Agregar(paquete);
            }
            _listaRecientes.AddView(fila);
            _listaRecientes.AddView(Vistas.Separador(this));
        }
    }
}
