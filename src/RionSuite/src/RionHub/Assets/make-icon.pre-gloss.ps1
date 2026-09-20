# Generates Assets/rion.ico — a bold "R" (the Rion logo) on an accent rounded square.
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
    $brush = New-Object System.Drawing.SolidBrush $accent
    $g.FillPath($brush, $path)

    # Geometry, not a font: a broad R with an open counter survives at 16 pixels.
    $g.ScaleTransform([single]($s / 100.0), [single]($s / 100.0))
    $mark = New-Object System.Drawing.Drawing2D.GraphicsPath
    $mark.AddPolygon([System.Drawing.PointF[]]@([System.Drawing.PointF]::new(25,20),[System.Drawing.PointF]::new(59,20),[System.Drawing.PointF]::new(76,32),[System.Drawing.PointF]::new(76,48),[System.Drawing.PointF]::new(61,59),[System.Drawing.PointF]::new(80,80),[System.Drawing.PointF]::new(58,80),[System.Drawing.PointF]::new(42,60),[System.Drawing.PointF]::new(42,80),[System.Drawing.PointF]::new(25,80)))
    $mark.AddPolygon([System.Drawing.PointF[]]@([System.Drawing.PointF]::new(42,34),[System.Drawing.PointF]::new(57,34),[System.Drawing.PointF]::new(60,37),[System.Drawing.PointF]::new(60,43),[System.Drawing.PointF]::new(56,46),[System.Drawing.PointF]::new(42,46)))
    $g.FillPath([System.Drawing.Brushes]::White, $mark)
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
