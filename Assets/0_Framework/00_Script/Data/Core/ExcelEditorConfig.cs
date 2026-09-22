using O2un.Core;
public class ExcelEditorConfig : EditorConfig<ExcelEditorConfig>
{
    public string ExcelDirectory = "Assets/0_Framework/30_Data/Excel";
    public string GeneratedScriptDirectory = "Assets/0_Framework/00_Script/Data/Generated";
    public string StaticDataScriptDirectory = "Assets/0_Framework/00_Script/Data";

    public string GameExcelDirectory = "Assets/1_Game/30_Data/Excel";
    public string GameGeneratedScriptDirectory = "Assets/1_Game/00_Script/Data/Generated";
    public string GameStaticDataScriptDirectory = "Assets/1_Game/00_Script/Data";
}
