using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using EasyConsole;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using PathHinter;
using Prod_O_Meter.Properties;
using Console = Colorful.Console;
using SearchOption = Microsoft.VisualBasic.FileIO.SearchOption;

namespace Prod_O_Meter
{
    internal class Program
    {
        private static Settings Settings => Settings.Default;
        private static Menu MainMenu;

        private static void Main(string[] args)
        {
            // FixPaths();

            MainMenu = new Menu();

            MainMenu.Add("Add project path", AddPathCallback);

            if (string.IsNullOrEmpty(Settings.ClocPath))
                MainMenu.Add("Set CLOC path", () => SetClocPath(false));
            else
                MainMenu.Add("Change CLOC path", () => SetClocPath(true));

            MainMenu.Add("List projects paths", ListProjectPaths)
                    .Add("Dump projects stats", () => DumpProjectStats(Languages.C_)) // TODO: Add menu to select language
                    .Add("Calc new project metrics", CalcNewProjectMetrics)
                    .Add("Exit", () => Environment.Exit(0));

            MainMenu.Display();
        }

        private static void FixPaths()
        {
            // int index = 0;
            var list = new List<string>();

            foreach (var path in Settings.ProjectsPath)
                list.Add(PathHint.ToWinDir(path));

            var collection = new StringCollection();
            collection.AddRange(list.ToArray());

            Settings.ProjectsPath = collection;
            Settings.Save();
        }

        private static void CalcNewProjectMetrics()
        {
            throw new NotImplementedException();
        }

        private static void DumpProjectStats(Languages langFilter = default)
        {
            var projectList = new List<ProjectData>();

            foreach (var path in Settings.ProjectsPath)
            {
                string upperFolder = Path.GetDirectoryName(path);
                projectList.Add(ProjectData.GetData(path, F.CreateProcess(Settings.ClocPath, new DirectoryInfo(path).Name, upperFolder), langFilter));
            }

            File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "projectList.json"), JsonConvert.SerializeObject(projectList, Formatting.Indented));

            MainMenu.Display();
        }

        private static void ListProjectPaths()
        {
            var options = Settings.ProjectsPath.CreateMenu(SelectedOption).ToArray();

            var menu = new Menu()
                .AddOptions(options);

            menu.Display();
        }

        private static void SetClocPath(bool changePath)
        {
            if (changePath)
                Console.WriteLine($"> Current Path: {Settings.ClocPath}");

            string clocPath = PathHint.ReadLine("Type CLOC path: ");

            // TODO: Check if clocPath is an executable file

            Settings.ClocPath = clocPath;
            Settings.Save();

            GoBack();
        }

        private static void AddPathCallback()
        {
            string path = PathHint.ReadLine("Type project path: ");

            // TODO: Check if path is directory

            if (Settings.ProjectsPath == null)
                Settings.ProjectsPath = new StringCollection();

            Settings.ProjectsPath.Add(path);
            Settings.Save();

            GoBack();
        }

        private static void SelectedOption(int index)
        {
            var menu = new Menu()
                .Add("Get dump", () => GetDump(index))
                .Add("Delete path", () => DeletePath(index));

            menu.Display();
        }

        private static void GoBack()
        {
            Console.Clear();
            MainMenu.Display();
        }

        private static void GetDump(int index)
        {
            // TODO: Parse dump from process
            // F.CreateProcess(Settings.ClocPath, string.Empty, GetProjectPathAt(index));
        }

        private static void DeletePath(int index)
        {
            Settings.ProjectsPath.RemoveAt(index);
            Settings.Save();
        }

        private static string GetProjectPathAt(int index)
        {
            return Settings.ProjectsPath[index];
        }
    }

    [Serializable]
    public sealed class ProjectData
    {
        public string Path { get; set; }
        public string Name => new DirectoryInfo(Path).Name;
        public int Lines { get; set; }

        public int Files { get; set; }
        public long Chars { get; set; }
        public float LinesPerFile => (float)Lines / Files;
        public float CharsPerLine => (float)Chars / Lines;

        public List<LanguageData> LangData { get; } = new List<LanguageData>();

        private ProjectData()
        {
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine($"- {Name} ({Lines:N3} lines; {Files:N3} files)");
            builder.AppendLine($"\t > Lines per file: {LinesPerFile:N3}");
            builder.AppendLine($"\t > Characters: {Chars:N3}");
            builder.AppendLine($"\t > Characters per line: {CharsPerLine:N3}");

            return builder.ToString();
        }

        public static ProjectData GetData(string path, string clocDump, Languages filterLang = default)
        {
            // TODO: Parse dump
            // TODO: Pass language filter in a param

            var projectData = new ProjectData();

            const string langNeedle = "Language";

            const string LangGroup = "Language";
            const string FileGroup = "File";
            const string BlankGroup = "Blank";
            const string CommentGroup = "Comment";
            const string CodeGroup = "Code";

            string RegexPattern = $@"^(?<{LangGroup}>(.+?)) {{1,}}(?<{FileGroup}>\d+) {{1,}}(?<{BlankGroup}>\d+) {{1,}}(?<{CommentGroup}>\d+) {{1,}}(?<{CodeGroup}>\d+)";

            var lines = Regex.Split(clocDump, @"\r?\n|\r");

            bool flagLang = false;

            foreach (var line in lines)
            {
                if (!line.StartsWith(langNeedle) && !flagLang)
                    continue;

                if (!flagLang)
                    flagLang = true;

                if (ProjectAspects.ContainsLine(line))
                {
                    var match = Regex.Match(line, RegexPattern);

                    string langName = match.Groups[LangGroup]?.Value;
                    string fileValue = match.Groups[FileGroup]?.Value;
                    string blankValue = match.Groups[BlankGroup]?.Value;
                    string commentValue = match.Groups[CommentGroup]?.Value;
                    string codeValue = match.Groups[CodeGroup]?.Value;

                    if (langName?.ToLowerInvariant().Contains("sum") == true)
                        continue;

                    var lang = ProjectAspects.GetLang(langName);

                    if (!lang.HasValue)
                    {
                        Console.WriteLine($"Lang '{langName}' not recognized!", Color.Red);
                        continue;
                    }

                    var data = new LanguageData(lang.Value, fileValue, blankValue, commentValue, codeValue);
                    projectData.LangData.Add(data);
                }
            }

            int? totalLines = GetLines(projectData.LangData, filterLang);

            //if (!totalLines.HasValue)
            //    throw new Exception();

            projectData.Path = path;
            projectData.Lines = totalLines.HasValue ? totalLines.Value : -1;
            projectData.Chars = GetChars(path, filterLang, out int fileCount);
            projectData.Files = fileCount;

            return projectData;
        }

        private static int? GetLines(List<LanguageData> langData, Languages filterLang)
        {
            if (filterLang == default)
                return langData.Sum(lang => lang.Code + lang.Blank + lang.Comment);

            var _lang = langData.FirstOrDefault(lang => lang.Lang == filterLang);

            return _lang?.Code + _lang?.Blank + _lang?.Comment;
        }

        private static long GetChars(string path, Languages filterLang, out int fileCount)
        {
            string ext = string.Join(";", ProjectAspects.GetExtensions(filterLang));
            var files = Directory.GetFiles(path, ext, System.IO.SearchOption.AllDirectories).ToArray();

            fileCount = files.Length;
            return files.Select(file => new FileInfo(file).Length).Sum();
        }
    }

    [Serializable]
    public class LanguageData
    {
        public Languages Lang { get; set; }
        public int Files { get; set; }
        public int Blank { get; set; }
        public int Comment { get; set; }
        public int Code { get; set; }

        private LanguageData()
        {
        }

        public LanguageData(Languages lang, int files, int blank, int comment, int code)
        {
            Lang = lang;
            Files = files;
            Blank = blank;
            Comment = comment;
            Code = code;
        }

        public LanguageData(Languages lang, string files, string blank, string comment, string code)
        {
            Lang = lang;
            Files = int.Parse(files);
            Blank = int.Parse(blank);
            Comment = int.Parse(comment);
            Code = int.Parse(code);
        }
    }
}