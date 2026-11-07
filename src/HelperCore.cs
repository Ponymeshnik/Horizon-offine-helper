using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HorizonFolderHelper {
    public sealed class ReadyGame {
        public string Launcher, Version, VersionDirectory, Resources, SourceFolder, SourceJar, JavaHome;
    }

    public sealed class HelperCore {
        public const string MinecraftVersion = "26.3";
        public readonly string Home;
        public readonly CancellationToken Cancel;
        readonly Action<string> log;
        readonly object logLock = new object();
        readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };
        public string LauncherOverride;
        public bool PortableOnly;
        public HelperCore(string home, Action<string> logger, CancellationToken cancel) {
            Home = Path.GetFullPath(home); log = logger ?? delegate { }; Cancel = cancel;
            Directory.CreateDirectory(Home);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            ServicePointManager.DefaultConnectionLimit=Math.Max(ServicePointManager.DefaultConnectionLimit,6);
        }
        public void Say(string value) { lock(logLock) { log(value); File.AppendAllText(Path.Combine(Home,"helper.log"), DateTime.Now.ToString("s")+" "+value+Environment.NewLine, Encoding.UTF8); } }
        public static string DefaultHome { get { return Environment.GetEnvironmentVariable("HORIZON_FOLDER_HELPER_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HorizonFolderHelper"); } }
        public static string HorizonHome { get { return Environment.GetEnvironmentVariable("HORIZON_LAUNCHER_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"horizon-launcher"); } }
        public static Dictionary<string,object> Obj(object value) { return value as Dictionary<string,object> ?? new Dictionary<string,object>(); }
        public static string Str(Dictionary<string,object> value,string key) { object result; return value.TryGetValue(key,out result) && result != null ? Convert.ToString(result) : ""; }
        public static IEnumerable<object> Arr(Dictionary<string,object> value,string key) { object result; return value.TryGetValue(key,out result) && result is IEnumerable && !(result is string) ? ((IEnumerable)result).Cast<object>() : Enumerable.Empty<object>(); }
        public Dictionary<string,object> ReadJson(string path) { return Obj(json.DeserializeObject(File.ReadAllText(path,Encoding.UTF8).TrimStart('\uFEFF'))); }
        public void WriteJson(string path,object value) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))); File.WriteAllText(path,json.Serialize(value),new UTF8Encoding(false)); }
        public static string Hash(string path,string algorithm) { using(var h=HashAlgorithm.Create(algorithm)) using(var f=File.OpenRead(path)) return BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant(); }
        public static string SafeChild(string root,string relative) {
            if(Path.IsPathRooted(relative)) throw new IOException("Неверный путь в описании файлов.");
            string start=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string result=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
            if(!result.StartsWith(start,StringComparison.OrdinalIgnoreCase)) throw new IOException("Путь выходит за пределы папки подготовки.");
            return result;
        }
        public static string Quote(string value) {
            if(value.Length>0 && !value.Any(c=>Char.IsWhiteSpace(c)||c=='"')) return value;
            var b=new StringBuilder("\""); int slashes=0;
            foreach(char c in value) { if(c=='\\') { slashes++; continue; } if(c=='"') b.Append('\\',slashes*2+1).Append(c); else b.Append('\\',slashes).Append(c); slashes=0; }
            return b.Append('\\',slashes*2).Append('"').ToString();
        }
        public string Run(string executable,IEnumerable<string> arguments,Dictionary<string,string> environment,bool verbose) {
            Cancel.ThrowIfCancellationRequested();
            var start=new ProcessStartInfo(executable,String.Join(" ",arguments.Select(Quote))) { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true };
            start.EnvironmentVariables["HORIZON_LAUNCHER_NO_SELF_UPDATE"]="1";
            if(environment!=null) foreach(var entry in environment) start.EnvironmentVariables[entry.Key]=entry.Value;
            var output=new StringBuilder(); var errors=new StringBuilder();
            using(var process=new Process { StartInfo=start }) {
                process.OutputDataReceived+=(s,e)=> { if(e.Data==null) return; lock(output) output.AppendLine(e.Data); if(verbose && e.Data.Length>0) Say(e.Data); };
                process.ErrorDataReceived+=(s,e)=> { if(e.Data==null) return; lock(errors) errors.AppendLine(e.Data); if(verbose && e.Data.Length>0) Say(e.Data); };
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                while(!process.WaitForExit(200)) if(Cancel.IsCancellationRequested) {
                    try { using(var kill=Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"taskkill.exe"),"/PID "+process.Id+" /T /F") { UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden })) kill.WaitForExit(5000); } catch { try {process.Kill();} catch {} }
                    Cancel.ThrowIfCancellationRequested();
                }
                process.WaitForExit();
                if(process.ExitCode!=0) throw new IOException("Программа Horizon завершилась с ошибкой "+process.ExitCode+".\r\n"+errors.ToString()+output.ToString());
            }
            return output.ToString();
        }
        byte[] GetBytes(string url) {
            Cancel.ThrowIfCancellationRequested();
            if(!url.StartsWith("https://",StringComparison.OrdinalIgnoreCase)) throw new IOException("Разрешена только защищённая загрузка HTTPS.");
            var request=(HttpWebRequest)WebRequest.Create(url); request.UserAgent="HorizonFolderHelper/1.0"; request.Timeout=90000; request.ReadWriteTimeout=90000;
            request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
            using(Cancel.Register(delegate { request.Abort(); })) using(var response=request.GetResponse()) using(var stream=response.GetResponseStream()) using(var memory=new MemoryStream()) {
                byte[] buffer=new byte[81920]; int count; while((count=stream.Read(buffer,0,buffer.Length))>0) { Cancel.ThrowIfCancellationRequested(); memory.Write(buffer,0,count); } return memory.ToArray();
            }
        }
        public Dictionary<string,object> GetJson(string url) { return Obj(json.DeserializeObject(Encoding.UTF8.GetString(GetBytes(url)).TrimStart('\uFEFF'))); }
        void Download(string url,string target,string hash,string algorithm) {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string temporary=target+".download-"+Guid.NewGuid().ToString("N");
            try { File.WriteAllBytes(temporary,GetBytes(url)); if(!String.IsNullOrEmpty(hash) && !String.Equals(Hash(temporary,algorithm),hash,StringComparison.OrdinalIgnoreCase)) throw new IOException("Загруженный файл не прошёл проверку целостности: "+Path.GetFileName(target));
                if(File.Exists(target)) File.Delete(target); File.Move(temporary,target);
            } finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
        public string FindLauncher(bool downloadIfMissing) {
            var candidates=new List<string>();
            if(!String.IsNullOrWhiteSpace(LauncherOverride)) candidates.Add(LauncherOverride);
            if(!PortableOnly) {
                candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"horizon-launcher.exe"));
                candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Horizon Launcher","horizon-launcher.exe"));
            }
            string runtime=Path.Combine(Home,"runtime");
            if(Directory.Exists(runtime)) candidates.AddRange(Directory.GetFiles(runtime,"horizon-launcher.exe",SearchOption.AllDirectories));
            string found=candidates.FirstOrDefault(File.Exists); if(found!=null) return Path.GetFullPath(found);
            if(!downloadIfMissing) return null;
            Say("Скачиваю официальный Horizon Launcher…");
            string assetName=Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64" ? "horizon-launcher-windows-arm64.zip" : "horizon-launcher-windows-x64.zip";
            object releases=json.DeserializeObject(Encoding.UTF8.GetString(GetBytes("https://api.github.com/repos/horizon-rust/horizon/releases?per_page=30")));
            foreach(object release in (IEnumerable)releases) foreach(object a in Arr(Obj(release),"assets")) {
                var asset=Obj(a); if(Str(asset,"name")!=assetName) continue;
                string digest=Str(asset,"digest"); if(!digest.StartsWith("sha256:")) throw new IOException("У официального архива отсутствует контрольная сумма.");
                string archive=Path.Combine(Home,"launcher.zip"); Download(Str(asset,"browser_download_url"),archive,digest.Substring(7),"SHA256");
                string destination=Path.Combine(runtime,"launcher-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(destination);
                ExtractChecked(archive,destination); found=Directory.GetFiles(destination,"horizon-launcher.exe",SearchOption.AllDirectories).FirstOrDefault();
                if(found==null) throw new IOException("В архиве Horizon не найден launcher."); return found;
            }
            throw new IOException("Не найден официальный архив Horizon для этой версии Windows.");
        }
        public static void ExtractChecked(string archive,string destination) {
            using(var zip=ZipFile.OpenRead(archive)) foreach(var entry in zip.Entries) {
                string target=SafeChild(destination,entry.FullName);
                if((entry.ExternalAttributes>>16 & 0xF000)==0xA000) throw new IOException("Ссылки в архиве программы не поддерживаются.");
                if(entry.FullName.EndsWith("/")) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)); entry.ExtractToFile(target,false);
            }
        }
        public ReadyGame FindGame(string launcher,bool install) {
            var versions=Obj(json.DeserializeObject(Run(launcher,new[]{"versions","--installed","--json"},null,false)));
            var tags=new List<string>(); string selected=Str(versions,"selected"); if(selected.Length>0) tags.Add(selected);
            tags.AddRange(Arr(versions,"installed").Select(Convert.ToString).Where(t=>t!=selected).Reverse());
            foreach(string tag in tags) {
                string dir; try { dir=SafeChild(Path.Combine(HorizonHome,"versions"),tag); } catch {continue;}
                string file=Path.Combine(dir,"research-lock.json");
                if(File.Exists(file) && File.Exists(Path.Combine(dir,"horizon-client.exe")) && Str(Obj(ReadJson(file)["minecraft"]),"version")==MinecraftVersion)
                    return new ReadyGame { Launcher=launcher,Version=tag,VersionDirectory=dir,Resources=Path.Combine(dir,".local","horizon-client",MinecraftVersion) };
            }
            if(install) { Say("Устанавливаю официальную сборку Horizon…"); Run(launcher,new[]{"install","latest"},null,true); return FindGame(launcher,false); }
            throw new IOException("Не найдена сборка Horizon для Minecraft 26.3. Установите совместимую версию через Horizon Launcher.");
        }
        public static IEnumerable<string> Parents(string path) {
            var directory=new DirectoryInfo(Path.GetFullPath(path)); int count=0;
            while(directory!=null && directory.Parent!=null && count++<12) { yield return directory.FullName; directory=directory.Parent; }
        }
        IEnumerable<string> Roots(string folder,string jar) {
            return Parents(folder).Concat(jar==null ? Enumerable.Empty<string>() : Parents(Path.GetDirectoryName(jar))).Distinct(StringComparer.OrdinalIgnoreCase);
        }
        public Dictionary<string,object> Manifest(ReadyGame game,string folder) {
            var pins=Obj(ReadJson(Path.Combine(game.VersionDirectory,"research-lock.json"))["minecraft"]);
            var accepted=new HashSet<string>(Arr(pins,"accepted_manifest_sha1").Select(Convert.ToString),StringComparer.OrdinalIgnoreCase); accepted.Add(Str(pins,"manifest_sha1"));
            var candidates=new List<string> { Path.Combine(Home,"minecraft",MinecraftVersion,"versions",MinecraftVersion,MinecraftVersion+".json"),Path.Combine(HorizonHome,"linked-minecraft","versions",MinecraftVersion,MinecraftVersion+".json"),Path.Combine(HorizonHome,"minecraft","versions",MinecraftVersion,MinecraftVersion+".json") };
            foreach(string root in Parents(folder)) candidates.Add(Path.Combine(root,"versions",MinecraftVersion,MinecraftVersion+".json"));
            foreach(string candidate in candidates) if(File.Exists(candidate) && accepted.Contains(Hash(candidate,"SHA1"))) return ReadJson(candidate);
            Say("Получаю описание Minecraft 26.3 с серверов Mojang…");
            var index=GetJson("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
            var version=Arr(index,"versions").Select(Obj).FirstOrDefault(v=>Str(v,"id")==MinecraftVersion);
            if(version==null || !accepted.Contains(Str(version,"sha1"))) throw new IOException("Описание Minecraft 26.3 обновилось. Нужна совместимая сборка Horizon.");
            string output=Path.Combine(Home,"minecraft",MinecraftVersion,"versions",MinecraftVersion,MinecraftVersion+".json");
            Download(Str(version,"url"),output,Str(version,"sha1"),"SHA1"); return ReadJson(output);
        }
        public string FindClient(string folder,string sha,long size) {
            if(!Directory.Exists(folder)) throw new IOException("Выбранная папка не существует.");
            if(new DirectoryInfo(Path.GetFullPath(folder)).Parent==null) throw new IOException("Выберите более конкретную папку с лаунчером или версией Minecraft 26.3.");
            Say("Ищу файлы Minecraft 26.3…");
            var tried=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Func<string,bool> matches=delegate(string file) { Cancel.ThrowIfCancellationRequested(); try { return tried.Add(file) && File.Exists(file) && new FileInfo(file).Length==size && Hash(file,"SHA1")==sha; } catch(IOException) {return false;} catch(UnauthorizedAccessException) {return false;} };
            foreach(string root in Parents(folder)) foreach(string suffix in new[]{"","meta","data","datastore","cache/minecraft"}) {
                string basePath=Path.Combine(root,suffix.Replace('/',Path.DirectorySeparatorChar));
                foreach(string relative in new[]{"versions/26.3/26.3.jar","libraries/com/mojang/minecraft/26.3/minecraft-26.3-client.jar","libraries/net/minecraft/client/26.3/client-26.3.jar","26.3.jar","minecraft-26.3-client.jar","client-26.3.jar","client.jar"}) {
                    string path=Path.Combine(basePath,relative.Replace('/',Path.DirectorySeparatorChar)); if(matches(path)) return path;
                }
            }
            var queue=new Queue<Tuple<string,int>>(); queue.Enqueue(Tuple.Create(Path.GetFullPath(folder),0)); int visited=0;
            var skip=new HashSet<string>(new[]{"objects","saves","logs","mods","resourcepacks","shaderpacks","java","runtime","jre","jdk","node_modules",".git"},StringComparer.OrdinalIgnoreCase);
            while(queue.Count>0 && visited++<10000) {
                Cancel.ThrowIfCancellationRequested(); var item=queue.Dequeue();
                try {
                    foreach(string file in Directory.GetFiles(item.Item1,"*.jar")) if(matches(file)) return file;
                    if(item.Item2<9) foreach(string child in Directory.GetDirectories(item.Item1)) if(!skip.Contains(Path.GetFileName(child)) && (File.GetAttributes(child)&FileAttributes.ReparsePoint)==0) queue.Enqueue(Tuple.Create(child,item.Item2+1));
                } catch(UnauthorizedAccessException) {} catch(IOException) {}
            }
            throw new IOException("В этой папке не найден оригинальный клиент Minecraft 26.3. Можно выбрать папку лаунчера, экземпляра или самой версии. Если файлы ещё не скачаны, сначала запустите 26.3 один раз в вашем лаунчере.");
        }
        List<string> Stores(string folder,string jar,string kind) {
            var values=new List<string>(); foreach(string root in Roots(folder,jar)) foreach(string sub in new[]{kind,"meta/"+kind,"data/"+kind,"datastore/"+kind,"cache/minecraft/"+kind,"minecraft/"+kind,".minecraft/"+kind}) { string candidate=Path.Combine(root,sub.Replace('/',Path.DirectorySeparatorChar)); if(Directory.Exists(candidate)) values.Add(candidate); }
            return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateHardLink(string newName,string existingName,IntPtr attributes);
        void Materialize(IEnumerable<string> sources,string target,string sha,string url) {
            Cancel.ThrowIfCancellationRequested();
            if(File.Exists(target) && Hash(target,"SHA1")==sha) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            foreach(string source in sources) if(File.Exists(source) && Hash(source,"SHA1")==sha) {
                if(File.Exists(target)) File.Delete(target);
                if(!CreateHardLink(target,source,IntPtr.Zero)) File.Copy(source,target,false); return;
            }
            if(String.IsNullOrEmpty(url)) throw new IOException("Не найден необходимый файл: "+Path.GetFileName(target));
            Download(url,target,sha,"SHA1");
        }
        public string Normalize(ReadyGame game,string folder,string jar,Dictionary<string,object> manifest) {
            string stage=Path.Combine(Home,"minecraft",MinecraftVersion);
            var pins=Obj(ReadJson(Path.Combine(game.VersionDirectory,"research-lock.json"))["minecraft"]);
            string jarSha=Str(pins,"client_sha1");
            Materialize(new[]{jar},Path.Combine(stage,"versions",MinecraftVersion,MinecraftVersion+".jar"),jarSha,null);
            string manifestFile=Path.Combine(stage,"versions",MinecraftVersion,MinecraftVersion+".json");
            // Preserve the original official JSON bytes, whose hash Horizon pins.
            var pinHashes=new HashSet<string>(Arr(pins,"accepted_manifest_sha1").Select(Convert.ToString),StringComparer.OrdinalIgnoreCase); pinHashes.Add(Str(pins,"manifest_sha1"));
            if(!File.Exists(manifestFile) || !pinHashes.Contains(Hash(manifestFile,"SHA1"))) {
                var candidates=new List<string> { Path.Combine(HorizonHome,"linked-minecraft","versions",MinecraftVersion,MinecraftVersion+".json"),Path.Combine(HorizonHome,"minecraft","versions",MinecraftVersion,MinecraftVersion+".json") };
                foreach(string root in Roots(folder,jar)) candidates.Add(Path.Combine(root,"versions",MinecraftVersion,MinecraftVersion+".json"));
                string source=candidates.FirstOrDefault(p=>File.Exists(p)&&pinHashes.Contains(Hash(p,"SHA1")));
                if(source!=null) { if(File.Exists(manifestFile)) File.Delete(manifestFile); File.Copy(source,manifestFile); }
                else throw new IOException("Не удалось сохранить проверенное описание версии. Повторите подготовку с подключением к интернету.");
            }
            var libraryRoots=Stores(folder,jar,"libraries");
            Say("Проверяю библиотеки Minecraft…");
            var libraries=Arr(manifest,"libraries").Select(Obj).ToArray(); int libraryCount=0;
            foreach(var lib in libraries) {
                var downloads=Obj(lib.ContainsKey("downloads")?lib["downloads"]:null); var artifact=Obj(downloads.ContainsKey("artifact")?downloads["artifact"]:null);
                string relative=Str(artifact,"path"); if(relative.Length==0) continue;
                Materialize(libraryRoots.Select(r=>SafeChild(r,relative)),SafeChild(Path.Combine(stage,"libraries"),relative),Str(artifact,"sha1"),Str(artifact,"url"));
                libraryCount++; if(libraryCount%10==0) Say("Библиотеки: "+libraryCount+" из "+libraries.Length);
            }
            var asset=Obj(manifest["assetIndex"]); string assetId=Str(asset,"id"); if(!Regex.IsMatch(assetId,"^[A-Za-z0-9_.-]+$") || assetId.Contains("..")) throw new IOException("Неверный индекс ресурсов.");
            var assetRoots=Stores(folder,jar,"assets"); string assetFile=Path.Combine(stage,"assets","indexes",assetId+".json");
            var acceptedAssets=new HashSet<string>(Arr(pins,"accepted_asset_index_sha1").Select(Convert.ToString),StringComparer.OrdinalIgnoreCase); acceptedAssets.Add(Str(asset,"sha1"));
            if(!File.Exists(assetFile) || !acceptedAssets.Contains(Hash(assetFile,"SHA1"))) {
                string original=assetRoots.Select(r=>Path.Combine(r,"indexes",assetId+".json")).FirstOrDefault(p=>File.Exists(p)&&acceptedAssets.Contains(Hash(p,"SHA1")));
                if(original!=null) { Directory.CreateDirectory(Path.GetDirectoryName(assetFile)); if(File.Exists(assetFile)) File.Delete(assetFile); File.Copy(original,assetFile); }
                else Download(Str(asset,"url"),assetFile,Str(asset,"sha1"),"SHA1");
            }
            var objects=Obj(ReadJson(assetFile)["objects"]); int complete=0;
            var hashes=objects.Values.Select(value=>Str(Obj(value),"hash")).Distinct(StringComparer.Ordinal).ToArray();
            Say("Подготавливаю звуки, шрифты и остальные ресурсы…");
            Parallel.ForEach(hashes,new ParallelOptions { MaxDegreeOfParallelism=4,CancellationToken=Cancel },delegate(string hash) {
                if(!Regex.IsMatch(hash,"^[0-9a-f]{40}$")) throw new IOException("Неверная контрольная сумма ресурса.");
                string relative=hash.Substring(0,2)+"/"+hash;
                Materialize(assetRoots.Select(r=>SafeChild(Path.Combine(r,"objects"),relative)),SafeChild(Path.Combine(stage,"assets","objects"),relative),hash,"https://resources.download.minecraft.net/"+relative);
                int count=Interlocked.Increment(ref complete); if(count%500==0 || count==hashes.Length) Say("Ресурсы: "+count+" из "+hashes.Length);
            });
            return stage;
        }
        public string FindJava(string folder,string jar) {
            var candidates=new List<string>(); string configured=Environment.GetEnvironmentVariable("JAVA_HOME"); if(!String.IsNullOrEmpty(configured)) candidates.Add(configured);
            ReadyGame previous=Saved(); if(previous!=null && !String.IsNullOrEmpty(previous.JavaHome)) candidates.Add(previous.JavaHome);
            foreach(string root in Roots(folder,jar)) foreach(string name in new[]{"java","runtime","jdk","jre"}) { string path=Path.Combine(root,name); if(Directory.Exists(path)) candidates.Add(path); }
            foreach(string path in new[]{Path.Combine(HorizonHome,"minecraft","runtime"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),".minecraft","runtime"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"PrismLauncher","java"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"prismlaunch","java")}) if(Directory.Exists(path)) candidates.Add(path);
            foreach(string top in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}) foreach(string name in new[]{"Java","Eclipse Adoptium","Microsoft","Amazon Corretto"}) { string path=Path.Combine(top,name); if(Directory.Exists(path)) candidates.Add(path); }
            var queue=new Queue<Tuple<string,int>>(candidates.Distinct().Select(p=>Tuple.Create(p,0))); int seen=0;
            while(queue.Count>0 && seen++<1000) { var item=queue.Dequeue(); try {
                string release=Path.Combine(item.Item1,"release");
                if(File.Exists(Path.Combine(item.Item1,"bin","java.exe")) && File.Exists(Path.Combine(item.Item1,"bin","javac.exe")) && File.Exists(release) && Regex.IsMatch(File.ReadAllText(release),"JAVA_VERSION=\"25(?:[.\"]|$)")) return item.Item1;
                if(item.Item2<5) foreach(string child in Directory.GetDirectories(item.Item1)) if(!new[]{"lib","bin","legal","jmods","include"}.Contains(Path.GetFileName(child))) queue.Enqueue(Tuple.Create(child,item.Item2+1));
            } catch(IOException) {} catch(UnauthorizedAccessException) {} }
            return null;
        }
        public ReadyGame Prepare(string folder) {
            folder=Path.GetFullPath(folder.Trim().Trim('"')); if(!Directory.Exists(folder)) throw new IOException("Выберите существующую папку с Minecraft 26.3.");
            Say("Проверяю Horizon…"); string launcher=FindLauncher(true); ReadyGame game=FindGame(launcher,true);
            var pins=Obj(ReadJson(Path.Combine(game.VersionDirectory,"research-lock.json"))["minecraft"]);
            var manifest=Manifest(game,folder); var client=Obj(Obj(manifest["downloads"])["client"]);
            game.SourceJar=FindClient(folder,Str(pins,"client_sha1"),Convert.ToInt64(client["size"])); game.SourceFolder=folder;
            string stage=Normalize(game,folder,game.SourceJar,manifest);
            game.JavaHome=FindJava(folder,game.SourceJar);
            var environment=new Dictionary<string,string>(); if(game.JavaHome!=null) { environment["JAVA_HOME"]=game.JavaHome; environment["PATH"]=Path.Combine(game.JavaHome,"bin")+Path.PathSeparator+Environment.GetEnvironmentVariable("PATH"); }
            Say("Horizon подготавливает игровые данные. Это может занять несколько минут…");
            Run(launcher,new[]{"prepare","--minecraft",stage,"--version",game.Version,"--json"},environment,true);
            if(!File.Exists(Path.Combine(game.Resources,"horizon-resources.json"))) throw new IOException("Horizon завершил подготовку, но готовые ресурсы не найдены.");
            WriteJson(Path.Combine(Home,"config.json"),game); Say("Готово. Horizon можно запускать."); return game;
        }
        public ReadyGame Saved() {
            string config=Path.Combine(Home,"config.json"); if(!File.Exists(config)) return null;
            try { return json.Deserialize<ReadyGame>(File.ReadAllText(config,Encoding.UTF8)); } catch {return null;}
        }
        public Process Launch(ReadyGame game,string testHome) {
            if(game==null || !File.Exists(Path.Combine(game.VersionDirectory,"horizon-client.exe")) || !File.Exists(Path.Combine(game.Resources,"horizon-resources.json"))) throw new IOException("Сначала выберите папку и выполните подготовку.");
            string gameHome=testHome ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"horizon"); Directory.CreateDirectory(gameHome);
            var arguments=new List<string> { "--resources",game.Resources,"--game-dir",gameHome };
            if(testHome!=null) arguments.AddRange(new[]{"--home",gameHome,"--frames","90","--smoke-report",Path.Combine(gameHome,"smoke.json"),"--width","960","--height","540"});
            var start=new ProcessStartInfo(Path.Combine(game.VersionDirectory,"horizon-client.exe"),String.Join(" ",arguments.Select(Quote))) { UseShellExecute=false,WorkingDirectory=gameHome,CreateNoWindow=true };
            if(testHome!=null) { start.EnvironmentVariables["HORIZON_HOME"]=testHome; start.EnvironmentVariables["HORIZON_LAUNCHER_HOME"]=Path.Combine(testHome,"launcher"); start.EnvironmentVariables["HORIZON_NO_AUDIO"]="1"; }
            return Process.Start(start);
        }
        public static void Shortcut(string path,string helper) {
            Type shellType=Type.GetTypeFromProgID("WScript.Shell"); dynamic shell=Activator.CreateInstance(shellType); dynamic link=shell.CreateShortcut(path);
            link.TargetPath=helper; link.Arguments="--play"; link.WorkingDirectory=Path.GetDirectoryName(helper); link.Description="Horizon 26.3"; link.Save();
            Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell);
        }
    }
}
