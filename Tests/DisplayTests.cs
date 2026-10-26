using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ScreenLite;
class DisplayTests {
 static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
 static void ExpectFailure(Action action){try{action();}catch{return;}throw new Exception("Expected failure");}
 static DisplayApi.DevMode Mode(uint width,uint hz){var m=DisplayApi.NewMode();m.Width=width;m.Height=1080;m.Frequency=hz;m.Bits=32;return m;}
 [STAThread] static void Main(){
 var old=Mode(1920,60);var next=Mode(2560,120);var calls=new List<uint>();ModePreview.Change ok=(device,mode,flags)=>{calls.Add(flags);return 0;};
 using(var p=new ModePreview("test",old,ok)){p.Apply(next);}Assert(string.Join(",",calls)=="2,0,1","Cancel should test, preview, restore");
 calls.Clear();using(var p=new ModePreview("test",old,ok)){p.Apply(next);p.Commit(next);}Assert(string.Join(",",calls)=="2,0,268435457","Confirmed mode should persist without resetting twice");
 calls.Clear();using(var p=new ModePreview("test",old,(device,mode,flags)=>{calls.Add(flags);return flags==2?-2:0;})){ExpectFailure(()=>p.Apply(next));}Assert(calls.Count==1,"Test failure must not change actual mode");
 calls.Clear();using(var p=new ModePreview("test",old,(device,mode,flags)=>{calls.Add(flags);return flags==0?-1:0;})){ExpectFailure(()=>p.Apply(next));}Assert(string.Join(",",calls)=="2,0,1","Apply failure must restore");
 calls.Clear();using(var p=new ModePreview("test",old,(device,mode,flags)=>{calls.Add(flags);return flags==0x10000001?-3:0;})){p.Apply(next);ExpectFailure(()=>p.Commit(next));}Assert(calls[calls.Count-1]==1,"Persistence failure must restore");
 var w=new SystemDisplay{DeviceName="test",DevicePath="unique",Name="test",HasMode=true,Current=old,HdrKnown=true,HdrSupported=true,HdrStatus="HDR 已关闭"};w.Modes.Add(new ScreenMode{Raw=old});w.Modes.Add(new ScreenMode{Raw=Mode(1920,144)});w.Modes.Add(new ScreenMode{Raw=Mode(2560,120)});
 var display=new Display{Name="test",Windows=w};using(var panel=new DisplaySettingsPanel()){panel.Bind(display,false);var f=BindingFlags.Instance|BindingFlags.NonPublic;var r=(ComboBox)typeof(DisplaySettingsPanel).GetField("resolution",f).GetValue(panel);var hz=(ComboBox)typeof(DisplaySettingsPanel).GetField("rate",f).GetValue(panel);Assert(hz.Items.Count==2,"Refresh rates must match chosen resolution");Assert(((ScreenMode)hz.SelectedItem).Raw.Frequency==60,"Keep current refresh if supported");r.SelectedItem="2560 × 1080";Assert(hz.Items.Count==1&&((ScreenMode)hz.SelectedItem).Raw.Frequency==120,"Resolution / refresh pairing");w.Clone=true;panel.Bind(display,false);Assert(!r.Enabled&&!hz.Enabled,"Clone control must disable");panel.Bind(null,false);Assert(!r.Enabled&&r.Items.Count==0,"No-device state");}
 using(var quick=new QuickPanel()){int combos=0,buttons=0;foreach(Control c in quick.Controls){if(c is ComboBox)combos++;if(c is Button)buttons++;}Assert(combos==0&&buttons==1,"Quick panel only has levels plus settings link");}
 using(var confirm=new KeepDisplayDialog()){confirm.Show();Application.DoEvents();typeof(KeepDisplayDialog).GetField("deadline",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(confirm,DateTime.UtcNow.AddSeconds(-1));typeof(KeepDisplayDialog).GetMethod("UpdateCountdown",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(confirm,null);Assert(confirm.DialogResult==DialogResult.Cancel&&!confirm.Visible,"Timeout must reject new settings");}
 Console.WriteLine("PASS: temporary preview, persistence, test rejection, apply/persist failure rollback, mode pairing, clone/empty states, compact panel, confirmation timeout. No real display changes.");
 }
}

