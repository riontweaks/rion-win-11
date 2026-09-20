# Generates Assets/rion.ico — a medium-weight "R" (the Rion logo) on an accent rounded square.
Add-Type -AssemblyName System.Drawing

$sizes  = 16,20,24,32,40,48,64,96,128,256
$accent = [System.Drawing.Color]::FromArgb(255, 47, 111, 235)
$outDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$blobs  = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)

    # rounded-square background
    $pad    = [Math]::Max(1, [int]($s * 0.06))
    $radius = [int]($s * 0.22)
    $rect   = New-Object System.Drawing.Rectangle $pad, $pad, ($s - 2*$pad), ($s - 2*$pad)
    $path   = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect, [System.Drawing.Color]::FromArgb(255, 107, 129, 162), [System.Drawing.Color]::FromArgb(255, 22, 33, 52), [single]90)
    $blend = [System.Drawing.Drawing2D.ColorBlend]::new(5)
    $blend.Positions = [single[]]@(0,.24,.53,.8,1)
    $blend.Colors = [System.Drawing.Color[]]@([System.Drawing.ColorTranslator]::FromHtml('#52677B'),[System.Drawing.ColorTranslator]::FromHtml('#344B65'),[System.Drawing.ColorTranslator]::FromHtml('#1E344E'),[System.Drawing.ColorTranslator]::FromHtml('#102238'),[System.Drawing.ColorTranslator]::FromHtml('#080F1B'))
    $brush.InterpolationColors = $blend
    $g.FillPath($brush, $path)
    # Dark outer edge and an inset metallic bevel, matching the reference's layered frame.
    $rim = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#101822'), [single][Math]::Max(1,$s*.024))
    $g.DrawPath($rim, $path)
    $inset = $path.Clone()
    $matrix = [System.Drawing.Drawing2D.Matrix]::new([single].94,0,0,[single].94,[single]($s*.03),[single]($s*.03))
    $inset.Transform($matrix)
    $bevelBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect,[System.Drawing.ColorTranslator]::FromHtml('#94A6B6'),[System.Drawing.ColorTranslator]::FromHtml('#182434'),[single]65)
    $bevel = [System.Drawing.Pen]::new($bevelBrush,[single][Math]::Max(.65,$s*.014))
    $g.DrawPath($bevel,$inset)
    $inset.Transform($matrix)
    $innerEdge = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(175,8,17,29),[single][Math]::Max(.5,$s*.01))
    $g.DrawPath($innerEdge,$inset)
    $innerEdge.Dispose(); $bevel.Dispose(); $bevelBrush.Dispose(); $matrix.Dispose(); $inset.Dispose()
    $brush.Dispose(); $rim.Dispose()

    # Geometry, not a font: a lighter R with an enlarged counter survives at 16 pixels.
    $g.ScaleTransform([single]($s / 100.0), [single]($s / 100.0))
    $mark = New-Object System.Drawing.Drawing2D.GraphicsPath
    $mark.AddLine(33,25,52,25)
    $mark.AddBezier(52,25,65,25,70,31,70,41)
    $mark.AddBezier(70,41,70,49,66,54,58,56)
    $mark.AddLine(58,56,71,73)
    $mark.AddBezier(71,73,72,75,71,76,69,76)
    $mark.AddLine(69,76,63,76)
    $mark.AddBezier(63,76,63,76,62,75,61,74)
    $mark.AddLine(61,74,48,57)
    $mark.AddLine(48,57,39,57)
    $mark.AddLine(39,57,39,74)
    $mark.AddBezier(39,74,39,76,38,76,37,76)
    $mark.AddLine(37,76,33,76)
    $mark.AddBezier(33,76,31,76,31,75,31,74)
    $mark.AddLine(31,74,31,27)
    $mark.AddBezier(31,27,31,25,32,25,33,25)
    $mark.CloseFigure()
    $mark.StartFigure()
    $mark.AddLine(39,32,52,32)
    $mark.AddBezier(52,32,59,32,62,35,62,41)
    $mark.AddBezier(62,41,62,47,58,50,52,50)
    $mark.AddLine(52,50,39,50)
    $mark.CloseFigure()
    $letterBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(30,24,43,53),[System.Drawing.Color]::White,[System.Drawing.ColorTranslator]::FromHtml('#C5D5E9'),[single]90)
    $g.FillPath($letterBrush, $mark)
    $letterBrush.Dispose()
    $g.ResetTransform()
    $mark.Dispose()
    $bmp.Save((Join-Path $outDir "rion-$s.png"), [System.Drawing.Imaging.ImageFormat]::Png)

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $blobs += ,($ms.ToArray())
}

# assemble .ico (PNG-compressed entries, valid on Vista+)
$ico = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter $ico
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $blobs[$i].Length
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($b in $blobs) { $bw.Write($b) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $outDir 'rion.ico'), $ico.ToArray())
Write-Host "wrote $(Join-Path $outDir 'rion.ico')  ($($ico.Length) bytes)"


