using System.Drawing.Drawing2D;

namespace PagoAlerta.Desktop.Ui;

/// <summary>Dibuja el icono de la app (campana + insignia con visto) a cualquier tamaño, igual que assets\icono.svg.</summary>
public static class IconoApp
{
    private static readonly Color Azul = ColorTranslator.FromHtml("#1F4FE0");
    private static readonly Color AzulOscuro = ColorTranslator.FromHtml("#0B2A8F");
    private static readonly Color Cian = ColorTranslator.FromHtml("#19C6E0");

    public static void Dibujar(Graphics g, float x, float y, float lado)
    {
        var estado = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(x, y);
        g.ScaleTransform(lado / 1024f, lado / 1024f);

        // Fondo azul redondeado (rx = 224 en el SVG)
        using (var caja = Tema.Redondeado(new Rectangle(0, 0, 1024, 1024), 224))
        using (var azul = new SolidBrush(Azul))
            g.FillPath(azul, caja);

        using var blanco = new SolidBrush(Color.White);
        using var trazoBlanco = new Pen(Color.White, 24) { LineJoin = LineJoin.Round };
        g.FillEllipse(blanco, 470, 222, 60, 60);                       // punto superior

        using (var campana = new GraphicsPath())
        {
            campana.AddBezier(500, 282, 408, 282, 338, 352, 338, 448);
            campana.AddLine(338, 448, 338, 530);
            campana.AddBezier(338, 530, 338, 590, 318, 630, 278, 670);
            campana.AddLine(278, 670, 278, 700); campana.AddLine(278, 700, 722, 700); campana.AddLine(722, 700, 722, 670);
            campana.AddBezier(722, 670, 682, 630, 662, 590, 662, 530);
            campana.AddLine(662, 530, 662, 448);
            campana.AddBezier(662, 448, 662, 352, 592, 282, 500, 282);
            campana.CloseFigure();
            g.FillPath(blanco, campana);
            g.DrawPath(trazoBlanco, campana);
        }
        using (var badajo = new GraphicsPath())
        {
            badajo.AddArc(438, 678, 124, 124, 0, 180);
            badajo.CloseFigure();
            g.FillPath(blanco, badajo);
        }

        // Insignia cian con borde azul y visto
        using (var cian = new SolidBrush(Cian)) g.FillEllipse(cian, 616, 174, 244, 244);
        using (var borde = new Pen(Azul, 30)) g.DrawEllipse(borde, 616, 174, 244, 244);
        using (var visto = new Pen(AzulOscuro, 32) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            g.DrawLines(visto, new[] { new PointF(682, 298), new PointF(722, 338), new PointF(796, 258) });

        g.Restore(estado);
    }
}
