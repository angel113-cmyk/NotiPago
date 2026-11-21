using Android.Graphics;
using PagoAlerta.Core.Logging;
using PagoAlerta.Core.Services;
using PagoAlerta.Movil.Infra;
using PagoAlerta.Movil.Ui;

namespace PagoAlerta.Movil.Tabs;

/// <summary>Cuenta: perfil, apps a monitorear (interruptores), permisos, registro de errores y cerrar sesion.</summary>
public class CuentaTab : ITab
{
    private readonly PrincipalActivity _a;
    private readonly ScrollView _vista;

    public View Vista => _vista;

    public CuentaTab(PrincipalActivity a)
    {
        _a = a;
        _vista = new ScrollView(a) { FillViewport = true, VerticalScrollBarEnabled = false };
    }

    public void AlOcultar() { }

    public void AlMostrar()
    {
        // Se reconstruye cada vez para reflejar cambios (apps agregadas, correo, etc.).
        _vista.RemoveAllViews();
        _vista.AddView(Construir());
        Anim.EntrarLista((ViewGroup)_vista.GetChildAt(0)!, 55, 8);
    }

    private LinearLayout Construir()
    {
        var c = _a;
        var col = new LinearLayout(c) { Orientation = Orientation.Vertical };
        var pad = Vistas.Px(c, 20);
        col.SetPadding(pad, Vistas.Px(c, 14), pad, Vistas.Px(c, 20));

        var cab = new LinearLayout(c) { Orientation = Orientation.Vertical };
        cab.AddView(Vistas.Etiqueta(c, "Configuración", 16, Colores.TextoSecundario));
        cab.AddView(Vistas.Etiqueta(c, "Mi cuenta", 30, Colores.PrimarioOscuro, negrita: true), Vistas.Margen(c, 2));
        col.AddView(cab, Vistas.Margen(c, 0, 18));

        // ---- Perfil ----
        var correo = Servicios.Pago?.CorreoActual ?? "";
        var perfil = Vistas.Tarjeta(c, oscura: true, padDp: 20);
        var fila = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        fila.SetGravity(GravityFlags.CenterVertical);
        fila.AddView(Vistas.Circulo(c, Vistas.Iniciales(correo), Colores.AzulClaro, Colores.Primario, 64, 22),
            Vistas.Margen(c, 0, 0, 0, 16, Vistas.Px(c, 64), Vistas.Px(c, 64)));
        var textos = new LinearLayout(c) { Orientation = Orientation.Vertical };
        textos.AddView(Vistas.Etiqueta(c, NombreVisible(correo), 22, Color.White, negrita: true));
        var mail = Vistas.Etiqueta(c, string.IsNullOrEmpty(correo) ? "Sin sesión" : correo, 14, Color.ParseColor("#C7D5FF"));
        mail.SetSingleLine(true);
        mail.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        textos.AddView(mail, Vistas.Margen(c, 3));
        fila.AddView(textos, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        fila.AddView(Vistas.Pildora(c, "ADMIN", Colores.Acento, Colores.PrimarioOscuro, 12));
        perfil.AddView(fila);
        col.AddView(perfil, Vistas.Margen(c, 0, 16));

        // ---- Avisos de pago ----
        var avisos = Vistas.Tarjeta(c, padDp: 20);
        var filaAviso = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        filaAviso.SetGravity(GravityFlags.CenterVertical);
        var txAviso = new LinearLayout(c) { Orientation = Orientation.Vertical };
        txAviso.AddView(Vistas.Etiqueta(c, "Avisar cuando llegue un pago", 18, Colores.Texto, negrita: true));
        var subAviso = Vistas.Etiqueta(c, "Notificación con sonido y vibración al detectar un pago.", 13, Colores.TextoSecundario);
        subAviso.SetLineSpacing(0, 1.12f);
        txAviso.AddView(subAviso, Vistas.Margen(c, 3));
        filaAviso.AddView(txAviso, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        var swAviso = Vistas.Interruptor(c, Servicios.Prefs.AvisoPago, Colores.Primario);
        swAviso.ScaleX = swAviso.ScaleY = 1.15f;
        swAviso.CheckedChange += (_, e) =>
        {
            Servicios.Prefs.AvisoPago = e.IsChecked;
            Registro.Info($"Aviso de pago {(e.IsChecked ? "activado" : "desactivado")}");
        };
        filaAviso.AddView(swAviso);
        avisos.AddView(filaAviso);

        var probar = Vistas.Etiqueta(c, "Probar aviso", 15, Colores.Primario, negrita: true);
        probar.SetPadding(0, Vistas.Px(c, 14), Vistas.Px(c, 12), 0);
        probar.Click += (_, _) =>
        {
            if (!Servicios.Prefs.AvisoPago) { _a.Mensaje("Activa el aviso para probarlo", error: true); return; }
            AvisoPago.Notificar(_a, PagoFactory.CrearPruebaPorParser(Servicios.Parser) ?? PagoFactory.CrearPrueba("Yape"));
            _a.Mensaje("Aviso de prueba enviado");
        };
        avisos.AddView(probar);
        col.AddView(avisos, Vistas.Margen(c, 0, 16));

        // ---- Apps a monitorear ----
        var apps = Vistas.Tarjeta(c, padDp: 20);
        apps.AddView(Vistas.Etiqueta(c, "Apps a monitorear", 20, Colores.Texto, negrita: true));
        apps.AddView(Vistas.Etiqueta(c, "Solo se leen las notificaciones de estas apps.", 14, Colores.TextoSecundario), Vistas.Margen(c, 4, 10));

        var todas = Servicios.Parser.PaquetesPorApp;
        foreach (var (app, paquetes) in todas)
        {
            var f = new LinearLayout(c) { Orientation = Orientation.Horizontal };
            f.SetGravity(GravityFlags.CenterVertical);
            f.SetPadding(0, Vistas.Px(c, 9), 0, Vistas.Px(c, 9));
            f.AddView(Vistas.Circulo(c, Siglas(app), Colores.AzulClaro, Colores.Primario, 46, 15, cuadrado: true),
                Vistas.Margen(c, 0, 0, 0, 14, Vistas.Px(c, 46), Vistas.Px(c, 46)));
            var tx = new LinearLayout(c) { Orientation = Orientation.Vertical };
            tx.AddView(Vistas.Etiqueta(c, app, 17, Colores.Texto, negrita: true));
            var sub = Vistas.Etiqueta(c, paquetes.Count > 0 ? paquetes[0] : "Paquete por confirmar", 12, Colores.TextoSecundario);
            sub.SetSingleLine(true);
            sub.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
            tx.AddView(sub, Vistas.Margen(c, 2));
            f.AddView(tx, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));

            var sw = Vistas.Interruptor(c, Servicios.Prefs.AppMarcada(app), Colores.Primario);
            sw.ScaleX = sw.ScaleY = 1.15f;
            var nombre = app;
            sw.CheckedChange += (_, e) =>
            {
                Servicios.Prefs.MarcarApp(nombre, e.IsChecked, todas.Keys);
                Registro.Info($"App {nombre} {(e.IsChecked ? "marcada" : "desmarcada")}");
            };
            f.AddView(sw);
            apps.AddView(f);
        }

        var extras = Servicios.Prefs.PaquetesExtra.Count;
        var agregar = Vistas.Etiqueta(c, extras > 0 ? $"+ Agregar otra app por paquete  ({extras})" : "+ Agregar otra app por paquete",
            16, Colores.Primario, negrita: true);
        agregar.Gravity = GravityFlags.Center;
        agregar.SetPadding(0, Vistas.Px(c, 15), 0, Vistas.Px(c, 15));
        var punteado = Vistas.Forma(c, Colores.Tarjeta, 40, Colores.Primario, 1.5f);
        punteado.SetStroke(Vistas.Px(c, 1.5f), Colores.Primario, Vistas.Px(c, 6), Vistas.Px(c, 4));
        agregar.Background = new Android.Graphics.Drawables.RippleDrawable(
            Android.Content.Res.ColorStateList.ValueOf(Color.ParseColor("#221F4FE0")), punteado, null);
        agregar.Click += (_, _) => _a.StartActivity(new Intent(_a, typeof(AppsActivity)));
        Vistas.EfectoPresion(agregar);
        apps.AddView(agregar, Vistas.Margen(c, 10));
        col.AddView(apps, Vistas.Margen(c, 0, 16));

        // ---- Accesos ----
        var accesos = Vistas.Tarjeta(c, padDp: 6);
        accesos.AddView(Acceso(Resource.Drawable.ic_shield, "Permisos y batería",
            () => _a.StartActivity(new Intent(_a, typeof(PermisosActivity)))));
        accesos.AddView(Vistas.Separador(c), Vistas.Margen(c, 0, 0, 16, 16));
        accesos.AddView(Acceso(Resource.Drawable.ic_doc, "Registro de errores",
            () => _a.StartActivity(new Intent(_a, typeof(RegistroActivity)))));
        col.AddView(accesos, Vistas.Margen(c, 0, 18));

        col.AddView(Vistas.Boton(c, "Cerrar sesión", EstiloBoton.ContornoRojo, () => ConfirmarCerrarSesion()), Vistas.Margen(c));
        return col;
    }

    private View Acceso(int icono, string texto, Action alClic)
    {
        var c = _a;
        var f = new LinearLayout(c) { Orientation = Orientation.Horizontal };
        f.SetGravity(GravityFlags.CenterVertical);
        f.SetPadding(Vistas.Px(c, 16), Vistas.Px(c, 16), Vistas.Px(c, 14), Vistas.Px(c, 16));
        f.Background = Vistas.ConOnda(c, Colores.Tarjeta, 16, Color.ParseColor("#1A1F4FE0"));
        f.AddView(Vistas.Icono(c, icono, Colores.Primario, 26), Vistas.Margen(c, 0, 0, 0, 16, Vistas.Px(c, 26), Vistas.Px(c, 26)));
        f.AddView(Vistas.Etiqueta(c, texto, 17, Colores.Texto), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        f.AddView(Vistas.Icono(c, Resource.Drawable.ic_chevron_r, Colores.TextoSecundario, 22));
        f.Click += (_, _) => alClic();
        return f;
    }

    private void ConfirmarCerrarSesion()
    {
        new AlertDialog.Builder(_a)!
            .SetTitle("Cerrar sesión")!
            .SetMessage("¿Seguro que quieres cerrar sesión? Los pagos pendientes de enviar se conservan en el celular.")!
            .SetNegativeButton("Cancelar", (IDialogInterfaceOnClickListener?)null)!
            .SetPositiveButton("Cerrar sesión", (_, _) => _a.IrALogin(cerrarSesion: true))!
            .Show();
    }

    private static string NombreVisible(string correo)
    {
        var local = correo.Split('@')[0].Replace('.', ' ').Replace('_', ' ').Trim();
        if (local.Length == 0) return "Mi tienda";
        return string.Join(' ', local.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private static string Siglas(string app) => app.Length <= 2 ? app.ToUpperInvariant() :
        app.Equals("Yape", StringComparison.OrdinalIgnoreCase) ? "YP" :
        app.Equals("Plin", StringComparison.OrdinalIgnoreCase) ? "PL" :
        app.Equals("BBVA", StringComparison.OrdinalIgnoreCase) ? "BB" : app[..2].ToUpperInvariant();
}
