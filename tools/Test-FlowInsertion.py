from pathlib import Path
import subprocess

root = Path(__file__).resolve().parent.parent
source = (root / "Kedit_GO_to_Def.ahk").read_text(encoding="utf-8-sig")
functions = source[source.index("BuildFlowInsertion(Prefix, Suffix, Name) {"):source.index("SetFlowClipboard(ByRef")]
fixture = root / ".pet-test" / "flow-terminal"
fixture.mkdir(parents=True, exist_ok=True)
harness = r'''
#NoEnv
prefix := "    apd_stress_chip_test,    "
for _, target in ["Bin1", "Bin42", "normal_test"] {
 for _, leading in ["", "        ", "`t`t"] {
  suffix := leading . target . ",                Bin11; // retained"
  result := prefix . BuildFlowInsertion(prefix,suffix,"Sample_test")
  rows := StrSplit(result,"`n")
  Check(rows.Length()=2,"two rows")
  RegExMatch(rows[1], "^(.*?)Sample_test", first)
  RegExMatch(rows[2], "^(.*?)" . target . ",", second)
  RegExMatch(rows[1], "^(.*?)Bin11;", fail1)
  RegExMatch(rows[2], "^(.*?)Bin31;", fail2)
  expected := FlowVisualColumn(prefix . leading . target . ",                ")
  Check(FlowVisualColumn(first1)=FlowVisualColumn(prefix),"caret column")
  Check(FlowVisualColumn(second1)=FlowVisualColumn(prefix),"next row column")
  Check(FlowVisualColumn(fail11)=expected && FlowVisualColumn(fail21)=expected,"failure columns")
  Check(InStr(rows[1],"// retained"),"comment preserved")
 }
}
Check(BuildFlowInsertion("a, Bin1, ","Bin11;","New_test")="","third column rejected")
Check(BuildFlowInsertion("a, Bi","n1, Bin11;","New_test")="","inside token rejected")
Check(BuildFlowInsertion("// a, ","Bin1, Bin11;","New_test")="","comment line rejected")
Check(BuildFlowInsertion("a, ","Bin1, Bin11;","two names")="","invalid name rejected")
Check(BuildFlowInsertion("a, ","Bin1, Bin11","New_test")="","missing semicolon rejected")
result := BuildFlowInsertion("a, ","Bin1, Bin11;","very_long_inserted_test_name")
Check(InStr(result,"very_long_inserted_test_name, "),"long name retains separator")
FileAppend, PASS: nodes and Bin terminals / caret alignment / comments / invalid positions / long names`n, *
ExitApp, 0
Check(value,label) {
 if !value {
  FileAppend, FAIL: %label%`n, *
  ExitApp, 1
 }
}
'''
for name, text in [("release-test.ahk", harness + functions), ("parse.ahk", "ExitApp\n" + source)]:
    path = fixture / name
    path.write_text(text, encoding="utf-8-sig")
    result = subprocess.run([r"C:\Program Files\AutoHotkey\AutoHotkey.exe", "/ErrorStdOut", str(path)], capture_output=True, timeout=30)
    print(result.stdout.decode(errors="replace"), end="")
    if result.returncode:
        raise SystemExit(result.returncode)
