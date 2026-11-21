namespace PagoAlerta.Movil.Infra;

/// <summary>
/// HT-08: pasos para que cada fabricante no cierre la app en segundo plano. Funciona en cualquier Android:
/// si la marca no esta en la lista se muestra la guia generica.
/// </summary>
public static class GuiaBateria
{
    public record Guia(string Marca, string[] Pasos, (string Paquete, string Clase)[] Pantallas);

    public static Guia ParaEsteEquipo()
    {
        var f = (Build.Manufacturer ?? "").ToLowerInvariant();
        var marca = string.IsNullOrWhiteSpace(Build.Manufacturer) ? "tu celular"
            : char.ToUpperInvariant(Build.Manufacturer![0]) + Build.Manufacturer[1..];

        if (f.Contains("xiaomi") || f.Contains("redmi") || f.Contains("poco"))
            return new Guia("Xiaomi / Redmi / POCO", new[]
            {
                "Ajustes > Apps > Administrar apps > PagoAlerta > Inicio automático: ACTIVADO.",
                "En esa misma pantalla: Ahorro de batería > Sin restricciones.",
                "Abre la vista de apps recientes, mantén pulsada la tarjeta de PagoAlerta y toca el candado (Bloquear)."
            }, new[]
            {
                ("com.miui.securitycenter", "com.miui.permcenter.autostart.AutoStartManagementActivity")
            });

        if (f.Contains("samsung"))
            return new Guia("Samsung", new[]
            {
                "Ajustes > Batería > Límites de uso en segundo plano.",
                "Desactiva \"Poner en suspensión las apps no usadas\".",
                "En \"Apps que nunca se suspenden\" agrega PagoAlerta.",
                "Ajustes > Apps > PagoAlerta > Batería > Sin restricciones."
            }, new[]
            {
                ("com.samsung.android.lool", "com.samsung.android.sm.battery.ui.BatteryActivity"),
                ("com.samsung.android.lool", "com.samsung.android.sm.ui.battery.BatteryActivity")
            });

        if (f.Contains("huawei") || f.Contains("honor"))
            return new Guia("Huawei / Honor", new[]
            {
                "Ajustes > Batería > Inicio de aplicaciones > PagoAlerta.",
                "Desactiva \"Administrar automáticamente\" y activa: Inicio automático, Inicio secundario y Ejecutar en segundo plano."
            }, new[]
            {
                ("com.huawei.systemmanager", "com.huawei.systemmanager.startupmgr.ui.StartupNormalAppListActivity"),
                ("com.huawei.systemmanager", "com.huawei.systemmanager.optimize.process.ProtectActivity")
            });

        if (f.Contains("oppo") || f.Contains("realme") || f.Contains("oneplus"))
            return new Guia("Oppo / Realme / OnePlus", new[]
            {
                "Ajustes > Batería > Más ajustes (o Optimización de batería) > PagoAlerta > No optimizar.",
                "Ajustes > Apps > PagoAlerta > Uso de batería: permitir actividad en segundo plano y inicio automático."
            }, new[]
            {
                ("com.coloros.safecenter", "com.coloros.safecenter.permission.startup.StartupAppListActivity"),
                ("com.oplus.battery", "com.oplus.powermanager.fuelgaue.PowerUsageModelActivity")
            });

        if (f.Contains("vivo") || f.Contains("iqoo"))
            return new Guia("Vivo / iQOO", new[]
            {
                "Ajustes > Batería > Consumo de batería en segundo plano > PagoAlerta > Permitir.",
                "Permisos > Inicio automático: activa PagoAlerta."
            }, new[]
            {
                ("com.vivo.permissionmanager", "com.vivo.permissionmanager.activity.BgStartUpManagerActivity")
            });

        return new Guia(marca, new[]
        {
            "Ajustes > Apps > PagoAlerta > Batería > Sin restricciones (o \"No optimizar\").",
            "Si tu celular tiene \"Inicio automático\" o \"Apps protegidas\", activa PagoAlerta ahí.",
            "No cierres la app deslizándola desde las apps recientes si tu marca la bloquea al hacerlo."
        }, Array.Empty<(string, string)>());
    }
}
