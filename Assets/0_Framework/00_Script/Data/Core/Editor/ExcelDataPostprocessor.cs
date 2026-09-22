using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;
using UnityEditor;

[InitializeOnLoad]
public class ExcelDataPostprocessor : AssetPostprocessor
{
    private const string FRAMEWORK_NAMESPACE = "O2un.Data";
    private const string GAME_NAMESPACE = "Game.Data";

    private static ExcelEditorConfig _config;
    public static ExcelEditorConfig Config => _config ??= ExcelEditorConfig.GetConfig();

    /// <summary>
    /// Excel 한 장이 어디로 생성될지를 소유자별로 묶는다. 소유자는 Excel 이 놓인 폴더로 갈린다 —
    /// 시트나 별도 목록에 적게 하면 등록을 빠뜨린 Excel 이 조용히 프레임워크 폴더로 생성된다.
    /// </summary>
    private sealed class OwnerTarget
    {
        public string ExcelDirectory;
        public string GeneratedScriptDirectory;
        public string StaticDataScriptDirectory;
        public string Namespace;
        public bool IsGame;
    }

    static ExcelDataPostprocessor()
    {
        EditorApplication.delayCall += CheckAllExcelFilesOnStartup;
    }

    private static List<OwnerTarget> BuildTargets()
    {
        var targets = new List<OwnerTarget>();

        if (null == Config)
        {
            return targets;
        }

        if (false == string.IsNullOrEmpty(Config.ExcelDirectory))
        {
            targets.Add(new OwnerTarget
            {
                ExcelDirectory = Config.ExcelDirectory,
                GeneratedScriptDirectory = Config.GeneratedScriptDirectory,
                StaticDataScriptDirectory = Config.StaticDataScriptDirectory,
                Namespace = FRAMEWORK_NAMESPACE,
                IsGame = false,
            });
        }

        if (false == string.IsNullOrEmpty(Config.GameExcelDirectory))
        {
            targets.Add(new OwnerTarget
            {
                ExcelDirectory = Config.GameExcelDirectory,
                GeneratedScriptDirectory = Config.GameGeneratedScriptDirectory,
                StaticDataScriptDirectory = Config.GameStaticDataScriptDirectory,
                Namespace = GAME_NAMESPACE,
                IsGame = true,
            });
        }

        return targets;
    }

    private static void CheckAllExcelFilesOnStartup()
    {
        EditorApplication.delayCall -= CheckAllExcelFilesOnStartup;

        if (SessionState.GetBool("ExcelDataPostprocessor_Initialized", false))
            return;
        SessionState.SetBool("ExcelDataPostprocessor_Initialized", true);

        bool isChanged = false;

        foreach (var target in BuildTargets())
        {
            string fullPath = Path.GetFullPath(target.ExcelDirectory);
            if (false == Directory.Exists(fullPath)) continue;

            string[] excelFiles = Directory.GetFiles(fullPath, "*.xlsx", SearchOption.AllDirectories)
                .Where(path => !path.Contains("~$"))
                .ToArray();

            if (0 == excelFiles.Length) continue;

            EnsureDirectoriesExist(target);

            foreach (var file in excelFiles)
            {
                if (ProcessExcelFile(target, file))
                {
                    isChanged = true;
                }
            }
        }

        if (isChanged)
        {
            AssetDatabase.Refresh();
        }
    }

    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        bool isChanged = false;

        foreach (var target in BuildTargets())
        {
            var targetAssets = importedAssets.Concat(movedAssets)
                .Where(path => path.StartsWith(target.ExcelDirectory) && path.EndsWith(".xlsx") && !path.Contains("~$"))
                .ToList();

            if (0 == targetAssets.Count) continue;

            EnsureDirectoriesExist(target);

            foreach (var assetPath in targetAssets)
            {
                if (ProcessExcelFile(target, assetPath))
                {
                    isChanged = true;
                }
            }
        }

        if (isChanged)
        {
            AssetDatabase.Refresh();
        }
    }

    private static void EnsureDirectoriesExist(OwnerTarget target)
    {
        if (!Directory.Exists(target.GeneratedScriptDirectory)) Directory.CreateDirectory(target.GeneratedScriptDirectory);

        if (!Directory.Exists(target.StaticDataScriptDirectory)) Directory.CreateDirectory(target.StaticDataScriptDirectory);
        if (!Directory.Exists(target.StaticDataScriptDirectory+"/StaticData")) Directory.CreateDirectory(target.StaticDataScriptDirectory+"/StaticData");
        if (!Directory.Exists(target.StaticDataScriptDirectory+"/Manager")) Directory.CreateDirectory(target.StaticDataScriptDirectory+"/Manager");
    }

    private static bool ProcessExcelFile(OwnerTarget target, string excelPath)
    {
        using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var result = reader.AsDataSet();

        if (result.Tables.Count == 0) return false;

        foreach (DataTable table in result.Tables)
        {
            if (table.Rows.Count < 2) continue;

            string sheetName = table.TableName;

            var names = new List<string>();
            var types = new List<string>();

            for (int i = 0; i < table.Columns.Count; i++)
            {
                string name = table.Rows[0][i]?.ToString()?.Trim();
                string type = table.Rows[1][i]?.ToString()?.Trim();

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type)) continue;

                string lowerType = type.ToLowerInvariant();
                if (lowerType == "group" || lowerType == "index")
                {
                    continue;
                }

                names.Add(name);
                types.Add(type);
            }

            GenerateMainScriptsIfNotExists(target, sheetName);
            GenerateGeneratedDataScript(target, sheetName, names, types);
        }

        return true;
    }

    private static void GenerateMainScriptsIfNotExists(OwnerTarget target, string sheetName)
    {
        string dataScriptPath = Path.Combine(target.StaticDataScriptDirectory, "StaticData", $"{sheetName}StaticData.cs");
        string managerScriptPath = Path.Combine(target.StaticDataScriptDirectory, "Manager", $"{sheetName}StaticDataManager.cs");

        // 프레임워크 밖 네임스페이스는 StaticData / StaticDataManager<T> 를 using 으로 끌어와야 한다.
        string dataUsing = true == target.IsGame ? $"using {FRAMEWORK_NAMESPACE};{System.Environment.NewLine}{System.Environment.NewLine}" : string.Empty;

        if (!File.Exists(dataScriptPath))
        {
            string dataTemplate =
$@"{dataUsing}namespace {target.Namespace}
{{
    public partial class {sheetName}StaticData : StaticData
    {{
        public override bool Set()
        {{
            return true;
        }}
        public override bool Link()
        {{
            return true;
        }}
    }}
}}";
            File.WriteAllText(dataScriptPath, dataTemplate, Encoding.UTF8);
        }

        if (!File.Exists(managerScriptPath))
        {
            string managerTemplate =
$@"{dataUsing}namespace {target.Namespace}
{{
    public partial class {sheetName}StaticDataManager : StaticDataManager<{sheetName}StaticData>
    {{
        protected override void SetProcess()
        {{
        }}
        protected override void LinkProcess()
        {{
        }}
    }}
}}";
            File.WriteAllText(managerScriptPath, managerTemplate, Encoding.UTF8);
        }
    }

    private static void GenerateGeneratedDataScript(OwnerTarget target, string sheetName, List<string> names, List<string> types)
    {
        string generatedPath = Path.Combine(target.GeneratedScriptDirectory, $"{sheetName}StaticData.g.cs");
        var sb = new StringBuilder();

        if (true == target.IsGame)
        {
            sb.AppendLine($"using {FRAMEWORK_NAMESPACE};");
        }

        sb.AppendLine("using O2un.Roslyn.Generator;");
        sb.AppendLine($"namespace {target.Namespace}");
        sb.AppendLine("{");
        sb.AppendLine("    [O2un.Roslyn.Generator.StaticData]");
        sb.AppendLine($"    public partial class {sheetName}StaticData");
        sb.AppendLine("    {");

        for (int i = 0; i < names.Count; i++)
        {
            string varName = names[i];
            string csharpType = GetCSharpType(types[i]);

            sb.AppendLine($"        public {csharpType} {varName} {{get; init;}}");
        }

        sb.AppendLine("    }");

        // 손편집 매니저가 아니라 여기에 쓴다. 지워도 다음 임포트에서 되살아나야
        // 게임 데이터가 프레임워크 Binary 폴더로 구워지는 일이 없다.
        if (true == target.IsGame)
        {
            sb.AppendLine();
            sb.AppendLine($"    public partial class {sheetName}StaticDataManager");
            sb.AppendLine("    {");
            sb.AppendLine("        protected override string ResolveBinaryDirectory(StaticDataConfig config)");
            sb.AppendLine("        {");
            sb.AppendLine("            return config.GAME_BINARYPATH;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");

        File.WriteAllText(generatedPath, sb.ToString(), Encoding.UTF8);
    }

    private static string GetCSharpType(string rawType)
    {
        string lowerType = rawType.ToLowerInvariant();

        return lowerType switch
        {
            "int" => "int",
            "float" => "float",
            "string" => "string",
            _ => "string"
        };
    }
}
