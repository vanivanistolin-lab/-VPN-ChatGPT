using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
partial class Program {
 static AdminHotkey adminHotkey; static Form adminWindow; static DateTime nextLogin=DateTime.MinValue;
 class ServerEntry {public string Id,Name,Key; public ServerEntry(string name,string key){Id=Guid.NewGuid().ToString("N");Name=name;Key=key;}}
 class AdminHotkey:NativeWindow,IDisposable {
  [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
  [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
  public AdminHotkey(){CreateHandle(new CreateParams{Caption="VPNKRUT-SUB hotkey",Parent=new IntPtr(-3)});if(!RegisterHotKey(Handle,7642,0x4001,0x41))Log("Alt+A unavailable; use the Admin button");}
  protected override void WndProc(ref Message m){if(m.Msg==0x312&&m.WParam.ToInt32()==7642){ShowAdminLogin();return;}base.WndProc(ref m);}
  public void Dispose(){UnregisterHotKey(Handle,7642);DestroyHandle();}
 }
 static string VaultPath{get{return Path.Combine(Home,"free-servers.bin");}}
 static byte[] EncryptLocal(byte[] bytes){return ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser);}
 static byte[] DecryptLocal(byte[] bytes){return ProtectedData.Unprotect(bytes,null,DataProtectionScope.CurrentUser);}
 static void AtomicProtectedWrite(string path,byte[] clear){Directory.CreateDirectory(Home);string temp=path+".new";byte[] encrypted=EncryptLocal(clear);File.WriteAllBytes(temp,encrypted);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
 static byte[] EncodeServers(List<ServerEntry> entries){using(var memory=new MemoryStream()){using(var writer=new BinaryWriter(memory,Encoding.UTF8,true)){writer.Write("VPNKRUT-SERVERS-1");writer.Write(entries.Count);foreach(var e in entries){writer.Write(e.Id);writer.Write(e.Name);writer.Write(e.Key);}}return memory.ToArray();}}
 static List<ServerEntry> DecodeServers(byte[] bytes){using(var memory=new MemoryStream(bytes))using(var reader=new BinaryReader(memory,Encoding.UTF8)){if(reader.ReadString()!="VPNKRUT-SERVERS-1")throw new InvalidDataException("Неверный формат хранилища.");int count=reader.ReadInt32();if(count<0||count>5000)throw new InvalidDataException("Неверное число записей.");var result=new List<ServerEntry>();for(int i=0;i<count;i++){string id=reader.ReadString(),name=reader.ReadString(),key=reader.ReadString();if(name.Length>100||key.Length>16384)throw new InvalidDataException("Повреждённая запись.");result.Add(new ServerEntry(name,key){Id=id});}return result;}}
 static bool ValidServer(string name,string key){if(String.IsNullOrWhiteSpace(name)||name.Length>100||String.IsNullOrWhiteSpace(key)||key.Length>16384||key.Contains("\n")||key.Contains("\r"))return false;int colon=key.IndexOf("://",StringComparison.Ordinal);if(colon<=0)return false;return new[]{"vless","vmess","trojan","ss","ssr","hysteria2","hy2","tuic","wireguard","https","http"}.Contains(key.Substring(0,colon).ToLowerInvariant());}
 static bool CheckAdmin(string user,string password){
  if(user.Trim()!="vanivani2016")return false;
  byte[] salt=Convert.FromBase64String("G91YbzQQvc2zniaxgpMHFaWYdB5UbujH");
  byte[] expected=Convert.FromBase64String("Ipvu3FgKVxWJO9aHAyB/Ha9cFVU4mPfZ4vje4ia/BYA=");
  using(var derive=new Rfc2898DeriveBytes(password,salt,150000)){byte[] actual=derive.GetBytes(32);int difference=0;for(int i=0;i<actual.Length;i++)difference|=actual[i]^expected[i];Array.Clear(actual,0,actual.Length);return difference==0;}
 }
 static TextBox AdminInput(Control host,int x,int y,int width,bool secret){var field=new TextBox{Left=x,Top=y,Width=width,BackColor=UiCard,ForeColor=UiInk,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",11),UseSystemPasswordChar=secret};host.Controls.Add(field);return field;}
 static bool loggingIn;
 static void ShowAdminLogin(){
  if(adminWindow!=null&&!adminWindow.IsDisposed){adminWindow.Show();adminWindow.Activate();return;}
  if(loggingIn)return;loggingIn=true;
  try{
   using(var dialog=new Form{Text="VPNKRUT-SUB · Вход администратора",Icon=BrandIcon(),ClientSize=new Size(410,340),StartPosition=FormStartPosition.CenterScreen,BackColor=UiBack,ForeColor=UiInk,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
    UiLabel(dialog,"Вход администратора",22,20,366,34,18,UiInk);
    UiLabel(dialog,"Управление вручную добавленными серверами",23,62,366,26,9,UiMuted);
    UiLabel(dialog,"Логин",23,104,365,20,10,UiMuted);var username=AdminInput(dialog,23,130,365,false);
    UiLabel(dialog,"Пароль",23,176,365,20,10,UiMuted);var password=AdminInput(dialog,23,202,365,true);
    var error=UiLabel(dialog,"",23,242,365,25,9,Color.Salmon);
    var enter=UiButton(dialog,"Войти",23,281,365,delegate{if(DateTime.Now<nextLogin){error.Text="Подождите несколько секунд перед повторной попыткой.";return;}if(CheckAdmin(username.Text,password.Text)){password.Clear();dialog.DialogResult=DialogResult.OK;dialog.Close();}else{nextLogin=DateTime.Now.AddSeconds(2);password.Clear();error.Text="Неверный логин или пароль.";}},true);dialog.AcceptButton=enter;
    if(dialog.ShowDialog()!=DialogResult.OK)return;
   }
   ShowServerManager();
  }finally{loggingIn=false;}
 }
 static void ShowServerManager(){
  List<ServerEntry> entries;
  try{entries=File.Exists(VaultPath)?DecodeServers(DecryptLocal(File.ReadAllBytes(VaultPath))):new List<ServerEntry>();}
  catch{MessageBox.Show("Не удалось открыть хранилище. Оно не будет перезаписано. Данные доступны только в исходной учётной записи Windows.","VPNKRUT-SUB",MessageBoxButtons.OK,MessageBoxIcon.Error);return;}
  adminWindow=new Form{Text="VPNKRUT-SUB · Бесплатные серверы",Icon=BrandIcon(),ClientSize=new Size(920,665),BackColor=UiBack,ForeColor=UiInk,Font=new Font("Segoe UI",10),StartPosition=FormStartPosition.CenterScreen,MinimumSize=new Size(936,704),MaximumSize=new Size(936,704),MaximizeBox=false};
  UiLabel(adminWindow,"Бесплатные серверы",22,20,650,36,21,UiInk);
  UiLabel(adminWindow,"Добавьте свои серверы и опубликуйте общий каталог",24,63,650,23,10,UiMuted);
  var grid=new DataGridView{Left=24,Top=110,Width=425,Height=414,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=UiCard,BorderStyle=BorderStyle.None,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,EnableHeadersVisualStyles=false,GridColor=Color.FromArgb(46,63,55)};
  grid.DefaultCellStyle.BackColor=UiCard;grid.DefaultCellStyle.ForeColor=UiInk;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(48,78,62);grid.DefaultCellStyle.SelectionForeColor=UiInk;grid.ColumnHeadersDefaultCellStyle.BackColor=UiBack;grid.ColumnHeadersDefaultCellStyle.ForeColor=UiMuted;grid.RowTemplate.Height=35;
  grid.Columns.Add("Server","Сервер");grid.Columns.Add("Protocol","Тип");grid.Columns[0].FillWeight=72;grid.Columns[1].FillWeight=28;adminWindow.Controls.Add(grid);
  UiLabel(adminWindow,"НАЗВАНИЕ СЕРВЕРА",478,114,415,23,9,UiMuted);var name=AdminInput(adminWindow,478,145,415,false);name.MaxLength=100;
  UiLabel(adminWindow,"VPN-КЛЮЧ ИЛИ ССЫЛКА ПОДПИСКИ",478,201,415,23,9,UiMuted);var key=AdminInput(adminWindow,478,232,415,true);key.MaxLength=16384;
  var show=new CheckBox{Left=478,Top=274,Width=415,Height=27,Text="Показать ключ",ForeColor=UiMuted};show.CheckedChanged+=delegate{key.UseSystemPasswordChar=!show.Checked;};adminWindow.Controls.Add(show);
  UiLabel(adminWindow,"Поддерживаются vless://, vmess://, trojan://, ss://\nи ссылки подписок https://. Один ключ — одна запись.",478,317,415,49,9,UiMuted);
  var feedback=UiLabel(adminWindow,"",478,466,415,58,10,UiMuted);
  string selected=null;bool filling=false;
  Action fill=delegate{filling=true;grid.Rows.Clear();foreach(var item in entries){string kind=item.Key.Substring(0,item.Key.IndexOf("://",StringComparison.Ordinal));int index=grid.Rows.Add(item.Name,kind.ToUpperInvariant());grid.Rows[index].Tag=item.Id;}grid.ClearSelection();filling=false;};
  Action reset=delegate{selected=null;name.Clear();key.Clear();show.Checked=false;grid.ClearSelection();feedback.Text="Новая запись. Укажите название и вставьте ключ.";};
  grid.SelectionChanged+=delegate{if(filling||grid.SelectedRows.Count==0)return;string id=grid.SelectedRows[0].Tag as string;var entry=entries.FirstOrDefault(e=>e.Id==id);if(entry==null)return;selected=entry.Id;name.Text=entry.Name;key.Text=entry.Key;show.Checked=false;feedback.Text="Редактирование выбранного сервера.";};
  UiButton(adminWindow,"Сохранить сервер",478,383,415,delegate{
   string serverName=name.Text.Trim(),value=key.Text.Trim();if(!ValidServer(serverName,value)){feedback.Text="Нужно название до 100 символов и корректная ссылка VPN без переносов строк.";return;}
   if(entries.Any(e=>e.Key==value&&e.Id!=selected)){feedback.Text="Этот ключ уже добавлен в каталог.";return;}
   var next=entries.Select(e=>new ServerEntry(e.Name,e.Key){Id=e.Id}).ToList();var existing=next.FirstOrDefault(e=>e.Id==selected);
   if(existing==null)next.Add(new ServerEntry(serverName,value));else{existing.Name=serverName;existing.Key=value;}
   try{AtomicProtectedWrite(VaultPath,EncodeServers(next));entries=next;fill();reset();feedback.Text="Сервер сохранён на этом компьютере.";}catch{feedback.Text="Не удалось сохранить. Прежний каталог не изменён.";}
  },true);
  UiButton(adminWindow,"Новый",24,541,132,delegate{reset();name.Focus();},false);
  UiButton(adminWindow,"Копировать",169,541,135,delegate{var item=entries.FirstOrDefault(e=>e.Id==selected);if(item==null){feedback.Text="Сначала выберите сервер.";return;}try{Clipboard.SetText(item.Key);feedback.Text="Ключ скопирован в буфер обмена.";}catch{feedback.Text="Буфер обмена временно недоступен.";}},false);
  UiButton(adminWindow,"Удалить",317,541,132,delegate{
   var item=entries.FirstOrDefault(e=>e.Id==selected);if(item==null){feedback.Text="Сначала выберите сервер.";return;}
   if(MessageBox.Show(adminWindow,"Удалить сервер «"+item.Name+"» из локального каталога?","Удаление сервера",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   var next=entries.Where(e=>e.Id!=selected).ToList();try{AtomicProtectedWrite(VaultPath,EncodeServers(next));entries=next;fill();reset();feedback.Text="Сервер удалён из каталога.";}catch{feedback.Text="Удаление не сохранено. Прежний каталог не изменён.";}
  },false);
  UiButton(adminWindow,"Опубликовать для всех",478,541,415,delegate{PublishCatalogDialog(entries);},true);
  UiLabel(adminWindow,"Локальные ключи не отправляются до нажатия Опубликовать. Для GitHub нужен токен владельца.",24,610,869,36,9,UiMuted);
  adminWindow.FormClosed+=delegate{key.Clear();foreach(var entry in entries)entry.Key="";entries.Clear();};fill();reset();adminWindow.Show();
 }
 static int TestAdminStore(){
  if(!ValidServer("Example","vless://test@127.0.0.1:443")||ValidServer("","vless://test")||ValidServer("Example","plain text")||ValidServer("Example","https://example.test\nsecond"))return 11;
  var original=new List<ServerEntry>{new ServerEntry("Example","vless://test@127.0.0.1:443")};var decoded=DecodeServers(DecryptLocal(EncryptLocal(EncodeServers(original))));
  return decoded.Count==1&&decoded[0].Id==original[0].Id&&decoded[0].Name==original[0].Name&&decoded[0].Key==original[0].Key?0:12;
 }
}

