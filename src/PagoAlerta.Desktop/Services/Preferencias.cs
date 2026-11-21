using System.Text.Json;
using PagoAlerta.Core;

namespace PagoAlerta.Desktop.Services;

/// <summary>Preferencias locales no sensibles (por ejemplo, el sonido de pago nuevo).</summary>
public class Preferencias
{
    public bool Sonido { get; set; }   // apagado por defecto

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
