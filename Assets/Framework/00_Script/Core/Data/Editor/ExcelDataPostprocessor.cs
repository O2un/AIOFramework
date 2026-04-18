using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;
using UnityEditor;
using UnityEngine;

public class ExcelDataPostprocessor : AssetPostprocessor
{
    private static ExcelEditorConfig _config;
    public static ExcelEditorConfig Config => _config ??= EditorHelper.GetOrCreateSettings<ExcelEditorConfig>(ExcelEditorConfig.EDITORCONFIGPATH);

    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        var targetAssets = importedAssets.Concat(movedAssets)
            .Where(path => path.StartsWith(Config.ExcelDirectory) && path.EndsWith(".xlsx"))
            .ToList();

        if (targetAssets.Count == 0) return;

        // EnsureDirectoriesExist();
        // bool needsRecompile = false;

        // foreach (var assetPath in targetAssets)
        // {
        //     var className = Path.GetFileNameWithoutExtension(assetPath);
            
        //     GenerateDeveloperScriptIfNotExists(className);
            
        //     var csvPath = $"{GeneratedCsvDirectory}/{className}.csv";
        //     ConvertExcelToCsv(assetPath, csvPath);

        //     if (UpdateRspFile(csvPath))
        //     {
        //         needsRecompile = true;
        //     }
        // }

        // AssetDatabase.Refresh();

        // if (needsRecompile)
        // {
        //     CompilationPipeline.RequestScriptCompilation();
        // }
    }

    private static void EnsureDirectoriesExist()
    {
        if (!Directory.Exists(Config.GeneratedCsvDirectory)) Directory.CreateDirectory(Config.GeneratedCsvDirectory);
        if (!Directory.Exists(Config.MainScriptDirectory)) Directory.CreateDirectory(Config.MainScriptDirectory);
    }

    private static void ConvertExcelToCsv(string excelPath, string csvPath)
    {
        using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var result = reader.AsDataSet();
        var table = result.Tables[0];

        var sb = new StringBuilder();

        for (int i = 0; i < table.Rows.Count; i++)
        {
            var rowData = new System.Collections.Generic.List<string>();
            for (int j = 0; j < table.Columns.Count; j++)
            {
                rowData.Add(table.Rows[i][j]?.ToString()?.Trim() ?? string.Empty);
            }
            sb.AppendLine(string.Join(",", rowData));
        }

        File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
    }
}
