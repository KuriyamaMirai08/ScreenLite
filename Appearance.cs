using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenLite {
static class Appearance {
 public static bool Dark;
 public static Color Background {get{return Dark?Color.FromArgb(15,24,32):Color.FromArgb(241,245,247);}}
 public static Color Surface {get{return Dark?Color.FromArgb(25,38,49):Color.White;}}
 public static Color Text {get{return Dark?Color.FromArgb(237,245,247):Color.FromArgb(25,43,54);}}
 public static Color Muted {get{return Dark?Color.FromArgb(151,174,187):Color.FromArgb(103,124,137);}}
 public static Color Border {get{return Dark?Color.FromArgb(44,62,75):Color.FromArgb(225,233,237);}}
 public static Color Accent {get{return Dark?Color.FromArgb(101,223,194):Color.FromArgb(0,127,112);}}
 public static Color Tint {get{return Dark?Color.FromArgb(29,60,59):Color.FromArgb(228,245,240);}}
 public static float Scale(Control c){return c.DeviceDpi/96f;}
 public static int Px(Control c,float value){return (int)Math.Round(value*Scale(c));}
 static string ThemePath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenLite","theme.txt");}}
 public static void Load(){try{Dark=File.ReadAllText(ThemePath).Trim()=="dark";}catch{}}
 public static void Save(){Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));File.WriteAllText(ThemePath,Dark?"dark":"light");}
 public static Label Label(string text,int x,int y,int w,int h,float size,bool bold,bool muted){return new Label{Text=text,Bounds=new Rectangle(x,y,w,h),Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular),Tag=muted?"muted":null,UseCompatibleTextRendering=false};}
 public static Button Button(string text,int x,int y,int w,int h,bool primary){return new SoftButton{Text=text,Bounds=new Rectangle(x,y,w,h),Cursor=Cursors.Hand,Tag=primary?"primary":null,UseVisualStyleBackColor=false};}
 public static void Apply(Control root){
  root.ForeColor=root.Tag as string=="muted"?Muted:root.Tag as string=="accent"?Accent:Text;
  var button=root as Button;
  if(button!=null){button.BackColor=button.Tag as string=="primary"?Accent:Surface;button.ForeColor=button.Tag as string=="primary"?(Dark?Color.FromArgb(17,44,38):Color.White):Text;}
  else if(root is ComboBox||root is Card){root.BackColor=Surface;}
  else if(root is Form||root is QuickPanel){root.BackColor=Background;}
  else root.BackColor=root.Parent!=null?root.Parent.BackColor:Background;
  foreach(Control c in root.Controls)Apply(c);var form=root as Form;if(form!=null&&form.IsHandleCreated)WindowChrome(form);root.Invalidate();
 }
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
 public static void WindowChrome(Form form){try{int dark=Dark?1:0;DwmSetWindowAttribute(form.Handle,20,ref dark,4);int corners=2;DwmSetWindowAttribute(form.Handle,33,ref corners,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
 public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 public static void DrawMark(Graphics g,Rectangle bounds){var state=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(bounds.X,bounds.Y);g.ScaleTransform(bounds.Width/64f,bounds.Height/64f);using(var brush=new LinearGradientBrush(new Point(4,4),new Point(60,60),Color.FromArgb(12,164,142),Color.FromArgb(0,115,111)))using(var path=Round(new RectangleF(2,2,60,60),16))g.FillPath(brush,path);using(var pen=new Pen(Color.White,2.8f)){pen.LineJoin=LineJoin.Round;g.DrawRectangle(pen,14,18,36,25);g.DrawLine(pen,32,43,32,49);g.DrawLine(pen,24,50,40,50);}using(var brush=new SolidBrush(Color.FromArgb(174,245,216)))g.FillEllipse(brush,26,24,12,12);g.Restore(state);}
 public static Bitmap Mark(int size){var bitmap=new Bitmap(size,size);using(var g=Graphics.FromImage(bitmap))DrawMark(g,new Rectangle(0,0,size,size));return bitmap;}
 public static Icon AppIcon(){return Icon.ExtractAssociatedIcon(Application.ExecutablePath);}
 public static void CopyText(string text){if(string.IsNullOrEmpty(text))return;try{Clipboard.SetText(text);}catch(ExternalException){MessageBox.Show("剪贴板正忙，请稍后再试。","轻屏",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
}
// WinForms owns geometry/font scaling; custom drawing uses DeviceDpi for stroke and hit-test metrics.
class AppWindow : Form {
 public Size DesignCanvas;
 public AppWindow(){AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;DoubleBuffered=true;}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Appearance.WindowChrome(this);}
 protected override void OnShown(EventArgs e){base.OnShown(e);FitToScreen();}
 protected override void OnDpiChanged(DpiChangedEventArgs e){base.OnDpiChanged(e);if(IsHandleCreated)BeginInvoke((Action)(()=>{if(!IsDisposed){FitToScreen();RefreshDpi(this);Invalidate(true);}}));}
 static void RefreshDpi(Control c){var combo=c as ThemeCombo;if(combo!=null)combo.RefreshMetrics();foreach(Control child in c.Controls)RefreshDpi(child);}
 public void FitToScreen(){if(DesignCanvas.IsEmpty)return;var area=Screen.FromHandle(Handle).WorkingArea;int width=Appearance.Px(this,DesignCanvas.Width),height=Appearance.Px(this,DesignCanvas.Height);int border=Width-ClientSize.Width,caption=Height-ClientSize.Height;int margin=Appearance.Px(this,12);if(AutoScroll)AutoScrollMinSize=new Size(width,height);ClientSize=new Size(Math.Min(width,Math.Max(160,area.Width-border-margin*2)),Math.Min(height,Math.Max(100,area.Height-caption-margin*2)));Left=Math.Max(area.Left,Math.Min(Left,area.Right-Width));Top=Math.Max(area.Top,Math.Min(Top,area.Bottom-Height));}
}
class Brand : Control {
 public Brand(){SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint,true);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Appearance.DrawMark(e.Graphics,ClientRectangle);}
}
class Card : Panel {
 public Card(){DoubleBuffered=true;}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent!=null?Parent.BackColor:Appearance.Background);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float s=Appearance.Scale(this);using(var shadow=Appearance.Round(new RectangleF(s,2*s,Width-2*s,Height-3*s),15*s))using(var b=new SolidBrush(Appearance.Dark?Color.FromArgb(10,17,23):Color.FromArgb(229,235,239)))g.FillPath(b,shadow);using(var path=Appearance.Round(new RectangleF(.5f*s,.5f*s,Width-s,Height-3*s),14*s)){using(var brush=new SolidBrush(Appearance.Surface))g.FillPath(brush,path);using(var pen=new Pen(Appearance.Border,Math.Max(1,s)))g.DrawPath(pen,path);}base.OnPaint(e);}
}
class SoftButton : Button {
 bool hover,down;
 public SoftButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent!=null?Parent.BackColor:Appearance.Background);g.SmoothingMode=SmoothingMode.AntiAlias;float s=Appearance.Scale(this);bool primary=Tag as string=="primary";Color fill=primary?Appearance.Accent:Appearance.Surface;if(hover||down)fill=primary?(Appearance.Dark?Color.FromArgb(127,235,211):Color.FromArgb(0,109,98)):Appearance.Tint;if(!Enabled)fill=Appearance.Background;using(var path=Appearance.Round(new RectangleF(s/2,s/2,Width-s,Height-s),9*s)){using(var brush=new SolidBrush(fill))g.FillPath(brush,path);if(!primary)using(var pen=new Pen(Appearance.Border,s))g.DrawPath(pen,path);}var color=!Enabled?Appearance.Muted:primary?(Appearance.Dark?Color.FromArgb(17,44,38):Color.White):Appearance.Text;TextRenderer.DrawText(g,Text,Font,ClientRectangle,color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle((int)(4*s),(int)(4*s),Width-(int)(8*s),Height-(int)(8*s)),Appearance.Accent,fill);}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=false;down=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){down=true;Invalidate();base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
}
class ThemeCombo : ComboBox {
 public ThemeCombo(){DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=26;FlatStyle=FlatStyle.Flat;DropDownStyle=ComboBoxStyle.DropDownList;}
 public void RefreshMetrics(){ItemHeight=Appearance.Px(this,26);Invalidate();}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);RefreshMetrics();}
 protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);RefreshMetrics();}
 protected override void OnDrawItem(DrawItemEventArgs e){if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0&&(e.State&DrawItemState.ComboBoxEdit)==0;using(var brush=new SolidBrush(selected?Appearance.Tint:Appearance.Surface))e.Graphics.FillRectangle(brush,e.Bounds);int pad=Appearance.Px(this,9);TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,new Rectangle(e.Bounds.X+pad,e.Bounds.Y,e.Bounds.Width-pad*2,e.Bounds.Height),Enabled?Appearance.Text:Appearance.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);}
 protected override void WndProc(ref Message m){base.WndProc(ref m);if((m.Msg==0xf||m.Msg==0x317||m.Msg==0x318)&&IsHandleCreated){using(var g=m.Msg==0xf||m.WParam==IntPtr.Zero?Graphics.FromHwnd(Handle):Graphics.FromHdc(m.WParam)){float s=Appearance.Scale(this);int w=Appearance.Px(this,25);using(var brush=new SolidBrush(Appearance.Surface))g.FillRectangle(brush,Width-w,1,w-1,Height-2);using(var pen=new Pen(Appearance.Border,s))g.DrawRectangle(pen,0,0,Width-1,Height-1);g.SmoothingMode=SmoothingMode.AntiAlias;using(var pen=new Pen(Enabled?Appearance.Muted:Appearance.Border,1.5f*s)){float x=Width-w/2f,y=Height/2f;g.DrawLines(pen,new[]{new PointF(x-4*s,y-2*s),new PointF(x,y+2*s),new PointF(x+4*s,y-2*s)});}}}}
}
class LevelSlider : Control {
 int value,maximum=100;bool dragging;
 public event EventHandler Scroll;
 public int Maximum {get{return maximum;}set{maximum=Math.Max(1,value);Value=this.value;}}
 public int TickFrequency {get;set;}
 public TickStyle TickStyle {get;set;}
 public int Value {get{return value;}set{this.value=Math.Max(0,Math.Min(maximum,value));AccessibleDescription=this.value+"%";Invalidate();}}
 public LevelSlider(){Height=40;TabStop=true;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.Slider;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float s=Appearance.Scale(this),pad=14*s,x=pad+(Width-pad*2)*Value/(float)Maximum,y=Height/2f;using(var track=new Pen(Appearance.Border,6*s)){track.StartCap=track.EndCap=LineCap.Round;g.DrawLine(track,pad,y,Width-pad,y);}if(Enabled){using(var fill=new Pen(Appearance.Accent,6*s)){fill.StartCap=fill.EndCap=LineCap.Round;g.DrawLine(fill,pad,y,x,y);}using(var shadow=new SolidBrush(Appearance.Tint))g.FillEllipse(shadow,x-12*s,y-12*s,24*s,24*s);using(var knob=new SolidBrush(Appearance.Accent))g.FillEllipse(knob,x-8*s,y-8*s,16*s,16*s);using(var center=new SolidBrush(Appearance.Surface))g.FillEllipse(center,x-3*s,y-3*s,6*s,6*s);}else using(var brush=new SolidBrush(Appearance.Muted))g.FillEllipse(brush,x-5*s,y-5*s,10*s,10*s);if(Focused)ControlPaint.DrawFocusRectangle(g,new Rectangle((int)(2*s),(int)(2*s),Width-(int)(4*s),Height-(int)(4*s)),Appearance.Accent,BackColor);}
 void SetFromMouse(int x){float pad=Appearance.Px(this,14);Value=(int)Math.Round((x-pad)*Maximum/(double)Math.Max(1,Width-pad*2));if(Scroll!=null)Scroll(this,EventArgs.Empty);}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();dragging=true;Capture=true;SetFromMouse(e.X);}}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)SetFromMouse(e.X);}
 protected override void OnMouseUp(MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;dragging=false;Capture=false;base.OnMouseUp(e);}
 protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)dragging=false;base.OnMouseCaptureChanged(e);}
 protected override bool IsInputKey(Keys k){return k==Keys.Left||k==Keys.Right||k==Keys.Up||k==Keys.Down||k==Keys.Home||k==Keys.End||base.IsInputKey(k);}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Home)Value=0;else if(e.KeyCode==Keys.End)Value=Maximum;else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Up)Value++;else if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Down)Value--;else return;e.Handled=true;if(Scroll!=null)Scroll(this,EventArgs.Empty);}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
}
class DisplayInfoView : Control {
 int offset,contentHeight;bool dragging;int startY,startOffset;
 public DisplayInfoView(){TabStop=true;AccessibleRole=AccessibleRole.Text;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);}
 int ContentWidth {get{return Math.Max(10,Width-Appearance.Px(this,16));}}
 void Measure(){contentHeight=TextRenderer.MeasureText(Text,Font,new Size(ContentWidth,100000),TextFormatFlags.WordBreak|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Height;offset=Math.Max(0,Math.Min(offset,Math.Max(0,contentHeight-Height)));}
 protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);offset=0;AccessibleDescription=Text;Invalidate();}
 protected override void OnResize(EventArgs e){base.OnResize(e);Invalidate();}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Measure();var state=e.Graphics.Save();e.Graphics.SetClip(new Rectangle(0,0,ContentWidth,Height));TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(0,-offset,ContentWidth,Math.Max(contentHeight,Height)),Appearance.Text,TextFormatFlags.WordBreak|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.PreserveGraphicsClipping);e.Graphics.Restore(state);if(contentHeight>Height){float s=Appearance.Scale(this),thumb=Math.Max(22*s,Height*(float)Height/contentHeight),top=(Height-thumb)*offset/Math.Max(1,contentHeight-Height);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var track=new Pen(Appearance.Border,3*s)){track.StartCap=track.EndCap=LineCap.Round;e.Graphics.DrawLine(track,Width-4*s,2*s,Width-4*s,Height-2*s);}using(var pen=new Pen(Appearance.Muted,3*s)){pen.StartCap=pen.EndCap=LineCap.Round;e.Graphics.DrawLine(pen,Width-4*s,top+2*s,Width-4*s,top+thumb-2*s);}}}
 void ScrollTo(int value){offset=Math.Max(0,Math.Min(value,Math.Max(0,contentHeight-Height)));Invalidate();}
 protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);Measure();ScrollTo(offset-e.Delta*Appearance.Px(this,48)/120);}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();Measure();if(e.Button==MouseButtons.Left&&e.X>ContentWidth){dragging=true;Capture=true;startY=e.Y;startOffset=offset;}}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)ScrollTo(startOffset+(e.Y-startY)*contentHeight/Math.Max(1,Height));}
 protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;base.OnMouseUp(e);}
 protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)dragging=false;base.OnMouseCaptureChanged(e);}
 protected override bool IsInputKey(Keys k){return k==Keys.Down||k==Keys.Up||k==Keys.PageDown||k==Keys.PageUp||k==Keys.Home||k==Keys.End||base.IsInputKey(k);}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);Measure();if(e.Control&&e.KeyCode==Keys.C){Appearance.CopyText(Text);e.Handled=true;e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Down)ScrollTo(offset+Appearance.Px(this,20));else if(e.KeyCode==Keys.Up)ScrollTo(offset-Appearance.Px(this,20));else if(e.KeyCode==Keys.PageDown)ScrollTo(offset+Height);else if(e.KeyCode==Keys.PageUp)ScrollTo(offset-Height);else if(e.KeyCode==Keys.Home)ScrollTo(0);else if(e.KeyCode==Keys.End)ScrollTo(contentHeight);}
}
}

