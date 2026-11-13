using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using Accessibility;
[assembly: AssemblyCompany("VPNKRUT-SUB")]
[assembly: AssemblyProduct("VPNKRUT-SUB")]
[assembly: AssemblyTitle("VPNKRUT-SUB")]
[assembly: AssemblyVersion("3.0.1.0")]
[assembly: AssemblyFileVersion("3.0.1.0")]
partial class Program {
 static readonly string Home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChatGPT-Happ");
 static readonly string Exe=Path.Combine(Home,"ChatGPTRefresh.exe");
 static readonly string Startup=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"ChatGPTRefresh.lnk");
 static readonly HashSet<string> Browsers=new HashSet<string>{"chrome","msedge","brave","opera","browser","vivaldi","firefox"};
 static NotifyIcon icon; static Label status; static Form panel; static bool busy,wasUp; static long epoch;
 static DateTime ready=DateTime.MaxValue, nextHapp=DateTime.MinValue; static string lastInfo="Ожидание ChatGPT";
 static EventWaitHandle showWindowSignal;
 static Dictionary<string,long> refreshed=new Dictionary<string,long>(); static Mutex mutex;
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr h,int command);
 [DllImport("oleacc.dll")] static extern int AccessibleObjectFromWindow(IntPtr h,uint id,ref Guid iid,[MarshalAs(UnmanagedType.Interface)] out object result);
 [DllImport("oleacc.dll")] static extern int AccessibleChildren(IAccessible parent,int start,int count,[Out,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=2)]object[] children,out int obtained);
 class Page { public string Key; public AutomationElement Reload; public IAccessible Legacy; public object Child=0; }
 static string ReadmePath(){string packaged=Path.Combine(Application.StartupPath,"README.txt");return File.Exists(packaged)?packaged:Path.Combine(Home,"README.md");}
 static void Log(string text){try{Directory.CreateDirectory(Home);string p=Path.Combine(Home,"standalone.log");if(File.Exists(p)&&new FileInfo(p).Length>524288)File.WriteAllText(p,"");File.AppendAllText(p,DateTime.Now.ToString("s")+" "+text+Environment.NewLine);}catch{}}
 static bool ChatUrl(string value,out string key){
  key=null;if(String.IsNullOrWhiteSpace(value))return false;
  value=value.Trim();Uri uri;
  if(!Uri.TryCreate(value.Contains("://")?value:"https://"+value,UriKind.Absolute,out uri))return false;
  if(uri.Scheme!="https"&&uri.Scheme!="http")return false;
  if(uri.Host!="chatgpt.com"&&uri.Host!="www.chatgpt.com"&&uri.Host!="chat.openai.com")return false;
  key=uri.Host+uri.AbsolutePath;return true;
 }
 static bool AddressName(string name){name=(name??"").ToLowerInvariant();return name.Contains("address")||name.Contains("адрес")||name.Contains("omnibox")||name.Contains("urlbar")||name.Contains("поиск или");}
 static bool ReloadName(string name){name=(name??"").Trim().ToLowerInvariant();return name=="reload"||name=="reload this page"||name=="refresh"||name=="перезагрузить"||name=="обновить"||name=="обновить эту страницу"||name=="перезагрузить страницу";}
 static Page Inspect(IntPtr window){
  uint pid;GetWindowThreadProcessId(window,out pid);
  try{if(!Browsers.Contains(Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant()))return null;}catch{return null;}
  var page=new Page();
  try{
   var root=AutomationElement.FromHandle(window);
   var toolbarCondition=new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ToolBar);
   var toolbars=root.FindAll(TreeScope.Descendants,toolbarCondition);
   foreach(AutomationElement toolbar in toolbars){
    foreach(AutomationElement e in toolbar.FindAll(TreeScope.Descendants,Condition.TrueCondition)){
     string name=e.Current.Name;object pattern;
     if(AddressName(name+" "+e.Current.AutomationId)&&e.TryGetCurrentPattern(ValuePattern.Pattern,out pattern)){
      string key;if(ChatUrl(((ValuePattern)pattern).Current.Value,out key))page.Key=key;
     }
     if(e.Current.ControlType==ControlType.Button&&ReloadName(name))page.Reload=e;
    }
   }
  }catch(Exception ex){Log("UIA lookup: "+ex.GetType().Name);}
  if(page.Key==null||page.Reload==null){
   try{Guid iid=new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");object obj;
    if(AccessibleObjectFromWindow(window,0xFFFFFFFC,ref iid,out obj)>=0&&obj is IAccessible){int budget=700;Scan((IAccessible)obj,0,0,ref budget,page);}
   }catch(Exception ex){Log("MSAA lookup: "+ex.GetType().Name);}
  }
  return page.Key==null?null:page;
 }
 static void Scan(IAccessible acc,object child,int depth,ref int budget,Page page){
  if(budget--<=0||depth>18)return;
  try{
   int role=Convert.ToInt32(acc.get_accRole(child));string name=acc.get_accName(child)??"";
   if(AddressName(name)){string key;if(ChatUrl(acc.get_accValue(child),out key))page.Key=key;}
   if(role==43&&ReloadName(name)){page.Legacy=acc;page.Child=child;}
   // Do not traverse webpage documents: only browser chrome is relevant.
   if(role==15||!(child is int)||(int)child!=0)return;
   int count=Math.Min(acc.accChildCount,120);if(count<=0)return;
   object[] children=new object[count];int got;
   if(AccessibleChildren(acc,0,count,children,out got)<0)return;
   for(int i=0;i<got&&budget>0;i++){
    if(children[i] is IAccessible)Scan((IAccessible)children[i],0,depth+1,ref budget,page);
    else if(children[i] is int)Scan(acc,children[i],depth+1,ref budget,page);
   }
  }catch{}
 }
 static bool Reload(IntPtr window,Page page){
  if(GetForegroundWindow()!=window)return false;
  try{
   object pattern;
   if(page.Reload!=null&&page.Reload.Current.IsEnabled&&page.Reload.TryGetCurrentPattern(InvokePattern.Pattern,out pattern)){((InvokePattern)pattern).Invoke();Log("Reload button invoked via UI Automation");return true;}
   if(page.Legacy!=null){page.Legacy.accDoDefaultAction(page.Child);Log("Reload button invoked via MSAA");return true;}
  }catch(Exception ex){Log("Reload failed: "+ex.Message);}
  return false;
 }
 static bool Tunnel(){return OwnVpnActive()||ExternalVpnActive();}

 static System.Drawing.Icon BrandIcon(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Brand.ico")){using(var original=new System.Drawing.Icon(stream)){return (System.Drawing.Icon)original.Clone();}}}
 static void SetStatus(string value){if(lastInfo!=value){lastInfo=value;Log(value);}if(status!=null&&!status.IsDisposed)status.Text=lastInfo;}
 static void Tick(object sender,EventArgs e){
  if(showWindowSignal!=null&&showWindowSignal.WaitOne(0))OpenPanel();
  if(busy)return;busy=true;
  try{
   bool up=Tunnel();if(up&&!wasUp){epoch++;ready=DateTime.Now.AddMilliseconds(800);Log("VPN adapter up; refresh queued");}
   if(!up)ready=DateTime.MaxValue;wasUp=up;
   IntPtr window=GetForegroundWindow();Page page=Inspect(window);
   bool app=Process.GetProcessesByName("ChatGPT").Any(p=>p.MainWindowHandle!=IntPtr.Zero&&IsWindowVisible(p.MainWindowHandle));
   if(!up&&(page!=null||app)){RequestAutoHapp();return;}
   if(page!=null&&up&&DateTime.Now>=ready){
    string id=window.ToString()+":"+page.Key;
    long done;if(!refreshed.TryGetValue(id,out done)||done!=epoch){
     if(Reload(window,page)){refreshed[id]=epoch;SetStatus("Команда обновления ChatGPT отправлена");}
     else SetStatus("Кнопка обновления браузера недоступна");
    }
   }
   else SetStatus(up?"VPN подключён · ожидание вкладки ChatGPT":"Ожидание VPN");
   icon.Text=up?"VPNKRUT-SUB · VPN подключён":"VPNKRUT-SUB · ожидание VPN";
  }catch(Exception ex){SetStatus("Ошибка: "+ex.GetType().Name);Log(ex.ToString());}
  finally{busy=false;}
 }
 static void OpenPanel(){ShowDashboard();}
 static int SelfTest(){
  string key;foreach(string s in new[]{"https://chatgpt.com/","chat.openai.com/c/123","https://www.chatgpt.com/c/abc"})if(!ChatUrl(s,out key))return 1;
  foreach(string s in new[]{"https://chatgpt.com.evil.example/","https://example.com/chatgpt.com","file:///chatgpt.com","https://playerok.com/"})if(ChatUrl(s,out key))return 2;
  if(Assembly.GetExecutingAssembly().GetManifestResourceStream("README.md")==null)return 3; using(var brand=BrandIcon()){if(brand.Width<16)return 4;} int admin=TestAdminStore();if(admin!=0)return admin;int catalog=TestPublicCatalog();if(catalog!=0)return catalog;return TestVpnConfig();
 }
 [STAThread] static int Main(string[] args){
  if(args.Contains("--self-test"))return SelfTest();
  if(args.Contains("--diagnose")){var p=Inspect(GetForegroundWindow());File.WriteAllText(Path.Combine(Path.GetTempPath(),"ChatGPTHelper-diagnostic.txt"),"ChatGPT URL recognized="+(p!=null)+"; reload control="+(p!=null&&(p.Reload!=null||p.Legacy!=null)));return 0;}
  Application.EnableVisualStyles(); LoadAutoHapp();
  bool created;mutex=new Mutex(true,"Local\\ChatGPTRefreshExe",out created);if(!created){if(args.Contains("--settings")){try{using(var signal=EventWaitHandle.OpenExisting("Local\\VPNKRUTSUB.ShowWindow")){signal.Set();}}catch{}}mutex.Dispose();return 0;}
  showWindowSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\VPNKRUTSUB.ShowWindow");
  if(!ConfirmAgreement()){mutex.ReleaseMutex();mutex.Dispose();showWindowSignal.Dispose();return 0;}
  icon=new NotifyIcon{Icon=BrandIcon(),Text="VPNKRUT-SUB",Visible=true};
  var menu=new ContextMenuStrip();menu.Items.Add("Открыть окно",null,delegate{OpenPanel();});menu.Items.Add("Инструкция",null,delegate{Process.Start("notepad.exe","\""+ReadmePath()+"\"");});menu.Items.Add("Выход",null,delegate{Application.Exit();});icon.ContextMenuStrip=menu;icon.DoubleClick+=delegate{OpenPanel();};
  adminHotkey=new AdminHotkey();
  var timer=new System.Windows.Forms.Timer{Interval=350};timer.Tick+=Tick;timer.Start();Log("Standalone 3.0 started");
  if(args.Contains("--settings"))OpenPanel();
  try{Application.Run();}finally{if(adminHotkey!=null)adminHotkey.Dispose();timer.Dispose();icon.Dispose();mutex.ReleaseMutex();mutex.Dispose();if(showWindowSignal!=null)showWindowSignal.Dispose();}return 0;
 }
}












