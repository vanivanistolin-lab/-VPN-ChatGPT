using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
[assembly:AssemblyCompany("VPNKRUT-SUB")]
[assembly:AssemblyProduct("VPNKRUT-SUB VPN engine host")]
[assembly:AssemblyVersion("3.0.0.0")]
class EngineHost {
 static Process core;static Form window;static NotifyIcon tray;static Label info;static bool stopping,started;
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool AttachConsole(uint id);
 [DllImport("kernel32.dll")]static extern bool FreeConsole();
 [DllImport("kernel32.dll")]static extern bool GenerateConsoleCtrlEvent(uint signal,uint group);
 [DllImport("kernel32.dll")]static extern bool SetConsoleCtrlHandler(IntPtr handler,bool add);
 static string Hash(string file){using(var sha=SHA256.Create())using(var stream=File.OpenRead(file)){return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}}
 static void Stop(){if(core==null||!started||core.HasExited){Application.Exit();return;}if(MessageBox.Show(window,"Отключить только соединение, запущенное встроенным движком VPNKRUT-SUB?","VPNKRUT-SUB",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;stopping=true;
  try{if(AttachConsole((uint)core.Id)){SetConsoleCtrlHandler(IntPtr.Zero,true);GenerateConsoleCtrlEvent(0,0);core.WaitForExit(5000);FreeConsole();SetConsoleCtrlHandler(IntPtr.Zero,false);}if(!core.HasExited){core.Kill();core.WaitForExit(3000);}}catch{}Application.Exit();
 }
 [STAThread]static int Main(string[] args){
  Application.EnableVisualStyles();if(args.Length!=1)return 1;
  bool created;using(var mutex=new Mutex(true,"Local\\VPNKRUTSUB.Engine",out created)){
   if(!created){MessageBox.Show("Встроенный движок уже запущен. Используйте его значок возле часов.","VPNKRUT-SUB");return 0;}
   string config=Path.GetFullPath(args[0]);
   try{
    if(config.StartsWith(@"\\")||!config.EndsWith(".json",StringComparison.OrdinalIgnoreCase)||!config.Contains(@"\ChatGPT-Happ\sessions\")||!File.Exists(config)||new FileInfo(config).Length>2097152)throw new InvalidOperationException("Неверный файл конфигурации.");
    string folder=Path.Combine(Application.StartupPath,"engine"),engine=Path.Combine(folder,"sing-box.exe"),driver=Path.Combine(folder,"wintun.dll");
    if(Hash(engine)!="8F30682EE198855440D2A7FE070D5D3653793B2D01BB8C85EEA88F93BBE7C8B9"||Hash(driver)!="E5DA8447DC2C320EDC0FC52FA01885C103DE8C118481F683643CACC3220DAFCE")throw new InvalidOperationException("Файлы движка изменены. Переустановите приложение из официального MSI.");
    var json=new JavaScriptSerializer{MaxJsonLength=2097152};var content=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(config));foreach(string key in content.Keys)if(Array.IndexOf(new[]{"log","dns","inbounds","outbounds","route"},key)<0)throw new InvalidOperationException("Посторонние разделы конфигурации запрещены.");
    window=new Form{Text="VPNKRUT-SUB · Встроенный VPN",ClientSize=new Size(480,210),StartPosition=FormStartPosition.CenterScreen,BackColor=Color.FromArgb(16,22,25),ForeColor=Color.White,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false};
    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Brand.ico")){window.Icon=new Icon(stream);}
    info=new Label{Left=22,Top=22,Width=436,Height=90,Text="Запуск встроенного VPN…\n\nПри закрытии этого окна движок останется в фоне.",Font=new Font("Segoe UI",11),ForeColor=Color.White};window.Controls.Add(info);
    var stop=new Button{Left=22,Top=135,Width=436,Height=40,Text="Отключить соединение VPNKRUT-SUB",BackColor=Color.FromArgb(151,227,181),FlatStyle=FlatStyle.Flat};stop.Click+=delegate{Stop();};window.Controls.Add(stop);
    tray=new NotifyIcon{Icon=window.Icon,Text="VPNKRUT-SUB · встроенный VPN",Visible=true};var menu=new ContextMenuStrip();menu.Items.Add("Открыть окно",null,delegate{window.Show();window.Activate();});menu.Items.Add("Отключить своё соединение",null,delegate{Stop();});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{window.Show();window.Activate();};
    core=new Process{StartInfo=new ProcessStartInfo(engine,"run -c \""+config+"\""){UseShellExecute=false,WorkingDirectory=folder,CreateNoWindow=false,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true},EnableRaisingEvents=true};
    core.OutputDataReceived+=delegate{};core.ErrorDataReceived+=delegate{};
    core.Exited+=delegate{if(window!=null&&!window.IsDisposed){try{window.BeginInvoke(new Action(delegate{if(!stopping){info.Text="Движок завершил работу. Соединение не подтверждено. Проверьте сервер или права Windows.";stop.Enabled=false;}}));}catch{}}};
    window.FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!stopping&&core!=null&&started&&!core.HasExited&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;window.Hide();}};
    var timer=new System.Windows.Forms.Timer{Interval=1000};timer.Tick+=delegate{if(started&&!core.HasExited){bool up=false;foreach(var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())if(adapter.Name=="VPNKRUT-SUB"&&adapter.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up)up=true;info.Text=up?"Туннель VPNKRUT-SUB создан.\nДоступность интернета через сервер не проверена.\nЗакрытие окна оставляет VPN в фоне.":"Движок работает, ожидание TUN-адаптера…\nЕсли запуск не завершается, проверьте сервер.";}};
    window.Shown+=delegate{try{core.Start();started=true;core.BeginOutputReadLine();core.BeginErrorReadLine();timer.Start();}catch(Exception ex){info.Text="Не удалось запустить VPN: "+ex.Message;stop.Enabled=false;}};
    Application.Run(window);
    timer.Dispose();if(tray!=null)tray.Dispose();
    if(core==null||!started||core.HasExited){try{File.Delete(config);}catch{}}
    return 0;
   }catch(Exception ex){MessageBox.Show(ex.Message,"VPNKRUT-SUB",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
   finally{mutex.ReleaseMutex();}
  }
 }
}
