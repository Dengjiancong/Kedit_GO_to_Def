#NoTrayIcon
#SingleInstance Force
#NoEnv
SetBatchLines -1

TargetURL := "https://github.com/Dengjiancong/Kedit_GO_to_Def/releases/latest/download/Kedit_GO_to_Def.exe"

Gui, Add, Text, x10 y10 w580, 点击 [开始深度诊断] ，脚本将模拟下载过程并捕获错误代码。
Gui, Add, Button, x10 y35 w120 h30 gStartTest, 开始深度诊断
Gui, Add, Button, x140 y35 w120 h30 gCopyLog, 复制完整日志
Gui, Add, Edit, x10 y75 w580 h400 vLogOutput ReadOnly
Gui, Show, w600 h500, Kedit 网络连接诊断器
GuiControl,, LogOutput, 准备就绪... 请点击开始。
return

GuiClose:
ExitApp

CopyLog:
    GuiControlGet, content,, LogOutput
    Clipboard := content
    MsgBox, 64, 提示, 日志已复制到剪贴板。
return

StartTest:
    GuiControl,, LogOutput, [正在初始化诊断...]
    Log("=== Kedit 网络环境深度诊断 (外置封装版) ===")
    Log("时间: " . A_YYYY . "-" . A_MM . "-" . A_DD . " " . A_Hour . ":" . A_Min . ":" . A_Sec)
    
    Log("`r`n[Step 1] 环境变量与 Curl 基础")
    EnvGet, hProxy, HTTP_PROXY
    if (hProxy != "")
        Log("[警告] 存在 HTTP_PROXY: " . hProxy)
    else
        Log("[正常] 无全局代理变量。")

    Log("`r`n[Step 2] 模拟真实下载行为")
    Log(">>> 测试 A: 强制直连 (DIRECT) + 忽略 SSL")
    
    ; 这里的双引号可以直接写，因为这是独立文件，不用怕主程序转义
    W_Param := " -w ""`r`n最终地址: %{url_effective}`r`n状态码: %{http_code}"""
    CurlCmd := "curl.exe -I -k -L --connect-timeout 5 --speed-time 5 --speed-limit 1 --noproxy ""*"" " . W_Param . " " . TargetURL . " 2>&1"
    
    Log("执行 Curl 测试...")
    OutputA := RunWaitOne(CurlCmd)
    Log("结果片段:`r`n" . SubStr(OutputA, -300))
    
    if (InStr(OutputA, "objects.githubusercontent.com"))
        Log("[分析] √ 重定向成功，已获取到 S3 下载链接。")
    else
        Log("[分析] × 未能获取最终下载链接。")

    Log("`r`n>>> 测试 B: 本地代理 (7890) + 忽略 SSL")
    CurlCmdB := "curl.exe -I -k -L --connect-timeout 5 -x http://127.0.0.1:7890 " . TargetURL . " 2>&1"
    OutputB := RunWaitOne(CurlCmdB)
    
    if (InStr(OutputB, "200 OK") || InStr(OutputB, "302 Found"))
        Log("[结论 B] √ 代理连接正常。")
    else
        Log("[结论 B] × 代理连接无响应。")

    Log("`r`n[Step 3] WinHttp 组件测试 (修复 UA)")
    TestWinHttp(TargetURL, 1, "WinHttp (修复User-Agent后)")
    Log("`r`n=== 诊断结束 ===")
return

RunWaitOne(command) {
    shell := ComObjCreate("WScript.Shell")
    exec := shell.Exec(ComSpec " /C " command)
    return exec.StdOut.ReadAll()
}

TestWinHttp(url, proxyMode, Desc) {
    Log(">>> 测试: " . Desc)
    try {
        whr := ComObjCreate("WinHttp.WinHttpRequest.5.1")
        whr.Open("HEAD", url, true)
        whr.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
        whr.Option(9) := 2048 
        whr.Option(6) := 1 
        if (proxyMode = 1)
            whr.SetProxy(1)
        else
            whr.SetProxy(0)
        whr.Send()
        whr.WaitForResponse(5)
        status := whr.Status
        Log("    状态码: " . status)
        if (status == 200)
            Log("    [结论] √ 修复 UA 后 WinHttp 连接成功！")
        else if (status == 404)
            Log("    [结论] × 仍然 404，说明 IP 被 GitHub 封禁。")
    } catch e {
        Log("    [异常] " . e.Message)
    }
}

Log(text) {
    GuiControlGet, currentLog,, LogOutput
    NewText := currentLog . text . Chr(13) . Chr(10)
    GuiControl,, LogOutput, %NewText%
    SendMessage, 0x0115, 7, 0, Edit1, Kedit 网络连接诊断器
}