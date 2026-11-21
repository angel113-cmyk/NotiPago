using PagoAlerta.Movil.Infra;

namespace PagoAlerta.Movil;

[Application(Label = "@string/app_name")]
public class PagoAlertaApp : Application
{
    public PagoAlertaApp(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }

    public override void OnCreate()
    {
        base.OnCreate();
        Servicios.Iniciar(this);
    }
}
