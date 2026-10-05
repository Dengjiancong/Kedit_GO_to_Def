using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kedit.Console
{
    // Opt-in local integration check; exercises the real renderer and window lifecycle.
    internal static class PetDiagnostics
    {
        internal static async void Run(App app, MainWindow panel, string directory, bool selfTest)
        {
            try
            {
                Directory.CreateDirectory(directory);
                panel.ShowPetPage();
                for (int i = 0; i < 150 && !app.Pet.IsReady; i++) await Task.Delay(200);
                Check(app.Pet.IsReady, "Live2D did not become ready: " + app.Pet.Status);
                if(selfTest && Array.IndexOf(Environment.GetCommandLineArgs(),"--self-test-life")>=0) {
                    await CheckLife(app,panel,directory);app.ExitConsole();return;
                }
                if(selfTest && Array.IndexOf(Environment.GetCommandLineArgs(),"--self-test-menu")>=0) {
                    await CheckMenu(app,panel,directory);app.ExitConsole();return;
                }
                if(selfTest && Array.IndexOf(Environment.GetCommandLineArgs(),"--self-test-sword")>=0) {
                    await CheckSword(app,panel,directory);app.ExitConsole();return;
                }
                if (selfTest && Array.IndexOf(Environment.GetCommandLineArgs(), "--self-test-presentation") >= 0) {
                    await CheckPresentation(app,panel,directory); app.ExitConsole(); return;
                }
                if (selfTest && Array.IndexOf(Environment.GetCommandLineArgs(), "--self-test-companion") >= 0) {
                    await CheckCompanion(app, panel, directory); app.ExitConsole(); return;
                }
                if (selfTest && Array.IndexOf(Environment.GetCommandLineArgs(), "--probe-typing") >= 0) {
                    await ProbeTyping(app, directory); app.ExitConsole(); return;
                }
                await Task.Delay(3000);
                app.Pet.Capture(Path.Combine(directory, "pet-1.png"));
                await Task.Delay(1100);
                app.Pet.Capture(Path.Combine(directory, "pet-2.png"));
                Capture(panel, Path.Combine(directory, "console.png"));
                if (!selfTest) return;
                CheckTransparency(app.Pet.DiagnosticWindow);
                await CheckInteractions(app, directory);
                panel.ShowPetInteractionsForDiagnostics();
                await Task.Delay(200);
                Capture(panel, Path.Combine(directory, "console-interactions.png"));
                // Benchmark before any motion cues, keeping the workload idle
                // and the model/window size identical for every FPS preset.
                await Task.Delay(5000);
                await Benchmark(app, panel, directory);
                Check((app.Pet.WindowStyle & 0x08000000) != 0, "NOACTIVATE is missing");
                app.Pet.SetOptions(true, true);
                Check((app.Pet.WindowStyle & 0x20) != 0, "Click-through was not applied");
                app.Pet.SetOptions(true, false);
                Check((app.Pet.WindowStyle & 0x20) == 0, "Click-through could not be cleared");
                double originalSize = app.Pet.Settings.Size;
                app.Pet.SetSize(480); await Task.Delay(500);
                app.Pet.Capture(Path.Combine(directory, "pet-resized.png"));
                app.Pet.SetSize(originalSize);
                app.Pet.ResetPosition();
                app.Pet.Notify(2); await Task.Delay(500);
                app.Pet.Capture(Path.Combine(directory, "pet-bookmark.png"));
                app.Pet.Notify(6); await Task.Delay(500);
                app.Pet.Capture(Path.Combine(directory, "pet-paused.png"));
                app.Pet.Notify(7); await Task.Delay(1000);
                app.Pet.Capture(Path.Combine(directory, "pet-resumed.png"));
                panel.Close();
                Check(!panel.IsVisible && app.Pet.IsReady, "Closing the console stopped the pet");
                app.ShowConsole();
                Check(panel.IsVisible, "Console could not be reopened");
                app.Pet.SetEnabled(false);
                Check(!app.Pet.HasWindow, "Disabled pet retained its render window");
                app.Pet.SetEnabled(true);
                for (int i = 0; i < 100 && !app.Pet.IsReady; i++) await Task.Delay(200);
                Check(app.Pet.IsReady, "Pet could not be re-enabled");
                Check(app.Pet.Settings.FrameLimit == 30, "Benchmark did not restore default limit");
                File.WriteAllText(Path.Combine(directory, "result.txt"), "PASS: adjustable gaze, single-key strokes, sustained speed/duration settings, authored scrolling with complete final cycle, real 60-second keyboard retention, pause/resume priority, hook installation/removal, original-canvas rendering, alpha-zero blank hit-through, body hit target, NOACTIVATE, click-through on/off, resize, event cues, 30/60/90/120 UI settings and real FPS reports, close/reopen console, dispose/re-enable pet. Typing activity was supplied by the diagnostic driver, not physical keyboard input. See fps-benchmark.csv for measured results.");
                app.ExitConsole();
            }
            catch (Exception ex) {
                PetRuntime.Log("Diagnostics failed: " + ex);
                File.WriteAllText(Path.Combine(directory, "result.txt"), "FAIL: " + ex);
                if (selfTest) app.ExitConsole();
            }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static async Task CheckLife(App app,MainWindow panel,string directory) {
            var pet=app.Pet;var w=pet.DiagnosticWindow;w.StopInputForDiagnostics();pet.SetSize(650);pet.SetOptions(true,false);pet.SetCare(false,20,false);pet.CompanionCommand("reset");await Task.Delay(400);
            NativePoint original;GetCursorPos(out original);IntPtr foreground=GetForegroundWindow(),handle=new System.Windows.Interop.WindowInteropHelper(w).Handle;
            Point head=new Point(w.ActualWidth*.5,65+(w.ActualHeight-65)*.60),move=new Point(w.ActualWidth*.5+15,65+(w.ActualHeight-65)*.60);
            try {
                await MouseMessage(w,handle,head,0x207,16);await Task.Delay(200);await MouseMessage(w,handle,move,0x200,16);await Task.Delay(300);
                Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.life.rubbing") == "true","Native rub did not begin");
                pet.Capture(Path.Combine(directory,"rub.png"),true);
                await MouseMessage(w,handle,move,0x208,0);await Task.Delay(400);
                Check(await w.EvaluateForDiagnostics("!window.petDiagnostics.companion.life.rubbing && window.petDiagnostics.companion.hits.length===0") == "true","Rub release became tap or stayed active");
                await MouseMessage(w,handle,head,0x207,16);await Task.Delay(200);await MouseMessage(w,handle,move,0x200,16);
                pet.SetOptions(true,true);await Task.Delay(300);
                Check(await w.EvaluateForDiagnostics("!window.petDiagnostics.companion.life.rubbing") == "true","Click-through failed to cancel rub");
                await MouseMessage(w,handle,move,0x208,0);pet.SetOptions(true,false);
                Check(GetForegroundWindow()==foreground,"Rub stole foreground focus");
            }finally{SetCursorPos(original.X,original.Y);}
            var r=new PetReminder{Enabled=true,Days=127,Time=DateTime.Now.ToString("HH:mm"),CatchUpMinutes=5,Exact=true};pet.SaveReminder(r);pet.TickAutomation(DateTime.Now);await Task.Delay(1500);
            Check(pet.Automation.Wardrobe!=null,"Wardrobe prepare/commit failed");
            pet.Capture(Path.Combine(directory,"scheduled-animation.png"),true);await Task.Delay(2000);
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.life.wardrobe.values.ParamExpression11===1") == "true","Scheduled costume result missing");
            Check(PetAutomationData.Load().Wardrobe.RestoreAt.Date==DateTime.Now.Date.AddDays(1),"Cross-night restoration not persisted");
            pet.CompanionCommand("drink");await Task.Delay(600);pet.Capture(Path.Combine(directory,"costume-with-pot.png"),true);
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.combination().includes('hand-pot') && window.petDiagnostics.companion.life.appearance.ParamExpression11>.99") == "true","Hand prop replaced wardrobe");
            pet.Automation.Wardrobe.RestoreAt=DateTime.Now.AddSeconds(-1);pet.TickAutomation(DateTime.Now);await Task.Delay(600);
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.life.appearance.ParamExpression11<.01 && window.petDiagnostics.companion.combination().includes('hand-pot')") == "true","Wardrobe restore changed hand prop or failed");
            pet.Capture(Path.Combine(directory,"restored-with-pot.png"),true);
            pet.CompanionCommand("select","cloth off",true);await Task.Delay(300);Check(pet.Automation.Wardrobe==null,"Manual appearance did not cancel old restoration");
            var once=new PetReminder{Enabled=true,Days=127,Time=DateTime.Now.ToString("HH:mm"),CatchUpMinutes=5,Exact=true,Policy="once"};
            pet.SaveReminder(once);pet.TickAutomation(DateTime.Now);await Task.Delay(500);
            Check(pet.Automation.Wardrobe!=null&&pet.Automation.Wardrobe.original["ParamExpression11"]>.99,"Snapshot did not preserve manually selected appearance");
            await Task.Delay(7000);
            Check(pet.Automation.Wardrobe.Token=="restored"&&await w.EvaluateForDiagnostics("window.petDiagnostics.companion.life.appearance.ParamExpression11>.99") == "true","Play-once did not restore previous appearance");
            pet.CompanionCommand("reset");await Task.Delay(400);
            var hold=new PetReminder{Enabled=true,Days=127,Time=DateTime.Now.ToString("HH:mm"),CatchUpMinutes=5,Exact=true,Policy="hold"};
            pet.SaveReminder(hold);pet.TickAutomation(DateTime.Now);await Task.Delay(2800);
            Check(PetAutomationData.Load().Wardrobe.RestoreAt==default(DateTime),"Long hold unexpectedly acquired a restore deadline");
            pet.SetEnabled(false);pet.SetEnabled(true);for(int i=0;i<100&&!pet.IsReady;i++)await Task.Delay(100);Check(pet.IsReady,"Reopen failed");
            w=pet.DiagnosticWindow;w.StopInputForDiagnostics();await Task.Delay(700);
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.life.appearance.ParamExpression11>.99 && !window.petDiagnostics.companion.layers.some(l=>l.scheduled)") == "true","Reopen lost wardrobe or replayed reminder animation");
            pet.Automation.Wardrobe.RestoreAt=DateTime.Now.AddSeconds(-1);pet.SaveAutomation();pet.SetEnabled(false);pet.SetEnabled(true);
            for(int i=0;i<100&&!pet.IsReady;i++)await Task.Delay(100);w=pet.DiagnosticWindow;w.StopInputForDiagnostics();await Task.Delay(700);
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.life.appearance.ParamExpression11<.01 && !window.petDiagnostics.companion.layers.some(l=>l.scheduled)") == "true","Expired restore not applied on reopen");
            pet.CompanionCommand("reset");await Task.Delay(400);
            await w.EvaluateForDiagnostics("(()=>{const d=window.petDiagnostics;d.peak=.1;d.musicTimer=setInterval(()=>d.companion.life.receive({type:'music',enabled:true,peak:d.peak,amount:70,sensitivity:1.5}),100);})()");await Task.Delay(3700);
            Check(await w.EvaluateForDiagnostics("!!window.petDiagnostics.companion.life.auto") == "true","Automatic music never entered");
            pet.Capture(Path.Combine(directory,"automatic-music.png"),true);
            await w.EvaluateForDiagnostics("(()=>{const d=window.petDiagnostics;d.oldCount=d.interactions.strokeCount;const input={x:0,y:0,presses:1,sentAt:Date.now()};d.companion.activity(input);d.interactions.receive(input);})()");
            Check(await w.EvaluateForDiagnostics("!window.petDiagnostics.companion.life.auto && window.petDiagnostics.interactions.strokeCount===window.petDiagnostics.oldCount+1") == "true","First typing stroke lost to music");
            await Task.Delay(3500);Check(await w.EvaluateForDiagnostics("!!window.petDiagnostics.companion.life.auto") == "true","Music did not resume after typing");
            pet.CompanionCommand("drink");await Task.Delay(500);Check(await w.EvaluateForDiagnostics("!window.petDiagnostics.companion.life.auto && window.petDiagnostics.companion.combination().includes('hand-pot')") == "true","Automatic music displaced manual prop");
            pet.CompanionCommand("reset");await Task.Delay(500);await w.EvaluateForDiagnostics("window.petDiagnostics.peak=0");await Task.Delay(2000);
            Check(await w.EvaluateForDiagnostics("!!window.petDiagnostics.companion.life.auto") == "true","Short silence removed music");await Task.Delay(7000);
            Check(await w.EvaluateForDiagnostics("!window.petDiagnostics.companion.life.auto") == "true","Stopped source did not exit music");
            await w.EvaluateForDiagnostics("clearInterval(window.petDiagnostics.musicTimer)");
            panel.ShowAutomationForDiagnostics(false);await Task.Delay(300);Capture(panel,Path.Combine(directory,"console-reminders.png"));
            panel.ShowAutomationForDiagnostics(true);await Task.Delay(300);Capture(panel,Path.Combine(directory,"console-music.png"));
            File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: native rub, release without tap, click-through cancellation, foreground preserved, scheduled authored animation, persistent cross-night deadline, costume retained with prop, restore without changing prop, manual override, automatic music, first typing stroke, resume, manual priority, brief silence/stop, UI. Music samples in this renderer check were injected; real audio meter is checked separately.");
        }
        private static async Task CheckMenu(App app, MainWindow panel, string directory)
        {
            var w=app.Pet.DiagnosticWindow; w.StopInputForDiagnostics();app.Pet.SetSize(550);app.Pet.SetOptions(true,false);app.Pet.SetCare(false,20,false);
            app.Pet.SetSword(1.5,65,100);await Task.Delay(300);
            Check(PetSettings.Load().SwordHeadAmount==100,"100 percent head did not persist");
            app.Pet.CompanionCommand("head");await Task.Delay(500);
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.layers.some(l=>!l.ending&&l.r.id==='emote-shy')") == "true","Head expression wrong");
            app.Pet.CompanionCommand("body");await Task.Delay(500);
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.layers.some(l=>!l.ending&&l.r.id==='emote-shy3') && !window.petDiagnostics.companion.layers.some(l=>!l.ending&&l.r.id==='emote-shy2')") == "true","Body expression wrong");
            app.Pet.CompanionCommand("reset");await Task.Delay(400);
            IntPtr foreground=GetForegroundWindow(),handle=new System.Windows.Interop.WindowInteropHelper(w).Handle;
            NativePoint original;GetCursorPos(out original);
            try {
                Point body=new Point(w.ActualWidth*.5,65+(w.ActualHeight-65)*.9);
                await MouseMessage(w,handle,body,0x204,2);await MouseMessage(w,handle,body,0x205,0);await Task.Delay(400);
                Check(w.ActionMenuForDiagnostics!=null&&w.ActionMenuForDiagnostics.IsOpen,"Right-click did not open menu");
                Check(GetForegroundWindow()==foreground,"Menu changed foreground window");
                CaptureElement(w.ActionMenuForDiagnostics,Path.Combine(directory,"action-menu.png"));
                string[] ids={"hand-pot","motion-weapon","motion-keyboard","motion-music"};
                for(int i=0;i<ids.Length;i++) {
                    if(i>0)w.OpenActionMenuForDiagnostics(ids[i-1]);
                    var menu=w.ActionMenuForDiagnostics;
                    if(i>0)Check(((System.Windows.Controls.MenuItem)menu.Items[i]).IsChecked,"Current action not marked");
                    ((System.Windows.Controls.MenuItem)menu.Items[i+1]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));menu.IsOpen=false;
                    await Task.Delay(400);
                    Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.combination().includes('"+ids[i]+"')") == "true","Menu action not applied: "+ids[i]);
                }
                w.OpenActionMenuForDiagnostics("motion-music");
                ((System.Windows.Controls.MenuItem)w.ActionMenuForDiagnostics.Items[0]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.MenuItem.ClickEvent));w.ActionMenuForDiagnostics.IsOpen=false;await Task.Delay(400);
                Check(await w.EvaluateForDiagnostics("!window.petDiagnostics.interactions.manualBusy") == "true","Natural menu failed to restore automatic mode");
            } finally {SetCursorPos(original.X,original.Y);if(w.ActionMenuForDiagnostics!=null)w.ActionMenuForDiagnostics.IsOpen=false;}
            panel.OpenPetResourcesForDiagnostics();await Task.Delay(400);Capture(panel,Path.Combine(directory,"console-dark.png"));
            // The ComboBox popup is a separate WPF visual, captured separately.
            CaptureElement(panel.PetResourcesPopupForDiagnostics(),Path.Combine(directory,"dropdown-dark.png"));
            panel.ClosePetResourcesForDiagnostics();app.Pet.SetSword(1.5,65,25);
            File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: fixed head/body expressions, 100% setting persistence, native right-click menu without foreground activation, all four mode switches, current checkmarks and natural/automatic restore.");
        }
        private static void CaptureElement(FrameworkElement element,string path)
        {
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(element);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
        }
        private static async Task CheckSword(App app, MainWindow panel, string directory)
        {
            var w=app.Pet.DiagnosticWindow;w.StopInputForDiagnostics();app.Pet.SetSize(650);app.Pet.SetCare(false,20,false);
            app.Pet.SetSword(1.5,65,25);app.Pet.CompanionCommand("weapon");await Task.Delay(1200);
            Check(PetSettings.Load().SwordAmount==65&&PetSettings.Load().SwordHeadAmount==25,"Sword settings did not persist");
            await w.EvaluateForDiagnostics(@"(()=>{const d=window.petDiagnostics;d.speed=0;d.cursor={x:.8,y:.6};d.swordTimer=setInterval(()=>{const data={...d.cursor,presses:0,mouseVX:d.speed,mouseVY:0,sentAt:Date.now()};d.interactions.receive(data);d.companion.activity(data);},33);d.model.internalModel.on('beforeModelUpdate',()=>{const c=d.model.internalModel.coreModel;d.swordPose={head:c.getParameterValueById('ParamAngleX'),arm:c.getParameterValueById('ParamExpression9')};});})()");
            await Task.Delay(1600);
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.swordPose.head>2 && window.petDiagnostics.swordPose.head<4") == "true","Head not reduced to 25 percent");
            Check(Array.Exists(app.Pet.Resources,r=>r.id=="motion-cloth off"&&r.label=="丧失戰衣（换装动画）"&&r.available),"Named costume animation unavailable");
            await w.EvaluateForDiagnostics("window.petDiagnostics.cursor={x:.1,y:.1}");
            app.Pet.SetSword(1.5,65,25,.5);await Task.Delay(1800);
            double low=double.Parse(await w.EvaluateForDiagnostics("window.petDiagnostics.swordPose.head"),CultureInfo.InvariantCulture);
            app.Pet.SetSword(1.5,65,25,4);await Task.Delay(1800);
            double high=double.Parse(await w.EvaluateForDiagnostics("window.petDiagnostics.swordPose.head"),CultureInfo.InvariantCulture);
            Check(high>low*3 && high<=3.375,"Independent head sensitivity ineffective or exceeded amplitude");
            Check(PetSettings.Load().SwordHeadSensitivity==4,"Head sensitivity not persisted");
            app.Pet.SetSword(1.5,65,25,1);await w.EvaluateForDiagnostics("window.petDiagnostics.cursor={x:.8,y:.6}");await Task.Delay(800);
            app.Pet.SetSword(1.5,65,25,1,2);await Task.Delay(100);
            Check(PetSettings.Load().SwordHeadSpeed==2,"Head speed not persisted");
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.settings.swordHeadSpeed===2") == "true","Head speed not delivered to renderer");
            app.Pet.SetSword(1.5,65,25,1,1);
            foreach(int direction in new[]{-1,1}) {
                await w.EvaluateForDiagnostics("window.petDiagnostics.cursor.x="+(direction*.8).ToString(CultureInfo.InvariantCulture)+";window.petDiagnostics.speed="+(direction*2));await Task.Delay(230);
                Check(await w.EvaluateForDiagnostics("window.petDiagnostics.swordPose.head*"+direction+">0") == "true","Head froze during fast sword swipe");
                app.Pet.Capture(Path.Combine(directory,direction<0?"sword-left.png":"sword-right.png"),true);
                Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.sword.position*"+direction+">.25") == "true","Sword failed to follow velocity direction");
                File.WriteAllText(Path.Combine(directory,direction<0?"left.json":"right.json"),await w.EvaluateForDiagnostics("window.petDiagnostics.swordPose"));
                await w.EvaluateForDiagnostics("window.petDiagnostics.speed=0");await Task.Delay(1600);
                Check(await w.EvaluateForDiagnostics("Math.abs(window.petDiagnostics.companion.sword.position)<.002 && Math.abs(window.petDiagnostics.swordPose.arm-.8)<.002") == "true","Sword did not return to resting grip");
            }
            app.Pet.Capture(Path.Combine(directory,"sword-rest.png"),true);
            app.Pet.CompanionCommand("reset");await Task.Delay(500);
            Check(await w.EvaluateForDiagnostics("!window.petDiagnostics.interactions.manualBusy && !window.petDiagnostics.companion.sword.active") == "true","Sword state leaked after exit");
            await w.EvaluateForDiagnostics("clearInterval(window.petDiagnostics.swordTimer)");panel.ShowSwordForDiagnostics();await Task.Delay(300);
            Capture(panel,Path.Combine(directory,"console-sword.png"));
            app.Pet.CompanionCommand("select","motion-cloth off",false);await Task.Delay(1400);app.Pet.Capture(Path.Combine(directory,"costume-animation.png"),true);
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.layers.some(l=>l.r.id==='motion-cloth off'&&!l.ending)") == "true","Costume animation failed to play");
            File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: directional swings, continuous head during fast swipes, independent head speed delivered and persisted, independent 0.5/4 head sensitivity with amplitude bound, resting grip, cleanup, named costume animation playback; injected anonymous motion samples used.");
        }
        private static async Task CheckPresentation(App app, MainWindow panel, string directory)
        {
            var w=app.Pet.DiagnosticWindow; w.StopInputForDiagnostics(); app.Pet.SetSize(600);
            app.Pet.SetCare(false,20,false); app.Pet.SetPresentation(30,75);
            await Task.Delay(300);
            Check(Math.Abs(w.BubbleTopForDiagnostics-(w.Height-65)*.3)<1,"Bubble offset did not follow canvas size");
            app.Pet.CompanionCommand("speechPreview"); await Task.Delay(700);
            app.Pet.Capture(Path.Combine(directory,"bubble-default.png"),true);
            app.Pet.SetPresentation(45,100); await Task.Delay(300);
            Check(Math.Abs(w.BubbleTopForDiagnostics-(w.Height-65)*.45)<1,"Live bubble adjustment failed");
            Check(PetSettings.Load().MouthAmount==100 && PetSettings.Load().BubbleOffsetPercent==45,"Presentation settings did not persist");
            app.Pet.Capture(Path.Combine(directory,"bubble-lowered.png"),true);
            app.Pet.CompanionCommand("reset"); await Task.Delay(400);
            app.Pet.SetPresentation(30,75);
            await w.EvaluateForDiagnostics(@"(()=>{const d=window.petDiagnostics;d.target={x:-.8,y:.6};d.gazeTimer=setInterval(()=>d.interactions.receive({...d.target,presses:0,sentAt:Date.now()}),33);d.model.internalModel.on('beforeModelUpdate',()=>{const c=d.model.internalModel.coreModel;d.pose={x:c.getParameterValueById('ParamAngleX'),y:c.getParameterValueById('ParamAngleY'),arm:c.getParameterValueById('ParamExpression9')};});})()");
            app.Pet.CompanionCommand("weapon"); await Task.Delay(1800);
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.pose.x < -1 && window.petDiagnostics.pose.y > 1") == "true","Weapon pinned gaze to authored angles");
            app.Pet.Capture(Path.Combine(directory,"weapon-left.png"),true);
            File.WriteAllText(Path.Combine(directory,"weapon-left.json"),await w.EvaluateForDiagnostics("window.petDiagnostics.pose"));
            await w.EvaluateForDiagnostics("window.petDiagnostics.target={x:.8,y:-.6}"); await Task.Delay(1000);
            Check(await w.EvaluateForDiagnostics("window.petDiagnostics.pose.x > 1 && window.petDiagnostics.pose.y < -1") == "true","Weapon gaze did not switch direction");
            app.Pet.Capture(Path.Combine(directory,"weapon-right.png"),true);
            File.WriteAllText(Path.Combine(directory,"weapon-right.json"),await w.EvaluateForDiagnostics("window.petDiagnostics.pose"));
            await w.EvaluateForDiagnostics("clearInterval(window.petDiagnostics.gazeTimer)");
            app.Pet.CompanionCommand("reset"); await Task.Delay(400);
            foreach(int amount in new[]{10,100}) {
                app.Pet.SetPresentation(30,amount); await Task.Delay(100);
                await w.EvaluateForDiagnostics("(()=>{const d=window.petDiagnostics;d.peak=0;d.companion.speech(2400);d.peakTimer=setInterval(()=>d.peak=Math.max(d.peak,d.companion.mouth),16);})()");
                await Task.Delay(1700);
                double peak=double.Parse(await w.EvaluateForDiagnostics("window.petDiagnostics.peak"),CultureInfo.InvariantCulture);
                Check(amount==10 ? peak<.12 : peak>.65,"Mouth amplitude setting ineffective");
                await w.EvaluateForDiagnostics("clearInterval(window.petDiagnostics.peakTimer);window.petDiagnostics.companion.speech(0)"); await Task.Delay(700);
                Check(await w.EvaluateForDiagnostics("window.petDiagnostics.companion.mouth<.001") == "true","Mouth did not close smoothly");
            }
            app.Pet.SetPresentation(30,75); app.Pet.CompanionCommand("speechPreview");
            panel.ShowPresentationForDiagnostics(); await Task.Delay(400); Capture(panel,Path.Combine(directory,"console-presentation.png"));
            File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: live bubble offset and scaling; settings persistence; real weapon weak left/right/up/down head follow; 10%/100% mouth amplitude and smooth closure. Author model has no independent eyeball parameters.");
        }

        private static async Task CheckCompanion(App app, MainWindow panel, string directory)
        {
            var window = app.Pet.DiagnosticWindow;
            window.StopInputForDiagnostics(); app.Pet.SetSize(700); app.Pet.SetOptions(true,false);
            app.Pet.SetCare(false, 20, false);
            await Task.Delay(700);
            File.WriteAllText(Path.Combine(directory,"head-geometry.json"),await window.EvaluateForDiagnostics(@"(()=>{const d=window.petDiagnostics,m=d.model,h=d.companion.headBounds(),internal=m.internalModel; const project=p=>m.toGlobal(internal.localTransform.apply(new PIXI.Point(p.x,p.y)));return {head:h,topLeft:project(h),bottomRight:project({x:h.x+h.width,y:h.y+h.height}),canvas:[internal.originalWidth,internal.originalHeight]};})()"));
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--probe-hit")>=0) {
                File.WriteAllText(Path.Combine(directory,"drawables.json"),await window.EvaluateForDiagnostics(@"(()=>{const d=window.petDiagnostics,c=d.model.internalModel.coreModel,m=d.model.internalModel,r=c._model;return Array.from(r.drawables.ids).map((id,i)=>{let j=r.drawables.parentPartIndices[i],chain=[],n=0;while(j>=0&&n++<r.parts.count){chain.push(r.parts.ids[j]);j=r.parts.parentIndices[j];}return {id,i,chain,opacity:c.getDrawableOpacity(i),bounds:m.getDrawableBounds(i)};}).filter(x=>x.opacity>.01);})()"));
                File.WriteAllText(Path.Combine(directory,"hit-debug.json"),await window.EvaluateForDiagnostics(@"(()=>{const d=window.petDiagnostics,c=d.model.internalModel.coreModel;return {headCount:d.companion.headDrawables.size,total:c.getDrawableCount(),parts:Array.from(c._model.parts.ids),hits:[.3,.4,.5,.6,.7,.8,.9,.95].map(y=>({y,hit:d.hitRegion(innerWidth*.5,innerHeight*y)}))};})()"));
                string data=serializer.Deserialize<string>(await window.EvaluateForDiagnostics(@"(()=>{const d=window.petDiagnostics,c=d.model.internalModel.coreModel,old=c.getDrawableOpacity,r=PIXI.RenderTexture.create({width:innerWidth,height:innerHeight,resolution:1});try{c.getDrawableOpacity=i=>d.companion.headDrawables.has(i)?old.call(c,i):0;d.app.renderer.render(d.app.stage,{renderTexture:r,clear:true});return d.app.renderer.extract.canvas(r).toDataURL();}finally{c.getDrawableOpacity=old;r.destroy(true);}})()"));
                File.WriteAllBytes(Path.Combine(directory,"head-mask.png"),Convert.FromBase64String(data.Substring(data.IndexOf(',')+1)));
                await CheckMiddleGestures(app,directory);return;
            }
            File.WriteAllText(Path.Combine(directory,"catalog.json"), serializer.Serialize(app.Pet.Resources), Encoding.UTF8);
            Check(app.Pet.Resources != null && app.Pet.Resources.Length == 23,"Expected all 23 model resources");
            foreach (var r in app.Pet.Resources) {
                Check(r.available,"Resource unavailable: " + r.id + " " + r.detail);
                await window.EvaluateForDiagnostics("window.petDiagnostics.companion.reset()"); await Task.Delay(350);
                app.Pet.CompanionCommand("select",r.id,true); await Task.Delay(1300);
                app.Pet.Capture(Path.Combine(directory,r.id+".png"),true);
            }
            await window.EvaluateForDiagnostics("window.petDiagnostics.companion.reset()"); await Task.Delay(400);
            await CheckMiddleGestures(app, directory);
            await window.EvaluateForDiagnostics("window.petDiagnostics.companion.reset()"); await Task.Delay(400);
            File.WriteAllText(Path.Combine(directory,"geometry.json"),await window.EvaluateForDiagnostics(@"(()=>{const d=window.petDiagnostics; return {head:d.companion.headBounds(),eyes:d.companion.eyeDrawables,mouth:d.interactions.index('ParamMouthOpenY'),width:innerWidth,height:innerHeight};})()"));
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.touchAt(0,0)") == "false","Transparent corner accepted a touch");
            app.Pet.CompanionCommand("head"); await Task.Delay(500);
            app.Pet.Capture(Path.Combine(directory,"touch-head.png"));
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.hits.length===1") == "true","Manual head button did not touch");
            for(int i=0;i<4;i++) { app.Pet.CompanionCommand("body"); await Task.Delay(400); }
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.layers.some(l=>!l.ending&&l.r.id==='emote-angry')") == "true","Repeated touches did not escalate");
            app.Pet.Capture(Path.Combine(directory,"touch-repeat.png"));
            await Task.Delay(4100);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.layers.every(l=>!l.touch)") == "true","Touch feedback did not recover");
            app.Pet.CompanionCommand("drink"); await Task.Delay(500);
            await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.receive({presses:3,x:0,y:0,sentAt:Date.now()})");
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.manualBusy && !window.petDiagnostics.interactions.pulses.length") == "true","Keys interrupted manual prop");
            app.Pet.CompanionCommand("select","emote-shy",true); app.Pet.CompanionCommand("save"); await Task.Delay(250);
            Check(app.Pet.Settings.FavoriteCombination.Length == 2,"Combination not saved");
            app.Pet.CompanionCommand("reset"); await Task.Delay(400);
            Check(await window.EvaluateForDiagnostics("!window.petDiagnostics.interactions.manualBusy && !window.petDiagnostics.interactions.pulses.length") == "true","Manual exit retained busy state or keys");
            app.Pet.CompanionCommand("restore"); await Task.Delay(500);
            app.Pet.Capture(Path.Combine(directory,"combination.png"));
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.combination().length===2") == "true","Combination not restored");
            app.Pet.CompanionCommand("reset"); await Task.Delay(500);
            await window.EvaluateForDiagnostics("window.petDiagnostics.companion.speech(3000)"); await Task.Delay(500);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.mouth>.05") == "true","Speech did not animate mouth");
            app.Pet.Capture(Path.Combine(directory,"mouth-open.png"));
            await window.EvaluateForDiagnostics("window.petDiagnostics.companion.speech(0)"); await Task.Delay(700);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.mouth<.001") == "true","Mouth did not settle");
            app.Pet.Capture(Path.Combine(directory,"mouth-closed.png"));
            Check((app.Pet.WindowStyle & 0x08000000) != 0,"NOACTIVATE lost");
            CheckTransparency(window);
            app.Pet.SetOptions(true,true); app.Pet.CompanionCommand("head"); await Task.Delay(500);
            Check((app.Pet.WindowStyle & 0x20) != 0,"Click through lost");
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.hits.length===1") == "true","Panel touch failed in click through");
            panel.ShowCompanionForDiagnostics(); await Task.Delay(300); Capture(panel,Path.Combine(directory,"console-companion.png"));
            app.Pet.SetEnabled(false); Check(!app.Pet.HasWindow,"Companion window leaked");
            app.Pet.SetEnabled(true);
            for (int i=0;i<150&&!app.Pet.IsReady;i++) await Task.Delay(200);
            Check(app.Pet.IsReady,"Companion could not reopen");
            Check(await app.Pet.DiagnosticWindow.EvaluateForDiagnostics("window.petDiagnostics.companion.layers.length===0") == "true","Transient layers survived reopening");
            File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: all 23 resources loaded and captured; native middle head/body routing, left-click isolation, middle hold/move rejection, focus preserved, deformed head-mask hit regions at two sizes, alpha-zero rejection; shared touch counting/escalation/recovery, prop priority/no queued keys, combination save/restore, mouth open/close, NOACTIVATE, transparent hit-through, panel touch in click-through, dispose/reopen. Mouse messages were supplied by the diagnostic driver to this test window, not physical user input.");
        }

        private static async Task CheckMiddleGestures(App app, string directory)
        {
            var window=app.Pet.DiagnosticWindow;
            NativePoint original; GetCursorPos(out original);
            IntPtr foreground=GetForegroundWindow(), handle=new System.Windows.Interop.WindowInteropHelper(window).Handle;
            try {
                Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.headDrawables.size>0") == "true","No authored head parts mapped");
                Check(await window.EvaluateForDiagnostics("window.petDiagnostics.hitRegion(innerWidth*.5,innerHeight*.60)==='head'") == "true","Head pixel was misclassified");
                Check(await window.EvaluateForDiagnostics("window.petDiagnostics.hitRegion(innerWidth*.5,innerHeight*.90)==='body'") == "true","Body pixel was misclassified");
                Point head=new Point(window.ActualWidth*.5,65+(window.ActualHeight-65)*.60);
                Point body=new Point(window.ActualWidth*.5,65+(window.ActualHeight-65)*.90);
                await MouseMessage(window,handle,head,0x201,1); await MouseMessage(window,handle,head,0x202,0);
                Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.hits.length===0") == "true","Left click triggered interaction");
                await MouseMessage(window,handle,head,0x207,16); await MouseMessage(window,handle,head,0x208,0); await Task.Delay(200);
                Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.hits.length===1 && window.petDiagnostics.companion.lastTouchKind==='head'") == "true","Native middle head click failed");
                await Task.Delay(400);
                await MouseMessage(window,handle,body,0x207,16); await MouseMessage(window,handle,body,0x208,0); await Task.Delay(200);
                Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.hits.length===2 && window.petDiagnostics.companion.lastTouchKind==='body'") == "true","Native middle body click failed");
                await MouseMessage(window,handle,head,0x207,16); await Task.Delay(550); await MouseMessage(window,handle,head,0x208,0);
                Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.hits.length===2") == "true","Long press counted as a tap");
                await MouseMessage(window,handle,head,0x207,16);
                await MouseMessage(window,handle,new Point(head.X+20,head.Y),0x200,16);
                await MouseMessage(window,handle,head,0x208,0);
                Check(await window.EvaluateForDiagnostics("window.petDiagnostics.companion.hits.length===2") == "true","Middle movement counted as a tap");
                Check(GetForegroundWindow()==foreground,"Pet interaction changed foreground focus");
                app.Pet.SetSize(360); await Task.Delay(400);
                Check(await window.EvaluateForDiagnostics("window.petDiagnostics.hitRegion(innerWidth*.5,innerHeight*.60)==='head' && window.petDiagnostics.hitRegion(innerWidth*.5,innerHeight*.90)==='body'") == "true","Hit regions did not follow resize");
                File.WriteAllText(Path.Combine(directory,"native-gestures.txt"),"PASS: native WPF routing, left-click isolation, middle head/body, long hold/move rejection, focus preserved, resized head/body mask.");
            } finally { SetCursorPos(original.X,original.Y); app.Pet.SetSize(700); }
        }
        private static async Task MouseMessage(PetWindow window, IntPtr handle, Point point, int message, int buttons)
        {
            Point screen=window.PointToScreen(point); SetCursorPos((int)screen.X,(int)screen.Y);
            var source=PresentationSource.FromVisual(window); Point device=source.CompositionTarget.TransformToDevice.Transform(point);
            PostMessage(handle,message,new IntPtr(buttons),new IntPtr(((int)device.Y<<16)|((int)device.X&0xffff)));
            await Task.Delay(70);
        }
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

        private static async Task ProbeTyping(App app, string directory)
        {
            app.Pet.SetInteractions(false, false, false, "editors");
            var window = app.Pet.DiagnosticWindow;
            await window.EvaluateForDiagnostics(@"(() => {
                const d=window.petDiagnostics; d.probe={ParamExpression7:1,ParamExpression12:.2,ParamExpression13:0,ParamExpression14:0,ParamExpression15:0};
                d.model.internalModel.on('beforeModelUpdate',()=>{for(const [id,value] of Object.entries(d.probe)) d.model.internalModel.coreModel.setParameterValueById(id,value);});
            })()");
            foreach (var item in new[] { "rest", "knock", "left", "right", "text", "text10", "text20", "text30", "text40", "text50" }) {
                string script = item == "knock" ? "d.probe.ParamExpression12=1" : item == "left" ? "d.probe.ParamExpression13=1" : item == "right" ? "d.probe.ParamExpression13=0;d.probe.ParamExpression14=1" : item == "text" ? "d.probe.ParamExpression15=60" : "";
                if (item.StartsWith("text") && item.Length > 4) script = "d.probe.ParamExpression15=" + item.Substring(4);
                await window.EvaluateForDiagnostics("(()=>{const d=window.petDiagnostics;" + script + "})()");
                await Task.Delay(600); app.Pet.Capture(Path.Combine(directory, "probe-" + item + ".png"));
            }
            File.WriteAllText(Path.Combine(directory,"parameters.json"), await window.EvaluateForDiagnostics("(()=>{const c=window.petDiagnostics.model.internalModel.coreModel; return c._parameterIds.map((id,i)=>({id,min:c.getParameterMinimumValue(i),max:c.getParameterMaximumValue(i),value:c.getParameterDefaultValue(i)}));})()"));
        }

        private static async Task CheckInteractions(App app, string directory)
        {
            var window = app.Pet.DiagnosticWindow;
            app.Pet.ResetInteractions();
            Check(app.Pet.Settings.MouseFollow && app.Pet.Settings.HeadFollow && app.Pet.Settings.TypingEnabled, "All interaction defaults must be enabled");
            Check(window.InputHookInstalled, "Keyboard hook was not installed");
            window.StopInputForDiagnostics();
            Check(!window.InputHookInstalled, "Keyboard hook was not released");
            Check(PetInput.IsTypingKey(0x41) && PetInput.IsTypingKey(0xE5) && !PetInput.IsTypingKey(0x70), "Key classification failed");
            Check(PetInput.AcceptKey(0x41,0,false) && !PetInput.AcceptKey(0x41,0,true) && !PetInput.AcceptKey(0x41,0x10,false) && !PetInput.AcceptKey(0x41,0x20,false), "Modifier/injected-key filter failed");
            await window.EvaluateForDiagnostics(@"(() => {
                const d=window.petDiagnostics; d.input={x:1,y:.5,presses:0};
                d.press=()=>d.interactions.receive({...d.input,presses:1});
                d.timer=setInterval(()=>d.interactions.receive(d.input),33);
                d.model.internalModel.on('beforeModelUpdate',()=>{
                    d.headX=d.model.internalModel.coreModel.getParameterValueById('ParamAngleX');
                    d.hand=d.model.internalModel.coreModel.getParameterValueById('ParamExpression12');
                    d.keyboard=d.model.internalModel.coreModel.getParameterValueById('ParamExpression7');
                });
            })()");
            await Task.Delay(700);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.headX>0") == "true", "Right gaze failed");
            app.Pet.Capture(Path.Combine(directory,"pet-look-right.png"));
            await window.EvaluateForDiagnostics("window.petDiagnostics.input.x=-1");
            await Task.Delay(700);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.headX<0") == "true", "Left gaze failed");
            app.Pet.Capture(Path.Combine(directory,"pet-look-left.png"));
            var panel=(MainWindow)app.MainWindow;
            panel.SetFollowForDiagnostics(80,3,2);
            await Task.Delay(300);
            var saved=PetSettings.Load();
            Check(saved.FollowAmount==80 && saved.FollowSensitivity==3 && saved.FollowSpeed==2, "Follow UI did not persist");
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.settings.followAmount===80") == "true", "Follow UI did not reach renderer");
            panel.SetFollowForDiagnostics(45,2,1.5);
            panel.SetScrollForDiagnostics(900,6); await Task.Delay(150);
            saved=PetSettings.Load();
            Check(saved.ScrollRate==900 && saved.ScrollSeconds==6,"Scroll settings did not persist through UI");
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.settings.scrollSeconds===6 && window.petDiagnostics.interactions.settings.scrollRate===900") == "true","Scroll settings did not reach renderer");
            panel.SetScrollForDiagnostics(600,5); await Task.Delay(100);
            await window.EvaluateForDiagnostics("window.petDiagnostics.press()");
            await Task.Delay(90); app.Pet.Capture(Path.Combine(directory,"pet-single-down.png"));
            await Task.Delay(250);
            Check(await window.EvaluateForDiagnostics("!window.petDiagnostics.interactions.typing && window.petDiagnostics.interactions.strokeCount===1 && window.petDiagnostics.interactions.keyboardWeight===1 && window.petDiagnostics.interactions.textWeight===0") == "true", "Single stroke did not settle with keyboard retained");
            app.Pet.Capture(Path.Combine(directory,"pet-single-up.png"));
            for(int i=0;i<4;i++) { await window.EvaluateForDiagnostics("window.petDiagnostics.press()"); await Task.Delay(500); }
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.textWeight===0") == "true", "Slow typing revealed text");
            app.Pet.Capture(Path.Combine(directory,"pet-slow-no-text.png"));
            await window.EvaluateForDiagnostics("window.petDiagnostics.fastTimer=setInterval(window.petDiagnostics.press,80)");
            await Task.Delay(4000);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.cycleStart===null") == "true","Brief high speed triggered scrolling before sustained duration");
            panel.ShowPetInteractionsForDiagnostics(); await Task.Delay(100);
            Capture(panel,Path.Combine(directory,"console-scroll-progress.png"));
            bool rolling=false;
            for(int i=0;i<100;i++) { await Task.Delay(50); if(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.cycleStart!==null") == "true") {rolling=true;break;} }
            Check(rolling,"Sustained typing failed to trigger scrolling");
            await window.EvaluateForDiagnostics("clearInterval(window.petDiagnostics.fastTimer)");
            Check(await window.EvaluateForDiagnostics("Math.abs(window.petDiagnostics.interactions.textCurve(1.3)-32.167)<.0001 && window.petDiagnostics.interactions.textDuration===2.4") == "true","Authored text curve or duration not preserved");
            await Task.Delay(650);
            double previous=double.Parse(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.textValue"),CultureInfo.InvariantCulture);
            app.Pet.Capture(Path.Combine(directory,"pet-scroll-entry.png"));
            await Task.Delay(650);
            double middle=double.Parse(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.textValue"),CultureInfo.InvariantCulture);
            Check(middle>previous && previous>0,"Text did not keep scrolling after typing stopped");
            app.Pet.Capture(Path.Combine(directory,"pet-scroll-middle.png"));
            await Task.Delay(650);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.textWeight===1") == "true","Final cycle was cut short");
            app.Pet.Capture(Path.Combine(directory,"pet-scroll-exit.png"));
            await Task.Delay(650);
            Check(await window.EvaluateForDiagnostics("!window.petDiagnostics.interactions.typing && window.petDiagnostics.interactions.textWeight===0 && window.petDiagnostics.keyboard>.9 && window.petDiagnostics.headX<0") == "true", "Stopped typing did not retain keyboard and restore gaze");
            app.Pet.Capture(Path.Combine(directory,"pet-stopped-retained.png"));
            // Verify a real minute of wall-clock retention, without shortening the product timeout.
            double age=double.Parse(await window.EvaluateForDiagnostics("performance.now()-window.petDiagnostics.interactions.lastKey"),CultureInfo.InvariantCulture);
            await Task.Delay(Math.Max(1,(int)(59800-age)));
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.keyboardWeight===1") == "true", "Keyboard disappeared before 60 seconds");
            await Task.Delay(700);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.keyboardWeight===0 && window.petDiagnostics.keyboard===0") == "true", "Keyboard did not disappear after 60 seconds");
            app.Pet.Capture(Path.Combine(directory,"pet-after-minute.png"));
            await window.EvaluateForDiagnostics("window.petDiagnostics.press()");
            await Task.Delay(60); app.Pet.Notify(6); await Task.Delay(400);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.paused && window.petDiagnostics.interactions.keyboardWeight===0") == "true", "Pause failed to clear typing state");
            await window.EvaluateForDiagnostics("window.petDiagnostics.press()"); app.Pet.Notify(7);
            for(int i=0;i<60;i++) { await Task.Delay(200); if(await window.EvaluateForDiagnostics("!window.petDiagnostics.interactions.busy && !window.petDiagnostics.interactions.manualBusy") == "true") break; }
            Check(await window.EvaluateForDiagnostics("!window.petDiagnostics.interactions.busy && !window.petDiagnostics.interactions.typing && window.petDiagnostics.interactions.keyboardWeight===0") == "true", "Resume replayed stale input or celebration never ended");
            await window.EvaluateForDiagnostics("window.petDiagnostics.press()"); await Task.Delay(300);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.keyboardWeight===1") == "true", "New input failed after resume");
            await window.EvaluateForDiagnostics("clearInterval(window.petDiagnostics.timer)");
            app.Pet.SetInteractions(true,true,true,"all"); await Task.Delay(350);
            Check(await window.EvaluateForDiagnostics("window.petDiagnostics.interactions.keyboardWeight===0") == "true", "Scope change retained old input");
            app.Pet.SetInteractions(false,false,false,"editors"); Check(!window.InputHookInstalled,"Disabled hook retained");
            app.Pet.ResetInteractions(); Check(window.InputHookInstalled,"Hook did not reinstall");
            File.WriteAllText(Path.Combine(directory,"interaction-state.json"),await window.EvaluateForDiagnostics("(()=>{const d=window.petDiagnostics,s=d.interactions;return {eyes:s.eyes,head:s.head,textDrawables:[...s.textDrawables],settings:s.settings,strokes:s.strokeCount};})()"));
        }

        private static void CheckTransparency(Window window)
        {
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            Point? blank = null, body = null;
            for (int y = 75; y < bitmap.PixelHeight - 5; y += 5)
                for (int x = 5; x < bitmap.PixelWidth - 5; x += 5) {
                    byte alpha = pixels[(y * bitmap.PixelWidth + x) * 4 + 3];
                    if (alpha == 0 && !blank.HasValue) blank = new Point(x, y);
                    if (alpha == 255 && !body.HasValue) body = new Point(x, y);
                }
            Check(blank.HasValue && body.HasValue, "Expected fully transparent margins and an opaque character");
            IntPtr own = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            Point blankScreen = window.PointToScreen(blank.Value), bodyScreen = window.PointToScreen(body.Value);
            Check(WindowFromPoint(new NativePoint(blankScreen)) != own, "Transparent margin intercepts desktop hit testing");
            Check(WindowFromPoint(new NativePoint(bodyScreen)) == own, "Character body is not a native mouse target");
        }

        private static async Task Benchmark(App app, MainWindow panel, string directory)
        {
            var report = new StringBuilder("limit,mean_render_fps,samples,seconds,process_tree_cpu_core_percent,summed_working_set_mib\r\n");
            foreach (int limit in new[] { 30, 60, 90, 120 }) {
                panel.SelectFrameLimitForDiagnostics(limit);
                Check(app.Pet.Settings.FrameLimit == limit, "FPS selection did not reach controller");
                Check(!app.Pet.RenderFps.HasValue, "Old FPS was not cleared on selection");
                Check(PetSettings.Load().FrameLimit == limit, "FPS selection was not persisted");
                await Task.Delay(2500);
                var samples = new List<double>();
                EventHandler sample = delegate { if (app.Pet.RenderFps.HasValue) samples.Add(app.Pet.RenderFps.Value); };
                app.Pet.MetricsChanged += sample;
                var start = ProcessTreeSnapshot();
                var clock = Stopwatch.StartNew();
                try { await Task.Delay(5000); }
                finally { app.Pet.MetricsChanged -= sample; }
                var end = ProcessTreeSnapshot();
                clock.Stop();
                Check(samples.Count >= 2, "No live renderer FPS samples for limit " + limit);
                double fps = 0, cpu = 0, memory = 0;
                foreach (double value in samples) fps += value;
                fps /= samples.Count;
                Check(fps > 0 && fps <= limit + 3, "FPS counter is invalid or limiter failed: " + fps);
                foreach (var pair in end) {
                    memory += pair.Value.Item2;
                    Tuple<double, long> before;
                    if (start.TryGetValue(pair.Key, out before)) cpu += Math.Max(0, pair.Value.Item1 - before.Item1);
                }
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:F1},{2},{3:F2},{4:F1},{5:F1}",
                    limit, fps, samples.Count, clock.Elapsed.TotalSeconds, cpu / clock.Elapsed.TotalSeconds * 100, memory / 1048576));
                File.WriteAllText(Path.Combine(directory, "fps-benchmark.csv"), report.ToString());
                Capture(panel, Path.Combine(directory, "console-fps-" + limit + ".png"));
            }
            panel.SelectFrameLimitForDiagnostics(30);
        }

        // Toolhelp snapshots keep measurements scoped to this app and its WebView2
        // descendants; unrelated browser processes are never included.
        private static Dictionary<int, Tuple<double, long>> ProcessTreeSnapshot()
        {
            var parents = new Dictionary<int, int>();
            IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
            if (snapshot == new IntPtr(-1)) throw new InvalidOperationException("Process snapshot failed");
            try {
                var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
                if (Process32First(snapshot, ref entry)) do { parents[(int)entry.Id] = (int)entry.Parent; } while (Process32Next(snapshot, ref entry));
            } finally { CloseHandle(snapshot); }
            var ids = new HashSet<int> { Process.GetCurrentProcess().Id };
            bool changed;
            do { changed = false; foreach (var pair in parents) if (ids.Contains(pair.Value)) changed |= ids.Add(pair.Key); } while (changed);
            var result = new Dictionary<int, Tuple<double, long>>();
            foreach (int id in ids) try {
                using (var process = Process.GetProcessById(id)) result[id] = Tuple.Create(process.TotalProcessorTime.TotalSeconds, process.WorkingSet64);
            } catch (ArgumentException) { } catch (InvalidOperationException) { }
            return result;
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; public NativePoint(Point p) { X = (int)p.X; Y = (int)p.Y; } }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ProcessEntry {
            public uint Size, Usage, Id; public IntPtr Heap; public uint Module, Threads, Parent; public int Priority; public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string File;
        }
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
        [DllImport("kernel32.dll")] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        private static void Capture(Window window, string path)
        {
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
