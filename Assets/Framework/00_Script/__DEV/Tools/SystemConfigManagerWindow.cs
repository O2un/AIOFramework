using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using O2un.Core;
using UnityEditor;
using UnityEngine;

namespace O2un.DEV
{
    public class SystemConfigManagerWindow : EditorWindow
    {
        private List<Type> _editorConfigs = new List<Type>();
        private List<Type> _globalConfigs = new List<Type>();

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        private Type _selectedType;
        private ScriptableObject _selectedInstance;
        private Editor _cachedEditor;

        private bool _showEditorConfigs = true;
        private bool _showGlobalConfigs = true;

        [MenuItem("Tools/System Config Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<SystemConfigManagerWindow>("Config Manager");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            LoadAllConfigTypes();
        }

        private void LoadAllConfigTypes()
        {
            _editorConfigs = TypeCache.GetTypesDerivedFrom<IEditorConfig>().Where(t => !t.IsAbstract).ToList();
            _globalConfigs = TypeCache.GetTypesDerivedFrom<IGlobalConfig>().Where(t => !t.IsAbstract).ToList();
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();

            DrawLeftPanel();
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));
            DrawRightPanel();

            GUILayout.EndHorizontal();
        }

        private void DrawLeftPanel()
        {
            GUILayout.BeginVertical("box", GUILayout.Width(250), GUILayout.ExpandHeight(true));
            
            if (GUILayout.Button("새로고침", GUILayout.Height(30))) LoadAllConfigTypes();
            GUILayout.Space(5);

            _leftScroll = GUILayout.BeginScrollView(_leftScroll);
            
            _showEditorConfigs = EditorGUILayout.Foldout(_showEditorConfigs, $"Editor Configs ({_editorConfigs.Count})", true, EditorStyles.foldoutHeader);
            if (_showEditorConfigs)
            {
                foreach (var type in _editorConfigs) DrawTypeButton(type);
            }
            GUILayout.Space(10);
            
            _showGlobalConfigs = EditorGUILayout.Foldout(_showGlobalConfigs, $"Global Configs ({_globalConfigs.Count})", true, EditorStyles.foldoutHeader);
            if (_showGlobalConfigs)
            {
                foreach (var type in _globalConfigs) DrawTypeButton(type);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawTypeButton(Type type)
        {
            GUI.backgroundColor = _selectedType == type ? new Color(0.3f, 0.5f, 0.8f) : Color.white;
            
            if (GUILayout.Button(type.Name, EditorStyles.toolbarButton, GUILayout.Height(25)))
            {
                SelectConfig(type);
            }
            
            GUI.backgroundColor = Color.white;
        }

        private void DrawRightPanel()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _rightScroll = GUILayout.BeginScrollView(_rightScroll);

            if (_selectedType == null || _selectedInstance == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("왼쪽에서 설정 파일을 선택해주세요.", EditorStyles.largeLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
            else
            {
                GUILayout.Label($"{_selectedType.Name} Settings", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                
                if (_cachedEditor != null)
                {
                    _cachedEditor.OnInspectorGUI();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void SelectConfig(Type type)
        {
            _selectedType = type;
            
            MethodInfo method = type.BaseType.GetMethod("GetConfig", BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                _selectedInstance = method.Invoke(null, null) as ScriptableObject;
                if (_cachedEditor != null) DestroyImmediate(_cachedEditor);
                _cachedEditor = Editor.CreateEditor(_selectedInstance);
            }
            else
            {
                Debug.LogError($"GetConfig 메서드를 찾을 수 없습니다: {type.Name}");
            }

            GUI.FocusControl(null);
        }
    }
}
