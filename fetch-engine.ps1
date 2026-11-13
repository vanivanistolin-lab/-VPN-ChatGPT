$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$engine=Join-Path $root 'engine'
$cache=Join-Path $root '.build-cache'
New-Item -ItemType Directory -Path $engine,$cache -Force | Out-Null
function Verify-Hash($path,$expected){return (Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $expected}
if(-not (Verify-Hash (Join-Path $engine 'sing-box.exe') '8F30682EE198855440D2A7FE070D5D3653793B2D01BB8C85EEA88F93BBE7C8B9')){
 $zip=Join-Path $cache 'sing-box-1.14.4-windows-amd64.zip'
 Invoke-WebRequest -Uri 'https://github.com/SagerNet/sing-box/releases/download/v1.14.4/sing-box-1.14.4-windows-amd64.zip' -OutFile $zip
 if(-not (Verify-Hash $zip '99185D75D314A07DBBA4A130C8AD986CBEB629A07927975636B341CA61F991DF')){throw 'sing-box archive checksum mismatch'}
 Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $cache 'sing-box') -Force
 $source=Join-Path $cache 'sing-box\sing-box-1.14.4-windows-amd64'
 Copy-Item -LiteralPath (Join-Path $source 'sing-box.exe') -Destination (Join-Path $engine 'sing-box.exe') -Force
 Copy-Item -LiteralPath (Join-Path $source 'LICENSE') -Destination (Join-Path $engine 'LICENSE-sing-box.txt') -Force
}
if(-not (Verify-Hash (Join-Path $engine 'wintun.dll') 'E5DA8447DC2C320EDC0FC52FA01885C103DE8C118481F683643CACC3220DAFCE')){
 $zip=Join-Path $cache 'wintun-0.14.1.zip'
 Invoke-WebRequest -Uri 'https://www.wintun.net/builds/wintun-0.14.1.zip' -OutFile $zip
 if(-not (Verify-Hash $zip '07C256185D6EE3652E09FA55C0B673E2624B565E02C4B9091C79CA7D2F24EF51')){throw 'Wintun archive checksum mismatch'}
 Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $cache 'wintun') -Force
 Copy-Item -LiteralPath (Join-Path $cache 'wintun\wintun\bin\amd64\wintun.dll') -Destination (Join-Path $engine 'wintun.dll') -Force
 Copy-Item -LiteralPath (Join-Path $cache 'wintun\wintun\LICENSE.txt') -Destination (Join-Path $engine 'LICENSE-wintun.txt') -Force
}
if(-not (Test-Path -LiteralPath (Join-Path $engine 'GPL-3.0.txt'))){Invoke-WebRequest -Uri 'https://www.gnu.org/licenses/gpl-3.0.txt' -OutFile (Join-Path $engine 'GPL-3.0.txt')}
Write-Output 'Official sing-box 1.14.4 and Wintun 0.14.1 verified.'
