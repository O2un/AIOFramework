using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class ExcelDataPostprocessor : AssetPostprocessor
{
    private static ExcelEditorConfig _config;
    public static ExcelEditorConfig Config => _config ??= ExcelEditorConfig.GetConfig();

    static ExcelDataPostprocessor()
    {
        EditorApplication.delayCall += CheckAllExcelFilesOnStartup;
    }

    private static void CheckAllExcelFilesOnStartup()
    {
        EditorApplication.delayCall -= CheckAllExcelFilesOnStartup;

        if (SessionState.GetBool("ExcelDataPostprocessor_Initialized", false))
            return;
        SessionState.SetBool("ExcelDataPostprocessor_Initialized", true);

        if (Config == null || string.IsNullOrEmpty(Config.ExcelDirectory)) return;

        string fullPath = Path.GetFullPath(Config.ExcelDirectory);
        if (!Directory.Exists(fullPath)) return;
        
        string[] excelFiles = Directory.GetFiles(fullPath, "*.xlsx", SearchOption.AllDirectories)
            .Where(path => !path.Contains("~$"))
            .ToArray();

        if (excelFiles.Length == 0) return;

        EnsureDirectoriesExist();

        foreach (var file in excelFiles)
        {
            ProcessExcelFile(file);
        }
        
        Debug.Log("[ExcelDataPostprocessor] 에디터 시작 중 엑셀 변경 사항을 감지하여 스크립트를 갱신했습니다.");
        AssetDatabase.Refresh();
    }

    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        var targetAssets = importedAssets.Concat(movedAssets)
            .Where(path => path.StartsWith(Config.ExcelDirectory) && path.EndsWith(".xlsx") && !path.Contains("~$"))
            .ToList();

        if (targetAssets.Count == 0) return;

        EnsureDirectoriesExist();

        bool isChanged = false;

        foreach (var assetPath in targetAssets)
        {
            if (ProcessExcelFile(assetPath))
            {
                isChanged = true;
            }
        }

        if (isChanged)
        {
            AssetDatabase.Refresh();
        }
    }

    private static void EnsureDirectoriesExist()
    {
        if (!Directory.Exists(Config.GeneratedScriptDirectory)) Directory.CreateDirectory(Config.GeneratedScriptDirectory);

        if (!Directory.Exists(Config.StaticDataScriptDirectory)) Directory.CreateDirectory(Config.StaticDataScriptDirectory);
        if (!Directory.Exists(Config.StaticDataScriptDirectory+"/StaticData")) Directory.CreateDirectory(Config.StaticDataScriptDirectory+"/StaticData");
        if (!Directory.Exists(Config.StaticDataScriptDirectory+"/Manager")) Directory.CreateDirectory(Config.StaticDataScriptDirectory+"/Manager");
    }

    private static bool ProcessExcelFile(string excelPath)
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

            GenerateMainScriptsIfNotExists(sheetName);
            GenerateGeneratedDataScript(sheetName, names, types);
        }

        return true;
    }

    private static void GenerateMainScriptsIfNotExists(string sheetName)
    {
        string dataScriptPath = Path.Combine(Config.StaticDataScriptDirectory, "StaticData", $"{sheetName}StaticData.cs");
        string managerScriptPath = Path.Combine(Config.StaticDataScriptDirectory, "Manager", $"{sheetName}StaticDataManager.cs");

        if (!File.Exists(dataScriptPath))
        {
            string dataTemplate = 
$@"namespace O2un.Data
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
$@"namespace O2un.Data
{{
    public partial class {sheetName}StaticDataManager : StaticDataManager<{sheetName}StaticData>
    {{
        protected override void SetProcess()
        {{
        }}
        //protected override void LinkProcess()
        {{
        }}
    }}
}}";
            File.WriteAllText(managerScriptPath, managerTemplate, Encoding.UTF8);
        }
    }

    private static void GenerateGeneratedDataScript(string sheetName, List<string> names, List<string> types)
    {
        string generatedPath = Path.Combine(Config.GeneratedScriptDirectory, $"{sheetName}StaticData.g.cs");
        var sb = new StringBuilder();
        sb.AppendLine("namespace O2un.Data");
        sb.AppendLine("{");
        sb.AppendLine("    [StaticData]");
        sb.AppendLine($"    public partial class {sheetName}StaticData");
        sb.AppendLine("    {");

        for (int i = 0; i < names.Count; i++)
        {
            string varName = names[i];
            string csharpType = GetCSharpType(types[i]);

            sb.AppendLine($"        public {csharpType} {varName} {{get; init;}}");
        }

        sb.AppendLine("    }");
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
