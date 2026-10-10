using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace Kedit.Console {
 internal sealed partial class DesignPreview {
  Border updateCard,updateBadge;TextBlock updateHeading,updateVersion,updateDetail,updateProgressText,updatePreviewLabel;
  ProgressBar updateProgress;StackPanel updateActions;bool updatesActive,updateSettingsExpanded,updateSubscribed;
  UpdateSnapshot previewUpdate;UpdateStage? renderedUpdateStage;
  UpdateService Updates {get{return ((App)Application.Current).Updates;}}
  void LeaveUpdatePage(){if(updateHold!=null){scene.Children.Remove(updateHold);updateHold=null;}updatesActive=false;previewUpdate=null;renderedUpdateStage=null;if(settingsToggle!=null)settingsToggle.Content=folded?"展开设置":"收起设置";}
  void ToggleSettings(){if(updatesActive){updateSettingsExpanded=!updateSettingsExpanded;panel.Visibility=updateSettingsExpanded?Visibility.Visible:Visibility.Collapsed;settingsToggle.Content=updateSettingsExpanded?"收起设置":"展开设置";return;}folded=!folded;settingsToggle.Content=folded?"展开设置":"收起设置";panel.Visibility=home||folded?Visibility.Collapsed:Visibility.Visible;}
  void InitializeUpdates(){
   if(updateSubscribed)return;updateSubscribed=true;
   Updates.Changed+=()=>Dispatcher.BeginInvoke(new Action(()=>{if(previewUpdate!=null&&Updates.PreviewBlocked){previewUpdate=null;renderedUpdateStage=null;}UpdateBadge();if(updatesActive&&previewUpdate==null)RenderUpdate();}));
   var tile=tiles["更新"];var container=tile.Parent as Panel;
   if(container!=null){int index=container.Children.IndexOf(tile);container.Children.Remove(tile);var wrapper=new Grid();wrapper.Children.Add(tile);updateBadge=new Border{Width=9,Height=9,CornerRadius=new CornerRadius(5),Background=B("#FFDE32"),BorderBrush=B("#191A1D"),BorderThickness=new Thickness(1),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,6,7,0),IsHitTestVisible=false,Visibility=Visibility.Collapsed};wrapper.Children.Add(updateBadge);container.Children.Insert(index,wrapper);}
   debugTools.Children.Add(Button("更新状态预览",ShowUpdatePreview));UpdateBadge();
  }
  void UpdateBadge(){if(updateBadge!=null)updateBadge.Visibility=Updates.Available?Visibility.Visible:Visibility.Collapsed;}
  void ShowUpdates(){
   updatesActive=true;updateSettingsExpanded=false;previewUpdate=null;renderedUpdateStage=null;home=false;dirty=false;title.Text="更新";title.FontSize=23;
   foreach(var tile in tiles)tile.Value.Tag=tile.Key=="更新";
   if(legacyPanel!=null)legacyPanel.Visibility=Visibility.Collapsed;if(usagePanel!=null)usagePanel.Visibility=Visibility.Collapsed;
   news.Visibility=nav.Visibility=Visibility.Collapsed;settingsToggle.Visibility=Visibility.Visible;settingsToggle.Content="展开设置";panel.Visibility=Visibility.Collapsed;
   form.Children.Clear();formHeader.Children.Clear();formFooter.Children.Clear();formHeader.Children.Add(Text("更新设置",24));
   var app=(App)Application.Current;
   if(liveMode){var toggle=app.Legacy.AutoUpdateToggle;var old=toggle.Parent as Panel;if(old!=null)old.Children.Remove(toggle);form.Children.Add(toggle);}
   else form.Children.Add(Text("从快捷键主程序启动后，可检查当前版本及修改自动检查设置。",13));
   form.Children.Add(Button("检查更新",CheckUpdate));form.Children.Add(Button("网络诊断",DiagnoseUpdate));
   form.Children.Add(Button("查看发行页面",()=>System.Diagnostics.Process.Start(UpdateService.Repository+"/releases")));
   form.Children.Add(Text("启动后延迟检查，不自动下载。检查和下载在后台继续；切换页面或收起设置不会中断任务。",12,"#AAB6C8"));
   form.Children.Add(Text("更新来源：NAS · Gitea",12,"#AAB6C8"));
   EnsureUpdateCard();panel.Margin=new Thickness(0,110,324,72);updateCard.Visibility=Visibility.Visible;RenderUpdate();
  }
  void EnsureUpdateCard(){if(updateCard!=null)return;var content=new StackPanel();updateCard=new Border{Width=278,Padding=new Thickness(23,20,23,18),CornerRadius=new CornerRadius(17),Background=B("#F218181B"),BorderBrush=B("#3B3D43"),BorderThickness=new Thickness(1),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,24,76),Child=content};
   updatePreviewLabel=Text("管理员预览 · 不执行真实更新",11,"#FFDE32");content.Children.Add(updatePreviewLabel);
   updateHeading=Text("",23);updateHeading.FontWeight=FontWeights.Bold;content.Children.Add(updateHeading);
   updateVersion=Text("",12,"#D6DCE5");content.Children.Add(updateVersion);updateDetail=Text("",13,"#ADB5C1");updateDetail.MaxHeight=112;content.Children.Add(updateDetail);
   updateProgress=new ProgressBar{Height=7,Minimum=0,Maximum=100,Foreground=B("#FFDE32"),Background=B("#343841"),BorderThickness=new Thickness(0),Margin=new Thickness(0,4,0,10)};content.Children.Add(updateProgress);
   updateProgressText=Text("",11,"#C4CBD4");content.Children.Add(updateProgressText);updateActions=new StackPanel();content.Children.Add(updateActions);scene.Children.Add(updateCard);
  }
  void CheckUpdate(){if(previewUpdate!=null){MessageBox.Show(this,"请先退出管理员预览，再执行真实检查。");return;}if(!liveMode){MessageBox.Show(this,"请从快捷键主程序打开中控。布局预览不执行真实更新。");return;}Updates.Check();}
  async void DiagnoseUpdate(){if(previewUpdate!=null){MessageBox.Show(this,"管理员预览：此处展示网络诊断，不发起请求。");return;}var dialog=new Window{Owner=this,Title="更新网络诊断 · NAS Gitea",Width=720,Height=460,Background=B("#191D26"),WindowStartupLocation=WindowStartupLocation.CenterOwner};var stack=new DockPanel{Margin=new Thickness(18)};var output=new TextBox{Text="正在检查版本接口与发行文件地址…",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=B("#101820"),Foreground=Brushes.White};var copy=Button("复制诊断结果",()=>{try{Clipboard.SetText(output.Text);}catch(Exception ex){MessageBox.Show(dialog,ex.Message);}});DockPanel.SetDock(copy,Dock.Bottom);stack.Children.Add(copy);stack.Children.Add(output);dialog.Content=stack;dialog.Show();output.Text=await Updates.Diagnose();}
  void RenderUpdate(){if(!updatesActive)return;EnsureUpdateCard();var state=previewUpdate??Updates.State;bool transition=renderedUpdateStage!=state.Stage;updatePreviewLabel.Visibility=previewUpdate==null?Visibility.Collapsed:Visibility.Visible;
   updateHeading.Text=UpdateTitle(state.Stage);updateVersion.Text="当前："+(string.IsNullOrEmpty(state.Current)?"等待连接":state.Current)+(string.IsNullOrEmpty(state.Latest)?"":"\n最新："+state.Latest)+"\n来源："+state.Source;updateDetail.Text=state.Detail;updateDetail.ToolTip=state.Detail;
   bool progressing=state.Stage==UpdateStage.Downloading||state.Stage==UpdateStage.Verifying;updateProgress.Visibility=updateProgressText.Visibility=progressing?Visibility.Visible:Visibility.Collapsed;updateProgress.IsIndeterminate=state.Stage==UpdateStage.Verifying||state.Total<=0;
   if(progressing){double value=state.Total>0?Math.Min(100,100.0*state.Bytes/state.Total):0;updateProgress.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,new DoubleAnimation(value,TimeSpan.FromMilliseconds(120)));updateProgressText.Text=state.Stage==UpdateStage.Verifying?"下载结束不等于校验通过":(state.Bytes/1048576.0).ToString("0.0")+" MB"+(state.Total>0?" / "+(state.Total/1048576.0).ToString("0.0")+" MB · "+value.ToString("0")+"%":" · 总大小未知");}
   if(transition){renderedUpdateStage=state.Stage;UpdateBackground(state.Stage);updateActions.Children.Clear();
    if(state.Stage==UpdateStage.Available||state.Stage==UpdateStage.DownloadFailed||state.Stage==UpdateStage.Cancelled){AddUpdateAction("▶  "+(state.Stage==UpdateStage.Available?"立即更新":"重试"),()=>{if(Updates.Available)Updates.Download();else CheckUpdate();},true);AddUpdateAction("暂不更新",()=>Navigate("首页"),false);}
    else if(state.Stage==UpdateStage.Downloading||state.Stage==UpdateStage.Verifying)AddUpdateAction("取消下载",()=>Updates.Cancel(),false);
    else if(state.Stage==UpdateStage.Ready){AddUpdateAction(Updates.Compiled||previewUpdate!=null?"安装并重启":"打开下载位置",InstallUpdate,true);AddUpdateAction("取消本次更新",()=>Updates.Discard(),false);}
    else if(state.Stage==UpdateStage.Connecting)AddUpdateAction("取消检查",()=>Updates.Cancel(),false);
    else if(state.Stage!=UpdateStage.Installing)AddUpdateAction(state.Stage==UpdateStage.ConnectionFailed?"重新连接":"检查更新",CheckUpdate,true);
    if(state.Stage==UpdateStage.Latest)AddUpdateAction("返回首页",()=>Navigate("首页"),false);
    if(state.Stage!=UpdateStage.Idle&&state.Stage!=UpdateStage.Installing)AddUpdateAction("查看详情 / 更新说明",ShowUpdateDetails,false);
    if(state.Stage!=UpdateStage.Installing)AddUpdateAction("网络诊断",DiagnoseUpdate,false);
    var offset=new TranslateTransform(0,9);updateCard.RenderTransform=offset;offset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(9,0,TimeSpan.FromMilliseconds(180)));updateCard.BeginAnimation(OpacityProperty,new DoubleAnimation(.5,1,TimeSpan.FromMilliseconds(180)));
   }
  }
  void ShowUpdateDetails(){var state=previewUpdate??Updates.State;var dialog=new Window{Owner=this,Title="更新详情",Width=700,Height=460,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#191D26")};dialog.Content=new TextBox{Margin=new Thickness(20),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=B("#101820"),Foreground=Brushes.White,Text=UpdateTitle(state.Stage)+"\n"+state.Source+"\n"+state.Detail+"\n\n更新说明：\n"+(string.IsNullOrEmpty(Updates.ReleaseNotes)?"发行方未提供说明。":Updates.ReleaseNotes)};dialog.Show();}
  void AddUpdateAction(string label,Action action,bool primary){var button=Button(label,()=>{if(previewUpdate!=null){status.Text="管理员预览不会执行真实操作";return;}action();},primary,22);if(primary){button.Background=B("#FFDE32");button.FontWeight=FontWeights.Bold;button.Height=40;}else {button.Background=Brushes.Transparent;button.Foreground=B("#A8AEB9");button.FontSize=12;}updateActions.Children.Add(button);}
  static string UpdateTitle(UpdateStage state){switch(state){case UpdateStage.Connecting:return "正在连接…";case UpdateStage.ConnectionFailed:return "连接暂时失败";case UpdateStage.Latest:return "已经是最新版本";case UpdateStage.Available:return "发现新版本！";case UpdateStage.Downloading:return "正在下载更新";case UpdateStage.Verifying:return "正在检查文件";case UpdateStage.Ready:return "准备安装";case UpdateStage.Installing:return "正在准备重启";case UpdateStage.DownloadFailed:return "更新未完成";case UpdateStage.Cancelled:return "操作已取消";default:return "软件更新";}}
  static string UpdateMediaKey(UpdateStage state){switch(state){case UpdateStage.Idle:case UpdateStage.Connecting:return "connecting";case UpdateStage.ConnectionFailed:return "connection-failed";case UpdateStage.Latest:return "latest";case UpdateStage.Downloading:return "downloading";case UpdateStage.Verifying:return "verifying";case UpdateStage.Ready:return "ready";case UpdateStage.Installing:return "installing";case UpdateStage.DownloadFailed:return "download-failed";case UpdateStage.Cancelled:return "cancelled";default:return "available";}}
  string DefaultUpdateMedia(string name){string local=Path.Combine(root,name);if(File.Exists(local))return local;var assembly=typeof(DesignPreview).Assembly;string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Kedit","UpdateMedia",assembly.ManifestModule.ModuleVersionId.ToString());Directory.CreateDirectory(folder);string dest=Path.Combine(folder,name);if(!File.Exists(dest))using(var input=assembly.GetManifestResourceStream("UpdateDefaults/"+name)){if(input==null)return "";using(var output=File.Create(dest))input.CopyTo(output);}return dest;}
  string UpdateParentKey(string key){if(key=="updates.connection-failed")return "updates.connecting";if(key=="updates.latest"||key=="updates.connecting")return "updates";return "updates.available";}
  string UpdateFallbackPage(string key){if(pageMedia.Has(key))return key;string parent=UpdateParentKey(key);if(pageMedia.Has(parent))return parent;return pageMedia.Has("updates")?"updates":key;}
  void UpdateBackground(UpdateStage state){currentMediaPage="updates."+UpdateMediaKey(state);string name=state==UpdateStage.Latest?"latest_bg.mp4":state==UpdateStage.Idle||state==UpdateStage.Connecting||state==UpdateStage.ConnectionFailed||(state==UpdateStage.Cancelled&&!Updates.Available&&previewUpdate==null)?"waiting.mp4":"update_bg.mp4";LoadPageMedia(DefaultUpdateMedia(name));}
  async void InstallUpdate(){
   if(previewUpdate!=null||Updates.State.Stage!=UpdateStage.Ready)return;
   if(!Updates.Compiled){System.Diagnostics.Process.Start("explorer.exe","/select,\""+Updates.Package+"\"");return;}
   if(!PrepareExit())return;var app=(App)Application.Current;string manifest=null;
   Updates.Installing();try{manifest=await System.Threading.Tasks.Task.Run(()=>UpdateInstaller.Prepare(Updates,app.OwnerProcess));UpdateInstaller.Start(manifest);if(!app.Legacy.SendCommandToAhk("exit_for_update"))throw new IOException("无法通知主程序退出。");await System.Threading.Tasks.Task.Delay(65000);if(!app.Exiting)Updates.InstallFailed("主程序未退出，安装已取消，请重试。");}
   catch(Exception ex){if(manifest!=null)UpdateInstaller.Abort(manifest);Updates.InstallFailed(ex.Message);}
  }

  sealed class UpdateChoice {public UpdateStage Stage {get;set;}public string Label {get;set;}}
  static UpdateChoice[] UpdatePreviewChoices(){var list=new System.Collections.Generic.List<UpdateChoice>();foreach(UpdateStage state in Enum.GetValues(typeof(UpdateStage)))list.Add(new UpdateChoice{Stage=state,Label=UpdateTitle(state)});return list.ToArray();}
  Image updateHold;
  void HoldUpdateBackground(){if(!updatesActive||scene.ActualWidth<1||scene.ActualHeight<1)return;try{var snapshot=new System.Windows.Media.Imaging.RenderTargetBitmap((int)scene.ActualWidth,(int)scene.ActualHeight,96,96,PixelFormats.Pbgra32);snapshot.Render(backgroundLayer);snapshot.Freeze();if(updateHold!=null)scene.Children.Remove(updateHold);updateHold=new Image{Source=snapshot,Stretch=Stretch.Fill,IsHitTestVisible=false};scene.Children.Insert(1,updateHold);}catch{}}
  void ReleaseUpdateBackground(){if(updateHold==null)return;var held=updateHold;updateHold=null;var fade=new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(180));fade.Completed+=delegate{scene.Children.Remove(held);};held.BeginAnimation(OpacityProperty,fade);}
  void ShowUpdatePreview(){if(!adminUnlocked)return;if(Updates.PreviewBlocked){MessageBox.Show(this,"真实更新任务正在进行，或文件已准备安装。请先完成或取消任务。");return;}if(!updatesActive)Navigate("更新");if(!updatesActive)return;
   var dialog=new Window{Owner=this,Title="更新状态预览（不执行真实更新）",Width=355,Height=340,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#191D26")};var stack=new StackPanel{Margin=new Thickness(22)};dialog.Content=stack;stack.Children.Add(Text("模拟状态",19));var states=new ComboBox{ItemsSource=UpdatePreviewChoices(),DisplayMemberPath="Label",SelectedValuePath="Stage",Margin=new Thickness(0,0,0,12)};stack.Children.Add(states);
   stack.Children.Add(Text("下载进度：0% / 50% / 100%",12));var slider=new Slider{Minimum=0,Maximum=100,TickFrequency=50,IsSnapToTickEnabled=true,Margin=new Thickness(0,0,0,16)};stack.Children.Add(slider);
   Action render=()=>{if(!updatesActive||!adminUnlocked||Updates.PreviewBlocked)return;var selected=states.SelectedValue;if(selected==null)return;previewUpdate=new UpdateSnapshot{Stage=(UpdateStage)selected,Current="v19.00-Amiya.v008",Latest="v19.00-Amiya.v009",Bytes=(long)slider.Value*1048576,Total=100*1048576,Detail="模拟预览 · "+UpdateTitle((UpdateStage)selected)+"\n可在管理员区选择该状态素材、调整构图。"};RenderUpdate();};states.SelectionChanged+=delegate{render();};slider.ValueChanged+=delegate{render();};states.SelectedValue=UpdateStage.Available;
   stack.Children.Add(Button("保留预览并编辑素材",()=>dialog.Close()));stack.Children.Add(Button("退出预览，恢复真实状态",()=>{previewUpdate=null;renderedUpdateStage=null;RenderUpdate();dialog.Close();}));stack.Children.Add(Text("关闭此工具保留模拟状态；离开更新页或锁定管理员后退出。",12,"#ADB5C1"));dialog.Show();
  }
 }
}
