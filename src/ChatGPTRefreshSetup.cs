using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
class ChatGPTRefresh {
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
 [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint f,uint t,out UIntPtr result);
 static readonly string Dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChatGPT-Happ");
 static readonly Regex Title=new Regex(@"\bChat\s?GPT\b",RegexOptions.IgnoreCase);
 static readonly Regex Url=new Regex(@"^(https?://)?(www\.)?(chatgpt\.com|chat\.openai\.com)([/:?#]|$)",RegexOptions.IgnoreCase);
 static bool previousUp=false,pending=false,busy=false;
 static string lastRequest="";
 static DateTime ready=DateTime.MaxValue;
 static HashSet<IntPtr> refreshed=new HashSet<IntPtr>();
 static NotifyIcon tray;
 static void Log(string s){try{Directory.CreateDirectory(Dir);File.AppendAllText(Path.Combine(Dir,"exe-refresh.log"),DateTime.Now.ToString("s")+" "+s+Environment.NewLine);}catch{}}
 static bool Connected(){foreach(var a in NetworkInterface.GetAllNetworkInterfaces())if(a.OperationalStatus==OperationalStatus.Up && Regex.IsMatch(a.Name+" "+a.Description,"happ|xray",RegexOptions.IgnoreCase))return true;return false;}
 static string Request(){try{var lines=File.ReadAllLines(Path.Combine(Dir,"status.log"));for(int i=lines.Length-1;i>=Math.Max(0,lines.Length-12);i--)if(lines[i].Contains("Connect requested;"))return lines[i];}catch{}return "";}
 static bool IsChat(IntPtr h){
  uint id;GetWindowThreadProcessId(h,out id);
  string name;try{name=Process.GetProcessById((int)id).ProcessName.ToLowerInvariant();}catch{return false;}
  if(!new HashSet<string>{"chrome","msedge","firefox","brave","opera","browser","vivaldi"}.Contains(name))return false;
  var title=new StringBuilder(2048);GetWindowText(h,title,title.Capacity);
  if(Title.IsMatch(title.ToString()))return true;
  try{
   var root=AutomationElement.FromHandle(h);
   var edits=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit));
   foreach(AutomationElement e in edits){
    string label=e.Current.Name+" "+e.Current.AutomationId;
    if(!Regex.IsMatch(label,"address|адрес|omnibox|urlbar|поиск или",RegexOptions.IgnoreCase))continue;
    object value;if(e.TryGetCurrentPattern(ValuePattern.Pattern,out value)&&Url.IsMatch(((ValuePattern)value).Current.Value.Trim()))return true;
   }
  }catch(Exception ex){Log("Address lookup: "+ex.GetType().Name);}
  return false;
 }
 static bool Refresh(IntPtr window){
  if(GetForegroundWindow()!=window)return false;
  UIntPtr handled;
  var sent=SendMessageTimeout(window,0x0319,window,new IntPtr(3<<16),2,1000,out handled);
  if(sent!=IntPtr.Zero && handled!=UIntPtr.Zero){Log("Browser accepted refresh command");return true;}
  if(GetForegroundWindow()!=window)return false;
  SendKeys.SendWait("{F5}");Log("F5 sent to ChatGPT browser window");return true;
 }
 static void Tick(object sender,EventArgs args){
  if(busy)return;busy=true;
  try{
   bool up=Connected();string request=Request();bool newRequest=request!=""&&request!=lastRequest;
   if(newRequest)lastRequest=request;
   if(up&&(!previousUp||newRequest)){pending=true;ready=DateTime.Now.AddSeconds(3);refreshed.Clear();Log("VPN connection detected; refresh queued");}
   if(!up){pending=false;refreshed.Clear();}
   tray.Text=up?"ChatGPT: VPN подключён":"ChatGPT: ожидание VPN";
   IntPtr window=GetForegroundWindow();
   if(up&&DateTime.Now>=ready&&!refreshed.Contains(window)&&IsChat(window)){
    if(Refresh(window)){refreshed.Add(window);pending=false;}
   }
   previousUp=up;
  }catch(Exception ex){Log("Error: "+ex.ToString());}
  finally{busy=false;}
 }
 static bool InstallForUser(){
  try {
   Directory.CreateDirectory(Dir);
   string current=Application.ExecutablePath;
   string installed=Path.Combine(Dir,"ChatGPTRefresh.exe");
   if(!String.Equals(current,installed,StringComparison.OrdinalIgnoreCase)) {
    foreach(var process in Process.GetProcessesByName("ChatGPTRefresh")) {
     try {if(process.Id!=Process.GetCurrentProcess().Id && String.Equals(process.MainModule.FileName,installed,StringComparison.OrdinalIgnoreCase)){process.Kill();process.WaitForExit(2000);}}catch{}
    }
    File.Copy(current,installed,true);
   var assembly=System.Reflection.Assembly.GetExecutingAssembly();
   string extensionDir=Path.Combine(Dir,"extension");Directory.CreateDirectory(extensionDir);
   foreach(string resource in assembly.GetManifestResourceNames()){
    string destination=null;
    if(resource.StartsWith("extension."))destination=Path.Combine(extensionDir,resource.Substring(10));
    else if(resource=="README.md")destination=Path.Combine(Dir,"README.md");
    else if(resource=="VpnStatusHost.exe")destination=Path.Combine(extensionDir,"VpnStatusHost.exe");
    if(destination!=null)using(var input=assembly.GetManifestResourceStream(resource))using(var output=File.Create(destination)){input.CopyTo(output);}
   }
   string hostExe=Path.Combine(extensionDir,"VpnStatusHost.exe");
   var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
   string hostJson=serializer.Serialize(new {name="com.chatgpt_happ.status",description="Read-only Happ VPN status",path=hostExe,type="stdio",allowed_origins=new string[]{"chrome-extension://ibdbopmjjoidijofindggapmndoecbhk/"}});
   string hostManifest=Path.Combine(extensionDir,"native-host.json");File.WriteAllText(hostManifest,hostJson,new UTF8Encoding(false));
   using(var registry=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Google\Chrome\NativeMessagingHosts\com.chatgpt_happ.status")){registry.SetValue("",hostManifest);}
   }
   dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
   dynamic shortcut=shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"ChatGPTRefresh.lnk"));
   shortcut.TargetPath=installed;
   shortcut.WorkingDirectory=Dir;
   shortcut.Description="ChatGPT Refresh";
   shortcut.Save();
   Marshal.FinalReleaseComObject(shortcut);
   Marshal.FinalReleaseComObject(shell);
   if(!String.Equals(current,installed,StringComparison.OrdinalIgnoreCase)) {
    Process.Start(new ProcessStartInfo(installed){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden});
    Process.Start(new ProcessStartInfo("notepad.exe", "\""+Path.Combine(Dir,"README.md")+"\""){UseShellExecute=true});
    try {
     string browser=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"Google\Chrome\Application\chrome.exe");
     if(!File.Exists(browser))browser=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Google\Chrome\Application\chrome.exe");
     if(!File.Exists(browser))browser="chrome.exe";
     Process.Start(new ProcessStartInfo(browser,"chrome://extensions/"){UseShellExecute=true});
    } catch {MessageBox.Show("Откройте в Chrome страницу chrome://extensions.","Подключение расширения");}
    Process.Start(new ProcessStartInfo("explorer.exe","\""+Path.Combine(Dir,"extension")+"\""){UseShellExecute=true});
    return false;
   }
   return true;
  }catch(Exception ex){MessageBox.Show("Не удалось установить автозагрузку: "+ex.Message,"ChatGPT Refresh",MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}
 }
 [STAThread] static void Main(){
  if(!InstallForUser())return;
  bool created;using(var mutex=new Mutex(true,"Local\\ChatGPTRefreshExe",out created)){
   if(!created)return;
   Application.EnableVisualStyles();
   tray=new NotifyIcon{Icon=System.Drawing.SystemIcons.Information,Visible=true,Text="ChatGPT: ожидание VPN"};
   var menu=new ContextMenuStrip();
   menu.Items.Add("Выход",null,delegate{Application.Exit();});tray.ContextMenuStrip=menu;
   var timer=new System.Windows.Forms.Timer{Interval=500};timer.Tick+=Tick;timer.Start();
   Log("EXE started");
   try{Application.Run();}finally{tray.Visible=false;tray.Dispose();mutex.ReleaseMutex();}
  }
 }
}




