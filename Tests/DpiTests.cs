using System;using System.Drawing;using System.Reflection;using System.Runtime.InteropServices;using System.Windows.Forms;using ScreenLite;
class DpiTests {
 [DllImport("user32.dll")]static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
 [DllImport("user32.dll")]static extern bool AreDpiAwarenessContextsEqual(IntPtr a,IntPtr b);
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int Left,Top,Right,Bottom;}
 static BindingFlags f=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static void Pump(){for(int i=0;i<4;i++){Application.DoEvents();System.Threading.Thread.Sleep(15);}}
 static void Dpi(Form window,int dpi){var rect=new Rect{Left=40,Top=40,Right=40+(int)(1100*dpi/96.0),Bottom=40+(int)(780*dpi/96.0)};IntPtr memory=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Rect)));try{Marshal.StructureToPtr(rect,memory,false);SendMessage(window.Handle,0x2e0,new IntPtr(dpi|(dpi<<16)),memory);Pump();}finally{Marshal.FreeHGlobal(memory);}}
 [STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var main=new MainForm()){
 var events=(System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component).GetProperty("Events",f).GetValue(main,null);var key=typeof(Form).GetField("EVENT_SHOWN",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);events.RemoveHandler(key,events[key]);main.Show();Pump();
 Assert(AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(main.Handle),new IntPtr(-4)),"Per-monitor V2 was not enabled");int original=main.DeviceDpi;var brand=main.Controls[0];Assert(Math.Abs(brand.Width-58*original/96.0)<2,"Initial geometry did not scale");var panel=(Control)typeof(MainForm).GetField("displaySettings",f).GetValue(main);Assert(Math.Abs(panel.Width-552*original/96.0)<2,"Nested settings panel did not scale");Console.WriteLine("PASS: native PMv2 context; actual DPI="+original+"; initial/nested geometry scaled");
 foreach(int dpi in new[]{144,192,96,original}){Dpi(main,dpi);Console.WriteLine("Synthetic notification "+dpi+": DeviceDpi="+main.DeviceDpi+", brand="+brand.Bounds+", panel="+panel.Bounds);Assert(main.DeviceDpi==dpi,"DPI notification not handled");Assert(Math.Abs(brand.Width-58*dpi/96.0)<3,"Brand geometry drift");Assert(Math.Abs(panel.Width-552*dpi/96.0)<3,"Nested panel geometry drift");}
 var quick=(QuickPopup)typeof(MainForm).GetField("popup",f).GetValue(main);var area=Screen.PrimaryScreen.WorkingArea;quick.OpenAt(new Point(area.Right-8,area.Bottom-8),area);Pump();Assert(area.Contains(quick.Bounds),"Popup is outside working area");using(var other=new Form()){other.Show();other.Activate();Pump();Assert(!quick.Visible,"Popup did not hide on focus loss");}
 Console.WriteLine("PASS: synthetic 150/200/100% DPI transitions, return, popup bounds, deactivation. No display settings changed.");}
 }
}
