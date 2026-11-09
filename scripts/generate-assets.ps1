Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Make-Logo([int]$size, [string]$path) {
    $image = [Drawing.Bitmap]::new($size,$size)
    $g = [Drawing.Graphics]::FromImage($image)
    try {
        $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.Clear([Drawing.ColorTranslator]::FromHtml('#101827'))
        $pen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#F59E0B'), [single]($size*0.045))
        $brush = [Drawing.SolidBrush]::new([Drawing.Color]::White)
        try {
            $g.DrawRectangle($pen,[single]($size*.15),[single]($size*.20),[single]($size*.70),[single]($size*.53))
            $g.DrawLine($pen,[single]($size*.34),[single]($size*.84),[single]($size*.66),[single]($size*.84))
            $g.DrawLine($pen,[single]($size*.50),[single]($size*.73),[single]($size*.50),[single]($size*.84))
            foreach($x in @(.28,.46,.64)) { $g.FillRectangle($brush,[single]($size*$x),[single]($size*.41),[single]($size*.09),[single]($size*.13)) }
            $g.DrawLine($pen,[single]($size*.37),[single]($size*.475),[single]($size*.64),[single]($size*.475))
            $image.Save($path,[Drawing.Imaging.ImageFormat]::Png)
        } finally { $pen.Dispose(); $brush.Dispose() }
    } finally { $g.Dispose(); $image.Dispose() }
}
foreach($size in @(16,32,48,256)) { Make-Logo $size (Join-Path $root "assets/icon-$size.png") }
Make-Logo 150 (Join-Path $root 'src/BrainiacDesk.Package/Assets/Square150x150Logo.png')
Make-Logo 44 (Join-Path $root 'src/BrainiacDesk.Package/Assets/Square44x44Logo.png')
Make-Logo 50 (Join-Path $root 'src/BrainiacDesk.Package/Assets/StoreLogo.png')
$stream = [IO.File]::Create((Join-Path $root 'assets/BrainiacDesk.ico'))
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]4)
    $offset=6+16*4
    foreach($size in @(16,32,48,256)) {
        $bytes=[IO.File]::ReadAllBytes((Join-Path $root "assets/icon-$size.png"))
        $dimension=if($size -eq 256){0}else{$size}
        $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$bytes.Length);$writer.Write([uint32]$offset)
        $offset += $bytes.Length
    }
    foreach($size in @(16,32,48,256)) { $writer.Write([IO.File]::ReadAllBytes((Join-Path $root "assets/icon-$size.png"))) }
} finally { $writer.Dispose(); $stream.Dispose() }
