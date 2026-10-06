using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kedit.Console {
    internal sealed class PetAutomationPanel : StackPanel {
        private readonly PetController pet;private readonly Style buttonStyle;
        private readonly ComboBox reminders=new ComboBox(),actions=new ComboBox(),policy=new ComboBox(),repeat=new ComboBox();
        private readonly ComboBox hour=TimeChoice(24),minute=TimeChoice(60),restoreHour=TimeChoice(24),restoreMinute=TimeChoice(60);
        private readonly TextBox name=Field(),text=Field(),catchup=Field("0"),snooze=Field("10");
        private readonly CheckBox exact=Check("准点播放动作，不等待打字停顿");
        private readonly CheckBox[] days=new CheckBox[7];
        private readonly WrapPanel week=new WrapPanel(),restoreRow=new WrapPanel();
        private readonly StackPanel wardrobeOptions=new StackPanel();
        private readonly TextBlock summary=Label(""),draft=Label(""),feedback=Label(""),status=Label(""),musicStatus=Label(""),musicPath=Label(""),musicValues=Label(""),musicBehavior=Label("");
        private readonly ProgressBar level=new ProgressBar{Minimum=0,Maximum=100,Height=9,Margin=new Thickness(0,6,0,6),Background=new SolidColorBrush(Color.FromRgb(27,44,60)),Foreground=new SolidColorBrush(Color.FromRgb(85,186,194))};
        private readonly Slider amount=new Slider{Minimum=0,Maximum=100,TickFrequency=5,IsSnapToTickEnabled=true},sensitivity=new Slider{Minimum=.5,Maximum=4,TickFrequency=.1,IsSnapToTickEnabled=true};
        private string editing;private bool loading,dirty;
        private readonly StackPanel[] pages={new StackPanel(),new StackPanel(),new StackPanel()};
        private readonly Button[] navigation=new Button[3];
        internal PetAutomationPanel(PetController pet,Style buttonStyle) {
            this.pet=pet;this.buttonStyle=buttonStyle;Margin=new Thickness(8);
            Children.Add(Label("揉揉头",18));Children.Add(Label("在头部按住中键，手掌出现后轻轻移动。兔兔会害羞、眯眼、晃晃脑袋；松开或移出头部结束。"));Children.Add(Button("揉揉头（3 秒）",()=>pet.CompanionCommand("rub")));
            Children.Add(Label("定时提醒 · 填好后点击“保存并启用”",18));
            Children.Add(new Border{Background=new SolidColorBrush(Color.FromRgb(24,48,63)),CornerRadius=new CornerRadius(8),Padding=new Thickness(12),Child=summary,Margin=new Thickness(0,5,0,10)});
            reminders.DisplayMemberPath="Display";reminders.SelectionChanged+=delegate{if(!loading)LoadReminder(reminders.SelectedItem as PetReminder);};Children.Add(reminders);
            var tools=new WrapPanel();tools.Children.Add(Button("新建下班提醒",()=>{loading=true;reminders.SelectedItem=null;loading=false;LoadReminder(null);}));tools.Children.Add(Button("保存并启用",()=>Save(true)));tools.Children.Add(Button("立即试播",Preview));tools.Children.Add(Button("删除所选提醒",()=>{if(editing!=null){pet.DeleteReminder(editing);RefreshList(null);feedback.Text="已删除。";}}));Children.Add(tools);
            Children.Add(draft);Children.Add(feedback);AddField("1. 提醒叫什么",name);
            Children.Add(Label("2. 什么时候提醒"));var timeRow=new WrapPanel();timeRow.Children.Add(hour);timeRow.Children.Add(Label("时"));timeRow.Children.Add(minute);timeRow.Children.Add(Label("分（24 小时制）"));Children.Add(timeRow);
            foreach(string s in new[]{"每天","工作日：周一至周五","每周五","自选星期"})repeat.Items.Add(s);Children.Add(repeat);
            string[] labels={"日","一","二","三","四","五","六"};for(int i=0;i<7;i++){days[i]=Check("周"+labels[i]);days[i].Margin=new Thickness(0,5,10,5);days[i].Checked+=Edited;days[i].Unchecked+=Edited;week.Children.Add(days[i]);}Children.Add(week);
            Children.Add(Label("3. 兔兔做什么"));AddChoice(actions,"换装：丧失戰衣","motion-cloth off","只说提示语，不播放动作","","庆祝一下","motion-celebrate","打碟一次","motion-music","拿热水壶","hand-pot");Children.Add(actions);AddField("兔兔说的话",text);
            wardrobeOptions.Children.Add(Label("换装后怎么办"));AddChoice(policy,"保留到第二天，再穿回来","morning","只展示约 6 秒，然后恢复原样","once","一直保留，直到我手动恢复","hold");wardrobeOptions.Children.Add(policy);
            restoreRow.Children.Add(Label("第二天"));restoreRow.Children.Add(restoreHour);restoreRow.Children.Add(Label("时"));restoreRow.Children.Add(restoreMinute);restoreRow.Children.Add(Label("分穿回来"));wardrobeOptions.Children.Add(restoreRow);Children.Add(wardrobeOptions);
            var advanced=new StackPanel{Margin=new Thickness(8)};advanced.Children.Add(exact);advanced.Children.Add(Label("默认先说提示语，停手至少 2 秒后播放动作，并留出至少 3 秒操作时间。"));advanced.Children.Add(Label("错过后，允许在几分钟内补提醒？0 = 不补（最多 60）"));advanced.Children.Add(catchup);advanced.Children.Add(Label("点击“稍后提醒”后等几分钟？（1～120）"));advanced.Children.Add(snooze);advanced.Children.Add(Label("电脑休眠／软件关闭时不唤醒；工作日只指周一至周五，不判断节假日。"));Children.Add(new Expander{Header="高级：打字时机、错过提醒、稍后间隔",Foreground=Brushes.White,Content=advanced,Margin=new Thickness(0,10,0,6)});
            var saveTools=new WrapPanel();saveTools.Children.Add(Button("保存并启用",()=>Save(true)));saveTools.Children.Add(Button("立即试播（不保存日程）",Preview));saveTools.Children.Add(Button("保存为停用",()=>Save(false)));Children.Add(saveTools);Children.Add(Label("“立即试播”只检查画面和提示语，约 6 秒后恢复，不会启用提醒或覆盖已有换装恢复计划。"));
            var response=new WrapPanel();response.Children.Add(Button("本次稍后提醒",()=>Respond(true)));response.Children.Add(Button("今天忽略",()=>Respond(false)));response.Children.Add(Button("立即恢复外观",()=>pet.RestoreWardrobe()));Children.Add(response);Children.Add(status);
            int musicIndex=Children.Count;
            Children.Add(Label("音乐陪伴",18));Children.Add(musicPath);var musicTools=new WrapPanel();musicTools.Children.Add(Button("选择播放器程序",ChoosePlayer));musicTools.Children.Add(Button("恢复自动打碟／重新检测",()=>pet.ResumeAutomaticMusic()));musicTools.Children.Add(Button("关闭自动打碟",()=>SetMusic("")));Children.Add(musicTools);
            Children.Add(Label("选择 Chrome 时会响应它所有标签页的声音，无法区分音乐与视频。声音条有变化但没有打碟时，请看下方原因；拿剑、拿壶等手动道具默认优先。"));Children.Add(level);Children.Add(musicStatus);Children.Add(musicBehavior);Children.Add(Label("连续发声约 3 秒后打碟，静音约 8 秒后退出；打字时让位，停手约 3 秒后恢复。"));
            Children.Add(musicValues);Children.Add(Label("随音乐起伏的幅度"));Children.Add(amount);Children.Add(Label("对音乐强弱的灵敏度"));Children.Add(sensitivity);
            var manual=new WrapPanel();manual.Children.Add(Button("手动保持打碟",()=>pet.CompanionCommand("select","motion-music",true)));manual.Children.Add(Button("退出手动保持",()=>pet.ResumeAutomaticMusic()));Children.Add(manual);
            loading=true;amount.Value=pet.Automation.MusicAmount;sensitivity.Value=pet.Automation.MusicSensitivity;loading=false;
            foreach(var box in new[]{name,text,catchup,snooze})box.TextChanged+=delegate{MarkDirty();};foreach(var box in new[]{hour,minute,restoreHour,restoreMinute,repeat,actions,policy})box.SelectionChanged+=delegate{UpdateVisibility();MarkDirty();};exact.Checked+=Edited;exact.Unchecked+=Edited;
            amount.ValueChanged+=delegate{if(!loading)SetMusic(pet.Automation.MusicPath);};sensitivity.ValueChanged+=delegate{if(!loading)SetMusic(pet.Automation.MusicPath);};
            var elements=Children.Cast<UIElement>().ToArray();Children.Clear();
            for(int i=0;i<elements.Length;i++)pages[i<3?0:i<musicIndex?1:2].Children.Add(elements[i]);
            var tabs=new WrapPanel();string[] titles={"揉头互动","定时提醒","音乐陪伴"};
            for(int i=0;i<3;i++){int index=i;navigation[i]=Button(titles[i],()=>SelectPage(index));tabs.Children.Add(navigation[i]);}
            Children.Add(tabs);foreach(var page in pages)Children.Add(page);SelectPage(1);
            pet.AutomationChanged+=Changed;Unloaded+=delegate{pet.AutomationChanged-=Changed;};Loaded+=delegate{pet.AutomationChanged-=Changed;pet.AutomationChanged+=Changed;};RefreshList(null);RefreshStatus();
        }
        private static TextBox Field(string value="") {return new TextBox{Text=value,Background=new SolidColorBrush(Color.FromRgb(24,39,53)),Foreground=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(65,85,103)),Padding=new Thickness(7),Margin=new Thickness(0,3,0,8)};}
        private static TextBlock Label(string value,double size=12) {return new TextBlock{Text=value,FontSize=size,Foreground=new SolidColorBrush(Color.FromRgb(175,198,216)),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,7,0,5)};}
        private static CheckBox Check(string value){return new CheckBox{Content=value,Foreground=Brushes.White,Margin=new Thickness(0,7,0,7)};}
        private static ComboBox TimeChoice(int count){var box=new ComboBox{Width=80,Margin=new Thickness(0,4,6,8)};for(int i=0;i<count;i++)box.Items.Add(i.ToString("00"));box.SelectedIndex=0;return box;}
        private Button Button(string text,Action action) {var b=new Button{Content=text,Margin=new Thickness(0,4,8,4),Style=buttonStyle};b.Click+=delegate{try{action();}catch(Exception e){feedback.Text=e.Message;feedback.BringIntoView();}};return b;}
        private void AddField(string title,TextBox box){Children.Add(Label(title));Children.Add(box);}
        private static void AddChoice(ComboBox box,params string[] values){box.SelectedValuePath="Tag";for(int i=0;i<values.Length;i+=2)box.Items.Add(new ComboBoxItem{Content=values[i],Tag=values[i+1]});box.SelectedIndex=0;}
        private void Edited(object sender,RoutedEventArgs e){MarkDirty();}
        private void MarkDirty(){if(loading)return;dirty=true;feedback.Text="";RefreshStatus();}
        private void UpdateVisibility(){week.Visibility=repeat.SelectedIndex==3?Visibility.Visible:Visibility.Collapsed;wardrobeOptions.Visibility=Convert.ToString(actions.SelectedValue)=="motion-cloth off"?Visibility.Visible:Visibility.Collapsed;restoreRow.Visibility=Convert.ToString(policy.SelectedValue)=="morning"?Visibility.Visible:Visibility.Collapsed;}
        private void RefreshList(string id){loading=true;reminders.ItemsSource=pet.Automation.Reminders.ToArray();reminders.SelectedItem=pet.Automation.Reminders.FirstOrDefault(r=>r.Id==id)??pet.Automation.Reminders.FirstOrDefault();loading=false;LoadReminder(reminders.SelectedItem as PetReminder);}
        private void LoadReminder(PetReminder selected) {
            loading=true;editing=selected==null?null:selected.Id;var r=selected??new PetReminder();name.Text=r.Name;text.Text=r.Text;catchup.Text=r.CatchUpMinutes.ToString();snooze.Text=r.SnoozeMinutes.ToString();exact.IsChecked=r.Exact;SetTime(hour,minute,r.Time);SetTime(restoreHour,restoreMinute,r.RestoreTime);
            repeat.SelectedIndex=r.Days==127?0:r.Days==62?1:r.Days==32?2:3;for(int i=0;i<7;i++)days[i].IsChecked=(r.Days&(1<<i))!=0;actions.SelectedValue=r.Action;policy.SelectedValue=r.Policy;loading=false;dirty=selected==null;feedback.Text="";UpdateVisibility();RefreshStatus();
        }
        private static void SetTime(ComboBox h,ComboBox m,string time){TimeSpan t;if(!TimeSpan.TryParse(time,out t))t=TimeSpan.Zero;h.SelectedIndex=Math.Max(0,Math.Min(23,t.Hours));m.SelectedIndex=Math.Max(0,Math.Min(59,t.Minutes));}
        private static string GetTime(ComboBox h,ComboBox m){return h.SelectedIndex.ToString("00")+":"+m.SelectedIndex.ToString("00");}
        private PetReminder ReadForm(bool enable) {
            int cm,sm;if(!int.TryParse(catchup.Text,out cm)||cm<0||cm>60||!int.TryParse(snooze.Text,out sm)||sm<1||sm>120)throw new ArgumentException("高级设置中，补提醒分钟数应为 0～60，稍后间隔应为 1～120。");
            var r=new PetReminder{Id=editing??Guid.NewGuid().ToString("N"),Name=name.Text.Trim(),Enabled=enable,Time=GetTime(hour,minute),Days=repeat.SelectedIndex==0?127:repeat.SelectedIndex==1?62:repeat.SelectedIndex==2?32:0,Action=Convert.ToString(actions.SelectedValue),Text=text.Text.Trim(),Policy=Convert.ToString(policy.SelectedValue),RestoreTime=GetTime(restoreHour,restoreMinute),Exact=exact.IsChecked==true,CatchUpMinutes=cm,SnoozeMinutes=sm};
            if(repeat.SelectedIndex==3)for(int i=0;i<7;i++)if(days[i].IsChecked==true)r.Days|=1<<i;PetSchedule.Validate(r);return r;
        }
        private void Save(bool enable){var r=ReadForm(enable);pet.SaveReminder(r);RefreshList(r.Id);feedback.Text=enable?"已保存并启用。请核对上方的下一次提醒时间。":"已保存为停用，不会自动触发。";ScrollToPage();}
        private void Preview(){pet.PreviewReminder(ReadForm(false));feedback.Text="正在试播，约 6 秒后恢复。日程仍需点击“保存并启用”。";}
        private void Respond(bool later){if(editing!=null)pet.RespondReminder(editing,later);RefreshStatus();}
        private void ChoosePlayer(){var d=new Microsoft.Win32.OpenFileDialog{Title="选择实际发出声音的播放器程序",Filter="程序 (*.exe)|*.exe",CheckFileExists=true};if(d.ShowDialog()==true)SetMusic(d.FileName);}
        private void SetMusic(string path){pet.MusicOptions(path,amount.Value,sensitivity.Value);RefreshStatus();}
        private void Changed(object sender,EventArgs e){RefreshStatus();}
        private void RefreshStatus(){
            musicPath.Text=string.IsNullOrEmpty(pet.Automation.MusicPath)?"未选择播放器 · 自动打碟关闭":"已选择："+pet.Automation.MusicPath;musicValues.Text="响应幅度 "+amount.Value.ToString("0")+"% · 灵敏度 "+sensitivity.Value.ToString("0.0")+" 倍";level.Value=100*Math.Sqrt(Math.Max(0,pet.MusicPeak));musicStatus.Text=pet.MusicStatus;musicBehavior.Text=pet.MusicBehavior;
            var r=pet.Automation.Reminders.FirstOrDefault(x=>x.Id==editing);DateTime? next=PetSchedule.Next(pet.Automation,r,DateTime.Now);
            summary.Text=r==null?"尚未设置提醒。填好下面三步，再点击“保存并启用”。":!r.Enabled?"这条提醒已停用，点击“保存并启用”后才会触发。":next.HasValue?"已启用：“"+r.Name+"”\n下一次："+next.Value.ToString("MM 月 dd 日 dddd HH:mm")+(pet.IsReady?"":"\n桌宠尚未就绪：请先开启桌宠。") : "这条提醒已启用，但没有可执行的下一次时间，请检查星期设置。";
            draft.Text=dirty?"● 有未保存的设置，填写时间不会自动启用提醒。":"设置已保存。";if(dirty&&hour.SelectedIndex>=0&&minute.SelectedIndex>=0&&DateTime.Today.Add(new TimeSpan(hour.SelectedIndex,minute.SelectedIndex,0))<DateTime.Now)draft.Text+=" 今天这个时间已过；保存后以下一次有效日期为准。";
            var o=pet.LatestOccurrence(editing);var w=pet.Automation.Wardrobe;status.Text=(o==null?"今天尚无执行记录。":"今天的记录："+State(o.Status)+(o.Status=="snoozed"?" · "+o.Next.ToString("HH:mm"):""))+(w==null?"":w.RestoreAt==default(DateTime)?" · 当前外观无自动恢复期限":" · 外观恢复："+w.RestoreAt.ToString("MM-dd HH:mm"));
        }
        private static string State(string s){switch(s){case "pending":return "已到点，等待停手或开始动作";case "snoozed":return "已延期";case "ignored":return "今天已忽略";case "executed":return "已触发";case "cancelled":return "修改设置后已取消";case "missed":return "当时桌宠未就绪或已错过时间，未补播";case "expired":return "等待超过有效期，已结束";default:return s;}}
        private void SelectPage(int selected){for(int i=0;i<3;i++){pages[i].Visibility=i==selected?Visibility.Visible:Visibility.Collapsed;if(navigation[i]!=null)navigation[i].Foreground=i==selected?Brushes.Gold:Brushes.White;}ScrollToPage();}
        private void ScrollToPage(){if(!IsLoaded)return;Dispatcher.BeginInvoke(new Action(delegate{UpdateLayout();DependencyObject parent=this;while(parent!=null&&!(parent is ScrollViewer))parent=VisualTreeHelper.GetParent(parent);var viewer=parent as ScrollViewer;if(viewer!=null&&viewer.Content is Visual)viewer.ScrollToVerticalOffset(TransformToAncestor((Visual)viewer.Content).Transform(new Point()).Y);}),System.Windows.Threading.DispatcherPriority.Loaded);}
        internal void ShowForDiagnostics(bool music){SelectPage(music?2:1);if(music)musicBehavior.BringIntoView();else summary.BringIntoView();}
        internal string ScheduleForDiagnostics(DateTime due){LoadReminder(null);name.Text="定时功能实测";repeat.SelectedIndex=0;SetTime(hour,minute,due.ToString("HH:mm"));exact.IsChecked=true;Save(true);return editing;}
        internal string SummaryForDiagnostics {get{return summary.Text;}}
        internal void PreviewForDiagnostics(){Preview();}
    }
}
