using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;
using O2un.Core.Utils;
using UnityEditor;

namespace O2un.Data.Editor
{
    public enum StaticDataOwner
    {
        Unknown = 0,
        Framework,
        Game,
    }

    public sealed class StaticDataEntry
    {
        public Type ManagerType;
        public Type DataType;
        public string Sheet;
        public StaticDataOwner Owner;
        public string ExcelPath;
        public string BinaryPath;
        public bool BinaryExists;
        public int Count;
        public bool IsLoaded;
        public string Note;

        public string ManagerName => null == ManagerType ? string.Empty : ManagerType.Name;
    }

    /// <summary>
    /// Excel 을 읽어 바이너리로 굽고, 구운 것을 다시 읽어 확인한다.
    ///
    /// 매니저는 자기 Excel 이 어디 있는지 모른다. 그 짝을 여기서 맞춘다 — 시트 이름은 생성기가
    /// 타입 이름을 짓는 규칙(<c>{시트}StaticDataManager</c>)의 역이고, 소유자는 Excel 이 놓인
    /// 폴더로 갈린다. 창과 CLI 가 같은 메서드를 부르도록 전부 static 으로 둔다.
    /// </summary>
    public static class StaticDataBaker
    {
        private const string MANAGER_SUFFIX = "StaticDataManager";

        public static List<StaticDataEntry> Collect()
        {
            Dictionary<string, (string Path, StaticDataOwner Owner)> sheets = IndexSheets();
            List<StaticDataEntry> entries = new();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<IStaticDataManager>())
            {
                if (true == type.IsAbstract || true == type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (null == type.GetConstructor(Type.EmptyTypes))
                {
                    continue;
                }

                StaticDataEntry entry = new()
                {
                    ManagerType = type,
                    DataType = ToDataType(type),
                    Sheet = ToSheetName(type.Name),
                };

                if (true == sheets.TryGetValue(entry.Sheet, out var source))
                {
                    entry.ExcelPath = source.Path;
                    entry.Owner = source.Owner;
                }
                else
                {
                    entry.Note = "Excel 없음";
                }

                Fill(entry);
                entries.Add(entry);
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.ManagerName, b.ManagerName));

            return entries;
        }

        public static List<StaticDataEntry> BakeAll()
        {
            List<StaticDataEntry> entries = Collect();

            foreach (StaticDataEntry entry in entries)
            {
                if (true == string.IsNullOrEmpty(entry.ExcelPath))
                {
                    continue;
                }

                try
                {
                    IStaticDataManager manager = Create(entry.ManagerType);
                    manager.BakeFromExcel(entry.ExcelPath, entry.Sheet);

                    entry.Count = manager.Count;
                    entry.IsLoaded = manager.IsLoaded;
                    entry.BinaryPath = manager.BinaryPath;
                    entry.BinaryExists = true == File.Exists(entry.BinaryPath);
                    entry.Note = string.Empty;
                }
                catch (Exception e)
                {
                    // 한 테이블이 깨져도 나머지는 구워야 어디가 문제인지 한 번에 드러난다.
                    entry.Note = e.Message;
                    Log.Print(Log.LogLevel.Error, $"{entry.ManagerName} 굽기 실패", exception: e);
                }
            }

            AssetDatabase.Refresh();

            return entries;
        }

        public static List<StaticDataEntry> VerifyAll()
        {
            List<StaticDataEntry> entries = Collect();

            foreach (StaticDataEntry entry in entries)
            {
                try
                {
                    IStaticDataManager manager = Create(entry.ManagerType);
                    manager.Load(isLoadFromBinary: true);

                    entry.Count = manager.Count;
                    entry.IsLoaded = manager.IsLoaded;

                    if (false == entry.IsLoaded)
                    {
                        entry.Note = "바이너리 없음 또는 읽기 실패";
                    }
                }
                catch (Exception e)
                {
                    entry.Note = e.Message;
                    Log.Print(Log.LogLevel.Error, $"{entry.ManagerName} 읽기 실패", exception: e);
                }
            }

            return entries;
        }

        public static string Report(List<StaticDataEntry> entries)
        {
            StringBuilder builder = new();

            builder.AppendLine("Manager\tOwner\tSheet\tCount\tLoaded\tBinary\tExcel\tNote");

            foreach (StaticDataEntry e in entries)
            {
                builder.AppendLine(string.Join("\t",
                    e.ManagerName,
                    e.Owner.ToString(),
                    e.Sheet,
                    e.Count.ToString(),
                    e.IsLoaded ? "yes" : "no",
                    true == e.BinaryExists ? e.BinaryPath : $"(없음) {e.BinaryPath}",
                    e.ExcelPath ?? string.Empty,
                    e.Note ?? string.Empty));
            }

            return builder.ToString();
        }

        public static string BakeAllAndReport() => Report(BakeAll());

        public static string VerifyAllAndReport() => Report(VerifyAll());

        private static void Fill(StaticDataEntry entry)
        {
            try
            {
                IStaticDataManager manager = Create(entry.ManagerType);
                entry.BinaryPath = manager.BinaryPath;
                entry.BinaryExists = false == string.IsNullOrEmpty(entry.BinaryPath) && true == File.Exists(entry.BinaryPath);
            }
            catch (Exception e)
            {
                entry.Note = e.Message;
            }
        }

        private static IStaticDataManager Create(Type type)
        {
            return (IStaticDataManager)Activator.CreateInstance(type);
        }

        private static Type ToDataType(Type managerType)
        {
            for (Type t = managerType; null != t; t = t.BaseType)
            {
                if (true == t.IsGenericType && typeof(StaticDataManager<>) == t.GetGenericTypeDefinition())
                {
                    return t.GetGenericArguments()[0];
                }
            }

            return null;
        }

        private static string ToSheetName(string managerTypeName)
        {
            return true == managerTypeName.EndsWith(MANAGER_SUFFIX, StringComparison.Ordinal)
                ? managerTypeName[..^MANAGER_SUFFIX.Length]
                : managerTypeName;
        }

        private static Dictionary<string, (string, StaticDataOwner)> IndexSheets()
        {
            Dictionary<string, (string, StaticDataOwner)> map = new(StringComparer.Ordinal);

            ExcelEditorConfig config = ExcelDataPostprocessor.Config;
            if (null == config)
            {
                return map;
            }

            Index(map, config.ExcelDirectory, StaticDataOwner.Framework);
            Index(map, config.GameExcelDirectory, StaticDataOwner.Game);

            return map;
        }

        private static void Index(
            Dictionary<string, (string, StaticDataOwner)> map,
            string directory,
            StaticDataOwner owner)
        {
            if (true == string.IsNullOrEmpty(directory) || false == Directory.Exists(directory))
            {
                return;
            }

            string[] files = Directory.GetFiles(directory, "*.xlsx", SearchOption.AllDirectories)
                .Where(path => !path.Contains("~$"))
                .ToArray();

            foreach (string file in files)
            {
                foreach (string sheet in ReadSheetNames(file))
                {
                    // 같은 시트 이름이 양쪽에 있으면 생성 코드도 이미 갈라져 있다. 먼저 찾은 쪽을 쓰되
                    // 덮어쓰지 않아야 프레임워크가 게임 Excel 로 구워지는 일이 없다.
                    if (false == map.ContainsKey(sheet))
                    {
                        map[sheet] = (file.Replace('\\', '/'), owner);
                    }
                }
            }
        }

        private static IEnumerable<string> ReadSheetNames(string excelPath)
        {
            List<string> names = new();

            try
            {
                using FileStream stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                DataSet result = reader.AsDataSet();

                foreach (DataTable table in result.Tables)
                {
                    names.Add(table.TableName);
                }
            }
            catch (Exception e)
            {
                // 열리지 않는 Excel 하나가 색인 전체를 막으면 나머지 테이블도 구울 수 없게 된다.
                Log.Print(Log.LogLevel.Warning, $"Excel 을 읽지 못했다. ({excelPath})", exception: e);
            }

            return names;
        }
    }
}
