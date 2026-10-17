import ctypes, subprocess, uuid
from pathlib import Path
root=Path(__file__).resolve().parent.parent
source=(root/'Kedit_GO_to_Def.ahk').read_text(encoding='utf-8-sig')
label=source[source.index('ConfirmUpdateStartup:\n'):]
k=ctypes.WinDLL('kernel32',use_last_error=True)
k.CreateEventW.argtypes=[ctypes.c_void_p,ctypes.c_int,ctypes.c_int,ctypes.c_wchar_p];k.CreateEventW.restype=ctypes.c_void_p
k.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_uint];k.CloseHandle.argtypes=[ctypes.c_void_p]
folder=root/'.pet-test/startup-ack';folder.mkdir(parents=True,exist_ok=True)
for success in [True,False]:
    name='Local\\KeditUpdateReady_'+uuid.uuid4().hex
    handle=k.CreateEventW(None,True,False,name)
    if not handle:raise ctypes.WinError(ctypes.get_last_error())
    path=folder/('success.ahk' if success else 'failure.ahk')
    path.write_text('#NoEnv\n#Persistent\nStartupHotkeysOK := '+('true' if success else 'false')+'\nSetTimer, ConfirmUpdateStartup, -10\nreturn\n'+label,encoding='utf-8-sig')
    process=subprocess.Popen([r'C:\Program Files\AutoHotkey\AutoHotkey.exe','/ErrorStdOut',str(path),'--update-ready',name],stdout=subprocess.PIPE)
    try:
        actual=k.WaitForSingleObject(handle,2000)
        assert actual==(0 if success else 258),(success,actual)
    finally:process.terminate();process.wait();k.CloseHandle(handle)
print('PASS: actual AHK ready event only after successful core initialization')
