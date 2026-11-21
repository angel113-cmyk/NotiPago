namespace PagoAlerta.Desktop.Ui;

/// <summary>
/// Suavizado simple para efectos hover / presionado / aparicion: el valor "persigue" al destino
/// en cada tick (aprox. 60 fps) y el temporizador se apaga solo al llegar.
/// </summary>
public sealed class Suave : IDisposable
{
    private readonly System.Windows.Forms.Timer _t = new() { Interval = 15 };
    private readonly float _factor;
    private float _valor, _destino;

    public float Valor => _valor;
    public event Action? Cambio;

    public Suave(float inicial = 0f, float factor = 0.28f)
    {
        _valor = _destino = inicial;
        _factor = factor;
        _t.Tick += (_, _) => Paso();
    }

    public void Hacia(float destino)
    {
        _destino = destino;
        if (!_t.Enabled && Math.Abs(_destino - _valor) > 0.0005f) _t.Start();
    }

    public void Fijar(float valor)
    {
        _valor = _destino = valor;
        _t.Stop();
        Cambio?.Invoke();
    }

    private void Paso()
    {
        var dif = _destino - _valor;
        if (Math.Abs(dif) < 0.004f) { _valor = _destino; _t.Stop(); }
        else _valor += dif * _factor;
        Cambio?.Invoke();
    }

    public void Dispose() => _t.Dispose();
}

public static class Mezcla
{
    /// <summary>Mezcla dos colores: t = 0 devuelve a, t = 1 devuelve b.</summary>
    public static Color De(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    public static float EaseOutCubic(float t) => 1f - (float)Math.Pow(1f - Math.Clamp(t, 0f, 1f), 3);
}
