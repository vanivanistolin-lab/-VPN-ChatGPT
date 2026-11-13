$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf=Join-Path (Split-Path $csc) 'WPF'
New-Item -ItemType Directory -Path (Join-Path $root 'dist') -Force | Out-Null
& $csc /nologo /target:exe /platform:x64 /optimize+ ("/out:$root\dist\VpnStatusHost.exe") "$root\src\VpnStatusHost.cs"
if($LASTEXITCODE -ne 0){throw 'Native helper build failed'}
$resources=@("/resource:$root\README.md,README.md","/resource:$root\dist\VpnStatusHost.exe,VpnStatusHost.exe")
foreach($name in @('manifest.json','background.js','popup.html','popup.css','popup.js')){$resources+="/resource:$root\extension\$name,extension.$name"}
& $csc /nologo /target:winexe /platform:x64 /optimize+ ("/out:$root\dist\ChatGPTRefresh-Setup.exe") /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll /reference:System.Web.Extensions.dll "/reference:$wpf\UIAutomationClient.dll" "/reference:$wpf\UIAutomationTypes.dll" "/reference:$wpf\WindowsBase.dll" @resources "$root\src\ChatGPTRefreshSetup.cs"
if($LASTEXITCODE -ne 0){throw 'Installer build failed'}
