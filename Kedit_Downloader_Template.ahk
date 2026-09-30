#NoTrayIcon
#SingleInstance Force
#NoEnv
SetBatchLines -1

; === 配置区域 ===
; 注意：这里我们让下载器自己计算路径，不再依赖主程序传参
; 由主程序传入当前 Gitea Release 的附件地址。
; 保留固定地址作为兼容回退，避免旧版主程序调用时没有参数。
ExeURL := A_Args.Length() >= 1 ? A_Args[1] : "https://gitea.evadd.xyz:88/EVADD/Kedit_GO_to_Def/releases/latest/download/Kedit_GO_to_Def.exe"
; Update_Temp.exe 保存到主程序目录（父进程目录）
UpdateTempFile := A_ScriptDir . "\Update_Temp.exe" 
; 状态文件放在 Temp
StatusFile := A_Temp . "\kedit_down_status.txt"

FileDelete, %StatusFile%

Modes := []
; 1. 优先尝试明确的本地代理端口
Modes.Push("127.0.0.1:7890")
Modes.Push("127.0.0.1:7897")
Modes.Push("127.0.0.1:10809")
; 2. 尝试系统默认 (WinHttp)
Modes.Push(0)
; 3. 最后尝试直连
Modes.Push("DIRECT")

IsSuccess := 0

for index, Mode in Modes {
    ; 通用参数: 忽略证书, 连接超时3秒, 速度限制防止死锁
    CommonParams := " -k -L -f --connect-timeout 3 --speed-time 3 --speed-limit 1 --max-time 60 "
    
    if (Mode = "DIRECT") {
         RunWait, curl.exe %CommonParams% --noproxy "*" -o "%UpdateTempFile%" "%ExeURL%", , Hide UseErrorLevel
    } else if (Mode != 0) {
         RunWait, curl.exe %CommonParams% -x "http://%Mode%" -o "%UpdateTempFile%" "%ExeURL%", , Hide UseErrorLevel
    } else {
         RunWait, curl.exe %CommonParams% -o "%UpdateTempFile%" "%ExeURL%", , Hide UseErrorLevel
    }
    
    if (ErrorLevel = 0) {
         FileGetSize, outSz, %UpdateTempFile%
         if (outSz > 1024) {
             FileAppend, success, %StatusFile%
             IsSuccess := 1
             break
         }
    }
    
    ; WinHttp 备用方案
    try {
        whr := ComObjCreate("WinHttp.WinHttpRequest.5.1")
        whr.Open("GET", ExeURL, true)
        whr.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
        whr.Option(9) := 2048 
        whr.Option(6) := 1    
        if (Mode = "DIRECT")
            whr.SetProxy(1) 
        else if (Mode = 0)
            whr.SetProxy(0) 
        else
            whr.SetProxy(2, Mode)
        
        whr.Send()
        whr.WaitForResponse(30)
        if (whr.Status == 200) {
            ado := ComObjCreate("ADODB.Stream")
            ado.Type := 1
            ado.Open()
            ado.Write(whr.ResponseBody)
            ado.SaveToFile(UpdateTempFile, 2)
            ado.Close()
            FileAppend, success, %StatusFile%
            IsSuccess := 1
            break
        }
    } catch {
        continue
    }
}

if (!IsSuccess)
    FileAppend, error, %StatusFile%
ExitApp
