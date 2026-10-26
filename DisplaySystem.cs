using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace ScreenLite {
// Structures follow the Windows SDK wingdi.h / winuser.h layout, including alignment.
static class DisplayApi {
 [StructLayout(LayoutKind.Sequential)] public struct Luid {public uint Low;public int High;}
 [StructLayout(LayoutKind.Sequential)] public struct Rational {public uint Numerator,Denominator;public double Hz {get{return Denominator==0?0:Numerator/(double)Denominator;}}}
 [StructLayout(LayoutKind.Sequential)] public struct Source {public Luid Adapter;public uint Id,Mode,Flags;}
 [StructLayout(LayoutKind.Sequential)] public struct Target {public Luid Adapter;public uint Id,Mode,Technology,Rotation,Scaling;public Rational Refresh;public uint Scanline;public int Available;public uint Flags;}
 [StructLayout(LayoutKind.Sequential)] public struct Path {public Source Source;public Target Target;public uint Flags;}
 [StructLayout(LayoutKind.Explicit,Size=64)] public struct ModeInfo {[FieldOffset(0)] public uint Type;[FieldOffset(4)]public uint Id;[FieldOffset(8)]public Luid Adapter;}
 [StructLayout(LayoutKind.Sequential)] public struct Header {public uint Type,Size;public Luid Adapter;public uint Id;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct SourceName {public Header Header;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct TargetName {public Header Header;public uint Flags,Technology;public ushort Manufacturer,Product;public uint Connector;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string DevicePath;}
 [StructLayout(LayoutKind.Sequential)] public struct ColorInfo {public Header Header;public uint Flags,Encoding,Bits;}
 [StructLayout(LayoutKind.Sequential)] public struct ColorInfo2 {public Header Header;public uint Flags,Encoding,Bits,ActiveMode;}
 [StructLayout(LayoutKind.Sequential)] public struct ColorState {public Header Header;public uint Enable;}
 [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct Device {public int Size;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Id;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Key;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct DevMode {
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string DeviceName;
  public ushort SpecVersion,DriverVersion,Size,DriverExtra;public uint Fields;
  public int X,Y;public uint Orientation,FixedOutput;
  public short Color,Duplex,YResolution,TTOption,Collate;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string FormName;
  public ushort LogPixels;public uint Bits,Width,Height,Flags,Frequency,ICMMethod,ICMIntent,MediaType,DitherType,Reserved1,Reserved2,PanningWidth,PanningHeight;
 }
 [DllImport("user32.dll")]public static extern int GetDisplayConfigBufferSizes(uint flags,out uint paths,out uint modes);
 [DllImport("user32.dll")]public static extern int QueryDisplayConfig(uint flags,ref uint paths,[Out]Path[] path,ref uint modes,[Out]ModeInfo[] mode,IntPtr topology);
 [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]public static extern int GetSource(ref SourceName info);
 [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]public static extern int GetTarget(ref TargetName info);
 [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]public static extern int GetColor(ref ColorInfo info);
 [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]public static extern int GetColor2(ref ColorInfo2 info);
 [DllImport("user32.dll",EntryPoint="DisplayConfigSetDeviceInfo")]public static extern int SetColor(ref ColorState info);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="EnumDisplaySettingsW")]public static extern bool EnumModes(string device,int index,ref DevMode mode);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="ChangeDisplaySettingsExW")]public static extern int ChangeMode(string device,ref DevMode mode,IntPtr window,uint flags,IntPtr param);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="GetMonitorInfoW")]public static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,EntryPoint="EnumDisplayDevicesW")]public static extern bool EnumDevices(string device,uint index,ref Device info,uint flags);
 public static Header MakeHeader<T>(uint type,Luid adapter,uint id){return new Header{Type=type,Size=(uint)Marshal.SizeOf(typeof(T)),Adapter=adapter,Id=id};}
 public static DevMode NewMode(){return new DevMode{Size=(ushort)Marshal.SizeOf(typeof(DevMode))};}
 public static DevMode Current(string device){var mode=NewMode();if(string.IsNullOrEmpty(device)||!EnumModes(device,-1,ref mode))throw new Exception("无法读取当前显示模式，请重新检测设备。");return mode;}
 public static string Error(int code){switch(code){case -1:return "显示驱动拒绝设置";case -2:return "当前连接不支持此模式";case -3:return "无法保存到用户设置";case -4:return "系统拒绝了设置参数";case -5:return "设置参数无效";case -6:return "当前多屏配置不支持";case 1:return "驱动要求重新启动，未作为即时更改保留";default:return "系统返回 "+code;}}
 public static void RequireModeSuccess(int code){if(code!=0)throw new Exception(Error(code)+"（代码 "+code+"）");}
}
class ScreenMode {
 public DisplayApi.DevMode Raw;
 public string Resolution {get{return Raw.Width+" × "+Raw.Height;}}
 public override string ToString(){return Raw.Frequency+" Hz";}
 public static List<ScreenMode> Read(string device,DisplayApi.DevMode current){
  var result=new List<ScreenMode>();var seen=new HashSet<string>();
  for(int i=0;i<4096;i++){var m=DisplayApi.NewMode();if(!DisplayApi.EnumModes(device,i,ref m))break;if(m.Bits!=32||m.Width==0||m.Height==0||m.Frequency<=1||(m.Flags&2)!=0||m.Orientation!=current.Orientation)continue;string key=m.Width+"/"+m.Height+"/"+m.Frequency;if(seen.Add(key))result.Add(new ScreenMode{Raw=m});}
  return result.OrderByDescending(m=>(long)m.Raw.Width*m.Raw.Height).ThenByDescending(m=>m.Raw.Width).ThenByDescending(m=>m.Raw.Frequency).ToList();
 }
}
class SystemDisplay {
 public string DeviceName,Name,DevicePath,Connection,Gpu,IdentityInfo="",Warning="",HdrStatus="未能读取 HDR 状态";
 public DisplayApi.Path Path; public DisplayApi.DevMode Current; public List<ScreenMode> Modes=new List<ScreenMode>();
 public bool HasMode,Clone,HdrKnown,HdrSupported,HdrEnabled,HdrBlocked,NewColorApi,Primary;
 public uint BitsPerChannel,Encoding;public double SignalHz;
 public string StableKey {get{return DeviceName+"|"+DevicePath;}}
 public bool CanMode {get{return HasMode&&!Clone&&!string.IsNullOrEmpty(DevicePath)&&Modes.Count>0;}}
 public bool CanHdr {get{return HdrKnown&&HdrSupported&&!HdrBlocked&&!Clone&&!string.IsNullOrEmpty(DevicePath);}}
 public static string ConnectionName(uint type){switch(type){case 0:return "VGA";case 4:return "DVI";case 5:return "HDMI";case 6:return "内屏 / LVDS";case 10:return "DisplayPort";case 11:return "内屏 / eDP";case 12:return "UDI";case 13:return "内屏 / UDI";case 15:return "Miracast";case 16:return "间接有线显示";case 17:return "间接虚拟显示";case 0x80000000:return "内置显示器";default:return "系统连接类型 "+type;}}
 public static List<SystemDisplay> Read(){
  DisplayApi.Path[] paths=null;uint count=0;bool ready=false;
  for(int attempt=0;attempt<4;attempt++){uint modes;int rc=DisplayApi.GetDisplayConfigBufferSizes(2,out count,out modes);if(rc!=0)throw new Win32Exception(rc);paths=new DisplayApi.Path[count];var buffer=new DisplayApi.ModeInfo[modes];rc=DisplayApi.QueryDisplayConfig(2,ref count,paths,ref modes,buffer,IntPtr.Zero);if(rc==122)continue;if(rc!=0)throw new Win32Exception(rc);ready=true;break;}
  if(!ready)throw new Exception("显示配置正在变化，请稍后重新检测。");
  var list=new List<SystemDisplay>();
  for(int i=0;i<count;i++){var path=paths[i];var source=new DisplayApi.SourceName{Header=DisplayApi.MakeHeader<DisplayApi.SourceName>(1,path.Source.Adapter,path.Source.Id)};if(DisplayApi.GetSource(ref source)!=0||string.IsNullOrEmpty(source.Name))continue;
   var target=new DisplayApi.TargetName{Header=DisplayApi.MakeHeader<DisplayApi.TargetName>(2,path.Target.Adapter,path.Target.Id)};int nr=DisplayApi.GetTarget(ref target);
   var d=new SystemDisplay{Path=path,DeviceName=source.Name,Name=nr==0&&!string.IsNullOrWhiteSpace(target.Name)?target.Name:"显示器 "+source.Name,DevicePath=nr==0?target.DevicePath:null,Connection=ConnectionName(path.Target.Technology),SignalHz=path.Target.Refresh.Hz};
   try{d.Current=DisplayApi.Current(d.DeviceName);d.HasMode=true;d.Modes=ScreenMode.Read(d.DeviceName,d.Current);}catch(Exception ex){d.Warning=ex.Message;}
   var gpu=new DisplayApi.Device{Size=Marshal.SizeOf(typeof(DisplayApi.Device))};for(uint gi=0;gi<128;gi++){gpu.Size=Marshal.SizeOf(typeof(DisplayApi.Device));if(!DisplayApi.EnumDevices(null,gi,ref gpu,0))break;if(string.Equals(gpu.Name,d.DeviceName,StringComparison.OrdinalIgnoreCase)){d.Gpu=gpu.Description;d.Primary=(gpu.Flags&4)!=0;break;}}
   d.ReadHdr();list.Add(d);
  }
  foreach(var d in list){d.Clone=list.Count(x=>string.Equals(x.DeviceName,d.DeviceName,StringComparison.OrdinalIgnoreCase))>1;if(d.Clone)d.Warning="复制显示模式：多个屏幕共享同一显示源，暂不提供独立 HDR / 显示模式更改。";}
  return list;
 }
 public void ReadHdr(){
  HdrKnown=false;HdrSupported=false;HdrEnabled=false;HdrBlocked=false;NewColorApi=false;
  var info2=new DisplayApi.ColorInfo2{Header=DisplayApi.MakeHeader<DisplayApi.ColorInfo2>(15,Path.Target.Adapter,Path.Target.Id)};
  int rc=DisplayApi.GetColor2(ref info2);
  if(rc==0){NewColorApi=true;HdrKnown=true;HdrSupported=(info2.Flags&16)!=0;HdrEnabled=(info2.Flags&32)!=0;HdrBlocked=(info2.Flags&8)!=0;BitsPerChannel=info2.Bits;Encoding=info2.Encoding;HdrStatus=!HdrSupported?"当前连接未提供 HDR 支持":HdrBlocked?"HDR 受系统策略或电源状态限制":HdrEnabled?(info2.ActiveMode==2?"HDR 已开启":"HDR 已启用，当前未激活"):"HDR 已关闭";return;}
  var info=new DisplayApi.ColorInfo{Header=DisplayApi.MakeHeader<DisplayApi.ColorInfo>(9,Path.Target.Adapter,Path.Target.Id)};rc=DisplayApi.GetColor(ref info);
  if(rc!=0){HdrStatus="系统/驱动未提供 HDR 接口（"+rc+"）";return;}
  HdrKnown=true;BitsPerChannel=info.Bits;Encoding=info.Encoding;HdrSupported=(info.Flags&1)!=0&&(info.Flags&4)==0;HdrEnabled=(info.Flags&2)!=0;HdrBlocked=(info.Flags&8)!=0;
  HdrStatus=(info.Flags&4)!=0?"当前为强制广色域模式，无法单独确认 HDR":!HdrSupported?"当前连接未提供 HDR 支持":HdrBlocked?"HDR 受系统或驱动限制":HdrEnabled?"HDR 已开启（兼容接口）":"HDR 已关闭（兼容接口）";
 }
 public static SystemDisplay Resolve(SystemDisplay previous){
  if(previous==null||string.IsNullOrEmpty(previous.DevicePath))throw new Exception("无法可靠识别目标显示器，请重新检测。");
  var matches=Read().Where(d=>string.Equals(d.StableKey,previous.StableKey,StringComparison.OrdinalIgnoreCase)).ToList();if(matches.Count!=1)throw new Exception("显示器连接已变化，请重新检测后重试。");return matches[0];
 }
 public void SetHdr(bool enable){var fresh=Resolve(this);if(!fresh.CanHdr)throw new Exception(fresh.Clone?fresh.Warning:fresh.HdrStatus);var state=new DisplayApi.ColorState{Header=DisplayApi.MakeHeader<DisplayApi.ColorState>(fresh.NewColorApi?16u:10u,fresh.Path.Target.Adapter,fresh.Path.Target.Id),Enable=enable?1u:0u};int rc=DisplayApi.SetColor(ref state);if(rc!=0)throw new Win32Exception(rc);for(int attempt=0;attempt<20;attempt++){fresh.ReadHdr();if(fresh.HdrKnown&&fresh.HdrEnabled==enable)return;System.Threading.Thread.Sleep(100);}throw new Exception("系统没有确认 HDR 状态变化，请检查 Windows 显示设置。");}
 public string Information(){
  string mode=HasMode?Current.Width+" × "+Current.Height+"  /  "+(SignalHz>0?SignalHz.ToString("0.###"):Current.Frequency.ToString())+" Hz":"当前显示模式不可用";
  string color=BitsPerChannel>0?BitsPerChannel+" 位/通道 · "+(Encoding==0?"RGB":Encoding==1?"YCbCr 4:4:4":Encoding==2?"YCbCr 4:2:2":Encoding==3?"YCbCr 4:2:0":"其他色彩编码"):"位深/色彩格式未报告";
  return Name+"\n"+mode+"\n"+Connection+(Primary?" · 主显示器":" · 非主显示器")+"\n"+color+"\n显卡："+(string.IsNullOrEmpty(Gpu)?"未报告":Gpu)+"\n设备："+DeviceName+(IdentityInfo.Length>0?"\n"+IdentityInfo:"");
 }
}
// A preview never persists settings before confirmation. Dispose restores on every non-commit path.
class ModePreview : IDisposable {
 public delegate int Change(string device,DisplayApi.DevMode mode,uint flags);
 readonly string device;readonly DisplayApi.DevMode original;readonly Change change;bool applied,committed;public string RollbackError;
 public ModePreview(string device,DisplayApi.DevMode original,Change change){this.device=device;this.original=original;this.change=change;}
 public void Apply(DisplayApi.DevMode selected){selected.Fields=0x7c0000;selected.DriverExtra=0;DisplayApi.RequireModeSuccess(change(device,selected,2));applied=true;DisplayApi.RequireModeSuccess(change(device,selected,0));}
 public void Commit(DisplayApi.DevMode selected){selected.Fields=0x7c0000;selected.DriverExtra=0;DisplayApi.RequireModeSuccess(change(device,selected,0x10000001));committed=true;}
 public void Dispose(){if(!applied||committed)return;var restore=original;restore.Fields=0x7c0000;restore.DriverExtra=0;int rc=change(device,restore,1);if(rc!=0)RollbackError="原显示模式恢复失败："+DisplayApi.Error(rc);applied=false;}
 public static int NativeChange(string device,DisplayApi.DevMode mode,uint flags){return DisplayApi.ChangeMode(device,ref mode,IntPtr.Zero,flags,IntPtr.Zero);}
}
}
