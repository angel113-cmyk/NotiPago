using Microsoft.Win32;
using PagoAlerta.Core;

namespace PagoAlerta.Desktop.Services;

/// <summary>Iniciar con Windows: entrada en HKCU\...\Run (solo del usuario actual, sin permisos de administrador).</summary>
public static class InicioConWindows
{
    private const string Clave = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool Activo
    {
        get
        {
            try { using var k = Registry.CurrentUser.OpenSubKey(Clave); return k?.GetValue(AppInfo.Nombre) is string; }
            catch { return false; }
        }
    }

    public static bool Establecer(bool activar)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(Clave);
            if (activar) k.SetValue(AppInfo.Nombre, $"\"{Application.ExecutablePath}\" {Program.ArgumentoSegundoPlano}");
            else k.DeleteValue(AppInfo.Nombre, throwOnMissingValue: false);
            return true;
        }
        catch { return false; }
    }
}
