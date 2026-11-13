using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
partial class Program {
 static bool autoHapp=true;
 static void LoadAutoHapp(){try{bool value;if(Boolean.TryParse(File.ReadAllText(Path.Combine(Home,"auto-happ.txt")).Trim(),out value))autoHapp=value;}catch{}}
 static void SaveAutoHapp(){Directory.CreateDirectory(Home);File.WriteAllText(Path.Combine(Home,"auto-happ.txt"),autoHapp.ToString());}
 static void RequestAutoHapp(){
  if(!autoHapp){SetStatus("Выберите сервер и нажмите Подключить в окне VPNKRUT-SUB");return;}
  if(DateTime.Now<nextHapp)return;nextHapp=DateTime.Now.AddSeconds(30);
  string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"FlyFrogLLC\Happ\Happ.exe");
  if(!File.Exists(path)){foreach(var process in Process.GetProcessesByName("Happ")){try{if(File.Exists(process.MainModule.FileName)){path=process.MainModule.FileName;break;}}catch{}}}
  if(!File.Exists(path)){SetStatus("Happ не найден. Можно выбрать встроенный сервер ниже.");return;}
  Process.Start(new ProcessStartInfo(path,"happ://connect"){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Minimized});
  SetStatus("ChatGPT открыт: Happ получил запрос подключения");
 }
}
