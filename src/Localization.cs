using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace HorizonFolderHelper {
    static class L {
        static string language="en";
        static readonly Dictionary<string,string[]> messages=new Dictionary<string,string[]> {
            {"WindowTitle",new[]{"Horizon-offine-helper · Prepare and play","Horizon-offine-helper · Подготовка и запуск"}},
            {"Subtitle",new[]{"Minecraft 26.3 from your launcher","Minecraft 26.3 из любого лаунчера"}},
            {"FolderLabel",new[]{"Minecraft 26.3 folder","Папка с Minecraft 26.3"}},
            {"FolderHint",new[]{"Choose a launcher, instance, or version folder.","Подойдёт папка лаунчера, экземпляра или самой версии."}},
            {"Browse",new[]{"Browse…","Выбрать…"}},
            {"DesktopShortcut",new[]{"Create a desktop shortcut","Создать ярлык на рабочем столе"}},
            {"PreparePlay",new[]{"Prepare and play","Подготовить и играть"}},
            {"Play",new[]{"Play","Играть"}},
            {"Cancel",new[]{"Cancel","Отмена"}},
            {"Continue",new[]{"Continue","Продолжить"}},
            {"Details",new[]{"Details","Подробности"}},
            {"Hide",new[]{"Hide","Скрыть"}},
            {"FolderDialog",new[]{"Select a folder containing Minecraft 26.3 files","Выберите папку с файлами Minecraft 26.3"}},
            {"ChooseFolder",new[]{"Choose a folder. The helper will prepare the remaining files.","Выберите папку. Остальные файлы программа подготовит сама."}},
            {"SavedPreparation",new[]{"Your preparation is saved. Play now or choose another folder.","Подготовка сохранена. Можно играть или выбрать другую папку."}},
            {"Stopping",new[]{"Stopping preparation…","Останавливаю подготовку…"}},
            {"Started",new[]{"Horizon is running.","Horizon запущен."}},
            {"Preparing",new[]{"Starting preparation…","Начинаю подготовку…"}},
            {"Cancelled",new[]{"Preparation stopped. You can try again later.","Подготовка остановлена. Её можно повторить позже."}},
            {"Launched",new[]{"Ready — Horizon is running. Next time, use the shortcut or Play.","Готово — Horizon запущен. Для следующего запуска используйте ярлык или кнопку «Играть»."}},
            {"FailedStatus",new[]{"Preparation did not finish. Open Details for more information.","Подготовка не завершена. Откройте подробности для проверки."}},
            {"NetworkError",new[]{"Could not download the required files. Check your connection and try again.","Не удалось загрузить необходимые файлы. Проверьте подключение к интернету и повторите подготовку."}},
            {"AccessError",new[]{"Could not access a required file or folder. Check its permissions and try again.","Нет доступа к нужному файлу или папке. Проверьте права доступа и повторите попытку."}},
            {"MoreDetails",new[]{"Full details are available in Details.","Полная запись доступна в разделе «Подробности»."}},
            {"ErrorTitle",new[]{"Could not prepare Horizon","Не удалось подготовить Horizon"}},
            {"FolderTitle",new[]{"Minecraft folder","Папка Minecraft"}},
            {"SelectExisting",new[]{"Choose an existing folder first.","Сначала выберите существующую папку."}},
            {"LanguageSaveError",new[]{"This language will be used for this session, but the choice could not be saved.\r\n\r\n{0}","Язык выбран для текущего запуска, но сохранить выбор не удалось.\r\n\r\n{0}"}},
            {"ShortcutFailed",new[]{"Could not create the shortcut: {0}. Use Play to launch the game.","Ярлык не создан: {0}. Для запуска используйте кнопку «Играть»."}},
            {"HorizonMissing",new[]{"Horizon is not installed.","Horizon не установлен."}},
            {"SmokeTimeout",new[]{"The test launch did not finish within two minutes.","Проверочный запуск не завершился за две минуты."}},
            {"InvalidFilePath",new[]{"Invalid path in the file description.","Неверный путь в описании файлов."}},
            {"PathEscape",new[]{"A file path points outside the preparation folder.","Путь выходит за пределы папки подготовки."}},
            {"ProcessFailed",new[]{"Horizon exited with error code {0}.","Программа Horizon завершилась с ошибкой {0}."}},
            {"HttpsRequired",new[]{"Only secure HTTPS downloads are allowed.","Разрешена только защищённая загрузка HTTPS."}},
            {"DownloadHashMismatch",new[]{"The downloaded file failed its integrity check: {0}","Загруженный файл не прошёл проверку целостности: {0}"}},
            {"DownloadLauncher",new[]{"Downloading the official Horizon Launcher…","Скачиваю официальный Horizon Launcher…"}},
            {"MissingArchiveHash",new[]{"The official archive does not include a checksum.","У официального архива отсутствует контрольная сумма."}},
            {"LauncherNotInArchive",new[]{"The Horizon archive does not contain the launcher.","В архиве Horizon не найден launcher."}},
            {"NoLauncherArchive",new[]{"No official Horizon archive was found for this Windows platform.","Не найден официальный архив Horizon для этой версии Windows."}},
            {"ArchiveLinksUnsupported",new[]{"Symbolic links in the program archive are not supported.","Ссылки в архиве программы не поддерживаются."}},
            {"InstallHorizon",new[]{"Installing an official Horizon build…","Устанавливаю официальную сборку Horizon…"}},
            {"NoCompatibleBuild",new[]{"No Horizon build for Minecraft 26.3 was found. Install a compatible build using Horizon Launcher.","Не найдена сборка Horizon для Minecraft 26.3. Установите совместимую версию через Horizon Launcher."}},
            {"FetchManifest",new[]{"Fetching Minecraft 26.3 metadata from Mojang…","Получаю описание Minecraft 26.3 с серверов Mojang…"}},
            {"ManifestChanged",new[]{"Minecraft 26.3 metadata has changed. A compatible Horizon build is required.","Описание Minecraft 26.3 обновилось. Нужна совместимая сборка Horizon."}},
            {"FolderMissing",new[]{"The selected folder does not exist.","Выбранная папка не существует."}},
            {"SelectSpecificFolder",new[]{"Choose a more specific launcher or Minecraft 26.3 folder.","Выберите более конкретную папку с лаунчером или версией Minecraft 26.3."}},
            {"FindClient",new[]{"Looking for Minecraft 26.3 files…","Ищу файлы Minecraft 26.3…"}},
            {"ClientMissing",new[]{"The original Minecraft 26.3 client was not found in this folder. Select a launcher, instance, or version folder. If the files have not been downloaded yet, launch 26.3 in your original launcher first.","В этой папке не найден оригинальный клиент Minecraft 26.3. Можно выбрать папку лаунчера, экземпляра или самой версии. Если файлы ещё не скачаны, сначала запустите 26.3 один раз в вашем лаунчере."}},
            {"RequiredFileMissing",new[]{"A required file was not found: {0}","Не найден необходимый файл: {0}"}},
            {"ManifestSaveFailed",new[]{"Could not save the verified version metadata. Try preparing again with an internet connection.","Не удалось сохранить проверенное описание версии. Повторите подготовку с подключением к интернету."}},
            {"CheckLibraries",new[]{"Checking Minecraft libraries…","Проверяю библиотеки Minecraft…"}},
            {"LibraryProgress",new[]{"Libraries: {0} of {1}","Библиотеки: {0} из {1}"}},
            {"InvalidAssetIndex",new[]{"Invalid asset index.","Неверный индекс ресурсов."}},
            {"PrepareAssets",new[]{"Preparing sounds, fonts, and other resources…","Подготавливаю звуки, шрифты и остальные ресурсы…"}},
            {"InvalidAssetHash",new[]{"Invalid resource checksum.","Неверная контрольная сумма ресурса."}},
            {"AssetProgress",new[]{"Resources: {0} of {1}","Ресурсы: {0} из {1}"}},
            {"SelectMinecraftFolder",new[]{"Choose an existing folder containing Minecraft 26.3.","Выберите существующую папку с Minecraft 26.3."}},
            {"CheckHorizon",new[]{"Checking Horizon…","Проверяю Horizon…"}},
            {"HorizonPreparing",new[]{"Horizon is preparing game data. This may take several minutes…","Horizon подготавливает игровые данные. Это может занять несколько минут…"}},
            {"PreparedResourcesMissing",new[]{"Horizon finished preparation, but the prepared resources were not found.","Horizon завершил подготовку, но готовые ресурсы не найдены."}},
            {"PreparationDone",new[]{"Ready. You can now launch Horizon.","Готово. Horizon можно запускать."}},
            {"PrepareFirst",new[]{"Select a folder and complete preparation first.","Сначала выберите папку и выполните подготовку."}}
        };
        public static string Language { get { return language; } }
        public static IEnumerable<string> Keys { get { return messages.Keys; } }
        public static void Use(string value) {
            if(value!="en" && value!="ru") throw new ArgumentException("Supported languages: en, ru.");
            language=value;
        }
        public static string For(string value,string key,params object[] arguments) {
            string text=messages[key][value=="ru"?1:0];
            return arguments.Length==0 ? text : String.Format(CultureInfo.GetCultureInfo(value=="ru"?"ru-RU":"en-US"),text,arguments);
        }
        public static string T(string key,params object[] arguments) { return For(language,key,arguments); }
        public static bool Load(string home) {
            language="en";
            string path=Path.Combine(home,"ui-settings.json"); if(!File.Exists(path)) return false;
            try {
                var data=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path,Encoding.UTF8).TrimStart('\uFEFF')) as Dictionary<string,object>;
                object value; if(data==null || !data.TryGetValue("language",out value) || !(value is string)) return false;
                string saved=(string)value; if(saved!="en" && saved!="ru") return false;
                language=saved; return true;
            } catch(IOException) {return false;} catch(UnauthorizedAccessException) {return false;}
              catch(ArgumentException) {return false;} catch(InvalidOperationException) {return false;}
        }
        public static void Save(string home,string value) {
            if(value!="en" && value!="ru") throw new ArgumentException("Supported languages: en, ru.");
            Directory.CreateDirectory(home);
            File.WriteAllText(Path.Combine(home,"ui-settings.json"),new JavaScriptSerializer().Serialize(new {language=value}),new UTF8Encoding(false));
        }
    }

    sealed class LanguageWindow : Form {
        readonly ComboBox choices=new ComboBox();
        readonly Button proceed=new Button();
        readonly Button cancel=new Button();
        public string SelectedLanguage { get {return choices.SelectedIndex==1?"ru":"en";} }
        public LanguageWindow() {
            Text="Horizon-offine-helper · Language / Язык"; ClientSize=new Size(440,224);
            FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false; ShowInTaskbar=false;
            StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
            BackColor=Color.FromArgb(246,247,251);
            Controls.Add(new Label {Text="Choose your language",Font=new Font("Segoe UI",16,FontStyle.Bold),AutoSize=true,Location=new Point(24,22)});
            Controls.Add(new Label {Text="Выберите язык интерфейса",AutoSize=true,ForeColor=Color.FromArgb(102,104,119),Location=new Point(26,63)});
            choices.DropDownStyle=ComboBoxStyle.DropDownList; choices.Items.AddRange(new object[]{"English","Русский"}); choices.SetBounds(26,101,388,32); choices.SelectedIndex=L.Language=="ru"?1:0; Controls.Add(choices);
            proceed.SetBounds(172,164,116,36); proceed.FlatStyle=FlatStyle.Flat; proceed.FlatAppearance.BorderSize=0; proceed.BackColor=Color.FromArgb(108,67,218); proceed.ForeColor=Color.White; proceed.DialogResult=DialogResult.OK; Controls.Add(proceed);
            cancel.SetBounds(298,164,116,36); cancel.FlatStyle=FlatStyle.Flat; cancel.FlatAppearance.BorderColor=Color.FromArgb(219,220,231); cancel.DialogResult=DialogResult.Cancel; Controls.Add(cancel);
            AcceptButton=proceed; CancelButton=cancel;
            choices.SelectedIndexChanged+=delegate {UpdateLabels();}; UpdateLabels();
        }
        void UpdateLabels() {proceed.Text=L.For(SelectedLanguage,"Continue");cancel.Text=L.For(SelectedLanguage,"Cancel");}
        public static bool Choose(IWin32Window owner) {
            using(var picker=new LanguageWindow()) {
                if(owner!=null) picker.StartPosition=FormStartPosition.CenterParent;
                else picker.ShowInTaskbar=true;
                if(picker.ShowDialog(owner)!=DialogResult.OK) return false;
                L.Use(picker.SelectedLanguage);
                try {L.Save(HelperCore.DefaultHome,L.Language);}
                catch(Exception error) {MessageBox.Show(owner,L.T("LanguageSaveError",error.Message),"Horizon-offine-helper",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
                return true;
            }
        }
    }
}
