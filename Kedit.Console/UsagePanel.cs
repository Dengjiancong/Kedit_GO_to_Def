using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Kedit.Console {
    internal sealed partial class DesignPreview {
        bool usageAnimatePending,usageAnimateRender;ScrollViewer usageScroll;
        Border usagePanel;StackPanel usageBody;TextBlock usageState;
        ComboBox usagePeriod,usageCategory,usageFunction,usageView,usageYear;DatePicker usageDate;
        DispatcherTimer usageTimer;bool usageLoading;UsageSnapshot usageSnapshot;Expander usageDetails;ScrollViewer usageBarScroll;string usageSignature;
        readonly UsageStore usageStore=new UsageStore(UsageDirectory());
        static string UsageDirectory(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--usage-test-dir");return Array.IndexOf(args,"--self-test-usage")>=0&&i>=0&&i+1<args.Length?System.IO.Path.GetFullPath(args[i+1]):UsageStore.DefaultDirectory;}
        sealed class UsageChoice {public string Id,Name,Category,Key;public override string ToString(){return Name;}}
        List<UsageChoice> UsageChoices(){
            var all=new List<UsageChoice>();
            foreach(var item in keditShortcuts)all.Add(new UsageChoice{Id=item.Id,Name=item.Title,Category="Kedit",Key=item.Default});
            foreach(var item in vsShortcuts)all.Add(new UsageChoice{Id=item.Id,Name="VS · "+item.Title,Category="Visual Studio",Key=item.Default});
            all.Add(new UsageChoice{Id="CtrlQ",Name="快速打开路径",Category="其他",Key="^q"});all.Add(new UsageChoice{Id="RunPy",Name="运行 Python 文件",Category="其他",Key="F8"});all.Add(new UsageChoice{Id="Pause",Name="暂停 / 恢复快捷键",Category="其他",Key="Pause"});return all;
        }
        void ShowUsage(){
            usageAnimatePending=true;home=false;title.Text="使用统计";title.FontSize=23;currentMediaPage="usage";LoadPageMedia("");
            news.Visibility=panel.Visibility=nav.Visibility=settingsToggle.Visibility=Visibility.Collapsed;
            if(legacyPanel!=null)legacyPanel.Visibility=Visibility.Collapsed;
            foreach(var item in tiles)item.Value.Tag=item.Key=="使用统计";
            if(usagePanel==null){
                var layout=new DockPanel();usagePanel=new Border{Margin=new Thickness(24,96,24,64),Padding=new Thickness(18),CornerRadius=new CornerRadius(18),Background=B("#F2191D26"),Child=layout};scene.Children.Add(usagePanel);
                var filters=new WrapPanel();DockPanel.SetDock(filters,Dock.Top);layout.Children.Add(filters);
                usagePeriod=UsageCombo(new[]{"日","周","月","年"});usageCategory=UsageCombo(new[]{"全部分类","Kedit","Visual Studio","其他"});usageView=UsageCombo(new[]{"使用趋势","常用时段"});
                usageDate=CreateUsageDate();
                usageFunction=new ComboBox{Width=170,Margin=new Thickness(4),ItemsSource=new[]{new UsageChoice{Id="",Name="全部功能"}}.Concat(UsageChoices()).ToList(),SelectedIndex=0};
                foreach(UIElement c in new UIElement[]{usagePeriod,usageDate,usageCategory,usageFunction,usageView})filters.Children.Add(c);
                filters.Children.Add(Button("刷新",()=>RefreshUsage(true)));filters.Children.Add(Button("导出",ExportUsage));filters.Children.Add(Button("清空",ClearUsage));
                usageState=Text("正在读取本地统计…",11,"#AAB6C8");DockPanel.SetDock(usageState,Dock.Top);layout.Children.Add(usageState);
                usageScroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};ConfigureScroll(usageScroll);usageBody=new StackPanel();usageScroll.Content=usageBody;layout.Children.Add(usageScroll);
                usagePeriod.SelectionChanged+=delegate{RenderUsage();};usageCategory.SelectionChanged+=delegate{RenderUsage();};usageFunction.SelectionChanged+=delegate{RenderUsage();};usageView.SelectionChanged+=delegate{RenderUsage();};usageDate.SelectedDateChanged+=delegate{RenderUsage();};
                usageTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};usageTimer.Tick+=delegate{if(IsVisible&&usagePanel.Visibility==Visibility.Visible)RefreshUsage();};usageTimer.Start();Closed+=delegate{usageTimer.Stop();};
            }
            usagePanel.Visibility=Visibility.Visible;RenderUsage();RefreshUsage();
        }
        static ComboBox UsageCombo(string[] items){return new ComboBox{ItemsSource=items,SelectedIndex=0,MinWidth=70,Margin=new Thickness(4),VerticalContentAlignment=VerticalAlignment.Center};}
        async void RefreshUsage(bool animate=false){
            if(animate)usageAnimatePending=true;
            if(usageLoading)return;usageLoading=true;
            try{var snapshot=await Task.Run(()=>usageStore.Read());if(snapshot.Generation!=usageStore.EnsureGeneration())return;string signature=snapshot.Generation+string.Join(";",snapshot.Rows.Select(r=>r.Hour.ToString("s")+r.Function+":"+r.OffsetMinutes+":"+r.Count+":"+r.Success).ToArray())+string.Join(";",snapshot.Warnings.ToArray());usageSnapshot=snapshot;if(signature!=usageSignature||usageAnimatePending){usageSignature=signature;RenderUsage();}}
            catch(Exception ex){usageState.Text="统计读取失败（未显示为零）："+ex.Message;}
            finally{usageLoading=false;}
        }
        string UsageName(string id){var item=UsageChoices().FirstOrDefault(c=>c.Id==id);return item==null?id:item.Name;}
        string UsageBinding(string id){var item=UsageChoices().FirstOrDefault(c=>c.Id==id);if(item==null)return "";var value=new StringBuilder(512);string file=settingsFile??System.IO.Path.Combine(root,"Kedit_Settings.ini");GetPrivateProfileString("Hotkeys",id,item.Key,value,512,file);return FriendlyKey(value.ToString());}
        IEnumerable<UsageRow> FilterUsage(){
            string category=(string)usageCategory.SelectedItem;var choice=usageFunction.SelectedItem as UsageChoice;
            var ids=new HashSet<string>(UsageChoices().Where(c=>category=="全部分类"||c.Category==category).Select(c=>c.Id));
            return usageSnapshot.Rows.Where(r=>(category=="全部分类"||ids.Contains(r.Function))&&(choice==null||choice.Id==""||choice.Id==r.Function));
        }
        void RenderUsage(){
            if(usageSnapshot==null||usageBody==null)return;
            usageAnimateRender=usageAnimatePending;usageAnimatePending=false;
            int period=Math.Max(0,usagePeriod.SelectedIndex);DateTime start=UsageStore.PeriodStart(usageDate.SelectedDate??DateTime.Today,period),end=UsageStore.PeriodEnd(start,period);
            var filtered=FilterUsage().ToList();var rows=filtered.Where(r=>r.Hour>=start&&r.Hour<end).ToList();usageBody.Children.Clear();
            usageState.Text="仅本地 · "+start.ToString("yyyy-MM-dd")+" — "+end.AddDays(-1).ToString("yyyy-MM-dd")+" · 周一为每周起点 · 记录时本地时间 · 每 3 秒刷新"+(usageSnapshot.Warnings.Count>0?"\n数据不完整："+string.Join("；",usageSnapshot.Warnings.ToArray()):"");
            var top=rows.GroupBy(r=>r.Function).OrderByDescending(g=>g.Sum(r=>r.Count)).FirstOrDefault();
            DrawUsageSummary(rows.Sum(r=>r.Count),rows.Where(r=>r.Count>0).Select(r=>r.Hour.Date).Distinct().Count(),top==null?"暂无记录":UsageName(top.Key));
            usageBody.Children.Add(Text("功能使用次数",15));DrawUsageBars(rows);DrawUsageDetails(rows);
            usageBody.Children.Add(Text((usageView.SelectedIndex==1?"常用时段 · 周期内相同小时合并累计":"使用趋势 · "+(period==0?"当天逐小时":period==3?"全年逐月":"周期内逐日"))+"（触发次数）",15));DrawUsageLine(rows,start,end,period);if(period==0)usageBody.Children.Add(Text("日视图中，两种视图均统计当天 24 小时；切换到周、月或年可查看趋势与常用时段的区别。",11,"#AAB6C8"));
            var annualHeader=new StackPanel{Orientation=Orientation.Horizontal};annualHeader.Children.Add(Text("年度活跃墙 · ",15));
            int selectedYear=usageYear==null?DateTime.Today.Year:(int)usageYear.SelectedItem;int first=Math.Min(usageSnapshot.Started.Year,DateTime.Today.Year);var years=Enumerable.Range(first,DateTime.Today.Year-first+1).ToArray();
            if(!years.Contains(selectedYear))selectedYear=DateTime.Today.Year;
            usageYear=new ComboBox{ItemsSource=years,SelectedItem=selectedYear,Width=85,Margin=new Thickness(4,0,8,8)};usageYear.SelectionChanged+=delegate{RenderUsage();};annualHeader.Children.Add(usageYear);annualHeader.Children.Add(Text("全年展示，沿用分类／功能筛选",11,"#AAB6C8"));usageBody.Children.Add(annualHeader);
            DrawUsageYear(filtered,selectedYear);
            usageBody.Children.Add(Text("深浅表示活跃度，不代表工作产出。成功次数仅适用于 FLOW 插入、Bin 编号及缩进整理；其他功能无法确认外部程序执行结果。",11,"#AAB6C8"));
        }
        static void FastUsageTooltip(DependencyObject element){ToolTipService.SetInitialShowDelay(element,120);ToolTipService.SetBetweenShowDelay(element,0);ToolTipService.SetShowDuration(element,15000);}
        void DrawUsageBars(List<UsageRow> rows){
            var totals=rows.GroupBy(r=>r.Function).Select(g=>new{Id=g.Key,Count=g.Sum(r=>r.Count),Success=g.Sum(r=>r.Success)}).OrderByDescending(x=>x.Count).ToList();
            if(totals.Count==0){usageBody.Children.Add(Text("此周期暂无记录；启用前的日期不补造历史。",12,"#AAB6C8"));return;}
            double width=Math.Max(870,totals.Count*88);var chart=new Canvas{Width=width,Height=215,Margin=new Thickness(0,0,0,14)};long max=Math.Max(1,totals.Max(x=>x.Count));
            for(int i=0;i<totals.Count;i++){var item=totals[i];double x=15+i*88,h=135.0*item.Count/max;string detail=UsageName(item.Id)+" · "+UsageBinding(item.Id)+"\n触发："+item.Count+"\n成功："+(UsageStore.SuccessSupported(item.Id)?item.Success.ToString():"不适用");
                var bar=new Rectangle{Width=44,Height=Math.Max(1,h),RadiusX=4,RadiusY=4,Fill=B("#57B7D3"),ToolTip=detail};FastUsageTooltip(bar);Canvas.SetLeft(bar,x+12);Canvas.SetTop(bar,155-h);chart.Children.Add(bar);AnimateUsageRise(bar,i*12);
                var count=Text(item.Count.ToString(),11);Canvas.SetLeft(count,x+12);Canvas.SetTop(count,137-h);chart.Children.Add(count);
                var label=Text(UsageName(item.Id),11);label.Width=80;label.MaxHeight=45;label.ToolTip=detail;FastUsageTooltip(label);Canvas.SetLeft(label,x);Canvas.SetTop(label,162);chart.Children.Add(label);
            }
            double offset=usageBarScroll==null?0:usageBarScroll.HorizontalOffset;usageBarScroll=new ScrollViewer{Content=chart,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};usageBody.Children.Add(usageBarScroll);usageBarScroll.ScrollToHorizontalOffset(offset);usageBarScroll.PreviewMouseWheel+=UsageBarWheel;
        }
        void DrawUsageDetails(List<UsageRow> rows){
            var list=new StackPanel();
            foreach(var group in rows.GroupBy(r=>r.Function).OrderByDescending(g=>g.Sum(r=>r.Count))){
                var row=new Grid{Margin=new Thickness(0,3,0,3)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(330)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(180)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(130)});row.ColumnDefinitions.Add(new ColumnDefinition());
                string[] cells={UsageName(group.Key),UsageBinding(group.Key),"触发 "+group.Sum(r=>r.Count),UsageStore.SuccessSupported(group.Key)?"成功 "+group.Sum(r=>r.Success):"成功：不适用"};
                for(int i=0;i<cells.Length;i++){var text=Text(cells[i],12);text.Margin=new Thickness(0,0,6,3);Grid.SetColumn(text,i);row.Children.Add(text);}list.Children.Add(row);
            }
            if(list.Children.Count>0){bool expanded=usageDetails!=null&&usageDetails.IsExpanded;usageDetails=new Expander{Header="查看功能明细 · 当前绑定 / 触发 / 成功",Foreground=B("#DCE5EF"),Content=list,IsExpanded=expanded,Margin=new Thickness(0,0,0,18)};StyleUsageDetails(usageDetails);usageBody.Children.Add(usageDetails);}
        }
        void UsageBarWheel(object sender,MouseWheelEventArgs e){
            if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)usageBarScroll.ScrollToHorizontalOffset(usageBarScroll.HorizontalOffset-e.Delta);
            else usageScroll.ScrollToVerticalOffset(usageScroll.VerticalOffset-e.Delta);
            e.Handled=true;
        }
        void AnimateUsageRise(FrameworkElement element,int delay=0){
            if(!usageAnimateRender)return;
            var scale=new ScaleTransform(1,0);element.RenderTransform=scale;element.RenderTransformOrigin=new Point(.5,1);
            bool started=false;ScrollChangedEventHandler onScroll=null;
            Action start=()=>{if(started||!element.IsLoaded)return;var bottom=element.TranslatePoint(new Point(0,element.ActualHeight),usageScroll).Y;if(bottom<=0||bottom-element.ActualHeight>=usageScroll.ActualHeight)return;started=true;usageScroll.ScrollChanged-=onScroll;scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(650)){BeginTime=TimeSpan.FromMilliseconds(Math.Min(delay,150)),EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});};
            onScroll=delegate{start();};element.Loaded+=delegate{usageScroll.ScrollChanged+=onScroll;start();};element.Unloaded+=delegate{usageScroll.ScrollChanged-=onScroll;};
        }
        void StyleUsageDetails(Expander details){
            details.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Expander'>
              <Border Background='#202B38' BorderBrush='#354657' BorderThickness='1' CornerRadius='10'><StackPanel>
              <ToggleButton IsChecked='{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}}' Foreground='#DCE5EF' Background='Transparent' Cursor='Hand' HorizontalContentAlignment='Stretch'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='head' Background='{TemplateBinding Background}' Padding='15,12' CornerRadius='9'><DockPanel><TextBlock x:Name='arrow' Text='›' FontSize='18' Foreground='#87BED5' DockPanel.Dock='Right'/><TextBlock Text='功能明细   ·   当前绑定 / 触发 / 成功' FontSize='12' Foreground='#DCE5EF' VerticalAlignment='Center'/></DockPanel></Border><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='arrow' Property='Text' Value='⌄'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='head' Property='Background' Value='#2C4052'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='head' Property='Background' Value='#2C4052'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template></ToggleButton>
              <Border x:Name='body' Visibility='Collapsed' Padding='16,12' BorderBrush='#354657' BorderThickness='0,1,0,0'><ContentPresenter/></Border>
              </StackPanel></Border><ControlTemplate.Triggers><Trigger Property='IsExpanded' Value='True'><Setter TargetName='body' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        }
        static Style UsageCalendarStyle(){
            return (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Calendar'>
            <Setter Property='Foreground' Value='#E8F1F8'/><Setter Property='Background' Value='#203342'/><Setter Property='BorderBrush' Value='#54748B'/>
            <Setter Property='FirstDayOfWeek' Value='Monday'/><Setter Property='CalendarDayButtonStyle'><Setter.Value><Style TargetType='CalendarDayButton'><Setter Property='Foreground' Value='#E8F1F8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CalendarDayButton'><Border x:Name='cell' Background='Transparent' CornerRadius='4' Margin='1' BorderBrush='#70BEDA' BorderThickness='0'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' Margin='5'/></Border><ControlTemplate.Triggers>
            <Trigger Property='IsInactive' Value='True'><Setter Property='Foreground' Value='#899DAE'/></Trigger><Trigger Property='IsToday' Value='True'><Setter TargetName='cell' Property='BorderThickness' Value='1'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='cell' Property='Background' Value='#3B566B'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='cell' Property='Background' Value='#73C8DF'/><Setter Property='Foreground' Value='#102531'/></Trigger><Trigger Property='IsBlackedOut' Value='True'><Setter Property='Opacity' Value='.35'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.35'/></Trigger>
            </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style></Setter.Value></Setter>
            <Setter Property='CalendarButtonStyle'><Setter.Value><Style TargetType='CalendarButton'><Setter Property='Foreground' Value='#E8F1F8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CalendarButton'><Border x:Name='cell' Background='Transparent' CornerRadius='4' Padding='8'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='cell' Property='Background' Value='#3B566B'/></Trigger><Trigger Property='HasSelectedDays' Value='True'><Setter TargetName='cell' Property='Background' Value='#3B566B'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style></Setter.Value></Setter>
            <Setter Property='CalendarItemStyle'><Setter.Value><Style TargetType='CalendarItem'><Style.Resources><DataTemplate x:Key='{x:Static CalendarItem.DayTitleTemplateResourceKey}'><TextBlock Text='{Binding}' Foreground='#AFC6D8' HorizontalAlignment='Center' Margin='0,6'/></DataTemplate></Style.Resources><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CalendarItem'>
            <Border Background='#203342' BorderBrush='#54748B' BorderThickness='1' CornerRadius='9' Padding='10'><Grid x:Name='PART_Root'><Grid.Resources>
            <Style TargetType='Button'><Setter Property='Foreground' Value='#E8F1F8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='b' Background='Transparent' CornerRadius='4' Padding='8'><ContentPresenter HorizontalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#3B566B'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>

            </Grid.Resources><Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='36'/><ColumnDefinition/><ColumnDefinition Width='36'/></Grid.ColumnDefinitions><Button x:Name='PART_PreviousButton' Content='‹'/><Button x:Name='PART_HeaderButton' Grid.Column='1'/><Button x:Name='PART_NextButton' Grid.Column='2' Content='›'/></Grid>
            <UniformGrid x:Name='weekdays' Grid.Row='1' Columns='7' Margin='0,4'><TextBlock Text='一' Foreground='#AFC6D8' HorizontalAlignment='Center'/><TextBlock Text='二' Foreground='#AFC6D8' HorizontalAlignment='Center'/><TextBlock Text='三' Foreground='#AFC6D8' HorizontalAlignment='Center'/><TextBlock Text='四' Foreground='#AFC6D8' HorizontalAlignment='Center'/><TextBlock Text='五' Foreground='#AFC6D8' HorizontalAlignment='Center'/><TextBlock Text='六' Foreground='#AFC6D8' HorizontalAlignment='Center'/><TextBlock Text='日' Foreground='#AFC6D8' HorizontalAlignment='Center'/></UniformGrid><Grid x:Name='PART_MonthView' Grid.Row='2'><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/></Grid.ColumnDefinitions><Grid.RowDefinitions><RowDefinition/><RowDefinition/><RowDefinition/><RowDefinition/><RowDefinition/><RowDefinition/><RowDefinition/></Grid.RowDefinitions></Grid>
            <Grid x:Name='PART_YearView' Grid.Row='2' Visibility='Collapsed'><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/></Grid.ColumnDefinitions><Grid.RowDefinitions><RowDefinition/><RowDefinition/><RowDefinition/></Grid.RowDefinitions></Grid>
            </Grid></Border><ControlTemplate.Triggers><DataTrigger Binding='{Binding DisplayMode, RelativeSource={RelativeSource AncestorType=Calendar}}' Value='Year'><Setter TargetName='weekdays' Property='Visibility' Value='Collapsed'/><Setter TargetName='PART_MonthView' Property='Visibility' Value='Collapsed'/><Setter TargetName='PART_YearView' Property='Visibility' Value='Visible'/></DataTrigger><DataTrigger Binding='{Binding DisplayMode, RelativeSource={RelativeSource AncestorType=Calendar}}' Value='Decade'><Setter TargetName='weekdays' Property='Visibility' Value='Collapsed'/><Setter TargetName='PART_MonthView' Property='Visibility' Value='Collapsed'/><Setter TargetName='PART_YearView' Property='Visibility' Value='Visible'/></DataTrigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter></Style></Setter.Value></Setter></Style>");
        }
        DatePicker CreateUsageDate(){
            var picker=new DatePicker{SelectedDate=DateTime.Today,Width=145,Height=34,Margin=new Thickness(4),SelectedDateFormat=DatePickerFormat.Short,Foreground=B("#E2EBF3"),Background=B("#1B2E3D"),BorderBrush=B("#415F74"),BorderThickness=new Thickness(1),VerticalAlignment=VerticalAlignment.Center,ToolTip="选择统计日期"};
            picker.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='DatePicker'>
              <Grid x:Name='PART_Root'><Border x:Name='edge' CornerRadius='7' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1'><Grid Margin='9,0,7,0'><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='22'/></Grid.ColumnDefinitions>
              <DatePickerTextBox x:Name='PART_TextBox' Foreground='#E2EBF3' Background='Transparent' BorderThickness='0' VerticalContentAlignment='Center' FontSize='12'><DatePickerTextBox.Template><ControlTemplate TargetType='DatePickerTextBox'><ScrollViewer x:Name='PART_ContentHost' Background='Transparent' Margin='0,7,0,0'/></ControlTemplate></DatePickerTextBox.Template></DatePickerTextBox>
              <Button x:Name='PART_Button' Grid.Column='1' Background='Transparent' BorderThickness='0' Cursor='Hand' Focusable='False'><Button.Template><ControlTemplate TargetType='Button'><Border Background='Transparent'><Path Width='13' Height='13' Stretch='Uniform' Stroke='#BCD3E2' StrokeThickness='1.2' Data='M 1,3 L 13,3 13,13 1,13 Z M 1,6 L 13,6 M 4,1 L 4,4 M 10,1 L 10,4'/></Border></ControlTemplate></Button.Template></Button>
              </Grid></Border><Popup x:Name='PART_Popup' Placement='Bottom' StaysOpen='False' AllowsTransparency='True'/></Grid>
              <ControlTemplate.Triggers><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='edge' Property='BorderBrush' Value='#79BCD9'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='edge' Property='BorderBrush' Value='#6B93AB'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            picker.CalendarStyle=UsageCalendarStyle();
            picker.DateValidationError+=delegate(object sender,DatePickerDateValidationErrorEventArgs e){e.ThrowException=false;picker.SelectedDate=DateTime.Today;};return picker;
        }
        void DrawUsageSummary(long count,int days,string favorite){
            var grid=new Grid{Margin=new Thickness(0,6,0,22)};for(int i=0;i<3;i++)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(i==2?1.5:1,GridUnitType.Star)});
            string[] labels={"快捷键触发","活跃天数","最常用功能"},values={count.ToString("N0"),days.ToString(),favorite},hints={"次 · 当前周期","天 · 当前周期","按触发次数统计"},colors={"#70CEE5","#8BDBB4","#B4ADEB"};
            for(int i=0;i<3;i++){var content=new StackPanel();content.Children.Add(Text(labels[i],12,"#A6B9C9"));var value=Text(values[i],i==2?20:28,colors[i]);value.FontWeight=FontWeights.SemiBold;value.TextWrapping=TextWrapping.NoWrap;value.TextTrimming=TextTrimming.CharacterEllipsis;value.ToolTip=values[i];FastUsageTooltip(value);value.Margin=new Thickness(0,0,0,5);content.Children.Add(value);var hint=Text(hints[i],10,"#8096AA");hint.Margin=new Thickness(0);content.Children.Add(hint);var card=new Border{Child=content,Padding=new Thickness(16,12,16,12),Margin=new Thickness(0,0,i==2?0:12,0),CornerRadius=new CornerRadius(12),Background=B("#202B38"),BorderBrush=B("#354657"),BorderThickness=new Thickness(1)};Grid.SetColumn(card,i);grid.Children.Add(card);}usageBody.Children.Add(grid);
        }
        void DrawUsageLine(List<UsageRow> rows,DateTime start,DateTime end,int period){
            bool clock=usageView.SelectedIndex==1;int n=clock||period==0?24:period==3?12:(end-start).Days;var values=new long[n];foreach(var row in rows){int i=clock||period==0?row.Hour.Hour:period==3?row.Hour.Month-1:(row.Hour.Date-start).Days;if(i>=0&&i<n)values[i]+=row.Count;}
            var chart=new Canvas{Width=900,Height=195,Margin=new Thickness(0,0,0,12)};long max=Math.Max(1,values.Max());var plot=new Canvas{Width=900,Height=150};chart.Children.Add(plot);var points=new List<Point>();
            for(int i=0;i<n;i++)points.Add(new Point(40+835.0*i/Math.Max(1,n-1),150-125.0*values[i]/max));
            // Shape-preserving cubic interpolation: no invented peaks or negative counts.
            var slope=new double[n];var delta=new double[n-1];for(int i=0;i<n-1;i++)delta[i]=(points[i+1].Y-points[i].Y)/(points[i+1].X-points[i].X);
            slope[0]=delta[0];slope[n-1]=delta[n-2];for(int i=1;i<n-1;i++)slope[i]=delta[i-1]*delta[i]<=0?0:2*delta[i-1]*delta[i]/(delta[i-1]+delta[i]);
            var figure=new PathFigure{StartPoint=points[0]};for(int i=0;i<n-1;i++){double dx=(points[i+1].X-points[i].X)/3;figure.Segments.Add(new BezierSegment(new Point(points[i].X+dx,points[i].Y+slope[i]*dx),new Point(points[i+1].X-dx,points[i+1].Y-slope[i+1]*dx),points[i+1],true));}
            var area=figure.Clone();area.Segments.Add(new LineSegment(new Point(points[n-1].X,150),true));area.Segments.Add(new LineSegment(new Point(points[0].X,150),true));area.IsClosed=true;
            plot.Children.Add(new Path{Data=new PathGeometry(new[]{area}),Fill=new LinearGradientBrush(Color.FromArgb(65,90,210,165),Color.FromArgb(5,90,210,165),90),IsHitTestVisible=false});
            plot.Children.Add(new Path{Data=new PathGeometry(new[]{figure}),Stroke=B("#81DBAE"),StrokeThickness=2,IsHitTestVisible=false});
            for(int i=0;i<n;i++){double x=points[i].X,y=points[i].Y;string label=clock||period==0?i+"时":period==3?(i+1)+"月":start.AddDays(i).ToString("MM/dd");var dot=new Border{Width=18,Height=18,Background=Brushes.Transparent,ToolTip=label+"："+values[i]+" 次",Child=new Ellipse{Width=7,Height=7,Fill=B("#A3ECC6"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false}};FastUsageTooltip(dot);Canvas.SetLeft(dot,x-9);Canvas.SetTop(dot,y-9);plot.Children.Add(dot);if(i%Math.Max(1,n/8)==0||i==n-1){var text=Text(label,10);Canvas.SetLeft(text,x-12);Canvas.SetTop(text,163);chart.Children.Add(text);}}
            AnimateUsageRise(plot);var maxLabel=Text(max.ToString(),10);Canvas.SetTop(maxLabel,16);chart.Children.Add(maxLabel);var zero=Text("0",10);Canvas.SetTop(zero,143);chart.Children.Add(zero);usageBody.Children.Add(chart);
        }
        void DrawUsageYear(List<UsageRow> rows,int year){
            var days=rows.GroupBy(r=>r.Hour.Date).ToDictionary(g=>g.Key,g=>g.ToList());var chart=new Canvas{Width=920,Height=148};DateTime first=new DateTime(year,1,1),last=first.AddYears(1);int offset=((int)first.DayOfWeek+6)%7;
            for(int d=0;d<(last-first).Days;d++){DateTime date=first.AddDays(d);List<UsageRow> day;days.TryGetValue(date,out day);long count=day==null?0:day.Sum(r=>r.Count);bool known=date>=usageSnapshot.Started.Date&&date<=DateTime.Today;string color=!known?"#10141B":count==0?"#303B47":count<10?"#285848":count<50?"#338661":count<100?"#43B77A":"#78E7A2";string detail=date.ToString("yyyy-MM-dd")+(!known?(date>DateTime.Today?" · 未来日期":" · 尚未记录"):(usageSnapshot.Warnings.Count>0?" · 数据可能不完整":"")+" · "+count+" 次");if(day!=null){var top=day.GroupBy(r=>r.Function).OrderByDescending(g=>g.Sum(r=>r.Count)).First();detail+="\n最常用："+UsageName(top.Key);}
                var cell=new Border{Width=13,Height=13,CornerRadius=new CornerRadius(2),Background=B(color),BorderBrush=B("#43505C"),BorderThickness=new Thickness(.4),ToolTip=detail};FastUsageTooltip(cell);int col=(offset+d)/7,row=(offset+d)%7;Canvas.SetLeft(cell,36+col*16);Canvas.SetTop(cell,24+row*16);chart.Children.Add(cell);if(date.Day==1){var month=Text(date.Month+"月",10);Canvas.SetLeft(month,36+col*16);chart.Children.Add(month);}}
            for(int i=0;i<7;i++){var label=Text(new[]{"一","二","三","四","五","六","日"}[i],9);Canvas.SetTop(label,24+i*16);chart.Children.Add(label);}usageBody.Children.Add(chart);usageBody.Children.Add(Text("未记录 / 未来：深底描边    已记录 0 次：灰色    绿色：1–9 / 10–49 / 50–99 / ≥100 次",11,"#AAB6C8"));
        }

        async void RunUsageChecks(){
            string directory=usageStore.DirectoryPath;
            try{
                System.IO.Directory.CreateDirectory(directory);string generation=DateTime.Today.Year+"0101000000_uitest";
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"generation.txt"),generation);
                var fixture=new StringBuilder("KEDIT_USAGE_V1|"+generation+"\n");
                int i=0;foreach(var choice in UsageChoices()){fixture.Append(DateTime.Today.ToString("yyyyMMdd")+"10|"+choice.Id+"|480|"+(24-i)+"|0\n");i++;}
                for(int day=0;day<180;day++)fixture.Append(new DateTime(DateTime.Today.Year,1,1).AddDays(day).ToString("yyyyMMdd")+"13|FindClipboard|480|"+(day%110+1)+"|0\n");
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,generation+"_ui.tsv"),fixture.ToString());
                Navigate("使用统计");usageTimer.Stop();for(int wait=0;usageLoading&&wait<100;wait++)await Task.Delay(30);
                if(usageSnapshot==null)throw new Exception("Statistics did not load");
                UpdateLayout();var testBar=(Rectangle)((Canvas)usageBarScroll.Content).Children[0];if(!(testBar.RenderTransform is ScaleTransform))throw new Exception("Rise transform missing");await Task.Delay(950);if(Math.Abs(((ScaleTransform)testBar.RenderTransform).ScaleY-1)>.01)throw new Exception("Rise animation did not finish");Capture(System.IO.Path.Combine(directory,"statistics.png"));
                usageBarScroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});UpdateLayout();if(usageScroll.VerticalOffset<=0)throw new Exception("Bar wheel did not reach outer scroll");
                usageDate.IsDropDownOpen=true;await Task.Delay(100);if(!usageDate.IsDropDownOpen)throw new Exception("Date calendar did not open");var popup=(System.Windows.Controls.Primitives.Popup)usageDate.Template.FindName("PART_Popup",usageDate);var popupVisual=(FrameworkElement)popup.Child;var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)popupVisual.ActualWidth,(int)popupVisual.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(popupVisual);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var output=System.IO.File.Create(System.IO.Path.Combine(directory,"calendar.png")))encoder.Save(output);usageDate.IsDropDownOpen=false;
                RefreshUsage(true);for(int wait=0;usageLoading&&wait<100;wait++)await Task.Delay(30);UpdateLayout();if(!usageAnimateRender)throw new Exception("Manual refresh did not animate");if(TileKey("使用统计")==TileKey("其他"))throw new Exception("Tile keys collide");usageDetails.IsExpanded=true;UpdateLayout();await Task.Delay(950);Capture(System.IO.Path.Combine(directory,"details.png"));
                usagePeriod.SelectedIndex=3;usageView.SelectedIndex=1;usageCategory.SelectedIndex=1;UpdateLayout();
                var scroll=(ScrollViewer)((DockPanel)usagePanel.Child).Children[2];scroll.ScrollToEnd();UpdateLayout();Capture(System.IO.Path.Combine(directory,"annual.png"));
                if(usageBody.Children.Count<7||scroll.ScrollableHeight<=0)throw new Exception("Charts or scrolling missing");
                foreach(int period in new[]{0,1,2,3}){usagePeriod.SelectedIndex=period;usageDate.SelectedDate=new DateTime(2024,2,29);RenderUsage();}
                var framingValue=new PageMedia.Framing{Zoom=2,X=1,Y=0};var geometry=BackgroundFraming.Layout(720,400,400,800,framingValue);if(geometry.Right<720||geometry.Bottom<400||geometry.X>0||geometry.Y>0)throw new Exception("Framing exposes blank area");
                string still=System.IO.Path.Combine(directory,"statistics.png");var cropWindow=new BackgroundFraming(this,still,scene.ActualWidth/scene.ActualHeight,framingValue);cropWindow.Show();cropWindow.UpdateLayout();var cropBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)cropWindow.ActualWidth,(int)cropWindow.ActualHeight,96,96,PixelFormats.Pbgra32);cropBitmap.Render(cropWindow);PreviewCrop.Save(cropBitmap,System.IO.Path.Combine(directory,"framing.png"));cropWindow.Close();
                LoadMedia(still);var originalStill=gif.Source;currentFraming=framingValue;LoadMedia(still);ApplyBackgroundFraming();if(!object.ReferenceEquals(originalStill,gif.Source))throw new Exception("Framing restarted identical media");
                Navigate("其他");UpdateLayout();if(settingsToggle.Visibility!=Visibility.Visible)throw new Exception("Other fold control missing");
                Navigate("更新");UpdateLayout();Capture(System.IO.Path.Combine(directory,"updates.png"));if(settingsToggle.Visibility!=Visibility.Visible||updateCard.Visibility!=Visibility.Visible)throw new Exception("Update controls missing");if(panel.Visibility!=Visibility.Collapsed)throw new Exception("Updates must start collapsed");settingsToggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));if(panel.Visibility!=Visibility.Visible)throw new Exception("Update unfolding failed");Navigate("首页");Navigate("更新");if(panel.Visibility!=Visibility.Collapsed)throw new Exception("Update re-entry must collapse");
                foreach(UpdateStage updateState in Enum.GetValues(typeof(UpdateStage))){previewUpdate=new UpdateSnapshot{Stage=updateState,Current="v19.00-Amiya.v008",Latest="v19.00-Amiya.v009",Bytes=50*1048576,Total=100*1048576,Detail="管理员预览 · 状态展示"};RenderUpdate();UpdateLayout();await Task.Delay(220);Capture(System.IO.Path.Combine(directory,"update-"+updateState+".png"));if(updateCard.Visibility!=Visibility.Visible)throw new Exception("State card hidden");}
                previewUpdate=new UpdateSnapshot{Stage=UpdateStage.Available};renderedUpdateStage=null;RenderUpdate();string beforeMedia=playingMediaIdentity;var beforeSource=video.Source;previewUpdate.Stage=UpdateStage.Downloading;RenderUpdate();previewUpdate.Bytes=50;RenderUpdate();if(playingMediaIdentity!=beforeMedia||video.Source!=beforeSource)throw new Exception("Progress restarted background");
                Navigate("首页");if(updateCard.Visibility!=Visibility.Collapsed)throw new Exception("Update card remained on home");if(usagePanel.Visibility!=Visibility.Collapsed)throw new Exception("Statistics overlay remained on home");
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"ui-result.txt"),"PASS: statistics charts, filters, leap date, annual wall, bar wheel forwarding, date popup, rise animation and home navigation");
            }catch(Exception ex){System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"ui-result.txt"),"FAIL: "+ex);}
            Application.Current.Shutdown();
        }
        void ExportUsage(){try{var dialog=new SaveFileDialog{Filter="完整统计 JSON|*.json|统计表 CSV|*.csv",FileName="Kedit-usage-"+DateTime.Today.ToString("yyyyMMdd")};if(dialog.ShowDialog(this)==true){UsageStore.Export(usageStore.Read(),dialog.FileName);MessageBox.Show(this,"已导出全部本地统计（不受页面筛选限制）。","使用统计");}}catch(Exception ex){MessageBox.Show(this,ex.Message,"导出失败");}}
        void ClearUsage(){if(MessageBox.Show(this,"清空全部本地统计？建议先导出。清空后从现在重新记录，历史日期将标记为未记录。此操作无法撤销。","清空本地统计",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;try{usageStore.Clear();usageSnapshot=null;usageSignature=null;usageBody.Children.Clear();usageState.Text="已清空，从现在重新记录。";RefreshUsage();}catch(Exception ex){MessageBox.Show(this,ex.Message,"清空失败");}}
    }
}
