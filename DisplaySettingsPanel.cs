using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenLite {
class DisplaySettingsPanel : UserControl {
 readonly ThemeCombo resolution=new ThemeCombo(),rate=new ThemeCombo();readonly Button apply,hdr;readonly Label current,hdrStatus,hint;readonly DisplayInfoView details;
 Display selected;bool syncing;
 public Func<Display,ScreenMode,Task> ModeRequested;public Func<Display,bool,Task> HdrRequested;
 public DisplaySettingsPanel(){
  AutoScaleMode=AutoScaleMode.Inherit;Size=new Size(552,518);Font=new Font("Microsoft YaHei UI",9.5f);
  var modes=new Card{Bounds=new Rectangle(0,0,552,190)};Controls.Add(modes);modes.Controls.Add(Appearance.Label("显示模式",22,17,300,28,13,true,false));
  current=Appearance.Label("请选择显示器",22,49,508,24,9.5f,false,true);modes.Controls.Add(current);
  modes.Controls.Add(Appearance.Label("分辨率",22,82,300,22,9,true,true));modes.Controls.Add(Appearance.Label("刷新率",348,82,182,22,9,true,true));
  resolution.SetBounds(22,108,312,34);rate.SetBounds(348,108,182,34);modes.Controls.Add(resolution);modes.Controls.Add(rate);
  hint=Appearance.Label("",22,153,312,26,8.5f,false,true);hint.AutoEllipsis=true;modes.Controls.Add(hint);apply=Appearance.Button("应用更改",348,149,182,30,true);modes.Controls.Add(apply);
  resolution.SelectedIndexChanged+=delegate{if(!syncing)PopulateRates();};apply.Click+=async delegate{var mode=rate.SelectedItem as ScreenMode;if(selected!=null&&mode!=null&&ModeRequested!=null)await ModeRequested(selected,mode);};
  var color=new Card{Bounds=new Rectangle(0,208,552,100)};Controls.Add(color);color.Controls.Add(Appearance.Label("HDR 高动态范围",22,18,360,28,13,true,false));
  hdrStatus=Appearance.Label("",22,53,358,30,9,false,true);color.Controls.Add(hdrStatus);hdr=Appearance.Button("开启 HDR",394,32,136,38,false);color.Controls.Add(hdr);hdr.Click+=async delegate{var d=selected;if(d!=null&&d.Windows!=null&&HdrRequested!=null)await HdrRequested(d,!d.Windows.HdrEnabled);};
  var information=new Card{Bounds=new Rectangle(0,326,552,192)};Controls.Add(information);information.Controls.Add(Appearance.Label("设备信息",22,16,300,28,12,true,false));var copy=Appearance.Button("复制信息",416,14,114,28,false);information.Controls.Add(copy);copy.Click+=delegate{Appearance.CopyText(details.Text);};
  details=new DisplayInfoView{AccessibleName="显示器信息",Bounds=new Rectangle(22,53,508,119),Font=new Font("Microsoft YaHei UI",9.5f)};information.Controls.Add(details);
 }
 void PopulateRates(){
  rate.Items.Clear();var d=selected==null?null:selected.Windows;if(d==null||resolution.SelectedItem==null)return;string size=resolution.SelectedItem.ToString();var choices=d.Modes.Where(m=>m.Resolution==size).ToArray();rate.Items.AddRange(choices);int index=Array.FindIndex(choices,m=>m.Raw.Frequency==d.Current.Frequency);if(choices.Length>0)rate.SelectedIndex=index>=0?index:0;
 }
 public void Bind(Display display,bool busy){
  bool changed=!object.ReferenceEquals(selected,display);selected=display;var d=display==null?null:display.Windows;
  if(changed){syncing=true;resolution.Items.Clear();rate.Items.Clear();if(d!=null){resolution.Items.AddRange(d.Modes.Select(m=>m.Resolution).Distinct().Cast<object>().ToArray());string size=d.Current.Width+" × "+d.Current.Height;int index=resolution.Items.IndexOf(size);if(resolution.Items.Count>0)resolution.SelectedIndex=index>=0?index:0;}syncing=false;PopulateRates();}
  resolution.Enabled=rate.Enabled=apply.Enabled=!busy&&d!=null&&d.CanMode;
  current.Text=d!=null&&d.HasMode?"当前  "+d.Current.Width+" × "+d.Current.Height+"  /  "+(d.SignalHz>0?d.SignalHz.ToString("0.###"):d.Current.Frequency.ToString())+" Hz":"当前显示模式未读取";
  hint.Text=d==null?"无法匹配 Windows 显示设备。":d.Clone?"复制屏幕共享显示源，请先改为扩展模式。":!d.CanMode?"系统未提供可安全设置的显示模式。":"更改后 15 秒内确认，否则自动还原。";
  hdr.Enabled=!busy&&d!=null&&d.CanHdr;hdr.Text=d!=null&&d.HdrEnabled?"关闭 HDR":"开启 HDR";hdrStatus.Text=d==null?"未匹配到 HDR 控制接口":d.Clone?"复制模式下暂不提供独立 HDR 开关":d.HdrStatus;
  details.Text=display==null?"请选择设备或重新检测。":d==null?display.Name+Environment.NewLine+"仅识别到独立硬件控制接口，无法可靠匹配 Windows 显示设置。":d.Information().Replace("\n",Environment.NewLine)+(d.Warning.Length>0?Environment.NewLine+d.Warning:"");
 }
}
class KeepDisplayDialog : AppWindow {
 readonly Timer timer=new Timer();readonly Label message;DateTime deadline;
 public KeepDisplayDialog(){SuspendLayout();
  Text="保留显示设置？";DesignCanvas=new Size(414,182);ClientSize=DesignCanvas;Font=new Font("Microsoft YaHei UI",10);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;TopMost=true;ShowInTaskbar=false;
  Controls.Add(Appearance.Label("屏幕显示正常吗？",22,20,370,30,14,true,false));message=Appearance.Label("",22,60,370,45,9,false,true);Controls.Add(message);
  var revert=Appearance.Button("还原",22,123,174,36,false);revert.DialogResult=DialogResult.Cancel;var keep=Appearance.Button("保留更改",208,123,184,36,true);keep.DialogResult=DialogResult.OK;Controls.Add(revert);Controls.Add(keep);CancelButton=revert;AcceptButton=revert;Appearance.Apply(this);
  timer.Interval=100;timer.Tick+=delegate{UpdateCountdown();};Shown+=delegate{deadline=DateTime.UtcNow.AddSeconds(15);UpdateCountdown();timer.Start();revert.Focus();};AutoScaleDimensions=new SizeF(96,96);ResumeLayout(true);
 }
 void UpdateCountdown(){int seconds=(int)Math.Ceiling((deadline-DateTime.UtcNow).TotalSeconds);message.Text="将在 "+Math.Max(0,seconds)+" 秒后自动恢复原设置。\n看不清时请等待，或按 Esc 立即还原。";if(seconds<=0){timer.Stop();DialogResult=DialogResult.Cancel;Close();}}
 protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
}



