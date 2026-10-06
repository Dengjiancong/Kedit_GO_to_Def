using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Kedit.Console {
    // Isolated first-round layout sample. Never writes production settings.
    internal sealed class DesignPreview : Window {
        readonly Grid stage=new Grid {Width=1280,Height=720};
        readonly Grid scene=new Grid();
        readonly MediaElement video=new MediaElement {LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Manual,Stretch=Stretch.Uniform,IsMuted=true};
        readonly Image gif=new Image {Stretch=Stretch.Uniform,Visibility=Visibility.Collapsed};
        readonly DispatcherTimer frames=new DispatcherTimer();
        GifBitmapDecoder decoder;int frame;bool paused,dirty,home=true,folded;
        readonly StackPanel form=new StackPanel();readonly Border panel=new Border();
        readonly StackPanel subnav=new StackPanel();readonly TextBlock title=new TextBlock();readonly TextBlock status=new TextBlock();
        Border nav;bool navFolded=true,adminUnlocked;Button debugMedia;StackPanel debugTools; readonly System.Collections.Generic.Dictionary<string,Button> tiles=new System.Collections.Generic.Dictionary<string,Button>();
        readonly Border news=new Border();readonly TextBox key=new TextBox(),target=new TextBox();
        string savedKey="Ctrl + Alt + O",savedTarget="D:\\Projects";
        string root;
        static Brush B(string s){return (Brush)new BrushConverter().ConvertFromString(s);}
        static TextBlock Text(string s,double size=14,string color="#ECEFF4"){return new TextBlock {Text=s,FontSize=size,Foreground=B(color),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};}
        Button Button(string s,Action action,bool accent=false){var b=new Button {Content=s,Foreground=B(accent?"#17191E":"#EEF1F6"),Background=B(accent?"#F4F51A":"#252831"),BorderThickness=new Thickness(0),Padding=new Thickness(12,10,12,10),Margin=new Thickness(0,4,0,4),Cursor=System.Windows.Input.Cursors.Hand};
            var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.Name="chrome";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(13));border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.MarginProperty,new Thickness(6));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);template.VisualTree=border;AddFeedback(template);b.Template=template;b.Click+=delegate{action();};return b;}
        static void AddFeedback(ControlTemplate template){
            var hover=new Trigger {Property=UIElement.IsMouseOverProperty,Value=true};
            hover.Setters.Add(new Setter(Border.BorderBrushProperty,B("#B8CDD9"),"chrome"));
            hover.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(1),"chrome"));
            hover.Setters.Add(new Setter(UIElement.EffectProperty,new System.Windows.Media.Effects.DropShadowEffect {Color=Colors.White,ShadowDepth=0,BlurRadius=9,Opacity=.22},"chrome"));template.Triggers.Add(hover);
            var focus=new Trigger {Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,B("#F4F51A"),"chrome"));focus.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(1),"chrome"));template.Triggers.Add(focus);
            var press=new Trigger {Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true};press.Setters.Add(new Setter(UIElement.RenderTransformOriginProperty,new Point(.5,.5),"chrome"));press.Setters.Add(new Setter(UIElement.RenderTransformProperty,new ScaleTransform(.92,.92),"chrome"));press.Setters.Add(new Setter(UIElement.OpacityProperty,.75,"chrome"));template.Triggers.Add(press);
        }
        Button WindowButton(bool close){
            var b=Button("",()=>{if(close)Close();else WindowState=WindowState.Minimized;});
            b.Width=39;b.Height=39;b.Margin=new Thickness(5,0,0,0);b.ToolTip=close?"关闭":"最小化";
            var template=new ControlTemplate(typeof(Button));var edge=new FrameworkElementFactory(typeof(Border));edge.Name="chrome";edge.SetValue(Border.CornerRadiusProperty,new CornerRadius(22));edge.SetValue(Border.BackgroundProperty,B("#E51B1C20"));edge.SetValue(Border.BorderBrushProperty,B("#48494C"));edge.SetValue(Border.BorderThicknessProperty,new Thickness(1));
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);edge.AppendChild(presenter);template.VisualTree=edge;AddFeedback(template);b.Template=template;
            b.Content=new System.Windows.Shapes.Path {Data=Geometry.Parse(close?"M 1,1 L 5,5 M 11,11 L 15,15 M 15,1 L 11,5 M 5,11 L 1,15":"M 0,8 L 16,8"),Stroke=Brushes.White,StrokeThickness=1.5,Width=16,Height=16,Stretch=Stretch.None};return b;
        }
        public DesignPreview(){
            root=FindRoot();UseLayoutRounding=true;SnapsToDevicePixels=true;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);Title="Kedit 中控 · 第一轮设计样板";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;WindowStartupLocation=WindowStartupLocation.CenterScreen;
            var work=SystemParameters.WorkArea;double scale=Math.Min(.85,Math.Min((work.Width-32)/1280,(work.Height-32)/720));Width=1280*scale;Height=720*scale;
            var view=new Viewbox {Stretch=Stretch.Uniform,Child=stage};Content=view;
            var outer=new Border {CornerRadius=new CornerRadius(30),Padding=new Thickness(12),Background=new LinearGradientBrush(Color.FromRgb(108,109,110),Color.FromRgb(58,59,60),90),BorderBrush=B("#747577"),BorderThickness=new Thickness(1)};stage.Children.Add(outer);
            var shell=new Grid();shell.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(86)});shell.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(10)});shell.ColumnDefinitions.Add(new ColumnDefinition());outer.Child=shell;
            var rail=new Border {Background=B("#191A1D"),CornerRadius=new CornerRadius(22)};shell.Children.Add(rail);var rails=new Grid();rail.Child=rails;
            var avatar=Button("◉",()=>SelectTile("avatar"));avatar.ToolTip="选择并裁切头像";avatar.VerticalAlignment=VerticalAlignment.Top;avatar.Margin=new Thickness(0,15,0,0);Tile(avatar,"avatar");rails.Children.Add(avatar);
            var groups=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6)};rails.Children.Add(groups);
            foreach(string group in new[]{"Kedit","Visual\nStudio","桌宠OSD","更新","其他"}){string g=group;var b=Button(g,()=>Navigate(g));b.ToolTip=g.Replace("\n"," ");b.Width=46;b.Height=46;b.HorizontalAlignment=HorizontalAlignment.Center;b.Margin=new Thickness(0,9,0,9);b.Background=B(g=="Kedit"?"#D57839":g.StartsWith("Visual")?"#7157A3":g=="桌宠OSD"?"#238FAD":g=="更新"?"#426F92":"#495364");b.Content=new TextBlock {Text=g=="Kedit"?"K":g.StartsWith("Visual")?"∞":g=="桌宠OSD"?"♧":g=="更新"?"↓":"⋯",FontSize=23,FontWeight=FontWeights.Medium,HorizontalAlignment=HorizontalAlignment.Center};Tile(b,g);groups.Children.Add(b);}
            var homeButton=Button("▦",()=>Navigate("首页"));homeButton.ToolTip="返回首页";homeButton.VerticalAlignment=VerticalAlignment.Bottom;homeButton.Margin=new Thickness(0,0,0,14);homeButton.Width=46;homeButton.Height=46;homeButton.HorizontalAlignment=HorizontalAlignment.Center;homeButton.FontSize=23;rails.Children.Add(homeButton);
            var main=new Border {CornerRadius=new CornerRadius(22),Background=B("#0C1422"),ClipToBounds=true};Grid.SetColumn(main,2);shell.Children.Add(main);main.Child=scene;
            scene.SizeChanged+=delegate{scene.Clip=new RectangleGeometry(new Rect(0,0,scene.ActualWidth,scene.ActualHeight),22,22);};scene.Children.Add(video);scene.Children.Add(gif);
            var shade=new Border {IsHitTestVisible=false,Background=new LinearGradientBrush(Color.FromArgb(25,5,10,19),Color.FromArgb(125,5,10,19),90)};scene.Children.Add(shade);
            var header=new Grid {Background=Brushes.Transparent,Height=90,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(36,20,24,0)};scene.Children.Add(header);header.MouseLeftButtonDown+=delegate(object s,System.Windows.Input.MouseButtonEventArgs e){if(e.OriginalSource==header)DragMove();};
            title.FontSize=32;title.FontWeight=FontWeights.Bold;title.Foreground=Brushes.White;title.IsHitTestVisible=false;header.Children.Add(title);
            var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top};header.Children.Add(actions);
            foreach(string icon in new[]{"♙","▤","⚙"}){bool admin=icon=="♙";var b=Button(icon,()=>{if(admin)AdminAccess();else MessageBox.Show(this,"功能预留","Kedit");});b.ToolTip=admin?"管理员调试（再次点击可锁定）":"功能预留";b.Width=39;b.Height=39;b.FontSize=15;b.Margin=new Thickness(4,0,0,0);actions.Children.Add(b);}actions.Children.Add(WindowButton(false));actions.Children.Add(WindowButton(true));
            news.HorizontalAlignment=HorizontalAlignment.Left;news.VerticalAlignment=VerticalAlignment.Bottom;news.Margin=new Thickness(36,0,0,34);news.Width=655;news.Height=156;news.CornerRadius=new CornerRadius(20);news.Background=B("#ED191B20");scene.Children.Add(news);
            var newsGrid=new Grid();newsGrid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(240)});newsGrid.ColumnDefinitions.Add(new ColumnDefinition());news.Child=newsGrid;news.SizeChanged+=delegate{news.Clip=new RectangleGeometry(new Rect(0,0,news.ActualWidth,news.ActualHeight),20,20);};
            string jpg=Path.Combine(root,"暂时展示用.jpg");if(File.Exists(jpg))newsGrid.Children.Add(new Image {Source=new BitmapImage(new Uri(jpg)),Stretch=Stretch.UniformToFill});var copy=new StackPanel {Margin=new Thickness(22)};Grid.SetColumn(copy,1);newsGrid.Children.Add(copy);copy.Children.Add(Text("公告     /     更新说明",17));copy.Children.Add(Text("你的快捷键，你的工作节奏。",19));copy.Children.Add(Text("本地展示内容 · 在线公告将在后续接入",12,"#9399A5"));
            panel.Width=235;panel.HorizontalAlignment=HorizontalAlignment.Right;panel.VerticalAlignment=VerticalAlignment.Stretch;panel.Margin=new Thickness(0,110,106,72);panel.Padding=new Thickness(18);panel.CornerRadius=new CornerRadius(18);panel.Background=B("#E51A1D25");panel.Child=new ScrollViewer {Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};scene.Children.Add(panel);
            nav=new Border {Width=45,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Background=B("#DD11151D"),CornerRadius=new CornerRadius(18),Padding=new Thickness(6)};nav.Child=subnav;scene.Children.Add(nav);
            var bottom=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,20,15)};scene.Children.Add(bottom);
            bottom.Children.Add(Button("收起 / 展开设置",()=>{folded=!folded;panel.Visibility=home||folded?Visibility.Collapsed:Visibility.Visible;}));bottom.Children.Add(Button("暂停 / 播放",()=>{paused=!paused;Playback();}));
            foreach(Button control in bottom.Children)control.Margin=new Thickness(6,4,6,4);
            debugTools=new StackPanel {Orientation=Orientation.Horizontal,Visibility=Visibility.Collapsed,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(36,0,0,15)};scene.Children.Add(debugTools);
            debugMedia=Button("选择 GIF / MP4",ChooseMedia);debugTools.Children.Add(debugMedia);debugTools.Children.Add(Button("重播",()=>{if(!adminUnlocked)return;frame=0;video.Position=TimeSpan.Zero;paused=false;Playback();}));debugTools.Children.Add(Button("分类图标",ManageTiles));foreach(Button b in debugTools.Children)b.Margin=new Thickness(0,4,12,4);
            video.MediaEnded+=delegate{video.Position=TimeSpan.Zero;Playback();};video.MediaFailed+=delegate{status.Text="媒体无法播放，请选择本地 GIF / MP4。";};frames.Tick+=delegate{if(decoder==null)return;gif.Source=decoder.Frames[frame];var meta=decoder.Frames[frame].Metadata as BitmapMetadata;int delay=10;try{if(meta!=null&&meta.ContainsQuery("/grctlext/Delay"))delay=Convert.ToInt32(meta.GetQuery("/grctlext/Delay"));}catch{}frames.Interval=TimeSpan.FromMilliseconds(Math.Max(20,delay*10));frame=(frame+1)%decoder.Frames.Count;};
            StateChanged+=delegate{Playback();};Closed+=delegate{frames.Stop();video.Close();Application.Current.Shutdown();};Closing+=delegate(object s,System.ComponentModel.CancelEventArgs e){if(!CanLeave())e.Cancel=true;};
            foreach(var input in new[]{key,target}){input.Background=B("#10151D");input.Foreground=Brushes.White;input.BorderBrush=B("#4B5364");input.CaretBrush=Brushes.White;input.Margin=new Thickness(0,4,0,14);}
            Navigate("首页");
            SourceInitialized+=delegate {var source=System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);source.AddHook(DpiMessage);};
            LocationChanged+=delegate{FitScreen();};
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--capture-preview")>=0)Loaded+=async delegate {
                string dir=Path.Combine(root,".pet-test","control-design");Directory.CreateDirectory(dir);
                await System.Threading.Tasks.Task.Delay(2500);Capture(Path.Combine(dir,"home.png"));
                Navigate("其他");await System.Threading.Tasks.Task.Delay(1500);Capture(Path.Combine(dir,"quick-open.png"));
                key.Text="Ctrl + Shift + O";if(!dirty)throw new InvalidOperationException("Draft tracking failed");Save();if(dirty||savedKey!="Ctrl + Shift + O")throw new InvalidOperationException("Preview save failed");
                panel.Visibility=Visibility.Collapsed;Capture(Path.Combine(dir,"collapsed.png"));
                LoadMedia(Path.Combine(root,"亚托莉猫猫Meme.gif"));await System.Threading.Tasks.Task.Delay(500);Capture(Path.Combine(dir,"gif.png"));
                var crop=new PreviewCrop(this,Path.Combine(root,"暂时展示用.jpg"));crop.Show();await System.Threading.Tasks.Task.Delay(300);
                var shot=new RenderTargetBitmap((int)crop.ActualWidth,(int)crop.ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(crop);PreviewCrop.Save(shot,Path.Combine(dir,"crop-dialog.png"));crop.Close();
                File.WriteAllText(Path.Combine(dir,"result.txt"),"PASS: home/config/collapsed/GIF rendered; draft changes isolated from production.");Close();
            };
        }
        IntPtr DpiMessage(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg==0x02E0)Dispatcher.BeginInvoke(new Action(FitScreen));return IntPtr.Zero;}
        void FitScreen(){var src=PresentationSource.FromVisual(this);if(src==null)return;var area=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;var logical=src.CompositionTarget.TransformFromDevice.Transform(new Vector(area.Width,area.Height));double scale=Math.Min(.85,Math.Min((logical.X-32)/1280,(logical.Y-32)/720));if(Math.Abs(Width-1280*scale)>.5){Width=1280*scale;Height=720*scale;}}
        void Capture(string path){var bmp=new RenderTargetBitmap(1280,720,96,96,PixelFormats.Pbgra32);bmp.Render(stage);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))png.Save(f);}
        string FindRoot(){var d=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);while(d!=null){if(File.Exists(Path.Combine(d.FullName,"暂时展示用.jpg")))return d.FullName;d=d.Parent;}return AppDomain.CurrentDomain.BaseDirectory;}
        bool CanLeave(){if(!dirty)return true;var result=MessageBox.Show(this,"保存本次样板草稿后继续？\n是：仅保存到本次预览；否：放弃修改；取消：留在当前页。","未保存修改",MessageBoxButton.YesNoCancel);if(result==MessageBoxResult.Cancel)return false;if(result==MessageBoxResult.Yes)Save();else dirty=false;return true;}
        void Save(){savedKey=key.Text;savedTarget=target.Text;dirty=false;status.Text="已保存到本次预览 · 未修改实际快捷键";}
        void Navigate(string page){if(!CanLeave())return;home=page=="首页";folded=false;foreach(var pair in tiles){pair.Value.BorderBrush=B(pair.Key==page?"#FFF9E7":"#35373C");pair.Value.BorderThickness=new Thickness(pair.Key==page?2:1);}title.Text=home?"KEDIT\n让操作，随心而动。":page.Replace("\n", " ")+"  /  布局预览";title.FontSize=home?34:23;news.Visibility=home?Visibility.Visible:Visibility.Collapsed;panel.Visibility=home?Visibility.Collapsed:Visibility.Visible;form.Children.Clear();subnav.Children.Clear();
            nav.Visibility=home?Visibility.Collapsed:Visibility.Visible; if(!home){subnav.Children.Add(Button("☰",()=>{navFolded=!navFolded;nav.Width=navFolded?45:92;foreach(var child in subnav.Children){var b=child as Button;if(b!=null&&b.Tag!=null)b.Content=navFolded?b.Tag:b.ToolTip;}}));foreach(string name in new[]{"⌘ 快速打开","◇ 使用说明"}){var entry=Button(name,()=>{panel.Visibility=Visibility.Visible;folded=false;});entry.ToolTip=name;entry.Tag=name.Substring(0,1);entry.Content=navFolded?entry.Tag:entry.ToolTip;subnav.Children.Add(entry);}
                form.Children.Add(Text("快速打开",23));form.Children.Add(Text("布局样板 · 设置仅保留在本次预览",11,"#F4F51A"));form.Children.Add(Text("快捷键",12));key.Text=savedKey;key.Margin=new Thickness(0,0,0,18);key.Padding=new Thickness(8);form.Children.Add(key);form.Children.Add(Text("打开路径或目标",12));target.Text=savedTarget;target.TextWrapping=TextWrapping.Wrap;target.Padding=new Thickness(8);form.Children.Add(target);
                form.Children.Add(Text("选择常用项目、文件夹或程序，通过一个快捷键快速访问。",13,"#ADB5C6"));form.Children.Add(Button("选择路径",()=>{var d=new OpenFileDialog();if(d.ShowDialog()==true)target.Text=d.FileName;}));form.Children.Add(Text("作用范围：全局\n执行方式：打开目标\n演示素材：暂用现有视频，可自行选择",12,"#A8B0BD"));form.Children.Add(Button("保存预览草稿",Save,true));form.Children.Add(Button("取消修改",()=>{key.Text=savedKey;target.Text=savedTarget;dirty=false;status.Text="已恢复预览值";}));status.Foreground=B("#C5C9D0");status.TextWrapping=TextWrapping.Wrap;status.Text="尚未接入真实配置";form.Children.Add(status);}
            dirty=false;key.TextChanged-=Changed;target.TextChanged-=Changed;key.TextChanged+=Changed;target.TextChanged+=Changed;LoadMedia(Path.Combine(root,home?"side.mp4":"update_bg.mp4"));
        }
        void Changed(object s,TextChangedEventArgs e){dirty=key.Text!=savedKey||target.Text!=savedTarget;status.Text=dirty?"有未保存修改":"预览草稿";}
        void AdminAccess(){
            if(adminUnlocked){adminUnlocked=false;debugTools.Visibility=Visibility.Collapsed;return;}
            var dialog=new Window {Owner=this,Title="管理员调试",Width=350,Height=220,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#191D26"),ShowInTaskbar=false};
            var layout=new StackPanel {Margin=new Thickness(24)};dialog.Content=layout;layout.Children.Add(Text("输入管理员密码",18));var password=new PasswordBox {Padding=new Thickness(8),Background=B("#10151D"),Foreground=Brushes.White};layout.Children.Add(password);var error=Text("仅解锁本次窗口的调试工具",12,"#A9B4C6");error.Margin=new Thickness(0,10,0,0);layout.Children.Add(error);
            Action unlock=()=>{if(password.Password=="QWEASD"){adminUnlocked=true;debugTools.Visibility=Visibility.Visible;dialog.DialogResult=true;}else {error.Text="密码不正确，请重试";password.Clear();password.Focus();}};
            layout.Children.Add(Button("解锁",unlock,true));password.KeyDown+=delegate(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Enter){unlock();e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Escape)dialog.Close();};dialog.Loaded+=delegate{password.Focus();};dialog.ShowDialog();
        }
        string TileKey(string name){return name=="avatar"?"avatar":name=="Kedit"?"kedit":name.StartsWith("Visual")?"vs":name=="桌宠OSD"?"pet":name=="更新"?"update":"other";}
        string TileFile(string name){return Path.Combine(root,"Kedit.Console","PreviewAssets",TileKey(name)+".png");}
        void Tile(Button b,string name){
            tiles[name]=b;b.Width=46;b.Height=46;b.HorizontalAlignment=HorizontalAlignment.Center;b.BorderBrush=B("#35373C");b.BorderThickness=new Thickness(1);
            var t=new ControlTemplate(typeof(Button));var edge=new FrameworkElementFactory(typeof(Border));edge.Name="chrome";edge.SetValue(Border.CornerRadiusProperty,new CornerRadius(13));
            foreach(string prop in new[]{"Background","BorderBrush","BorderThickness"})edge.SetBinding(prop=="Background"?Border.BackgroundProperty:prop=="BorderBrush"?Border.BorderBrushProperty:Border.BorderThicknessProperty,new System.Windows.Data.Binding(prop){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
            edge.SetValue(UIElement.EffectProperty,new System.Windows.Media.Effects.DropShadowEffect {Color=Colors.Black,BlurRadius=6,ShadowDepth=3,Opacity=.6});
            var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);edge.AppendChild(content);t.VisualTree=edge;AddFeedback(t);b.Template=t;RefreshTile(name);
        }
        void RefreshTile(string name){
            try {BitmapImage bitmap=null;string file=TileFile(name);if(File.Exists(file)){bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(file);bitmap.EndInit();}
                else using(var input=typeof(DesignPreview).Assembly.GetManifestResourceStream("PreviewAssets/"+TileKey(name)+".png")){if(input!=null){bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=input;bitmap.EndInit();}}
                if(bitmap!=null){bitmap.Freeze();tiles[name].Content=new Image {Source=bitmap,Width=42,Height=42,Stretch=Stretch.UniformToFill,Clip=new RectangleGeometry(new Rect(0,0,42,42),11,11)};}
            }catch(Exception ex){MessageBox.Show(this,"图标读取失败："+ex.Message);}
        }
        void SelectTile(string name,Window owner=null){if(name!="avatar"&&!adminUnlocked)return;var d=new OpenFileDialog {Filter="图片|*.png;*.jpg;*.jpeg;*.bmp"};if(d.ShowDialog(owner??this)!=true)return;
            try{var crop=new PreviewCrop(owner??this,d.FileName);if(crop.ShowDialog()==true){PreviewCrop.Save(crop.Result,TileFile(name));RefreshTile(name);}}catch(Exception ex){MessageBox.Show(this,"图片未保存："+ex.Message);}}
        void ManageTiles(){if(!adminUnlocked)return;var dialog=new Window {Owner=this,Title="分类图标 · 保存后随下次构建打包",Width=330,Height=390,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#191D26"),ResizeMode=ResizeMode.NoResize};var list=new StackPanel {Margin=new Thickness(20)};dialog.Content=list;list.Children.Add(Text("为每个分类选择图片并裁切",16));foreach(string name in new[]{"Kedit","Visual\nStudio","桌宠OSD","更新","其他"}){string n=name;list.Children.Add(Button(n.Replace("\n"," "),()=>SelectTile(n,dialog)));}list.Children.Add(Text("保存为项目图标资源；后续构建自动嵌入，不依赖原图片路径。",12));dialog.ShowDialog();}
        void ChooseMedia(){if(!adminUnlocked)return;var d=new OpenFileDialog {Filter="演示动画|*.gif;*.mp4"};if(d.ShowDialog()==true)LoadMedia(d.FileName);}
        void LoadMedia(string path){video.Stretch=Stretch.UniformToFill;gif.Stretch=video.Stretch;frames.Stop();video.Stop();video.Source=null;decoder=null;paused=false;if(!File.Exists(path))return;if(Path.GetExtension(path).Equals(".gif",StringComparison.OrdinalIgnoreCase)){decoder=new GifBitmapDecoder(new Uri(path),BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);frame=0;gif.Source=decoder.Frames[0];gif.Visibility=Visibility.Visible;video.Visibility=Visibility.Collapsed;}else{gif.Visibility=Visibility.Collapsed;video.Visibility=Visibility.Visible;video.Source=new Uri(path);}Playback();}
        void Playback(){bool run=!paused&&WindowState!=WindowState.Minimized;if(decoder!=null){if(run){frames.Interval=TimeSpan.FromMilliseconds(100);frames.Start();}else frames.Stop();}else if(video.Source!=null){if(run)video.Play();else video.Pause();}}
    }
}
