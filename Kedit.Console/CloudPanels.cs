using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Kedit.Console {
    internal sealed partial class DesignPreview {
        HomeContent homeContent;int slideIndex,tabIndex,pictureRequest;bool cloudRefreshing,slideAnimating;Image displayedSlide;
        Grid slideHost;StackPanel dots,cloudTabs,cloudItems;DispatcherTimer carouselTimer;string cloudResult="尚未刷新";
        void InitializeCloudHome(){
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--self-test-usage")>=0)return;
            news.CornerRadius=new CornerRadius(16);news.BorderBrush=B("#454348");news.BorderThickness=new Thickness(1);news.Background=B("#ED19191D");
            news.SizeChanged+=delegate{news.Clip=new RectangleGeometry(new Rect(0,0,news.ActualWidth,news.ActualHeight),16,16);};
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(250)});grid.ColumnDefinitions.Add(new ColumnDefinition());news.Child=grid;
            slideHost=new Grid{ClipToBounds=true,Cursor=System.Windows.Input.Cursors.Hand,Background=B("#273344")};grid.Children.Add(slideHost);
            slideHost.MouseLeftButtonUp+=delegate{if(homeContent.carousel.items.Length>0)CloudContent.Open(homeContent.carousel.items[slideIndex].target_url);};
            dots=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,5)};grid.Children.Add(dots);
            grid.Children.Add(CarouselArrow(false));grid.Children.Add(CarouselArrow(true));
            var right=new Grid{Margin=new Thickness(20,10,12,10)};right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition());Grid.SetColumn(right,1);grid.Children.Add(right);
            cloudTabs=new StackPanel{Orientation=Orientation.Horizontal};right.Children.Add(cloudTabs);cloudItems=new StackPanel();var scroll=new ScrollViewer{Content=cloudItems,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};ConfigureScroll(scroll);Grid.SetRow(scroll,1);right.Children.Add(scroll);
            using(var fallback=typeof(CloudContent).Assembly.GetManifestResourceStream("PreviewAssets/kedit.png")){if(fallback!=null){using(var bytes=new System.IO.MemoryStream()){fallback.CopyTo(bytes);slideHost.Background=new ImageBrush(CloudContent.Decode(bytes.ToArray())){Stretch=Stretch.UniformToFill,Opacity=0.4};}}}
            homeContent=CloudContent.Cached<HomeContent>("home.json");RenderCloudHome();
            carouselTimer=new DispatcherTimer();carouselTimer.Tick+=delegate{if(slideAnimating||!IsVisible||WindowState==WindowState.Minimized||news.Visibility!=Visibility.Visible||(homeContent.carousel.pause_on_hover&&news.IsMouseOver)||homeContent.carousel.items.Length<2)return;slideIndex=(slideIndex+1)%homeContent.carousel.items.Length;ShowCloudSlide(true);};carouselTimer.Interval=TimeSpan.FromSeconds(Math.Max(2,homeContent.carousel.interval_seconds));carouselTimer.Start();Closed+=delegate{carouselTimer.Stop();pictureRequest++;};
            Loaded+=async delegate{await RefreshCloudHome();};
        }
        Button CarouselArrow(bool right){
            var button=new Button{Width=28,Height=28,HorizontalAlignment=right?HorizontalAlignment.Right:HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,10,0),Background=B("#506B819B"),BorderThickness=new Thickness(0),Cursor=System.Windows.Input.Cursors.Hand,ToolTip=right?"向右切换":"向左切换"};
            var arrow=new System.Windows.Shapes.Path{Data=Geometry.Parse(right?"M 0,0 L 4,4 L 0,8":"M 4,0 L 0,4 L 4,8"),Stroke=B("#EDF3FA"),StrokeThickness=1.3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};button.Content=arrow;
            var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.Name="chrome";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);template.VisualTree=border;
            var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,B("#90758BA5"),"chrome"));template.Triggers.Add(hover);var pressed=new Trigger{Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true};pressed.Setters.Add(new Setter(UIElement.OpacityProperty,.65));template.Triggers.Add(pressed);button.Template=template;
            button.Click+=delegate(object sender,RoutedEventArgs e){e.Handled=true;int count=homeContent.carousel.items.Length;if(slideAnimating||count<2)return;slideIndex=(slideIndex+(right?-1:1)+count)%count;ShowCloudSlide(true,right?1:-1);carouselTimer.Stop();carouselTimer.Start();};return button;
        }
        async Task RefreshCloudHome(){if(cloudRefreshing)return;cloudRefreshing=true;try{var updated=await CloudContent.Refresh<HomeContent>("home.json");homeContent=updated;slideIndex=0;tabIndex=0;RenderCloudHome();ShowCloudSlide(false);carouselTimer.Interval=TimeSpan.FromSeconds(Math.Max(2,Math.Min(60,homeContent.carousel.interval_seconds)));cloudResult="云端公告已更新："+DateTime.Now.ToString("HH:mm:ss");}catch(Exception ex){cloudResult="保留本地内容；云端更新失败："+ex.Message;}finally{cloudRefreshing=false;}}
        Button CloudButton(string text,Action action){
            var button=Button(text,action);var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.Name="chrome";border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
            border.SetBinding(Border.PaddingProperty,new System.Windows.Data.Binding("Padding"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Stretch);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(presenter);template.VisualTree=border;
            var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(UIElement.OpacityProperty,.8));template.Triggers.Add(hover);button.Template=template;return button;
        }
        void RenderCloudHome(){
            cloudTabs.Children.Clear();
            for(int i=0;i<homeContent.tabs.Length;i++){
                int index=i;var label=new StackPanel();label.Children.Add(new TextBlock{Text=homeContent.tabs[i].title,FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=i==tabIndex?Brushes.White:B("#85858A")});
                label.Children.Add(new Border{Height=3,Margin=new Thickness(0,5,0,0),Background=i==tabIndex?B("#F4F51A"):Brushes.Transparent});
                var button=CloudButton("",()=>{tabIndex=index;RenderCloudHome();});button.Content=label;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;button.Margin=new Thickness(0,0,23,9);cloudTabs.Children.Add(button);
            }
            cloudItems.Children.Clear();
            if(homeContent.tabs.Length>0){var items=homeContent.tabs[tabIndex].items;if(items.Length==0)cloudItems.Children.Add(Text("暂无内容",12,"#9399A5"));
                foreach(var item in items){
                    var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(48)});
                    DateTime date;string displayDate=DateTime.TryParse(item.date,out date)?date.ToString("MM/dd"):item.date??"";
                    var time=new TextBlock{Text=displayDate,Foreground=B("#85858A"),FontSize=12,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(time,1);row.Children.Add(time);
                    row.Children.Add(new TextBlock{Text=item.title,Foreground=B("#D1D1D5"),FontSize=13,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)});
                    var button=CloudButton("",()=>CloudContent.Open(item.target_url));button.Content=row;button.ToolTip=item.title;button.Background=Brushes.Transparent;button.Padding=new Thickness(0,4,0,4);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,0,0,2);cloudItems.Children.Add(button);
                }
            }
            // Switching text tabs must not reset an in-flight carousel transition.
            if(displayedSlide==null)ShowCloudSlide(false);
        }
        async void ShowCloudSlide(bool animate,int direction=-1){
            if(slideAnimating)return;int request=++pictureRequest;
            if(homeContent.carousel.items.Length==0){slideHost.Children.Clear();displayedSlide=null;dots.Children.Clear();return;}
            var item=homeContent.carousel.items[slideIndex];
            if(slideHost.Children.Count==0)slideHost.Children.Add(new TextBlock{Text="KEDIT\n"+item.title,Foreground=Brushes.White,FontSize=18,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(16),VerticalAlignment=VerticalAlignment.Center});
            try{
                var source=await CloudContent.Picture(item.image_url);if(request!=pictureRequest)return;
                var image=new Image{Source=source,Stretch=Stretch.UniformToFill};var previous=displayedSlide;
                slideHost.ToolTip=item.title;
                if(!animate||previous==null){slideHost.Children.Clear();slideHost.Children.Add(image);displayedSlide=image;}
                else{
                    double width=slideHost.ActualWidth>0?slideHost.ActualWidth:250;
                    var oldTransform=new TranslateTransform();var newTransform=new TranslateTransform(-direction*width,0);
                    previous.RenderTransform=oldTransform;image.RenderTransform=newTransform;slideHost.Children.Add(image);slideAnimating=true;
                    var duration=TimeSpan.FromMilliseconds(420);var easing=new CubicEase{EasingMode=EasingMode.EaseInOut};
                    var outgoing=new DoubleAnimation(0,direction*width,duration){EasingFunction=easing};var incoming=new DoubleAnimation(-direction*width,0,duration){EasingFunction=easing};
                    incoming.Completed+=delegate{slideHost.Children.Remove(previous);image.RenderTransform=Transform.Identity;displayedSlide=image;slideAnimating=false;};
                    oldTransform.BeginAnimation(TranslateTransform.XProperty,outgoing);newTransform.BeginAnimation(TranslateTransform.XProperty,incoming);
                }
                dots.Children.Clear();for(int i=0;i<homeContent.carousel.items.Length;i++){int index=i;var button=Button("●",()=>{if(slideAnimating||slideIndex==index)return;slideIndex=index;ShowCloudSlide(true);});button.FontSize=9;button.Padding=new Thickness(3,0,3,0);button.Background=Brushes.Transparent;button.Foreground=i==slideIndex?Brushes.White:Brushes.Gray;dots.Children.Add(button);}
            }catch{ /* Keep the last decoded image while a replacement is unavailable. */ }
        }

    }
    internal static class ModelDownloadDialog {
        public static void Show(Window owner,PetController pet,bool enable){
            var window=new Window{Owner=owner,Title="下载兔兔模型",Width=440,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=new SolidColorBrush(Color.FromRgb(28,32,40))};
            var panel=new StackPanel{Margin=new Thickness(22)};window.Content=panel;var info=new TextBlock{Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap};panel.Children.Add(info);var progress=new ProgressBar{Height=6,Margin=new Thickness(0,15,0,15),Maximum=100};panel.Children.Add(progress);var status=new TextBlock{Foreground=Brushes.LightGray,TextWrapping=TextWrapping.Wrap};panel.Children.Add(status);
            ModelContent content=CloudContent.Cached<ModelContent>("models.json");DownloadModel model=content.models.First(x=>x.id==content.default_model_id);bool running=false,closed=false;var cancellation=new CancellationTokenSource();
            Action update=()=>{info.Text=model.name+" · "+model.version+" · "+model.size_display+"\n作者："+(model.author==null?"见模型说明":model.author.name)+"\n下载完成后自动选择模型，保留原包署名和使用说明。";};update();
            Func<string,Action,Button> button=(text,action)=>{var b=new Button{Content=text,Margin=new Thickness(0,8,0,0),Padding=new Thickness(8)};b.Click+=delegate{action();};panel.Children.Add(b);return b;};
            Button start=null,company=null;Action<bool> download=async companyOnly=>{if(running)return;running=true;start.IsEnabled=false;company.IsEnabled=false;string stage="正在从公司网盘复制模型";status.Text=stage;try{string path=await CloudContent.Install(model,cancellation.Token,new Progress<int>(n=>{progress.IsIndeterminate=n<0;if(n>=0)progress.Value=n;status.Text=stage+(n<0?"…":" "+n+"%");}),companyOnly,new Progress<string>(message=>{stage=message;status.Text=message;}));if(closed)return;pet.SelectModel(path);if(enable)pet.SetEnabled(true);status.Text="已安装并选择兔兔模型。";}catch(OperationCanceledException){if(!closed)status.Text="已取消下载。";}catch(Exception ex){if(!closed)status.Text="下载未完成："+ex.Message+"\n可使用下方网页入口。";}finally{running=false;if(!closed){start.IsEnabled=true;company.IsEnabled=true;}}};start=button("下载并使用兔兔",()=>download(false));
            button("打开Pingvin Share",()=>CloudContent.Open(model.share_url));company=button("从公司网盘路径下载",()=>download(true));button("前往作者原下载页",()=>CloudContent.Open(model.original_download_page));button("作者主页",()=>{if(model.author!=null)CloudContent.Open(model.author.url);});button("取消 / 关闭",()=>window.Close());window.Closed+=delegate{closed=true;cancellation.Cancel();};
            window.Loaded+=async delegate{try{var updated=await CloudContent.Refresh<ModelContent>("models.json");if(!closed&&!running){model=updated.models.First(x=>x.id==updated.default_model_id);update();status.Text="已取得最新模型信息。";}}catch{if(!closed&&!running)status.Text="云端不可用，使用内置或缓存的模型信息。";}};window.ShowDialog();
        }
    }
}
