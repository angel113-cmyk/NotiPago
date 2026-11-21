using System.Text;

namespace PagoAlerta.Core.Logging;

/// <summary>
/// HT-17: registro de errores en archivo (rotado). Nunca guarda nombres, montos ni texto de notificaciones:
/// solo eventos, tipos de error y mensajes recortados.
/// </summary>
public static class Registro
{
    private const long TamanoMaximo = 1_000_000;   // 1 MB por archivo
    private const int ArchivosAConservar = 3;
    private static readonly object Candado = new();
    private static string? _ruta;

    public static string? RutaActual => _ruta;

    /// <summary>Define la carpeta de logs. Si falla, el registro se desactiva sin afectar a la app.</summary>
    public static void Iniciar(string carpeta, string nombre = "pagoalerta.log")
    {
        try
        {
            Directory.CreateDirectory(carpeta);
            _ruta = Path.Combine(carpeta, nombre);
            Info($"Inicio de {AppInfo.Nombre}");
        }
        catch { _ruta = null; }
    }

    public static void Info(string mensaje) => Escribir("INFO", mensaje);
    public static void Aviso(string mensaje) => Escribir("AVISO", mensaje);

    public static void Error(string contexto, Exception? ex = null) =>
        Escribir("ERROR", ex is null ? contexto : $"{contexto} | {ex.GetType().Name}: {Recortar(ex.Message)}");

    /// <summary>Quita saltos de linea y limita el largo (los mensajes de error pueden traer datos del cuerpo).</summary>
    public static string Recortar(string? s, int max = 160)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var limpio = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return limpio.Length <= max ? limpio : limpio[..max] + "…";
    }

    private static void Escribir(string nivel, string mensaje)
    {
        var ruta = _ruta;
        if (ruta is null) return;
        try
        {
            lock (Candado)
            {
                Rotar(ruta);
                File.AppendAllText(ruta, $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z [{nivel}] {mensaje}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch { /* el registro jamas debe tumbar la app */ }
    }

    private static void Rotar(string ruta)
    {
        var info = new FileInfo(ruta);
        if (!info.Exists || info.Length < TamanoMaximo) return;
        for (var i = ArchivosAConservar - 1; i >= 1; i--)
        {
            var origen = $"{ruta}.{i}";
            if (File.Exists(origen)) File.Move(origen, $"{ruta}.{i + 1}", overwrite: true);
        }
        File.Move(ruta, $"{ruta}.1", overwrite: true);
    }
}
