# Genera src\PagoAlerta.Desktop\pagoalerta.ico (logo propio: campana + S/). Se ejecuta una sola vez.
Add-Type -AssemblyName System.Drawing

function Dibujar([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    $azul = [System.Drawing.ColorTranslator]::FromHtml('#1F4FE0')
    $cian = [System.Drawing.ColorTranslator]::FromHtml('#19C6E0')
    $r = [int]($s * 0.26); $d = $r * 2
    $caja = New-Object System.Drawing.Drawing2D.GraphicsPath
    $caja.AddArc(0, 0, $d, $d, 180, 90); $caja.AddArc($s - $d - 1, 0, $d, $d, 270, 90)
    $caja.AddArc($s - $d - 1, $s - $d - 1, $d, $d, 0, 90); $caja.AddArc(0, $s - $d - 1, $d, $d, 90, 90)
    $caja.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $azul), $caja)

    $blanco = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $cx = $s / 2.0; $top = $s * 0.14
    $campana = New-Object System.Drawing.Drawing2D.GraphicsPath
    $campana.AddArc([single]($cx - $s * 0.19), [single]$top, [single]($s * 0.38), [single]($s * 0.38), 180, 180)
    $campana.AddLine([single]($cx + $s * 0.19), [single]($top + $s * 0.19), [single]($cx + $s * 0.23), [single]($top + $s * 0.40))
    $campana.AddLine([single]($cx + $s * 0.23), [single]($top + $s * 0.40), [single]($cx - $s * 0.23), [single]($top + $s * 0.40))
    $campana.CloseFigure()
    $g.FillPath($blanco, $campana)
    $g.FillEllipse($blanco, [single]($cx - $s * 0.05), [single]($top + $s * 0.42), [single]($s * 0.10), [single]($s * 0.07))
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $cian), [single]($cx + $s * 0.12), [single]($top - $s * 0.02), [single]($s * 0.13), [single]($s * 0.13))

    $fuente = New-Object System.Drawing.Font 'Segoe UI', ([single]($s * 0.19)), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
    $g.DrawString('S/', $fuente, $blanco, (New-Object System.Drawing.RectangleF 0, ([single]($s * 0.62)), $s, ([single]($s * 0.32))), $fmt)
    $g.Dispose()
    return $bmp
}

$tamanos = 16, 32, 48, 64, 128, 256
$pngs = foreach ($t in $tamanos) {
    $ms = New-Object System.IO.MemoryStream
    (Dibujar $t).Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    , $ms.ToArray()
}

$destino = Join-Path $PSScriptRoot '..\src\PagoAlerta.Desktop\pagoalerta.ico'
$fs = [System.IO.File]::Create($destino)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$tamanos.Count)
$offset = 6 + 16 * $tamanos.Count
for ($i = 0; $i -lt $tamanos.Count; $i++) {
    $t = $tamanos[$i]; $dim = if ($t -ge 256) { 0 } else { $t }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close(); $fs.Close()
"Icono creado: $((Resolve-Path $destino).Path)"
