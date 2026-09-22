using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace O2un.Data.Editor
{
    /// <summary>
    /// 어떤 StaticData 테이블이 어느 소유자에서 나와 어디로 구워졌고, 지금 몇 건이 올라와 있는지 본다.
    ///
    /// Play 중에는 <see cref="StaticDataFactory.Registered"/> 에 실제로 등록된 매니저의 값을 덮어쓴다.
    /// 등록하는 코드가 아직 없으면 그 열이 비는 것이 맞는 답이다 — 창이 스스로 만든 인스턴스를
    /// 등록된 것처럼 보여주면 배선이 안 된 것을 배선된 것으로 읽는다.
    /// </summary>
    public sealed class StaticDataDiagnosticsWindow : EditorWindow
    {
        private static readonly GUILayoutOption[] WIDE = { GUILayout.Width(190f) };
        private static readonly GUILayoutOption[] MID = { GUILayout.Width(90f) };
        private static readonly GUILayoutOption[] NARROW = { GUILayout.Width(56f) };

        private List<StaticDataEntry> _entries = new();
        private Vector2 _scroll;

        [MenuItem("O2un/Data/Static Data Diagnostics")]
        private static void Open()
        {
            StaticDataDiagnosticsWindow window = GetWindow<StaticDataDiagnosticsWindow>();
            window.titleContent = new GUIContent("StaticData");
            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnInspectorUpdate()
        {
            if (true == EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawHeader();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (StaticDataEntry entry in _entries)
            {
                DrawRow(entry);
            }

            EditorGUILayout.EndScrollView();

            if (0 == _entries.Count)
            {
                EditorGUILayout.HelpBox("StaticDataManager 를 상속한 구체 타입이 없다.", MessageType.Info);
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (true == GUILayout.Button("Bake All", EditorStyles.toolbarButton, MID))
            {
                _entries = StaticDataBaker.BakeAll();
            }

            if (true == GUILayout.Button("Verify Binaries", EditorStyles.toolbarButton, WIDE))
            {
                _entries = StaticDataBaker.VerifyAll();
            }

            if (true == GUILayout.Button("Refresh", EditorStyles.toolbarButton, MID))
            {
                Refresh();
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label(
                true == EditorApplication.isPlaying
                    ? $"Play · 등록 {StaticDataFactory.Registered.Count}"
                    : "Edit",
                EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Manager", EditorStyles.miniBoldLabel, WIDE);
            GUILayout.Label("Owner", EditorStyles.miniBoldLabel, MID);
            GUILayout.Label("Count", EditorStyles.miniBoldLabel, NARROW);
            GUILayout.Label("Loaded", EditorStyles.miniBoldLabel, NARROW);
            GUILayout.Label("Live", EditorStyles.miniBoldLabel, NARROW);
            GUILayout.Label("Binary", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRow(StaticDataEntry entry)
        {
            IStaticDataManager live = ResolveLive(entry);

            int count = null == live ? entry.Count : live.Count;
            bool isLoaded = null == live ? entry.IsLoaded : live.IsLoaded;

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(new GUIContent(entry.ManagerName, entry.ExcelPath), WIDE);
            GUILayout.Label(entry.Owner.ToString(), MID);
            GUILayout.Label(count.ToString(), NARROW);
            GUILayout.Label(true == isLoaded ? "yes" : "no", NARROW);
            GUILayout.Label(null == live ? "-" : "yes", NARROW);

            Color previous = GUI.color;
            if (false == entry.BinaryExists)
            {
                GUI.color = Color.gray;
            }
            GUILayout.Label(new GUIContent(entry.BinaryPath ?? string.Empty, entry.BinaryPath), EditorStyles.miniLabel);
            GUI.color = previous;

            EditorGUILayout.EndHorizontal();

            if (false == string.IsNullOrEmpty(entry.Note))
            {
                EditorGUILayout.LabelField(" ", entry.Note, EditorStyles.miniLabel);
            }
        }

        private static IStaticDataManager ResolveLive(StaticDataEntry entry)
        {
            if (false == EditorApplication.isPlaying || null == entry.DataType)
            {
                return null;
            }

            return true == StaticDataFactory.Registered.TryGetValue(entry.DataType, out IStaticDataManager manager)
                ? manager
                : null;
        }

        private void Refresh()
        {
            _entries = StaticDataBaker.Collect();
        }
    }
}
