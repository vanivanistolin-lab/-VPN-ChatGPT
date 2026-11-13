using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
partial class Program {
 [StructLayout(LayoutKind.Sequential)] struct SCROLLINFO {public uint cbSize,mask;public int min,max;public uint page;public int pos,track;}
 [DllImport("user32.dll")] static extern bool GetScrollInfo(IntPtr window,int bar,ref SCROLLINFO info);
 static string AgreementText(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("USER_AGREEMENT.md"))using(var reader=new StreamReader(stream,Encoding.UTF8)){return reader.ReadToEnd();}}
 static bool ConfirmAgreement(){
  string text=AgreementText(),digest;
  using(var hash=SHA256.Create()){digest=Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));}
  string acceptance=Path.Combine(Home,"agreement.accepted");
  try{if(File.ReadAllText(acceptance).Trim()==digest)return true;}catch{}
  using(var dialog=new Form{Text="VPNKRUT-SUB · Условия использования",ClientSize=new Size(660,590),StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=UiBack,ForeColor=UiInk,Font=new Font("Segoe UI",10),Icon=BrandIcon()}){
   UiLabel(dialog,"Условия использования",20,16,620,34,18,UiInk);
   UiLabel(dialog,"Прокрутите документ до конца, чтобы открыть галочку принятия.",20,57,620,28,10,UiMuted);
   var document=new RichTextBox{Left=20,Top=94,Width=620,Height=350,ReadOnly=true,Text=text,BackColor=UiCard,ForeColor=UiInk,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",10),ScrollBars=RichTextBoxScrollBars.ForcedVertical,DetectUrls=false};dialog.Controls.Add(document);
   var check=new CheckBox{Left=20,Top=462,Width=620,Height=27,Text="Принимаю условия использования",Enabled=false,ForeColor=UiInk};dialog.Controls.Add(check);
   var hint=UiLabel(dialog,"VPN-ключи при принятии условий не отправляются.",20,494,620,26,9,UiMuted);
   var proceed=new Button{Left=380,Top=535,Width=260,Height=35,Text="Продолжить",Enabled=false,DialogResult=DialogResult.OK,FlatStyle=FlatStyle.Flat,BackColor=UiGreen,ForeColor=UiBack};dialog.Controls.Add(proceed);
   var cancel=new Button{Left=20,Top=535,Width=150,Height=35,Text="Отмена",DialogResult=DialogResult.Cancel,FlatStyle=FlatStyle.Flat,BackColor=UiCard,ForeColor=UiInk};dialog.Controls.Add(cancel);dialog.CancelButton=cancel;
   Action update=delegate{if(check.Enabled)return;var info=new SCROLLINFO{cbSize=(uint)Marshal.SizeOf(typeof(SCROLLINFO)),mask=0x17};if(GetScrollInfo(document.Handle,1,ref info)&&info.pos+(long)info.page>=info.max){check.Enabled=true;hint.Text="Документ прокручен до конца. Решение о принятии — за вами.";}};
   document.VScroll+=delegate{document.BeginInvoke(update);};document.KeyUp+=delegate{update();};document.MouseUp+=delegate{update();};dialog.Shown+=delegate{document.BeginInvoke(update);};check.CheckedChanged+=delegate{proceed.Enabled=check.Enabled&&check.Checked;};
   if(dialog.ShowDialog()!=DialogResult.OK||!check.Enabled||!check.Checked)return false;
  }
  try{Directory.CreateDirectory(Home);File.WriteAllText(acceptance,digest,Encoding.UTF8);}catch(Exception ex){MessageBox.Show("Не удалось сохранить принятие условий: "+ex.Message,"VPNKRUT-SUB");return false;}
  return true;
 }
}
