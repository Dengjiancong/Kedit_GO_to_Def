using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Kedit.Console {
    internal sealed class PreviewCrop : Window {
        readonly Canvas canvas=new Canvas {Width=280,Height=280,Background=Brushes.Black,ClipToBounds=true};
        readonly Image image=new Image();readonly Slider zoom=new Slider {Minimum=1,Maximum=4,Value=1};
        readonly BitmapSource source;double baseScale,x,y;Point last;bool dragging;
        internal BitmapSource Result {get;private set;}
        internal PreviewCrop(Window owner,string file){
            Owner=owner;Title="裁切图片 · 拖动调整位置";Width=380;Height=470;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=new SolidColorBrush(Color.FromRgb(26,29,36));
            var bmp=new BitmapImage();bmp.BeginInit();bmp.CacheOption=BitmapCacheOption.OnLoad;bmp.UriSource=new Uri(file);bmp.EndInit();bmp.Freeze();source=bmp;
            var panel=new StackPanel {Margin=new Thickness(24)};Content=panel;panel.Children.Add(new TextBlock {Text="拖动图片选择画面，滑动调整大小",Foreground=Brushes.White,Margin=new Thickness(0,0,0,14)});
            panel.Children.Add(canvas);image.Source=source;canvas.Children.Add(image);baseScale=Math.Max(280.0/source.PixelWidth,280.0/source.PixelHeight);x=(280-source.PixelWidth*baseScale)/2;y=(280-source.PixelHeight*baseScale)/2;Update();
            canvas.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){last=e.GetPosition(canvas);dragging=canvas.CaptureMouse();};canvas.MouseMove+=delegate(object s,MouseEventArgs e){if(!dragging)return;var now=e.GetPosition(canvas);x+=now.X-last.X;y+=now.Y-last.Y;last=now;Update();};canvas.MouseLeftButtonUp+=delegate{dragging=false;canvas.ReleaseMouseCapture();};canvas.LostMouseCapture+=delegate{dragging=false;};
            zoom.Margin=new Thickness(0,18,0,12);panel.Children.Add(zoom);zoom.ValueChanged+=delegate(object s,RoutedPropertyChangedEventArgs<double> e){double ratio=e.NewValue/e.OldValue;x=140+(x-140)*ratio;y=140+(y-140)*ratio;Update();};canvas.MouseWheel+=delegate(object s,MouseWheelEventArgs e){zoom.Value=Math.Max(1,Math.Min(4,zoom.Value+(e.Delta>0?.1:-.1)));};
            var save=new Button {Content="使用这张图片",Padding=new Thickness(10),Background=Brushes.LightYellow};save.Click+=delegate{Result=Crop(source,baseScale*zoom.Value,x,y);DialogResult=true;};panel.Children.Add(save);
        }
        void Update(){double w=source.PixelWidth*baseScale*zoom.Value,h=source.PixelHeight*baseScale*zoom.Value;x=Math.Max(280-w,Math.Min(0,x));y=Math.Max(280-h,Math.Min(0,y));image.Width=w;image.Height=h;Canvas.SetLeft(image,x);Canvas.SetTop(image,y);}
        internal static BitmapSource Crop(BitmapSource source,double scale,double x,double y){var v=new DrawingVisual();using(var dc=v.RenderOpen())dc.DrawImage(source,new Rect(x,y,source.PixelWidth*scale,source.PixelHeight*scale));var b=new RenderTargetBitmap(280,280,96,96,PixelFormats.Pbgra32);b.Render(v);b.Freeze();return b;}
        internal static void Save(BitmapSource source,string file){Directory.CreateDirectory(Path.GetDirectoryName(file));var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(source));using(var stream=File.Create(file))e.Save(stream);}
    }
}
