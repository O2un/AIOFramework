using UnityEngine;

public class ExcelEditorConfig : SystemConfig
{
    public static readonly string EDITORCONFIGPATH = "Assets/Framework/99_DEV/SystemConfg/ExcelEditorConfig.asset";

    public string ExcelDirectory = "Assets/Data/Excel";
    public string GeneratedCsvDirectory = "Assets/Data/GeneratedCsv";
    public string MainScriptDirectory = "Assets/Scripts/Data";
    private string RspFilePath = "Assets/csc.rsp";
}
