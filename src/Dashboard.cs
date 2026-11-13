using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Windows.Forms;
partial class Program {
 static readonly Color UiBack=Color.FromArgb(16,22,25), UiCard=Color.FromArgb(26,35,39), UiInk=Color.FromArgb(235,243,237), UiMuted=Color.FromArgb(151,171,163), UiGreen=Color.FromArgb(154,228,183);
 static Label UiLabel(Control host,string text,int x,int y,int w,int h,float size,Color color){var item=new Label{Left=x,Top=y,Width=w,Height=h,Text=text,ForeColor=color,Font=new Font("Segoe UI",size)};host.Controls.Add(item);return item;}
 static Button UiButton(Control host,string text,int x,int y,int w,EventHandler handler,bool primary){var item=new Button{Left=x,Top=y,Width=w,Height=42,Text=text,FlatStyle=FlatStyle.Flat,BackColor=primary?UiGreen:UiCard,ForeColor=primary?UiBack:UiInk,Font=new Font("Segoe UI",10)};item.FlatAppearance.BorderColor=Color.FromArgb(55,76,66);item.Click+=handler;host.Controls.Add(item);return item;}
 static void ShowDashboard(){ShowVpnDashboard();}
}

