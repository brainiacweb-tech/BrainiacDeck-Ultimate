$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$logo=Join-Path $root 'assets/BrainiacDeck-logo.png'
if(!(Test-Path -LiteralPath $logo)) { throw 'The supplied BrainiacDeck logo is required.' }
$source=[Drawing.Image]::FromFile($logo)
$frames=[Collections.Generic.List[object]]::new()
function Render-Logo([int]$size,[string]$path) {
 $bitmap=[Drawing.Bitmap]::new($size,$size)
 $graphics=[Drawing.Graphics]::FromImage($bitmap)
 try {
  $graphics.Clear([Drawing.Color]::Transparent)
  $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $graphics.DrawImage($source,0,0,$size,$size)
  $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
 } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
try {
 foreach($size in @(16,32,48,256)) {
  $path=Join-Path $root ("assets/icon-$size.png"); Render-Logo $size $path
  $frames.Add(@{size=$size;bytes=[IO.File]::ReadAllBytes($path)})
 }
 $stream=[IO.File]::Create((Join-Path $root 'assets/BrainiacDesk.ico'))
 $writer=[IO.BinaryWriter]::new($stream)
 try {
  $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
  $offset=6+16*$frames.Count
  foreach($frame in $frames) {
   $dimension=if($frame.size -eq 256) {0} else {$frame.size}
   $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
   $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.bytes.Length); $writer.Write([uint32]$offset)
   $offset+=$frame.bytes.Length
  }
  foreach($frame in $frames) { $writer.Write([byte[]]$frame.bytes) }
 } finally { $writer.Dispose(); $stream.Dispose() }
 foreach($asset in @(@(44,'Square44x44Logo.png'),@(150,'Square150x150Logo.png'),@(50,'StoreLogo.png'))) { Render-Logo $asset[0] (Join-Path $root ('src/BrainiacDesk.Package/Assets/'+$asset[1])) }
} finally { $source.Dispose() }
Write-Output 'Generated application, tray and package icons from the supplied logo; package identity was retained.'
