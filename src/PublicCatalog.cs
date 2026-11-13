using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
partial class Program {
 const string CatalogRepo="vanivanistolin-lab/-VPN-ChatGPT";
 const string CatalogAddress="https://raw.githubusercontent.com/vanivanistolin-lab/-VPN-ChatGPT/main/catalog/servers.json";
 static string CatalogCache{get{return Path.Combine(Home,"public-catalog.json");}}
 static List<ServerEntry> LocalServers(){return File.Exists(VaultPath)?DecodeServers(DecryptLocal(File.ReadAllBytes(VaultPath))):new List<ServerEntry>();}
 static string CatalogJson(List<ServerEntry> entries){return Json().Serialize(Obj("format",1,"updatedUtc",DateTime.UtcNow.ToString("o"),"servers",entries.Select(e=>Obj("id",e.Id,"name",e.Name,"key",e.Key)).ToArray()));}
 static List<ServerEntry> ParseCatalog(string text){
  if(text.Length>2097152)throw new InvalidDataException("Каталог слишком большой.");
  var document=Json().Deserialize<Dictionary<string,object>>(text);if(!document.ContainsKey("format")||Convert.ToInt32(document["format"])!=1||!document.ContainsKey("servers"))throw new InvalidDataException("Неверный формат каталога.");
  var array=document["servers"] as System.Collections.ArrayList;var plain=document["servers"] as object[];
  var rows=plain??(array==null?null:array.ToArray());if(rows==null||rows.Length>5000)throw new InvalidDataException("Неверное число серверов.");
  var result=new List<ServerEntry>();var ids=new HashSet<string>();
  foreach(object raw in rows){var row=raw as Dictionary<string,object>;if(row==null||!row.ContainsKey("id")||!row.ContainsKey("name")||!row.ContainsKey("key"))throw new InvalidDataException("Неверная запись каталога.");string id=Convert.ToString(row["id"]),name=Convert.ToString(row["name"]),key=Convert.ToString(row["key"]);if(!ValidServer(name,key)||id.Length>100||!ids.Add(id))throw new InvalidDataException("Неверная или повторяющаяся запись каталога.");result.Add(new ServerEntry(name,key){Id=id});}
  return result;
 }
 static async Task<string> DownloadText(string address){using(var client=new HttpClient()){client.Timeout=TimeSpan.FromSeconds(15);client.DefaultRequestHeaders.UserAgent.ParseAdd("VPNKRUT-SUB/3.0");using(var response=await client.GetAsync(address,HttpCompletionOption.ResponseHeadersRead)){response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>2097152)throw new InvalidDataException("Ответ слишком большой.");using(var stream=await response.Content.ReadAsStreamAsync())using(var memory=new MemoryStream()){byte[] bytes=new byte[8192];int count;while((count=await stream.ReadAsync(bytes,0,bytes.Length))>0){if(memory.Length+count>2097152)throw new InvalidDataException("Ответ слишком большой.");memory.Write(bytes,0,count);}return Encoding.UTF8.GetString(memory.ToArray());}}}}
 static async Task<List<ServerEntry>> FetchCatalog(){string text=await DownloadText(CatalogAddress+"?v="+DateTime.UtcNow.Ticks);var entries=ParseCatalog(text);Directory.CreateDirectory(Home);File.WriteAllText(CatalogCache,text,Encoding.UTF8);return entries;}
 static List<ServerEntry> CachedServers(){try{return ParseCatalog(File.ReadAllText(CatalogCache));}catch{try{return ParseCatalog(File.ReadAllText(Path.Combine(Application.StartupPath,"catalog.json")));}catch{return new List<ServerEntry>();}}}
 static async Task UploadCatalog(string token,string text){
  using(var client=new HttpClient()){
   client.Timeout=TimeSpan.FromSeconds(25);client.DefaultRequestHeaders.UserAgent.ParseAdd("VPNKRUT-SUB/3.0");client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
   using(var who=await client.GetAsync("https://api.github.com/user")){if(!who.IsSuccessStatusCode)throw new InvalidOperationException("GitHub отклонил токен. Нужен токен владельца репозитория.");var profile=Json().Deserialize<Dictionary<string,object>>(await who.Content.ReadAsStringAsync());if(Convert.ToString(profile["login"])!="vanivanistolin-lab")throw new InvalidOperationException("Публикация разрешена только аккаунту vanivanistolin-lab.");}
   string endpoint="https://api.github.com/repos/"+CatalogRepo+"/contents/catalog/servers.json";
   var payload=Obj("message","Update public free VPN server catalog","branch","main","content",Convert.ToBase64String(Encoding.UTF8.GetBytes(text)));
   using(var previous=await client.GetAsync(endpoint+"?ref=main")){if(previous.IsSuccessStatusCode){var old=Json().Deserialize<Dictionary<string,object>>(await previous.Content.ReadAsStringAsync());payload["sha"]=Convert.ToString(old["sha"]);}else if(previous.StatusCode!=System.Net.HttpStatusCode.NotFound)throw new InvalidOperationException("Не удалось проверить каталог GitHub: "+(int)previous.StatusCode);}
   using(var content=new StringContent(Json().Serialize(payload),Encoding.UTF8,"application/json"))using(var response=await client.PutAsync(endpoint,content)){if(!response.IsSuccessStatusCode)throw new InvalidOperationException("GitHub не принял публикацию: HTTP "+(int)response.StatusCode+". Проверьте разрешение Contents: Read and write для этого репозитория.");}
  }
 }
 static void PublishCatalogDialog(List<ServerEntry> entries){
  using(var form=new Form{Text="VPNKRUT-SUB · Публикация каталога",Icon=BrandIcon(),ClientSize=new Size(600,430),StartPosition=FormStartPosition.CenterScreen,BackColor=UiBack,ForeColor=UiInk,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
   UiLabel(form,"Опубликовать бесплатные серверы",22,18,555,40,18,UiInk);
   UiLabel(form,"Будут опубликованы ключи из этого каталога: "+entries.Count+".\nОни станут доступны всем пользователям и посетителям GitHub.\nРепозиторий: "+CatalogRepo,23,72,555,75,10,UiMuted);
   UiLabel(form,"Токен GitHub владельца (Contents: Read and write)",23,166,555,25,10,UiMuted);var token=AdminInput(form,23,200,555,true);
   var remember=new CheckBox{Left=23,Top=242,Width=555,Height=28,Text="Сохранить токен зашифрованным только в этой учётной записи",ForeColor=UiMuted};form.Controls.Add(remember);
   string tokenPath=Path.Combine(Home,"publisher-token.bin");try{if(File.Exists(tokenPath)){token.Text=Encoding.UTF8.GetString(DecryptLocal(File.ReadAllBytes(tokenPath)));remember.Checked=true;}}catch{}
   var feedback=UiLabel(form,"Токен не входит в установщик и не передаётся другим пользователям.",23,284,555,48,9,UiMuted);
   var publish=UiButton(form,"Опубликовать для всех",23,357,555,null,true);
   publish.Click+=async delegate{
    if(String.IsNullOrWhiteSpace(token.Text)){feedback.Text="Введите токен GitHub владельца репозитория.";return;}
    publish.Enabled=false;feedback.Text="Публикация…";
    try{string text=CatalogJson(entries);await UploadCatalog(token.Text.Trim(),text);if(remember.Checked)AtomicProtectedWrite(tokenPath,Encoding.UTF8.GetBytes(token.Text.Trim()));Directory.CreateDirectory(Home);File.WriteAllText(CatalogCache,text,Encoding.UTF8);feedback.Text="Опубликовано. Другие пользователи получат каталог при обновлении списка.";}
    catch(Exception ex){feedback.Text=ex.Message;}finally{if(!form.IsDisposed)publish.Enabled=true;}
   };
   form.FormClosed+=delegate{token.Clear();};form.ShowDialog();
  }
 }
 static int TestPublicCatalog(){var entries=new List<ServerEntry>{new ServerEntry("Example","vless://test@127.0.0.1:443")};var read=ParseCatalog(CatalogJson(entries));if(read.Count!=1||read[0].Key!=entries[0].Key)return 31;try{ParseCatalog("{\"format\":9,\"servers\":[]}");return 32;}catch(InvalidDataException){}return 0;}
}
