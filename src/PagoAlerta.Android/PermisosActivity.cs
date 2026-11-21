using Android.Graphics;
using Android.Net;
using Android.Provider;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil;

/// <summary>HU-11: guia paso a paso para activar "Acceso a notificaciones" y desactivar el ahorro de bateria.</summary>
[Activity(Label = "Permisos", Exported = false, Theme = "@android:style/Theme.DeviceDefault.Light.NoActionBar")]
public class PermisosActivity : PantallaBase
{
    private TextView _estadoNotif = null!, _estadoBateria = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Construir("Permisos", "Sigue los pasos para que no se pierda ningún pago");
        var c = Contenido;

        // Paso 1
        var p1 = Vistas.Tarjeta(this);
        p1.AddView(Vistas.Etiqueta(this, "Paso 1 · Acceso a notificaciones", 17, Colores.Texto, negrita: true));
        _estadoNotif = Vistas.Etiqueta(this, "", 14, Colores.TextoSecundario, negrita: true);
        p1.AddView(_estadoNotif, Vistas.Margen(this, 4, 8));
        p1.AddView(Vistas.Etiqueta(this,
            "1. Toca \"Abrir ajuste\".\n2. Busca PagoAlerta en la lista.\n3. Activa el interruptor y acepta el aviso.",
            14, Colores.Texto), Vistas.Margen(this, 0, 12));
        p1.AddView(Vistas.Boton(this, "Abrir ajuste", true, AbrirAjusteNotificaciones));
        p1.AddView(Vistas.Etiqueta(this,
            "¿El interruptor aparece gris o dice \"Ajuste restringido\"? En Android 13 o superior, abre " +
            "\"Info. de la app\", toca el menú ⋮ (arriba a la derecha) y elige \"Permitir ajustes restringidos\". " +
            "Luego vuelve al paso 1.", 13, Colores.TextoSecundario), Vistas.Margen(this, 12, 8));
        p1.AddView(Vistas.Boton(this, "Abrir info. de la app", false, AbrirInfoApp));
        c.AddView(p1, Vistas.Margen(this, 0, 14));

        // Paso 2
        var p2 = Vistas.Tarjeta(this);
        p2.AddView(Vistas.Etiqueta(this, "Paso 2 · Ahorro de batería", 17, Colores.Texto, negrita: true));
        _estadoBateria = Vistas.Etiqueta(this, "", 14, Colores.TextoSecundario, negrita: true);
        p2.AddView(_estadoBateria, Vistas.Margen(this, 4, 8));
        p2.AddView(Vistas.Etiqueta(this,
            "Para que Android no cierre la app en segundo plano, ponla como \"Sin restricciones\" " +
            "o \"No optimizar\".\n1. Toca \"Abrir ajuste\".\n2. Busca PagoAlerta y elige \"No optimizar\" / \"Sin restricciones\".",
            14, Colores.Texto), Vistas.Margen(this, 0, 12));
        p2.AddView(Vistas.Boton(this, "Abrir ajuste", true, AbrirAjusteBateria));
        c.AddView(p2, Vistas.Margen(this, 0, 14));

        c.AddView(Vistas.Boton(this, "Volver", false, Finish), Vistas.Margen(this));
    }

    protected override void OnResume()
    {
        base.OnResume();
        Marcar(_estadoNotif, Servicios.ListenerHabilitado(this), "Activado", "Desactivado");
        Marcar(_estadoBateria, Servicios.IgnoraBateria(this), "Sin restricciones", "Con ahorro de batería");
    }

    private static void Marcar(TextView t, bool ok, string si, string no)
    {
        t.Text = ok ? $"✓ {si}" : $"✗ {no}";
        t.SetTextColor(ok ? Colores.Verde : Colores.Ambar);
    }

    private void AbrirAjusteNotificaciones() => Abrir(() => new Intent(Settings.ActionNotificationListenerSettings),
        "No se pudo abrir el ajuste. Ve a Ajustes > Notificaciones > Acceso a notificaciones.");

    private void AbrirAjusteBateria() => Abrir(() => new Intent(Settings.ActionIgnoreBatteryOptimizationSettings),
        "No se pudo abrir el ajuste. Ve a Ajustes > Batería.");

    private void AbrirInfoApp() => Abrir(
        () => new Intent(Settings.ActionApplicationDetailsSettings, Uri.FromParts("package", PackageName, null)),
        "No se pudo abrir la información de la app.");

    private void Abrir(Func<Intent> crear, string errorMensaje)
    {
        try { StartActivity(crear()); }
        catch (ActivityNotFoundException) { Aviso(errorMensaje); }
    }
}
