namespace PagoAlerta.Desktop.Ui;

/// <summary>
/// Muestra los recuadros de pago apilados en la esquina inferior derecha (el mas nuevo abajo), como Chrome.
/// Maximo 3 a la vez: si llegan mas, el mas antiguo se cierra.
/// </summary>
public static class GestorToasts
{
    private const int Maximo = 3;
    private static readonly List<ToastPago> Activos = new();

    /// <summary>Debe llamarse desde el hilo de la interfaz.</summary>
    public static void Mostrar(string titulo, string monto, string detalle, bool esPrueba, Color colorMonto, Action? alClic, int duracionMs = 8000)
    {
        var t = new ToastPago(titulo, monto, detalle, esPrueba, colorMonto, alClic, duracionMs);
        t.Cerrado += () => { Activos.Remove(t); Reordenar(); };
        Activos.Add(t);
        // El cierre es animado: se cuentan solo los que siguen a la vista y se desvanece el mas antiguo.
        foreach (var viejo in Activos.Where(a => !a.Cerrando).Take(Math.Max(0, Activos.Count(a => !a.Cerrando) - Maximo)).ToList())
            viejo.CerrarYa();
        Reordenar();
        t.Show();
    }

    public static void CerrarTodos()
    {
        foreach (var t in Activos.ToList()) { try { t.Close(); } catch { } }
        Activos.Clear();
    }

    private static void Reordenar()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        // Se colocan por la TARJETA (la ventana trae margen extra para la sombra): pegada a 14 px del borde derecho
        // y 16 px del borde inferior; el mas nuevo abajo y los demas apilados encima, con 10 px entre tarjetas.
        var tarjetaAbajo = area.Bottom - 16;
        foreach (var t in Enumerable.Reverse(Activos))
        {
            var k = t.Width / (float)ToastPago.Ancho;
            var x = (int)(area.Right - 14 - (ToastPago.MargenX + ToastPago.AnchoTarjeta) * k);
            var y = (int)(tarjetaAbajo - (ToastPago.MargenY + ToastPago.AltoTarjeta) * k);
            t.IrA(x, y);
            tarjetaAbajo -= (int)((ToastPago.AltoTarjeta + 10) * k);
        }
    }
}