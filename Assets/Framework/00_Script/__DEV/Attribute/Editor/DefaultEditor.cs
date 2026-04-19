using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Linq;

namespace O2un.Core
{
    [CustomEditor(typeof(SafeMono), true)] 
    [CanEditMultipleObjects]
    public class O2unEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var methods = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttribute<TestButton>() != null);

            if (!methods.Any()) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Functions", EditorStyles.boldLabel);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<TestButton>();
                string label = string.IsNullOrEmpty(attr.ButtonName) ? method.Name : attr.ButtonName;

                // 버튼 스타일링 (선택 사항)
                GUI.backgroundColor = Color.cyan;
                if (GUILayout.Button(label, GUILayout.Height(25)))
                {
                    foreach (var t in targets) // Multi-object selection 대응
                    {
                        method.Invoke(t, null);
                    }
                }

                GUI.backgroundColor = Color.white;
            }
        }
    }
}