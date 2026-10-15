using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Kedit.Console {
 internal sealed class ColumnFillWindow : Window {
  readonly string folder, source;
  readonly TextBox editor = new TextBox();
  readonly TextBlock status = new TextBlock();
  readonly CheckBox skip = new CheckBox { Content="列编辑跳过空白行", Foreground=Brushes.WhiteSmoke };
  readonly Stack<State> undo = new Stack<State>(), redo = new Stack<State>();
  SelectionOverlay overlay;
  bool rectangle, dragging, changing;
  int ar, br, ac, bc;
  string previous, cachedText;
  string[] cachedLines;
  int[] cachedStarts;
  const int TabSize=4;
  sealed class State {
   internal string Text; internal int Start, Length, A, B, C, D; internal bool Rectangle;
  }
  internal ColumnFillWindow(string directory) {
   folder=directory; source=File.ReadAllText(Path.Combine(folder,"source.txt"));
   Title="Kedit 列编辑"; Width=1000; Height=700; MinWidth=700; MinHeight=450;
   WindowStartupLocation=WindowStartupLocation.CenterScreen;
   Background=new SolidColorBrush(Color.FromRgb(25,29,38)); Foreground=Brushes.WhiteSmoke;
   var root=new DockPanel { Margin=new Thickness(18) }; Content=root;
   var header=new StackPanel { Margin=new Thickness(0,0,0,12) };
   DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
   header.Children.Add(new TextBlock { Text="直接编辑文本 · Alt + 鼠标拖动选择矩形 · 竖线同时插入，矩形替换", Margin=new Thickness(0,0,0,8) });
   header.Children.Add(skip);
   var footer=new DockPanel { Margin=new Thickness(0,12,0,0) };
   DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
   var buttons=new StackPanel { Orientation=Orientation.Horizontal };
   DockPanel.SetDock(buttons,Dock.Right); footer.Children.Add(buttons);
   var confirm=new Button { Content="确认写回  Ctrl+Enter", Padding=new Thickness(16,8,16,8) };
   var cancel=new Button { Content="取消", Padding=new Thickness(16,8,16,8), Margin=new Thickness(10,0,0,0), IsCancel=true };
   buttons.Children.Add(confirm); buttons.Children.Add(cancel); footer.Children.Add(status);
   confirm.Click+=delegate { Save(); }; cancel.Click+=delegate { Close(); };
   editor.AcceptsReturn=true; editor.AcceptsTab=true; editor.TextWrapping=TextWrapping.NoWrap;
   editor.VerticalScrollBarVisibility=ScrollBarVisibility.Auto; editor.HorizontalScrollBarVisibility=ScrollBarVisibility.Auto;
   editor.Background=new SolidColorBrush(Color.FromRgb(16,21,29)); editor.Foreground=Brushes.WhiteSmoke;
   editor.CaretBrush=Brushes.White; editor.SelectionBrush=Brushes.SteelBlue;
   editor.FontFamily=new FontFamily("Consolas"); editor.FontSize=15; editor.Padding=new Thickness(10);
   editor.IsUndoEnabled=false; editor.Text=source; previous=source;
   var decorator=new AdornerDecorator { Child=editor }; root.Children.Add(decorator);
   Loaded+=delegate {
    overlay=new SelectionOverlay(editor,this);
    AdornerLayer.GetAdornerLayer(editor).Add(overlay); editor.Focus(); Refresh();
   };
   editor.TextChanged+=delegate {
    if(!changing) {
     var state=Capture(); state.Text=previous; state.Start=Math.Min(state.Start,previous.Length); state.Length=0;
     undo.Push(state); redo.Clear(); rectangle=false;
    }
    previous=editor.Text; Refresh();
   };
   editor.SelectionChanged+=delegate { Refresh(); };
   editor.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler(delegate { Refresh(); }));
   editor.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {
    // Scrollbar interaction belongs to the viewport, not the text selection.
    DependencyObject target=e.OriginalSource as DependencyObject;
    while(target!=null && target!=editor) {
     if(target is System.Windows.Controls.Primitives.ScrollBar)return;
     target=target is Visual?VisualTreeHelper.GetParent(target):null;
    }
    if((Keyboard.Modifiers&ModifierKeys.Alt)==0) {
     rectangle=false;
     if(e.ClickCount==2 && Keyboard.Modifiers==ModifierKeys.None) {
      int index=editor.GetCharacterIndexFromPoint(e.GetPosition(editor),false);
      int start,length;
      if(IdentifierRange(editor.Text,index,out start,out length)){editor.Focus();editor.Select(start,length);e.Handled=true;}
     }
     Refresh(); return;
    }
    BeginRectangle(e.GetPosition(editor));
    dragging=true; editor.CaptureMouse(); Refresh(); e.Handled=true;
   };
   editor.PreviewMouseMove+=delegate(object sender,MouseEventArgs e) {
    if(!dragging)return;
    Point p=e.GetPosition(editor);
    Rect bounds=TextViewport();
    if(p.Y<bounds.Top)editor.ScrollToVerticalOffset(Math.Max(0,editor.VerticalOffset-22));
    if(p.Y>bounds.Bottom)editor.ScrollToVerticalOffset(editor.VerticalOffset+22);
    if(p.X<bounds.Left)editor.ScrollToHorizontalOffset(Math.Max(0,editor.HorizontalOffset-20));
    if(p.X>bounds.Right)editor.ScrollToHorizontalOffset(editor.HorizontalOffset+20);
    editor.UpdateLayout();
    Hit(p,out br,out bc); Refresh(); e.Handled=true;
   };
   editor.PreviewMouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e) {
    if(!dragging)return; dragging=false; editor.ReleaseMouseCapture(); e.Handled=true;
   };
   editor.LostMouseCapture+=delegate { dragging=false; };
   editor.PreviewTextInput+=delegate(object sender,TextCompositionEventArgs e) {
    if(rectangle && !string.IsNullOrEmpty(e.Text)) { ApplyRectangle(e.Text,0); e.Handled=true; }
   };
   DataObject.AddPastingHandler(editor,delegate(object sender,DataObjectPastingEventArgs e) {
    if(!rectangle)return;
    if(e.DataObject.GetDataPresent(DataFormats.UnicodeText)) {
     string text=e.DataObject.GetData(DataFormats.UnicodeText) as string;
     if(text!=null && text.IndexOfAny(new[]{'\r','\n'})<0)ApplyRectangle(text,0);
     else status.Text="列选区仅支持单行粘贴；点击文本后可正常粘贴多行。";
    }
    e.CancelCommand();
   });
   editor.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,delegate { Restore(undo,redo); },delegate(object s,CanExecuteRoutedEventArgs e){e.CanExecute=undo.Count>0;e.Handled=true;}));
   editor.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,delegate { Restore(redo,undo); },delegate(object s,CanExecuteRoutedEventArgs e){e.CanExecute=redo.Count>0;e.Handled=true;}));
   PreviewKeyDown+=OnKey;
   Refresh();
  }
  Rect TextViewport() {
   var host=editor.Template.FindName("PART_ContentHost",editor) as FrameworkElement;
   if(host==null)return new Rect(0,0,editor.ActualWidth,editor.ActualHeight);
   var scroll=host as ScrollViewer;
   Point origin=host.TranslatePoint(new Point(0,0),editor);
   return new Rect(origin.X,origin.Y,scroll==null?host.ActualWidth:scroll.ViewportWidth,scroll==null?host.ActualHeight:scroll.ViewportHeight);
  }
  // TextBox caret visibility requests must not override the rectangle's viewport.
  void PreserveView(Action edit) {
   double x=editor.HorizontalOffset,y=editor.VerticalOffset;
   RequestBringIntoViewEventHandler suppress=delegate(object sender,RequestBringIntoViewEventArgs e){e.Handled=true;};
   editor.AddHandler(FrameworkElement.RequestBringIntoViewEvent,suppress,true);
   try { edit();editor.UpdateLayout();editor.ScrollToHorizontalOffset(x);editor.ScrollToVerticalOffset(y);editor.UpdateLayout(); }
   finally { editor.RemoveHandler(FrameworkElement.RequestBringIntoViewEvent,suppress); }
  }
  internal void BeginRectangle(Point point) {
   Hit(point,out ar,out ac);br=ar;bc=ac;rectangle=true;
   PreserveView(delegate { editor.Focus();editor.Select(IndexAt(ar,ac),0); });
   Refresh();
  }
  internal static bool IdentifierRange(string text,int index,out int start,out int length) {
   start=index;length=0;
   if(index<0||index>=text.Length||!IdentifierChar(text[index]))return false;
   int end=index+1;
   while(start>0&&IdentifierChar(text[start-1]))start--;
   while(end<text.Length&&IdentifierChar(text[end]))end++;
   length=end-start;return true;
  }
  static bool IdentifierChar(char c) { return c=='_'||c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'; }
  internal void MoveRectangle(int dx,int dy) {
   if(!rectangle)return;
   dx=Math.Max(-Math.Min(ac,bc),dx);
   dy=Math.Max(-Math.Min(ar,br),Math.Min(Lines().Length-1-Math.Max(ar,br),dy));
   ac+=dx;bc+=dx;ar+=dy;br+=dy;
   PreserveView(delegate { editor.Select(IndexAt(br,bc),0); });
   FollowRectangle(dx,dy);
   Refresh();
  }
  void FollowRectangle(int dx,int dy) {
   editor.UpdateLayout();
   int row=dy<0?Math.Min(ar,br):dy>0?Math.Max(ar,br):br;
   Rect bounds=TextViewport();
   Rect caret=editor.GetRectFromCharacterIndex(IndexAt(row,bc));
   if(caret.IsEmpty || caret.Top<bounds.Top || caret.Bottom>bounds.Bottom) {
    editor.ScrollToLine(row);editor.UpdateLayout();
   }
   bounds=TextViewport();
   int column=dx<0?Math.Min(ac,bc):dx>0?Math.Max(ac,bc):bc;
   double x=XAt(row,column),padding=Math.Min(12,bounds.Width/4);
   if(x<bounds.Left+padding)
    editor.ScrollToHorizontalOffset(Math.Max(0,editor.HorizontalOffset+x-bounds.Left-padding));
   else if(x>bounds.Right-padding)
    editor.ScrollToHorizontalOffset(editor.HorizontalOffset+x-bounds.Right+padding);
   editor.UpdateLayout();
  }
  void OnKey(object sender,KeyEventArgs e) {
   Key key=e.Key==Key.System?e.SystemKey:e.Key;
   bool ctrl=(Keyboard.Modifiers&ModifierKeys.Control)!=0;
   if(ctrl&&key==Key.Enter) { Save(); e.Handled=true; return; }
   if(!editor.IsKeyboardFocusWithin)return;
   if(ctrl&&(key==Key.Z||key==Key.Y)) { if(key==Key.Y||(Keyboard.Modifiers&ModifierKeys.Shift)!=0)Restore(redo,undo);else Restore(undo,redo); e.Handled=true; return; }
   if(!rectangle)return;
   if(Keyboard.Modifiers==ModifierKeys.None && (key==Key.Left||key==Key.Right||key==Key.Up||key==Key.Down)) {
    MoveRectangle(key==Key.Left?-1:key==Key.Right?1:0,key==Key.Up?-1:key==Key.Down?1:0);
    e.Handled=true;return;
   }
   if(ctrl&&(key==Key.C||key==Key.X)) {
    if(ac==bc){e.Handled=true;return;}
    try { Clipboard.SetText(CopyRectangle(editor.Text,Math.Min(ar,br),Math.Max(ar,br),Math.Min(ac,bc),Math.Max(ac,bc))); if(key==Key.X)ApplyRectangle("",0); }
    catch(Exception ex) { status.Text="剪贴板暂不可用："+ex.Message; }
    e.Handled=true;return;
   }
   if(ctrl&&key==Key.A) { rectangle=false;Refresh();return; }
   if(key==Key.Back||key==Key.Delete) { ApplyRectangle("",key==Key.Back?-1:1); e.Handled=true; }
   else if(key==Key.Tab) { ApplyRectangle(new string(' ',TabSize-Math.Min(ac,bc)%TabSize),0); e.Handled=true; }
   else if(key==Key.Enter) {
    // Enter is an ordinary newline at the active end, never confirmation.
    int index=IndexAt(br,bc); rectangle=false; editor.Select(index,0); Refresh();
   }
   else if(key==Key.Left||key==Key.Right||key==Key.Up||key==Key.Down||key==Key.Home||key==Key.End) {
    int index=IndexAt(br,bc); rectangle=false; editor.Select(index,0); Refresh();
   }
  }
  State Capture() { return new State { Text=editor.Text,Start=editor.SelectionStart,Length=editor.SelectionLength,Rectangle=rectangle,A=ar,B=br,C=ac,D=bc }; }
  void Restore(Stack<State> from,Stack<State> to) {
   if(from.Count==0)return; to.Push(Capture()); State s=from.Pop(); changing=true;
   try { PreserveView(delegate { editor.Text=s.Text; editor.Select(Math.Min(s.Start,s.Text.Length),Math.Min(s.Length,s.Text.Length-Math.Min(s.Start,s.Text.Length)));
    rectangle=s.Rectangle;ar=s.A;br=s.B;ac=s.C;bc=s.D;previous=s.Text; });
   } finally { changing=false; } Refresh();
  }
  internal void ApplyRectangle(string text,int deletion) {
   int left=Math.Min(ac,bc),right=Math.Max(ac,bc),count;
   if(left==right && deletion<0) { if(left==0)return; left--; }
   else if(left==right && deletion>0)right++;
   string result=Transform(editor.Text,text,Math.Min(ar,br),Math.Max(ar,br),left,right,TabSize,skip.IsChecked==true,out count);
   if(result==editor.Text) {
    // Replacing a selection with identical text still collapses it for subsequent typing.
    if(left!=right && text.Length>0){ac=bc=VisualColumn(text,left);Refresh();}
    return;
   }
   undo.Push(Capture());redo.Clear();changing=true;
   try { PreserveView(delegate { editor.Text=result;previous=result;ac=bc=VisualColumn(text,left);editor.Select(IndexAt(br,bc),0); }); } finally { changing=false; }
   Refresh();
  }
  static int VisualColumn(string text,int column) { foreach(char c in text)column+=c=='\t'?TabSize-column%TabSize:1; return column; }
  string[] Lines() {
   if(cachedText!=editor.Text) {
    cachedText=editor.Text;cachedLines=Regex.Split(cachedText,"\r\n|\n|\r");
    cachedStarts=new int[cachedLines.Length];var matches=Regex.Matches(cachedText,"\r\n|\n|\r");
    for(int i=0;i<matches.Count;i++)cachedStarts[i+1]=matches[i].Index+matches[i].Length;
   }return cachedLines;
  }
  int StartOf(int row) { Lines(); return cachedStarts[Math.Max(0,Math.Min(row,cachedStarts.Length-1))]; }
  int IndexAt(int row,int col) {
   string[] lines=Lines();row=Math.Max(0,Math.Min(row,lines.Length-1));int width=0,i=0;
   while(i<lines[row].Length&&width<col){width=VisualColumn(lines[row][i].ToString(),width);i++;}
   return StartOf(row)+i;
  }
  double CellWidth { get { return new FormattedText("M",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(editor.FontFamily,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal),editor.FontSize,Brushes.White,VisualTreeHelper.GetDpi(editor).PixelsPerDip).WidthIncludingTrailingWhitespace; } }
  // Use actual TextBox character rectangles, including its rendered tab widths.
  double XAt(int row,int column) {
   string line=Lines()[row];int start=StartOf(row),width=0;
   for(int i=0;i<line.Length;i++) {
    int next=VisualColumn(line[i].ToString(),width);
    if(column<=next) {
     Rect a=editor.GetRectFromCharacterIndex(start+i),b=editor.GetRectFromCharacterIndex(start+i+1);
     if(a.IsEmpty||b.IsEmpty)return 10-editor.HorizontalOffset+column*CellWidth;
     return a.X+(b.X-a.X)*(column-width)/(next-width);
    } width=next;
   }
   Rect end=editor.GetRectFromCharacterIndex(start+line.Length);
   return (end.IsEmpty?10-editor.HorizontalOffset:end.X)+(column-width)*CellWidth;
  }
  void Hit(Point point,out int row,out int col) {
   int index=editor.GetCharacterIndexFromPoint(point,true);if(index<0)index=editor.Text.Length;
   row=Regex.Matches(editor.Text.Substring(0,Math.Min(index,editor.Text.Length)),"\r\n|\n|\r").Count;
   string line=Lines()[row]; int max=VisualColumn(line,0)+Math.Max(10,(int)(editor.ActualWidth/CellWidth));
   int lo=0,hi=max;while(lo<hi){int mid=(lo+hi)/2;if(point.X>(XAt(row,mid)+XAt(row,mid+1))/2)lo=mid+1;else hi=mid;}col=lo;
  }
  void Refresh() {
   if(overlay!=null)overlay.InvalidateVisual();
   status.Text=rectangle?"列选区："+(Math.Min(ar,br)+1)+"～"+(Math.Max(ar,br)+1)+" 行 · "+(Math.Abs(ac-bc)==0?"同时插入":"替换 "+Math.Abs(ac-bc)+" 列"):"普通编辑 · Enter 换行 · Ctrl+Z 撤销";
  }
  void Save() {
   try {
    if(File.Exists(Path.Combine(folder,"accepted")))File.Delete(Path.Combine(folder,"accepted"));
    File.WriteAllText(Path.Combine(folder,"result.txt"),editor.Text,new UTF8Encoding(false));
    File.WriteAllText(Path.Combine(folder,"tab.txt"),TabSize.ToString());
    File.WriteAllText(Path.Combine(folder,"accepted"),editor.Text==source?"0":"1");
    Close();
   } catch(Exception ex) { MessageBox.Show(this,"保存编辑结果失败："+ex.Message,"列编辑"); }
  }
  sealed class SelectionOverlay : Adorner {
   readonly ColumnFillWindow owner;
   internal SelectionOverlay(TextBox box,ColumnFillWindow window):base(box){owner=window;IsHitTestVisible=false;}
   protected override void OnRender(DrawingContext dc) {
    if(!owner.rectangle)return;
    dc.PushClip(new RectangleGeometry(new Rect(2,2,Math.Max(0,ActualWidth-20),Math.Max(0,ActualHeight-20))));
    for(int row=Math.Min(owner.ar,owner.br);row<=Math.Max(owner.ar,owner.br);row++) {
     Rect r=owner.editor.GetRectFromCharacterIndex(owner.StartOf(row));
     if(r.IsEmpty)continue;
     double left=owner.XAt(row,Math.Min(owner.ac,owner.bc)),right=owner.XAt(row,Math.Max(owner.ac,owner.bc));
     dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(90,70,160,240)),new Pen(Brushes.LightSkyBlue,1),new Rect(left,r.Top,Math.Max(1,right-left),r.Height));
    } dc.Pop();
   }
  }
  internal static string CopyRectangle(string text,int first,int last,int left,int right) {
   var lines=Regex.Split(text,"\r\n|\n|\r");var result=new List<string>();
   for(int row=first;row<=last&&row<lines.Length;row++){
    var expanded=new StringBuilder();int column=0;
    foreach(char c in lines[row]){
     int next=VisualColumn(c.ToString(),column);
     if(c=='\t')expanded.Append(' ',next-column);else expanded.Append(c);column=next;
    }
    string line=expanded.ToString().PadRight(right);
    result.Add(line.Substring(left,right-left));
   }
   return string.Join(Environment.NewLine,result);
  }
  internal static void Split(string line,int column,int tab,out string left,out string right){int width=0;for(int i=0;i<line.Length;i++){if(width==column){left=line.Substring(0,i);right=line.Substring(i);return;}int next=width+(line[i]=='\t'?tab-width%tab:1);if(column<next){left=line.Substring(0,i)+new string(' ',column-width);right=new string(' ',next-column)+line.Substring(i+1);return;}width=next;}left=line+new string(' ',Math.Max(0,column-width));right="";}
  internal static string Transform(string source,string data,int first,int last,int left,int right,int tab,bool skip,out int count){count=0;var parts=Regex.Split(source,"(\r\n|\n|\r)");for(int i=0;i<parts.Length;i+=2){int row=i/2;if(row<first||row>last||(i==parts.Length-1&&parts[i]==""&&i>0)||skip&&string.IsNullOrWhiteSpace(parts[i]))continue;if(data.Length==0 && VisualColumn(parts[i],0)<=left)continue;string a,b,c,d;Split(parts[i],left,tab,out a,out b);Split(parts[i],right,tab,out c,out d);parts[i]=a+data+d;count++;}return string.Concat(parts);}
 }
}
