using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Kedit.Console {
    // Read only Windows per-session peak meters. No capture buffers, microphone or files.
    internal sealed class PetAudio : IDisposable {
        private readonly Thread worker;private readonly ManualResetEvent stop=new ManualResetEvent(false);
        private readonly string path;private readonly Action<double,string> publish;
        internal PetAudio(string path,Action<double,string> publish) {
            this.path=path;this.publish=publish;worker=new Thread(Run){IsBackground=true,Name="Pet audio meter"};worker.SetApartmentState(ApartmentState.MTA);worker.Start();
        }
        private void Run() {
            var sessions=new List<object>();DateTime next=DateTime.MinValue;string status="等待所选播放器发声";
            try {
                while(!stop.WaitOne(100)) {
                    try {
                        if(DateTime.UtcNow>=next) {
                            foreach(var s in sessions)Release(s);sessions.Clear();
                            Discover(sessions);next=DateTime.UtcNow.AddSeconds(2);
                            status=sessions.Count==0?"等待所选播放器的独立音频会话（不支持时不会监听其他应用）":"已连接所选播放器 · 音量起伏模式";
                        }
                        float peak=0;
                        foreach(var s in sessions) { int state;((ISession)s).GetState(out state);if(state!=1)continue;float p;((IMeter)s).GetPeakValue(out p);if(!float.IsNaN(p))peak=Math.Max(peak,p); }
                        publish(Math.Max(0,Math.Min(1,peak)),status);
                    }catch(Exception e) {next=DateTime.MinValue;publish(0,"音频会话暂不可用，将自动重试："+e.Message);stop.WaitOne(900);}
                }
            }finally {foreach(var s in sessions)Release(s);}
        }
        private void Discover(List<object> result) {
            IDevices enumerator=null;IDeviceCollection devices=null;
            try {
                enumerator=(IDevices)new DeviceEnumerator();enumerator.EnumAudioEndpoints(0,1,out devices);
                uint count;devices.GetCount(out count);
                for(uint i=0;i<count;i++) {
                    IDevice device=null;object manager=null;ISessionList list=null;
                    try {
                        devices.Item(i,out device);Guid iid=typeof(IManager).GUID;device.Activate(ref iid,23,IntPtr.Zero,out manager);
                        ((IManager)manager).GetSessionEnumerator(out list);int total;list.GetCount(out total);
                        for(int n=0;n<total;n++) {
                            object session=null;
                            try {
                                list.GetSession(n,out session);uint pid;int hr=((ISession)session).GetProcessId(out pid);
                                // Shared sessions cannot be attributed safely. Never substitute the mix endpoint.
                                if(hr!=0||pid==0)continue;
                                using(var process=Process.GetProcessById((int)pid)) {
                                    if(!string.Equals(process.MainModule.FileName,path,StringComparison.OrdinalIgnoreCase))continue;
                                }
                                if(!(session is IMeter))continue;
                                result.Add(session);session=null;
                            }catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
                            finally {Release(session);}
                        }
                    }finally{Release(list);Release(manager);Release(device);}
                }
            }finally{Release(devices);Release(enumerator);}
        }
        private static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
        public void Dispose(){stop.Set();if(worker.Join(3000))stop.Dispose();}

        [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]private class DeviceEnumerator {}
        [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDevices {
            void EnumAudioEndpoints(int flow,uint mask,out IDeviceCollection devices);
        }
        [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDeviceCollection {
            void GetCount(out uint count);void Item(uint index,out IDevice device);
        }
        [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IDevice {
            void Activate(ref Guid iid,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)]out object value);
        }
        [ComImport,Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IManager {
            void GetAudioSessionControl(IntPtr guid,uint flags,out IntPtr control);void GetSimpleAudioVolume(IntPtr guid,uint flags,out IntPtr volume);
            void GetSessionEnumerator(out ISessionList list);
        }
        [ComImport,Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface ISessionList {
            void GetCount(out int count);void GetSession(int index,[MarshalAs(UnmanagedType.IUnknown)]out object session);
        }
        [ComImport,Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface ISession {
            void GetState(out int state);void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)]out string name);
            void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr context);
            void GetIconPath([MarshalAs(UnmanagedType.LPWStr)]out string path);void SetIconPath([MarshalAs(UnmanagedType.LPWStr)]string path,IntPtr context);
            void GetGroupingParam(out Guid guid);void SetGroupingParam(ref Guid guid,IntPtr context);
            void RegisterAudioSessionNotification(IntPtr client);void UnregisterAudioSessionNotification(IntPtr client);
            void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
            [PreserveSig]int GetProcessId(out uint id);
        }
        [ComImport,Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]private interface IMeter {void GetPeakValue(out float value);}
    }
}
