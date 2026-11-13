using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
class VpnStatusHost {
 static bool ReadAll(Stream s,byte[] b){int at=0,n;while(at<b.Length){n=s.Read(b,at,b.Length-at);if(n==0)return false;at+=n;}return true;}
 static void Main(){
  var input=Console.OpenStandardInput();var output=Console.OpenStandardOutput();
  try{while(true){
   byte[] size=new byte[4];if(!ReadAll(input,size))break;int len=BitConverter.ToInt32(size,0);if(len<0||len>65536)break;
   byte[] request=new byte[len];if(!ReadAll(input,request))break;
   bool connected=false;
   foreach(var a in NetworkInterface.GetAllNetworkInterfaces())if(a.OperationalStatus==OperationalStatus.Up&&Regex.IsMatch(a.Name+" "+a.Description,"happ|xray",RegexOptions.IgnoreCase)){connected=true;break;}
   byte[] body=Encoding.UTF8.GetBytes(connected?"{\"connected\":true}":"{\"connected\":false}");
   byte[] length=BitConverter.GetBytes(body.Length);output.Write(length,0,4);output.Write(body,0,body.Length);output.Flush();
  }}catch{}
 }
}
