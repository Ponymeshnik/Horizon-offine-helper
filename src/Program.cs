using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Horizon-offine-helper")]
[assembly: AssemblyDescription("Prepare and launch Horizon using Minecraft 26.3 files from your launcher")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace HorizonFolderHelper {
    static class Program {
        [STAThread] static int Main(string[] args) {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);
            string report=null;
            try {
                if(args.Length>1 && args[0]=="--echo-args") { File.WriteAllText(args[1],new JavaScriptSerializer().Serialize(args.Skip(2).ToArray())); return 0; }
                if(args.Length>1 && args[0]=="--wait-test") { File.WriteAllText(args[1],Process.GetCurrentProcess().Id.ToString()); Thread.Sleep(30000); return 0; }
                for(int i=0;i+1<args.Length;i++) if(args[i]=="--report") report=args[i+1];
                bool languageSaved=L.Load(HelperCore.DefaultHome);
                var core=new HelperCore(HelperCore.DefaultHome,null,CancellationToken.None);
                if(args.Length>0 && args[0]=="--self-test") return SelfTest.Run(core,report);
                if(args.Length>1 && args[0]=="--probe") {
                    string launcher=core.FindLauncher(false); if(launcher==null) throw new IOException(L.T("HorizonMissing"));
                    ReadyGame game=core.FindGame(launcher,false); var manifest=core.Manifest(game,args[1]);
                    var pins=HelperCore.Obj(core.ReadJson(Path.Combine(game.VersionDirectory,"research-lock.json"))["minecraft"]);
                    var client=HelperCore.Obj(HelperCore.Obj(manifest["downloads"])["client"]);
                    string jar=core.FindClient(args[1],HelperCore.Str(pins,"client_sha1"),Convert.ToInt64(client["size"]));
                    core.WriteJson(report,new { ok=true,folder=Path.GetFullPath(args[1]),jar=jar,java=core.FindJava(args[1],jar),version=game.Version }); return 0;
                }
                if(args.Length>1 && args[0]=="--prepare-only") { ReadyGame game=core.Prepare(args[1]); core.WriteJson(report,new {ok=true,game=game}); return 0; }
                if(args.Length>0 && args[0]=="--bootstrap-only") { core.PortableOnly=args.Contains("--portable"); string launcher=core.FindLauncher(true); core.WriteJson(report,new {ok=true,launcher=launcher}); return 0; }
                if(args.Length>1 && args[0]=="--game-smoke") {
                    using(var game=core.Launch(core.Saved(),Path.GetFullPath(args[1]))) {
                        if(!game.WaitForExit(120000)) {game.Kill(); throw new IOException(L.T("SmokeTimeout"));}
                        core.WriteJson(report,new {ok=game.ExitCode==0,exit_code=game.ExitCode,smoke=Path.Combine(Path.GetFullPath(args[1]),"smoke.json")}); return game.ExitCode==0?0:1;
                    }
                }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                if(args.Contains("--play")) { try { core.Launch(core.Saved(),null); return 0; } catch {} }
                if(!languageSaved && !LanguageWindow.Choose(null)) return 0;
                Application.Run(new MainWindow()); return 0;
            } catch(Exception error) {
                Exception actual=error.GetBaseException();
                if(report!=null) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))); File.WriteAllText(report,new JavaScriptSerializer().Serialize(new {ok=false,error=actual.Message,detail=actual.ToString()}),Encoding.UTF8); }
                else MessageBox.Show(actual.Message,L.T("ErrorTitle"),MessageBoxButtons.OK,MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    sealed class MainWindow : Form {
        readonly TextBox source=new TextBox(); readonly Button browse=new Button();
        readonly Button prepare=new Button(); readonly Button play=new Button(); readonly Button cancel=new Button();
        readonly Label status=new Label(); readonly ProgressBar progress=new ProgressBar(); readonly TextBox details=new TextBox();
        readonly CheckBox desktop=new CheckBox(); readonly LinkLabel expand=new LinkLabel();
        readonly Label subtitle=new Label(); readonly Label folderLabel=new Label(); readonly Label hint=new Label();
        readonly LinkLabel language=new LinkLabel();
        string statusKey="ChooseFolder";
        CancellationTokenSource cancellation; ReadyGame ready; bool busy,closingAfterCancel;
        readonly Color purple=Color.FromArgb(108,67,218);
        public MainWindow() {
            Text="Horizon-offine-helper"; StartPosition=FormStartPosition.CenterScreen; ClientSize=new Size(780,476); MinimumSize=new Size(796,515);
            Font=new Font("Segoe UI",10); BackColor=Color.FromArgb(246,247,251); AutoScaleMode=AutoScaleMode.Dpi;
            var title=new Label { Text="Horizon-offine-helper",Font=new Font("Segoe UI",24,FontStyle.Bold),ForeColor=Color.FromArgb(31,31,45),AutoSize=true,Location=new Point(28,22) }; Controls.Add(title);
            subtitle.AutoSize=true; subtitle.ForeColor=Color.FromArgb(102,104,119); subtitle.Location=new Point(31,77); Controls.Add(subtitle);
            language.Text="English / Русский"; language.SetBounds(590,39,160,24); language.TextAlign=ContentAlignment.MiddleRight; language.Anchor=AnchorStyles.Top|AnchorStyles.Right; language.LinkColor=purple; Controls.Add(language);
            var card=new Panel {BackColor=Color.White,Location=new Point(28,119),Size=new Size(724,139),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right}; Controls.Add(card);
            folderLabel.AutoSize=true; folderLabel.Location=new Point(18,17); folderLabel.Font=new Font("Segoe UI",10,FontStyle.Bold); card.Controls.Add(folderLabel);
            source.SetBounds(18,49,566,30); source.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right; source.AutoCompleteMode=AutoCompleteMode.SuggestAppend; source.AutoCompleteSource=AutoCompleteSource.FileSystemDirectories; card.Controls.Add(source);
            browse.SetBounds(596,46,108,33); browse.Anchor=AnchorStyles.Top|AnchorStyles.Right; browse.FlatStyle=FlatStyle.Flat; browse.FlatAppearance.BorderColor=Color.FromArgb(219,220,231); card.Controls.Add(browse);
            hint.ForeColor=Color.FromArgb(111,113,127); hint.AutoSize=true; hint.Location=new Point(18,96); card.Controls.Add(hint);
            desktop.Checked=true; desktop.AutoSize=true; desktop.Location=new Point(31,276); Controls.Add(desktop);
            status.ForeColor=Color.FromArgb(94,96,111); status.SetBounds(31,314,718,43); status.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top; Controls.Add(status);
            progress.SetBounds(31,363,718,5); progress.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top; progress.Style=ProgressBarStyle.Continuous; Controls.Add(progress);
            prepare.SetBounds(31,389,249,43); prepare.FlatStyle=FlatStyle.Flat; prepare.FlatAppearance.BorderSize=0; prepare.BackColor=purple; prepare.ForeColor=Color.White; prepare.Font=new Font("Segoe UI",10,FontStyle.Bold); Controls.Add(prepare);
            play.SetBounds(291,389,118,43); play.Enabled=false; play.FlatStyle=FlatStyle.Flat; play.FlatAppearance.BorderColor=Color.FromArgb(219,220,231); Controls.Add(play);
            cancel.SetBounds(420,389,108,43); cancel.Enabled=false; cancel.FlatStyle=FlatStyle.Flat; cancel.FlatAppearance.BorderColor=Color.FromArgb(219,220,231); Controls.Add(cancel);
            expand.SetBounds(622,401,127,24); expand.TextAlign=ContentAlignment.MiddleRight; expand.Anchor=AnchorStyles.Top|AnchorStyles.Right; expand.LinkColor=purple; Controls.Add(expand);
            details.Multiline=true; details.ReadOnly=true; details.ScrollBars=ScrollBars.Vertical; details.Font=new Font("Consolas",8); details.BackColor=Color.White; details.BorderStyle=BorderStyle.FixedSingle; details.SetBounds(31,455,718,130); details.Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top; details.Visible=false; Controls.Add(details);
            browse.Click+=delegate { using(var dialog=new FolderBrowserDialog {Description=L.T("FolderDialog"),ShowNewFolderButton=false,SelectedPath=Directory.Exists(source.Text)?source.Text:""}) if(dialog.ShowDialog(this)==DialogResult.OK) source.Text=dialog.SelectedPath; };
            language.LinkClicked+=delegate {if(!busy && LanguageWindow.Choose(this)) ApplyLanguage();};
            prepare.Click+=delegate { StartPreparation(); };
            cancel.Click+=delegate { if(cancellation!=null) {cancellation.Cancel(); SetStatus("Stopping");} };
            play.Click+=delegate { try { new HelperCore(HelperCore.DefaultHome,Log,CancellationToken.None).Launch(ready,null); SetStatus("Started"); } catch(Exception e) {ShowError(e);} };
            expand.LinkClicked+=delegate { details.Visible=!details.Visible; ClientSize=new Size(ClientSize.Width,details.Visible?609:476); expand.Text=L.T(details.Visible?"Hide":"Details"); };
            FormClosing+=delegate(object sender,FormClosingEventArgs e) {if(busy && cancellation!=null) {e.Cancel=true;closingAfterCancel=true;cancellation.Cancel();SetStatus("Stopping");}};
            var core=new HelperCore(HelperCore.DefaultHome,null,CancellationToken.None); ready=core.Saved();
            if(ready!=null) {source.Text=ready.SourceFolder; play.Enabled=File.Exists(Path.Combine(ready.Resources,"horizon-resources.json")); if(play.Enabled) statusKey="SavedPreparation";}
            else foreach(string initial in new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"prismlaunch"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"PrismLauncher"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),".minecraft")}) if(Directory.Exists(initial)) {source.Text=initial;break;}
            ApplyLanguage();
        }
        void SetStatus(string key) {statusKey=key;status.Text=L.T(key);}
        void ApplyLanguage() {
            Text=L.T("WindowTitle"); subtitle.Text=L.T("Subtitle");folderLabel.Text=L.T("FolderLabel");hint.Text=L.T("FolderHint");
            browse.Text=L.T("Browse");desktop.Text=L.T("DesktopShortcut");prepare.Text=L.T("PreparePlay");play.Text=L.T("Play");cancel.Text=L.T("Cancel");
            expand.Text=L.T(details.Visible?"Hide":"Details");status.Text=L.T(statusKey);
        }
        void Log(string message) {
            if(IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)delegate { if(IsDisposed) return; details.AppendText(message+Environment.NewLine); if(details.TextLength>18000) details.Text=details.Text.Substring(details.TextLength-12000); details.SelectionStart=details.TextLength; details.ScrollToCaret();
            });
        }
        void Progress(string message) {if(IsDisposed || !IsHandleCreated) return;BeginInvoke((Action)delegate {if(!IsDisposed && busy && cancellation!=null && !cancellation.IsCancellationRequested) status.Text=message;});}
        void SetBusy(bool value) { busy=value; prepare.Enabled=!value; browse.Enabled=!value; source.ReadOnly=value; desktop.Enabled=!value; language.Enabled=!value; play.Enabled=!value&&ready!=null; cancel.Enabled=value; progress.Style=value?ProgressBarStyle.Marquee:ProgressBarStyle.Continuous; progress.Value=0; }
        void ShowError(Exception e) {
            SetStatus("FailedStatus");
            details.AppendText(Environment.NewLine+e.ToString()+Environment.NewLine);
            Exception actual=e.GetBaseException();
            string message=actual is System.Net.WebException ? L.T("NetworkError") : actual is UnauthorizedAccessException ? L.T("AccessError") : actual.Message;
            if(message.Length>700) message=message.Substring(0,700)+"…\r\n\r\n"+L.T("MoreDetails");
            MessageBox.Show(this,message,L.T("ErrorTitle"),MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
        void StartPreparation() {
            if(busy) return; if(!Directory.Exists(source.Text.Trim().Trim('"'))) {MessageBox.Show(this,L.T("SelectExisting"),L.T("FolderTitle"),MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            string folder=source.Text; bool shortcut=desktop.Checked; cancellation=new CancellationTokenSource(); var token=cancellation.Token; SetBusy(true); SetStatus("Preparing");
            Task.Run(delegate { var core=new HelperCore(HelperCore.DefaultHome,Log,token,Progress); ReadyGame result=core.Prepare(folder);
                token.ThrowIfCancellationRequested();
                if(shortcut) { string stable=Path.Combine(HelperCore.DefaultHome,"Horizon-offine-helper.exe"); string current=Application.ExecutablePath;
                    try {
                        if(!String.Equals(Path.GetFullPath(current),Path.GetFullPath(stable),StringComparison.OrdinalIgnoreCase) && (!File.Exists(stable)||HelperCore.Hash(stable,"SHA256")!=HelperCore.Hash(current,"SHA256"))) File.Copy(current,stable,true);
                        HelperCore.Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Horizon 26.3.lnk"),stable);
                    } catch(Exception shortcutError) { core.Say(L.T("ShortcutFailed",shortcutError.Message)); }
                }
                token.ThrowIfCancellationRequested(); core.Launch(result,null); return result;
            }).ContinueWith(task=> { if(IsDisposed || !IsHandleCreated) return; BeginInvoke((Action)delegate {
                SetBusy(false); if(token.IsCancellationRequested) SetStatus("Cancelled");
                else if(task.IsFaulted) ShowError(task.Exception); else {ready=task.Result;play.Enabled=true;SetStatus("Launched");}
                cancellation.Dispose(); cancellation=null;
                if(closingAfterCancel) Close();
            }); });
        }
    }

    static class SelfTest {
        public static int Run(HelperCore core,string report) {
            var results=new List<string>(); string root=Path.Combine(core.Home,"self-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            Action<string,bool> check=delegate(string name,bool value) {if(!value) throw new Exception("Self-test: "+name);results.Add(name);};
            byte[] bytes=Encoding.UTF8.GetBytes("local source fixture 26.3"); string source=Path.Combine(root,"Лаунчер % ! &","unusual cache","renamed.jar"); Directory.CreateDirectory(Path.GetDirectoryName(source)); File.WriteAllBytes(source,bytes); string hash=HelperCore.Hash(source,"SHA1");
            check("arbitrary nested launcher layout",core.FindClient(Path.Combine(root,"Лаунчер % ! &"),hash,bytes.Length)==source);
            check("direct version folder",core.FindClient(Path.GetDirectoryName(source),hash,bytes.Length)==source);
            string official=Path.Combine(root,"official","versions","26.3","26.3.jar"); Directory.CreateDirectory(Path.GetDirectoryName(official));File.Copy(source,official);
            check("official layout from version folder",core.FindClient(Path.GetDirectoryName(official),hash,bytes.Length)==official);
            string prism=Path.Combine(root,"prism","libraries","com","mojang","minecraft","26.3","minecraft-26.3-client.jar");Directory.CreateDirectory(Path.GetDirectoryName(prism));File.Copy(source,prism);
            string instance=Path.Combine(root,"prism","instances","26.3","minecraft");Directory.CreateDirectory(instance);
            check("Prism instance resolves shared libraries",core.FindClient(instance,hash,bytes.Length)==prism);
            bool rejected=false;try {core.FindClient(Path.GetDirectoryName(source),new string('0',40),bytes.Length);}catch(IOException){rejected=true;}check("wrong client hash rejected",rejected);
            rejected=false;try {core.FindClient(Path.GetPathRoot(root),hash,bytes.Length);}catch(IOException){rejected=true;}check("whole drive scan rejected",rejected);
            rejected=false;try {HelperCore.SafeChild(root,"../escape.txt");}catch(IOException){rejected=true;}check("parent path escape rejected",rejected);
            rejected=false;try {HelperCore.SafeChild(root,Path.GetPathRoot(root)+"escape.txt");}catch(IOException){rejected=true;}check("absolute path escape rejected",rejected);
            string zip=Path.Combine(root,"bad.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){using(var writer=new StreamWriter(archive.CreateEntry("../escaped.txt").Open())) writer.Write("not allowed");}
            rejected=false;try {HelperCore.ExtractChecked(zip,Path.Combine(root,"extract"));}catch(IOException){rejected=true;}check("ZIP path escape rejected",rejected&&!File.Exists(Path.Combine(root,"escaped.txt")));
            var arguments=new[]{"обычная строка","C:\\folder with spaces\\","a\"b","%PATH% ! & (test)",""}; string echo=Path.Combine(root,"echo.json");
            core.Run(Application.ExecutablePath,new[]{"--echo-args",echo}.Concat(arguments),null,false);
            string[] echoed=new JavaScriptSerializer().Deserialize<string[]>(File.ReadAllText(echo));check("Windows arguments roundtrip without shell",arguments.SequenceEqual(echoed));
            var config=new ReadyGame {Version="test",SourceFolder="Папка с пробелами",Resources="resources"};core.WriteJson(Path.Combine(root,"config.json"),config);
            var roundtrip=new JavaScriptSerializer().Deserialize<ReadyGame>(File.ReadAllText(Path.Combine(root,"config.json")));check("configuration roundtrip",roundtrip.SourceFolder==config.SourceFolder&&roundtrip.Version==config.Version);
            string shortcut=Path.Combine(root,"Ярлык % ! &.lnk");HelperCore.Shortcut(shortcut,Application.ExecutablePath);
            dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));dynamic saved=shell.CreateShortcut(shortcut);
            check("shortcut keeps executable path and play argument",String.Equals((string)saved.TargetPath,Application.ExecutablePath,StringComparison.OrdinalIgnoreCase)&&(string)saved.Arguments=="--play");
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(saved);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            string childMarker=Path.Combine(root,"cancel-child.txt");bool cancelled=false;using(var cancellation=new CancellationTokenSource()) {
                cancellation.CancelAfter(700);var cancellable=new HelperCore(Path.Combine(root,"cancel"),null,cancellation.Token);
                try {cancellable.Run(Application.ExecutablePath,new[]{"--wait-test",childMarker},null,false);}catch(OperationCanceledException){cancelled=true;}
            }
            bool childStopped=true;if(File.Exists(childMarker)){int pid=Int32.Parse(File.ReadAllText(childMarker));try{using(var child=Process.GetProcessById(pid)){childStopped=child.HasExited;}}catch(ArgumentException){childStopped=true;}}
            check("cancellation terminates only its child process",cancelled&&childStopped);
            string originalLanguage=L.Language;
            try {
                string preferences=Path.Combine(root,"language preferences");Directory.CreateDirectory(preferences);
                string gameConfig=Path.Combine(preferences,"config.json");File.Copy(Path.Combine(root,"config.json"),gameConfig);
                string configHash=HelperCore.Hash(gameConfig,"SHA256");
                L.Use("ru");check("first launch defaults to English",!L.Load(preferences)&&L.Language=="en");
                L.Save(preferences,"ru");L.Use("en");
                check("language persists without changing game configuration",L.Load(preferences)&&L.Language=="ru"&&HelperCore.Hash(gameConfig,"SHA256")==configHash);
                File.WriteAllText(Path.Combine(preferences,"ui-settings.json"),"{invalid");
                check("corrupt language preference falls back to English",!L.Load(preferences)&&L.Language=="en");
                File.WriteAllText(Path.Combine(preferences,"ui-settings.json"),"{\"language\":\"unknown\"}");L.Use("ru");
                check("unsupported language preference falls back to English",!L.Load(preferences)&&L.Language=="en");
                var progress=new List<string>();var localized=new HelperCore(Path.Combine(root,"localized"),null,CancellationToken.None,m=>progress.Add(m));
                var errors=new List<string>();
                foreach(string choice in new[]{"en","ru"}) {
                    L.Use(choice);localized.FindClient(Path.GetDirectoryName(source),hash,bytes.Length);
                    try {localized.FindClient(Path.Combine(root,"missing"),hash,bytes.Length);}catch(IOException error){errors.Add(error.Message);}
                }
                check("preparation progress and errors follow the selected language",progress.Count==2&&progress[0]!=progress[1]&&errors.Count==2&&errors[0]!=errors[1]&&!System.Text.RegularExpressions.Regex.IsMatch(progress[0]+errors[0],"[А-Яа-яЁё]")&&System.Text.RegularExpressions.Regex.IsMatch(progress[1]+errors[1],"[А-Яа-яЁё]"));
                bool complete=true;foreach(string choice in new[]{"en","ru"}) foreach(string key in L.Keys) {
                    string translated=L.For(choice,key,17,29);complete=complete&&!String.IsNullOrWhiteSpace(translated)&&!translated.Contains("{0}")&&!translated.Contains("{1}");
                }
                check("both languages contain valid formatted messages",complete);
            } finally {L.Use(originalLanguage);}
            core.WriteJson(report,new {ok=true,passed=results.Count,checks=results,fixture=root});return 0;
        }
    }
}
