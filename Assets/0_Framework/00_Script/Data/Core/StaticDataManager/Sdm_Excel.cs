using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using ExcelDataReader;
using O2un.Core;
using O2un.Core.Utils;
using UnityEngine;

namespace O2un.Data 
{
    public abstract partial class StaticDataManager<T> : IStaticDataManager where T : StaticData, new()
    {
        private const string INDEX_TYPE = "index";
        private const string GROUP_TYPE = "group";

        private string _indexColumn;
        private string _groupColumn;

        protected UniqueKey LoadKey(Dictionary<string, string> row)
        {
            string indexColumn = true == string.IsNullOrEmpty(_indexColumn) ? INDEX_TYPE : _indexColumn;

            if (false == row.TryGetValue(indexColumn, out string indexText) ||
                false == int.TryParse(indexText, out int index))
            {
                Log.Print(Log.LogLevel.Error, $"Key(Index)가 비정상입니다. 타입이 index 인 칼럼({indexColumn})은 무조건 존재해야 하며 숫자여야 합니다");
                return UniqueKey.Undefined;
            }

            if (true == string.IsNullOrEmpty(_groupColumn) ||
                false == row.TryGetValue(_groupColumn, out string groupText) ||
                true == string.IsNullOrEmpty(groupText))
            {
                return new UniqueKey(0, index);
            }

            if (false == int.TryParse(groupText, out int group))
            {
                Log.Print(Log.LogLevel.Error, $"Key(Group)가 비정상입니다. 칼럼({_groupColumn})은 숫자여야 합니다");
                return UniqueKey.Undefined;
            }

            return new UniqueKey(group, index);
        }

        // 생성기는 키 칼럼을 타입 행으로 가려내 생성에서 빼므로 파싱도 같은 기준이어야 한다.
        // 이름으로 찾으면 칼럼 이름이 index 가 아닌 순간 생성은 멀쩡한데 파싱만 0건이 된다.
        private void ResolveKeyColumns(DataTable table, List<string> columnNames)
        {
            _indexColumn = null;
            _groupColumn = null;

            for (int i = 0; i < columnNames.Count; ++i)
            {
                string type = table.Rows[1][i]?.ToString()?.Trim().ToLowerInvariant();

                if (INDEX_TYPE == type)
                {
                    _indexColumn = columnNames[i];
                }
                else if (GROUP_TYPE == type)
                {
                    _groupColumn = columnNames[i];
                }
            }
        }
        
#if UNITY_EDITOR
        // ReadExcelAndParse 는 [Conditional] 이라 인터페이스 멤버가 될 수 없다 (CS0629).
        // 굽기 도구가 비제네릭 IStaticDataManager 로 부를 수 있게 감싼다.
        public void BakeFromExcel(string excelPath, string sheetName)
        {
            Clear();
            ReadExcelAndParse(excelPath, sheetName);
            CompleteLoad();
            SaveToBinary();
        }
#endif

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
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

            ResolveKeyColumns(table, columnNames);

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
    }
}