using System.Text.Json;
using PagoAlerta.Core;

namespace PagoAlerta.Desktop.Services;

/// <summary>Preferencias locales no sensibles (sonido y aviso de pago nuevo).</summary>
public class Preferencias
{
    /// <summary>Sonido al llegar un pago (tambien con la ventana oculta). Encendido por defecto.</summary>
    public bool Sonido { get; set; } = true;

    /// <summary>Aviso de Windows ("Pago recibido · S/ 35.00 · ...") cuando la ventana no esta a la vista.</summary>
    public bool AvisoPago { get; set; } = true;

    private static string Ruta
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Nombre);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "preferencias.json");
        }
    }

    public static Preferencias Cargar()
    {
        try { return JsonSerializer.Deserialize<Preferencias>(File.ReadAllText(Ruta)) ?? new(); }
        catch { return new(); }
    }

    public void Guardar()
    {
        try { File.WriteAllText(Ruta, JsonSerializer.Serialize(this)); } catch { }
    }
}
