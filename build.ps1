$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$wpf=Join-Path (Split-Path $csc) 'WPF'
New-Item -ItemType Directory -Path (Join-Path $root 'dist') -Force | Out-Null
& $csc /nologo /target:winexe "/win32icon:$root\assets\vpnkrut-sub.ico" "/resource:$root\assets\vpnkrut-sub.ico,Brand.ico" /platform:x64 /optimize+ ("/out:$root\dist\ChatGPTRefresh.exe") /reference:System.Core.dll /reference:System.Net.Http.dll /reference:System.Web.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll /reference:System.Management.dll /reference:Accessibility.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll "/reference:$wpf\UIAutomationClient.dll" "/reference:$wpf\UIAutomationTypes.dll" "/reference:$wpf\WindowsBase.dll" "/resource:$root\README.md,README.md" "/resource:$root\USER_AGREEMENT.md,USER_AGREEMENT.md" "$root\src\ChatGPTRefreshSetup.cs" "$root\src\Dashboard.cs" "$root\src\Agreement.cs" "$root\src\AdminPanel.cs" "$root\src\VpnConfig.cs" "$root\src\PublicCatalog.cs" "$root\src\BuiltinVpn.cs" "$root\src\AutoHapp.cs"
if($LASTEXITCODE -ne 0){throw 'Build failed'}
$p=Start-Process -FilePath "$root\dist\ChatGPTRefresh.exe" -ArgumentList '--self-test' -PassThru -Wait -WindowStyle Hidden
if($p.ExitCode -ne 0){throw "Self-test failed: $($p.ExitCode)"}






& $csc /nologo /target:winexe /platform:x64 /optimize+ ("/out:$root\dist\VPNKRUTEngine.exe") /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/win32manifest:$root\src\engine.manifest" "/win32icon:$root\assets\vpnkrut-sub.ico" "/resource:$root\assets\vpnkrut-sub.ico,Brand.ico" "$root\src\EngineHost.cs"
if($LASTEXITCODE -ne 0){throw 'Engine host build failed'}


