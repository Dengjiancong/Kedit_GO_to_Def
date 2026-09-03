; =======================================================
; Ctrl 幽灵侦测器 (防闪烁 + 一键复制版)
; =======================================================
#SingleInstance Force
#Persistent
SetBatchLines, -1

; --- UI 界面 ---
Gui, +AlwaysOnTop +ToolWindow
Gui, Font, s11 Bold, 微软雅黑
Gui, Add, Text, vStatusText w400 h70 cBlue, 正在初始化监控引擎...

Gui, Font, s9 Norm, Consolas
Gui, Add, ListView, r15 w400 vLogView, 时间戳|事件类别|详情
LV_ModifyCol(1, 100)
LV_ModifyCol(2, 90)
LV_ModifyCol(3, 180)

Gui, Font, s10, 微软雅黑
; 重新排版底部按钮，加入一键复制
Gui, Add, Button, gClearLog w80 h30, 清空日志
Gui, Add, Button, x+10 gCopyLog w80 h30, 复制日志
Gui, Add, Button, x+10 gForceReset w180 h30, 🚑 强行发送释放信号
Gui, Show, x50 y50, Ctrl 幽灵侦测器

global Prev_LP := -1, Prev_LL := -1, Prev_RP := -1, Prev_RL := -1
global Prev_StatStr := ""
global Prev_IsGhost := -1

; 启动 10ms 级高频扫描
SetTimer, MonitorKeys, 10
return

MonitorKeys:
    ; 获取左 Ctrl 的物理(P)与逻辑状态
    LP := GetKeyState("LCtrl", "P")
    LL := GetKeyState("LCtrl")
    
    ; 获取右 Ctrl 的物理(P)与逻辑状态
    RP := GetKeyState("RCtrl", "P")
    RL := GetKeyState("RCtrl")
    
    ; --- 状态变化日志记录 ---
    if (LP != Prev_LP && Prev_LP != -1)
        AddLog(LP ? "▼ 物理按下" : "▲ 物理松开", "左 Ctrl")
    if (LL != Prev_LL && Prev_LL != -1)
        AddLog(LL ? "▽ 逻辑按下" : "△ 逻辑松开", "左 Ctrl")
    if (RP != Prev_RP && Prev_RP != -1)
        AddLog(RP ? "▼ 物理按下" : "▲ 物理松开", "右 Ctrl")
    if (RL != Prev_RL && Prev_RL != -1)
        AddLog(RL ? "▽ 逻辑按下" : "△ 逻辑松开", "右 Ctrl")
    
    Prev_LP := LP, Prev_LL := LL, Prev_RP := RP, Prev_RL := RL
    
    ; --- 实时状态面板刷新 (防闪烁核心逻辑) ---
    StatStr := "【左 Ctrl】 物理状态: " (LP?"按下":"松开") "    逻辑状态: " (LL?"按下":"松开") "`n"
    StatStr .= "【右 Ctrl】 物理状态: " (RP?"按下":"松开") "    逻辑状态: " (RL?"按下":"松开") "`n"
    
    IsGhost := ((LL && !LP) || (RL && !RP))
    
    if (IsGhost) {
        StatStr .= "⚠️ 警告：幽灵死锁！系统认为你按着，但你手已经松了！"
    } else {
        StatStr .= "✅ 状态正常同步"
    }

    if (StatStr != Prev_StatStr) {
        GuiControl,, StatusText, %StatStr%
        Prev_StatStr := StatStr
    }

    if (IsGhost != Prev_IsGhost) {
        if (IsGhost)
            Gui, Color, FFB3B3  ; 窗口变红警告
        else
            Gui, Color, Default ; 恢复默认颜色
        Prev_IsGhost := IsGhost
    }
return

AddLog(Event, Detail) {
    FormatTime, TimeString, %A_Now%, HH:mm:ss
    TimeString .= "." . SubStr(A_TickCount, -3)
    LV_Insert(1, "", TimeString, Event, Detail)
    if (LV_GetCount() > 50)
        LV_Delete(51)
}

; =======================================================
; 新增：一键完整复制日志
; =======================================================
CopyLog:
    ; 1. 先抓取当前顶部的状态栏信息
    LogData := "====== 侦测器状态快照 ======`n"
    LogData .= Prev_StatStr . "`n"
    LogData .= "====== 时序监控日志 ======`n"
    
    ; 2. 遍历 ListView 抓取历史数据
    Loop % LV_GetCount()
    {
        LV_GetText(Col1, A_Index, 1)
        LV_GetText(Col2, A_Index, 2)
        LV_GetText(Col3, A_Index, 3)
        LogData .= Col1 . "  " . Col2 . "  " . Col3 . "`n"
    }
    
    ; 3. 强行写入剪贴板 (绕过键盘 Ctrl+C)
    Clipboard := LogData
    
    ; 4. 视觉反馈
    MsgBox, 64, 复制成功, 状态快照与所有日志已存入系统剪贴板！`n`n您可以直接用鼠标右键粘贴发给我了。
return

ClearLog:
    LV_Delete()
return

ForceReset:
    SendInput, {Blind}{LCtrl Up}{RCtrl Up}
    AddLog("★ 手动抢救", "发射 {Ctrl Up} 洗白指令")
return

GuiClose:
    ExitApp