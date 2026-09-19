param([Parameter(Mandatory=$true)][string]$OutputPath, [string]$PreviewPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
function New-IconPng([int]$size) {
    $bmp=New-Object Drawing.Bitmap($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.ScaleTransform($size/256.0,$size/256.0)
        $path=New-Object Drawing.Drawing2D.GraphicsPath
        try {
            $path.AddArc(12,12,76,76,180,90); $path.AddArc(168,12,76,76,270,90)
            $path.AddArc(168,168,76,76,0,90); $path.AddArc(12,168,76,76,90,90); $path.CloseFigure()
            $bg=New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.Rectangle(0,0,256,256)),([Drawing.Color]::FromArgb(31,45,61)),([Drawing.Color]::FromArgb(13,19,29)),90.0)
            try {$g.FillPath($bg,$path)} finally {$bg.Dispose()}
        } finally {$path.Dispose()}
        $screen=New-Object Drawing.Pen(([Drawing.Color]::FromArgb(104,194,235)),11)
        $screen.StartCap='Round'; $screen.EndCap='Round'; $screen.LineJoin='Round'
        try {
            $g.DrawLine($screen,47,65,209,65); $g.DrawLine($screen,47,65,47,189)
            $g.DrawLine($screen,47,189,209,189); $g.DrawLine($screen,209,189,209,65)
        } finally {$screen.Dispose()}
        $pulse=New-Object Drawing.Pen(([Drawing.Color]::FromArgb(67,224,161)),16)
        $pulse.StartCap='Round'; $pulse.EndCap='Round'; $pulse.LineJoin='Round'
        try {
            $points=[Drawing.Point[]]@((New-Object Drawing.Point(66,138)),(New-Object Drawing.Point(91,138)),(New-Object Drawing.Point(108,109)),(New-Object Drawing.Point(131,169)),(New-Object Drawing.Point(150,126)),(New-Object Drawing.Point(164,138)),(New-Object Drawing.Point(190,138)))
            $g.DrawLines($pulse,$points)
        } finally {$pulse.Dispose()}
        $ms=New-Object IO.MemoryStream
        try {$bmp.Save($ms,[Drawing.Imaging.ImageFormat]::Png); return ,$ms.ToArray()} finally {$ms.Dispose()}
    } finally {$g.Dispose();$bmp.Dispose()}
}
$sizes=@(16,24,32,48,64,256)
$images=@($sizes | ForEach-Object {New-IconPng $_})
$full=[IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full)) | Out-Null
$writer=New-Object IO.BinaryWriter([IO.File]::Create($full))
try {
    $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count)
    $offset=6+16*$sizes.Count
    for($i=0;$i -lt $sizes.Count;$i++){
        $writer.Write([byte]($sizes[$i]%256));$writer.Write([byte]($sizes[$i]%256))
        $writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length);$writer.Write([uint32]$offset)
        $offset+=$images[$i].Length
    }
    foreach($bytes in $images){$writer.Write([byte[]]$bytes)}
} finally {$writer.Dispose()}
if($PreviewPath){[IO.File]::WriteAllBytes([IO.Path]::GetFullPath($PreviewPath),$images[$images.Count-1])}
