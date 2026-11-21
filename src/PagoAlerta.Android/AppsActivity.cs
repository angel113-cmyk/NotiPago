using Android.Graphics;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil;

/// <summary>
/// HU-15: apps a monitorear (casillas para Yape, Plin, BBVA) + paquetes agregados a mano.
/// Incluye "Notificaciones recientes" para descubrir el nombre de paquete real de cada app.
/// </summary>
[Activity(Label = "Apps a monitorear", Exported = false, Theme = "@android:style/Theme.DeviceDefault.Light.NoActionBar")]
public class AppsActivity : PantallaBase
{
    private LinearLayout _listaExtra = null!, _listaRecientes = null!;
    private EditText _nuevoPaquete = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Construir("Apps a monitorear", "Solo se leen las notificaciones de las apps marcadas");
        var c = Contenido;

        // Apps conocidas
        var t1 = Vistas.Tarjeta(this);
        var apps = Servicios.Parser.PaquetesPorApp;
        foreach (var (app, paquetes) in apps)
        {
            var cb = new CheckBox(this) { Text = app, Checked = Servicios.Prefs.AppMarcada(app) };
            cb.SetTextSize(Android.Util.ComplexUnitType.Sp, 17);
            cb.SetTextColor(Colores.Texto);
            var nombre = app;
            cb.CheckedChange += (_, e) => Servicios.Prefs.MarcarApp(nombre, e.IsChecked, apps.Keys);
            t1.AddView(cb);
            t1.AddView(Vistas.Etiqueta(this, string.Join("\n", paquetes), 12, Colores.TextoSecundario),
                Vistas.Margen(this, 0, 10, 34));
        }
        c.AddView(t1, Vistas.Margen(this, 0, 14));

        // Otras apps
        var t2 = Vistas.Tarjeta(this);
        t2.AddView(Vistas.Etiqueta(this, "Otras apps (por nombre de paquete)", 17, Colores.Texto, negrita: true));
        _nuevoPaquete = Vistas.Campo(this, "ej. com.empresa.app");
        _nuevoPaquete.InputType = InputTypes.ClassText | InputTypes.TextVariationUri;
        t2.AddView(_nuevoPaquete, Vistas.Margen(this, 10, 8));
        t2.AddView(Vistas.Boton(this, "Agregar", true, () => Agregar(_nuevoPaquete.Text)));
        _listaExtra = new LinearLayout(this) { Orientation = Orientation.Vertical };
        t2.AddView(_listaExtra, Vistas.Margen(this, 8));
        c.AddView(t2, Vistas.Margen(this, 0, 14));

        // Recientes
        var t3 = Vistas.Tarjeta(this);
        t3.AddView(Vistas.Etiqueta(this, "Notificaciones recientes", 17, Colores.Texto, negrita: true));
        t3.AddView(Vistas.Etiqueta(this,
            "Aquí se ven los paquetes que mandaron notificaciones (sin su contenido). Toca uno para agregarlo.",
            13, Colores.TextoSecundario), Vistas.Margen(this, 4, 8));
        _listaRecientes = new LinearLayout(this) { Orientation = Orientation.Vertical };
        t3.AddView(_listaRecientes);
        t3.AddView(Vistas.Boton(this, "Actualizar", false, PintarRecientes), Vistas.Margen(this, 10));
        c.AddView(t3, Vistas.Margen(this, 0, 14));

        c.AddView(Vistas.Boton(this, "Volver", false, Finish), Vistas.Margen(this));
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
        if (p.Length < 3 || !p.Contains('.') || p.Contains(' ')) { Aviso("Escribe un nombre de paquete válido, por ejemplo com.empresa.app."); return; }
        var set = Servicios.Prefs.PaquetesExtra;
        set.Add(p);
        Servicios.Prefs.PaquetesExtra = set;
        _nuevoPaquete.Text = "";
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
            _listaExtra.AddView(Vistas.Etiqueta(this, "Ninguna agregada.", 13, Colores.TextoSecundario));
        foreach (var p in extra)
        {
            var fila = new LinearLayout(this) { Orientation = Orientation.Horizontal };
            fila.SetGravity(GravityFlags.CenterVertical);
            fila.AddView(Vistas.Etiqueta(this, p, 14, Colores.Texto), new LinearLayout.LayoutParams(0, -2, 1f));
            var quitar = Vistas.Etiqueta(this, "Quitar", 14, Colores.Error, negrita: true);
            quitar.SetPadding(Vistas.Px(this, 12), Vistas.Px(this, 10), 0, Vistas.Px(this, 10));
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
        var vistos = Servicios.Recientes()
            .GroupBy(r => r.Paquete)
            .Select(g => g.First())
            .Take(15).ToList();

        if (vistos.Count == 0)
        {
            _listaRecientes.AddView(Vistas.Etiqueta(this, "Aún no se ha visto ninguna notificación.\n" +
                "Activa el permiso y espera a que llegue una.", 13, Colores.TextoSecundario));
            return;
        }

        foreach (var r in vistos)
        {
            var fila = new LinearLayout(this) { Orientation = Orientation.Vertical };
            fila.SetPadding(0, Vistas.Px(this, 8), 0, Vistas.Px(this, 8));
            fila.AddView(Vistas.Etiqueta(this, r.Paquete, 14, Colores.Texto, negrita: true));
            var estado = monitoreados.Contains(r.Paquete)
                ? (r.EraPago ? "Monitoreada · pago detectado" : "Monitoreada")
                : "No monitoreada · toca para agregar";
            fila.AddView(Vistas.Etiqueta(this, $"{r.HoraLocal:HH:mm:ss}  ·  {estado}", 12,
                monitoreados.Contains(r.Paquete) ? Colores.Verde : Colores.TextoSecundario));
            var paquete = r.Paquete;
            if (!monitoreados.Contains(paquete)) fila.Click += (_, _) => Agregar(paquete);
            _listaRecientes.AddView(fila);
            _listaRecientes.AddView(Vistas.Separador(this));
        }
    }
}
