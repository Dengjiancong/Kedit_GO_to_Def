param([switch]$UI,[string]$PetModel)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dir=Join-Path $root '.pet-test\f1-migration'
New-Item -ItemType Directory -Force $dir | Out-Null
$source=[IO.File]::ReadAllText((Join-Path $root 'Kedit_GO_to_Def_ver18.30.ahk'))
$start=$source.IndexOf('AddKeditMenu(Name, Caption) {')
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

for Name, Value in {ShiftF2:"XButton2", AltF:"!f", CtrlW:"^w", AltA:"!a", ColumnInsert:"!i", ToggleComment:"^/", SpacesToTabs:"^\", SmartClick:"~MButton"} {
    Key_%Name% := Value
    Action := Name = "ToggleComment" ? "ProcessCommentToggle" : "Label_" . Name
    Hotkey, %Value%, %Action%, On
}
Hotkey, ^b, Label_GoToDef, On
VSDefinitionAction := "GoTo"
Hotkey, IfWinActive, ahk_exe devenv.exe
Key_VS_Peek := "MButton"
Hotkey, MButton, Label_VS_DefinitionAction, On
AddKeditMenu("VS_Peek","VS_Peek")
Key_VS_Back := "^b"
Hotkey, ^b, Label_VS_NavigateBack, On
AddKeditMenu("VS_Back","VS_Back")
Key_VS_Build := "F7"
Hotkey, F7, Label_VS_SendCtrlB, On
AddKeditMenu("VS_Build","VS_Build")
Key_VS_ToggleComment := "^/"
Hotkey, ^/, Label_VS_ToggleComment, On
AddKeditMenu("VS_ToggleComment","VS_ToggleComment")
Key_VS_BookmarkToggle := "^F2"
Hotkey, ^F2, Label_VS_BookmarkToggle, On
AddKeditMenu("VS_BookmarkToggle","VS_BookmarkToggle")
Key_VS_BookmarkNext := "F2"
Hotkey, F2, Label_VS_BookmarkNext, On
AddKeditMenu("VS_BookmarkNext","VS_BookmarkNext")
Key_VS_BookmarkPrevious := "+F2"
Hotkey, +F2, Label_VS_BookmarkPrevious, On
AddKeditMenu("VS_BookmarkPrevious","VS_BookmarkPrevious")
Key_VS_Redo := "^y"
Hotkey, ^y, Label_VS_Redo, On
AddKeditMenu("VS_Redo","VS_Redo")
Hotkey, IfWinActive
Hotkey, $MButton, Label_VS_DefinitionAction, On

for _, Name in ["FindClipboard", "GoToDef", "ShiftF2", "AltF", "CtrlW", "AltA", "ColumnInsert", "ToggleComment", "SpacesToTabs", "SmartClick"]
    AddKeditMenu(Name, Name)

Check(SaveFindClipboardFromConsole("^F8") = 1, "valid save")
Check(NormalizeConsoleHotkey("<!vkBF") = NormalizeConsoleHotkey(">!NumpadDiv"), "Alt and slash aliases")
Check(SaveFindClipboardFromConsole("^NumpadDiv") = 3, "numpad slash conflict")
Check(SaveFindClipboardFromConsole("!NumpadDiv") = 1, "slash alias save")
Check(Key_FindClipboard = "!/", "canonical slash saved")
Check(SaveFindClipboardFromConsole("^F8") = 1, "restore after alias test")
IniRead, Actual, %IniFile%, Hotkeys, FindClipboard
Check(Actual = "^F8", "persisted")
Check(InStr(KeditMenuLabels["FindClipboard"], "^F8"), "tray caption refreshed")
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
SetKey_FindClipboard:
SetKey_GoToDef:
SetKey_ShiftF2:
SetKey_AltF:
SetKey_CtrlW:
SetKey_AltA:
SetKey_ColumnInsert:
SetKey_ToggleComment:
SetKey_SpacesToTabs:
SetKey_SmartClick:
Label_GoToDef:
Label_ShiftF2:
Label_AltF:
Label_CtrlW:
Label_AltA:
Label_ColumnInsert:
ProcessCommentToggle:
Label_SpacesToTabs:
Label_SmartClick:
Label_VS_DefinitionAction:
Label_VS_NavigateBack:
Label_VS_SendCtrlB:
Label_VS_ToggleComment:
Label_VS_BookmarkToggle:
Label_VS_BookmarkNext:
Label_VS_BookmarkPrevious:
Label_VS_Redo:
SetKey_VS_Peek:
SetKey_VS_Back:
SetKey_VS_Build:
SetKey_VS_ToggleComment:
SetKey_VS_BookmarkToggle:
SetKey_VS_BookmarkNext:
SetKey_VS_BookmarkPrevious:
SetKey_VS_Redo:
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
SetTimer, StopFixture, -50000
return
StopFixture:
ExitApp
SetKey_FindClipboard:
SetKey_GoToDef:
SetKey_ShiftF2:
SetKey_AltF:
SetKey_CtrlW:
SetKey_AltA:
SetKey_ColumnInsert:
SetKey_ToggleComment:
SetKey_SpacesToTabs:
SetKey_SmartClick:
Label_GoToDef:
Label_ShiftF2:
Label_AltF:
Label_CtrlW:
Label_AltA:
Label_ColumnInsert:
ProcessCommentToggle:
Label_SpacesToTabs:
Label_SmartClick:
Label_VS_DefinitionAction:
Label_VS_NavigateBack:
Label_VS_SendCtrlB:
Label_VS_ToggleComment:
Label_VS_BookmarkToggle:
Label_VS_BookmarkNext:
Label_VS_BookmarkPrevious:
Label_VS_Redo:
SetKey_VS_Peek:
SetKey_VS_Back:
SetKey_VS_Build:
SetKey_VS_ToggleComment:
SetKey_VS_BookmarkToggle:
SetKey_VS_BookmarkNext:
SetKey_VS_BookmarkPrevious:
SetKey_VS_Redo:
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
        if($PetModel){$arguments+=' --pet-model "'+$PetModel+'"'}
        $report=Join-Path $output 'result.txt'
        if(Test-Path $report){Remove-Item -LiteralPath $report}
        $uiProcess=Start-Process (Join-Path $root 'Kedit.Console\bin\MigrationF1\Kedit.Console.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if(!$uiProcess.WaitForExit(40000)){$uiProcess.Kill();throw 'UI checks timed out'}
        $message=Get-Content $report
        if($message -notlike 'PASS:*'){throw ($message -join "`n")}
        $message
    } finally {if(!$fixture.HasExited){$fixture.Kill()}}
}
