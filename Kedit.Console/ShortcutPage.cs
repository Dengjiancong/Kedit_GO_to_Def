using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace Kedit.Console {
    internal sealed partial class DesignPreview {
        sealed class KeditShortcut {
            public string Id,Title,Default,Description,Media,Icon;
            public KeditShortcut(string id,string title,string key,string description,string media,string icon){Id=id;Title=title;Default=key;Description=description;Media=media;Icon=icon;}
        }
        static readonly KeditShortcut[] keditShortcuts={
            new KeditShortcut("FindClipboard","查找剪贴板内容","F1","依次执行 Ctrl+F、Ctrl+V、Enter，查找剪贴板内容。","find-clipboard","⌕"),
            new KeditShortcut("GoToDef","侧后键 / Ctrl+B","XButton1","发送 Ctrl+B。","go-to-def","↗"),
            new KeditShortcut("ShiftF2","侧前键 / Shift+F2","XButton2","发送 Shift+F2。","shift-f2","↶"),
            new KeditShortcut("AltF","文件中查找","!f","依次发送 Alt+T、N，打开 Find in files。","find-in-files","▤"),
            new KeditShortcut("CtrlW","关闭窗口","^w","执行原有关闭窗口操作。","close-window","×"),
            new KeditShortcut("AltA","另存为","!a","依次发送 Alt+F、A，打开另存为。","save-as","▣"),
            new KeditShortcut("ColumnInsert","列填入数据","!i","对列选择区域批量填入数据。","column-insert","▥"),
            new KeditShortcut("ToggleComment","注释 / 取消注释","^/","智能切换所选代码的注释状态。","toggle-comment","/"),
            new KeditShortcut("SpacesToTabs","行首空格转 Tab","^\\","按 4 列制表位整理所选文本的行首缩进，保持正文视觉位置。","spaces-to-tabs","⇥"),
            new KeditShortcut("SmartClick","智能点击 / 跳转定义","~MButton","执行原有智能点击功能。鼠标键建议保留 ~ 前缀，例如 ~MButton，以保留原按键功能。","smart-click","◎")
        };
        int selectedShortcut;
        KeditShortcut CurrentShortcut {get{return keditShortcuts[selectedShortcut];}}
        void SelectShortcut(int index){if(!CanLeave())return;selectedShortcut=index;NavigateLive("Kedit");}
        readonly bool liveMode;
        IntPtr ahkWindow;
        string settingsFile;
        Border legacyPanel;
        bool recording;
        [StructLayout(LayoutKind.Sequential)] struct CopyData { public IntPtr Id; public int Bytes; public IntPtr Text; }
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,ref CopyData l,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern uint GetPrivateProfileString(string section,string key,string fallback,StringBuilder value,uint size,string file);
        string ReadShortcut(){var value=new StringBuilder(512);GetPrivateProfileString("Hotkeys",CurrentShortcut.Id,CurrentShortcut.Default,value,512,settingsFile);return value.ToString();}
        internal void OpenRoute(bool find,IntPtr source){OpenRoute(find?1:0,source);}
        internal void OpenRoute(int route,IntPtr source){
            if(source!=IntPtr.Zero)ahkWindow=source;
            if(settingsFile==null){var args=Environment.GetCommandLineArgs();long handle;if(args.Length>2&&long.TryParse(args[2],out handle))ahkWindow=new IntPtr(handle);for(int i=1;i+1<args.Length;i++)if(args[i]=="--settings")settingsFile=Path.GetFullPath(args[i+1]);if(settingsFile==null)settingsFile=Path.Combine(root,"Kedit_Settings.ini");}
            if(route>0 && route<=keditShortcuts.Length)SelectShortcut(route-1);else Navigate("首页");
        }
        void NavigateLive(string page){
            if(settingsFile==null){settingsFile=Path.Combine(root,"Kedit_Settings.ini");var args=Environment.GetCommandLineArgs();long handle;if(args.Length>2&&long.TryParse(args[2],out handle))ahkWindow=new IntPtr(handle);for(int i=1;i+1<args.Length;i++)if(args[i]=="--settings")settingsFile=Path.GetFullPath(args[i+1]);}
            recording=false;home=page=="首页";folded=false;dirty=false;
            foreach(var pair in tiles)pair.Value.Tag=pair.Key==page;
            if(legacyPanel!=null)legacyPanel.Visibility=Visibility.Collapsed;
            news.Visibility=home?Visibility.Visible:Visibility.Collapsed;settingsToggle.Visibility=page=="Kedit"?Visibility.Visible:Visibility.Collapsed;
            panel.Visibility=page=="Kedit"?Visibility.Visible:Visibility.Collapsed;nav.Visibility=page=="Kedit"?Visibility.Visible:Visibility.Collapsed;
            form.Children.Clear();formHeader.Children.Clear();formFooter.Children.Clear();subnav.Children.Clear();
            title.FontSize=home?34:23;title.Text=home?"KEDIT\n让操作，随心而动。":page.Replace("\n"," ");
            if(home){LoadMedia(Path.Combine(root,"side.mp4"));return;}
            if(page!="Kedit"){
                LoadMedia("");var app=(App)Application.Current;
                if(legacyPanel==null){var content=app.Legacy.DetachPages();legacyPanel=new Border{Child=content,Margin=new Thickness(24,90,24,56),Background=B("#EE151D29")};scene.Children.Add(legacyPanel);}
                app.Legacy.ShowCategory(page=="桌宠OSD"?"pet":page.StartsWith("Visual")?"vs":page=="更新"?"system":"other");legacyPanel.Visibility=Visibility.Visible;return;
            }
            nav.Width=navFolded?45:160;panel.Margin=new Thickness(0,110,navFolded?106:176,72);
            savedKey=ReadShortcut();key.TextChanged-=Changed;key.Text=savedKey;key.TextChanged+=Changed;
            title.Text=CurrentShortcut.Title+" · "+savedKey;
            formHeader.Children.Add(Text(CurrentShortcut.Title,23));formHeader.Children.Add(Text("当前："+savedKey+"　默认："+CurrentShortcut.Default,13));
            form.Children.Add(Text("仅在 Kedit 中生效。"+CurrentShortcut.Description,13));
            form.Children.Add(Text("快捷键（支持直接输入 AHK 代码）",12));key.FontSize=14;key.Padding=new Thickness(8);form.Children.Add(key);
            key.PreviewKeyDown-=RecordShortcut;key.PreviewKeyDown+=RecordShortcut;
            form.Children.Add(Button("录入键盘快捷键",()=>{recording=true;key.Focus();status.Text="请按组合键；Esc 取消录入。鼠标键请使用下方代码。";}));
            form.Children.Add(Text("Ctrl：^　Alt：!　Shift：+　Win：#\n鼠标：MButton / XButton1 / XButton2\n示例：^F1、!f、~MButton",12,"#ADB5C6"));
            form.Children.Add(Button("恢复默认 "+CurrentShortcut.Default+"（保存后生效）",()=>key.Text=CurrentShortcut.Default));
            formFooter.Children.Add(Button("保存",Save,true));formFooter.Children.Add(Button("取消修改",()=>{key.Text=savedKey;dirty=false;status.Text="已取消修改";}));
            status.Foreground=B("#C5C9D0");status.TextWrapping=TextWrapping.Wrap;status.Text=IsWindow(ahkWindow)?"修改后点击保存，由快捷键主程序应用。":"主程序未连接，暂时无法保存。";formFooter.Children.Add(status);
            subnav.Children.Add(Button("☰",()=>{navFolded=!navFolded;nav.Width=navFolded?45:160;panel.Margin=new Thickness(0,110,navFolded?106:176,72);foreach(var child in subnav.Children){var b=child as Button;if(b!=null&&b.Tag!=null)b.Content=navFolded?b.Tag:b.ToolTip;}}));
            for(int i=0;i<keditShortcuts.Length;i++){int index=i;var item=keditShortcuts[i];var entry=Button(item.Icon,()=>SelectShortcut(index));entry.Tag=item.Icon;entry.ToolTip=item.Title;entry.Content=navFolded?entry.Tag:entry.ToolTip;entry.FontSize=12;entry.Height=32;entry.Margin=new Thickness(0,2,0,2);if(i==selectedShortcut)entry.Background=B("#526175");subnav.Children.Add(entry);}
            string media=Path.Combine(root,"Kedit.Console","Demos",CurrentShortcut.Media+".mp4");if(!File.Exists(media))media=Path.ChangeExtension(media,"gif");LoadMedia(media);if(!File.Exists(media))form.Children.Add(Text("演示待补充",13,"#BBC4D2"));dirty=false;
        }
        void RecordShortcut(object sender,KeyEventArgs e){
            if(!recording)return;e.Handled=true;var k=e.Key==Key.System?e.SystemKey:e.Key;
            if(k==Key.Escape){recording=false;status.Text="已取消录入";return;}
            if(k==Key.LeftCtrl||k==Key.RightCtrl||k==Key.LeftAlt||k==Key.RightAlt||k==Key.LeftShift||k==Key.RightShift||k==Key.LWin||k==Key.RWin)return;
            var modifiers=Keyboard.Modifiers;string name=k.ToString();if(name.Length==2&&name[0]=='D'&&char.IsDigit(name[1]))name=name.Substring(1);if(name=="Return")name="Enter";if(name=="Back")name="Backspace";if(name.StartsWith("Oem")||name.StartsWith("NumPad")||name=="Add"||name=="Subtract"||name=="Multiply"||name=="Divide"||name=="Decimal")name="vk"+KeyInterop.VirtualKeyFromKey(k).ToString("X2");
            key.Text=((modifiers&ModifierKeys.Control)!=0?"^":"")+((modifiers&ModifierKeys.Alt)!=0?"!":"")+((modifiers&ModifierKeys.Shift)!=0?"+":"")+((modifiers&ModifierKeys.Windows)!=0?"#":"")+name;recording=false;status.Text="已录入，点击保存后生效。";
        }
        bool ConfirmLiveLeave(){
            bool proceed=false;var dialog=new Window {Owner=this,Title="未保存修改",Width=400,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#191D26"),ShowInTaskbar=false};
            var content=new StackPanel {Margin=new Thickness(22)};dialog.Content=content;content.Children.Add(Text("当前快捷键有未保存的修改。",17));
            content.Children.Add(Button("保存并继续",()=>{SaveShortcut();proceed=!dirty;dialog.Close();},true));content.Children.Add(Button("放弃修改",()=>{dirty=false;proceed=true;dialog.Close();}));content.Children.Add(Button("取消跳转",()=>dialog.Close()));dialog.ShowDialog();return proceed;
        }
        void ChooseLeaveForTest(string label){
            Dispatcher.BeginInvoke(new Action(()=>{foreach(Window window in OwnedWindows){var content=window.Content as StackPanel;if(content==null)continue;foreach(var child in content.Children){var button=child as Button;if(button!=null && (string)button.Content==label){button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));return;}}}throw new Exception("missing unsaved dialog action");}),System.Windows.Threading.DispatcherPriority.Background);
        }
        async void RunF1Checks(){
            var args=Environment.GetCommandLineArgs();string output=null;for(int i=0;i+1<args.Length;i++)if(args[i]=="--data-dir")output=args[i+1];if(output==null)throw new InvalidOperationException("isolated data directory required");Directory.CreateDirectory(output);
            try{
                await System.Threading.Tasks.Task.Delay(500);OpenRoute(true,IntPtr.Zero);
                if(savedKey!="F1")throw new Exception("initial value");
                key.Text="^F8";SaveShortcut();if(dirty||savedKey!="^F8"||ReadShortcut()!="^F8")throw new Exception("IPC save: "+status.Text);
                key.Text="^b";SaveShortcut();if(!dirty||savedKey!="^F8")throw new Exception("conflict preserves draft");
                ChooseLeaveForTest("保存并继续");if(CanLeave()||!dirty)throw new Exception("failed save must block navigation");
                ChooseLeaveForTest("取消跳转");if(CanLeave()||!dirty)throw new Exception("cancel navigation preserves draft");
                ChooseLeaveForTest("放弃修改");if(!CanLeave()||dirty)throw new Exception("discard draft");
                key.Text=savedKey;dirty=false;Navigate("首页");if(settingsToggle.Visibility!=Visibility.Collapsed)throw new Exception("home controls");
                Navigate("Kedit");if(key.Text!="^F8")throw new Exception("reload saved value");UpdateLayout();Capture(Path.Combine(output,"f1-page.png"));
                Navigate("桌宠OSD");UpdateLayout();Capture(Path.Combine(output,"legacy-pet.png"));Navigate("Kedit");
                for(int i=0;i<keditShortcuts.Length;i++){OpenRoute(i+1,IntPtr.Zero);if(CurrentShortcut.Id!=keditShortcuts[i].Id)throw new Exception("route mismatch");key.Text="^!F"+(i+1);SaveShortcut();if(dirty||ReadShortcut()!=key.Text)throw new Exception("save "+CurrentShortcut.Id+": "+status.Text);key.Text=CurrentShortcut.Default;SaveShortcut();if(dirty||ReadShortcut()!=CurrentShortcut.Default)throw new Exception("restore default "+CurrentShortcut.Id+": "+status.Text);}
                UpdateLayout();Capture(Path.Combine(output,"all-kedit.png"));
                File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: all 10 Kedit routes and saves; native IPC save, conflict draft, persisted reload, home route, unsaved-change choices, embedded pet page");
            }catch(Exception ex){File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL: "+ex);}
            finally{dirty=false;((App)Application.Current).ExitConsole();}
        }
        void SaveShortcut(){
            string value=key.Text.Trim();if(value.Length==0||value.Contains("|")||value.Contains("\n")||value.Contains("\r")){status.Text="请输入有效快捷键。";return;}
            if(!IsWindow(ahkWindow)){status.Text="快捷键主程序未连接，请从托盘重新打开中控。";return;}
            string command="save_kedit_hotkey|"+CurrentShortcut.Id+"|"+value;IntPtr text=Marshal.StringToHGlobalUni(command);
            try{var data=new CopyData{Bytes=(command.Length+1)*2,Text=text};IntPtr result;
                if(SendMessageTimeout(ahkWindow,0x4A,new System.Windows.Interop.WindowInteropHelper(this).Handle,ref data,2,4000,out result)==IntPtr.Zero){status.Text="主程序响应超时，结果未确认；草稿已保留，请重新打开页面核对。";return;}
                int code=result.ToInt32();if(code!=1){status.Text=code==2?"快捷键格式无效或无法注册。":code==3?"该快捷键与 Kedit 现有快捷键或保留键冲突。":code==4?"配置文件写入失败，已恢复原快捷键。":"主程序未支持此操作，请重启更新后的快捷键软件。";return;}
                savedKey=value;dirty=false;title.Text=CurrentShortcut.Title+" · "+value;((TextBlock)formHeader.Children[1]).Text="当前："+value+"　默认："+CurrentShortcut.Default;status.Text="已保存并生效";
            }finally{Marshal.FreeHGlobal(text);}
        }
    }
}
