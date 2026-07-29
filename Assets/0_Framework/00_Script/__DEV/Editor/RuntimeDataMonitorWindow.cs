using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using O2un.Core.Data;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace O2un.DEV
{
    public sealed class RuntimeDataMonitorWindow : EditorWindow
    {
        private sealed class RuntimeDataEntry
        {
            public Type Type { get; }
            public string Key { get; }
            public string Path { get; }

            public RuntimeDataEntry(Type type, string key)
            {
                Type = type;
                Key = key;
                Path = System.IO.Path.Combine(Application.persistentDataPath, $"{key}.json");
            }
        }

        private readonly List<RuntimeDataEntry> _entries = new();
        private readonly Dictionary<string, string> _validationErrors = new();

        private ScrollView _entryList;
        private ScrollView _fieldList;
        private Label _emptyState;
        private Label _selectedTypeLabel;
        private Label _statusLabel;
        private Label _validationLabel;
        private Button _saveButton;
        private Button _deleteButton;
        private RuntimeDataEntry _selectedEntry;
        private IRuntimeData _selectedData;
        private bool _hasUnsupportedFields;

        [MenuItem("Tools/O2un/Runtime Data Monitor", priority = 4951)]
        public static void ShowWindow()
        {
            var window = GetWindow<RuntimeDataMonitorWindow>("Runtime Data Monitor");
            window.minSize = new Vector2(640f, 420f);
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;

            root.Add(CreateToolbar());
            root.Add(CreateContent());

            RefreshEntries();
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(RefreshEntries) { text = "새로고침" });
            toolbar.Add(new ToolbarButton(OpenDataFolder) { text = "폴더 열기" });

            var pathLabel = new Label(Application.persistentDataPath);
            pathLabel.tooltip = Application.persistentDataPath;
            pathLabel.style.flexGrow = 1f;
            pathLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            pathLabel.style.marginRight = 6f;
            toolbar.Add(pathLabel);

            return toolbar;
        }

        private VisualElement CreateContent()
        {
            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Row;
            content.style.flexGrow = 1f;

            content.Add(CreateEntryPanel());
            content.Add(CreateEditorPanel());

            return content;
        }

        private VisualElement CreateEntryPanel()
        {
            var panel = new VisualElement();
            panel.style.width = 220f;
            panel.style.flexShrink = 0f;
            panel.style.paddingLeft = 6f;
            panel.style.paddingRight = 6f;
            panel.style.paddingTop = 6f;
            panel.style.paddingBottom = 6f;
            panel.style.borderRightWidth = 1f;
            panel.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f);

            var title = new Label("RuntimeData 타입");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4f;
            panel.Add(title);

            _emptyState = new Label("등록된 RuntimeData 타입이 없습니다.");
            _emptyState.style.whiteSpace = WhiteSpace.Normal;
            _emptyState.style.marginTop = 6f;
            panel.Add(_emptyState);

            _entryList = new ScrollView();
            _entryList.style.flexGrow = 1f;
            panel.Add(_entryList);

            return panel;
        }

        private VisualElement CreateEditorPanel()
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1f;
            panel.style.paddingLeft = 8f;
            panel.style.paddingRight = 8f;
            panel.style.paddingTop = 6f;
            panel.style.paddingBottom = 6f;

            _selectedTypeLabel = new Label("왼쪽에서 RuntimeData 타입을 선택하세요.");
            _selectedTypeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _selectedTypeLabel.style.marginBottom = 4f;
            panel.Add(_selectedTypeLabel);

            _statusLabel = new Label();
            _statusLabel.style.marginBottom = 4f;
            panel.Add(_statusLabel);

            _fieldList = new ScrollView();
            _fieldList.style.flexGrow = 1f;
            panel.Add(_fieldList);

            _validationLabel = new Label();
            _validationLabel.style.color = new Color(1f, 0.45f, 0.35f);
            _validationLabel.style.whiteSpace = WhiteSpace.Normal;
            _validationLabel.style.marginTop = 4f;
            panel.Add(_validationLabel);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.justifyContent = Justify.FlexEnd;
            actions.style.marginTop = 6f;

            _saveButton = new Button(SaveSelectedData) { text = "저장" };
            _saveButton.SetEnabled(false);
            actions.Add(_saveButton);

            _deleteButton = new Button(DeleteSelectedFile) { text = "파일 삭제" };
            _deleteButton.style.marginLeft = 4f;
            _deleteButton.SetEnabled(false);
            actions.Add(_deleteButton);

            panel.Add(actions);
            return panel;
        }

        private void RefreshEntries()
        {
            if (null == _entryList)
            {
                return;
            }

            string selectedKey = _selectedEntry?.Key;
            _entries.Clear();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<IRuntimeData>().Where(type => false == type.IsAbstract && null != type.GetConstructor(Type.EmptyTypes)))
            {
                var data = (IRuntimeData)Activator.CreateInstance(type);
                _entries.Add(new RuntimeDataEntry(type, data.Key));
            }

            _entries.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.OrdinalIgnoreCase));
            _entryList.Clear();

            foreach (RuntimeDataEntry entry in _entries)
            {
                RuntimeDataEntry currentEntry = entry;
                string suffix = File.Exists(entry.Path) ? string.Empty : " (파일 없음)";
                var button = new Button(() => SelectEntry(currentEntry)) { text = $"{entry.Key}{suffix}" };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginBottom = 2f;
                _entryList.Add(button);
            }

            _emptyState.style.display = 0 == _entries.Count ? DisplayStyle.Flex : DisplayStyle.None;

            RuntimeDataEntry selectedEntry = _entries.FirstOrDefault(entry => entry.Key == selectedKey);
            if (null != selectedEntry)
            {
                SelectEntry(selectedEntry);
            }
            else
            {
                ClearSelection();
            }
        }

        private void SelectEntry(RuntimeDataEntry entry)
        {
            try
            {
                var data = (IRuntimeData)Activator.CreateInstance(entry.Type);
                if (File.Exists(entry.Path))
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(entry.Path), data);
                }

                _selectedEntry = entry;
                _selectedData = data;
                _selectedTypeLabel.text = entry.Type.Name;
                _statusLabel.text = File.Exists(entry.Path) ? entry.Path : "아직 저장된 파일이 없습니다. 저장하면 생성됩니다.";
                BuildFieldEditors();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                EditorUtility.DisplayDialog("RuntimeData 읽기 실패", e.Message, "확인");
                ClearSelection();
            }
        }

        private void BuildFieldEditors()
        {
            _fieldList.Clear();
            _validationErrors.Clear();
            _hasUnsupportedFields = false;

            List<FieldInfo> fields = GetSerializedFields(_selectedEntry.Type);
            foreach (FieldInfo field in fields)
            {
                VisualElement editor = CreateFieldEditor(field);
                editor.tooltip = field.FieldType.Name;
                editor.style.marginBottom = 4f;
                _fieldList.Add(editor);
            }

            if (0 == fields.Count)
            {
                _fieldList.Add(new Label("수정 가능한 직렬화 필드가 없습니다."));
            }

            UpdateValidationState();
        }

        private VisualElement CreateFieldEditor(FieldInfo field)
        {
            string label = ObjectNames.NicifyVariableName(field.Name.TrimStart('_'));
            object value = field.GetValue(_selectedData);
            Type type = field.FieldType;

            if (typeof(string) == type)
            {
                var control = new TextField(label) { value = (string)value };
                control.RegisterValueChangedCallback(evt => field.SetValue(_selectedData, evt.newValue));
                return control;
            }

            if (typeof(bool) == type)
            {
                var control = new Toggle(label) { value = (bool)value };
                control.RegisterValueChangedCallback(evt => field.SetValue(_selectedData, evt.newValue));
                return control;
            }

            if (typeof(int) == type)
            {
                var control = new IntegerField(label) { value = (int)value };
                control.RegisterValueChangedCallback(evt => field.SetValue(_selectedData, evt.newValue));
                return control;
            }

            if (typeof(ushort) == type)
            {
                var control = new IntegerField(label) { value = (ushort)value };
                control.RegisterValueChangedCallback(evt => SetBoundedInteger(field, evt.newValue, ushort.MinValue, ushort.MaxValue, converted => (ushort)converted));
                return control;
            }

            if (typeof(short) == type)
            {
                var control = new IntegerField(label) { value = (short)value };
                control.RegisterValueChangedCallback(evt => SetBoundedInteger(field, evt.newValue, short.MinValue, short.MaxValue, converted => (short)converted));
                return control;
            }

            if (typeof(byte) == type)
            {
                var control = new IntegerField(label) { value = (byte)value };
                control.RegisterValueChangedCallback(evt => SetBoundedInteger(field, evt.newValue, byte.MinValue, byte.MaxValue, converted => (byte)converted));
                return control;
            }

            if (typeof(sbyte) == type)
            {
                var control = new IntegerField(label) { value = (sbyte)value };
                control.RegisterValueChangedCallback(evt => SetBoundedInteger(field, evt.newValue, sbyte.MinValue, sbyte.MaxValue, converted => (sbyte)converted));
                return control;
            }

            if (typeof(long) == type)
            {
                var control = new LongField(label) { value = (long)value };
                control.RegisterValueChangedCallback(evt => field.SetValue(_selectedData, evt.newValue));
                return control;
            }

            if (typeof(uint) == type)
            {
                var control = new LongField(label) { value = (uint)value };
                control.RegisterValueChangedCallback(evt => SetBoundedLong(field, evt.newValue, uint.MinValue, uint.MaxValue, converted => (uint)converted));
                return control;
            }

            if (typeof(ulong) == type)
            {
                var control = new TextField(label) { value = ((ulong)value).ToString(CultureInfo.InvariantCulture) };
                control.RegisterValueChangedCallback(evt => SetUnsignedLong(field, evt.newValue));
                return control;
            }

            if (typeof(float) == type)
            {
                var control = new FloatField(label) { value = (float)value };
                control.RegisterValueChangedCallback(evt => field.SetValue(_selectedData, evt.newValue));
                return control;
            }

            if (typeof(double) == type)
            {
                var control = new DoubleField(label) { value = (double)value };
                control.RegisterValueChangedCallback(evt => field.SetValue(_selectedData, evt.newValue));
                return control;
            }

            if (type.IsEnum)
            {
                var control = new EnumField(label, (Enum)value);
                control.RegisterValueChangedCallback(evt => field.SetValue(_selectedData, evt.newValue));
                return control;
            }

            _hasUnsupportedFields = true;
            return new Label($"{label}: 지원하지 않는 타입 ({type.Name})");
        }

        private void SetBoundedInteger(FieldInfo field, int value, int minimum, int maximum, Func<int, object> convert)
        {
            bool isValid = minimum <= value && value <= maximum;
            SetFieldValidation(field.Name, isValid, $"{minimum}~{maximum} 범위만 입력할 수 있습니다.");
            if (true == isValid)
            {
                field.SetValue(_selectedData, convert(value));
            }
        }

        private void SetBoundedLong(FieldInfo field, long value, long minimum, long maximum, Func<long, object> convert)
        {
            bool isValid = minimum <= value && value <= maximum;
            SetFieldValidation(field.Name, isValid, $"{minimum}~{maximum} 범위만 입력할 수 있습니다.");
            if (true == isValid)
            {
                field.SetValue(_selectedData, convert(value));
            }
        }

        private void SetUnsignedLong(FieldInfo field, string value)
        {
            bool isValid = ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed);
            SetFieldValidation(field.Name, isValid, "0 이상의 정수만 입력할 수 있습니다.");
            if (true == isValid)
            {
                field.SetValue(_selectedData, parsed);
            }
        }

        private void SetFieldValidation(string fieldName, bool isValid, string message)
        {
            if (true == isValid)
            {
                _validationErrors.Remove(fieldName);
            }
            else
            {
                _validationErrors[fieldName] = $"{ObjectNames.NicifyVariableName(fieldName.TrimStart('_'))}: {message}";
            }

            UpdateValidationState();
        }

        private void UpdateValidationState()
        {
            if (true == _hasUnsupportedFields)
            {
                _validationLabel.text = "지원하지 않는 필드 타입이 있어 저장할 수 없습니다.";
            }
            else
            {
                _validationLabel.text = string.Join(Environment.NewLine, _validationErrors.Values);
            }

            bool canSave = null != _selectedData && false == _hasUnsupportedFields && 0 == _validationErrors.Count;
            _saveButton.SetEnabled(canSave);
            _deleteButton.SetEnabled(null != _selectedEntry && File.Exists(_selectedEntry.Path));
        }

        private void SaveSelectedData()
        {
            if (null == _selectedEntry || null == _selectedData || true == _hasUnsupportedFields || 0 < _validationErrors.Count)
            {
                return;
            }

            try
            {
                File.WriteAllText(_selectedEntry.Path, JsonUtility.ToJson(_selectedData, true));
                _statusLabel.text = $"저장됨: {_selectedEntry.Path}";
                _deleteButton.SetEnabled(true);
                RefreshEntryButtonLabels();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                EditorUtility.DisplayDialog("RuntimeData 저장 실패", e.Message, "확인");
            }
        }

        private void DeleteSelectedFile()
        {
            if (null == _selectedEntry || false == File.Exists(_selectedEntry.Path))
            {
                return;
            }

            if (false == EditorUtility.DisplayDialog("RuntimeData 삭제", $"{_selectedEntry.Key}.json 파일을 삭제할까요?", "삭제", "취소"))
            {
                return;
            }

            try
            {
                File.Delete(_selectedEntry.Path);
                string selectedKey = _selectedEntry.Key;
                RefreshEntries();
                RuntimeDataEntry entry = _entries.FirstOrDefault(item => item.Key == selectedKey);
                if (null != entry)
                {
                    SelectEntry(entry);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                EditorUtility.DisplayDialog("RuntimeData 삭제 실패", e.Message, "확인");
            }
        }

        private void RefreshEntryButtonLabels()
        {
            int index = 0;
            foreach (VisualElement child in _entryList.Children())
            {
                if (child is Button button && index < _entries.Count)
                {
                    RuntimeDataEntry entry = _entries[index++];
                    string suffix = File.Exists(entry.Path) ? string.Empty : " (파일 없음)";
                    button.text = $"{entry.Key}{suffix}";
                }
            }
        }

        private static List<FieldInfo> GetSerializedFields(Type type)
        {
            var fields = new List<FieldInfo>();
            for (Type current = type; null != current && typeof(object) != current; current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    bool isSerialized = field.IsPublic || null != field.GetCustomAttribute<SerializeField>();
                    bool isHidden = null != field.GetCustomAttribute<HideInInspector>();
                    if (false == field.IsStatic && false == field.IsNotSerialized && true == isSerialized && false == isHidden)
                    {
                        fields.Add(field);
                    }
                }
            }

            fields.Sort((left, right) => left.MetadataToken.CompareTo(right.MetadataToken));
            return fields;
        }

        private static void OpenDataFolder()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                EditorUtility.RevealInFinder(Application.persistentDataPath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                EditorUtility.DisplayDialog("RuntimeData 폴더 열기 실패", e.Message, "확인");
            }
        }

        private void ClearSelection()
        {
            _selectedEntry = null;
            _selectedData = null;
            _validationErrors.Clear();
            _hasUnsupportedFields = false;
            _selectedTypeLabel.text = "왼쪽에서 RuntimeData 타입을 선택하세요.";
            _statusLabel.text = string.Empty;
            _validationLabel.text = string.Empty;
            _fieldList.Clear();
            _saveButton.SetEnabled(false);
            _deleteButton.SetEnabled(false);
        }
    }
}
