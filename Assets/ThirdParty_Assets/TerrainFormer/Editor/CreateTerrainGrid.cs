using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace JesseStiller.TerrainFormerExtension {
    public class CreateTerrainGrid : EditorWindow {
        private const string heightmapResolutionWarning = "Terrains with a heightmap resolution of 2049 or higher may lead to poor performance while sculpting or in-game. (In general, it's better to have multiple terrains with a lower heightmap resolution rather than a few terrains with a maximum heightmap resolution of 4097.)";

        private int cellSize = 24;

        private string parentName = "Terrain Grid";
        private string saveDirectory = "Terrains";

        // The terrain that all created terrains will copy off in terms of settings, brushes, textures and size. Can be null.
        private UnityEngine.Object terrainTemplate;

        private bool[,] terrainsToCreate;
        private int terrainsHorizontally = 5;
        private int terrainsVertically = 5;

        // Terrain settings
        private bool showTerrainSettings = true;
        private float terrainLateralSize = 500f;
        private float terrainHeight = 100f;
        private int heightmapResolution = 513;
        private int detailResolution = 1024;
        private int detailResolutionPerPatch = 8;
        private int controlTextureResolution = 512; // TODO: This isn't being set from the template terrain because it's a private property
        private float basemapDistance = 1000f;
        private int baseTextureResolution = 1024;

        private bool showStatistics = false;

        private GUIStyle cellStyle;
        private GUIStyle arrowFloatFieldStyle;
        private GUIStyle verticalArrowGUIStyle;
        private GUIStyle horizontalArrowGUIStyle;
        private GUIStyle boldFoldoutStyle;

        private Texture2D terrainActiveTexture, terrainInactiveTexture, arrowHorizontalTexture, arrowVerticalTexture, mutedTextFieldFocusedTexture;

        private bool paintActive;

        private Vector2 lastMouseDownPosition;
        private Vector2 scrollPosition;

        private StringBuilder warningHelpBoxText;
#if UNITY_EDITOR_WIN
        [MenuItem("GameObject/3D Object/&Create Terrain Grid…")]
#else
        [MenuItem("GameObject/3D Object/Create Terrain Grid…")]
#endif
        private static void Initialize() {
            CreateTerrainGrid createTerrainGrid = CreateInstance<CreateTerrainGrid>();
            createTerrainGrid.minSize = new Vector2(450f, 548f);
            createTerrainGrid.maxSize = new Vector2(1800f, 1000f);
            createTerrainGrid.ShowUtility();

#if !UNITY_5_0
            createTerrainGrid.titleContent = new GUIContent("Create Terrain Grid");
#else
            createTerrainGrid.title = "Create Terrain Grid";
#endif
        }

        void OnEnable() {
            terrainsToCreate = new bool[terrainsHorizontally, terrainsVertically];
            for(int x = 0; x < terrainsHorizontally; x++) {
                for(int y = 0; y < terrainsVertically; y++) {
                    terrainsToCreate[x, y] = true;
                }
            }

            cellStyle = new GUIStyle();
            cellStyle.border = new RectOffset(1, 1, 1, 1);

            if(string.IsNullOrEmpty(TerrainFormerEditor.texturesDirectory)) TerrainFormerEditor.InitializeSettings();

            // NOTE: LoadAssetAtPath is not using the generic version to ensure compatability with Unity 5.0
            Type texture2DType = typeof(Texture2D);
            terrainActiveTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(TerrainFormerEditor.texturesDirectory + "TerrainActive.png", texture2DType);
            terrainInactiveTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(TerrainFormerEditor.texturesDirectory + "TerrainInactive.png", texture2DType);
            arrowVerticalTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(TerrainFormerEditor.texturesDirectory + "ArrowVertical.psd", texture2DType);
            arrowHorizontalTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(TerrainFormerEditor.texturesDirectory +  "ArrowHorizontal.psd", texture2DType);
            mutedTextFieldFocusedTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(TerrainFormerEditor.texturesDirectory + "MutedTextFieldFocused.psd", texture2DType);
        }

        private void UpdateTerrainsToCreate() {
            bool[,] newTerrainsToCreate = new bool[terrainsHorizontally, terrainsVertically];
            for(int x = 0; x < terrainsHorizontally; x++) {
                for(int y = 0; y < terrainsVertically; y++) {
                    if(x < terrainsToCreate.GetLength(0) && y < terrainsToCreate.GetLength(1)) {
                        newTerrainsToCreate[x, y] = terrainsToCreate[x, y];
                    } else {
                        newTerrainsToCreate[x, y] = true;
                    }
                }
            }

            terrainsToCreate = newTerrainsToCreate;
        }

        private static bool IsNullOrWhiteSpace(string value) {
            if(value == null) return true;

            for(int i = 0; i < value.Length; i++) {
                if(char.IsWhiteSpace(value[i]) == false) return false;
            }
            return true;
        }

        void OnGUI() {
            EditorGUIUtility.labelWidth = 220f;

            if(arrowFloatFieldStyle == null) {
                arrowFloatFieldStyle = new GUIStyle(GUI.skin.textField);
                arrowFloatFieldStyle.normal.background = null;
                arrowFloatFieldStyle.focused.background = mutedTextFieldFocusedTexture;
            }
            if(horizontalArrowGUIStyle == null) {
                horizontalArrowGUIStyle = new GUIStyle(GUI.skin.box);
                horizontalArrowGUIStyle.normal.background = arrowHorizontalTexture;
                horizontalArrowGUIStyle.border = new RectOffset(8, 8, 0, 0);
            }
            if(verticalArrowGUIStyle == null) {
                verticalArrowGUIStyle = new GUIStyle(GUI.skin.box);
                verticalArrowGUIStyle.normal.background = arrowVerticalTexture;
                verticalArrowGUIStyle.border = new RectOffset(0, 0, 8, 8);
            }
            if(boldFoldoutStyle == null) {
                boldFoldoutStyle = new GUIStyle(GUI.skin.GetStyle("foldout"));
                boldFoldoutStyle.fontStyle = FontStyle.Bold;
            }
            
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(4f);

            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
            EditorGUI.indentLevel = 1;
            parentName = EditorGUILayout.TextField(TerrainSettings.nameGUIContent, parentName);
            
            EditorGUI.BeginChangeCheck();
            // TODO: A terrain template contains more data than simply a TerrainData template
            terrainTemplate = EditorGUILayout.ObjectField(TerrainSettings.terrainTemplateGUIContent, terrainTemplate, typeof(UnityEngine.Object), true);
            // Check if the template terrain has been set to a new value
            if(EditorGUI.EndChangeCheck()) {
                TerrainData terrainData = null;
                if(terrainTemplate is Terrain) {
                    Terrain terrain = (Terrain)terrainTemplate;
                    terrainData = terrain.terrainData;
                } else if(terrainTemplate is TerrainData) {
                    terrainData = (TerrainData)terrainTemplate;
                }

                if(terrainData == null) {
                    terrainTemplate = null;
                } else { 
                    terrainLateralSize = terrainData.size.x;
                    terrainHeight = terrainData.size.y;
                    heightmapResolution = terrainData.heightmapResolution;
                    detailResolution = terrainData.detailResolution;
                    baseTextureResolution = terrainData.baseMapResolution;
                }
            }

            int oldHorizontalTerrains = terrainsHorizontally;
            int oldVerticalTerrains = terrainsVertically;

            terrainsHorizontally = EditorGUILayout.IntSlider("Horizontal Count", terrainsHorizontally, 1, 10);
            terrainsVertically = EditorGUILayout.IntSlider("Vertical Count", terrainsVertically, 1, 10);

            if(oldHorizontalTerrains != terrainsHorizontally || oldVerticalTerrains != terrainsVertically) {
                UpdateTerrainsToCreate();
            }

            EditorGUI.indentLevel = 0;
            showTerrainSettings = EditorGUILayout.Foldout(showTerrainSettings, "Terrain Settings", boldFoldoutStyle);
            if(showTerrainSettings) {
                EditorGUI.indentLevel = 1;
                terrainLateralSize = Mathf.Max(EditorGUILayout.FloatField("Terrain Width/Length", terrainLateralSize), 1f);
                terrainHeight = Mathf.Max(EditorGUILayout.FloatField("Terrain Height", terrainHeight), 1f);
                
                heightmapResolution = EditorGUILayout.IntPopup(TerrainSettings.heightmapResolutionContent, heightmapResolution, TerrainSettings.heightmapResolutionsContents, TerrainSettings.heightmapResolutions);

                detailResolution = Utilities.RoundToNearestAndClamp(
                    currentNumber: EditorGUILayout.IntField(TerrainSettings.detailResolutionContent, detailResolution),
                    desiredNearestNumber: 8,
                    minimum: 0,
                    maximum: 4048
                );
                detailResolutionPerPatch = Utilities.RoundToNearestAndClamp(
                    currentNumber: EditorGUILayout.IntField(TerrainSettings.detailResolutionPerPatchContent, detailResolutionPerPatch), 
                    desiredNearestNumber: 8, 
                    minimum: 8, 
                    maximum: 128
                );

                controlTextureResolution = Mathf.Clamp(EditorGUILayout.IntField(TerrainSettings.controlTextureResolutionContent, controlTextureResolution), 0, 2000);
                basemapDistance = Mathf.Clamp(EditorGUILayout.FloatField(TerrainSettings.basemapDistanceContent, basemapDistance), 0f, 2000f);
                baseTextureResolution = Mathf.Clamp(EditorGUILayout.IntField(TerrainSettings.baseTextureResolutionContent, baseTextureResolution), 0, 2000);
                EditorGUI.indentLevel = 0;
            }
            
            EditorGUILayout.LabelField("Terrain Grid Layout", EditorStyles.boldLabel);

            GUILayout.Space(35f);
            
            float tableWidth = terrainsHorizontally * cellSize;
            float tableHeight = terrainsVertically * cellSize;
            Rect terrainsGridRect = GUILayoutUtility.GetRect(tableWidth, tableHeight);
            terrainsGridRect = new Rect(Screen.width * 0.5f - (tableWidth * 0.5f), terrainsGridRect.y, tableWidth, tableHeight);
            
            // Painting terrains as active and inactive
            Event currentEvent = Event.current;
            if(currentEvent.isMouse) {
                Vector3 mousePosition = Event.current.mousePosition;
                if(mousePosition.x > terrainsGridRect.x && mousePosition.x <= terrainsGridRect.xMax && mousePosition.y > terrainsGridRect.y && mousePosition.y <= terrainsGridRect.yMax) {
                    Vector2 deltaFromRect = new Vector2(mousePosition.x - terrainsGridRect.x, mousePosition.y - terrainsGridRect.y);

                    int cellX = Mathf.CeilToInt((deltaFromRect.x / tableWidth) * terrainsHorizontally) - 1;
                    int cellY = Mathf.CeilToInt((deltaFromRect.y / tableHeight) * terrainsVertically) - 1;

                    if(Event.current.type == EventType.MouseDrag) {
                        terrainsToCreate[cellX, cellY] = paintActive;
                        Repaint();
                    } else if(Event.current.type == EventType.MouseDown) {
                        lastMouseDownPosition = currentEvent.mousePosition;
                        paintActive = !terrainsToCreate[cellX, cellY];
                    } else if(currentEvent.type == EventType.MouseUp && lastMouseDownPosition == currentEvent.mousePosition) {
                        terrainsToCreate[cellX, cellY] = !terrainsToCreate[cellX, cellY];
                        Repaint();
                    }
                }
            }
            
            /**
            * Draw the terrain grid's visual representation
            */
            bool atLeastOneActiveTerrain = false;
            for(int x = 0; x < terrainsHorizontally; x++) {
                for(int y = 0; y < terrainsVertically; y++) {
                    if(terrainsToCreate[x, y]) {
                        cellStyle.normal.background = terrainActiveTexture;
                        atLeastOneActiveTerrain = true;
                    } else {
                        cellStyle.normal.background = terrainInactiveTexture;
                    }
                    
                    GUI.Box(new Rect(terrainsGridRect.x + x * cellSize, terrainsGridRect.y + y * cellSize, cellSize, cellSize), GUIContent.none, cellStyle);
                }
            }

            /**
            * Total Horizontal Terrain Grid Size
            */
            arrowFloatFieldStyle.alignment = TextAnchor.MiddleCenter;
            Rect horizontalArrowRect = new Rect(Screen.width * 0.5f - tableWidth * 0.5f, terrainsGridRect.y - 20f, tableWidth, 16f);
            GUI.Box(horizontalArrowRect, GUIContent.none, horizontalArrowGUIStyle);
            
            EditorGUI.BeginChangeCheck();
            float totalSizeHorizontally = EditorGUI.FloatField(new Rect(Screen.width * 0.5f - 27f, terrainsGridRect.y - 36f, 54f, 18f), terrainsHorizontally * terrainLateralSize, arrowFloatFieldStyle);
            if(EditorGUI.EndChangeCheck()) {
                terrainLateralSize = Mathf.Max(totalSizeHorizontally / terrainsHorizontally, 1f);
            }

            /**
            * Total Vertical Terrain Grid Size
            */
            arrowFloatFieldStyle.alignment = TextAnchor.MiddleRight;
            Rect verticalArrowRect = new Rect(terrainsGridRect.x - 20f, terrainsGridRect.y, 16f, tableHeight);
            GUI.Box(verticalArrowRect, GUIContent.none, verticalArrowGUIStyle);
            EditorGUI.BeginChangeCheck();
            float totalSizeVertically = EditorGUI.FloatField(new Rect(terrainsGridRect.x - 72f, terrainsGridRect.y + tableHeight * 0.5f - 9f, 54f, 18f), 
                terrainsVertically * terrainLateralSize, arrowFloatFieldStyle);
            if(EditorGUI.EndChangeCheck()) {
                terrainLateralSize = Mathf.Max(totalSizeVertically / terrainsVertically, 1f);
            }

            GUILayout.Space(8f);

            /**
            * Cell types legend (expalining what the empty and green filled cells mean)
            */
            GUI.BeginGroup(EditorGUILayout.GetControlRect());
            cellStyle.normal.background = terrainActiveTexture;
            GUI.Box(new Rect(Screen.width * 0.5f - 126f, 1f, 14f, 14f), GUIContent.none, cellStyle);
            GUI.Label(new Rect(Screen.width * 0.5f - 108f, 0f, 84f, 14f), "Create terrain");
            cellStyle.normal.background = terrainInactiveTexture;
            GUI.Box(new Rect(Screen.width * 0.5f - 16f, 1f, 14f, 14f), GUIContent.none, cellStyle);
            GUI.Label(new Rect(Screen.width * 0.5f + 2f, 0f, 115f, 14f), "Don't create terrain");
            GUI.EndGroup();
            
            int numberOfTerrainsToBeCreated = 0;
            foreach(bool createTerrain in terrainsToCreate) {
                if(createTerrain) numberOfTerrainsToBeCreated++;
            }
            int totalTerrainCells = terrainsToCreate.GetLength(0) * terrainsToCreate.GetLength(1);

            GUILayout.Space(5f);

            /**
            * Select/Deselect All Cells
            */
            GUILayoutOption[] selectDeselectAllCellsLayoutOptions = new GUILayoutOption[] { GUILayout.Width(totalTerrainCells == 1 ? 110 : totalTerrainCells == 2 ? 145 : 135f), GUILayout.Height(25f) };
            using(new EditorGUILayout.HorizontalScope()) {
                GUILayout.FlexibleSpace();
                GUI.enabled = numberOfTerrainsToBeCreated != totalTerrainCells;

                string specificCellText = totalTerrainCells == 1 ? "Cell" : totalTerrainCells == 2 ? "Both Cells" : "All Cells";

                if(GUILayout.Button("Select " + specificCellText, selectDeselectAllCellsLayoutOptions)) {
                    SetTerrainsActive(true);
                }
                GUI.enabled = numberOfTerrainsToBeCreated != 0; 
                if(GUILayout.Button("Deselect " + specificCellText, selectDeselectAllCellsLayoutOptions)) {
                    SetTerrainsActive(false);
                }
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(6f);

            // Warn the user if they are making a grid with far too many samples
            double heightmapSamplesPerTerrain = Math.Pow(heightmapResolution, 2d);
            double totalHeightmapSamples = numberOfTerrainsToBeCreated * heightmapSamplesPerTerrain;
            double heightmapSampleMillions = totalHeightmapSamples / 1000000d;
            bool showHeightmapSamplesWarning = totalHeightmapSamples > 50000000; // 50 million
            bool showHeightmapResolutionWarning = heightmapResolution == 2049 && numberOfTerrainsToBeCreated > 4 || heightmapResolution == 4097 && numberOfTerrainsToBeCreated > 2;
            
            if(showHeightmapSamplesWarning || showHeightmapResolutionWarning) {
                if(warningHelpBoxText == null) warningHelpBoxText = new StringBuilder();
                warningHelpBoxText.Length = 0;
                
                if(showHeightmapSamplesWarning) {
                    warningHelpBoxText.Append("The current terrain grid configuration will have ");
                    warningHelpBoxText.Append(heightmapSampleMillions.ToString("0.0"));
                    warningHelpBoxText.Append(" million heightmap samples. This may lead to poor performance while sculpting or in-game.");
                }
                if(showHeightmapSamplesWarning && showHeightmapResolutionWarning) {
                    warningHelpBoxText.Append("\n\n");
                }
                if(showHeightmapResolutionWarning) {
                    warningHelpBoxText.Append(heightmapResolutionWarning);
                }
                EditorGUILayout.HelpBox(warningHelpBoxText.ToString(), MessageType.Warning);
            }

            /**
            * Create button
            */
            
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if(GUILayout.Button("Create…", new GUILayoutOption[] { GUILayout.Width(85f), GUILayout.Height(25f) })) {
                    if(IsNullOrWhiteSpace(parentName)) {
                        EditorUtility.DisplayDialog("Create", "The parent name can't be empty.", "Close");
                        return;
                    }

                    if(atLeastOneActiveTerrain == false) {
                        EditorUtility.DisplayDialog("Create", "At least one terrain cell must be selected.", "Close");
                        return;
                    }

                    GUILayout.EndScrollView();
                    
                    CreateGrid();
                    return;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            
            
            /**
            * Statistics
            */
            showStatistics = EditorGUILayout.Foldout(showStatistics, "Statistics", boldFoldoutStyle);
            if(showStatistics) {
                float heightmapSampleDensity = heightmapResolution / terrainLateralSize;

                EditorGUI.indentLevel = 1;
                EditorGUILayout.LabelField("Heightmap sample density (units):", string.Format("{0:N2}", heightmapSampleDensity));
                EditorGUILayout.LabelField("Heightmap samples per terrain:", heightmapSamplesPerTerrain.ToString("#,##0"));
                EditorGUILayout.LabelField("Total heightmap samples:", totalHeightmapSamples.ToString("#,##0"));
                EditorGUILayout.LabelField("Estimated asset size (each):", FormatBytes(heightmapSamplesPerTerrain * 4d));
                EditorGUILayout.LabelField("Estimated total size of assets:", FormatBytes(totalHeightmapSamples * 4d));
                EditorGUI.indentLevel = 0;
            }
            
            GUILayout.EndScrollView();
        }

        private void SetTerrainsActive(bool active) {
            for(int x = 0; x < terrainsHorizontally; x++) {
                for(int y = 0; y < terrainsVertically; y++) {
                    terrainsToCreate[x, y] = active;
                }
            }
        }

        private void CreateGrid(string folderBrowserDialogTitle = "Create") {
            string newSaveDirectory = EditorUtility.SaveFolderPanel(folderBrowserDialogTitle, "/Assets", null);
            if(string.IsNullOrEmpty(newSaveDirectory)) return;

            if(IsDirectoryValid(newSaveDirectory)) {
                if(newSaveDirectory == Application.dataPath) {
                    saveDirectory = "";
                } else {
                    saveDirectory = newSaveDirectory.Remove(0, Application.dataPath.Length + 1);
                }
            } else {
                if(EditorUtility.DisplayDialog("Select Folder", "This directory is invalid because it isn't inside the current project's Assets folder.", "Select Different Folder", "Cancel")) {
                    CreateGrid("Select Different Folder");
                }
                return;
            }
            
            /**
            * Find any files that will be overwritten
            */
            StringBuilder filesToBeOverwrittenNewLineList = new StringBuilder();
            StringBuilder filesToBeOverwrittenCommaList = new StringBuilder("\n");
            int numberOfFilesToBeOverwritten = 0;
            
            for(int y = 0; y < terrainsVertically; y++) {
                for(int x = 0; x < terrainsHorizontally; x++) {
                    if(terrainsToCreate[x, y] == false) continue;
                    
                    string localTerrainSavePath = saveDirectory + "/" + GetTerrainName(x + 1, y + 1) + ".asset";
                    
                    if(File.Exists(Application.dataPath + "/" + localTerrainSavePath)) {
                        string fileName = Path.GetFileNameWithoutExtension(localTerrainSavePath);
                        filesToBeOverwrittenNewLineList.Append("\n   • " + fileName);
                        filesToBeOverwrittenCommaList.Append((numberOfFilesToBeOverwritten > 0 ? ", " : "") + "\"" + fileName + "\"");
                        numberOfFilesToBeOverwritten++;
                    }
                }
            }

            if(numberOfFilesToBeOverwritten > 0) {
                string message = "The following asset file" + (numberOfFilesToBeOverwritten > 1 ? "s" : "") + " will be overwritten in " + (string.IsNullOrEmpty(saveDirectory) ? "the Assets folder" : "the folder \"" +
                    saveDirectory + "\"") + string.Format(":{0}\n\nDo you want to create the terrain grid anyway?", numberOfFilesToBeOverwritten <= 60 ? filesToBeOverwrittenNewLineList : filesToBeOverwrittenCommaList);
                if(EditorUtility.DisplayDialog("Select Folder", message, "Create Anyway", "Cancel") == false) return;
            }
            
            Terrain[,] createdTerrains = new Terrain[terrainsHorizontally, terrainsVertically];
            GameObject terrainParentGameObject = new GameObject(parentName);
            Vector2 halfGridTerrainSize = new Vector2(terrainsHorizontally * terrainLateralSize * 0.5f, terrainsVertically * terrainLateralSize * 0.5f);
            float totalOperations = terrainsHorizontally * terrainsVertically;
            float operationsCompleted = 0f;
            string progressDialogTitle = numberOfFilesToBeOverwritten > 0 ? "Create Anyway" : "Select Folder";
            EditorUtility.DisplayCancelableProgressBar(progressDialogTitle, "Creating and saving terrain assets…", 0f);

            string dataPathWithoutAssets = Application.dataPath.Remove(Application.dataPath.Length - 6);

            Directory.CreateDirectory(dataPathWithoutAssets + "/" + saveDirectory + "/");

            // Used only to delete newly created files when the creation task is cancelled
            List<string> filesCreated = new List<string>();

            for(int y = 0; y < terrainsVertically; y++) {
                for(int x = 0; x < terrainsHorizontally; x++) {
                    if(terrainsToCreate[x, y] == false) continue;

                    string terrainName = GetTerrainName(x + 1, y + 1);
                    GameObject terrainGameObject = new GameObject(terrainName);
                    terrainGameObject.transform.parent = terrainParentGameObject.transform;
                    terrainGameObject.transform.position = new Vector3(x * terrainLateralSize - halfGridTerrainSize.x, 0f, y * terrainLateralSize - halfGridTerrainSize.y);
                    Terrain terrain = terrainGameObject.AddComponent<Terrain>();

                    TerrainData terrainData = new TerrainData();
                    terrainData.heightmapResolution = heightmapResolution;
                    terrainData.SetDetailResolution(detailResolution, detailResolutionPerPatch);
                    terrainData.baseMapResolution = baseTextureResolution;
                    terrainData.size = new Vector3(terrainLateralSize, terrainHeight, terrainLateralSize);

                    TerrainCollider terrainCollider = terrainGameObject.AddComponent<TerrainCollider>();
                    
                    string terrainSaveLocalPath = "Assets/" + saveDirectory + "/" + terrainName + ".asset";
                    
                    AssetDatabase.CreateAsset(terrainData, terrainSaveLocalPath);
                    
                    terrainCollider.terrainData = terrainData;
                    terrain.terrainData = terrainData;
                    createdTerrains[x, y] = terrain;

                    operationsCompleted++;
                    filesCreated.Add(terrainSaveLocalPath);

                    if(EditorUtility.DisplayCancelableProgressBar(progressDialogTitle, "Creating and saving terrain assets…", operationsCompleted / totalOperations)) {
                        EditorUtility.ClearProgressBar();
                        // Destroy all created Terrains
                        foreach(Terrain t in createdTerrains) {
                            if(t == null) continue;
                            DestroyImmediate(t.gameObject);
                        }
                        // Delete all newly created files
                        foreach(string createdFile in filesCreated) {
                            if(File.Exists(dataPathWithoutAssets + createdFile) == false) continue;
                            AssetDatabase.DeleteAsset(createdFile);
                        }
                        DestroyImmediate(terrainParentGameObject);
                        return;
                    }
                }
            }
            EditorUtility.ClearProgressBar();
            
            // Set neighbours
            for(int x = 0; x < terrainsHorizontally; x++) {
                for(int y = 0; y < terrainsVertically; y++) {
                    if(terrainsToCreate[x, y] == false) continue;

                    Terrain topTerrain = null;
                    Terrain leftTerrain = null;
                    Terrain rightTerrain = null;
                    Terrain bottomTerrain = null;

                    // Top bounds check
                    if(y != terrainsVertically - 1) {
                        topTerrain = createdTerrains[x, y + 1];
                    }
                    // Bottom bounds check
                    if(y != 0) {
                        bottomTerrain = createdTerrains[x, y - 1];
                    }
                    // Left bounds check
                    if(x != 0) {
                        leftTerrain = createdTerrains[x - 1, y];
                    }
                    // Right bounds check
                    if(x != terrainsHorizontally - 1) {
                        rightTerrain = createdTerrains[x + 1, y];
                    }

                    TerrainSetNeighbours setNeighboursComponent = createdTerrains[x, y].gameObject.AddComponent<TerrainSetNeighbours>();
                    setNeighboursComponent.SetNeighbours(leftTerrain, topTerrain, rightTerrain, bottomTerrain);
                }
            }

            Close();
        }

        private string GetTerrainName(int x, int y) {
            return string.Format("Terrain {0}x{1}", x, y);
        }

        private bool IsDirectoryValid(string directory) {
            return string.IsNullOrEmpty(directory) == false && directory.Length >= Application.dataPath.Length && directory.StartsWith(Application.dataPath) && 
                directory.StartsWith("Assets") == false && File.Exists(Application.dataPath + directory) == false;
        }

        private readonly string[] storageSpaceUnits = { "bytes", "KB", "MB", "GB", "TB", "PB" };
        private string FormatBytes(double byteCount) {
            int divisionCount = 0;
            
            while(Math.Abs(byteCount) > 1024d) {
                byteCount /= 1024d;
                divisionCount++;
            }
            
            return byteCount.ToString("0.## ") + storageSpaceUnits[divisionCount];
        }
    }
}