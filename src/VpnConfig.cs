using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
partial class Program {
 static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=2097152,RecursionLimit=64};}
 static Dictionary<string,object> Obj(params object[] pairs){var result=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)result[(string)pairs[i]]=pairs[i+1];return result;}
 static string Q(NameValueCollection query,string name,string fallback=""){return query[name]??fallback;}
 static string Unbase64(string text){text=text.Trim().Replace('-','+').Replace('_','/');text=text.PadRight(text.Length+(4-text.Length%4)%4,'=');return Encoding.UTF8.GetString(Convert.FromBase64String(text));}
 static Dictionary<string,object> LinkOutbound(string link){
  string scheme=link.Substring(0,link.IndexOf("://",StringComparison.Ordinal)).ToLowerInvariant();
  if(scheme=="vmess")return VmessOutbound(link);
  if(scheme=="ss")return ShadowsocksOutbound(link);
  if(!new[]{"vless","trojan","hysteria2","hy2","tuic"}.Contains(scheme))throw new InvalidOperationException("Встроенный движок пока не поддерживает этот тип ссылки.");
  var uri=new Uri(link);int port=uri.IsDefaultPort?443:uri.Port;if(port<1||port>65535||String.IsNullOrEmpty(uri.Host))throw new InvalidOperationException("Неверный адрес сервера.");
  var query=HttpUtility.ParseQueryString(uri.Query);string info=Uri.UnescapeDataString(uri.UserInfo);
  var outbound=Obj("type",scheme=="hy2"?"hysteria2":scheme,"tag","proxy","server",uri.Host,"server_port",port);
  if(scheme=="vless"){Guid id;if(!Guid.TryParse(info,out id))throw new InvalidOperationException("VLESS: неверный UUID.");outbound["uuid"]=id.ToString();if(Q(query,"flow")!="")outbound["flow"]=Q(query,"flow");}
  else if(scheme=="tuic"){int colon=info.IndexOf(':');Guid id;if(colon<0||!Guid.TryParse(info.Substring(0,colon),out id))throw new InvalidOperationException("TUIC: нужны UUID и пароль.");outbound["uuid"]=id.ToString();outbound["password"]=info.Substring(colon+1);outbound["congestion_control"]=Q(query,"congestion_control","bbr");}
  else outbound["password"]=info;
  string security=Q(query,"security",scheme=="vless"?"none":"tls");
  if(security=="tls"||security=="reality"||scheme=="trojan"||scheme=="hy2"||scheme=="hysteria2"||scheme=="tuic"){
   if(Q(query,"allowInsecure")=="1"||Q(query,"insecure")=="1")throw new InvalidOperationException("Ссылки с отключённой проверкой TLS не поддерживаются.");
   var tls=Obj("enabled",true,"server_name",Q(query,"sni",Q(query,"peer",uri.Host)));
   if(Q(query,"alpn")!="")tls["alpn"]=Q(query,"alpn").Split(',');
   if(Q(query,"fp")!="")tls["utls"]=Obj("enabled",true,"fingerprint",Q(query,"fp"));
   if(security=="reality"){
    if(Q(query,"pbk")=="")throw new InvalidOperationException("Reality: отсутствует публичный ключ сервера.");
    tls["reality"]=Obj("enabled",true,"public_key",Q(query,"pbk"),"short_id",Q(query,"sid"));
    if(!tls.ContainsKey("utls"))tls["utls"]=Obj("enabled",true,"fingerprint","chrome");
   }
   outbound["tls"]=tls;
  }else if(security!="none"&&security!="")throw new InvalidOperationException("Неподдерживаемый режим безопасности ссылки.");
  if(scheme=="vless"||scheme=="trojan")AddTransport(outbound,Q(query,"type","tcp"),Q(query,"path","/"),Q(query,"host"),Q(query,"serviceName"));
  if((scheme=="hy2"||scheme=="hysteria2")&&Q(query,"obfs")!=""){
   if(Q(query,"obfs")!="salamander")throw new InvalidOperationException("Hysteria2: неизвестный тип obfs.");
   outbound["obfs"]=Obj("type","salamander","password",Q(query,"obfs-password"));
  }
  return outbound;
 }
 static void AddTransport(Dictionary<string,object> outbound,string type,string path,string host,string service){
  if(type=="tcp"||type=="raw"||type=="")return;
  if(type=="ws"){var transport=Obj("type","ws","path",path);if(host!="")transport["headers"]=Obj("Host",host);outbound["transport"]=transport;}
  else if(type=="grpc")outbound["transport"]=Obj("type","grpc","service_name",service);
  else if(type=="httpupgrade")outbound["transport"]=Obj("type","httpupgrade","path",path,"host",host);
  else throw new InvalidOperationException("Транспорт "+type+" пока не поддерживается встроенным движком.");
 }
 static Dictionary<string,object> VmessOutbound(string link){
  var values=Json().Deserialize<Dictionary<string,object>>(Unbase64(link.Substring(8).Split('#')[0]));
  Func<string,string,string> get=(key,fallback)=>values.ContainsKey(key)?Convert.ToString(values[key]):fallback;
  int port;Guid uuid;if(!Int32.TryParse(get("port","443"),out port)||port<1||port>65535||!Guid.TryParse(get("id",""),out uuid)||get("add","")=="")throw new InvalidOperationException("VMess: неверный адрес, порт или UUID.");
  int alter;Int32.TryParse(get("aid","0"),out alter);
  var result=Obj("type","vmess","tag","proxy","server",get("add",""),"server_port",port,"uuid",uuid.ToString(),"security",get("scy","auto"),"alter_id",alter);
  if(get("tls","")=="tls")result["tls"]=Obj("enabled",true,"server_name",get("sni",get("host",get("add",""))));
  AddTransport(result,get("net","tcp"),get("path","/"),get("host",""),get("path",""));return result;
 }
 static Dictionary<string,object> ShadowsocksOutbound(string link){
  string payload=link.Substring(5).Split('#')[0];if(payload.Contains("?")){
   var query=HttpUtility.ParseQueryString(payload.Substring(payload.IndexOf('?')+1));if(query["plugin"]!=null)throw new InvalidOperationException("Shadowsocks-плагины пока не поддерживаются.");payload=payload.Split('?')[0];
  }
  if(!payload.Contains("@"))payload=Unbase64(payload);
  var uri=new Uri("ss://"+payload);string credentials=Uri.UnescapeDataString(uri.UserInfo);if(!credentials.Contains(":"))credentials=Unbase64(credentials);
  int colon=credentials.IndexOf(':');if(colon<=0||uri.Port<1)throw new InvalidOperationException("Неверная ссылка Shadowsocks.");
  return Obj("type","shadowsocks","tag","proxy","server",uri.Host,"server_port",uri.Port,"method",credentials.Substring(0,colon),"password",credentials.Substring(colon+1));
 }
 static string EngineConfig(string link){
  var outbound=LinkOutbound(link);
  var config=Obj("log",Obj("level","warn","timestamp",true),
   "dns",Obj("servers",new object[]{Obj("type","udp","tag","bootstrap","server","1.1.1.1","detour","direct"),Obj("type","udp","tag","remote","server","1.1.1.1","detour","proxy")},"final","remote","strategy","prefer_ipv4"),
   "inbounds",new object[]{Obj("type","tun","tag","tun-in","interface_name","VPNKRUT-SUB","address",new[]{"172.29.253.1/30","fdfe:dcba:9876::1/126"},"auto_route",true,"strict_route",false,"stack","mixed","dns_mode","hijack")},
   "outbounds",new object[]{outbound,Obj("type","direct","tag","direct")},
   "route",Obj("auto_detect_interface",true,"default_domain_resolver","bootstrap","final","proxy","rules",new object[]{Obj("protocol","dns","action","hijack-dns"),Obj("ip_is_private",true,"action","route","outbound","direct")}));
  return Json().Serialize(config);
 }
 static int TestVpnConfig(){
  string uuid="bf000d23-0752-40b4-affe-68f7707a9661";
  var output=LinkOutbound("vless://"+uuid+"@127.0.0.1:443?security=tls&type=ws&path=%2Fws&sni=example.test");
  if((string)output["type"]!="vless"||(int)output["server_port"]!=443||!output.ContainsKey("transport"))return 21;
  try{LinkOutbound("vless://"+uuid+"@127.0.0.1:443?security=tls&allowInsecure=1");return 22;}catch(InvalidOperationException){}
  try{LinkOutbound("vless://"+uuid+"@127.0.0.1:443?type=unknown");return 23;}catch(InvalidOperationException){}
  return 0;
 }
}
