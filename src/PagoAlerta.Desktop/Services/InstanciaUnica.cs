namespace PagoAlerta.Desktop.Services;

/// <summary>
/// Una sola copia de la app por usuario. Si ya hay una (por ejemplo, oculta en la bandeja) y se abre otra,
/// la nueva le avisa a la primera que se muestre y se cierra.
/// </summary>
public static class InstanciaUnica
{
    private const string NombreMutex = @"Local\PagoAlerta.Instancia";
    private const string NombreEvento = @"Local\PagoAlerta.Mostrar";
    private static Mutex? _mutex;
    private static EventWaitHandle? _evento;

    /// <summary>True si esta es la primera copia. Si no, avisa a la que ya corre.</summary>
    public static bool EsPrimera()
    {
        _evento = new EventWaitHandle(false, EventResetMode.AutoReset, NombreEvento);
        _mutex = new Mutex(true, NombreMutex, out var nueva);
        if (!nueva) { try { _evento.Set(); } catch { } }
        return nueva;
    }

    /// <summary>Ejecuta <paramref name="alSolicitar"/> (en un hilo de fondo) cada vez que otra copia pida mostrar la ventana.</summary>
    public static void Escuchar(Action alSolicitar)
    {
        var evento = _evento;
        if (evento is null) return;
        new Thread(() =>
        {
            while (true)
            {
                try { evento.WaitOne(); alSolicitar(); }
                catch { return; }
            }
        })
        { IsBackground = true, Name = "InstanciaUnica" }.Start();
    }
}
