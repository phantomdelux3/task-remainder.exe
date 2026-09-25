# Generates the installer artwork with the developer mark (gold "deadpool1555" with sparkles).
#   Dialog.bmp  493x312  side panel on the Welcome and Finish pages (the right part stays white for text)
#   Banner.bmp  493x58   strip across the top of the other pages (the left part stays white for titles)
# Windows Installer needs 24-bit BMP files. Run: powershell -File installer\assets\make-branding.ps1
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$ink = [System.Drawing.Color]::FromArgb(0x11, 0x18, 0x27)
$inkLight = [System.Drawing.Color]::FromArgb(0x1F, 0x29, 0x37)
$darkGold = [System.Drawing.Color]::FromArgb(0xB8, 0x86, 0x0B)
$gold = [System.Drawing.Color]::FromArgb(0xFF, 0xD7, 0x00)
$paleGold = [System.Drawing.Color]::FromArgb(0xFF, 0xF8, 0xDC)
$grey = [System.Drawing.Color]::FromArgb(0xD1, 0xD5, 0xDB)

function New-Canvas([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::White)
    return $bmp, $g
}

function Fill-Dark($g, [System.Drawing.RectangleF]$rect) {
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $inkLight, $ink, ([System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($brush, $rect)
    $brush.Dispose()
}

# Draws text as a gold, outlined, glowing shape; returns its bounds.
function Draw-GoldText($g, [string]$text, [float]$emPixels, [float]$centerX, [float]$centerY) {
    $family = New-Object System.Drawing.FontFamily 'Segoe UI'
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddString($text, $family, [int][System.Drawing.FontStyle]::Bold, $emPixels, (New-Object System.Drawing.PointF 0, 0), [System.Drawing.StringFormat]::GenericTypographic)
    $b = $path.GetBounds()
    $m = New-Object System.Drawing.Drawing2D.Matrix
    $m.Translate($centerX - $b.Width / 2 - $b.Left, $centerY - $b.Height / 2 - $b.Top)
    $path.Transform($m)
    $b = $path.GetBounds()

    $glow = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(80, $gold)), 5
    $glow.LineJoin = 'Round'
    $g.DrawPath($glow, $path)

    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $b, $darkGold, $darkGold, ([System.Drawing.Drawing2D.LinearGradientMode]::Horizontal)
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend
    $blend.Colors = @($darkGold, $gold, $paleGold, $gold, $darkGold)
    $blend.Positions = @([single]0, [single]0.3, [single]0.5, [single]0.7, [single]1)
    $brush.InterpolationColors = $blend
    $g.FillPath($brush, $path)

    $outline = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(170, $darkGold)), 1
    $g.DrawPath($outline, $path)
    return $b
}

function Draw-Star($g, [float]$x, [float]$y, [float]$r, [int]$alpha) {
    $w = $r * 0.22
    $pts = @(
        (New-Object System.Drawing.PointF $x, ($y - $r)), (New-Object System.Drawing.PointF ($x + $w), ($y - $w)),
        (New-Object System.Drawing.PointF ($x + $r), $y), (New-Object System.Drawing.PointF ($x + $w), ($y + $w)),
        (New-Object System.Drawing.PointF $x, ($y + $r)), (New-Object System.Drawing.PointF ($x - $w), ($y + $w)),
        (New-Object System.Drawing.PointF ($x - $r), $y), (New-Object System.Drawing.PointF ($x - $w), ($y - $w)))
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($alpha, $paleGold))
    $g.FillPolygon($brush, $pts)
}

# Fixed "random" sparkle layout so the output is the same every run.
function Draw-Sparkles($g, [System.Drawing.RectangleF]$around, [int]$seed, [int]$count) {
    $random = New-Object System.Random $seed
    for ($i = 0; $i -lt $count; $i++) {
        $x = $around.Left - 8 + $random.NextDouble() * ($around.Width + 16)
        $y = $around.Top - 6 + $random.NextDouble() * ($around.Height + 12)
        Draw-Star $g $x $y (2.5 + $random.NextDouble() * 4) (140 + $random.Next(115))
    }
}

function Draw-Centered($g, [string]$text, [System.Drawing.Font]$font, [System.Drawing.Color]$color, [float]$centerX, [float]$y) {
    $size = $g.MeasureString($text, $font)
    $brush = New-Object System.Drawing.SolidBrush $color
    $g.DrawString($text, $font, $brush, ($centerX - $size.Width / 2), $y)
}

# --- Dialog.bmp: dark branded panel on the left 164 px -------------------------------------------
$bmp, $g = New-Canvas 493 312
$panel = New-Object System.Drawing.RectangleF 0, 0, 164, 312
Fill-Dark $g $panel
$cx = 82

# App mark: the yellow marquee strip from the app icon.
$g.FillRectangle((New-Object System.Drawing.SolidBrush $gold), 32, 70, 100, 30)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $ink), 42, 80, 32, 10)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $ink), 80, 80, 12, 10)
$g.FillPolygon((New-Object System.Drawing.SolidBrush $ink), @(
    (New-Object System.Drawing.PointF 100, 85), (New-Object System.Drawing.PointF 120, 75), (New-Object System.Drawing.PointF 120, 95)))

Draw-Centered $g 'Marquee Reminder' (New-Object System.Drawing.Font 'Segoe UI', 11, ([System.Drawing.FontStyle]::Bold)) ([System.Drawing.Color]::White) $cx 116
Draw-Centered $g 'Developer' (New-Object System.Drawing.Font 'Segoe UI', 9) $grey $cx 196
$nameBounds = Draw-GoldText $g 'deadpool1555' 18 $cx 232
Draw-Sparkles $g $nameBounds 1555 12
$bmp.Save((Join-Path $here 'Dialog.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$g.Dispose(); $bmp.Dispose()

# --- Banner.bmp: developer mark on a dark tab at the right --------------------------------------
$bmp, $g = New-Canvas 493 58
$tab = New-Object System.Drawing.RectangleF 293, 0, 200, 58
Fill-Dark $g $tab
Draw-Centered $g 'Developer' (New-Object System.Drawing.Font 'Segoe UI', 8) $grey 393 7
$nameBounds = Draw-GoldText $g 'deadpool1555' 16 393 36
Draw-Sparkles $g $nameBounds 555 8
$bmp.Save((Join-Path $here 'Banner.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
$g.Dispose(); $bmp.Dispose()

"Wrote Dialog.bmp and Banner.bmp to $here"
