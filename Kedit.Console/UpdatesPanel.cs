using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Kedit.Console {
 internal sealed partial class DesignPreview {
  Border updateCard;
  void ShowUpdates(){
   home=false;dirty=false;currentMediaPage="updates";LoadPageMedia(System.IO.Path.Combine(root,"update_bg.mp4"));title.Text="更新";title.FontSize=23;
   foreach(var tile in tiles)tile.Value.Tag=tile.Key=="更新";
   if(legacyPanel!=null)legacyPanel.Visibility=Visibility.Collapsed;if(usagePanel!=null)usagePanel.Visibility=Visibility.Collapsed;
   news.Visibility=nav.Visibility=Visibility.Collapsed;settingsToggle.Visibility=Visibility.Visible;panel.Visibility=folded?Visibility.Collapsed:Visibility.Visible;
   form.Children.Clear();formHeader.Children.Clear();formFooter.Children.Clear();formHeader.Children.Add(Text("更新设置",24));
   var app=(App)Application.Current;
   if(liveMode){var legacy=app.Legacy;var old=legacy.AutoUpdateToggle.Parent as Panel;if(old!=null)old.Children.Remove(legacy.AutoUpdateToggle);form.Children.Add(legacy.AutoUpdateToggle);old=legacy.OsdToggle.Parent as Panel;if(old!=null)old.Children.Remove(legacy.OsdToggle);form.Children.Add(legacy.OsdToggle);}
   else form.Children.Add(Text("请从快捷键主程序打开中控，以修改自动更新设置。",13));
   form.Children.Add(Text("设置立即生效。检查到新版本后，将打开原有更新确认卡片，继续使用现有下载、取消与安装流程。",12,"#AAB6C8"));
   if(updateCard==null){var content=new StackPanel();updateCard=new Border{Width=240,Padding=new Thickness(20),CornerRadius=new CornerRadius(14),Background=B("#F0181818"),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,24,76),Child=content};content.Children.Add(Text("软件更新",23));content.Children.Add(Text("检查是否有新版本可用",13,"#B9C2CA"));var button=Button("▶  检查更新",()=>{if(!liveMode||!app.Legacy.SendCommandToAhk("check_updates"))MessageBox.Show(this,"请先启动快捷键主程序，再检查更新。");},true,22);button.Background=B("#FFDE32");content.Children.Add(button);scene.Children.Add(updateCard);}
   // Keep the original right-hand settings panel clear of the update card.
   panel.Margin=new Thickness(0,110,300,72);updateCard.Margin=new Thickness(0,0,24,76);updateCard.Visibility=Visibility.Visible;
  }
 }
}
