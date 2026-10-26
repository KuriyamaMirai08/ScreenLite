using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenLite {
static class Native {
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct Physical { public IntPtr Handle; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Name; }
 public delegate bool MonitorProc(IntPtr h,IntPtr dc,IntPtr rect,IntPtr data);
 [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr rect,MonitorProc callback,IntPtr data);
 [DllImport("dxva2.dll",SetLastError=true)] public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr h,out uint n);
 [DllImport("dxva2.dll",SetLastError=true)] public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr h,uint n,[Out] Physical[] p);
 [DllImport("dxva2.dll")] public static extern bool DestroyPhysicalMonitor(IntPtr h);
 [DllImport("dxva2.dll",SetLastError=true)] public static extern bool GetMonitorBrightness(IntPtr h,out uint min,out uint cur,out uint max);
 [DllImport("dxva2.dll",SetLastError=true)] public static extern bool GetMonitorContrast(IntPtr h,out uint min,out uint cur,out uint max);
 [DllImport("dxva2.dll",SetLastError=true)] public static extern bool SetMonitorBrightness(IntPtr h,uint v);
 [DllImport("dxva2.dll",SetLastError=true)] public static extern bool SetMonitorContrast(IntPtr h,uint v);
 [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
 [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
class Display : IDisposable {
 public string Name, WmiPath; public SystemDisplay Windows; public IntPtr Handle; public bool Bright, Contrast; public uint BMin,BMax,CMin,CMax; public int B,C;
 public override string ToString(){return Name;}
 public void Dispose(){if(Handle!=IntPtr.Zero){Native.DestroyPhysicalMonitor(Handle);Handle=IntPtr.Zero;}}
 public void Set(bool brightness,int percent){
  if(brightness ? !Bright : !Contrast) return;
  if(WmiPath!=null&&brightness){using(var m=new ManagementObject(WmiPath)){object result=m.InvokeMethod("WmiSetBrightness",new object[]{(uint)0,(byte)percent}); if(result!=null && Convert.ToUInt32(result)!=0) throw new Exception("WMI 拒绝设置："+result);}}
  else {uint lo=brightness?BMin:CMin, hi=brightness?BMax:CMax; uint value=lo+(uint)Math.Round((hi-lo)*percent/100.0); bool ok=brightness?Native.SetMonitorBrightness(Handle,value):Native.SetMonitorContrast(Handle,value);if(!ok)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
  if(brightness)B=percent;else C=percent;
 }
 static string Identity(string value){if(string.IsNullOrEmpty(value))return "";value=value.Replace("#","\\").TrimStart('\\','?');int end=value.IndexOf("\\{");if(end>=0)value=value.Substring(0,end);return System.Text.RegularExpressions.Regex.Replace(value,@"_\d+$","").ToUpperInvariant();}
 static string Decode(object value){var array=value as ushort[];if(array==null)return "";return new string(Array.ConvertAll(array,x=>(char)x)).Trim('\0',' ');}
 static void ReadDdc(Display d,Native.Physical p){d.Handle=p.Handle;uint cur;d.Bright=Native.GetMonitorBrightness(p.Handle,out d.BMin,out cur,out d.BMax)&&d.BMax>d.BMin;if(d.Bright)d.B=(int)Math.Round((cur-d.BMin)*100.0/(d.BMax-d.BMin));d.Contrast=Native.GetMonitorContrast(p.Handle,out d.CMin,out cur,out d.CMax)&&d.CMax>d.CMin;if(d.Contrast)d.C=(int)Math.Round((cur-d.CMin)*100.0/(d.CMax-d.CMin));}
 public static List<Display> Scan(){
  var list=new List<Display>();ScanWarning="";
  try{
   try{foreach(var system in SystemDisplay.Read())list.Add(new Display{Name=system.Name+" · "+system.DeviceName.Replace(@"\\.\DISPLAY","屏幕 "),Windows=system});}catch(Exception ex){ScanWarning="Windows 显示配置读取失败："+ex.Message;}
   Native.EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,delegate(IntPtr h,IntPtr dc,IntPtr rect,IntPtr data){
    var mi=new DisplayApi.MonitorInfo{Size=Marshal.SizeOf(typeof(DisplayApi.MonitorInfo))};DisplayApi.GetMonitorInfo(h,ref mi);
    var candidates=list.FindAll(d=>d.Windows!=null&&string.Equals(d.Windows.DeviceName,mi.Device,StringComparison.OrdinalIgnoreCase));uint n;
    if(Native.GetNumberOfPhysicalMonitorsFromHMONITOR(h,out n)&&n>0){var ps=new Native.Physical[n];if(Native.GetPhysicalMonitorsFromHMONITOR(h,n,ps))foreach(var p in ps){Display d;if(n==1&&candidates.Count==1&&candidates[0].Handle==IntPtr.Zero)d=candidates[0];else{d=new Display{Name=p.Name+" [DDC/CI · 独立硬件接口]"};list.Add(d);}ReadDdc(d,p);}}return true;
   },IntPtr.Zero);
   try{
    var methods=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    using(var search=new ManagementObjectSearcher("root\\wmi","SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=True"))using(var rows=search.Get())foreach(ManagementObject row in rows)using(row)methods[(string)row["InstanceName"]]=row.Path.Path;
    using(var search=new ManagementObjectSearcher("root\\wmi","SELECT * FROM WmiMonitorBrightness WHERE Active=True"))using(var rows=search.Get())foreach(ManagementObject row in rows)using(row){string instance=(string)row["InstanceName"];string path;if(!methods.TryGetValue(instance,out path))continue;var matches=list.FindAll(item=>item.Windows!=null&&Identity(item.Windows.DevicePath)==Identity(instance));Display d;if(matches.Count==1)d=matches[0];else{d=new Display{Name="内屏 / WMI："+instance};list.Add(d);}if(!d.Bright){d.WmiPath=path;d.Bright=true;d.B=Convert.ToInt32(row["CurrentBrightness"]);}}
   }catch(Exception ex){ScanWarning+=(ScanWarning.Length>0?"\n":"")+"WMI 亮度检测不可用："+ex.Message;}
   try{using(var search=new ManagementObjectSearcher("root\\wmi","SELECT * FROM WmiMonitorID WHERE Active=True"))using(var rows=search.Get())foreach(ManagementObject row in rows)using(row){string id=Identity((string)row["InstanceName"]);foreach(var d in list)if(d.Windows!=null&&Identity(d.Windows.DevicePath)==id){string serial=Decode(row["SerialNumberID"]);d.Windows.IdentityInfo="制造商："+Decode(row["ManufacturerName"])+(serial.Length>0?" · 序列号："+serial:"")+" · "+row["YearOfManufacture"]+" 年";}}}catch{/* Optional EDID identity data is not exposed by all drivers. */}
   return list;
  }catch{foreach(var d in list)d.Dispose();throw;}
 }
 public static string ScanWarning="";
}
class QuickPanel : UserControl {
 readonly LevelSlider b=new LevelSlider(),c=new LevelSlider(); readonly Label bl,cl,bv,cv,status,device; public Action<bool,int> Change;public Action Settings;
 public QuickPanel(){
  AutoScaleMode=AutoScaleMode.Inherit;Size=new Size(400,354);Font=new Font("Microsoft YaHei UI",9.5f);
  Controls.Add(new Brand{Bounds=new Rectangle(22,20,42,42)});Controls.Add(Appearance.Label("轻屏",76,18,220,30,17,true,false));
  device=Appearance.Label("未选择显示器",77,52,298,24,9,false,true);device.AutoEllipsis=true;Controls.Add(device);
  var card=new Card{Bounds=new Rectangle(20,94,360,184)};Controls.Add(card);
  bl=Appearance.Label("亮度",18,20,208,28,11,true,false);cl=Appearance.Label("对比度",18,100,208,28,11,true,false);bv=Appearance.Label("—",226,12,114,38,22,true,false);cv=Appearance.Label("—",226,92,114,38,22,true,false);bv.Tag=cv.Tag="accent";bv.TextAlign=cv.TextAlign=ContentAlignment.MiddleRight;card.Controls.AddRange(new Control[]{bl,cl,bv,cv});
  b.SetBounds(10,51,340,38);c.SetBounds(10,131,340,38);b.AccessibleName="快速面板亮度";c.AccessibleName="快速面板对比度";card.Controls.Add(b);card.Controls.Add(c);
  b.Scroll+=delegate{bv.Text=b.Value+"%";};c.Scroll+=delegate{cv.Text=c.Value+"%";};
  b.MouseUp+=delegate{if(Change!=null)Change(true,b.Value);};b.KeyUp+=delegate{if(Change!=null)Change(true,b.Value);};c.MouseUp+=delegate{if(Change!=null)Change(false,c.Value);};c.KeyUp+=delegate{if(Change!=null)Change(false,c.Value);};
  var settings=Appearance.Button("完整设置  →",252,300,128,34,false);Controls.Add(settings);settings.Click+=delegate{if(Settings!=null)Settings();};status=Appearance.Label("",22,305,222,35,8.5f,false,true);Controls.Add(status);Appearance.Apply(this);
 }
 public void LoadDevices(List<Display> list,int selected){}
 public void Sync(Display d,bool busy,int selected){b.Enabled=!busy&&d!=null&&d.Bright;c.Enabled=!busy&&d!=null&&d.Contrast;device.Text=d!=null?d.Name:"未选择显示器";if(d!=null){b.Value=Math.Max(0,Math.Min(100,d.B));c.Value=Math.Max(0,Math.Min(100,d.C));}bv.Text=d!=null&&d.Bright?b.Value+"%":"—";cv.Text=d!=null&&d.Contrast?c.Value+"%":"—";bl.Text=d!=null&&d.Bright?"亮度":"亮度 · 不支持";cl.Text=d!=null&&d.Contrast?"对比度":"对比度 · 不支持";status.Text=busy?"正在应用硬件设置…":d==null?"请在完整设置中选择设备":"点击外部或按 Esc 收起";}
}
class QuickPopup : AppWindow {
 public QuickPopup(QuickPanel panel){SuspendLayout();
  FormBorderStyle=FormBorderStyle.FixedSingle;ControlBox=false;Text="";
  ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;TopMost=true;
  DesignCanvas=panel.Size;ClientSize=panel.Size;Controls.Add(panel);AutoScaleDimensions=new SizeF(96,96);ResumeLayout(true);
 }
 protected override void OnDeactivate(EventArgs e){base.OnDeactivate(e);Hide();}
 protected override bool ProcessCmdKey(ref Message msg,Keys keyData){if(keyData==Keys.Escape){Hide();return true;}return base.ProcessCmdKey(ref msg,keyData);}
 protected override void OnFormClosing(FormClosingEventArgs e){if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}base.OnFormClosing(e);}
 public void OpenAt(Point point,Rectangle area){
  Location=new Point(Math.Max(area.Left,Math.Min(point.X-Width/2,area.Right-Width)),Math.Max(area.Top,Math.Min(point.Y-Height-10,area.Bottom-Height)));
  Show();FitToScreen();Native.SetForegroundWindow(Handle);Activate();BeginInvoke((Action)(()=>{if(!IsDisposed&&Visible)Location=new Point(Math.Max(area.Left,Math.Min(point.X-Width/2,area.Right-Width)),Math.Max(area.Top,Math.Min(point.Y-Height-10,area.Bottom-Height)));}));
 }
}
class MainForm : AppWindow {
 QuickPanel quick; QuickPopup popup; DisplaySettingsPanel displaySettings;
 readonly ComboBox screens=new ThemeCombo(); readonly LevelSlider brightness=new LevelSlider(),contrast=new LevelSlider(); readonly Label info=new Label(); readonly NotifyIcon tray=new NotifyIcon(); readonly Button refresh=new SoftButton(); readonly CheckBox startup=new CheckBox();
 Label brightValue,contrastValue,connectionBadge,dpiStatus; ComboBox theme; Icon appIcon; readonly ToolTip statusTip=new ToolTip();
 List<Display> displays=new List<Display>(); bool busy,exiting; int preset1=30,preset2=70; string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenLite","presets.txt");
 public MainForm(){
  SuspendLayout();Appearance.Load();Text="轻屏 · ScreenLite";DesignCanvas=new Size(1100,780);ClientSize=DesignCanvas;AutoScroll=true;AutoScrollMinSize=DesignCanvas;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;Font=new Font("Microsoft YaHei UI",9.5f);StartPosition=FormStartPosition.CenterScreen;
  appIcon=Appearance.AppIcon();Icon=appIcon;
  Controls.Add(new Brand{Bounds=new Rectangle(24,24,58,58)});
  Controls.Add(Appearance.Label("轻屏",98,22,150,38,25,true,false));Controls.Add(Appearance.Label("SCREENLITE",207,36,180,22,9,true,true));
  Controls.Add(Appearance.Label("屏幕控制，刚刚好。",100,64,550,24,10,false,true));
  Controls.Add(Appearance.Label("外观",850,43,70,24,9,false,true));
  theme=new ThemeCombo();theme.Items.AddRange(new object[]{"浅色主题","深色主题"});theme.SetBounds(932,36,144,34);theme.SelectedIndex=Appearance.Dark?1:0;Controls.Add(theme);
  var deviceCard=new Card{Bounds=new Rectangle(24,110,1052,80)};Controls.Add(deviceCard);deviceCard.Controls.Add(Appearance.Label("当前显示器",20,10,200,22,8.5f,true,true));
  connectionBadge=Appearance.Label("等待检测",600,10,430,22,8.5f,false,true);connectionBadge.TextAlign=ContentAlignment.MiddleRight;deviceCard.Controls.Add(connectionBadge);
  screens.SetBounds(20,36,856,32);screens.SelectedIndexChanged+=delegate{UpdateView();};deviceCard.Controls.Add(screens);
  refresh.Text="重新检测";refresh.SetBounds(890,34,142,34);refresh.Click+=async delegate{await Scan();};deviceCard.Controls.Add(refresh);
  var levels=new Card{Bounds=new Rectangle(24,210,480,290)};Controls.Add(levels);
  levels.Controls.Add(Appearance.Label("画面调节",22,18,260,28,13,true,false));levels.Controls.Add(Appearance.Label("硬件亮度与对比度",22,49,300,22,9,false,true));
  levels.Controls.Add(Appearance.Label("亮度",22,91,240,28,11,true,false));levels.Controls.Add(Appearance.Label("对比度",22,188,240,28,11,true,false));
  brightValue=Appearance.Label("—",322,81,136,42,25,true,false);brightValue.Tag="accent";brightValue.TextAlign=ContentAlignment.MiddleRight;contrastValue=Appearance.Label("—",322,178,136,42,25,true,false);contrastValue.Tag="accent";contrastValue.TextAlign=ContentAlignment.MiddleRight;levels.Controls.Add(brightValue);levels.Controls.Add(contrastValue);
  brightness.SetBounds(16,126,448,44);contrast.SetBounds(16,223,448,44);brightness.AccessibleName="亮度";contrast.AccessibleName="对比度";levels.Controls.Add(brightness);levels.Controls.Add(contrast);
  brightness.Scroll+=delegate{brightValue.Text=brightness.Value+"%";};contrast.Scroll+=delegate{contrastValue.Text=contrast.Value+"%";};
  brightness.MouseUp+=async delegate{await Apply(true,brightness.Value);};brightness.KeyUp+=async delegate{await Apply(true,brightness.Value);};contrast.MouseUp+=async delegate{await Apply(false,contrast.Value);};contrast.KeyUp+=async delegate{await Apply(false,contrast.Value);};
  var presets=new Card{Bounds=new Rectangle(24,518,480,114)};Controls.Add(presets);presets.Controls.Add(Appearance.Label("快捷预设",22,15,200,25,11,true,false));presets.Controls.Add(Appearance.Label("一键切换亮度",264,17,192,22,9,false,true));
  var p1=Appearance.Button("预设 1",22,55,112,38,true);var p2=Appearance.Button("预设 2",146,55,112,38,false);var save=Appearance.Button("保存当前亮度",270,55,188,38,false);presets.Controls.AddRange(new Control[]{p1,p2,save});
  p1.Click+=async delegate{await Apply(true,preset1);};p2.Click+=async delegate{await Apply(true,preset2);};save.Click+=delegate{var d=Selected;if(d==null||!d.Bright)return;var a=MessageBox.Show(this,"保存为预设 1？\n选择“否”保存为预设 2。",Text,MessageBoxButtons.YesNoCancel);if(a==DialogResult.Cancel)return;if(a==DialogResult.Yes)preset1=d.B;else preset2=d.B;Save();};
  var general=new Card{Bounds=new Rectangle(24,650,480,78)};Controls.Add(general);startup.Text="登录 Windows 时启动";startup.SetBounds(22,13,436,26);startup.Cursor=Cursors.Hand;using(var key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))startup.Checked=key!=null&&key.GetValue("ScreenLite")!=null;startup.CheckedChanged+=delegate{try{using(var key=Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){if(startup.Checked)key.SetValue("ScreenLite","\""+Application.ExecutablePath+"\" --tray");else key.DeleteValue("ScreenLite",false);}}catch(Exception e){MessageBox.Show(e.Message);}};general.Controls.Add(startup);general.Controls.Add(Appearance.Label("自动收起到系统托盘，随时轻点调节",43,43,412,24,9,false,true));
  info.SetBounds(28,747,824,25);info.Tag="muted";info.Font=new Font(Font.FontFamily,8.5f);info.AutoEllipsis=true;Controls.Add(info);dpiStatus=Appearance.Label("",866,747,208,25,8.5f,false,true);dpiStatus.TextAlign=ContentAlignment.MiddleRight;Controls.Add(dpiStatus);
  var menu=new ContextMenuStrip();menu.Items.Add("打开完整设置",null,delegate{Open();});menu.Items.Add("亮度预设 1",null,async delegate{await Apply(true,preset1);});menu.Items.Add("亮度预设 2",null,async delegate{await Apply(true,preset2);});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出轻屏",null,delegate{if(busy){MessageBox.Show("正在访问硬件，请稍后再退出。");return;}exiting=true;Close();});tray.Icon=appIcon;tray.Text="轻屏 · 点击快速调节";tray.ContextMenuStrip=menu;tray.Visible=true;
  displaySettings=new DisplaySettingsPanel{Bounds=new Rectangle(524,210,552,518)};displaySettings.ModeRequested=ChangeDisplayMode;displaySettings.HdrRequested=ChangeHdr;Controls.Add(displaySettings);
  quick=new QuickPanel();popup=new QuickPopup(quick);Appearance.Apply(this);Appearance.Apply(popup);
  quick.Change=async delegate(bool b,int v){await Apply(b,v);};quick.Settings=delegate{popup.Hide();Open();};tray.MouseClick+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left)ShowQuick();};
  theme.SelectedIndexChanged+=delegate{Appearance.Dark=theme.SelectedIndex==1;Appearance.Apply(this);Appearance.Apply(popup);try{Appearance.Save();}catch(Exception e){MessageBox.Show("主题已切换，但保存失败："+e.Message);}};
  try{var a=File.ReadAllLines(settings);preset1=Math.Max(0,Math.Min(100,int.Parse(a[0])));preset2=Math.Max(0,Math.Min(100,int.Parse(a[1])));}catch{}
  DpiChanged+=delegate(object sender,DpiChangedEventArgs e){dpiStatus.Text="界面缩放  "+Math.Round(e.DeviceDpiNew/96.0*100)+"%";};
  Shown+=async delegate{dpiStatus.Text="界面缩放  "+Math.Round(DeviceDpi/96.0*100)+"%";await Scan();if(Array.IndexOf(Environment.GetCommandLineArgs(),"--tray")>=0)Hide();};FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!exiting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};AutoScaleDimensions=new SizeF(96,96);ResumeLayout(true);
 }
 Display Selected {get{return screens.SelectedItem as Display;}}
 void AddLabel(string text,int x,int y){var l=new Label{Text=text};l.SetBounds(x,y,100,25);Controls.Add(l);}
 Button ButtonAt(string text,int x,int y){var b=new Button{Text=text};b.SetBounds(x,y,125,30);Controls.Add(b);return b;}
 void Open(){if(popup!=null)popup.Hide();Show();WindowState=FormWindowState.Normal;Activate();}
 void ShowQuick(){if(popup.Visible){popup.Hide();return;}quick.LoadDevices(displays,screens.SelectedIndex);quick.Sync(Selected,busy,screens.SelectedIndex);var point=Cursor.Position;popup.OpenAt(point,Screen.FromPoint(point).WorkingArea);quick.Focus();}
 void Save(){try{Directory.CreateDirectory(Path.GetDirectoryName(settings));File.WriteAllLines(settings,new[]{preset1.ToString(),preset2.ToString()});}catch(Exception e){MessageBox.Show("保存失败："+e.Message);}}
 void SetStatus(string text){info.Text=text.Replace("\n"," · ");statusTip.SetToolTip(info,text);}
 void UpdateView(){var d=Selected;if(displaySettings!=null)displaySettings.Bind(d,busy);screens.Enabled=!busy;brightValue.Text=d!=null&&d.Bright?d.B+"%":"不支持";contrastValue.Text=d!=null&&d.Contrast?d.C+"%":"不支持";if(quick!=null)quick.Sync(d,busy,screens.SelectedIndex);brightness.Enabled=!busy&&d!=null&&d.Bright;contrast.Enabled=!busy&&d!=null&&d.Contrast;if(d!=null){brightness.Value=Math.Max(0,Math.Min(100,d.B));contrast.Value=Math.Max(0,Math.Min(100,d.C));}connectionBadge.Text=d==null?"等待检测":(busy?"正在应用 / 检测…":d.Windows!=null?d.Windows.Connection+"   ·   "+(d.WmiPath!=null?"WMI 内屏亮度":d.Handle!=IntPtr.Zero?"DDC/CI 硬件控制":"Windows 显示接口"):"独立硬件控制接口");SetStatus(d==null?"请选择设备或重新检测。":busy?"正在访问显示器，请稍候…":"Ctrl + Alt + ↑ / ↓ 调亮度     ·     Ctrl + Alt + 1 / 2 切换预设     ·     关闭窗口收起到托盘");}
 async Task Scan(){if(busy)return;busy=true;refresh.Enabled=false;UpdateView();info.Text="正在检测硬件能力…";try{string previous=Selected!=null&&Selected.Windows!=null?Selected.Windows.StableKey:null;var next=await Task.Run(()=>Display.Scan());foreach(var d in displays)d.Dispose();displays=next;screens.Items.Clear();screens.Items.AddRange(displays.ToArray());if(screens.Items.Count>0){int selected=displays.FindIndex(d=>d.Windows!=null&&string.Equals(d.Windows.StableKey,previous,StringComparison.OrdinalIgnoreCase));screens.SelectedIndex=selected>=0?selected:0;}}catch(Exception e){MessageBox.Show("检测失败："+e.Message);}finally{busy=false;refresh.Enabled=true;UpdateView();if(Display.ScanWarning.Length>0)SetStatus(info.Text+"\n"+Display.ScanWarning);}}
 async Task Apply(bool b,int value){var d=Selected;if(busy||d==null||(b?!d.Bright:!d.Contrast))return;busy=true;refresh.Enabled=false;screens.Enabled=false;UpdateView();try{await Task.Run(()=>d.Set(b,Math.Max(0,Math.Min(100,value))));}catch(Exception e){MessageBox.Show("硬件设置失败，请重新检测。\n"+e.Message,Text);}finally{busy=false;refresh.Enabled=true;screens.Enabled=true;UpdateView();}}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);int failed=0;uint[] keys={0x26,0x28,0x31,0x32};for(int i=0;i<4;i++)if(!Native.RegisterHotKey(Handle,i+1,0x4003,keys[i]))failed++;if(failed>0)BeginInvoke((Action)(()=>MessageBox.Show("部分全局快捷键被其他程序占用，仍可用托盘和按钮操作。",Text)));}
 protected override void WndProc(ref Message m){if(m.Msg==0x312){int id=m.WParam.ToInt32();var d=Selected;if(d!=null)HandleHotkey(id,d);return;}base.WndProc(ref m);}
 async void HandleHotkey(int id,Display d){await Apply(true,id==1?d.B+5:id==2?d.B-5:id==3?preset1:preset2);}
 async Task ChangeDisplayMode(Display display,ScreenMode requested){
  if(busy||display!=Selected||display.Windows==null)return;busy=true;refresh.Enabled=false;UpdateView();ModePreview preview=null;string failure=null;
  try{
   var target=await Task.Run(()=>SystemDisplay.Resolve(display.Windows));if(!target.CanMode)throw new Exception(target.Clone?target.Warning:"当前设备没有可安全设置的显示模式。");
   var mode=target.Modes.Find(m=>m.Raw.Width==requested.Raw.Width&&m.Raw.Height==requested.Raw.Height&&m.Raw.Frequency==requested.Raw.Frequency);if(mode==null)throw new Exception("此分辨率和刷新率组合已不可用，请重新检测。");
   preview=new ModePreview(target.DeviceName,target.Current,delegate(string device,DisplayApi.DevMode candidate,uint flags){SystemDisplay.Resolve(target);return ModePreview.NativeChange(device,candidate,flags);});await Task.Run(()=>preview.Apply(mode.Raw));
   var actual=await Task.Run(()=>DisplayApi.Current(target.DeviceName));if(actual.Width!=mode.Raw.Width||actual.Height!=mode.Raw.Height||actual.Frequency!=mode.Raw.Frequency)throw new Exception("驱动未应用所选模式，将恢复原设置。");
   using(var confirm=new KeepDisplayDialog()){if(confirm.ShowDialog(this)==DialogResult.OK)await Task.Run(()=>preview.Commit(mode.Raw));}
  }catch(Exception ex){failure=ex.Message;}
  {if(preview!=null){try{await Task.Run(()=>preview.Dispose());if(preview.RollbackError!=null)failure=(failure==null?"":failure+"\n")+preview.RollbackError;}catch(Exception ex){failure="恢复显示模式时出错："+ex.Message;}}busy=false;refresh.Enabled=true;await Scan();}
  if(failure!=null)MessageBox.Show(this,failure+"\n如有需要，请从 Windows 显示设置检查当前模式。",Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);
 }
 async Task ChangeHdr(Display display,bool enabled){
  if(busy||display!=Selected||display.Windows==null)return;busy=true;refresh.Enabled=false;UpdateView();SystemDisplay target=null;bool attempted=false,kept=false;string failure=null;
  try{target=await Task.Run(()=>SystemDisplay.Resolve(display.Windows));if(!target.CanHdr)throw new Exception(target.HdrStatus);if(target.HdrEnabled==enabled){kept=true;}else{attempted=true;await Task.Run(()=>target.SetHdr(enabled));using(var confirm=new KeepDisplayDialog()){kept=confirm.ShowDialog(this)==DialogResult.OK;}}}
  catch(Exception ex){failure=ex.Message;}
  {if(attempted&&!kept&&target!=null){try{await Task.Run(()=>target.SetHdr(target.HdrEnabled));}catch(Exception ex){failure=(failure==null?"":failure+"\n")+"恢复 HDR 状态失败："+ex.Message;}}busy=false;refresh.Enabled=true;await Scan();}
  if(failure!=null)MessageBox.Show(this,failure,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);
 }
 protected override void Dispose(bool disposing){if(disposing){for(int i=1;i<=4;i++)Native.UnregisterHotKey(Handle,i);tray.Dispose();statusTip.Dispose();if(popup!=null)popup.Dispose();foreach(var d in displays)d.Dispose();if(appIcon!=null)appIcon.Dispose();}base.Dispose(disposing);}
}
static class Program {
 [STAThread] static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var mutex=new Mutex(false,"Local\\ScreenLite.Singleton")){if(!mutex.WaitOne(0,false)){MessageBox.Show("轻屏已在运行，请从系统托盘打开。");return;}try{Application.Run(new MainForm());}finally{mutex.ReleaseMutex();}}}
}
}









