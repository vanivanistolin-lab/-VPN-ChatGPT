$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$assembly=[Reflection.Assembly]::LoadFile((Join-Path $root 'dist\ChatGPTRefresh.exe'))
$method=$assembly.GetType('Program').GetMethod('EngineConfig',[Reflection.BindingFlags]'NonPublic,Static')
$id='bf000d23-0752-40b4-affe-68f7707a9661'
$public=[Convert]::ToBase64String([byte[]](1..32)).TrimEnd('=').Replace('+','-').Replace('/','_')
$vmess=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('{"add":"127.0.0.1","port":"443","id":"'+$id+'","aid":"0","net":"tcp","tls":""}'))
$links=@(('vless://'+$id+'@127.0.0.1:443?security=tls&sni=example.test'),('vless://'+$id+'@127.0.0.1:443?security=reality&sni=example.test&pbk='+$public+'&sid=abcd&flow=xtls-rprx-vision'),('vless://'+$id+'@127.0.0.1:443?security=tls&type=ws&path=%2Fws&sni=example.test'),('vless://'+$id+'@127.0.0.1:443?security=tls&type=grpc&serviceName=test&sni=example.test'),('vmess://'+$vmess),'trojan://test-password@127.0.0.1:443?sni=example.test','ss://aes-128-gcm:test-password@127.0.0.1:8388','hysteria2://test-password@127.0.0.1:443?sni=example.test',('tuic://'+$id+':test-password@127.0.0.1:443?sni=example.test'))
$tests=Join-Path $root '.build-cache\tests'
New-Item -ItemType Directory -Path $tests -Force | Out-Null
$i=0
foreach($link in $links){$i++;$json=$method.Invoke($null,@($link));$path=Join-Path $tests ("config-$i.json");[IO.File]::WriteAllText($path,$json,(New-Object Text.UTF8Encoding $false));& "$root\engine\sing-box.exe" check -c $path;if($LASTEXITCODE -ne 0){throw "Engine configuration test $i failed"}}
Write-Output "Validated $i engine configurations without starting any VPN."
