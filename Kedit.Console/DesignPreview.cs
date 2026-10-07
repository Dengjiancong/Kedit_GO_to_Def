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
    internal sealed partial class DesignPreview : Window {
        readonly Grid stage=new Grid {Width=1280,Height=720};
        readonly Grid scene=new Grid();
        readonly MediaElement video=new MediaElement {LoadedBehavior=MediaState.Manual,UnloadedBehavior=MediaState.Manual,Stretch=Stretch.Uniform,IsMuted=true};
        readonly Image gif=new Image {Stretch=Stretch.Uniform,Visibility=Visibility.Collapsed};
        readonly DispatcherTimer frames=new DispatcherTimer();
        GifBitmapDecoder decoder;int frame;bool paused,dirty,home=true,folded;
        readonly StackPanel form=new StackPanel();readonly StackPanel formHeader=new StackPanel(),formFooter=new StackPanel();readonly Border panel=new Border();
        readonly StackPanel subnav=new StackPanel();readonly TextBlock title=new TextBlock();readonly TextBlock status=new TextBlock();
        Border nav;bool navFolded=true,adminUnlocked;Button debugMedia,settingsToggle;StackPanel debugTools; readonly System.Collections.Generic.Dictionary<string,Button> tiles=new System.Collections.Generic.Dictionary<string,Button>();
        readonly Border news=new Border();readonly TextBox key=new TextBox(),target=new TextBox();
        string savedKey="Ctrl + Alt + O",savedTarget="D:\\Projects";
        string root;
        static Brush B(string s){return (Brush)new BrushConverter().ConvertFromString(s);}
        static TextBlock Text(string s,double size=14,string color="#ECEFF4"){return new TextBlock {Text=s,FontSize=size,Foreground=B(color),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};}
        Button Button(string s,Action action,bool accent=false,double corner=13){var b=new Button {Content=s,Foreground=B(accent?"#17191E":"#EEF1F6"),Background=B(accent?"#F4F51A":"#252831"),BorderThickness=new Thickness(0),Padding=new Thickness(12,10,12,10),Margin=new Thickness(0,4,0,4),Cursor=System.Windows.Input.Cursors.Hand};
            var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.Name="chrome";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(corner));border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.MarginProperty,new Thickness(6));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);template.VisualTree=border;AddFeedback(template);b.Template=template;b.Click+=delegate{action();};return b;}
        static void AddFeedback(ControlTemplate template,bool glow=true){
            var hover=new Trigger {Property=UIElement.IsMouseOverProperty,Value=true};
            hover.Setters.Add(new Setter(Border.BorderBrushProperty,B("#B8CDD9"),"chrome"));
            hover.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(1),"chrome"));
            if(glow)hover.Setters.Add(new Setter(UIElement.EffectProperty,new System.Windows.Media.Effects.DropShadowEffect {Color=Colors.White,ShadowDepth=0,BlurRadius=9,Opacity=.22},"chrome"));template.Triggers.Add(hover);
            var focus=new Trigger {Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,B(glow?"#F4F51A":"#B8CDD9"),"chrome"));focus.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(1),"chrome"));if(glow)template.Triggers.Add(focus);
            var press=new Trigger {Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true};press.Setters.Add(new Setter(UIElement.RenderTransformOriginProperty,new Point(.5,.5),"chrome"));press.Setters.Add(new Setter(UIElement.RenderTransformProperty,new ScaleTransform(.92,.92),"chrome"));press.Setters.Add(new Setter(UIElement.OpacityProperty,.75,"chrome"));template.Triggers.Add(press);
        }
        static void ConfigureScroll(ScrollViewer viewer){
            const string xaml=@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'>
              <Setter Property='Width' Value='3'/><Setter Property='Background' Value='Transparent'/>
              <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'>
                <Track x:Name='PART_Track' Orientation='Vertical' IsDirectionReversed='True'>
                  <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb MinHeight='26'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border x:Name='thumb' Margin='0,2' CornerRadius='2' Background='#4D5864'/><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='thumb' Property='Background' Value='#8393A3'/></Trigger><Trigger Property='IsDragging' Value='True'><Setter TargetName='thumb' Property='Background' Value='#A4B5C5'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
                </Track></Grid></ControlTemplate></Setter.Value></Setter></Style>";
            viewer.Resources["SlimScroll"]=(Style)System.Windows.Markup.XamlReader.Parse(xaml);
            viewer.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollViewer'>
              <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
                <ScrollContentPresenter x:Name='PART_ScrollContentPresenter' CanContentScroll='{TemplateBinding CanContentScroll}' Content='{TemplateBinding Content}' ContentTemplate='{TemplateBinding ContentTemplate}' Margin='0,0,6,0'/>
                <ScrollBar x:Name='PART_VerticalScrollBar' Grid.Column='1' Width='3' MinWidth='0' MaxWidth='3' Orientation='Vertical' Style='{DynamicResource SlimScroll}' Minimum='0' Maximum='{TemplateBinding ScrollableHeight}' ViewportSize='{TemplateBinding ViewportHeight}' Value='{Binding VerticalOffset, RelativeSource={RelativeSource TemplatedParent}, Mode=OneWay}' Visibility='{TemplateBinding ComputedVerticalScrollBarVisibility}'/>
              </Grid></ControlTemplate>");
        }
        Button WindowButton(bool close){
            var b=Button("",()=>{if(close)Close();else WindowState=WindowState.Minimized;});
            b.Width=41.25;b.Height=41.25;b.Margin=new Thickness(13,0,0,0);b.ToolTip=close?"关闭":"最小化";
            var template=new ControlTemplate(typeof(Button));var edge=new FrameworkElementFactory(typeof(Border));edge.Name="chrome";edge.SetValue(Border.CornerRadiusProperty,new CornerRadius(22));edge.SetValue(Border.BackgroundProperty,B("#E51B1C20"));edge.SetValue(Border.BorderBrushProperty,B("#48494C"));edge.SetValue(Border.BorderThicknessProperty,new Thickness(1));
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);edge.AppendChild(presenter);template.VisualTree=edge;AddFeedback(template);b.Template=template;
            var icon=new Grid {Width=16,Height=16};
            icon.Children.Add(new System.Windows.Shapes.Path {Data=Geometry.Parse(close?"M 2.5,2.5 L 5,5 M 11,11 L 13.5,13.5 M 13.5,2.5 L 11,5 M 5,11 L 2.5,13.5":"M 1,8 L 15,8"),Stroke=Brushes.White,StrokeThickness=1.5,Stretch=Stretch.None,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
            if(close)icon.Children.Add(new System.Windows.Shapes.Ellipse {Width=2.5,Height=2.5,Fill=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});b.Content=icon;return b;
        }
        Button HeaderButton(string label,Action action,string geometry,double width){
            var b=Button("",action,false,22);b.Width=width;b.Height=41.25;b.Margin=new Thickness(16,0,0,0);b.Background=B("#E51B1C20");b.ToolTip="功能预留";
            var border=new FrameworkElementFactory(typeof(Border));border.Name="chrome";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(22));border.SetValue(Border.BackgroundProperty,b.Background);border.SetValue(Border.BorderBrushProperty,B("#48494C"));border.SetValue(Border.BorderThicknessProperty,new Thickness(1));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(presenter);var template=new ControlTemplate(typeof(Button)){VisualTree=border};AddFeedback(template);b.Template=template;
            var content=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};content.Children.Add(new System.Windows.Shapes.Path {Data=Geometry.Parse(geometry),Stroke=Brushes.White,StrokeThickness=1.5,Width=19,Height=19,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center});if(label!=null)content.Children.Add(new TextBlock {Text=label,Foreground=Brushes.White,FontSize=13,Margin=new Thickness(9,0,0,0),VerticalAlignment=VerticalAlignment.Center});b.Content=content;return b;
        }
        public DesignPreview(bool live=false){
            liveMode=live;
            PreviewMouseLeftButtonDown+=BeginWindowDrag;PreviewMouseMove+=ContinueWindowDrag;PreviewMouseLeftButtonUp+=delegate{dragOrigin=null;};Deactivated+=delegate{dragOrigin=null;};
            root=FindRoot();UseLayoutRounding=true;SnapsToDevicePixels=true;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);Title=liveMode?"Kedit 配置中心":"Kedit 中控 · 第一轮设计样板";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;WindowStartupLocation=WindowStartupLocation.CenterScreen;
            var work=SystemParameters.WorkArea;double scale=Math.Min(1,Math.Min((work.Width-32)/1280,(work.Height-32)/720));Width=1280*scale;Height=720*scale;
            var view=new Viewbox {Stretch=Stretch.Uniform,Child=stage};Content=view;
            var outer=new Border {CornerRadius=new CornerRadius(30),Padding=new Thickness(12),Background=new LinearGradientBrush(Color.FromRgb(108,109,110),Color.FromRgb(58,59,60),90),BorderBrush=B("#747577"),BorderThickness=new Thickness(1)};stage.Children.Add(outer);
            var shell=new Grid();shell.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(86)});shell.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(10)});shell.ColumnDefinitions.Add(new ColumnDefinition());outer.Child=shell;
            var rail=new Border {Background=B("#191A1D"),CornerRadius=new CornerRadius(22)};shell.Children.Add(rail);var rails=new Grid();rail.Child=rails;
            var avatar=Button("◉",()=>SelectTile("avatar"));avatar.ToolTip="选择并裁切头像";avatar.VerticalAlignment=VerticalAlignment.Top;avatar.Margin=new Thickness(0,15,0,0);Tile(avatar,"avatar");rails.Children.Add(avatar);
            var groups=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6)};rails.Children.Add(groups);
            foreach(string group in new[]{"Kedit","Visual\nStudio","桌宠OSD","更新","其他"}){string g=group;var b=Button(g,()=>Navigate(g));b.ToolTip=g.Replace("\n"," ");b.Width=51;b.Height=51;b.HorizontalAlignment=HorizontalAlignment.Center;b.Margin=new Thickness(0,9,0,9);b.Background=B(g=="Kedit"?"#D57839":g.StartsWith("Visual")?"#7157A3":g=="桌宠OSD"?"#238FAD":g=="更新"?"#426F92":"#495364");b.Content=new TextBlock {Text=g=="Kedit"?"K":g.StartsWith("Visual")?"∞":g=="桌宠OSD"?"♧":g=="更新"?"↓":"⋯",FontSize=26,FontWeight=FontWeights.Medium,HorizontalAlignment=HorizontalAlignment.Center};Tile(b,g);groups.Children.Add(b);}
            var homeButton=Button("▦",()=>Navigate("首页"),false,7);homeButton.ToolTip="返回首页";homeButton.VerticalAlignment=VerticalAlignment.Bottom;homeButton.Margin=new Thickness(0,0,0,14);homeButton.Width=51;homeButton.Height=51;homeButton.HorizontalAlignment=HorizontalAlignment.Center;homeButton.FontSize=30;rails.Children.Add(homeButton);
            var main=new Border {CornerRadius=new CornerRadius(22),Background=B("#0C1422"),ClipToBounds=true};Grid.SetColumn(main,2);shell.Children.Add(main);main.Child=scene;
            scene.SizeChanged+=delegate{scene.Clip=new RectangleGeometry(new Rect(0,0,scene.ActualWidth,scene.ActualHeight),22,22);};scene.Children.Add(video);scene.Children.Add(gif);
            var shade=new Border {IsHitTestVisible=false,Background=new LinearGradientBrush(Color.FromArgb(25,5,10,19),Color.FromArgb(125,5,10,19),90)};scene.Children.Add(shade);
            var header=new Grid {Background=Brushes.Transparent,Height=90,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(36,20,24,0)};scene.Children.Add(header);
            title.FontSize=32;title.FontWeight=FontWeights.Bold;title.Foreground=Brushes.White;title.IsHitTestVisible=false;header.Children.Add(title);
            var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top};header.Children.Add(actions);
            var adminButton=HeaderButton("管理员",AdminAccess,"M 6,5 A 3,3 0 1 0 12,5 A 3,3 0 1 0 6,5 M 2,17 L 2,12 Q 9,8 16,12 L 16,17 M 5,15 L 13,15",130);adminButton.ToolTip="管理员调试（再次点击可锁定）";actions.Children.Add(adminButton);
            var displayButton=HeaderButton(null,()=>MessageBox.Show(this,"功能预留","Kedit"),"M 1,2 L 18,2 L 18,12 L 1,12 Z M 5,16 L 13,16 M 9,12 L 9,16 M 1,6 L 18,6 M 14,14 L 20,14 M 17,11 L 17,17",41.25);actions.Children.Add(displayButton);
            actions.Children.Add(new Border {Width=1,Height=24,Background=B("#626269"),Margin=new Thickness(16,0,16,0),VerticalAlignment=VerticalAlignment.Center});
            var settingsButton=HeaderButton(null,()=>MessageBox.Show(this,"功能预留","Kedit"),"M 7,1 L 11,1 L 12,4 L 15,4 L 17,7 L 15,9 L 17,12 L 15,15 L 12,14 L 11,17 L 7,17 L 6,14 L 3,15 L 1,12 L 3,9 L 1,7 L 3,4 L 6,4 Z M 6,9 A 3,3 0 1 0 12,9 A 3,3 0 1 0 6,9",41.25);var gear=(System.Windows.Shapes.Path)((StackPanel)settingsButton.Content).Children[0];gear.Width=14;gear.Height=14;gear.StrokeThickness=1.8;settingsButton.Margin=new Thickness(0);actions.Children.Add(settingsButton);actions.Children.Add(WindowButton(false));actions.Children.Add(WindowButton(true));
            news.HorizontalAlignment=HorizontalAlignment.Left;news.VerticalAlignment=VerticalAlignment.Bottom;news.Margin=new Thickness(36,0,0,34);news.Width=655;news.Height=156;news.CornerRadius=new CornerRadius(20);news.Background=B("#ED191B20");scene.Children.Add(news);
            var newsGrid=new Grid();newsGrid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(240)});newsGrid.ColumnDefinitions.Add(new ColumnDefinition());news.Child=newsGrid;news.SizeChanged+=delegate{news.Clip=new RectangleGeometry(new Rect(0,0,news.ActualWidth,news.ActualHeight),20,20);};
            string jpg=Path.Combine(root,"暂时展示用.jpg");if(File.Exists(jpg))newsGrid.Children.Add(new Image {Source=new BitmapImage(new Uri(jpg)),Stretch=Stretch.UniformToFill});var copy=new StackPanel {Margin=new Thickness(22)};Grid.SetColumn(copy,1);newsGrid.Children.Add(copy);copy.Children.Add(Text("公告     /     更新说明",17));copy.Children.Add(Text("你的快捷键，你的工作节奏。",19));copy.Children.Add(Text("本地展示内容 · 在线公告将在后续接入",12,"#9399A5"));
            panel.Width=350;panel.MaxHeight=512;panel.HorizontalAlignment=HorizontalAlignment.Right;panel.VerticalAlignment=VerticalAlignment.Bottom;panel.Margin=new Thickness(0,110,106,72);panel.Padding=new Thickness(18);panel.CornerRadius=new CornerRadius(18);panel.Background=B("#E51A1D25");var formLayout=new Grid();formLayout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});formLayout.RowDefinitions.Add(new RowDefinition {Height=new GridLength(1,GridUnitType.Star)});formLayout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});formLayout.Children.Add(formHeader);var scroller=new ScrollViewer {Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};ConfigureScroll(scroller);Grid.SetRow(scroller,1);formLayout.Children.Add(scroller);Grid.SetRow(formFooter,2);formLayout.Children.Add(formFooter);panel.Child=formLayout;scene.Children.Add(panel);
            nav=new Border {Width=45,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Background=B("#DD11151D"),CornerRadius=new CornerRadius(18),Padding=new Thickness(6)};nav.Child=subnav;scene.Children.Add(nav);
            var bottom=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,20,15)};scene.Children.Add(bottom);
            settingsToggle=Button("收起 / 展开设置",()=>{folded=!folded;panel.Visibility=home||folded?Visibility.Collapsed:Visibility.Visible;});bottom.Children.Add(settingsToggle);bottom.Children.Add(Button("暂停 / 播放",()=>{paused=!paused;Playback();}));
            foreach(Button control in bottom.Children)control.Margin=new Thickness(6,4,6,4);
            debugTools=new StackPanel {Orientation=Orientation.Horizontal,Visibility=Visibility.Collapsed,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(36,0,0,15)};scene.Children.Add(debugTools);
            debugMedia=Button("选择 GIF / MP4",ChooseMedia);debugTools.Children.Add(debugMedia);debugTools.Children.Add(Button("重播",()=>{if(!adminUnlocked)return;frame=0;video.Position=TimeSpan.Zero;paused=false;Playback();}));debugTools.Children.Add(Button("分类图标",ManageTiles));foreach(Button b in debugTools.Children)b.Margin=new Thickness(0,4,12,4);
            video.MediaEnded+=delegate{video.Position=TimeSpan.Zero;Playback();};video.MediaFailed+=delegate{status.Text="媒体无法播放，请选择本地 GIF / MP4。";};frames.Tick+=delegate{if(decoder==null)return;gif.Source=decoder.Frames[frame];var meta=decoder.Frames[frame].Metadata as BitmapMetadata;int delay=10;try{if(meta!=null&&meta.ContainsQuery("/grctlext/Delay"))delay=Convert.ToInt32(meta.GetQuery("/grctlext/Delay"));}catch{}frames.Interval=TimeSpan.FromMilliseconds(Math.Max(20,delay*10));frame=(frame+1)%decoder.Frames.Count;};
            StateChanged+=delegate{Playback();};Closed+=delegate{frames.Stop();video.Close();Application.Current.Shutdown();};Closing+=delegate(object s,System.ComponentModel.CancelEventArgs e){if(!CanLeave()){e.Cancel=true;return;}if(liveMode && !((App)Application.Current).Exiting && ((App)Application.Current).Pet.Settings.Enabled){e.Cancel=true;Hide();video.Pause();}};
            foreach(var input in new[]{key,target}){input.Background=B("#10151D");input.Foreground=Brushes.White;input.BorderBrush=B("#4B5364");input.CaretBrush=Brushes.White;input.Margin=new Thickness(0,4,0,14);}
            Navigate("首页");
            if(liveMode && Array.IndexOf(Environment.GetCommandLineArgs(),"--self-test-f1")>=0)Loaded+=delegate{RunF1Checks();};
            SourceInitialized+=delegate {var source=System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);source.AddHook(DpiMessage);};
            LocationChanged+=delegate{FitScreen();};
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--capture-preview")>=0)Loaded+=async delegate {
                string dir=Path.Combine(root,".pet-test","control-design-1280");Directory.CreateDirectory(dir);
                await System.Threading.Tasks.Task.Delay(2500);Capture(Path.Combine(dir,"home.png"));
                Navigate("其他");await System.Threading.Tasks.Task.Delay(1500);Capture(Path.Combine(dir,"quick-open.png"));
                int originalCount=form.Children.Count;for(int i=0;i<25;i++)form.Children.Add(Text("滚动验证项目 "+i));UpdateLayout();
                var scroll=(ScrollViewer)((Grid)panel.Child).Children[1];File.WriteAllText(Path.Combine(dir,"scroll-debug.txt"),"range="+scroll.ScrollableHeight+" height="+panel.ActualHeight);if(scroll.ScrollableHeight<=0||panel.ActualHeight>512.5)throw new InvalidOperationException("Form overflow layout failed");
                var bar=(System.Windows.Controls.Primitives.ScrollBar)scroll.Template.FindName("PART_VerticalScrollBar",scroll);File.WriteAllText(Path.Combine(dir,"scroll-width.txt"),bar.ActualWidth.ToString());if(bar.ActualWidth>3.1)throw new InvalidOperationException("Scrollbar too wide");File.WriteAllText(Path.Combine(dir,"scroll-width.txt"),bar.ActualWidth.ToString());scroll.ScrollToEnd();UpdateLayout();if(scroll.VerticalOffset<=0)throw new InvalidOperationException("Scroll failed");Capture(Path.Combine(dir,"long-form.png"));while(form.Children.Count>originalCount)form.Children.RemoveAt(form.Children.Count-1);UpdateLayout();
                File.WriteAllText(Path.Combine(dir,"layout.txt"),"Window="+ActualWidth+"x"+ActualHeight+"; canvas=1280x720; panel="+panel.ActualWidth+"x"+panel.ActualHeight+"; overflow passed");
                key.Text="Ctrl + Shift + O";if(!dirty)throw new InvalidOperationException("Draft tracking failed");Save();if(dirty||savedKey!="Ctrl + Shift + O")throw new InvalidOperationException("Preview save failed");
                panel.Visibility=Visibility.Collapsed;Capture(Path.Combine(dir,"collapsed.png"));
                LoadMedia(Path.Combine(root,"亚托莉猫猫Meme.gif"));await System.Threading.Tasks.Task.Delay(500);Capture(Path.Combine(dir,"gif.png"));
                var crop=new PreviewCrop(this,Path.Combine(root,"暂时展示用.jpg"));crop.Show();await System.Threading.Tasks.Task.Delay(300);
                var shot=new RenderTargetBitmap((int)crop.ActualWidth,(int)crop.ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(crop);PreviewCrop.Save(shot,Path.Combine(dir,"crop-dialog.png"));crop.Close();
                File.WriteAllText(Path.Combine(dir,"result.txt"),"PASS: home/config/collapsed/GIF rendered; draft changes isolated from production.");Close();
            };
        }
        Point? dragOrigin;
        bool CanDragFrom(DependencyObject source){
            for(var node=source;node!=null;){
                if(node==news || node is System.Windows.Controls.Primitives.ButtonBase || node is System.Windows.Controls.Primitives.TextBoxBase || node is PasswordBox || node is System.Windows.Controls.Primitives.Selector || node is System.Windows.Controls.Primitives.RangeBase || node is System.Windows.Controls.Primitives.Thumb || node is System.Windows.Documents.Hyperlink || node is Menu || node is MenuItem)return false;
                if(node is Visual || node is System.Windows.Media.Media3D.Visual3D)node=VisualTreeHelper.GetParent(node);
                else {var element=node as FrameworkContentElement;node=element!=null?element.Parent:LogicalTreeHelper.GetParent(node);}
            }
            return true;
        }
        void BeginWindowDrag(object sender,System.Windows.Input.MouseButtonEventArgs e){
            dragOrigin=null;if(e.ClickCount==1 && CanDragFrom(e.OriginalSource as DependencyObject))dragOrigin=e.GetPosition(this);
        }
        void ContinueWindowDrag(object sender,System.Windows.Input.MouseEventArgs e){
            if(e.LeftButton!=System.Windows.Input.MouseButtonState.Pressed){dragOrigin=null;return;}
            if(!dragOrigin.HasValue)return;var current=e.GetPosition(this);var start=dragOrigin.Value;
            if(Math.Abs(current.X-start.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            dragOrigin=null;e.Handled=true;DragMove();
        }
        IntPtr DpiMessage(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg==0x02E0)Dispatcher.BeginInvoke(new Action(FitScreen));return IntPtr.Zero;}
        void FitScreen(){var src=PresentationSource.FromVisual(this);if(src==null)return;var area=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;var logical=src.CompositionTarget.TransformFromDevice.Transform(new Vector(area.Width,area.Height));double scale=Math.Min(1,Math.Min((logical.X-32)/1280,(logical.Y-32)/720));if(Math.Abs(Width-1280*scale)>.5){Width=1280*scale;Height=720*scale;}}
        void Capture(string path){var bmp=new RenderTargetBitmap(1280,720,96,96,PixelFormats.Pbgra32);bmp.Render(stage);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))png.Save(f);}
        string FindRoot(){var d=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);while(d!=null){if(File.Exists(Path.Combine(d.FullName,"暂时展示用.jpg")))return d.FullName;d=d.Parent;}return AppDomain.CurrentDomain.BaseDirectory;}
        internal bool PrepareExit(){return CanLeave();}
        bool CanLeave(){if(!dirty)return true;if(liveMode)return ConfirmLiveLeave();var result=MessageBox.Show(this,liveMode?"有未保存的修改。\n是：保存并继续；否：放弃修改；取消：留在当前页。":"保存本次样板草稿后继续？\n是：仅保存到本次预览；否：放弃修改；取消：留在当前页。","未保存修改",MessageBoxButton.YesNoCancel);if(result==MessageBoxResult.Cancel)return false;if(result==MessageBoxResult.Yes){Save();return !dirty;}else dirty=false;return true;}
        void Save(){if(liveMode){SaveShortcut();return;}savedKey=key.Text;savedTarget=target.Text;dirty=false;status.Text="已保存到本次预览 · 未修改实际快捷键";}
        void Navigate(string page){if(!CanLeave())return;if(liveMode){NavigateLive(page);return;}home=page=="首页";folded=false;settingsToggle.Visibility=home?Visibility.Collapsed:Visibility.Visible;foreach(var pair in tiles){pair.Value.Tag=pair.Key==page;}title.Text=home?"KEDIT\n让操作，随心而动。":page.Replace("\n", " ")+"  /  布局预览";title.FontSize=home?34:23;news.Visibility=home?Visibility.Visible:Visibility.Collapsed;panel.Visibility=home?Visibility.Collapsed:Visibility.Visible;form.Children.Clear();formHeader.Children.Clear();formFooter.Children.Clear();subnav.Children.Clear();
            nav.Visibility=home?Visibility.Collapsed:Visibility.Visible; if(!home){subnav.Children.Add(Button("☰",()=>{navFolded=!navFolded;nav.Width=navFolded?45:92;foreach(var child in subnav.Children){var b=child as Button;if(b!=null&&b.Tag!=null)b.Content=navFolded?b.Tag:b.ToolTip;}}));foreach(string name in new[]{"⌘ 快速打开","◇ 使用说明"}){var entry=Button(name,()=>{panel.Visibility=Visibility.Visible;folded=false;});entry.ToolTip=name;entry.Tag=name.Substring(0,1);entry.Content=navFolded?entry.Tag:entry.ToolTip;subnav.Children.Add(entry);}
                formHeader.Children.Add(Text("快速打开",23));formHeader.Children.Add(Text("布局样板 · 设置仅保留在本次预览",12,"#F4F51A"));form.Children.Add(Text("快捷键",12));key.Text=savedKey;key.Margin=new Thickness(0,0,0,18);key.Padding=new Thickness(8);key.FontSize=14;form.Children.Add(key);form.Children.Add(Text("打开路径或目标",12));target.Text=savedTarget;target.TextWrapping=TextWrapping.Wrap;target.Padding=new Thickness(8);target.FontSize=14;form.Children.Add(target);
                form.Children.Add(Text("选择常用项目、文件夹或程序，通过一个快捷键快速访问。",13,"#ADB5C6"));form.Children.Add(Button("选择路径",()=>{var d=new OpenFileDialog();if(d.ShowDialog()==true)target.Text=d.FileName;}));form.Children.Add(Text("作用范围：全局\n执行方式：打开目标\n演示素材：暂用现有视频，可自行选择",12,"#A8B0BD"));formFooter.Children.Add(Button("保存预览草稿",Save,true));formFooter.Children.Add(Button("取消修改",()=>{key.Text=savedKey;target.Text=savedTarget;dirty=false;status.Text="已恢复预览值";}));status.Foreground=B("#C5C9D0");status.TextWrapping=TextWrapping.Wrap;status.Text="尚未接入真实配置";formFooter.Children.Add(status);}
            dirty=false;key.TextChanged-=Changed;target.TextChanged-=Changed;key.TextChanged+=Changed;target.TextChanged+=Changed;LoadMedia(Path.Combine(root,home?"side.mp4":"update_bg.mp4"));
        }
        void Changed(object s,TextChangedEventArgs e){if(liveMode){dirty=key.Text!=savedKey;return;}dirty=key.Text!=savedKey||target.Text!=savedTarget;status.Text=dirty?"有未保存修改":"预览草稿";}
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
            tiles[name]=b;b.Width=51;b.Height=51;b.HorizontalAlignment=HorizontalAlignment.Center;b.BorderBrush=B("#35373C");b.BorderThickness=new Thickness(1);
            var t=new ControlTemplate(typeof(Button));var edge=new FrameworkElementFactory(typeof(Border));edge.Name="chrome";edge.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));
            foreach(string prop in new[]{"Background","BorderBrush","BorderThickness"})edge.SetBinding(prop=="Background"?Border.BackgroundProperty:prop=="BorderBrush"?Border.BorderBrushProperty:Border.BorderThicknessProperty,new System.Windows.Data.Binding(prop){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});

            var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);edge.AppendChild(content);var layers=new FrameworkElementFactory(typeof(Grid));var shadow=new FrameworkElementFactory(typeof(Border));shadow.SetValue(Border.BackgroundProperty,Brushes.Black);shadow.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));shadow.SetValue(UIElement.EffectProperty,new System.Windows.Media.Effects.DropShadowEffect {Color=Colors.Black,BlurRadius=6,ShadowDepth=3,Opacity=.6});layers.AppendChild(shadow);layers.AppendChild(edge);
            var selection=new FrameworkElementFactory(typeof(Border));selection.Name="selection";selection.SetValue(FrameworkElement.MarginProperty,new Thickness(-5));selection.SetValue(Border.CornerRadiusProperty,new CornerRadius(12));selection.SetValue(Border.BorderBrushProperty,Brushes.White);selection.SetValue(Border.BorderThicknessProperty,new Thickness(2));selection.SetValue(UIElement.IsHitTestVisibleProperty,false);selection.SetValue(UIElement.VisibilityProperty,Visibility.Collapsed);layers.AppendChild(selection);
            var selected=new Trigger {Property=FrameworkElement.TagProperty,Value=true};selected.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"selection"));t.Triggers.Add(selected);t.VisualTree=layers;AddFeedback(t,false);b.Template=t;RefreshTile(name);
        }
        void RefreshTile(string name){
            try {BitmapImage bitmap=null;string file=TileFile(name);if(File.Exists(file)){bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(file);bitmap.EndInit();}
                else using(var input=typeof(DesignPreview).Assembly.GetManifestResourceStream("PreviewAssets/"+TileKey(name)+".png")){if(input!=null){bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=input;bitmap.EndInit();}}
                if(bitmap!=null){bitmap.Freeze();tiles[name].Content=new Image {Source=bitmap,Width=47,Height=47,Stretch=Stretch.UniformToFill,Clip=new RectangleGeometry(new Rect(0,0,47,47),5,5)};}
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
