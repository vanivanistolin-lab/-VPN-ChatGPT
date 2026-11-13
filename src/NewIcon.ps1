Add-Type -AssemblyName System.Drawing
$assets=Join-Path $PSScriptRoot '..\assets'
$sizes=@(16,24,32,48,64,128,256)
$frames=@()
foreach($size in $sizes){
 $bmp=New-Object Drawing.Bitmap($size,$size)
 $g=[Drawing.Graphics]::FromImage($bmp)
 $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 $g.ScaleTransform(($size/256.0),($size/256.0))
 $g.Clear([Drawing.Color]::Transparent)
 $shape=New-Object Drawing.Drawing2D.GraphicsPath
 $shape.AddArc(8,8,64,64,180,90);$shape.AddArc(184,8,64,64,270,90);$shape.AddArc(184,184,64,64,0,90);$shape.AddArc(8,184,64,64,90,90);$shape.CloseFigure()
 $brush=New-Object Drawing.Drawing2D.LinearGradientBrush([Drawing.Point]::new(0,0),[Drawing.Point]::new(256,256),[Drawing.Color]::FromArgb(30,67,59),[Drawing.Color]::FromArgb(11,28,34))
 $g.FillPath($brush,$shape)
 $shield=New-Object Drawing.Drawing2D.GraphicsPath
 $shield.AddLines([Drawing.PointF[]]@([Drawing.PointF]::new(128,35),[Drawing.PointF]::new(205,64),[Drawing.PointF]::new(197,143)))
 $shield.AddBezier(197,143,184,184,151,207,128,221)
 $shield.AddBezier(128,221,102,207,71,184,59,143)
 $shield.AddLine(59,143,51,64);$shield.CloseFigure()
 $border=New-Object Drawing.Pen([Drawing.Color]::FromArgb(147,232,182),9)
 $border.LineJoin=[Drawing.Drawing2D.LineJoin]::Round
 $g.DrawPath($border,$shield)
 $pen=New-Object Drawing.Pen([Drawing.Color]::FromArgb(238,255,245),16)
 $pen.StartCap=[Drawing.Drawing2D.LineCap]::Round;$pen.EndCap=$pen.StartCap;$pen.LineJoin=[Drawing.Drawing2D.LineJoin]::Round
 $g.DrawLine($pen,99,89,99,167)
 $g.DrawLine($pen,158,89,112,128)
 $g.DrawLine($pen,112,128,159,168)
 $g.Dispose();$brush.Dispose();$border.Dispose();$pen.Dispose();$shape.Dispose();$shield.Dispose()
 $memory=New-Object IO.MemoryStream
 $bmp.Save($memory,[Drawing.Imaging.ImageFormat]::Png)
 $frames+=,@{Size=$size;Bytes=$memory.ToArray()}
 if($size -eq 256){$bmp.Save((Join-Path $assets 'vpnkrut-sub.png'),[Drawing.Imaging.ImageFormat]::Png)}
 $memory.Dispose();$bmp.Dispose()
}
$stream=[IO.File]::Create((Join-Path $assets 'vpnkrut-sub.ico'))
$writer=New-Object IO.BinaryWriter($stream)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
$offset=6+16*$frames.Count
foreach($frame in $frames){$dim=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$dim);$writer.Write([byte]$dim);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
$writer.Dispose()
