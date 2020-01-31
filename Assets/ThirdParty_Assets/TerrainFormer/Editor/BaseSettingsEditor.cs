using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class BaseSettingsEditor : EditorWindow {
        private static readonly List<Type> availableTypes = new List<Type> {
            typeof(int), typeof(float), typeof(bool), typeof(Vector3), typeof(string), typeof(Color), typeof(BrushSelectionDisplayType)
        };
        private static readonly Dictionary<Type, string> cSharpTypeNames = new Dictionary<Type, string> {
            { typeof(Boolean), "bool" },
            { typeof(Single), "float" },
            { typeof(Int32), "int" },
            { typeof(String), "string" }
        };
        private static GUIContent[] availableTypesGUIContent;
        private static string baseSettingsLocation;
        private static GUIStyle boldLabelCentred;
        private static GUIStyle miniBoldLabelCentred;

        private static ReorderableList settingsReorderableList;
        
        [MenuItem("Jesse Stiller/Terrain Former Settings Editor")]
        private static void OnEnable() {
            settingsReorderableList = new ReorderableList(new List<Setting>(), typeof(Setting));

            settingsReorderableList.displayAdd = true;
            settingsReorderableList.displayRemove = true;
            
            settingsReorderableList.drawElementCallback = (rect, index, isActive, isFocused) => {
                Setting setting = (Setting)settingsReorderableList.list[index];
                
                setting.newName = EditorGUI.TextField(new Rect(rect.x, rect.y, rect.width * 0.35f, EditorGUIUtility.singleLineHeight), setting.newName);

                int currentTypeIndex = 0;
                for(int i = 0; i < availableTypes.Count; i++) {
                    if(setting.UsedType == availableTypes[i]) {
                        currentTypeIndex = i;
                        break;
                    }
                }
                setting.UsedType = availableTypes[EditorGUI.Popup(new Rect(rect.x + (rect.width * 0.35f) + 10f, rect.y, rect.width * 0.25f - 10f, EditorGUIUtility.singleLineHeight), 
                    currentTypeIndex, availableTypesGUIContent)];
    
                Rect valueFieldRect = new Rect(rect.x + rect.width * 0.6f + 10f, rect.y, rect.width * 0.22f, EditorGUIUtility.singleLineHeight);
                switch(setting.UsedType.Name) {
                    case "Int32":
                        setting.defaultValue = EditorGUI.IntField(valueFieldRect, (int)setting.defaultValue);
                        break;
                    case "Boolean":
                        setting.defaultValue = EditorGUI.Toggle(valueFieldRect, (bool)setting.defaultValue);
                        break;
                    case "Single":
                        setting.defaultValue = EditorGUI.FloatField(valueFieldRect, (float)setting.defaultValue);
                        break;
                    case "Vector3":
                        setting.defaultValue = EditorGUI.Vector3Field(valueFieldRect, GUIContent.none, (Vector3)setting.defaultValue);
                        break;
                    case "Color":
                        setting.defaultValue = EditorGUI.ColorField(valueFieldRect, (Color)setting.defaultValue);
                        break;
                    case "BrushSelectionDisplayType":
                        setting.defaultValue = EditorGUI.EnumPopup(valueFieldRect, (BrushSelectionDisplayType)setting.defaultValue);
                        break;
                }

                GUILayout.Space(10f);

                EditorGUI.LabelField(new Rect(rect.x + rect.width * 0.82f + 20f, rect.y, 80f, EditorGUIUtility.singleLineHeight), "OnChanged");
                setting.hasChangedEvent = EditorGUI.Toggle(new Rect(rect.x + rect.width * 0.82f + 95f, rect.y, 20f, EditorGUIUtility.singleLineHeight), setting.hasChangedEvent);
            };
            settingsReorderableList.drawHeaderCallback = rect => {
                EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width * 0.35f, settingsReorderableList.headerHeight), "Name", boldLabelCentred);
                EditorGUI.LabelField(new Rect(rect.x + (rect.width * 0.35f) + 15f, rect.y, rect.width * 0.25f - 10f, settingsReorderableList.headerHeight), "Type", boldLabelCentred);
                EditorGUI.LabelField(new Rect(rect.x + rect.width * 0.6f + 12f, rect.y, rect.width * 0.25f, settingsReorderableList.headerHeight), "Default", boldLabelCentred);
                EditorGUI.LabelField(new Rect(rect.x + rect.width * 0.82f + 20f, rect.y, 100f, settingsReorderableList.headerHeight), "Changed Action", miniBoldLabelCentred);
            };
            settingsReorderableList.onCanRemoveCallback = reoderableList => {
                return reoderableList.count > 1;
            };
            settingsReorderableList.onRemoveCallback = reoderableList => {
                if(EditorUtility.DisplayDialog("Terrain Former Settings Editor", "Are you sure you want to remove the selected item?", "Remove", "Cancel")) {
                    ReorderableList.defaultBehaviours.DoRemoveButton(reoderableList);
                }
            };
            settingsReorderableList.onAddCallback = reoderableList => {
                int index = reoderableList.index;
                if(index < 0 || index > reoderableList.count) index = reoderableList.count;
                reoderableList.list.Insert(index, new Setting());
            };

            BaseSettingsEditor baseSettingsEditor = GetWindow<BaseSettingsEditor>();
#if UNITY_5_3_OR_NEWER && !UNITY_5_3_0 && !UNITY_5_3_1 && !UNITY_5_3_2 && !UNITY_5_3_3 && !UNITY_5_3_4 // Unity 5.3.5 or newer 
            baseSettingsEditor.titleContent.text = "Terrain Former - Properties Editor";
#endif
            baseSettingsEditor.ShowUtility();

            BaseSettings baseSettings = new BaseSettings();

            baseSettingsLocation = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(baseSettingsEditor));

            if(string.IsNullOrEmpty(baseSettingsLocation)) {
                Debug.LogError("Couldn't find the BaseSettings.cs file");
                return;
            }
            
            baseSettingsLocation = baseSettingsLocation.Substring(0, baseSettingsLocation.Length - 9) + ".cs";
            
            if(File.Exists(baseSettingsLocation) == false) {
                Debug.LogError("Couldn't find the BaseSettings.cs file");
                return;
            }
            
            FieldInfo[] fields = typeof(BaseSettings).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            foreach(FieldInfo field in fields) {
                string name;
                bool isDefaultField = false;
                bool hasChangedEvent = false;
                object defaultValue = null;

                Type fieldType = null;

                if(field.Name.EndsWith("Default")) {
                    name = field.Name.Remove(field.Name.Length - 7, 7);
                    defaultValue = field.GetValue(baseSettings);
                    fieldType = field.FieldType;
                    isDefaultField = true;
                } else if(field.FieldType == typeof(Action) && field.Name.EndsWith("Changed")) {
                    name = field.Name.Remove(field.Name.Length - 7, 7); // Remove "Changed"
                    name = name[0].ToString().ToLower() + name.Substring(1); // Make the first character lower case
                    hasChangedEvent = true;
                } else {
                    name = field.Name;
                    fieldType = field.FieldType;
                }

                Setting setting = null;
                foreach(Setting s in settingsReorderableList.list) {
                    if(s.newName == name) {
                        setting = s;
                        break;
                    }
                }
                bool addNewSetting = false;
                if(setting == null) {
                    addNewSetting = true;
                    setting = new Setting(name);
                }

                if(fieldType != null) setting.UsedType = fieldType;
                if(isDefaultField) setting.defaultValue = defaultValue;
                if(hasChangedEvent) setting.hasChangedEvent = true;

                if(addNewSetting) {
                    settingsReorderableList.list.Add(setting);
                }
            }
            
            availableTypesGUIContent = new GUIContent[availableTypes.Count];

            for(int i = 0; i < availableTypes.Count; i++) {
                availableTypesGUIContent[i] = new GUIContent(availableTypes[i].Name, availableTypes[i].FullName);
            }
        }

        private void OnGUI() {
            if(settingsReorderableList == null) OnEnable();

            if(boldLabelCentred == null) {
                boldLabelCentred = new GUIStyle(EditorStyles.boldLabel);
                boldLabelCentred.alignment = TextAnchor.MiddleCenter;

                miniBoldLabelCentred = new GUIStyle(boldLabelCentred);
                miniBoldLabelCentred.fontSize = 10;
            }

            settingsReorderableList.DoLayoutList();
            
            bool enableSaveButton = true;

            // Check for identical names
            int x = 0;
            foreach(Setting s1 in settingsReorderableList.list) {
                int y = 0;

                foreach(Setting s2 in settingsReorderableList.list) {
                    if(x == y) continue;

                    if(s1.newName == s2.newName) {
                        EditorGUILayout.HelpBox("There are settings with the matching name of: " + s1.newName, MessageType.Error);
                        enableSaveButton = false;
                    }

                    y++;
                }
                x++;
            }

            GUI.enabled = enableSaveButton;
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("Save", GUILayout.Width(95f), GUILayout.Height(22f))) {
                Save();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUI.enabled = true;
        }

        private void Save() {
            using(StreamWriter sw = File.CreateText(baseSettingsLocation)) {
                sw.WriteLine("using System;");
                sw.WriteLine("using TinyJSON;");
                sw.WriteLine("using UnityEngine;" + Environment.NewLine);
                sw.WriteLine("namespace JesseStiller.TerrainFormerExtension {");
                sw.WriteLine("\t// NOTE: This class is auto-generated by the author of this software \"Jesse Stiller\"");
                sw.WriteLine("\tinternal class BaseSettings {");

                StringBuilder areSettingsDefaultCode = new StringBuilder("\t\tinternal virtual bool AreSettingsDefault() {" + Environment.NewLine + "\t\t\treturn ");
                StringBuilder restoreDefaultSettingsCode = new StringBuilder("\t\tinternal virtual void RestoreDefaultSettings() {" + Environment.NewLine);

                int settingNumber = 0;
                foreach(Setting setting in settingsReorderableList.list) {
                    string cSharpType;
                    if(cSharpTypeNames.ContainsKey(setting.UsedType)) {
                        cSharpType = cSharpTypeNames[setting.UsedType];
                    } else {
                        cSharpType = setting.UsedType.Name;
                    }
                    
                    // Field: Constant/Readonly default value
                    string defaultValueAccessModifier = setting.UsedType.IsPrimitive || setting.UsedType.IsEnum ? "private const" : "private static readonly";
                    string defaultValueDeclaration;
                    if(setting.UsedType == typeof(bool)) {
                        defaultValueDeclaration = ((bool)setting.defaultValue).ToString().ToLower();
                    } else if(setting.UsedType == typeof(float)) {
                        defaultValueDeclaration = ((float)setting.defaultValue) + "f";
                    } else if(setting.UsedType == typeof(Color)) {
                        Color color = (Color)setting.defaultValue;
                        /**
                        * TODO: It should save the colours as fractions (eg, not 0.69803... but instead 178/255) if they contain decimal places, and if they are 
                        * whole numbers display them as they are. Or it could save them as hexadecimal.
                        */
                        defaultValueDeclaration = string.Format("new Color({0}f, {1}f, {2}f, {3}f)", color.r, color.g, color.b, color.a);
                    } else if(setting.UsedType.IsEnum) {
                        defaultValueDeclaration = setting.UsedType.Name + "." + setting.defaultValue;
                    } else {
                        defaultValueDeclaration = Convert.ChangeType(setting.defaultValue, setting.UsedType).ToString();
                    }
                    
                    sw.WriteLine("\t\t" + string.Format("{0} {1} {2}Default = {3};", defaultValueAccessModifier, cSharpType, setting.newName, defaultValueDeclaration));

                    // Field: Instance value
                    //string instanceValueAccessModifier = setting.hasChangedEvent ? "private" : "internal";
                    // HACK: private instance modifiers stop alwaysShowBrushSelection from being serialized
                    string instanceValueAccessModifier = "internal";
                    sw.WriteLine("\t\t[Include]");
                    sw.WriteLine("\t\t" + string.Format("{0} {1} {2} = {2}Default;", instanceValueAccessModifier, cSharpType, setting.newName));
                    
                    if(setting.hasChangedEvent) {
                        string firstCharacter = setting.newName[0].ToString();
                        string actionName = setting.newName.Remove(0, 1).Insert(0, firstCharacter.ToUpper()) + "Changed";
                        
                        // Property: Instance property
                        string propertyName = firstCharacter.ToUpper() + setting.newName.Remove(0, 1);
                        sw.WriteLine("\t\tinternal " + cSharpType + " " + propertyName + " {");
                        sw.WriteLine("\t\t\tget {");
                        sw.WriteLine("\t\t\t\treturn {0};", setting.newName);
                        sw.WriteLine("\t\t\t}");
                        sw.WriteLine("\t\t\tset {");
                        sw.WriteLine("\t\t\t\tif(value == {0}) return;", setting.newName);
                        sw.WriteLine("\t\t\t\t");
                        sw.WriteLine("\t\t\t\t{0} = value;", setting.newName);
                        sw.WriteLine("\t\t\t\tif({0} != null) {0}();", actionName);
                        sw.WriteLine("\t\t\t}");
                        sw.WriteLine("\t\t}");

                        // Action: Changed
                        sw.WriteLine("\t\tinternal Action {0};", actionName);
                    }

                    // Method: Are Settings Default
                    areSettingsDefaultCode.Append(setting.newName + " == " + setting.newName + "Default");
                    if(settingNumber != settingsReorderableList.list.Count - 1) {
                        areSettingsDefaultCode.Append(" &&" + Environment.NewLine + "\t\t\t\t");
                    }

                    // Method: Restore Default Settings
                    restoreDefaultSettingsCode.AppendLine("\t\t\t" + setting.newName + " = " + setting.newName + "Default;");

                    sw.WriteLine();

                    settingNumber++;
                }
                
                areSettingsDefaultCode.Append(";" + Environment.NewLine + "\t\t}");
                restoreDefaultSettingsCode.Append("\t\t}");
                
                sw.Write(areSettingsDefaultCode);
                sw.WriteLine();
                sw.WriteLine();
                sw.Write(restoreDefaultSettingsCode);
                sw.WriteLine();

                sw.Write("\t}" + Environment.NewLine + "}");
            }
        }

        private class Setting {
            internal string newName;
            private Type usedType;
            internal Type UsedType {
                get {
                    return usedType;
                }
                set {
                    if(value == usedType) return;
                    usedType = value;
                    MakeDefaultValueTypeDefault();
                }
            }
            internal object defaultValue;
            internal bool hasChangedEvent;

            internal Setting() {
                newName = "";
                usedType = typeof(Boolean);
                defaultValue = false;
            }

            internal Setting(string name) {
                newName = name;
            }

            private void MakeDefaultValueTypeDefault() {
                switch(usedType.Name) {
                    case "Int32":
                        defaultValue = default(int);
                        break;
                    case "Boolean":
                        defaultValue = default(bool);
                        break;
                    case "Single":
                        defaultValue = default(float);
                        break;
                    case "Vector3":
                        defaultValue = new Vector3();
                        break;
                    case "Color":
                        defaultValue = new Color();
                        break;
                    case "BrushSelectionDisplayType":
                        defaultValue = BrushSelectionDisplayType.Tabbed;
                        break;
                }
            }
        }
    }
}