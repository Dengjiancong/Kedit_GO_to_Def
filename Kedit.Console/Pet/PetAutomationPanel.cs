using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kedit.Console {
    internal sealed class PetAutomationPanel : StackPanel {
        private readonly PetController pet;
        private readonly Style buttonStyle;
        private readonly ComboBox reminders=new ComboBox(),actions=new ComboBox(),policy=new ComboBox(),repeat=new ComboBox();
        private readonly TextBox name=Field(),time=Field("18:00"),text=Field("下班啦！下班啦！辛苦啦！"),restore=Field("07:00"),catchup=Field("0"),snooze=Field("10");
        private readonly CheckBox enabled=Check("启用这条提醒"),exact=Check("准点播放动作（否则等待停手；至少保留 3 秒操作时间）");
        private readonly CheckBox[] days=new CheckBox[7];
        private readonly TextBlock status=Label(""),musicStatus=Label(""),musicPath=Label(""),musicValues=Label("");
        private readonly Slider amount=new Slider{Minimum=0,Maximum=100,TickFrequency=5,IsSnapToTickEnabled=true},sensitivity=new Slider{Minimum=.5,Maximum=4,TickFrequency=.1,IsSnapToTickEnabled=true};
        private string editing;
        internal PetAutomationPanel(PetController pet,Style buttonStyle) {
            this.pet=pet;this.buttonStyle=buttonStyle;Margin=new Thickness(8);
            Children.Add(Label("连续揉头：在头部按住中键，移动超过 6 像素并持续约 0.18 秒即可；松开或移出头部自然结束。"));
            Children.Add(Button("揉揉头（3 秒）",()=>pet.CompanionCommand("rub")));
            Children.Add(Label("定时动作提醒",18));
            reminders.DisplayMemberPath="Display";reminders.SelectionChanged+=delegate{LoadReminder(reminders.SelectedItem as PetReminder);};Children.Add(reminders);
            var tools=new WrapPanel();tools.Children.Add(Button("新增提醒",()=>{var r=new PetReminder();pet.SaveReminder(r);RefreshList(r.Id);}));
            tools.Children.Add(Button("删除所选",()=>{if(editing!=null){pet.DeleteReminder(editing);RefreshList(null);}}));Children.Add(tools);
            Children.Add(enabled);AddField("名称",name);AddField("提醒时间（HH:mm）",time);
            foreach(string s in new[]{"每天","工作日（周一至周五）","每周五","自选星期"})repeat.Items.Add(s);
            repeat.SelectedIndex=1;Children.Add(repeat);
            var week=new WrapPanel();string[] labels={"日","一","二","三","四","五","六"};
            for(int i=0;i<7;i++){days[i]=Check("周"+labels[i]);days[i].Margin=new Thickness(0,5,10,5);week.Children.Add(days[i]);}
            Children.Add(week);
            repeat.SelectionChanged+=delegate{int mask=repeat.SelectedIndex==0?127:repeat.SelectedIndex==1?62:repeat.SelectedIndex==2?32:-1;if(mask>=0)for(int i=0;i<7;i++)days[i].IsChecked=(mask&(1<<i))!=0;};
            AddChoice(actions,"仅显示提示","", "丧失戰衣（换装动画）","motion-cloth off","庆祝","motion-celebrate","打碟一次","motion-music","拿热水壶","hand-pot");
            Children.Add(Label("动作（需当前模型具备对应资源）"));Children.Add(actions);AddField("提示语",text);
            AddChoice(policy,"保持到次日指定时间","morning","仅播放一次，约 6 秒后恢复","once","长期保持，直到手动恢复","hold");
            Children.Add(Label("换装结束策略（其他动作只播放一次）"));Children.Add(policy);AddField("次日恢复时间（HH:mm）",restore);Children.Add(exact);
            AddField("错过后补提醒窗口（分钟，0 为不补，最多 60）",catchup);AddField("稍后提醒间隔（分钟，1～120）",snooze);
            Children.Add(Label("按本机时间执行，不唤醒电脑；工作日不包含节假日／调休判断。修改、禁用或删除会取消待播动作，已生效换装仍按原期限恢复。"));
            var controls=new WrapPanel();controls.Children.Add(Button("保存提醒",Save));controls.Children.Add(Button("稍后提醒",()=>Respond(true)));controls.Children.Add(Button("今天忽略",()=>Respond(false)));controls.Children.Add(Button("立即恢复外观",()=>pet.RestoreWardrobe()));Children.Add(controls);Children.Add(status);
            Children.Add(Label("音乐陪伴",18));Children.Add(musicPath);
            var musicTools=new WrapPanel();musicTools.Children.Add(Button("选择播放器程序",ChoosePlayer));musicTools.Children.Add(Button("清空选择／关闭",()=>SetMusic("")));musicTools.Children.Add(Button("手动保持打碟",()=>pet.CompanionCommand("select","motion-music",true)));musicTools.Children.Add(Button("退出手动保持",()=>pet.CompanionCommand("reset")));Children.Add(musicTools);
            Children.Add(Label("选择发出声音的播放器 .exe；所有输出设备上该程序的独立音频会话均可响应。浏览器内音乐、视频和不同标签页无法区分；无法识别的来源不会回退到系统混音。"));
            Children.Add(Label("持续发声约 3 秒进入，短暂静音保留场景，静音约 8 秒退出；打字优先，停手约 3 秒后恢复。仅分析音量起伏，不保证精确踩点。"));
            Children.Add(musicValues);Children.Add(Label("响应幅度"));Children.Add(amount);Children.Add(Label("响应灵敏度"));Children.Add(sensitivity);Children.Add(musicStatus);
            amount.Value=pet.Automation.MusicAmount;sensitivity.Value=pet.Automation.MusicSensitivity;
            amount.ValueChanged+=delegate{SetMusic(pet.Automation.MusicPath);};sensitivity.ValueChanged+=delegate{SetMusic(pet.Automation.MusicPath);};
            pet.AutomationChanged+=Changed;Unloaded+=delegate{pet.AutomationChanged-=Changed;};Loaded+=delegate{pet.AutomationChanged-=Changed;pet.AutomationChanged+=Changed;};
            RefreshList(null);RefreshStatus();
        }
        private static TextBox Field(string value="") {return new TextBox{Text=value,Background=new SolidColorBrush(Color.FromRgb(24,39,53)),Foreground=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(65,85,103)),Padding=new Thickness(7),Margin=new Thickness(0,3,0,8)};}
        private static TextBlock Label(string value,double size=12) {return new TextBlock{Text=value,FontSize=size,Foreground=new SolidColorBrush(Color.FromRgb(175,198,216)),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,7,0,5)};}
        private static CheckBox Check(string value){return new CheckBox{Content=value,Foreground=Brushes.White,Margin=new Thickness(0,7,0,7)};}
        private Button Button(string text,Action action) {var b=new Button{Content=text,Margin=new Thickness(0,4,8,4),Style=buttonStyle};b.Click+=delegate{try{action();}catch(Exception e){status.Text=e.Message;}};return b;}
        private void AddField(string title,TextBox box){Children.Add(Label(title));Children.Add(box);}
        private static void AddChoice(ComboBox box,params string[] values){box.SelectedValuePath="Tag";for(int i=0;i<values.Length;i+=2)box.Items.Add(new ComboBoxItem{Content=values[i],Tag=values[i+1]});box.SelectedIndex=0;}
        private void RefreshList(string id){reminders.ItemsSource=pet.Automation.Reminders.ToArray();reminders.SelectedItem=pet.Automation.Reminders.FirstOrDefault(r=>r.Id==id)??pet.Automation.Reminders.FirstOrDefault();if(reminders.SelectedItem==null)LoadReminder(null);}
        private void LoadReminder(PetReminder r) {
            editing=r==null?null:r.Id;if(r==null){status.Text="点击新增提醒开始设置；新提醒默认关闭。";return;}
            name.Text=r.Name;time.Text=r.Time;text.Text=r.Text;restore.Text=r.RestoreTime;catchup.Text=r.CatchUpMinutes.ToString();snooze.Text=r.SnoozeMinutes.ToString();enabled.IsChecked=r.Enabled;exact.IsChecked=r.Exact;
            repeat.SelectedIndex=r.Days==127?0:r.Days==62?1:r.Days==32?2:3;for(int i=0;i<7;i++)days[i].IsChecked=(r.Days&(1<<i))!=0;
            actions.SelectedValue=r.Action;policy.SelectedValue=r.Policy;RefreshStatus();
        }
        private void Save() {
            int cm,sm;if(!int.TryParse(catchup.Text,out cm)||cm<0||cm>60||!int.TryParse(snooze.Text,out sm)||sm<1||sm>120)throw new ArgumentException("请填写有效的补提醒和稍后提醒分钟数。");
            var r=new PetReminder{Id=editing??Guid.NewGuid().ToString("N"),Name=name.Text,Enabled=enabled.IsChecked==true,Time=time.Text,Days=0,Action=Convert.ToString(actions.SelectedValue),Text=text.Text,Policy=Convert.ToString(policy.SelectedValue),RestoreTime=restore.Text,Exact=exact.IsChecked==true,CatchUpMinutes=cm,SnoozeMinutes=sm};
            for(int i=0;i<7;i++)if(days[i].IsChecked==true)r.Days|=1<<i;
            pet.SaveReminder(r);RefreshList(r.Id);status.Text="已保存。";
        }
        private void Respond(bool later){if(editing!=null)pet.RespondReminder(editing,later);RefreshStatus();}
        private void ChoosePlayer(){var d=new Microsoft.Win32.OpenFileDialog{Title="选择实际发出声音的播放器程序",Filter="程序 (*.exe)|*.exe",CheckFileExists=true};if(d.ShowDialog()==true)SetMusic(d.FileName);}
        private void SetMusic(string path){pet.MusicOptions(path,amount.Value,sensitivity.Value);RefreshStatus();}
        private void Changed(object sender,EventArgs e){RefreshStatus();}
        private void RefreshStatus(){
            musicPath.Text=string.IsNullOrEmpty(pet.Automation.MusicPath)?"播放器：未选择（关闭）":"播放器："+pet.Automation.MusicPath;
            musicValues.Text="响应幅度 "+amount.Value.ToString("0")+"% · 灵敏度 "+sensitivity.Value.ToString("0.0")+" 倍";musicStatus.Text=pet.MusicStatus;
            var o=pet.LatestOccurrence(editing);var w=pet.Automation.Wardrobe;
            status.Text=(o==null?"今天暂无可操作的提醒。":"本次提醒："+State(o.Status)+(o.Status=="snoozed"?" · "+o.Next.ToString("HH:mm"):""))+
                (w==null?"":w.RestoreAt==default(DateTime)?" · 外观已保留，无定时恢复":" · 外观恢复："+w.RestoreAt.ToString("MM-dd HH:mm"));
        }
        private static string State(string s){switch(s){case "pending":return "等待播放";case "snoozed":return "已延期";case "ignored":return "今天已忽略";case "executed":return "已执行";case "cancelled":return "已取消";default:return s;}}
        internal void ShowForDiagnostics(bool music){if(music)musicStatus.BringIntoView();else{RefreshList(null);time.BringIntoView();}}
    }
}
