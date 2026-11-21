# Genera TODOS los iconos de PagoAlerta a partir del diseno de assets\icono.svg
# (campana + insignia con visto). Se dibuja con las mismas formas del SVG (1024 x 1024).
#   - Escritorio / instalador:  src\PagoAlerta.Desktop\pagoalerta.ico
#   - Android (iconos clasicos y adaptables):  src\PagoAlerta.Android\Resources\mipmap-*\appicon*.png
# Uso:  powershell -ExecutionPolicy Bypass -File installer\crear-iconos.ps1
Add-Type -AssemblyName System.Drawing
$raiz = Split-Path $PSScriptRoot -Parent
$azul = [System.Drawing.ColorTranslator]::FromHtml('#1F4FE0')
$azulOscuro = [System.Drawing.ColorTranslator]::FromHtml('#0B2A8F')
$cian = [System.Drawing.ColorTranslator]::FromHtml('#19C6E0')

function Nueva-Imagen([int]$s) {
    $b = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    return @($b, $g)
}

function Cuadro-Redondeado([single]$s, [single]$r) {
    $d = $r * 2
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc(0, 0, $d, $d, 180, 90); $p.AddArc($s - $d, 0, $d, $d, 270, 90)
    $p.AddArc($s - $d, $s - $d, $d, $d, 0, 90); $p.AddArc(0, $s - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

# Dibuja la campana y la insignia en el espacio del SVG (1024 x 1024).
function Dibujar-Glifo($g) {
    $blanco = [System.Drawing.Brushes]::White
    $penBlanco = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 24
    $penBlanco.LineJoin = 'Round'

    # punto superior
    $g.FillEllipse($blanco, 470, 222, 60, 60)

    # cuerpo de la campana
    $campana = New-Object System.Drawing.Drawing2D.GraphicsPath
    $campana.AddBezier(500,282, 408,282, 338,352, 338,448)
    $campana.AddLine(338,448, 338,530)
    $campana.AddBezier(338,530, 338,590, 318,630, 278,670)
    $campana.AddLine(278,670, 278,700); $campana.AddLine(278,700, 722,700); $campana.AddLine(722,700, 722,670)
    $campana.AddBezier(722,670, 682,630, 662,590, 662,530)
    $campana.AddLine(662,530, 662,448)
    $campana.AddBezier(662,448, 662,352, 592,282, 500,282)
    $campana.CloseFigure()
    $g.FillPath($blanco, $campana); $g.DrawPath($penBlanco, $campana)

    # badajo (semicirculo inferior)
    $badajo = New-Object System.Drawing.Drawing2D.GraphicsPath
    $badajo.AddArc(438, 678, 124, 124, 0, 180); $badajo.CloseFigure()
    $g.FillPath($blanco, $badajo)

    # insignia cian con borde azul y visto
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $cian), 616, 174, 244, 244)
    $penBorde = New-Object System.Drawing.Pen $azul, 30
    $g.DrawEllipse($penBorde, 616, 174, 244, 244)
    $penVisto = New-Object System.Drawing.Pen $azulOscuro, 32
    $penVisto.StartCap = 'Round'; $penVisto.EndCap = 'Round'; $penVisto.LineJoin = 'Round'
    $g.DrawLines($penVisto, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF 682,298), (New-Object System.Drawing.PointF 722,338), (New-Object System.Drawing.PointF 796,258)))
}

# --- Maestro 1: icono completo (fondo azul redondeado + glifo), 1024 px ---
$img = Nueva-Imagen 1024; $maestro = $img[0]; $g = $img[1]
$g.FillPath((New-Object System.Drawing.SolidBrush $azul), (Cuadro-Redondeado 1024 224))
Dibujar-Glifo $g; $g.Dispose()

# --- Maestro 2: solo el glifo, centrado y dentro de la zona segura del icono adaptable ---
$img = Nueva-Imagen 1024; $primerPlano = $img[0]; $g = $img[1]
$g.TranslateTransform(512, 512); $g.ScaleTransform(0.70, 0.70); $g.TranslateTransform(-570, -480.5)
Dibujar-Glifo $g; $g.Dispose()

function Escalar($origen, [int]$s) {
    $b = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.SmoothingMode = 'HighQuality'; $g.PixelOffsetMode = 'HighQuality'
    $g.CompositingQuality = 'HighQuality'
    $g.DrawImage($origen, 0, 0, $s, $s); $g.Dispose(); return $b
}
function Guardar-Png($bmp, [string]$ruta) { New-Item -ItemType Directory -Force (Split-Path $ruta) | Out-Null; $bmp.Save($ruta, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose() }

# --- Android ---
$res = Join-Path $raiz 'src\PagoAlerta.Android\Resources'
$densidades = [ordered]@{ 'mdpi' = 48; 'hdpi' = 72; 'xhdpi' = 96; 'xxhdpi' = 144; 'xxxhdpi' = 192 }
foreach ($d in $densidades.Keys) {
    $lado = $densidades[$d]; $adaptable = [int]($lado * 108 / 48)
    Guardar-Png (Escalar $maestro $lado) (Join-Path $res "mipmap-$d\appicon.png")                    # icono clasico (Android 7.x)
    Guardar-Png (Escalar $primerPlano $adaptable) (Join-Path $res "mipmap-$d\appicon_foreground.png")  # capa frontal adaptable
    $fondo = New-Object System.Drawing.Bitmap $adaptable, $adaptable
    $gf = [System.Drawing.Graphics]::FromImage($fondo); $gf.Clear($azul); $gf.Dispose()
    Guardar-Png $fondo (Join-Path $res "mipmap-$d\appicon_background.png")                           # capa de fondo adaptable
}
@"
<?xml version="1.0" encoding="utf-8"?>
<resources>
    <color name="ic_launcher_background">#1F4FE0</color>
</resources>
"@ | Set-Content (Join-Path $res 'values\ic_launcher_background.xml') -Encoding utf8

# --- Escritorio: .ico con PNG de 16 a 256 px ---
$tamanos = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($t in $tamanos) {
    $ms = New-Object System.IO.MemoryStream
    $b = Escalar $maestro $t; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
    , $ms.ToArray()
}
$destino = Join-Path $raiz 'src\PagoAlerta.Desktop\pagoalerta.ico'
$fs = [System.IO.File]::Create($destino); $bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$tamanos.Count)
$offset = 6 + 16 * $tamanos.Count
for ($i = 0; $i -lt $tamanos.Count; $i++) {
    $t = $tamanos[$i]; $dim = if ($t -ge 256) { 0 } else { $t }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset); $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close(); $fs.Close()

# Vista previa (para revisar el resultado)
$vista = Join-Path $raiz 'assets\icono-preview.png'
(Escalar $maestro 512).Save($vista, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Iconos generados. Vista previa: $vista"
