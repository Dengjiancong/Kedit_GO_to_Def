/*
    Kedit 网络连接调试工具
    功能：分析 WinHttp 组件为何无法连接 GitHub
*/
#SingleInstance Force
#NoEnv
SetBatchLines -1

; 定义要测试的目标 URL (从你的源代码中提取)
TargetURL := "https://raw.githubusercontent.com/Dengjiancong/Kedit_GO_to_Def/main/version.txt"

Gui, Add, Text, x10 y10 w400, 点击下方按钮开始测试网络连接状况...
Gui, Add, Button, x10 y35 w120 h30 gStartTest, 开始网络诊断
Gui, Add, Button, x140 y35 w120 h30 gCopyLog, 复制日志
Gui, Add, Edit, x10 y75 w580 h400 vLogOutput ReadOnly, 等待测试...
Gui, Show, w600 h500, Kedit 网络连接调试器
return

GuiClose:
ExitApp

CopyLog:
    GuiControlGet, content,, LogOutput
    Clipboard := content
    MsgBox, 64, 提示, 日志已复制到剪贴板。
return

StartTest:
    Log("=== 开始诊断 ===")
    Log("系统时间: " . A_YYYY . "-" . A_MM . "-" . A_DD . " " . A_Hour . ":" . A_Min . ":" . A_Sec)
    Log("测试目标 URL: " . TargetURL)
    
    ; 1. 基础连通性测试 (百度) - 验证本地网络是否正常
    Log("`r`n[测试 1] 访问 www.baidu.com (验证本机是否有网)")
    TestConnection("https://www.baidu.com", 0)

    ; 2. 原始代码逻辑测试 (不设代理)
    Log("`r`n[测试 2] 模拟 Kedit 原始逻辑 (直连/系统默认)")
    TestConnection(TargetURL, "Default")

    ; 3. 强制无代理测试
    Log("`r`n[测试 3] 强制直连 (Proxy = 1, No Proxy)")
    TestConnection(TargetURL, "NoProxy")

    ; 4. 尝试检测本地常见代理端口 (如果你开了梯子)
    Log("`r`n[测试 4] 尝试常见本地代理端口 (127.0.0.1:7890)")
    TestConnection(TargetURL, "127.0.0.1:7890")

    Log("`r`n=== 诊断结束 ===")
return

TestConnection(url, proxyMode) {
    Log(">>> 正在连接: " . url)
    
    try {
        whr := ComObjCreate("WinHttp.WinHttpRequest.5.1")
        whr.Open("GET", url, true)
        
        ; 模拟你的 User-Agent 设置
        whr.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
        
        ; 确保 TLS 1.2/1.3 开启 (关键)
        ; 2048 (TLS 1.2) + 128 (TLS 1.1) + 512 (TLS 1.3 experimental in some versions)
        whr.Option(9) := 2048 
        whr.Option(6) := 1 ; Enable Redirects
        
        ; 代理设置逻辑
        if (proxyMode = "Default") {
            Log("    模式: 默认 (SetProxy(0))")
            whr.SetProxy(0) 
        } else if (proxyMode = "NoProxy") {
            Log("    模式: 强制直连 (SetProxy(1))")
            whr.SetProxy(1)
        } else {
            Log("    模式: 指定代理 (" . proxyMode . ")")
            whr.SetProxy(2, proxyMode)
        }

        whr.Send()
        
        ; 等待响应
        Log("    正在等待响应...")
        whr.WaitForResponse(10) ; 10秒超时
        
        status := whr.Status
        Log("    HTTP 状态码: " . status)
        
        if (status == 200) {
            Log("    [成功] 连接成功！内容长度: " . StrLen(whr.ResponseText))
            ; 打印前50个字符确认内容
            Log("    内容预览: " . SubStr(whr.ResponseText, 1, 50) . "...")
        } else {
            Log("    [失败] 服务器返回非 200 状态。")
        }

    } catch e {
        ; 捕获具体的 COM 错误代码
        Log("    [严重错误] WinHttp 组件报错:")
        Log("    错误信息: " . e.Message)
        Log("    错误代码: " . Format("0x{:X}", e.Exception.Number))
        Log("    具体描述: " . e.Extra)
        
        AnalyzeError(e.Message, e.Exception.Number)
    }
}

AnalyzeError(msg, code) {
    if (InStr(msg, "0x80072EE7"))
        Log("    -> 分析: DNS 解析失败。找不到主机名。可能被 DNS 污染或断网。")
    else if (InStr(msg, "0x80072EFD"))
        Log("    -> 分析: 连接被拒绝。服务器可能挂了或者防火墙阻止了连接。")
    else if (InStr(msg, "0x80072EE2"))
        Log("    -> 分析: 连接超时。网络太慢或被丢包。")
    else if (InStr(msg, "0x80072F7D"))
        Log("    -> 分析: SSL/TLS 握手失败。可能是证书问题或协议版本不匹配。")
    else if (InStr(msg, "0x80090302"))
        Log("    -> 分析: 证书不可信。可能存在中间人攻击或系统根证书缺失。")
}

Log(text) {
    Global LogOutput
    GuiControlGet, currentLog,, LogOutput
    GuiControl,, LogOutput, %currentLog%%text%`r`n
    SendMessage, 0x0115, 7, 0, Edit1, Kedit 网络连接调试器 ; 滚动到底部
}