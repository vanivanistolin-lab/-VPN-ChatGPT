using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
partial class Program {
 static bool ExternalVpnActive(){return NetworkInterface.GetAllNetworkInterfaces().Any(a=>a.OperationalStatus==OperationalStatus.Up&&a.Name!="VPNKRUT-SUB"&&new[]{"happ","xray","incy","v2ray","sing-box","singbox","wireguard","wintun"}.Any(part=>(a.Name+" "+a.Description).ToLowerInvariant().Contains(part)));}
 static bool OwnVpnActive(){return NetworkInterface.GetAllNetworkInterfaces().Any(a=>a.Name=="VPNKRUT-SUB"&&a.OperationalStatus==OperationalStatus.Up);}
 static async Task<string> ResolveKey(string key,IWin32Window owner){
  if(!key.StartsWith("https://",StringComparison.OrdinalIgnoreCase)&&!key.StartsWith("http://",StringComparison.OrdinalIgnoreCase))return key;
  if(!key.StartsWith("https://",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Подписка должна использовать HTTPS.");
  string text=(await DownloadText(key)).Trim();if(!text.Contains("://")){try{text=Unbase64(text);}catch{throw new InvalidOperationException("Не удалось прочитать подписку. Поддерживаются списки VPN-ссылок, в том числе base64.");}}
  string[] keys=text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Contains("://")&&!s.StartsWith("http",StringComparison.OrdinalIgnoreCase)).Take(1000).ToArray();
  if(keys.Length==0)throw new InvalidOperationException("В подписке нет VPN-ссылок поддерживаемого формата.");
  if(keys.Length==1)return keys[0];
  using(var dialog=new Form{Text="Выберите сервер из подписки",ClientSize=new Size(590,360),StartPosition=FormStartPosition.CenterParent,BackColor=UiBack,ForeColor=UiInk,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
   var list=new ListBox{Left=20,Top=20,Width=550,Height=265,BackColor=UiCard,ForeColor=UiInk,HorizontalScrollbar=true};
   for(int i=0;i<keys.Length;i++){int hash=keys[i].IndexOf('#');string name=hash>=0?Uri.UnescapeDataString(keys[i].Substring(hash+1)):"Сервер "+(i+1);list.Items.Add(name);}list.SelectedIndex=0;dialog.Controls.Add(list);
   var select=UiButton(dialog,"Выбрать",20,305,550,delegate{dialog.DialogResult=DialogResult.OK;dialog.Close();},true);dialog.AcceptButton=select;
   if(dialog.ShowDialog(owner)!=DialogResult.OK)throw new OperationCanceledException();return keys[list.SelectedIndex];
  }
 }
 static async Task ConnectServer(ServerEntry server,IWin32Window owner){
  if(OwnVpnActive()){MessageBox.Show(owner,"Встроенный VPN уже подключён. Управление им доступно через отдельный значок VPNKRUT-SUB возле часов.","VPNKRUT-SUB");return;}
  if(ExternalVpnActive()){MessageBox.Show(owner,"Обнаружен другой работающий VPN. Он оставлен включённым. Чтобы выбрать новый сервер, сначала самостоятельно завершите другое соединение.","VPNKRUT-SUB");return;}
  string core=Path.Combine(Application.StartupPath,"engine","sing-box.exe"),host=Path.Combine(Application.StartupPath,"VPNKRUTEngine.exe");
  if(!File.Exists(core)||!File.Exists(host))throw new InvalidOperationException("Встроенный движок отсутствует. Переустановите программу из полного MSI.");
  string key=await ResolveKey(server.Key,owner);string configuration=EngineConfig(key);
  string directory=Path.Combine(Home,"sessions");Directory.CreateDirectory(directory);
  var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);
  foreach(var sid in new[]{WindowsIdentity.GetCurrent().User,new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null)})security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));Directory.SetAccessControl(directory,security);
  string path=Path.Combine(directory,Guid.NewGuid().ToString("N")+".json");File.WriteAllText(path,configuration,new UTF8Encoding(false));
  bool valid=await Task.Run(delegate{using(var process=Process.Start(new ProcessStartInfo(core,"check -c \""+path+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(core)})){var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(15000)){process.Kill();return false;}Task.WaitAll(stdout,stderr);return process.ExitCode==0;}});
  if(!valid){File.Delete(path);throw new InvalidOperationException("Движок не принял параметры сервера. Этот вариант ссылки пока не поддерживается; VPN не запускался.");}
  if(ExternalVpnActive()){File.Delete(path);throw new InvalidOperationException("За время проверки появился другой VPN. Новое соединение не запущено.");}
  try{Process.Start(new ProcessStartInfo(host,"\""+path+"\""){UseShellExecute=true,Verb="runas",WorkingDirectory=Application.StartupPath});SetStatus("Ожидание подключения выбранного сервера и подтверждения Windows");}
  catch{File.Delete(path);throw;}
 }
 static void ShowVpnDashboard(){
  if(panel!=null&&!panel.IsDisposed){panel.Show();panel.Activate();return;}
  panel=new Form{Text="VPNKRUT-SUB",Icon=BrandIcon(),ClientSize=new Size(720,700),BackColor=UiBack,ForeColor=UiInk,StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedSingle,MaximizeBox=false,Font=new Font("Segoe UI",10)};
  UiLabel(panel,"VPNKRUT-SUB",24,22,490,38,24,UiInk);UiLabel(panel,"СВОЙ VPN-КЛИЕНТ  /  3.0",26,69,490,23,9,UiMuted);
  UiButton(panel,"Админ · Alt+A",545,34,151,delegate{ShowAdminLogin();},false);
  var box=new Panel{Left=24,Top=112,Width=672,Height=88,BackColor=UiCard};panel.Controls.Add(box);UiLabel(box,"СОСТОЯНИЕ",17,13,630,19,8,UiMuted);status=UiLabel(box,lastInfo,17,40,630,37,12,UiInk);
  UiLabel(panel,"Бесплатные серверы",24,229,460,30,17,UiInk);
  var update=UiButton(panel,"Обновить список",530,220,166,null,false);
  var grid=new DataGridView{Left=24,Top=279,Width=672,Height=210,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=UiCard,BorderStyle=BorderStyle.None,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,EnableHeadersVisualStyles=false,GridColor=Color.FromArgb(46,63,55)};
  grid.DefaultCellStyle.BackColor=UiCard;grid.DefaultCellStyle.ForeColor=UiInk;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(48,78,62);grid.DefaultCellStyle.SelectionForeColor=UiInk;grid.ColumnHeadersDefaultCellStyle.BackColor=UiBack;grid.ColumnHeadersDefaultCellStyle.ForeColor=UiMuted;grid.RowTemplate.Height=35;grid.Columns.Add("Name","Сервер");grid.Columns.Add("Protocol","Тип");grid.Columns[0].FillWeight=75;grid.Columns[1].FillWeight=25;panel.Controls.Add(grid);
  var hint=UiLabel(panel,"",24,546,672,50,9,UiMuted);List<ServerEntry> entries=CachedServers();
  Action fill=delegate{grid.Rows.Clear();foreach(var entry in entries){int row=grid.Rows.Add(entry.Name,entry.Key.Substring(0,entry.Key.IndexOf("://",StringComparison.Ordinal)).ToUpperInvariant());grid.Rows[row].Tag=entry;}if(grid.Rows.Count>0)grid.Rows[0].Selected=true;};fill();
  var connect=UiButton(panel,"Подключить выбранный сервер",24,504,672,null,true);
  connect.Click+=async delegate{if(grid.SelectedRows.Count==0){hint.Text="Выберите сервер в списке.";return;}connect.Enabled=false;hint.Text="Проверка параметров сервера…";try{await ConnectServer((ServerEntry)grid.SelectedRows[0].Tag,panel);hint.Text="Если Windows запросит разрешение, подтвердите его вручную. Другие VPN не отключаются.";}catch(OperationCanceledException){hint.Text="Выбор отменён.";}catch(Exception ex){hint.Text=ex is System.ComponentModel.Win32Exception?"Запуск отменён или не разрешён Windows.":ex.Message;}finally{if(!panel.IsDisposed)connect.Enabled=true;}};
  update.Click+=async delegate{update.Enabled=false;hint.Text="Загрузка общего каталога с GitHub…";try{entries=await FetchCatalog();if(panel.IsDisposed)return;fill();hint.Text=entries.Count==0?"Каталог пока пуст. Администратору нужно добавить и опубликовать серверы.":"Каталог обновлён. Серверов: "+entries.Count;}catch{if(!panel.IsDisposed)hint.Text=entries.Count>0?"Сеть недоступна. Показан последний сохранённый каталог.":"Не удалось загрузить каталог. Проверьте интернет или публикацию серверов.";}finally{if(!panel.IsDisposed)update.Enabled=true;}};
  UiButton(panel,"Открыть ChatGPT",24,620,328,delegate{Process.Start(new ProcessStartInfo("https://chatgpt.com/"){UseShellExecute=true});},true);
  UiButton(panel,"Инструкция",368,620,328,delegate{Process.Start("notepad.exe","\""+ReadmePath()+"\"");},false);
  UiLabel(panel,"Список общий для пользователей. Подключение — только после вашего нажатия.",24,674,672,24,9,UiMuted);
  var refreshTimer=new System.Windows.Forms.Timer{Interval=60000};refreshTimer.Tick+=delegate{if(!update.IsDisposed&&update.Enabled)update.PerformClick();};
  panel.Shown+=delegate{update.PerformClick();refreshTimer.Start();};panel.FormClosed+=delegate{refreshTimer.Dispose();};panel.Show();
 }
}
