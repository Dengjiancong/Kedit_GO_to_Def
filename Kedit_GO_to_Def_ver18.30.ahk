;; =======================================================
;; 【核心修复】强制脚本以管理员身份运行 (放在脚本最顶部)
;; =======================================================
;Loop, %0%  ; 兼容参数传递
;  params .= A_Space . %A_Index%
;
;if not A_IsAdmin
;{
;   try{
;		Run *RunAs "%A_ScriptFullPath%" %params%
;   }
;   catch{
;       MsgBox, 48, 权限错误, Kedit 需要管理员权限才能控制 VS (管理员模式)。`n请手动右键脚本 -> 以管理员身份运行。
;   }
;   ExitApp
;}

; =======================================================
; Kedit 助手 - 终极完整版 (v18.30-Meme.v0.01 FileInstall + OSD)
; =======================================================
#SingleInstance Force
#NoEnv
SendMode Input
SetWorkingDir %A_ScriptDir%
Global CurrentVersion := "v18.30-Meme.v0.01"

; 定义配置文件路径
IniFile := A_ScriptDir . "\Kedit_Settings.ini"

; --- 1. 读取或初始化快捷键设置 ---
IniRead, Key_GoToDef,    %IniFile%, Hotkeys, GoToDef,    XButton1
IniRead, Key_ShiftF2,    %IniFile%, Hotkeys, ShiftF2,    XButton2
IniRead, Key_AltF,       %IniFile%, Hotkeys, AltF,       !f
IniRead, Key_CtrlW,		 %IniFile%, Hotkeys, CtrlW, 	 ^w
IniRead, Key_SmartClick, %IniFile%, Hotkeys, SmartClick, ~MButton
IniRead, Key_AltA,       %IniFile%, Hotkeys, AltA,       !a  ; <--- 新增 Alt+A 变量
IniRead, Key_ColumnInsert, %IniFile%, Hotkeys, ColumnInsert, !i  ; [新增代码] --- 列选择批量填入
IniRead, Key_ToggleComment, %IniFile%, Hotkeys, ToggleComment, ^/	; [新增代码] --- 注释/取消注释 (单键切换)
IniRead, Key_SpacesToTabs, %IniFile%, Hotkeys, SpacesToTabs, ^\ ; 行首每 4 个空格转换为 1 个 Tab
IniRead, Key_FindClipboard, %IniFile%, Hotkeys, FindClipboard, F1 ; 查找剪贴板内容

; [新增代码] --- Visual Studio 专用快捷键设置
IniRead, Key_VS_Peek,    %IniFile%, Hotkeys, VS_Peek,    MButton
IniRead, Key_VS_Back,    %IniFile%, Hotkeys, VS_Back,    ^b
IniRead, Key_VS_Build,   %IniFile%, Hotkeys, VS_Build,   F7

; --- Kedit以外的快捷键设置
IniRead, Key_RunPy,      %IniFile%, Hotkeys, RunPy,      F8
IniRead, Key_CtrlQ,      %IniFile%, Hotkeys, CtrlQ,      ^q
IniRead, Path_QuickOpen, %IniFile%, Settings, QuickPath, Z:\misc\testing\ATE\K2

; 读取 OSD 开关设置 (默认开启 = 1)
IniRead, EnableOSD,      %IniFile%, Settings, EnableOSD, 1
IniRead, EnableCompanionOSD, %IniFile%, Settings, EnableCompanionOSD, 1
IniRead, CompanionChance, %IniFile%, Settings, CompanionChance, 25
IniRead, CompanionCooldown, %IniFile%, Settings, CompanionCooldown, 15
if (CompanionChance != 15 && CompanionChance != 25 && CompanionChance != 50 && CompanionChance != 100)
    CompanionChance := 25
if (CompanionCooldown < 1)
    CompanionCooldown := 15
Global LastCompanionTick := 0

; --- 2. 设置托盘菜单 ---
Menu, Tray, NoStandard
Menu, Tray, Add, 设置: 默认 侧后键 (Ctrl+B), SetKey_GoToDef
Menu, Tray, Add, 设置: 默认 侧前键 (ShiftF2), SetKey_ShiftF2
Menu, Tray, Add, 设置: 默认 Alt+F (Find in files), SetKey_AltF
Menu, Tray, Add, 设置: 默认 Ctrl+W (关闭窗口), SetKey_CtrlW
Menu, Tray, Add, 设置: 默认 Alt+A (另存为), SetKey_AltA ; <--- 新增菜单项
Menu, Tray, Add, 设置: 默认 Alt+I (列填入数据), SetKey_ColumnInsert ; [新增代码]
Menu, Tray, Add, 设置: 默认 Ctrl+/ (注释/取消注释), SetKey_ToggleComment	; [新增代码] --- 注释切换菜单
Menu, Tray, Add, 设置: 默认 Ctrl+\ (行首空格转 Tab), SetKey_SpacesToTabs
Menu, Tray, Add, 设置: 默认 F1 (查找剪贴板内容), SetKey_FindClipboard
Menu, Tray, Add, 设置: 默认 中键 (跳转至定义), SetKey_SmartClick

; --- Visual Studio 的设置入口
Menu, Tray, Add  ; 分隔线
Menu, Tray, Add, 设置: VS 预览定义 (默认中键), SetKey_VS_Peek
Menu, Tray, Add, 设置: VS 回退 (默认Ctrl+B), SetKey_VS_Back
Menu, Tray, Add, 设置: VS 生成/Ctrl+B (默认F7), SetKey_VS_Build

; --- Kedit 以外的设置入口
Menu, Tray, Add  ; 分隔线
Menu, Tray, Add, 设置: 默认 F8 (运行Py脚本), SetKey_RunPy
Menu, Tray, Add, 设置: 快速打开 (键位与路径), SetQuickOpen_All

; OSD 开关菜单项
Menu, CompanionChanceMenu, Add, 低频（15％）, SetCompanionChance15
Menu, CompanionChanceMenu, Add, 标准（25％）, SetCompanionChance25
Menu, CompanionChanceMenu, Add, 较多（50％）, SetCompanionChance50
Menu, CompanionChanceMenu, Add, 每次（100％）, SetCompanionChance100
UpdateCompanionChanceMenu()
Menu, CompanionPreviewMenu, Add, 成功表情, PreviewCompanionSuccess
Menu, CompanionPreviewMenu, Add, 疑惑表情, PreviewCompanionQuestion
Menu, CompanionPreviewMenu, Add, 处理中表情, PreviewCompanionBusy

Menu, Tray, Add, 开启屏幕操作提示 (OSD), ToggleOSD
if (EnableOSD = 1)
    Menu, Tray, Check, 开启屏幕操作提示 (OSD)
else
    Menu, Tray, Uncheck, 开启屏幕操作提示 (OSD)
Menu, Tray, Add, 开启陪伴表情 (OSD), ToggleCompanionOSD
if (EnableCompanionOSD = 1)
    Menu, Tray, Check, 开启陪伴表情 (OSD)
else
    Menu, Tray, Uncheck, 开启陪伴表情 (OSD)
Menu, Tray, Add, 表情出现频率, :CompanionChanceMenu
Menu, Tray, Add, 预览陪伴表情, :CompanionPreviewMenu
Menu, Tray, Add, 恢复默认快捷键设置, RestoreDefaults
Menu, Tray, Add  ; 分隔线
Menu, Tray, Add, 检查更新, CheckForUpdate
Menu, Tray, Add, 关于 Kedit 助手, ShowAboutGui
Menu, Tray, Add  ; 分隔线
Menu, Tray, Add, 重启脚本, ReloadScript
Menu, Tray, Add, 退出, ExitScript

Global IsCheckingUpdate := false
Global TargetMB := 205

; 窗口同步句柄
Global hMainBg := 0
Global hCard := 0
Global OffsetX := 0
Global OffsetY := 0

; [重连机制变量]
Global CheckStartTime := 0
Global WaitingGuiShown := false
Global CheckerPID := 0
Global RetryCount := 0
Global MaxRetries := 5

; ★ MPV 进程管理数组
Global MPV_PIDs := {}

; --- 3. 激活动态快捷键 ---
UpdateHotkeys()

; =======================================================
; 原有的注册表逻辑
; =======================================================
extensionsModified := false
if (!extensionsModified) {
    RegRead, showExtensions, HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced, HideFileExt
    if (showExtensions = 1) {
        RegWrite, REG_DWORD, HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced, HideFileExt, 0
        MsgBox, File extensions are now Enabled.`nIf the file extension is not displayed, please Refresh.
    }
    extensionsModified := true
}

; 脚本启动后 300ms 开始后台预释放资源，避免开界面时才触发 FileInstall 的 I/O 卡顿
SetTimer, PreinstallAssets, -300
return

; =======================================================
; 辅助菜单标签
; =======================================================
ReloadScript:
    Gosub, KillAllMpv
    Reload
return

ExitScript:
    Gosub, KillAllMpv
ExitApp
return

KillAllMpv:
    for pid, val in MPV_PIDs {
        Process, Close, %pid%
    }
    MPV_PIDs := {}
return

; =======================================================
; 后台预释放资源 (脚本启动 300ms 后异步执行)
; 将 mpv.exe 和视频素材提前写入 Temp，
; 避免首次打开设置/更新界面时 FileInstall 造成卡顿
; =======================================================
PreinstallAssets:
    GetTempPath("mpv.exe")
    GetTempPath("side.mp4")
    GetTempPath("waiting.mp4")
    GetTempPath("update_bg.mp4")
    GetTempPath("latest_bg.mp4")
    GetTempPath("logo.png")
    GetTempPath("btn_yellow.png")
    GetTempPath("companion_success.png")
    GetTempPath("companion_question.png")
    GetTempPath("companion_busy.png")
return

; =======================================================
; 恢复默认设置
; =======================================================
RestoreDefaults:
    MsgBox, 36, 确认操作, 是否确定要将所有快捷键恢复为初始默认值？`n(您当前的自定义设置将会丢失),
    IfMsgBox, No
        return

    ;确保下方热键对所有窗口生效
    Hotkey, IfWinActive
    try {
        ; 注册 Ctrl+Q (快速打开目录) 为全局热键
        Hotkey, %Key_CtrlQ%, Label_CtrlQ, On
    } catch e {
        MsgBox, 16, 错误, 无法注册全局快捷键 (%Key_CtrlQ%)
    }

    Hotkey, IfWinActive, ahk_exe kedit.exe
    try {
        Hotkey, %Key_GoToDef%, Off
        Hotkey, %Key_ShiftF2%, Off
        Hotkey, %Key_AltF%, Off
        Hotkey, %Key_CtrlW%, Off
        Hotkey, %Key_SmartClick%, Off
        Hotkey, %Key_ColumnInsert%, Off ; [新增代码] 取消旧热键
        Hotkey, %Key_ToggleComment%, Off
        Hotkey, %Key_SpacesToTabs%, Off
        Hotkey, %Key_FindClipboard%, Off
    }

    Hotkey, IfWinActive, ahk_class CabinetWClass
    try {
        Hotkey, %Key_RunPy%, Off
    }

    Key_GoToDef    := "XButton1"
    Key_ShiftF2    := "XButton2"
    Key_AltF       := "!f"
    Key_CtrlW 	   := "^w"
    Key_AltA       := "!a"        ; <--- 重置为默认值
    Key_ColumnInsert := "!i"      ; [新增代码]
    Key_SmartClick := "~MButton"
    Key_RunPy      := "F8"
    Key_ToggleComment := "^/"  ; <--- 恢复默认值
    Key_SpacesToTabs := "^\"
    Key_FindClipboard := "F1"

    IniWrite, %Key_GoToDef%,    %IniFile%, Hotkeys, GoToDef
    IniWrite, %Key_ShiftF2%,    %IniFile%, Hotkeys, ShiftF2
    IniWrite, %Key_AltF%,       %IniFile%, Hotkeys, AltF
    IniWrite, %Key_CtrlW%, 		%IniFile%, Hotkeys, CtrlW
    IniWrite, %Key_AltA%, %IniFile%, Hotkeys, AltA
    IniWrite, %Key_ColumnInsert%, %IniFile%, Hotkeys, ColumnInsert ; [新增代码]
    IniWrite, %Key_SmartClick%, %IniFile%, Hotkeys, SmartClick
    IniWrite, %Key_RunPy%,      %IniFile%, Hotkeys, RunPy
    IniWrite, %Key_ToggleComment%, %IniFile%, Hotkeys, ToggleComment ; <--- 写入 INI
    IniWrite, %Key_SpacesToTabs%, %IniFile%, Hotkeys, SpacesToTabs
    IniWrite, %Key_FindClipboard%, %IniFile%, Hotkeys, FindClipboard

    UpdateHotkeys()
    MsgBox, 64, 成功, 所有快捷键已恢复为默认设置！
return

; =======================================================
; 核心逻辑标签 (集成 OSD)
; =======================================================
Label_GoToDef:
    ShowOSD("Back To Edit Point")
    Send ^b
return

Label_ShiftF2:
    ShowOSD("Previous Bookmark")
    Send +{F2}
return

Label_AltF:
    ShowOSD("Find in Files")
    Send !tn
return

Label_CtrlW:
    ShowOSD("Close Window")
    Send !fc  ; 恢复为 Kedit 的关闭窗口菜单指令 (File -> Close)
return

Label_AltA:  ; <--- 新增 Alt+A 逻辑标签
    ShowOSD("Action: Alt+F+A")
    Send !fa
return

Label_FindClipboard:
    ShowOSD("Find Clipboard Text")
    SendInput, ^f
    Sleep, 10
    SendInput, ^v
    Sleep, 10
    SendInput, {Enter}
return

; =======================================================
; 按 4 列制表位整理所选文本的行首缩进，并保持正文的视觉列不变
; =======================================================
Label_SpacesToTabs:
    if (IsSpacesToTabsBusy) {
        ShowOSD("正在转换缩进，请稍候...")
        return
    }

    IsSpacesToTabsBusy := true
    SetTimer, ProcessSpacesToTabs, -1
return

ProcessSpacesToTabs:
    ; 从热键线程中剥离后释放修饰键，避免 Ctrl 处于按下状态干扰复制/粘贴。
    SendInput, {Blind}{LCtrl Up}{RCtrl Up}{LAlt Up}{RAlt Up}{LShift Up}{RShift Up}

    SpacesToTabs_ClipSaved := ClipboardAll
    Clipboard := ""

    BlockInput, On
    SendInput, ^c
    BlockInput, Off

    SendInput, {Blind}{LCtrl Up}{RCtrl Up}{LAlt Up}{RAlt Up}{LShift Up}{RShift Up}
    ClipWait, 2.0
    if (ErrorLevel) {
        Clipboard := SpacesToTabs_ClipSaved
        IsSpacesToTabsBusy := false
        ShowOSD("未选中文本或复制超时")
        return
    }

    SpacesToTabs_Source := Clipboard
    SpacesToTabs_Result := ConvertLeadingSpacesToTabs(SpacesToTabs_Source, SpacesToTabs_TabCount, SpacesToTabs_LineCount)

    if (SpacesToTabs_TabCount = 0) {
        Clipboard := SpacesToTabs_ClipSaved
        IsSpacesToTabsBusy := false
        ShowOSD("行首缩进无需整理")
        return
    }

    Clipboard := SpacesToTabs_Result
    Sleep, 30

    BlockInput, On
    SendInput, ^v
    Sleep, 120
    BlockInput, Off

    SendInput, {Blind}{LCtrl Up}{RCtrl Up}{LAlt Up}{RAlt Up}{LShift Up}{RShift Up}
    ShowOSD("缩进整理: " . SpacesToTabs_LineCount . " 行 / " . SpacesToTabs_TabCount . " 个 Tab")
    SetTimer, RestoreSpacesToTabsClipboard, -150
return

RestoreSpacesToTabsClipboard:
    ; 若用户在后台恢复前主动复制了其他内容，则保留用户的新剪贴板。
    if (Clipboard = SpacesToTabs_Result)
        Clipboard := SpacesToTabs_ClipSaved
    SpacesToTabs_ClipSaved := ""
    IsSpacesToTabsBusy := false
return

ConvertLeadingSpacesToTabs(Text, ByRef ConvertedTabs, ByRef ConvertedLines) {
    TabSize := 4
    ConvertedTabs := 0
    ConvertedLines := 0
    Result := ""
    Lines := StrSplit(Text, "`n")

    for LineIndex, CurrentLine in Lines {
        ; StrSplit 保留了 Windows 换行中的 `r，先暂时移除以正确识别纯空行。
        CarriageReturn := ""
        if (SubStr(CurrentLine, 0) = "`r") {
            CarriageReturn := "`r"
            LineBody := SubStr(CurrentLine, 1, -1)
        } else {
            LineBody := CurrentLine
        }

        PrefixLength := 0
        VisualColumn := 0
        LineLength := StrLen(LineBody)

        Loop, %LineLength% {
            PrefixChar := SubStr(LineBody, A_Index, 1)
            if (PrefixChar = " ") {
                VisualColumn++
            } else if (PrefixChar = "`t") {
                VisualColumn += TabSize - Mod(VisualColumn, TabSize)
            } else {
                break
            }
            PrefixLength++
        }

        ; 没有正文的纯空行保持原样，避免产生无意义的隐藏修改。
        if (PrefixLength < LineLength) {
            TabsForLine := Floor(VisualColumn / TabSize)
            SpacesForLine := Mod(VisualColumn, TabSize)
            NewPrefix := ""

            Loop, %TabsForLine%
                NewPrefix .= "`t"
            Loop, %SpacesForLine%
                NewPrefix .= " "

            OldPrefix := SubStr(LineBody, 1, PrefixLength)
            if (NewPrefix != OldPrefix) {
                LineBody := NewPrefix . SubStr(LineBody, PrefixLength + 1)
                ConvertedTabs += TabsForLine
                ConvertedLines++
            }
        }

        CurrentLine := LineBody . CarriageReturn

        if (LineIndex > 1)
            Result .= "`n"
        Result .= CurrentLine
    }

    return Result
}

Label_SmartClick:
    ; =======================================================
    ; 【极速模式】BlockInput + 强制逻辑释放
    ; =======================================================

    ; 1. 提升线程优先级，确保中间不被其他定时器打断
    Critical

    ; 2. 【核心回答】开启输入阻断
    ; 这会屏蔽键盘和鼠标的物理输入，防止您在脚本执行期间的微操作干扰逻辑
    BlockInput, On

    ; 3. 【解决死锁的更优解】
    ; 不等待物理按键松开，而是直接发送一个逻辑“弹起”信号。
    ; 这告诉 Windows：“中键已经松开了”（哪怕您的手还按着）。
    ; 这能瞬间消除“中键+右键”的冲突状态，且零延迟。
    if (InStr(A_ThisHotkey, "MButton"))
        SendInput {MButton Up}

    ; --- 原有业务逻辑 (保持不变) ---
    delay := 2
    if (A_PriorHotkey <> A_ThisHotkey or A_TimeSincePriorHotkey > 300)
    {
        SendInput {RButton}
        WinGetTitle, winTitle, A

        if (InStr(winTitle, "Boya_patterns") or InStr(winTitle, "Boya2_patterns2"))
        {
            ShowOSD("Smart: Up x 3")
            Sleep, delay
            SendInput {Up 3}
        }
        else if !(InStr(winTitle, ".kpl"))
        {
            ShowOSD("Smart: Up x 2")
            Sleep, delay
            SendInput {Up 2}
        }
        else if (InStr(winTitle, ".kpl"))
        {
            ShowOSD("Smart: Up x 1")
            Sleep, delay
            SendInput {Up 1}
        }
        ProcessKeditWindow(winTitle, delay)
    }
    else
    {
        WinGetTitle, winTitle, A
        if (InStr(winTitle, ".kpl"))
        {
            SendInput {RButton}
            ShowOSD("Smart: Double (Up 1)")
            Sleep, delay
            SendInput {Up 1}
            ProcessKeditWindow(winTitle, delay)
        }
        else
        {
            SendInput {RButton}
            ShowOSD("Smart: Double (Up 3)")
            Sleep, delay
            SendInput {Up 3}
            ProcessKeditWindow(winTitle, delay)
        }
    }

    ; 4. 解除输入阻断，恢复正常控制
    BlockInput, Off
return

; [新增代码] =======================================================
; 普通选择模拟列块插入 (完美修复 Tab 制表符对齐错位问题)
; =======================================================
Label_ColumnInsert:
    ; --- 🔧 关键配置：你的编辑器一个 Tab(制表符) 占用几个空格的宽度？ ---
    ; 绝大多数现代 IDE 是 4，部分老牌编辑器(如纯正的Kedit)可能是 8。
    ; 如果还是有轻微错位，请把这里改成 8。
    TabSize := 4

    ; 1. 防粘滞，强制释放控制键
    SendInput, {Blind}{LCtrl Up}{RCtrl Up}{LAlt Up}{RAlt Up}{LShift Up}{RShift Up}

    ClipSaved := ClipboardAll
    Clipboard := ""

    ; 2. 复制你的普通选区
    BlockInput, On
    SendInput, ^c
    BlockInput, Off

    ClipWait, 1.0
    if (ErrorLevel) {
        ShowOSD("Error: 未选中内容")
        Clipboard := ClipSaved
        return
    }

    FullText := Clipboard

    ; 检查是否包含换行符
    StrReplace(FullText, "`n", "`n", LFCount)
    if (LFCount == 0) {
        ShowOSD("Error: 请跨行选择以确定列位置")
        Clipboard := ClipSaved
        return
    }

    ; 3. 弹窗询问要填入的数据
    InputBox, InsertData, Kedit 列填充, 请输入要垂直插入的数据 (例如 8,)：,, 320, 150
    if (ErrorLevel) {
        Clipboard := ClipSaved
        return
    }

    ShowOSD("⏳ 正在计算视觉列偏移...", 0)

    Lines := StrSplit(FullText, "`n", "`r")
    LineCount := Lines.MaxIndex()

    ; =======================================================
    ; 💡 核心魔法 1：计算最后一行前缀的“视觉宽度”(Visual Width)
    ; 将 \t 展开为实际的屏幕列数，破除混排对齐陷阱
    ; =======================================================
    TargetVWidth := 0
    Loop, Parse, % Lines[LineCount]
    {
        if (A_LoopField == "`t")
            TargetVWidth += TabSize - Mod(TargetVWidth, TabSize)
        else
            TargetVWidth += 1
    }

    ; 4. 逐行处理拼接
    NewText := ""
    For index, line in Lines
    {
        if (index == 1) {
            ; 首行：直接加在前面
            NewText .= InsertData . line
        }
        else if (index == LineCount) {
            ; 末行：直接加在后面
            NewText .= line . InsertData
        }
        else {
            ; =======================================================
            ; 💡 核心魔法 2：用视觉宽度在中间行寻找准确的“字符切分点”
            ; =======================================================
            vWidth := 0
            SplitIndex := StrLen(line)

            Loop, Parse, line
            {
                ; 一旦当前扫描的视觉宽度达到基准宽度，这就是切割点
                if (vWidth >= TargetVWidth) {
                    SplitIndex := A_Index - 1
                    break
                }

                if (A_LoopField == "`t")
                    vWidth += TabSize - Mod(vWidth, TabSize)
                else
                    vWidth += 1
            }

            LeftPart := SubStr(line, 1, SplitIndex)
            RightPart := SubStr(line, SplitIndex + 1)

            ; 容错机制：如果某中间行非常短，用空格给它补齐到基准列
            if (vWidth < TargetVWidth) {
                Loop, % TargetVWidth - vWidth
                    LeftPart .= " "
            }

            NewText .= LeftPart . InsertData . RightPart
        }

        ; 加上换行符拼接回去
        if (index < LineCount) {
            NewText .= "`r`n"
        }
    }

    ; 5. 覆盖粘贴回编辑器
    Clipboard := NewText
    Sleep, 50

    BlockInput, On
    SendInput, ^v
    Sleep, 150
    BlockInput, Off

    ; 6. 异步恢复原有的剪贴板内容
    SetTimer, RestoreClipboardCol, -200
return

RestoreClipboardCol:
    Clipboard := ClipSaved
    ShowOSD("✔ 批量列插入完成")
return

; =======================================================
; 多行注释 / 取消注释 (单键 Toggle 智能模式 - 完美时序版)
; =======================================================

; 1. 快捷键触发入口 (带互斥状态锁)
Label_ToggleComment:
    if (IsToggleBusy) {
        ShowOSD("⏳ 正在处理中，请稍候...")
        return
    }

    IsToggleBusy := true

    ; ★ 架构级防御：使用异步定时器剥离“热键生命周期”
    SetTimer, ProcessCommentToggle, -1
return

; 2. 实际干活的处理车间
ProcessCommentToggle:
    ; ★ 终极防粘滞：左右键精准点名释放 (剿灭 RCtrl 幽灵)
    SendInput, {Blind}{LCtrl Up}{RCtrl Up}{LAlt Up}{RAlt Up}{LShift Up}{RShift Up}

    ClipSaved := ClipboardAll
    Clipboard := ""

    BlockInput, On
    SendInput, ^c
    BlockInput, Off

    ; ★ 补丁 1
    SendInput, {Blind}{LCtrl Up}{RCtrl Up}{LAlt Up}{RAlt Up}{LShift Up}{RShift Up}

    ClipWait, 6.0
    if (ErrorLevel) {
        ShowOSD("Error: Copy Timeout")
        Clipboard := ClipSaved
        IsToggleBusy := false

        ; ★ 补丁 2
        SendInput, {Blind}{LCtrl Up}{RCtrl Up}{LAlt Up}{RAlt Up}{LShift Up}{RShift Up}
        SendInput, {LCtrl}
        return
    }

    FullText := Clipboard

    ; 【防堆叠机制：智能单行判定】
    StrReplace(FullText, "`n", "`n", LFCount)
    if (LFCount == 0 || (LFCount == 1 && RegExMatch(FullText, "\n$"))) {
        BlockInput, On
        SendInput, {Home}{Home}+{End}
        Sleep, 50
        Clipboard := ""
        SendInput, ^c
        BlockInput, Off

        ; ★ 补丁 3
        SendInput, {Blind}{LCtrl Up}{RCtrl Up}{LAlt Up}{RAlt Up}{LShift Up}{RShift Up}

        ClipWait, 1.0
        if (!ErrorLevel) {
            FullText := Clipboard
        } else {
            FullText := ""
        }
    }

    ; =======================================================
    ; 【核心修复：纯空行与 AHK 数组特性处理】
    ; =======================================================
    if (FullText == "`r`n" || FullText == "`n" || FullText == "") {
        TailStr := ""
        ProcessText := ""
    } else {
        RegExMatch(FullText, "(\r?\n)$", TailStr)
        ProcessText := RegExReplace(FullText, "(\r?\n)$", "")
    }

    Lines := StrSplit(ProcessText, "`n", "`r")
    LineCount := Lines.MaxIndex()
    if (LineCount == "")
        LineCount := 0

    if (LineCount == 0) {
        LineCount := 1
        Lines[1] := ""
    }

    Global CurrentProcessLines := LineCount

    ; 【状态判定】
    IsAllCommented := true
    for index, CurrentLine in Lines
    {
        if (CurrentLine == "" || !RegExMatch(CurrentLine, "^//")) {
            IsAllCommented := false
            Break
        }
    }

    ; 【自适应 OSD 反馈】
    ActionStr := IsAllCommented ? "Uncomment All" : "Comment All"
    if (LineCount > 1000) {
        ShowOSD(ActionStr . "  ⏳ 写入编辑器...", 0)
    } else {
        ShowOSD(ActionStr)
    }

    ; 【文本处理】
    NewText := ""
    for index, CurrentLine in Lines
    {
        if (IsAllCommented) {
            ProcessedLine := RegExReplace(CurrentLine, "^//", "")
        } else {
            ProcessedLine := "//" . CurrentLine
        }

        if (index > 1)
            NewText .= "`r`n" . ProcessedLine
        else
            NewText .= ProcessedLine
    }
    NewText .= TailStr

    ; =======================================================
    ; 【全新进化：安全粘贴与空内容清除】
    ; =======================================================
    if (NewText == "") {
        BlockInput, On
        SendInput, {Del}
        BlockInput, Off

        ; ★ 补丁 4
        SendInput, {Blind}{LCtrl Up}{RCtrl Up}{LAlt Up}{RAlt Up}{LShift Up}{RShift Up}
        SendInput, {LCtrl}

        PasteDelay := 100
    } else {
        Clipboard := NewText
        Sleep, 50

        BlockInput, On
        SendInput, ^v
        Sleep, 150
        BlockInput, Off

        ; ★ 补丁 5
        SendInput, {Blind}{LCtrl Up}{RCtrl Up}{LAlt Up}{RAlt Up}{LShift Up}{RShift Up}
        SendInput, {LCtrl}

        TotalChars := StrLen(NewText)
        PasteDelay := 100 + (TotalChars // 1000) * 5
        MaxPasteDelay := 1000

        if (LineCount > 50000) {
            MaxPasteDelay := 4000
        } else if (LineCount > 40000) {
            MaxPasteDelay := 3000
        } else if (LineCount > 30000) {
            MaxPasteDelay := 2000
        }

        if (PasteDelay > MaxPasteDelay) {
            PasteDelay := MaxPasteDelay
        }
    }

    SetTimer, RestoreClipboardBg, -%PasteDelay%
return

; =======================================================
; 后台恢复车间 (带指纹校验与最终反馈)
; =======================================================
RestoreClipboardBg:
    if (Clipboard == NewText) {
        Clipboard := ClipSaved
    }

    IsToggleBusy := false

    if (CurrentProcessLines > 1000) {
        ShowOSD("✔ Done!")
    }
return

; =======================================================
; Visual Studio 专用逻辑
; =======================================================
Label_VS_PeekDef:
    ShowOSD("VS: Peek Definition")
    ; 1. 先发送左键点击，将光标定位到鼠标指向的单词上
    SendInput {LButton}

    ; 2. 稍微等待一下 (50毫秒)，确保 VS 有时间把光标移过去
    Sleep, 2

    ; 3. 发送 Alt + F12 调出预览窗口
    SendInput !{F12}
return

Label_VS_NavigateBack:
    ShowOSD("VS: Navigate Back")
    ; 发送 Ctrl + -(减号)
    SendInput ^-
return

Label_VS_SendCtrlB:
    ShowOSD("VS: Send Ctrl+B")
    ; 发送 Ctrl + B (触发 VS 原生的生成或其他功能)
    SendInput ^b
return

ProcessKeditWindow(winTitle, delay)
{
    ; 【优化建议】
    ; 原代码使用 IfWinExist, Kedit，这很危险。
    ; 如果 Kedit 只是"存在"在后台，但当前激活的是 Chrome，
    ; SendInput {Enter} 会把回车键发送给 Chrome。
    ; 建议改为 IfWinActive (只有当 Kedit 是激活窗口时才发回车)
    ; 或者保持原样（如果您确信 Kedit 此时一定是激活的）

    IfWinActive, Kedit  ; <--- 建议修改此处，确保只有 Kedit 在前台时才发 Enter
    {
        WinGetText, winText, Kedit
        if !(InStr(winText, "This file has been modified outside of Kedit"))
        {
            SendInput {Enter}
        }
        else
        {
            return
        }
    }
    else
    {
        return
    }
}

UpdateHotkeys() {
    global
    ; --- [新增代码] 第一组：全局快捷键 ---
    Hotkey, IfWinActive
    try {
        ; 注册 Ctrl+Q (快速打开目录)
        Hotkey, %Key_CtrlQ%, Label_CtrlQ, On
    } catch e {
        MsgBox, 16, 错误, 无法注册全局快捷键 (%Key_CtrlQ%)
    }

    ; --- [新增代码] 第二组：Kedit 专用快捷键 ---
    Hotkey, IfWinActive, ahk_exe kedit.exe
    try {
        Hotkey, %Key_GoToDef%,    Label_GoToDef,    On
        Hotkey, %Key_ShiftF2%,    Label_ShiftF2,    On
        Hotkey, %Key_AltF%,       Label_AltF,       On
        Hotkey, %Key_CtrlW%, 	  Label_CtrlW, 		On
        Hotkey, %Key_AltA%,       Label_AltA,       On ; <--- 注册新快捷键 [cite: 23]
        Hotkey, %Key_ColumnInsert%, Label_ColumnInsert, On ; [新增代码]
        Hotkey, %Key_SmartClick%, Label_SmartClick, On
        Hotkey, %Key_ToggleComment%, ProcessCommentToggle, On
        Hotkey, %Key_SpacesToTabs%, Label_SpacesToTabs, On
        Hotkey, %Key_FindClipboard%, Label_FindClipboard, On
    } catch e {
        MsgBox, 16, 错误, 加载快捷键失败。
    }

    ; --- [新增代码] 第三组：资源管理器专用快捷键 ---
    Hotkey, IfWinActive, ahk_class CabinetWClass
    try {
        Hotkey, %Key_RunPy%,      Label_RunPy,      On
    } catch e {
        MsgBox, 16, 错误, 无法注册资源管理器快捷键 (%Key_RunPy%)
    }

    ; [新增代码] Visual Studio 专用区域 (devenv.exe)
    Hotkey, IfWinActive, ahk_exe devenv.exe
    try {
        ; [修改] 使用变量 Key_VS_Peek
        Hotkey, %Key_VS_Peek%, Label_VS_PeekDef, On

        ; [修改] 使用变量 Key_VS_Back
        Hotkey, %Key_VS_Back%, Label_VS_NavigateBack, On

        ; [修改] 使用变量 Key_VS_Build
        Hotkey, %Key_VS_Build%, Label_VS_SendCtrlB, On
    } catch e {
        MsgBox, 16, 错误, 无法注册 Visual Studio 快捷键 (%Key_RunPy%)
    }
}

; [新增代码] =======================================================
; Ctrl+Q 快速打开逻辑 (支持文件和文件夹)
; =======================================================
Label_CtrlQ:
    ; 1. 获取路径属性，检查是否存在
    PathAttr := FileExist(Path_QuickOpen)

    if (!PathAttr) {
        ShowOSD("路径不存在!")
        MsgBox, 48, 错误, 目标路径或文件不存在: `n%Path_QuickOpen%`n`n请在托盘菜单中 [设置: 修改打开的路径] 进行重新配置。
        return
    }

    ; 2. 显示 OSD
    ShowOSD("Open: " . Path_QuickOpen)

    ; 3. 根据属性判断是文件夹还是文件
    if InStr(PathAttr, "D") {
        ; 如果属性包含 "D"，说明是文件夹，使用 explore 打开资源管理器
        Run, explore %Path_QuickOpen%
    } else {
        ; 否则是文件，直接 Run 会调用系统默认关联程序打开该文件
        Run, %Path_QuickOpen%
    }
return

; =======================================================
; 设置界面逻辑
; =======================================================
SetKey_GoToDef:
    ChangeHotkey("GoToDef", "相当于 Ctrl+B 的快捷键", Key_GoToDef)
return

SetKey_ShiftF2:
    ChangeHotkey("ShiftF2", "相当于 Shift+F2 的快捷键", Key_ShiftF2)
return

SetKey_AltF:
    ChangeHotkey("AltF", "相当于 Alt+T 然后 N 的快捷键", Key_AltF)
return

SetKey_CtrlW:
    ChangeHotkey("CtrlW", "关闭窗口的快捷键", Key_CtrlW)
return

SetKey_AltA:
    ChangeHotkey("AltA", "相当于 Alt+F 然后 A 的快捷键", Key_AltA)
return

SetKey_ColumnInsert:
    ChangeHotkey("ColumnInsert", "列选择填入数据 (默认 Alt+I)", Key_ColumnInsert)
return

SetKey_SmartClick:
    ChangeHotkey("SmartClick", "智能点击功能 (原中键)`n注意: 鼠标键建议加上 ~ 前缀 (如 ~MButton) 以保留原功能", Key_SmartClick)
return

SetKey_RunPy:
    ChangeHotkey("RunPy", "在资源管理器中直接运行选中脚本`n(仅对 .py 文件生效)", Key_RunPy)
return

SetKey_ToggleComment:
    ChangeHotkey("ToggleComment", "注释/取消注释 (智能切换)`n(建议使用 Ctrl+/)", Key_ToggleComment)
return

SetKey_SpacesToTabs:
    ChangeHotkey("SpacesToTabs", "按 4 列制表位整理所选文本每行的行首缩进`n(默认 Ctrl+\；保持正文原来的视觉位置)", Key_SpacesToTabs)
return

SetKey_FindClipboard:
    ChangeHotkey("FindClipboard", "查找剪贴板内容`n(依次发送 Ctrl+F、Ctrl+V、回车，默认 F1)", Key_FindClipboard)
return

SetQuickOpen_All:
    ; 调用新的二合一设置函数
    ; 参数: (当前快捷键, 当前路径)
    ChangeQuickOpenSettings(Key_CtrlQ, Path_QuickOpen)
return

; =======================================================
; Visual Studio 快捷键设置入口
; =======================================================
SetKey_VS_Peek:
    ChangeHotkey("VS_Peek", "VS 中键替换`n(功能: 选中单词并 Alt+F12 预览定义)", Key_VS_Peek)
return

SetKey_VS_Back:
    ChangeHotkey("VS_Back", "VS 回退功能替换`n(功能: 发送 Ctrl + -)", Key_VS_Back)
return

SetKey_VS_Build:
    ChangeHotkey("VS_Build", "VS 生成/旧Ctrl+B替换`n(功能: 发送原版 Ctrl+B)", Key_VS_Build)
return

; =======================================================
; ★★★ 核心辅助：强制获取 Temp 路径 ★★★
; =======================================================
GetTempPath(FileName) {
    MediaDir := A_Temp . "\Kedit_Media"
    if (!FileExist(MediaDir))
        FileCreateDir, %MediaDir%

    TargetPath := MediaDir . "\" . FileName

    ; --- 混合更新逻辑 ---
    ; 1. 经常变动的小文件：强制覆盖 (Flag 设为 1)，确保换图后立即生效
    if (FileName = "logo.png") {
        FileInstall, logo.png, %TargetPath%, 1
    }
    else if (FileName = "btn_yellow.png") {
        FileInstall, btn_yellow.png, %TargetPath%, 1
    }
    else if (FileName = "companion_success.png") {
        FileInstall, osd_assets\companion_success.png, %TargetPath%, 1
    }
    else if (FileName = "companion_question.png") {
        FileInstall, osd_assets\companion_question.png, %TargetPath%, 1
    }
    else if (FileName = "companion_busy.png") {
        FileInstall, osd_assets\companion_busy.png, %TargetPath%, 1
    }

    ; 2. 庞大的静态资源：仅在不存在时释放，节省启动时间
    else {
        if (!FileExist(TargetPath)) {
            if (FileName = "mpv.exe")
                FileInstall, mpv.exe, %TargetPath%, 1
            else if (FileName = "side.mp4")
                FileInstall, side.mp4, %TargetPath%, 1
            else if (FileName = "waiting.mp4")
                FileInstall, waiting.mp4, %TargetPath%, 1
            else if (FileName = "update_bg.mp4")
                FileInstall, update_bg.mp4, %TargetPath%, 1
            else if (FileName = "latest_bg.mp4")
                FileInstall, latest_bg.mp4, %TargetPath%, 1
        }
    }
    return TargetPath
}

PlayMpvInGui(Hwnd, VideoPath) {
    Global MPV_PIDs

    MpvPath := GetTempPath("mpv.exe")
    if (!FileExist(MpvPath))
        return 0

    HwndDec := Hwnd + 0

    Args := " --wid=" . HwndDec
    Args .= " --no-config"
    Args .= " --terminal=no"
    Args .= " --loop=inf"
    Args .= " --no-border"
    Args .= " --autofit=100%x100%"
    Args .= " --panscan=1.0"
    Args .= " --no-input-default-bindings"
    Args .= " --no-osc"
    Args .= " --mute=yes"
    Args .= " --keep-open=yes"
    Args .= " --vo=gpu"
    Args .= " --hwdec=auto"
    Args .= " --force-window=immediate"
    Args .= " --demuxer-readahead-secs=0"  ; 禁用预读缓冲，加快首帧出现
    Args .= " --cache=no"                  ; 禁用文件缓存，进一步降低首帧延迟

    Cmd := """" . MpvPath . """" . Args . " """ . VideoPath . """"

    Run, %Cmd%, , Hide, ThisPID
    MPV_PIDs[ThisPID] := 1
    return ThisPID
}

; =======================================================
; 通用修改函数
; =======================================================
ChangeHotkey(KeyName, DisplayText, CurrentKey) {
    global
    Gui, Destroy
    Gui, -MinimizeBox

    Gui, -Caption +Border +HwndhSettingsGui
    Gui, Color, White

    SideW := 760
    SideH := 430

    SidebarPath := GetTempPath("side.mp4")
    XStart := 20

    if (FileExist(SidebarPath)) {
        Gui, Add, Text, x0 y0 w%SideW% h%SideH% HwndhVideoContainer
        XStart := SideW + 20
    }

    Gui, Add, Text, x%XStart% y40, %DisplayText%

    Gui, Add, Text, x%XStart% y+10, 当前设置:
    Gui, Font, Bold
    Gui, Add, Text, x+5 yp cBlue, %CurrentKey%
    Gui, Font
    Gui, Add, Text, x%XStart% y+10 h2 w300 0x10
    Gui, Add, Text, x%XStart% y+10, 方法A - 键盘录入 (不支持鼠标键):
    Gui, Add, Hotkey, vNewHotkey Limit1 x%XStart% y+5, %CurrentKey%
    Gui, Add, Text, x%XStart% y+15, 方法B - 手动输入代码 (鼠标/组合键):
    Gui, Add, CheckBox, vManualMode gToggleInput x%XStart% y+2=5 h25, 启用手动输入
    Gui, Add, Edit, vManualInput Disabled w200 x%XStart% y+5, %CurrentKey%
    Gui, Add, GroupBox, x%XStart% y+15 w280 h110, 鼠标按键代码速查 (复制填入下方)
    Gui, Add, Text, xp+10 yp+20, 左键: LButton`t`t右键: RButton
    Gui, Add, Text, y+5, 中键: MButton`t`t侧键后: XButton1
    Gui, Add, Text, y+5, 侧键前: XButton2`t滚轮: WheelUp/Down
    Gui, Add, Text, y+10 cRed, 组合键符号:  Ctrl(^), Alt(!), Shift(+)

    BtnStartX := XStart + 35
    Gui, Add, Button, default gSaveHotkey h30 w100 x%BtnStartX% y+40, 保存修改
    Gui, Add, Button, gCancelHotkey h30 w100 x+10, 取消修改

    Gui, Show, h%SideH%, 修改快捷键 - %KeyName%

    if (FileExist(SidebarPath)) {
        PlayMpvInGui(hVideoContainer, SidebarPath)
    }

    OnMessage(0x201, "WM_LBUTTONDOWN")

    CurrentEditingKey := KeyName
    return

    ToggleInput:
    Gui, Submit, NoHide
    if (ManualMode) {
        GuiControl, Disable, NewHotkey
        GuiControl, Enable, ManualInput
        GuiControl, Focus, ManualInput
    } else {
        GuiControl, Enable, NewHotkey
        GuiControl, Disable, ManualInput
        GuiControl, Focus, NewHotkey
    }
    return

    SaveHotkey:
    Gui, Submit
    Gosub, KillAllMpv
    FinalKey := (ManualMode) ? ManualInput : NewHotkey
    if (FinalKey = "") {
        MsgBox, 48, 提示, 快捷键不能为空。
        return
    }
    OldKey := Key_%CurrentEditingKey%
    Hotkey, IfWinActive, ahk_exe kedit.exe
    try {
        Hotkey, %OldKey%, Off
    }
    Key_%CurrentEditingKey% := FinalKey
    IniWrite, %FinalKey%, %IniFile%, Hotkeys, %CurrentEditingKey%
    UpdateHotkeys()
    Gui, Destroy
    MsgBox, 64, 成功, %KeyName% 已更新为: %FinalKey%
    return

    CancelHotkey:
    Gosub, KillAllMpv
    Gui, Destroy
    return
}

WM_LBUTTONDOWN() {
    PostMessage, 0xA1, 2
}

WM_MOVE(wParam, lParam, msg, hwnd) {
    Global hMainBg, hCard, OffsetX, OffsetY
    if (hwnd = hMainBg) {
        WinGetPos, mX, mY, , , ahk_id %hMainBg%
        NewX := mX + OffsetX
        NewY := mY + OffsetY
        WinMove, ahk_id %hCard%, , %NewX%, %NewY%
    }
}

; =======================================================
; 新增：通用的路径修改界面 (保持 UI 风格一致)
; =======================================================
; =======================================================
; 新增：快速打开功能的【二合一设置界面】
; =======================================================
ChangeQuickOpenSettings(CurrentKey, CurrentPath) {
    global
    Gui, Destroy
    Gui, -MinimizeBox
    Gui, -Caption +Border +HwndhSettingsGui
    Gui, Color, White

    ; --- 界面尺寸与背景 ---
    SideW := 760
    SideH := 450
    SidebarPath := GetTempPath("side.mp4")
    XStart := 20

    if (FileExist(SidebarPath)) {
        Gui, Add, Text, x0 y0 w%SideW% h%SideH% HwndhVideoContainer
        XStart := SideW + 20
    }

    ; --- 标题 ---
    Gui, Add, Text, x%XStart% y30, 快速打开 - 功能设置
    Gui, Add, Text, x%XStart% y+10 h2 w300 0x10 ; 分割线

    ; --- 区域 1: 快捷键设置 ---
    ; [修复点 1] 先设置字体为粗体+蓝色，再添加文本
    Gui, Font, Bold cBlue
    Gui, Add, Text, x%XStart% y+20 h20, 1. 触发快捷键
    ; [修复点 2] 恢复默认字体 (黑色, 正常粗细)
    Gui, Font, Norm cDefault

    Gui, Add, Text, x%XStart% y+10, 当前: %CurrentKey%
    Gui, Add, Text, x+20 yp, 新快捷键 (请直接按下):

    Gui, Add, Hotkey, vNewKeyInput x%XStart% y+5 w200 h25, %CurrentKey%

    Gui, Add, CheckBox, vUseManualKey gToggleManualKey x+10 yp+5, 手动输入代码
    Gui, Add, Edit, vNewKeyManual Disabled x%XStart% y+5 w200 h25 Hidden, %CurrentKey%

    ; --- 区域 2: 目标路径设置 ---
    ; [修复点 3] 同样的逻辑，先设粗体
    Gui, Font, Bold cBlue
    Gui, Add, Text, x%XStart% y+30 h20, 2. 目标文件夹路径
    ; [修复点 4] 恢复默认
    Gui, Font, Norm cDefault

    Gui, Add, Edit, vNewPathInput x%XStart% y+10 w240 h25, %CurrentPath%
    Gui, Add, Button, gBrowseFolderTarget x+10 yp w80 h25, 📂 浏览...

    ; --- 底部按钮 ---
    Gui, Add, Button, default gSaveQuickOpenSettings h35 w120 x%XStart% y+40, 保存全部修改
    Gui, Add, Button, gCancelQuickOpenSettings h35 w100 x+20, 取消

    Gui, Show, h%SideH%, 快速打开设置

    if (FileExist(SidebarPath)) {
        PlayMpvInGui(hVideoContainer, SidebarPath)
    }

    OnMessage(0x201, "WM_LBUTTONDOWN")
    return

    ; --- 内部交互逻辑 ---
    ToggleManualKey:
    Gui, Submit, NoHide
    if (UseManualKey) {
        GuiControl, Hide, NewKeyInput
        GuiControl, Show, NewKeyManual
        GuiControl, Enable, NewKeyManual
        GuiControl, Focus, NewKeyManual
    } else {
        GuiControl, Show, NewKeyInput
        GuiControl, Hide, NewKeyManual
        GuiControl, Disable, NewKeyManual
        GuiControl, Focus, NewKeyInput
    }
    return

    BrowseFolderTarget:
    Gui, Submit, NoHide
    FileSelectFolder, SelectedFolder, *%NewPathInput%, 3, 请选择目标文件夹
    if (SelectedFolder != "") {
        GuiControl,, NewPathInput, %SelectedFolder%
    }
    return

    SaveQuickOpenSettings:
    Gui, Submit
    Gosub, KillAllMpv

    ; 1. 获取最终的快捷键
    FinalKey := (UseManualKey) ? NewKeyManual : NewKeyInput
    if (FinalKey = "") {
        MsgBox, 48, 错误, 快捷键不能为空。
        return
    }

    ; 2. 获取最终路径
    FinalPath := NewPathInput
    if (FinalPath = "") {
        MsgBox, 48, 错误, 路径不能为空。
        return
    }

    ; 3. 简单的路径检查 (这里放宽了检查，只要不是空的就行，因为可能是网络路径)
    if (FinalPath = "") {
        MsgBox, 48, 警告, 路径无效。
        return
    }

    ; 4. 关闭旧快捷键
    Hotkey, IfWinActive
    try {
        Hotkey, %Key_CtrlQ%, Off
    }

    ; 5. 更新变量与 INI
    Key_CtrlQ := FinalKey
    Path_QuickOpen := FinalPath

    IniWrite, %Key_CtrlQ%, %IniFile%, Hotkeys, CtrlQ
    IniWrite, %Path_QuickOpen%, %IniFile%, Settings, QuickPath

    ; 6. 重新注册所有热键
    UpdateHotkeys()

    Gui, Destroy
    MsgBox, 64, 成功, 设置已更新！`n快捷键: %Key_CtrlQ%`n路径: %Path_QuickOpen%
    return

    CancelQuickOpenSettings:
    Gosub, KillAllMpv
    Gui, Destroy
    return
}

; =======================================================
; 自动更新逻辑模块
; =======================================================
CheckForUpdate:
    if (IsCheckingUpdate)
        return
    IsCheckingUpdate := true
    RetryCount := 0
    WaitingGuiShown := false
    Global CheckStartTime := A_TickCount
    GoSub, LaunchVersionChecker
return

LaunchVersionChecker:
    VersionURL := "https://raw.githubusercontent.com/Dengjiancong/Kedit_GO_to_Def/main/version.txt"

    VersionFile := A_Temp . "\version_check.txt"
    StatusFile := A_Temp . "\check_status.txt"
    CheckerAhk := A_Temp . "\kedit_checker.ahk"
    InterpreterPath := A_Temp . "\Kedit_Interpreter.exe"

    if (!FileExist(InterpreterPath))
        FileInstall, AutoHotkey.exe, %InterpreterPath%, 1

    FileDelete, %VersionFile%
    FileDelete, %StatusFile%
    if (CheckerPID)
        Process, Close, %CheckerPID%
    FileDelete, %CheckerAhk%

    ActualURL := VersionURL . "?t=" . A_TickCount

    ; 简单的版本检查脚本 (这个很短，直接写不会有问题)
    ScriptContent =
    (
    #NoTrayIcon
    URL := "%ActualURL%"
    SavePath := "%VersionFile%"
    StatusFile := "%StatusFile%"

    Modes := []
    Modes.Push("127.0.0.1:7890")
    Modes.Push("127.0.0.1:7897")
    Modes.Push("127.0.0.1:10809")
    Modes.Push(0)

    IsSuccess := 0
    for index, Mode in Modes {
        try {
            whr := ComObjCreate("WinHttp.WinHttpRequest.5.1")
            whr.Open("GET", URL, true)
            whr.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
            whr.Option(9) := 2048
            if (Mode = 0)
                 whr.SetProxy(0)
            else
                whr.SetProxy(2, Mode)
            whr.Send()
            whr.WaitForResponse(10)
            if (whr.Status == 200) {
                FileAppend, `% whr.ResponseText, `%SavePath`%
                FileAppend, success, `%StatusFile`%
                IsSuccess := 1
                break
            }
        } catch {
            continue
        }
    }
    if (!IsSuccess)
        FileAppend, error, `%StatusFile`%
    ExitApp
    )
    FileAppend, %ScriptContent%, %CheckerAhk%

    if (WaitingGuiShown) {
        DisplayCount := RetryCount + 1
        GuiControl, WaitCard:, WaitText, 正在尝试多种连接方式 (%DisplayCount%/%MaxRetries%)...
    }

    CheckStartTime := A_TickCount
    Run, "%InterpreterPath%" "%CheckerAhk%", , , CheckerPID
    SetTimer, MonitorVersionCheck, 100
return

MonitorVersionCheck:
    if (!WaitingGuiShown && (A_TickCount - CheckStartTime > 2000)) {
        WaitingGuiShown := true
        GoSub, ShowConnectingGui
    }

    if (A_TickCount - CheckStartTime > 3000) {
        SetTimer, MonitorVersionCheck, Off
        RetryCount++
        if (RetryCount < MaxRetries) {
            GoSub, LaunchVersionChecker
            return
        } else {
            GoSub, CloseWaitGui
            MsgBox, 20, 连接超时 - 需要诊断吗?, 尝试连接 GitHub %MaxRetries% 次均无响应。`n请检查网络环境或开启代理。`n`n是否运行【网络连接诊断工具】来分析具体原因？
            IfMsgBox, Yes
            {
                GoSub, LaunchNetworkDebugger
            }
            IsCheckingUpdate := false
            return
        }
    }

    StatusFile := A_Temp . "\check_status.txt"
    if (FileExist(StatusFile)) {
        SetTimer, MonitorVersionCheck, Off
        if (WaitingGuiShown) {
            GoSub, CloseWaitGui
        }
        FileRead, Status, %StatusFile%
        FileDelete, %A_Temp%\kedit_checker.ahk
        FileDelete, %StatusFile%

        if (InStr(Status, "success")) {
            VersionFile := A_Temp . "\version_check.txt"
            FileRead, LatestVersion, %VersionFile%
            FileDelete, %VersionFile%
            LatestVersion := Trim(LatestVersion, " `t`n`r")

            if (LatestVersion != "") {
                if (LatestVersion > CurrentVersion) {
                    IsNewVersion := true
                } else {
                    IsNewVersion := false
                }
                GoSub, ShowUpdateGui
            } else {
                MsgBox, 16, 错误, 获取到的版本号为空。
            }
        } else {
            RetryCount++
            if (RetryCount < MaxRetries) {
                GoSub, LaunchVersionChecker
                return
            }
            MsgBox, 16, 连接失败, 无法连接到 GitHub。
        }
        IsCheckingUpdate := false
    }
return

CloseWaitGui:
    Gosub, KillAllMpv
    Gui, WaitBg:Destroy
    Gui, WaitCard:Destroy
    WaitingGuiShown := false
return

GetScale() {
    dpi := A_ScreenDPI
    if (dpi = "")
        dpi := 96
    return dpi / 96
}

; =======================================================
; 等待连接窗口
; =======================================================
ShowConnectingGui:
    Gui, WaitBg:Destroy
    Gui, WaitCard:Destroy
    Scale := GetScale()

    WaitVideoPath := GetTempPath("waiting.mp4")

    WinW := Round(952 * Scale)
    WinH := Round(540 * Scale)

    Gui, WaitBg:New, +HwndhMainBg -MinimizeBox -MaximizeBox -Caption -DPIScale
    Gui, WaitBg:Color, 000000
    Gui, WaitBg:Margin, 0, 0

    if (FileExist(WaitVideoPath)) {
        Gui, WaitBg:Add, Text, x0 y0 w%WinW% h%WinH% HwndhWaitContainer
    }

    Gui, WaitBg:Show, w%WinW% h%WinH% Center, Kedit 检查更新

    if (FileExist(WaitVideoPath)) {
        PlayMpvInGui(hWaitContainer, WaitVideoPath)
    }

    WinGetPos, BgX, BgY, , , ahk_id %hMainBg%

    ; --- 上层卡片 ---
    CardW := Round(210 * Scale)
    CardH := Round(210 * Scale)
    MarginRight := Round(30 * Scale)
    MarginBottom := Round(30 * Scale)

    OffsetX := WinW - CardW - MarginRight
    OffsetY := WinH - CardH - MarginBottom
    CardX := BgX + OffsetX
    CardY := BgY + OffsetY

    Gui, WaitCard:New, +Owner%hMainBg% +HwndhCard -Caption +ToolWindow -DPIScale
    Gui, WaitCard:Color, 151515
    Gui, WaitCard:Margin, 0, 0

    Gui, WaitCard:Font, s14 cWhite Bold, 微软雅黑
    TxtY1 := Round(80 * Scale)
    Gui, WaitCard:Add, Text, x0 y%TxtY1% w%CardW% Center BackgroundTrans, 请稍候...

    Gui, WaitCard:Font, s10 cWhite Norm
    TxtY2 := Round(15 * Scale)
    Gui, WaitCard:Add, Text, x0 y+%TxtY2% w%CardW% Center BackgroundTrans vWaitText, 正在连接 GitHub...

    Gui, WaitCard:Show, x%CardX% y%CardY% w%CardW% h%CardH% NoActivate

    WinSet, Region, 0-0 w%CardW% h%CardH% R20-20, ahk_id %hCard%

    OnMessage(0x201, "WM_LBUTTONDOWN")
    OnMessage(0x03, "WM_MOVE")
return

; =======================================================
; 更新界面
; =======================================================
ShowUpdateGui:
    Gui, BGVideoGui:Destroy
    Gui, CardPanel:Destroy
    Scale := GetScale()

    if (IsNewVersion) {
        VideoPath := GetTempPath("update_bg.mp4")
    } else {
        VideoPath := GetTempPath("latest_bg.mp4")
    }

    BtnYellowPath := GetTempPath("btn_yellow.png")

    WinW := Round(952 * Scale)
    WinH := Round(540 * Scale)

    Gui, BGVideoGui:New, +HwndhMainBg -MinimizeBox -MaximizeBox -Caption -DPIScale
    Gui, BGVideoGui:Color, 000000
    Gui, BGVideoGui:Margin, 0, 0

    if (FileExist(VideoPath)) {
        Gui, BGVideoGui:Add, Text, x0 y0 w%WinW% h%WinH% HwndhBGContainer
    }

    Gui, BGVideoGui:Show, w%WinW% h%WinH% Center, Kedit 更新窗口

    if (FileExist(VideoPath)) {
        PlayMpvInGui(hBGContainer, VideoPath)
    }

    WinGetPos, BgX, BgY, , , ahk_id %hMainBg%

    ; --- 上层卡片 ---
    CardW := Round(210 * Scale)
    CardH := Round(210 * Scale)
    MarginRight := Round(30 * Scale)
    MarginBottom := Round(30 * Scale)

    OffsetX := WinW - CardW - MarginRight
    OffsetY := WinH - CardH - MarginBottom

    CardX := BgX + OffsetX
    CardY := BgY + OffsetY

    Gui, CardPanel:New, +Owner%hMainBg% +HwndhCard -Caption +ToolWindow -DPIScale
    Gui, CardPanel:Color, 151515
    Gui, CardPanel:Margin, 0, 0

    Y_StartNew := Round(25 * Scale)
    Y_StartOld := Round(35 * Scale)
    Y_Gap1 := Round(15 * Scale)
    Y_Gap2 := Round(8 * Scale)
    Y_GapBtn := Round(18 * Scale)
    Y_GapLink := Round(12 * Scale)

    if (IsNewVersion) {
        Gui, CardPanel:Font, s14 cWhite Bold, 微软雅黑
        Gui, CardPanel:Add, Text, x0 y%Y_StartNew% w%CardW% Center BackgroundTrans, 发现新版本!
        Gui, CardPanel:Font, s10 cWhite Norm, 微软雅黑
        Gui, CardPanel:Add, Text, x0 y+%Y_Gap1% w%CardW% Center BackgroundTrans, 当前: %CurrentVersion%  →  最新: %LatestVersion%

        Gui, CardPanel:Font, s9 c888888 Norm
        Gui, CardPanel:Add, Text, x0 y+%Y_Gap2% w%CardW% Center BackgroundTrans, 是否立即下载并安装更新？

        BtnW := Round(130 * Scale)
        BtnH := Round(40 * Scale)
        BtnX := (CardW - BtnW) / 2

        Gui, CardPanel:Add, Picture, x%BtnX% y+%Y_GapBtn% w%BtnW% h%BtnH% gUpdateConfirmed vBtnImg BackgroundTrans, %BtnYellowPath%

        BtnTxtOffset := Round(9 * Scale)
        Gui, CardPanel:Font, s11 c222222 Bold
        Gui, CardPanel:Add, Text, xp yp+%BtnTxtOffset% w%BtnW% h25 Center BackgroundTrans gUpdateConfirmed vBtnTxt, 立即更新

        Gui, CardPanel:Font, s9 c666666 Underline
        Gui, CardPanel:Add, Text, x0 y+%Y_GapLink% w%CardW% Center gUpdateCanceled vLinkTxt, 暂不更新

        ProgressY := Round(135 * Scale)
        Gui, CardPanel:Font, s11 cWhite Bold
        Gui, CardPanel:Add, Text, x0 y%ProgressY% w%CardW% r2 Center BackgroundTrans vMyProgressTxt Hidden, 准备开始... 0`%

        ; 按钮区域
        HalfW := Round(CardW / 2)
        Gui, CardPanel:Font, s9 c888888 Underline
        Gui, CardPanel:Add, Text, x0 y+5 w%HalfW% Center BackgroundTrans gLaunchDebugFromUpdate vDebugBtn Hidden, [ 网络诊断 ]
        Gui, CardPanel:Add, Text, x+0 yp w%HalfW% Center BackgroundTrans gUpdateCanceled vCancelDownloadBtn Hidden, [ 取消下载 ]

    } else {
        Gui, CardPanel:Font, s14 cWhite Bold, 微软雅黑
        Gui, CardPanel:Add, Text, x0 y%Y_StartOld% w%CardW% Center BackgroundTrans, 当前已是最新版本

        Gui, CardPanel:Font, s11 cWhite Norm, 微软雅黑
        Gui, CardPanel:Add, Text, x0 y+20 w%CardW% Center BackgroundTrans, 版本号: %CurrentVersion%

        Gui, CardPanel:Font, s9 c888888 Norm
        Gui, CardPanel:Add, Text, x0 y+10 w%CardW% Center BackgroundTrans, 暂无可用更新，请尽情使用。

        BtnW := Round(130 * Scale)
        BtnH := Round(40 * Scale)
        BtnX := (CardW - BtnW) / 2

        Gui, CardPanel:Add, Picture, x%BtnX% y+25 w%BtnW% h%BtnH% gUpdateCanceled vBtnImg BackgroundTrans, %BtnYellowPath%

        BtnTxtOffset := Round(9 * Scale)
        Gui, CardPanel:Font, s11 c222222 Bold
        Gui, CardPanel:Add, Text, xp yp+%BtnTxtOffset% w%BtnW% h25 Center BackgroundTrans gUpdateCanceled vBtnTxt, 关 闭
    }

    Gui, CardPanel:Show, x%CardX% y%CardY% w%CardW% h%CardH% NoActivate

    WinSet, Region, 0-0 w%CardW% h%CardH% R20-20, ahk_id %hCard%
    WinSet, Redraw, , ahk_id %hCard%

    OnMessage(0x201, "WM_LBUTTONDOWN")
    OnMessage(0x03, "WM_MOVE")
return

LaunchDebugFromUpdate:
    GoSub, CloseUpdateGui
    GoSub, LaunchNetworkDebugger
return

; =======================================================
; ★★★ 确认下载 (FileInstall 版) ★★★
; =======================================================
UpdateConfirmed:
    GuiControl, Hide, BtnImg
    GuiControl, Hide, BtnTxt
    GuiControl, Hide, LinkTxt

    GuiControl, Show, MyProgressTxt
    GuiControl, Show, CancelDownloadBtn
    GuiControl, Show, DebugBtn

    GuiControl,, MyProgressTxt, % "正在寻找最佳线路... 0%"

    Global TargetBytes := TargetMB * 1024 * 1024
    UpdateTempFile := A_Temp . "\Update_Temp.exe"
    DownloaderAhk := A_Temp . "\kedit_downloader.ahk"
    StatusFile := A_Temp . "\kedit_down_status.txt"
    InterpreterPath := A_Temp . "\Kedit_Interpreter.exe"

    FileDelete, %DownloaderAhk%
    FileDelete, %StatusFile%
    FileDelete, %UpdateTempFile%

    if (!FileExist(InterpreterPath))
        FileInstall, AutoHotkey.exe, %InterpreterPath%, 1

    ; ★ 核心：释放外部下载脚本 (请确保 Kedit_Downloader_Template.ahk 存在于源码目录)
    FileInstall, Kedit_Downloader_Template.ahk, %DownloaderAhk%, 1

    Run, "%InterpreterPath%" "%DownloaderAhk%"
    SetTimer, MonitorShadowDownload, 100
return

; 监控下载进度
MonitorShadowDownload:
    Gui, CardPanel:Default
    StatusFile := A_Temp . "\kedit_down_status.txt"
    UpdateTempFile := A_Temp . "\Update_Temp.exe"

    FileGetSize, CurrentSize, %UpdateTempFile%
    if (ErrorLevel)
        CurrentSize := 0

    if (TargetMB > 0) {
        LocalTotalBytes := TargetMB * 1024 * 1024
        Percent := Floor((CurrentSize / LocalTotalBytes) * 100)
        if (Percent < 0)
            Percent := 0
        if (Percent > 99)
            Percent := 99
        SizeNow := Round(CurrentSize / 1024 / 1024, 2)

        if (CurrentSize == 0) {
            GuiControl,, MyProgressTxt, 正在下载 (代理缓冲中, 请稍候)...
        } else {
            GuiControl,, MyProgressTxt, 正在下载... %Percent%`%`n(%SizeNow% / %TargetMB% MB)
        }
    } else {
        SizeNow := Round(CurrentSize / 1024 / 1024, 2)
        GuiControl,, MyProgressTxt, 正在下载... 已获取: %SizeNow% MB
    }

    if (FileExist(StatusFile)) {
        SetTimer, MonitorShadowDownload, Off
        FileRead, Status, %StatusFile%
        FileDelete, %A_Temp%\kedit_downloader.ahk
        FileDelete, %StatusFile%

        if (InStr(Status, "success")) {
            FileGetSize, FinalSize, %UpdateTempFile%
            if (FinalSize < 102400) {
                MsgBox, 16, 错误, 下载文件校验失败(文件过小)。
                MsgBox, 调试信息: 正在查找的文件路径是`n%UpdateTempFile%`n当前大小: %FinalSize% 字节
                GoSub, CloseUpdateGui
                return
            }
            GuiControl,, MyProgressTxt, 下载完成 100`%
            Sleep, 1000
            GoSub, ExecuteUpdateBat
        } else {
            SetTimer, MonitorShadowDownload, Off
            MsgBox, 20, 下载失败 - 需要诊断吗?, 尝试了所有线路（含虚拟网卡直连优化）均无法下载。`n`n是否运行【网络连接诊断工具】来获取 Curl 和 WinHttp 的具体报错信息？`n(这将有助于分析是 DNS 问题还是端口拦截)`n
            IfMsgBox, Yes
            {
                GoSub, CloseUpdateGui
                GoSub, LaunchNetworkDebugger
                return
            }
            GoSub, CloseUpdateGui
        }
    }
return

ExecuteUpdateBat:
    UpdateTempFile := A_Temp . "\Update_Temp.exe"
    BatScript =
    (
    @echo off
    timeout /t 1 /nobreak >nul
    :loop
    del "%A_ScriptFullPath%" >nul 2>&1
    if exist "%A_ScriptFullPath%" goto loop
    move "%UpdateTempFile%" "%A_ScriptFullPath%"
    start "" "%A_ScriptFullPath%"
    del "%A_Temp%\updater.bat"
    )

    FileDelete, %A_Temp%\updater.bat
    FileAppend, %BatScript%, %A_Temp%\updater.bat
    Run, %A_Temp%\updater.bat, , Hide
ExitApp
return

UpdateCanceled:
CloseUpdateGui:
UpdateGuiGuiClose:
UpdateGuiGuiEscape:
    SetTimer, MonitorShadowDownload, Off
    Process, Close, kedit_downloader.ahk
    Run, taskkill /F /IM curl.exe,, Hide
    Sleep, 200
    UpdateTempFile := A_Temp . "\Update_Temp.exe"
    if (FileExist(UpdateTempFile)) {
        FileDelete, %UpdateTempFile%
    }
    Gui, CardPanel:Destroy
    Gui, BGVideoGui:Destroy
    Gui, WaitCard:Destroy
    Gui, WaitBg:Destroy
    IsCheckingUpdate := false
return

; =======================================================
; ★★★ 启动诊断工具 (FileInstall 版) ★★★
; =======================================================
LaunchNetworkDebugger:
    DebugAhkPath := A_Temp . "\Kedit_Debug_Tool.ahk"
    InterpreterPath := A_Temp . "\Kedit_Interpreter.exe"

    if (!FileExist(InterpreterPath))
        FileInstall, AutoHotkey.exe, %InterpreterPath%, 1

    FileDelete, %DebugAhkPath%

    ; ★ 核心：释放外部诊断脚本 (请确保 Kedit_Debug_Tool_Template.ahk 存在于源码目录)
    FileInstall, Kedit_Debug_Tool_Template.ahk, %DebugAhkPath%, 1

    Run, "%InterpreterPath%" "%DebugAhkPath%"
return

; [新增代码] =======================================================
; 资源管理器 - 运行 Python 脚本逻辑
; =======================================================
Label_RunPy:
    ; 1. 获取选中文件路径
    Clipboard := ""
    Send, ^c
    ClipWait, 0.5
    if (ErrorLevel)
        return

    FullPath := Clipboard
    SplitPath, FullPath, Name, Dir, Ext, NameNoExt

    ; 2. 安全检查：只允许 .py 文件 (可根据需要注释掉)
    if (Ext != "py" && Ext != "pyw") {
        ShowOSD("非 Python 文件: " . Ext)
        return
    }

    ShowOSD("运行脚本: " . Name)

    ; 3. 打开 CMD 并运行
    ; /k 保持窗口，方便看 print 输出
    Run, %ComSpec% /k title Python Run [%NameNoExt%] && py "%Name%", %Dir%
return

; =======================================================
; Ctrl+Q打开指定路径
; =======================================================

SetPath_QuickOpen:
    ; 弹出文件夹选择框，默认定位到当前设置的路径
    FileSelectFolder, SelectedFolder, *%Path_QuickOpen%, 3, 请选择 Ctrl+Q 要打开的目标文件夹:

    ; 如果用户选了路径（没有点取消）
    if (SelectedFolder != "") {
        Path_QuickOpen := SelectedFolder
        IniWrite, %Path_QuickOpen%, %IniFile%, Settings, QuickPath
        MsgBox, 64, 设置成功, 快捷打开路径已更新为:`n%Path_QuickOpen%
    }
return

; =======================================================
; OSD (On-Screen Display) 操作反馈系统
; =======================================================
ToggleOSD:
    EnableOSD := !EnableOSD
    if (EnableOSD) {
        Menu, Tray, Check, 开启屏幕操作提示 (OSD)
        ShowOSD("屏幕提示已开启")
    } else {
        Menu, Tray, Uncheck, 开启屏幕操作提示 (OSD)
        Gui, CompanionOSD:Destroy
        ShowOSD("屏幕提示已关闭")
    }
    IniWrite, %EnableOSD%, %IniFile%, Settings, EnableOSD
return

ToggleCompanionOSD:
    EnableCompanionOSD := !EnableCompanionOSD
    if (EnableCompanionOSD) {
        Menu, Tray, Check, 开启陪伴表情 (OSD)
        ShowOSD("陪伴表情已开启")
    } else {
        Menu, Tray, Uncheck, 开启陪伴表情 (OSD)
        Gui, CompanionOSD:Destroy
        ShowOSD("陪伴表情已关闭")
    }
    IniWrite, %EnableCompanionOSD%, %IniFile%, Settings, EnableCompanionOSD
return

SetCompanionChance15:
    SetCompanionChance(15)
return

SetCompanionChance25:
    SetCompanionChance(25)
return

SetCompanionChance50:
    SetCompanionChance(50)
return

SetCompanionChance100:
    SetCompanionChance(100)
return

PreviewCompanionSuccess:
    ShowCompanionOSD(GetTempPath("companion_success.png"), 2500)
return

PreviewCompanionQuestion:
    ShowCompanionOSD(GetTempPath("companion_question.png"), 2500)
return

PreviewCompanionBusy:
    ShowCompanionOSD(GetTempPath("companion_busy.png"), 2500)
return

HideCompanionOSD:
    Gui, CompanionOSD:Destroy
return

SetCompanionChance(NewChance) {
    global CompanionChance, IniFile
    CompanionChance := NewChance
    IniWrite, %CompanionChance%, %IniFile%, Settings, CompanionChance
    UpdateCompanionChanceMenu()
    ShowOSD("表情出现频率: " . CompanionChance . "%")
}

UpdateCompanionChanceMenu() {
    global CompanionChance
    Menu, CompanionChanceMenu, Uncheck, 低频（15％）
    Menu, CompanionChanceMenu, Uncheck, 标准（25％）
    Menu, CompanionChanceMenu, Uncheck, 较多（50％）
    Menu, CompanionChanceMenu, Uncheck, 每次（100％）

    if (CompanionChance = 15)
        Menu, CompanionChanceMenu, Check, 低频（15％）
    else if (CompanionChance = 50)
        Menu, CompanionChanceMenu, Check, 较多（50％）
    else if (CompanionChance = 100)
        Menu, CompanionChanceMenu, Check, 每次（100％）
    else
        Menu, CompanionChanceMenu, Check, 标准（25％）
}

; =======================================================
; OSD (On-Screen Display) 操作反馈系统 (支持自定义时长)
; =======================================================
ShowOSD(Text, DisplayTime := 1200) {  ; ★ 新增了 DisplayTime 参数，默认 1200ms
    Global EnableOSD
    if (!EnableOSD)
        return

    MaybeShowCompanion(Text, DisplayTime)

    Gui, OSD:Destroy
    Gui, OSD:New, +AlwaysOnTop +ToolWindow -Caption +HwndhOSD +E0x20
    Gui, OSD:Color, 1F1F1F
    Gui, OSD:Font, s11 cWhite Bold, 微软雅黑

    TextW := StrLen(Text) * 10 + 40
    if (TextW < 140)
        TextW := 140

    Gui, OSD:Add, Text, x0 y9 w%TextW% Center BackgroundTrans, %Text%
    WinSet, Transparent, 150, ahk_id %hOSD%
    WinSet, Region, 0-0 w%TextW% h40 R10-10, ahk_id %hOSD%
    SysGet, Sw, 0
    SysGet, Sh, 1
    ; PosX: 距离屏幕左边缘 50 像素
    PosX := 50
    ; PosY: 距离屏幕顶端 88% 的位置 (即底部偏上一点)
    PosY := Sh * 0.88

    Gui, OSD:Show, NoActivate x%PosX% y%PosY% w%TextW% h40

    ; ★ 核心逻辑：如果传入的时间大于 0，才启动消失倒计时
    ; 如果传入 0，OSD 就会一直悬浮在屏幕上，直到下一次调用 ShowOSD
    SetTimer, FadeOutOSD, Off
    if (DisplayTime > 0) {
        SetTimer, FadeOutOSD, -%DisplayTime%
    }
    return

    FadeOutOSD:
    Gui, OSD:Destroy
    Gui, CompanionOSD:Destroy
    return
}

MaybeShowCompanion(Text, DisplayTime) {
    global EnableCompanionOSD, CompanionChance, CompanionCooldown, LastCompanionTick

    ; 新提示到来时先清除旧表情，避免“处理中”的常驻图片残留。
    Gui, CompanionOSD:Destroy
    SetTimer, HideCompanionOSD, Off

    if (!EnableCompanionOSD)
        return

    IsPriority := false
    if (InStr(Text, "Error") || InStr(Text, "错误") || InStr(Text, "未选中")
        || InStr(Text, "无需") || InStr(Text, "不存在") || InStr(Text, "超时")
        || InStr(Text, "失败")) {
        CompanionFile := "companion_question.png"
        IsPriority := true
    } else if (InStr(Text, "正在") || InStr(Text, "稍候") || InStr(Text, "⏳")) {
        CompanionFile := "companion_busy.png"
        IsPriority := true
    } else {
        CompanionFile := "companion_success.png"
    }

    if (!IsPriority) {
        if (CompanionChance <= 0)
            return

        CooldownMs := CompanionCooldown * 1000
        if (LastCompanionTick > 0 && A_TickCount - LastCompanionTick < CooldownMs)
            return

        Random, CompanionRoll, 1, 100
        if (CompanionRoll > CompanionChance)
            return
    }

    CompanionPath := GetTempPath(CompanionFile)
    if (!FileExist(CompanionPath))
        return

    LastCompanionTick := A_TickCount
    ; 使用文字 OSD 的同一个 DisplayTime，并由文字 OSD 的淡出定时器统一关闭。
    ShowCompanionOSD(CompanionPath, DisplayTime, true)
}

ShowCompanionOSD(ImagePath, DisplayTime, SyncWithText := false) {
    ImageSize := 180
    Gui, CompanionOSD:Destroy
    Gui, CompanionOSD:New, +AlwaysOnTop +ToolWindow -Caption +HwndhCompanionOSD +E0x20
    Gui, CompanionOSD:Margin, 0, 0
    Gui, CompanionOSD:Color, 010203
    Gui, CompanionOSD:Add, Picture, x0 y0 w%ImageSize% h%ImageSize% BackgroundTrans, %ImagePath%

    SysGet, CompanionScreenW, 0
    SysGet, CompanionScreenH, 1
    CompanionX := 45
    CompanionY := Round(CompanionScreenH * 0.88) - ImageSize - 8
    if (CompanionY < 0)
        CompanionY := 0

    Gui, CompanionOSD:Show, NoActivate x%CompanionX% y%CompanionY% w%ImageSize% h%ImageSize%
    WinSet, TransColor, 010203 255, ahk_id %hCompanionOSD%

    SetTimer, HideCompanionOSD, Off
    ; 托盘手动预览使用独立定时器；自动表情由文字 OSD 定时器同步关闭。
    if (!SyncWithText && DisplayTime > 0)
        SetTimer, HideCompanionOSD, -%DisplayTime%
}

; =======================================================
; “关于”窗口 UI 实现 (修复标签报错 + 系统阴影 + 保持自定义参数)
; =======================================================
ShowAboutGui:
    Gui, AboutGui:Destroy
    Scale := GetScale()

    ; --- 1. 基础尺寸预计算 (完全保留你的自定义参数) ---
    vWinW      := Round(400 * Scale)    ;宽度
    vWinH      := Round(280 * Scale)    ;总高度
    vTopH      := Round(180 * Scale)    ;视频区域高度
    vIconSize  := Round(60 * Scale)     ;头像尺寸

    vMarginX   := Round(15 * Scale)     ;
    vIconY     := vTopH + Round(20 * Scale) ;头像下移

    vTextX     := vMarginX + vIconSize + Round(15 * Scale)
    vTextY     := vTopH + Round(25 * Scale)
    vLinkY     := vTextY + Round(20 * Scale)

    vVerY      := vWinH - Round(25 * Scale)

    vBtnW      := Round(60 * Scale)
    vBtnX      := vWinW - vMarginX - vBtnW
    vBtnY      := vVerY

    ; --- 2. 创建窗口 ---
    Gui, AboutGui:New, +HwndhAboutGui -Caption +ToolWindow -DPIScale
    Gui, AboutGui:Color, White

    ; ★ 增加系统级阴影 ★
    DllCall("SetClassLong", "Ptr", hAboutGui, "Int", -26, "Ptr", DllCall("GetClassLong", "Ptr", hAboutGui, "Int", -26) | 0x20000)

    ; 上半部分：MPV 容器
    Gui, AboutGui:Add, Text, x0 y0 w%vWinW% h%vTopH% HwndhAboutVideoContainer

    ; --- 3. 圆形 Logo 区域 ---
    vLogoPath := GetTempPath("logo.png")
    if (FileExist(vLogoPath)) {
        Gui, AboutGui:Add, Picture, x%vMarginX% y%vIconY% w%vIconSize% h%vIconSize% HwndhLogo BackgroundTrans, %vLogoPath%
    } else {
        Gui, AboutGui:Add, Progress, x%vMarginX% y%vIconY% w%vIconSize% h%vIconSize% Background0088EE HwndhLogo Disabled, 0
    }

    WinSet, Region, % "0-0 w" vIconSize " h" vIconSize " E", ahk_id %hLogo%

    ; --- 4. 文本信息 ---
    Gui, AboutGui:Font, s9 c000000 Norm, 微软雅黑
    Gui, AboutGui:Add, Text, x%vTextX% y%vTextY% BackgroundTrans, 项目链接:

    Gui, AboutGui:Font, s9 c0055AA Bold Underline
    vLinkW := vWinW - vTextX - vMarginX
    ; ★ 修复核心：将 gOpenGitHub 放在选项最后，并确保与前一个选项有空格，防止解析错误 ★
    Gui, AboutGui:Add, Text, x%vTextX% y%vLinkY% w%vLinkW% h18 BackgroundTrans gOpenGitHub, https://github.com/Dengjiancong/Kedit_GO_to_Def

    ; --- 5. 底部状态行 ---
    Gui, AboutGui:Font, s8 c888888 Norm, 微软雅黑
    Gui, AboutGui:Add, Text, % "x0 y" vVerY " w" vWinW " Center BackgroundTrans", % "Version " . CurrentVersion

    ; 关闭按钮 (同样将 g 标签放在最后)
    Gui, AboutGui:Add, Text, x%vBtnX% y%vBtnY% w%vBtnW% Right BackgroundTrans gAboutGuiGuiClose, [ 关闭 ]

    ; --- 6. 最终显示与效果 ---
    Gui, AboutGui:Show, w%vWinW% h%vWinH% Center, 关于 Kedit 助手
    WinSet, Region, % "0-0 w" vWinW " h" vWinH " R20-20", ahk_id %hAboutGui%

    vAboutVideoPath := GetTempPath("side.mp4")
    if (FileExist(vAboutVideoPath)) {
        PlayMpvInGui(hAboutVideoContainer, vAboutVideoPath)
    }

    OnMessage(0x201, "WM_LBUTTONDOWN")
return

; ★ 确保这两个标签存在且在 ShowAboutGui 函数体之外 ★
OpenGitHub:
    Gosub, KillAllMpv
    Gui, AboutGui:Destroy
    Run, https://github.com/Dengjiancong/Kedit_GO_to_Def
return

AboutGuiGuiEscape:
AboutGuiGuiClose:
    Gosub, KillAllMpv
    Gui, AboutGui:Destroy
return
