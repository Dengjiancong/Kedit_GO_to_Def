param([switch]$UI)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dir=Join-Path $root '.pet-test\f1-migration'
New-Item -ItemType Directory -Force $dir | Out-Null
$source=[IO.File]::ReadAllText((Join-Path $root 'Kedit_GO_to_Def_ver18.30.ahk'))
$start=$source.IndexOf('NormalizeConsoleHotkey(Key) {')
$end=$source.IndexOf('ReceiveConsoleCommand(wParam,',$start)
$functions=$source.Substring($start,$end-$start)
$harness=@'
#NoEnv
#SingleInstance Off
SetWorkingDir, %A_ScriptDir%
IniFile := A_ScriptDir . "\fixture.ini"
FileDelete, %IniFile%
IniWrite, keep, %IniFile%, Other, Untouched
Key_FindClipboard := "F1"
Key_GoToDef := "^b"
Key_CtrlQ := "^q"
HotkeysSuspended := false
FindClipboardMenu := "F1 fixture"
Menu, Tray, Add, %FindClipboardMenu%, Label_FindClipboard
Hotkey, IfWinActive, ahk_exe kedit.exe
Hotkey, F1, Label_FindClipboard, On
Check(SaveFindClipboardFromConsole("^F8") = 1, "valid save")
IniRead, Actual, %IniFile%, Hotkeys, FindClipboard
Check(Actual = "^F8", "persisted")
IniRead, Actual, %IniFile%, Other, Untouched
Check(Actual = "keep", "preserve unrelated fields")
Check(SaveFindClipboardFromConsole("~^b") = 3, "scope conflict including pass-through")
Check(SaveFindClipboardFromConsole("Pause") = 3, "reserved key")
Check(SaveFindClipboardFromConsole("not_a_key") = 2, "invalid key")
Check(SaveFindClipboardFromConsole("") = 2, "empty key")
Check(Key_FindClipboard = "^F8", "failures preserve active value")
HotkeysSuspended := true
Check(SaveFindClipboardFromConsole("+F9") = 1, "save while suspended")
OriginalIni := IniFile
IniFile := A_ScriptDir . "\missing\fixture.ini"
Check(SaveFindClipboardFromConsole("F10") = 4, "write failure")
Check(Key_FindClipboard = "+F9", "write failure preserves active value")
IniFile := OriginalIni
FileAppend, PASS: F1 transaction and validation`n, result.txt
ExitApp
Label_FindClipboard:
return
Check(Condition, Name) {
    if (!Condition) {
        FileAppend, FAIL: %Name%`n, result.txt
        ExitApp, 1
    }
}
'@
$test=Join-Path $dir 'fixture.ahk'
[IO.File]::WriteAllText($test,$harness+"`r`n"+$functions,(New-Object Text.UTF8Encoding($true)))
$result=Join-Path $dir 'result.txt'
if(Test-Path $result){Remove-Item -LiteralPath $result}
$p=Start-Process 'C:\Program Files\AutoHotkey\AutoHotkey.exe' -ArgumentList @('/ErrorStdOut',('"'+$test+'"')) -WindowStyle Hidden -Wait -PassThru
if($p.ExitCode -ne 0 -or !(Test-Path $result)){throw 'AHK fixture failed'}
Get-Content $result
if($UI){
    $prefix=$harness.Substring(0,$harness.IndexOf('Check(SaveFindClipboard'))
    $receiverStart=$source.IndexOf('ReceiveConsoleCommand(wParam,')
    $receiverEnd=$source.IndexOf('PollConsoleCommand:',$receiverStart)
    $receiver=$source.Substring($receiverStart,$receiverEnd-$receiverStart)
    $server=@'
OnMessage(0x4A, "ReceiveConsoleCommand")
NumericHwnd := A_ScriptHwnd + 0
FileAppend, %NumericHwnd%, server-hwnd.txt
SetTimer, StopFixture, -30000
return
StopFixture:
ExitApp
Label_FindClipboard:
AutoCheckForUpdateInitial:
AutoCheckForUpdate:
return
AppendConsoleCommandLog(Message) {
}
UpdateHotkeys() {
}
'@
    $serverPath=Join-Path $dir 'server.ahk'
    [IO.File]::WriteAllText($serverPath,$prefix+$server+"`r`n"+$functions+$receiver,(New-Object Text.UTF8Encoding($true)))
    $hwndPath=Join-Path $dir 'server-hwnd.txt'
    if(Test-Path $hwndPath){Remove-Item -LiteralPath $hwndPath}
    $fixture=Start-Process 'C:\Program Files\AutoHotkey\AutoHotkey.exe' -ArgumentList @('/ErrorStdOut',('"'+$serverPath+'"')) -WindowStyle Hidden -PassThru
    try {
        for($i=0;$i -lt 30 -and !(Test-Path $hwndPath);$i++){Start-Sleep -Milliseconds 100}
        $handle=Get-Content $hwndPath
        $output=Join-Path $dir 'ui'
        $arguments='side.mp4 '+$handle+' --settings "'+$dir+'\fixture.ini" --data-dir "'+$output+'" --self-test-f1 --find-clipboard'
        $report=Join-Path $output 'result.txt'
        if(Test-Path $report){Remove-Item -LiteralPath $report}
        $uiProcess=Start-Process (Join-Path $root 'Kedit.Console\bin\MigrationF1\Kedit.Console.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if(!$uiProcess.WaitForExit(20000)){$uiProcess.Kill();throw 'UI checks timed out'}
        $message=Get-Content $report
        if($message -notlike 'PASS:*'){throw ($message -join "`n")}
        $message
    } finally {if(!$fixture.HasExited){$fixture.Kill()}}
}
