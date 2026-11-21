using System.Media;

namespace PagoAlerta.Desktop.Services;

/// <summary>Sonido de pago nuevo: el de notificaciones de Windows; si no existe, el sonido de sistema.</summary>
public static class Sonido
{
    private static readonly string Archivo =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "Windows Notify System Generic.wav");
    private static SoundPlayer? _player;

    public static void Reproducir()
    {
        try
        {
            if (File.Exists(Archivo))
            {
                _player ??= new SoundPlayer(Archivo);
                _player.Play();            // se reproduce en un hilo aparte
            }
            else SystemSounds.Asterisk.Play();
        }
        catch { try { SystemSounds.Asterisk.Play(); } catch { } }
    }
}
