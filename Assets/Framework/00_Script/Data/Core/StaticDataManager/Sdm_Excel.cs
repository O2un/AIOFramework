using System;
using System.Collections.Generic;
using System.IO;
using ExcelDataReader;
using UnityEngine;

namespace O2un.Data 
{
    public abstract partial class StaticDataManager<T> : IStaticDataManager where T : StaticData, new()
    {
#if UNITY_EDITOR
        protected void ReadExcelAndParse(string excelPath, string sheetName)
        {
            if (!File.Exists(excelPath))
            {
                Debug.LogError($"[StaticDataManager] 엑셀 파일이 존재하지 않습니다: {excelPath}");
                return;
            }

            using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var result = reader.AsDataSet();

            if (!result.Tables.Contains(sheetName))
            {
                Debug.LogError($"[StaticDataManager] 엑셀에 '{sheetName}' 시트가 없습니다.");
                return;
            }

            var table = result.Tables[sheetName];
            if (table.Rows.Count < 2) return;

            var rawDataList = new List<Dictionary<string, string>>();
            var columnNames = new List<string>();

            for (int i = 0; i < table.Columns.Count; i++)
            {
                columnNames.Add(table.Rows[0][i]?.ToString()?.Trim() ?? string.Empty);
            }

            for (int i = 2; i < table.Rows.Count; i++)
            {
                var rowData = new Dictionary<string, string>();
                bool isEmptyRow = true;

                for (int j = 0; j < table.Columns.Count; j++)
                {
                    string colName = columnNames[j];
                    if (string.IsNullOrEmpty(colName)) continue;

                    string cellValue = table.Rows[i][j]?.ToString()?.Trim() ?? string.Empty;
                    rowData[colName] = cellValue;

                    if (!string.IsNullOrEmpty(cellValue))
                    {
                        isEmptyRow = false;
                    }
                }

                if (!isEmptyRow)
                {
                    rawDataList.Add(rowData);
                }
            }

            ParseGeneratedData(rawDataList);
        }

        protected abstract void ParseGeneratedData(List<Dictionary<string, string>> rawData);
#endif
    }
}