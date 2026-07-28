using UnityEditor;
using UnityEngine;

namespace O2un.DEV
{
    /// <summary>
    /// <see cref="TestServerLauncher"/> 를 켜고 끄고, 표준출력을 보여 주는 창.
    /// </summary>
    public sealed class TestServerWindow : EditorWindow
    {
        private Vector2 _scroll;
        private bool _followTail = true;

        [MenuItem("Tools/O2un/Test Server")]
        public static void ShowWindow()
        {
            var window = GetWindow<TestServerWindow>("Test Server");
            window.minSize = new Vector2(420, 280);
            window.Show();
        }

        private void OnEnable()
        {
            TestServerLauncher.OnLogChanged += Repaint;
        }

        private void OnDisable()
        {
            TestServerLauncher.OnLogChanged -= Repaint;
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawStatus();
            DrawLog();
        }

        private void DrawToolbar()
        {
            using var _ = new EditorGUILayout.HorizontalScope(EditorStyles.toolbar);

            bool isRunning = TestServerLauncher.IsRunning;

            bool isBusy = TestServerLauncher.IsTaskRunning;

            using (new EditorGUI.DisabledScope(isRunning || isBusy))
            {
                if (true == GUILayout.Button("시작", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    TestServerLauncher.Start();
            }

            using (new EditorGUI.DisabledScope(false == isRunning))
            {
                if (true == GUILayout.Button("중지", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    TestServerLauncher.Stop();
            }

            using (new EditorGUI.DisabledScope(isBusy || false == isRunning))
            {
                if (true == GUILayout.Button("자가 진단", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    TestServerLauncher.Diagnose();
            }

            using (new EditorGUI.DisabledScope(isBusy))
            {
                if (true == GUILayout.Button("npm install", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    TestServerLauncher.Install();
            }

            GUILayout.FlexibleSpace();

            _followTail = GUILayout.Toggle(_followTail, "자동 스크롤", EditorStyles.toolbarButton, GUILayout.Width(80));

            if (true == GUILayout.Button("로그 지우기", EditorStyles.toolbarButton, GUILayout.Width(80)))
                TestServerLauncher.ClearLog();
        }

        private void DrawStatus()
        {
            if (true == TestServerLauncher.IsTaskRunning)
            {
                EditorGUILayout.HelpBox("작업 실행 중", MessageType.Info);
            }
            else if (true == TestServerLauncher.IsRunning)
            {
                EditorGUILayout.HelpBox("실행 중 — ws://localhost:8080 (Play 진입·도메인 리로드에도 유지됩니다)", MessageType.Info);
            }
            else if (false == TestServerLauncher.HasModules)
            {
                EditorGUILayout.HelpBox("node_modules 가 없습니다. [npm install] 을 먼저 실행하세요.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("중지됨", MessageType.None);
            }

            EditorGUILayout.LabelField(TestServerLauncher.ServerPath, EditorStyles.miniLabel);
        }

        private void DrawLog()
        {
            using var scope = new EditorGUILayout.ScrollViewScope(_scroll, EditorStyles.textArea);

            var lines = TestServerLauncher.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                EditorGUILayout.SelectableLabel(lines[i], EditorStyles.miniLabel, GUILayout.Height(15));
            }

            _scroll = _followTail ? new Vector2(scope.scrollPosition.x, float.MaxValue) : scope.scrollPosition;
        }
    }
}
