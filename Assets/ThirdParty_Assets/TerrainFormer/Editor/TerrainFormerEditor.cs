using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/**
* IMPORTANT NOTE:
* Unity's terrain data co-ordinates are not setup as you might expect.
* Assuming the terrain is not rotated, this is the terrain strides vs axis:
* [0, 0]            = -X, -Z
* [width, 0]        = +X, -Z
* [0, height]       = -X, +Z
* [width, height]   = +X, +Z
* 
* This means that the that X goes out to the 3D Z-Axis, and Y goes out into the 3D X-Axis.
* This also means that a world space position such as the mouse position from a raycast needs 
*   its worldspace X-Axis position mapped to Z, and the worldspace Y-Axis mapped to X
*   
* Another thing of note is that LoadAssetAtPath usages aren't using the generic method - this is simply
* to support older Unity 5.0 releases.
*/

namespace JesseStiller.TerrainFormerExtension {
    [CustomEditor(typeof(TerrainFormer))]
    internal class TerrainFormerEditor : Editor {
        internal static TerrainFormerEditor Instance;
        private TerrainFormer terrainFormer;

        internal static readonly Dictionary<string, Type> terrainBrushTypes;
        internal List<TerrainBrush> terrainBrushesOfCurrentType = new List<TerrainBrush>();

        internal static float[,] allTerrainHeights, allUnmodifiedTerrainHeights;
        internal static float[,,] allTextureSamples;
        internal static SplatPrototype[] splatPrototypes;
        internal int totalHeightmapSamplesHorizontally, totalHeightmapSamplesVertically;
        internal int totalToolSamplesHorizontally, totalToolSamplesVertically;

        private TerrainMismatchManager terrainMismatchManager;

        /**
        * Caching the terrain brush is especially useful for RotateTemporaryBrushSamples. It would take >500ms when accessing the terrain brush
        * through the property. Using it in when it's been cached makes roughly a 10x speedup and doesn't allocated ~3 MB of garbage.
        */
        private TerrainBrush cachedTerrainBrush;
        private float[,] temporarySamples;

        // Reflection fields
        private object unityTerrainInspector;
        private Type unityTerrainInspectorType;
        private PropertyInfo unityTerrainSelectedTool;
        private static PropertyInfo guiUtilityTextFieldInput;

#if UNITY_5_0 || UNITY_5_1_0 || UNITY_5_1_1
        private static MethodInfo setHeightsDelayedLODMethod;
        private static MethodInfo applyDelayedHeightmapModificationMethod;
#endif
#if UNITY_5_0_0 || UNITY_5_0_1
        internal static PropertyInfo terrainDataAlphamapTexturesPropertyInfo;
#endif
        private static MethodInfo terrainDataSetBasemapDirtyMethodInfo;

        private static string mainDirectory;
        internal static string texturesDirectory;

        // The first terrain in the terrain grid or the sole terrain that's selected
        private Terrain firstTerrain;
        private TerrainData firstTerrainData;

        private bool isTerrainGridParentSelected = false;

        // Parameters shared across all terrains
        private int heightmapResolution;
        private int alphamapResolution;
        private int currentToolsResolution;
        private Vector3 terrainSize;

        internal static Settings settings;
        internal bool attemptedToInitilizeSettings = false;
        internal static bool exceptionUponLoadingSettings = false;

        private bool neighboursFoldout = false;

        // Flatten fields
        private float flattenHeight = -1f;

        // Heightfield fields
        private Texture2D heightmapTexture;

        // States and Information
        private int lastHeightmapResolultion;
        internal bool isSelectingBrush = false;
        private bool falloffChangeQueued = false;
        private double lastTimeBrushSamplesWereUpdated = -1d;

        [Flags]
        private enum SamplesDirty {
            None = 0,
            InspectorTexture = 1,
            ProjectorTexture = 2,
            BrushSamples = 4,
        }
        private SamplesDirty samplesDirty = SamplesDirty.None;

        private float halfBrushSizeInSamples;
        private int brushSizeInSamples;
        private int BrushSizeInSamples {
            get {
                return brushSizeInSamples;
            }
            set {
                if(brushSizeInSamples == value) return;
                brushSizeInSamples = value;
                halfBrushSizeInSamples = brushSizeInSamples * 0.5f;
            }
        }

        // Gizmos
        private GameObject gridPlane;
        private Material gridPlaneMaterial;

        // Projector and cursor fields
        private GameObject brushProjectorGameObject;
        private Projector brushProjector;
        private Material brushProjectorMaterial;
        private GameObject topPlaneGameObject;
        private Material topPlaneMaterial;

        internal static Texture2D brushProjectorTexture;

        // Brush fields
        private const float minBrushSpeed = 0.02f;
        private const float maxBrushSpeed = 2f;
        private const float minSpacingBounds = 0.1f;
        private const float maxSpacingBounds = 30f;
        private const float minRandomOffset = 0.001f;
        private const float minRandomRotationBounds = -180f;
        private const float maxRandomRotationBounds = 180f;
        private BrushCollection brushCollection;
        private readonly static GUIContent[] brushSizeIncrementLabels = new GUIContent[] { new GUIContent("0.05%"), new GUIContent("0.1%"), new GUIContent("0.2%"), new GUIContent("0.5%") };
        private readonly static float[] brushSizeIncrementValues = new float[] { 0.0005f, 0.001f, 0.002f, 0.005f };

        // Terrain fields
        private static readonly int terrainEditorHash = "TerrainFormerEditor".GetHashCode(); // Used for the TerrainEditor windows' events

        // The first mode in order from left to right that is not a scultping tool.
        private const Tool firstNonSculptiveTool = Tool.Heightmap;

        // Mode fields
        private static GUIContent[] modeIcons;
        private static readonly GUIContent[] modeNames = new GUIContent[] {
            new GUIContent("Raise/Lower", null, null),
            new GUIContent("Smooth", null, null),
            new GUIContent("Set Height", null, null),
            new GUIContent("Flatten", null, null),
            new GUIContent("Paint Texture", null, null),
            new GUIContent("Heightmap", null, null),
            new GUIContent("Generate", null, null),
            new GUIContent("Settings", null, null)
        };

        // Mouse related fields
        private bool mouseIsDown;
        private Vector2 mousePosition = new Vector2(); // The current screen-space position of the mouse. This position is used for raycasting
        private Vector2 lastMousePosition;
        private Vector3 lastWorldspaceMousePosition;
        private Vector3 lastClickPosition; // The point of the terrain the mouse clicked on
        private float mouseSpacingDistance = 0f;
        private float randomSpacing;
        private float currentTotalMouseDelta = 0f;
        internal float CurrentTotalMouseDelta {
            get {
                return currentTotalMouseDelta;
            }
        }

        // Styles
        private GUIStyle largeBoldLabel;
        private GUIStyle showBehaviourFoldoutStyle;
        private GUIStyle sceneViewInformationAreaStyle;
        private GUIStyle brushNameAlwaysShowBrushSelectionStyle;
        private GUIStyle gridListStyle;
        private GUIStyle miniBoldLabelCentered;
        private GUIStyle miniButtonWithoutMargin;
        private GUIStyle neighboursCellBoxStyle;

        // GUI Contents
        private static readonly GUIContent smoothAllTerrainContent = new GUIContent("Smooth All", "Smooths the entirety of the terrain based on the smoothing settings.");
        private static readonly GUIContent boxFilterSizeContent = new GUIContent("Smooth Radius", "Sets the number of adjacent terrain segments that are taken into account when smoothing " +
            "each segment. A higher value will more quickly smooth the area to become almost flat, but it may slow down performance while smoothing.");
        private static readonly GUIContent smoothingIterationsContent = new GUIContent("Smooth Iterations", "Sets how many times the entire terrain will be smoothed. (This setting only " +
            "applies to the Smooth All button.)");
        private static readonly GUIContent flattenModeContent = new GUIContent("Flatten Mode", "Sets the mode of flattening that will be used.\n- Flatten: Terrain higher than the current " +
            "click location height will be set to the click location height.\n- Bridge: The entire terrain will be set to the click location height.\n- Extend: Terrain lower than the current " +
            "click location height wil be set to the click location height.");
        private static readonly GUIContent showSculptingGridPlaneContent = new GUIContent("Show Sculpting Plane Grid", "Sets whether or not a grid plane will be visible while sculpting.");
        private static readonly GUIContent raycastModeLabelContent = new GUIContent("Sculpt Onto", "Sets the way terrain will be sculpted.\n- Plane: Sculpting will be projected onto a plane " +
            "that's located where you initially left-clicked at.\n- Terrain: Sculpting will be projected onto the terrain.");

        private static readonly GUIContent brushSizeIncrementContent = new GUIContent("Brush Size Increment", "Sets the percent of the terrain size that will be added/subtracted from the " +
            "brush size while using the brush size increment/decrement shortcuts. (Eg, a value of 2% with a terrain size of 512 will increment 10.54 [2% of 512].)");
        private static readonly GUIContent alwaysUpdateTerrainLODsContent = new GUIContent("Always Update Terrain LOD", "Sets whether or not the terrain's level-of-details (LOD) will be updated " +
            "every time the terrain is updated. This is especially useful when your computer is heavily GPU-bound or when painting across large amounts of terrain.");
        private static readonly GUIContent alwaysShowBrushSelectionContent = new GUIContent("Always Show Brush Selection", "Sets whether or not the brush selection control will be expanded " +
            "in the general brush settings area.");
        private static readonly GUIContent[] heightmapSources = new GUIContent[] { new GUIContent("Greyscale"), new GUIContent("Alpha") };
        private static readonly GUIContent collectDetailPatchesContent = new GUIContent("Collect Detail Patches", "If enabled the detail patches in the Terrain will be removed from memory when not visible. If the property is set to false, the patches are kept in memory until the Terrain object is destroyed or the collectDetailPatches property is set to true.\n\nBy setting the property to false all the detail patches for a given density will be initialized and kept in memory. Changing the density will recreate the patches.");

        private static readonly string[] brushSelectionDisplayTypeLabels = { "Image Only", "Image with Type Icon", "Tabbed" };
        private static readonly string[] raycastModes = { "Plane", "Terrain" };
        private static readonly string[] previewSizesContent = new string[] { "32px", "48px", "64px" };
        private static readonly int[] previewSizeValues = new int[] { 32, 48, 64 };

        internal List<TerrainInformation> terrainInformations;
        internal int numberOfTerrainsHorizontally = 1;
        internal int numberOfTerrainsVertically = 1;
        private Transform bottomLeftMostTerrainTransform;

        private TerrainCommand currentCommand;
        
        private static SavedInt currentTool;
        internal Tool CurrentTool {
            get {
                if(Tools.current == UnityEditor.Tool.None) {
                    return (Tool)currentTool.Value;
                } else {
                    return Tool.None;
                }
            }
            private set {
                if(value == CurrentTool) return;
                
                if(value != Tool.None) Tools.current = UnityEditor.Tool.None;

                // If the built-in Unity tools were active, make them inactive by setting their mode to None (-1)
                if(unityTerrainInspector != null && (int)unityTerrainSelectedTool.GetValue(unityTerrainInspector, null) != -1) {
                    unityTerrainSelectedTool.SetValue(unityTerrainInspector, -1, null);

                    // Update the heights of the terrain editor in case they were edited in the Unity terrain editor
                    UpdateAllHeightsFromSourceAssets();
                }

                currentTool.Value = (int)value;
            }
        }

        // Cached for less typing, some minor speed improvement and less memory allocations
        internal ModeSettings currentToolSettings;
        
        internal float[,] BrushSamplesWithSpeed {
            get {
                return brushCollection.brushes[currentToolSettings.SelectedBrushId].samplesWithSpeed;
            }
        }

        private TerrainBrush CurrentBrush {
            get {
                if(currentToolSettings == null) return null;
                if(brushCollection.brushes.ContainsKey(currentToolSettings.SelectedBrushId) == false) {
                    currentToolSettings.SelectedBrushId = brushCollection.brushes.Keys.First();
                }

                return brushCollection.brushes[currentToolSettings.SelectedBrushId];
            }
        }

        private float MaxBrushSize {
            get {
                return terrainSize.x;
            }
        }

        // The minimum brush size is set to the total length of five heightmap segments (with one segment being the length from one sample to its neighbour)
        private float MinBrushSize {
            get {
                return (terrainSize.x / heightmapResolution) * 5f;
            }
        }

        // Only show the topPlane if the height is more than 1/200th of the heightmap scale
        public float MinHeightDifferenceToShowTopPlane {
            get {
                return firstTerrainData.heightmapScale.y * 0.005f;
            }
        }

        internal AnimationCurve BrushFalloff {
            get {
                return currentToolSettings.brushFalloff;
            }
            set {
                currentToolSettings.brushFalloff = value;
            }
        }
        
        private class TerrainBrushTypesInfo {
            internal int sortOrder;
            internal string prettyTypeName;
            internal Type type;

            internal TerrainBrushTypesInfo(int sortOrder, string prettyTypeName, Type type) {
                this.sortOrder = sortOrder;
                this.prettyTypeName = prettyTypeName;
                this.type = type;
            }
        }

        static TerrainFormerEditor() {
            terrainBrushTypes = new Dictionary<string, Type>();
            terrainBrushTypes.Add("All", null);

            List<TerrainBrushTypesInfo> terrainBrushTypesInfo = new List<TerrainBrushTypesInfo>();

            Type[] allAssemblyTypes = typeof(TerrainFormerEditor).Assembly.GetTypes();
            // Gather all classes that derrive from TerrainBrush
            foreach(Type type in allAssemblyTypes) {
                if(type.IsSubclassOf(typeof(TerrainBrush)) == false) continue;

                BindingFlags nonPublicStaticBindingFlags = BindingFlags.NonPublic | BindingFlags.Static;
                FieldInfo typeSortOrderFieldInfo = type.GetField("typeSortOrder", nonPublicStaticBindingFlags);
                int typeSortOrder = typeSortOrderFieldInfo == null ? 10 : (int)typeSortOrderFieldInfo.GetValue(null);

                FieldInfo prettyTypeNameFieldInfo = type.GetField("prettyTypeName", nonPublicStaticBindingFlags);
                string prettyTypeName = prettyTypeNameFieldInfo == null ? type.Name : (string)prettyTypeNameFieldInfo.GetValue(null);

                terrainBrushTypesInfo.Add(new TerrainBrushTypesInfo(typeSortOrder, prettyTypeName, type));
            }

            terrainBrushTypesInfo.Sort(delegate (TerrainBrushTypesInfo x, TerrainBrushTypesInfo y) {
                if(x.sortOrder < y.sortOrder) return x.sortOrder;
                else return y.sortOrder;
            });

            foreach(TerrainBrushTypesInfo t in terrainBrushTypesInfo) {
                terrainBrushTypes.Add(t.prettyTypeName, t.type);
            }
            
#if UNITY_5_0 || UNITY_5_1_0 || UNITY_5_1_1
            BindingFlags internalInstanceBindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            setHeightsDelayedLODMethod = typeof(TerrainData).GetMethod("SetHeightsDelayedLOD", internalInstanceBindingFlags);
            applyDelayedHeightmapModificationMethod = typeof(Terrain).GetMethod("ApplyDelayedHeightmapModification", internalInstanceBindingFlags);
#endif
        }
        
        // Simple initialization logic that doesn't rely on any secondary data
        internal void OnEnable() {
            Instance = this;
            terrainFormer = (TerrainFormer)target;
            currentTool = new SavedInt("TerrainFormer/CurrentMode", -1);
            // If there is a Unity tool selected, make sure Terrain Former's mode is set to None
            if(Tools.current != UnityEditor.Tool.None) {
                currentTool.Value = (int)Tool.None;
            }
            currentTool.ValueChanged = CurrentToolChanged;

            // Forcibly re-initialize just in case variables were lost during an assembly reload
            Initialize(true);

            modeIcons = new GUIContent[] {
                new GUIContent(null, (Texture2D)AssetDatabase.LoadAssetAtPath(mainDirectory + "Textures/Icons/RaiseLower.png", typeof(Texture2D)), "Raise/Lower"),
                new GUIContent(null, (Texture2D)AssetDatabase.LoadAssetAtPath(mainDirectory + "Textures/Icons/Smooth.png", typeof(Texture2D)), "Smooth"),
                new GUIContent(null, (Texture2D)AssetDatabase.LoadAssetAtPath(mainDirectory + "Textures/Icons/SetHeight.png", typeof(Texture2D)), "Set Height"),
                new GUIContent(null, (Texture2D)AssetDatabase.LoadAssetAtPath(mainDirectory + "Textures/Icons/Flatten.png", typeof(Texture2D)), "Flatten"),
                new GUIContent(null, (Texture2D)AssetDatabase.LoadAssetAtPath(mainDirectory + "Textures/Icons/PaintTexture.psd", typeof(Texture2D)), "Paint Texture"),
                new GUIContent(null, (Texture2D)AssetDatabase.LoadAssetAtPath(mainDirectory + "Textures/Icons/Heightmap.psd", typeof(Texture2D)), "Heightmap"),
                new GUIContent(null, (Texture2D)AssetDatabase.LoadAssetAtPath(mainDirectory + "Textures/Icons/Generate.png", typeof(Texture2D)), "Generate"),
                new GUIContent(null, (Texture2D)AssetDatabase.LoadAssetAtPath(mainDirectory + "Textures/Icons/Settings.png", typeof(Texture2D)), "Settings")
            };

            ModeSettings.UseFalloffForCustomBrushesChanged = UpdatePreviewTexturesAndBrushSamples;
            ModeSettings.UseAlphaFalloffChanged = UpdatePreviewTexturesAndBrushSamples;
            ModeSettings.BrushSpeedChanged = BrushSpeedChanged;
            ModeSettings.BrushRoundnessChanged = BrushRoundnessChanged;
            ModeSettings.BrushAngleDeltaChanged = BrushAngleDeltaChanged;
            ModeSettings.SelectedBrushChanged = SelectedBrushChanged;
            ModeSettings.RandomOffsetChanged = RandomOffsetChanged;
            ModeSettings.RandomRotationChanged = RandomRotationChanged;
            ModeSettings.RandomSpacingChanged = RandomSpacingChanged;
            ModeSettings.InvertBrushTextureChanged = InvertBrushTextureChanged;
            ModeSettings.BrushSizeChanged = BrushSizeChanged;
            ModeSettings.SelectedBrushTabChanged = SelectedBrushTabChanged;
            
            Undo.undoRedoPerformed += UndoRedoPerformed;
            
            // Set the Terrain Former component icon
            Type editorGUIUtilityType = typeof(EditorGUIUtility);
            MethodInfo setIcon = editorGUIUtilityType.GetMethod("SetIconForObject", BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic, null, new Type[]{typeof(UnityEngine.Object), typeof(Texture2D)}, null);
            Texture2D icon = (Texture2D)AssetDatabase.LoadAssetAtPath(texturesDirectory + "Icons/Icon.png", typeof(Texture2D));
            setIcon.Invoke(null, new object[] { target, icon});
        }
        
        /**
        * Initialize contains logic that is intrinsically tied to this entire terrain tool. If any of these fields and 
        * other things are missing, then the entire editor will break. An attempt will be made every GUI frame to find them.
        * Returns true if the initialization was successful or if everything is already initialized, false otherwise.
        * If the user moves Terrain Former's Editor folder away and brings it back, the brushProjector dissapears. This is why
        * it is checked for on Initialization.
        */
        private bool Initialize(bool forceReinitialize = false) {
            if(forceReinitialize == false && terrainFormer != null && brushProjector != null) {
                return true;
            }
            
            /**
            * If there is more than one object selected, do not even bother initializing. This also fixes a strange 
            * exception occurance when two terrains or more are selected; one with Terrain Former and one without
            */
            if(Selection.objects.Length != 1) return false;

            // Make sure there is only ever one Terrain Former
            TerrainFormer[] terrainFormerInstances = terrainFormer.GetComponents<TerrainFormer>();
            if(terrainFormerInstances.Length > 1) {
                for(int i = terrainFormerInstances.Length - 1; i > 0; i--) {
                    DestroyImmediate(terrainFormerInstances[i]);
                }
                EditorUtility.DisplayDialog("Terrain Former", "You can't add multiple Terrain Former components to a single Terrain object.", "Close");
                return false;
            }
            
            Terrain terrainComponentOfTarget = terrainFormer.GetComponent<Terrain>();
            List<Terrain> allTerrains = new List<Terrain>();
            if(terrainComponentOfTarget) {
                firstTerrain = terrainComponentOfTarget;

                // If there is a terrain component attached to this object, check if it's one of many terrains inside of a grid.
                if(terrainFormer.transform.parent != null && terrainFormer.transform.childCount > 0) {
                    allTerrains.AddRange(terrainFormer.transform.parent.GetComponentsInChildren<Terrain>());
                } else {
                    allTerrains.Add(firstTerrain);
                }
            } else {
                // If Terrain Former is attached to a game object with children that contain Terrains, allow Terrain Former to look into the child terrain objects.
                Terrain[] terrainChildren = terrainFormer.GetComponentsInChildren<Terrain>();
                if(terrainChildren != null && terrainChildren.Length > 0) {
                    isTerrainGridParentSelected = true;
                    firstTerrain = terrainChildren[0];
                } else {
                    return false;
                }

                allTerrains.AddRange(terrainChildren);
            }
            
            if(firstTerrain == null) return false;

            firstTerrainData = firstTerrain.terrainData;
            if(firstTerrainData == null) return false;
            
            terrainSize = firstTerrainData.size;
            heightmapResolution = firstTerrainData.heightmapResolution;
            alphamapResolution = firstTerrainData.alphamapResolution;
            lastHeightmapResolultion = heightmapResolution;

            terrainInformations = new List<TerrainInformation>();

            foreach(Terrain terrain in allTerrains) terrainInformations.Add(new TerrainInformation(terrain));

            // If there is more than one terrain, find the top-left most terrain to determine grid coordinates
            if(terrainInformations.Count > 1) {
                Vector3 bottomLeftMostValue = new Vector3(float.MaxValue, 0f, float.MaxValue);
                Vector3 currentTerrainPosition;
                foreach(TerrainInformation ti in terrainInformations) {
                    currentTerrainPosition = ti.terrain.GetPosition();
                    if(currentTerrainPosition.x <= bottomLeftMostValue.x && currentTerrainPosition.z <= bottomLeftMostValue.z) {
                        bottomLeftMostValue = currentTerrainPosition;
                        bottomLeftMostTerrainTransform = ti.terrain.transform;
                    }
                }
                foreach(TerrainInformation terrainInformation in terrainInformations) {
                    terrainInformation.gridXCoordinate = Mathf.RoundToInt((terrainInformation.transform.position.x - bottomLeftMostValue.x) / terrainSize.x);
                    terrainInformation.gridYCoordinate = Mathf.RoundToInt((terrainInformation.transform.position.z - bottomLeftMostValue.z) / terrainSize.z);
                    terrainInformation.alphamapsXOffset = terrainInformation.gridXCoordinate * alphamapResolution;
                    terrainInformation.alphamapsYOffset = terrainInformation.gridYCoordinate * alphamapResolution;
                    terrainInformation.heightmapXOffset = terrainInformation.gridXCoordinate * heightmapResolution - terrainInformation.gridXCoordinate;
                    terrainInformation.heightmapYOffset = terrainInformation.gridYCoordinate * heightmapResolution - terrainInformation.gridYCoordinate;

                    if(terrainInformation.gridXCoordinate + 1 > numberOfTerrainsHorizontally) {
                        numberOfTerrainsHorizontally = terrainInformation.gridXCoordinate + 1;
                    } else if(terrainInformation.gridYCoordinate + 1 > numberOfTerrainsVertically) {
                        numberOfTerrainsVertically = terrainInformation.gridYCoordinate + 1;
                    }
                }
            } else {
                bottomLeftMostTerrainTransform = firstTerrain.transform;
            }
            
            totalHeightmapSamplesHorizontally = numberOfTerrainsHorizontally * heightmapResolution - (numberOfTerrainsHorizontally - 1);
            totalHeightmapSamplesVertically = numberOfTerrainsVertically * heightmapResolution - (numberOfTerrainsVertically - 1);
            
            if(terrainMismatchManager == null) {
                terrainMismatchManager = new TerrainMismatchManager();
            }
            terrainMismatchManager.Initialize(terrainInformations);
            if(terrainMismatchManager.IsMismatched) return false;

            splatPrototypes = firstTerrainData.splatPrototypes;

            allTerrainHeights = new float[totalHeightmapSamplesVertically, totalHeightmapSamplesHorizontally];
            UpdateAllHeightsFromSourceAssets();
            
            if(settings == null) {
                InitializeSettings();
            }

            if(settings == null) return false;

            settings.mainDirectory = mainDirectory;
            settings.AlwaysShowBrushSelectionChanged += AlwaysShowBrushSelectionValueChanged;
            settings.brushColour.ValueChanged += BrushColourChanged;
            
            brushCollection = new BrushCollection();

            CreateProjector();

            CreateGridPlane();

            /**
            * On startup, the current mode is assigned to a new value but the ValueChanged event is not fired since other parts have not
            * been initialized (ie; the projector). After everything has been initialized call CurrentToolChanged to update parameters such
            * as brush size pixels and update the brush textures.
            */
            CurrentToolChanged();
            
            /**
            * Get an instance of the built-in Unity Terrain Inspector so we can override the selectedTool property
            * when the user selects a different tool in Terrain Former. This makes it so the user can't accidentally
            * use two terain tools at once (eg. Unity Terrain's raise/lower, and Terrain Former's raise/lower)
            */
            unityTerrainInspectorType = Assembly.GetAssembly(typeof(Editor)).GetType("UnityEditor.TerrainInspector");
            unityTerrainSelectedTool = unityTerrainInspectorType.GetProperty("selectedTool", BindingFlags.NonPublic | BindingFlags.Instance);
            
            UnityEngine.Object[] terrainInspectors = Resources.FindObjectsOfTypeAll(unityTerrainInspectorType);
            // Iterate through each Unity terrain inspector to find the Terrain Inspector that belongs to this object
            foreach(UnityEngine.Object inspector in terrainInspectors) {
                Editor inspectorAsType = inspector as Editor;
                GameObject inspectorGameObject = ((Terrain)inspectorAsType.target).gameObject;

                if(inspectorGameObject == null) continue;

                if(inspectorGameObject == terrainFormer.gameObject) {
                    unityTerrainInspector = inspector;
                }
            }
            
            guiUtilityTextFieldInput = typeof(GUIUtility).GetProperty("textFieldInput", BindingFlags.NonPublic | BindingFlags.Static);

            terrainDataSetBasemapDirtyMethodInfo = typeof(TerrainData).GetMethod("SetBasemapDirty", BindingFlags.Instance | BindingFlags.NonPublic);
#if UNITY_5_0_0 || UNITY_5_0_1
            terrainDataAlphamapTexturesPropertyInfo = typeof(TerrainData).GetProperty("alphamapTextures", BindingFlags.NonPublic | BindingFlags.Instance);
#endif
            AssetWatcher.OnAssetsImported += OnAssetsImported;
            AssetWatcher.OnAssetsMoved += OnAssetsMoved;
            AssetWatcher.OnAssetsDeleted += OnAssetsDeleted;
            AssetWatcher.OnWillSaveAssetsAction += OnWillSaveAssets;
            
            return true;
        }
        
        internal static void InitializeSettings() {
            // Look for the main directory by finding the path of the Terrain Former script.
            GameObject temporaryGameObject = EditorUtility.CreateGameObjectWithHideFlags("TF", HideFlags.HideAndDontSave);
            TerrainFormer terrainFormerComponent = temporaryGameObject.AddComponent<TerrainFormer>();
            string terrainFormerPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(terrainFormerComponent));
            mainDirectory = Path.GetDirectoryName(terrainFormerPath) + "/";
            DestroyImmediate(terrainFormerComponent);
            DestroyImmediate(temporaryGameObject);
            texturesDirectory = mainDirectory + "Textures/";

            if(string.IsNullOrEmpty(mainDirectory)) return;

            string settingsPath = Path.Combine(Application.dataPath.Remove(Application.dataPath.Length - 7, 7), Path.Combine(mainDirectory, "Settings.tf"));
            settings = Settings.Create(settingsPath);
        }

        void OnDisable() {
            if(settings != null) settings.Save();

            brushCollection = null;

            // Destroy all gizmos
            if(brushProjectorGameObject != null) {
                DestroyImmediate(brushProjectorMaterial);
                DestroyImmediate(brushProjectorGameObject);
                DestroyImmediate(topPlaneGameObject);
                brushProjector = null;
            }

            if(gridPlane != null) {
                DestroyImmediate(gridPlaneMaterial);
                DestroyImmediate(gridPlane.gameObject);
                gridPlaneMaterial = null;
                gridPlane = null;
            }
            
            UnsubscribeFromUnityEvents();

            currentTool.ValueChanged = null;

            ModeSettings.UseFalloffForCustomBrushesChanged = null;
            ModeSettings.UseAlphaFalloffChanged = null;
            ModeSettings.BrushSpeedChanged = null;
            ModeSettings.BrushRoundnessChanged = null;
            ModeSettings.BrushAngleDeltaChanged = null;
            ModeSettings.SelectedBrushChanged = null;
            ModeSettings.RandomOffsetChanged = null;
            ModeSettings.RandomRotationChanged = null;
            ModeSettings.RandomSpacingChanged = null;
            ModeSettings.BrushSizeChanged = null;
            ModeSettings.InvertBrushTextureChanged = null;
            ModeSettings.SelectedBrushTabChanged = null;

            if(settings != null) {
                settings.AlwaysShowBrushSelectionChanged -= AlwaysShowBrushSelectionValueChanged;
                settings.brushColour.ValueChanged -= BrushColourChanged;
            }

            sceneViewInformationAreaStyle = null;

            Instance = null;
        }

        void OnDestroy() {
            UnsubscribeFromUnityEvents();
        }

        private void UnsubscribeFromUnityEvents() {
            // TODO: This (and possibly other global events) can be called multiple times when only one Terrain Former should exist
            Undo.undoRedoPerformed -= UndoRedoPerformed;
            AssetWatcher.OnAssetsImported -= OnAssetsImported;
            AssetWatcher.OnAssetsMoved -= OnAssetsMoved;
            AssetWatcher.OnAssetsDeleted -= OnAssetsDeleted;
            AssetWatcher.OnWillSaveAssetsAction -= OnWillSaveAssets;
        }

        public override void OnInspectorGUI() {
            // TODO: Handle the complete absence of any Terrain Datas

            bool displayingError = false;

            // Stop if the initialization was unsuccessful
            // TODO: Check for these faults in all terrains and list each terrain's fault in a category view
            if(firstTerrainData == null) {
                EditorGUILayout.HelpBox("Missing terrain data asset. Reassign the terrain asset in the Unity Terrain component.", MessageType.Error);
                displayingError = true;
            }
            if(target == null) {
                EditorGUILayout.HelpBox("There is no target object. Make sure Terrain Former is a component of a terrain object.", MessageType.Error);
                displayingError = true;
            }

            if(terrainMismatchManager != null) {
                terrainMismatchManager.Draw();
            }

            if(settings == null && attemptedToInitilizeSettings) {
                if(exceptionUponLoadingSettings == true) {
                    EditorGUILayout.HelpBox("The Settings.tf file couldn't load. Look at the errors in the Console for more details.", MessageType.Error);
                } else {
                    EditorGUILayout.HelpBox("The Settings.tf file couldn't load. There must be some invalid JSON in the file, possibly caused by a merge that happened in a source control system. You can safely ignore the Settings.tf file from source control.", MessageType.Error);
                }

                displayingError = true;
            }
            
            if(displayingError) return;
            
            if(Initialize() == false) return;

            if(largeBoldLabel == null) {
                largeBoldLabel = new GUIStyle(EditorStyles.largeLabel);
                largeBoldLabel.fontSize = 13;
                largeBoldLabel.fontStyle = FontStyle.Bold;
                largeBoldLabel.alignment = TextAnchor.MiddleCenter;
            }
            if(showBehaviourFoldoutStyle == null) {
                showBehaviourFoldoutStyle = new GUIStyle(EditorStyles.boldLabel);
                showBehaviourFoldoutStyle.padding = new RectOffset(2, 2, 1, 2);
                showBehaviourFoldoutStyle.margin = new RectOffset(4, 4, 0, 0);
                showBehaviourFoldoutStyle.fontStyle = FontStyle.Bold;
            }
            if(brushNameAlwaysShowBrushSelectionStyle == null) {
                brushNameAlwaysShowBrushSelectionStyle = new GUIStyle(GUI.skin.label);
                brushNameAlwaysShowBrushSelectionStyle.alignment = TextAnchor.MiddleRight;
            }
            if(gridListStyle == null) {
                gridListStyle = GUI.skin.GetStyle("GridList");
            }
            if(miniBoldLabelCentered == null) {
                miniBoldLabelCentered = new GUIStyle(EditorStyles.miniBoldLabel);
                miniBoldLabelCentered.alignment = TextAnchor.MiddleCenter;
                miniBoldLabelCentered.margin = new RectOffset();
                miniBoldLabelCentered.padding = new RectOffset();
                miniBoldLabelCentered.wordWrap = true;
            }
            if(miniButtonWithoutMargin == null) {
                miniButtonWithoutMargin = EditorStyles.miniButton;
                miniButtonWithoutMargin.margin = new RectOffset();
            }
            if(neighboursCellBoxStyle == null) {
                neighboursCellBoxStyle = new GUIStyle(GUI.skin.box);
                neighboursCellBoxStyle.padding = new RectOffset();
                neighboursCellBoxStyle.contentOffset = new Vector2();
                neighboursCellBoxStyle.alignment = TextAnchor.MiddleCenter;
                
                if(numberOfTerrainsHorizontally >= 10 || numberOfTerrainsVertically >= 10) {
                    neighboursCellBoxStyle.fontSize = 8;
                } else {
                    neighboursCellBoxStyle.fontSize = 10;
                }
            }

            if(currentToolSettings == null && CurrentTool != Tool.None && CurrentTool < firstNonSculptiveTool) {
                currentToolSettings = settings.modeSettings[CurrentTool];
            }

            EditorGUIUtility.labelWidth = CurrentTool == Tool.Settings ? 188f : 125f;

            CheckKeyboardShortcuts(Event.current);

            if(Event.current.type == EventType.MouseUp || Event.current.type == EventType.KeyUp) {
                UpdateDirtyBrushSamples();
            }

            // Check if the user modified the heightmap resolution. If so, update the brush samples
            int heightmapResolution = firstTerrainData.heightmapResolution;
            if(lastHeightmapResolultion != -1 && lastHeightmapResolultion != heightmapResolution) {
                BrushSizeChanged();
                lastHeightmapResolultion = heightmapResolution;
            }
            
            /** 
            * Get the current Unity Terrain Inspector mode, and set the Terrain Former mode to none if the Unity Terrain
            * Inspector mode is not none.
            */
            if(unityTerrainInspector != null && CurrentTool != Tool.None) {
                int unityTerrainMode = (int)unityTerrainSelectedTool.GetValue(unityTerrainInspector, null);
                // If the mode is not "None" (-1), then the Terrain Former mode must be set to none
                if(unityTerrainMode != -1) {
                    currentTool.Value = (int)Tool.None;
                }
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Space(-8f); // HACK: This offset is required (for some reason) to make the toolbar horizontally centered

            Rect modeToolbarRect = EditorGUILayout.GetControlRect(false, 22f, GUILayout.MinWidth(250f), GUILayout.MaxWidth(280f));
            Tool newMode = (Tool)GUI.Toolbar(modeToolbarRect, (int)CurrentTool, modeIcons);
            Event currentEvent = Event.current;
            if(currentEvent.type == EventType.Repaint && modeToolbarRect.Contains(currentEvent.mousePosition)) {
                float mouseHorizontalDelta = currentEvent.mousePosition.x - modeToolbarRect.x;
                float tabWidth = modeToolbarRect.width / modeNames.Length;
                int modeIndex = Mathf.FloorToInt((mouseHorizontalDelta / modeToolbarRect.width) * modeNames.Length);
                float centerOfTabHoveredOver = modeIndex * tabWidth + tabWidth * 0.5f + modeToolbarRect.x;

                Vector2 tooltipBoxSize = GUI.skin.box.CalcSize(modeNames[modeIndex]);

                GUI.Box(new Rect(centerOfTabHoveredOver - tooltipBoxSize.x * 0.5f, modeToolbarRect.y - 20f, tooltipBoxSize.x + 6f, tooltipBoxSize.y), modeNames[modeIndex].text);
            }
            if(newMode != CurrentTool) {
                CurrentTool = newMode;
                SceneView.RepaintAll();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if(CurrentTool == Tool.None) {
                return;
            } else {
                GUILayout.Label(modeNames[(int)CurrentTool], largeBoldLabel);
            }

            switch(CurrentTool) {
                case Tool.Smooth:
                    settings.boxFilterSize = EditorGUILayout.IntSlider(boxFilterSizeContent, settings.boxFilterSize, 1, 5);

                    GUILayout.Label("Smooth All", EditorStyles.boldLabel);

                    GUIUtilities.FillAndRightControl(
                        fillControl: (r) => {
                            settings.smoothingIterations = EditorGUI.IntSlider(r, smoothingIterationsContent, settings.smoothingIterations, 1, 10);
                        },
                        rightControl: (r) => {
                            r.yMin -= 2f;
                            r.yMax += 2f;
                            if(GUI.Button(r, smoothAllTerrainContent)) {
                                SmoothAll();
                            }
                        },
                        rightControlWidth: 100
                    );
                    break;
                case Tool.SetHeight:
                    GUIUtilities.FillAndRightControl(
                        fillControl: (r) => {
                            settings.setHeight = EditorGUI.Slider(r, "Set Height", settings.setHeight, 0f, terrainSize.y);
                        },
                        rightControl: (r) => {
                            r.yMax += 2;
                            r.yMin -= 2;
                            if(GUI.Button(r, "Apply to Terrain")) {
                                FlattenTerrain(settings.setHeight / terrainSize.y);
                            }
                        },
                        rightControlWidth: 111
                    );

                    break;
                case Tool.Flatten:
                    settings.flattenMode = (FlattenMode)EditorGUILayout.EnumPopup(flattenModeContent, settings.flattenMode);
                    break;
                case Tool.PaintTexture:
                    EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);

                    Texture2D[] splatIcons = new Texture2D[splatPrototypes.Length];
                    for(int i = 0; i < splatIcons.Length; ++i) {
                        splatIcons[i] = AssetPreview.GetAssetPreview(splatPrototypes[i].texture) ?? splatPrototypes[i].texture;
                    }

                    settings.selectedTextureIndex = GUIUtilities.TextureSelectionGrid(settings.selectedTextureIndex, splatIcons);

                    settings.targetOpacity = EditorGUILayout.Slider("Target Opacity", settings.targetOpacity, 0f, 1f);
                    break;
                case Tool.Heightmap:

                    Rect raycastModeRect = EditorGUILayout.GetControlRect();
                    EditorGUI.PrefixLabel(raycastModeRect, raycastModeLabelContent);

                    Rect heightmapSourceRect = EditorGUILayout.GetControlRect();
                    Rect heightmapSourceToolbarRect = EditorGUI.PrefixLabel(heightmapSourceRect, new GUIContent("Source"));
                    settings.heightmapSourceIsAlpha = GUI.Toolbar(heightmapSourceToolbarRect, settings.heightmapSourceIsAlpha ? 1 : 0, 
                        heightmapSources, EditorStyles.radioButton) == 1 ? true : false;
                    heightmapTexture = (Texture2D)EditorGUILayout.ObjectField("Heightmap Texture", heightmapTexture, typeof(Texture2D), false);

                    GUI.enabled = heightmapTexture != null;
                    Rect importHeightmapButtonRect = EditorGUILayout.GetControlRect(GUILayout.Width(140f), GUILayout.Height(20f));
                    importHeightmapButtonRect.x = Screen.width * 0.5f - 70f;
                    if(GUI.Button(importHeightmapButtonRect, "Import Heightmap")) {
                        ImportHeightmap();
                    }
                    GUI.enabled = true;
                    break;
                case Tool.Generate:
                    AnimationCurve newCurve = EditorGUILayout.CurveField("Falloff", new AnimationCurve(settings.generateRampCurve.keys));
                    if(AreAnimationCurvesEqual(newCurve, settings.generateRampCurve) == false) {
                        settings.generateRampCurve = newCurve;
                        ClampAnimationCurve(settings.generateRampCurve);
                    }

                    settings.generateHeight = EditorGUILayout.Slider("Height", settings.generateHeight, 0f, terrainSize.y);

                    using(new EditorGUILayout.HorizontalScope()) {
                        GUILayout.Space(Screen.width * 0.1f);
                        if(GUILayout.Button("Create Linear Ramp", GUILayout.Height(20f))) {
                            CreateRampCurve(settings.generateHeight);
                        }
                        if(GUILayout.Button("Create Circular Ramp", GUILayout.Height(20f))) {
                            CreateCircularRampCurve(settings.generateHeight);
                        }
                        GUILayout.Space(Screen.width * 0.1f);
                    }

                    break;
                case Tool.Settings:
                    Rect goToPreferencesButtonRect = EditorGUILayout.GetControlRect(false, 22f);
                    goToPreferencesButtonRect.xMin = goToPreferencesButtonRect.xMax - 175f;
                    if(GUI.Button(goToPreferencesButtonRect, "Terrain Former Preferences")) {
                        Type preferencesWindowType = Assembly.GetAssembly(typeof(Editor)).GetType("UnityEditor.PreferencesWindow");
                        MethodInfo showPreferencesWindowMethodInfo = preferencesWindowType.GetMethod("ShowPreferencesWindow", BindingFlags.NonPublic | BindingFlags.Static);
                        FieldInfo selectedSectionIndexFieldInfo = preferencesWindowType.GetField("m_SelectedSectionIndex", BindingFlags.NonPublic | BindingFlags.Instance);

                        FieldInfo sectionsFieldInfo = preferencesWindowType.GetField("m_Sections", BindingFlags.NonPublic | BindingFlags.Instance);

                        Type sectionType = preferencesWindowType.GetNestedType("Section", BindingFlags.NonPublic);

                        showPreferencesWindowMethodInfo.Invoke(null, null);
                        EditorWindow preferencesWindow = EditorWindow.GetWindowWithRect(preferencesWindowType, new Rect(100f, 100f, 500f, 400f), true, "Unity Preferences");

                        // Call PreferencesWindow.OnGUI method force it to add the custom sections so we have access to all sections.
                        MethodInfo preferencesWindowOnGUIMethodInfo = preferencesWindowType.GetMethod("OnGUI", BindingFlags.NonPublic | BindingFlags.Instance);
                        preferencesWindowOnGUIMethodInfo.Invoke(preferencesWindow, null);

                        IList sections = (IList)sectionsFieldInfo.GetValue(preferencesWindow);
                        for(int i = 0; i < sections.Count; i++) {
                            GUIContent sectionsContent = (GUIContent)sectionType.GetField("content").GetValue(sections[i]);
                            string sectionText = sectionsContent.text;
                            if(sectionText == "Terrain Former") {
                                selectedSectionIndexFieldInfo.SetValue(preferencesWindow, i);
                                break;
                            }
                        }
                    }

                    EditorGUILayout.LabelField("Size", EditorStyles.boldLabel);

                    float newTerrainLateralSize = Mathf.Max(GUIUtilities.DelayedFloatField("Terrain Width/Length", firstTerrainData.size.x), 0f);
                    float newTerrainHeight = Mathf.Max(GUIUtilities.DelayedFloatField("Terrain Height", firstTerrainData.size.y), 0f);

                    bool terrainSizeChangedLaterally = newTerrainLateralSize != firstTerrainData.size.x;
                    if(terrainSizeChangedLaterally || newTerrainHeight != firstTerrainData.size.y) {
                        List<UnityEngine.Object> objectsThatWillBeModified = new List<UnityEngine.Object>();
                        
                        foreach(TerrainInformation ti in terrainInformations) {
                            objectsThatWillBeModified.Add(ti.terrainData);
                            if(terrainSizeChangedLaterally) objectsThatWillBeModified.Add(ti.transform);
                        }

                        // Calculate the center of the terrain grid and use that to decide where how to resposition the terrain grid cells.
                        Vector2 previousTerrainGridSize = new Vector2(numberOfTerrainsHorizontally * terrainSize.x, numberOfTerrainsVertically * terrainSize.z);
                        Vector3 centerOfTerrainGrid = new Vector3(bottomLeftMostTerrainTransform.position.x + previousTerrainGridSize.x * 0.5f, bottomLeftMostTerrainTransform.position.y,
                            bottomLeftMostTerrainTransform.position.z + previousTerrainGridSize.y * 0.5f);
                        Vector3 newTerrainGridSizeHalf = new Vector3(numberOfTerrainsHorizontally * newTerrainLateralSize * 0.5f, 0f, 
                            numberOfTerrainsVertically * newTerrainLateralSize * 0.5f);
                        
                        Undo.RegisterCompleteObjectUndo(objectsThatWillBeModified.ToArray(), terrainInformations.Count == 1 ? "Terrain Size Changed" : "Terrain Sizes Changed");

                        foreach(TerrainInformation ti in terrainInformations) {
                            // Reposition the terrain grid (if there is more than one terrain) because the terrain size has changed laterally
                            if(terrainSizeChangedLaterally) {
                                ti.transform.position = new Vector3(
                                    (centerOfTerrainGrid.x - newTerrainGridSizeHalf.x) + ti.gridXCoordinate * newTerrainLateralSize, 
                                    ti.transform.position.y,
                                    (centerOfTerrainGrid.z - newTerrainGridSizeHalf.z) + ti.gridYCoordinate * newTerrainLateralSize
                                );
                            }

                            ti.terrainData.size = new Vector3(newTerrainLateralSize, newTerrainHeight, newTerrainLateralSize);
                        }

                        terrainSize = new Vector3(newTerrainLateralSize, newTerrainHeight, newTerrainLateralSize);
                    }

                    /**
                    * The following code is highly repetitive, but it must be written in this fashion. Writing this code in a more generalized fashion
                    * requires Reflection, but unfortunately virtually all properties are attributed with "MethodImplOptions.InternalCall", which as far as I
                    * know are not possible to be invoked using Reflection. As such, these properties must be set the manual way for all of their behaviours 
                    * to be executed.
                    */

                    EditorGUI.BeginChangeCheck();

                    // Base Terrain
                    bool newDrawHeightmap = EditorGUILayout.BeginToggleGroup("Base Terrain", firstTerrain.drawHeightmap);
                    if(firstTerrain.drawHeightmap != newDrawHeightmap) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.drawHeightmap = newDrawHeightmap;
                    }

                    EditorGUI.indentLevel = 1;
                    float newHeightmapPixelError = EditorGUILayout.Slider("Pixel Error", firstTerrain.heightmapPixelError, 1f, 200f);
                    if(firstTerrain.heightmapPixelError != newHeightmapPixelError) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.heightmapPixelError = newHeightmapPixelError;
                    }
                    
                    bool newCastShadows = EditorGUILayout.Toggle("Cast Shadows", firstTerrain.castShadows);
                    if(firstTerrain.castShadows != newCastShadows) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.castShadows = newCastShadows;
                    }
                    
                    Terrain.MaterialType newMaterialType = (Terrain.MaterialType)EditorGUILayout.EnumPopup("Material Type", firstTerrain.materialType);
                    if(firstTerrain.materialType != newMaterialType) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.materialType = newMaterialType;
                    }

                    switch(newMaterialType) {
                        case Terrain.MaterialType.BuiltInLegacySpecular:
                            EditorGUI.indentLevel++;
                            Color newLegacySpecular = EditorGUILayout.ColorField("Specular Colour", firstTerrain.legacySpecular);
                            if(firstTerrain.legacySpecular != newLegacySpecular) {
                                foreach(TerrainInformation ti in terrainInformations) ti.terrain.legacySpecular = newLegacySpecular;
                            }

                            float newLegacyShininess = EditorGUILayout.Slider("Shininess", firstTerrain.legacyShininess, 0.03f, 1f);
                            if(firstTerrain.legacyShininess != newLegacyShininess) {
                                foreach(TerrainInformation ti in terrainInformations) ti.terrain.legacyShininess = newLegacyShininess;
                            }
                            EditorGUI.indentLevel--;
                            break;
                        case Terrain.MaterialType.Custom:
                            EditorGUI.indentLevel++;
                            Material newMaterialTemplate = (Material)EditorGUILayout.ObjectField("Custom Material", firstTerrain.materialTemplate, typeof(Material), false);
                            if(firstTerrain.materialTemplate != newMaterialTemplate) {
                                foreach(TerrainInformation ti in terrainInformations) ti.terrain.materialTemplate = newMaterialTemplate;
                            }

                            if(firstTerrain.materialTemplate != null && TerrainSettings.ShaderHasTangentChannel(firstTerrain.materialTemplate.shader))
                                EditorGUILayout.HelpBox("Materials with shaders that require tangent geometry shouldn't be used on terrains. Instead, use one of the shaders found under Nature/Terrain.", MessageType.Warning, true);
                            EditorGUI.indentLevel--;
                            break;
                    }
                    
                    if(newMaterialType == Terrain.MaterialType.BuiltInStandard || newMaterialType == Terrain.MaterialType.Custom) {
                        ReflectionProbeUsage newReflectionProbeUsage = (ReflectionProbeUsage)EditorGUILayout.EnumPopup("Reflection Probes", firstTerrain.reflectionProbeUsage);
                        if(firstTerrain.reflectionProbeUsage != newReflectionProbeUsage) {
                            foreach(TerrainInformation ti in terrainInformations) ti.terrain.reflectionProbeUsage = newReflectionProbeUsage;
                        }
                        // TODO: Implement an equivalent of these private methods and fields
                        //if(firstTerrain.reflectionProbeUsage != ReflectionProbeUsage.Off) {
                        //    firstTerrain.GetClosestReflectionProbes(this.m_BlendInfoList);
                        //    RendererEditorBase.Probes.ShowClosestReflectionProbes(this.m_BlendInfoList);
                        //}
                    }

                    float newThickness = EditorGUILayout.FloatField("Thickness", firstTerrainData.thickness);
                    if(firstTerrainData.thickness != newThickness) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrainData.thickness = newThickness;
                    }
                    EditorGUI.indentLevel = 0;

                    EditorGUILayout.EndToggleGroup();

                    // Tree and Detail Objects
                    bool newDrawTreesAndFoliage = EditorGUILayout.BeginToggleGroup("Tree and Detail Objects", firstTerrain.drawTreesAndFoliage);
                    if(firstTerrain.drawTreesAndFoliage != newDrawTreesAndFoliage) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.drawTreesAndFoliage = newDrawTreesAndFoliage;
                    }

                    EditorGUI.indentLevel = 1;
                    bool newBakeLightProbesForTrees = EditorGUILayout.Toggle("Bake Light Probes for Trees", firstTerrain.bakeLightProbesForTrees);
                    if(firstTerrain.bakeLightProbesForTrees != newBakeLightProbesForTrees) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.bakeLightProbesForTrees = newBakeLightProbesForTrees;
                    }

                    float newDetailObjectDistance = EditorGUILayout.Slider("Detail Distance", firstTerrain.detailObjectDistance, 0f, 250f);
                    if(firstTerrain.detailObjectDistance != newDetailObjectDistance) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.detailObjectDistance = newDetailObjectDistance;
                    }

                    bool newCollectDetailPatches = EditorGUILayout.Toggle(collectDetailPatchesContent, firstTerrain.collectDetailPatches);
                    if(firstTerrain.collectDetailPatches != newCollectDetailPatches) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.collectDetailPatches = newCollectDetailPatches;
                    }

                    float newDetailObjectDensity = EditorGUILayout.Slider("Detail Density", firstTerrain.detailObjectDensity, 0f, 1f);
                    if(firstTerrain.detailObjectDensity != newDetailObjectDensity) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.detailObjectDensity = newDetailObjectDensity;
                    }

                    float newTreeDistance = EditorGUILayout.Slider("Tree Distance", firstTerrain.treeDistance, 0f, 2000f);
                    if(firstTerrain.treeDistance != newTreeDistance) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.treeDistance = newTreeDistance;
                    }
                    
                    float newTreeBillboardDistance = EditorGUILayout.Slider("Billboard Start", firstTerrain.treeBillboardDistance, 5f, 2000f);
                    if(firstTerrain.treeBillboardDistance != newTreeBillboardDistance) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.treeBillboardDistance = newTreeBillboardDistance;
                    }

                    float newTreeCrossFadeLength = EditorGUILayout.Slider("Fade Length", firstTerrain.treeCrossFadeLength, 0f, 200f);
                    if(firstTerrain.treeCrossFadeLength != newTreeCrossFadeLength) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.treeCrossFadeLength = newTreeCrossFadeLength;
                    }

                    int newTreeMaximumFullLODCount = EditorGUILayout.IntSlider("Max. Mesh Trees", firstTerrain.treeMaximumFullLODCount, 0, 10000);
                    if(firstTerrain.treeMaximumFullLODCount != newTreeMaximumFullLODCount) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.treeMaximumFullLODCount = newTreeMaximumFullLODCount;
                    }

                    EditorGUI.indentLevel = 0;

                    EditorGUILayout.EndToggleGroup();
                    // If any tree/detail/base terrain settings have changed, redraw the scene view
                    if(EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

                    GUILayout.Label("Wind Settings for Grass", EditorStyles.boldLabel);

                    float newWavingGrassStrength = EditorGUILayout.Slider("Strength", firstTerrainData.wavingGrassStrength, 0f, 1f);
                    if(firstTerrainData.wavingGrassStrength != newWavingGrassStrength) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrainData.wavingGrassStrength = newWavingGrassStrength;
                    }

                    float newWavingGrassSpeed = EditorGUILayout.Slider("Speed", firstTerrainData.wavingGrassSpeed, 0f, 1f);
                    if(firstTerrainData.wavingGrassSpeed != newWavingGrassSpeed) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrainData.wavingGrassSpeed = newWavingGrassSpeed;
                    }

                    float newWavingGrassAmount = EditorGUILayout.Slider("Bending", firstTerrainData.wavingGrassAmount, 0f, 1f);
                    if(firstTerrainData.wavingGrassAmount != newWavingGrassAmount) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrainData.wavingGrassAmount = newWavingGrassAmount;
                    }

                    Color newWavingGrassTint = EditorGUILayout.ColorField("Tint", firstTerrainData.wavingGrassTint);
                    if(firstTerrainData.wavingGrassTint != newWavingGrassTint) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrainData.wavingGrassTint = newWavingGrassTint;
                    }
                    
                    GUILayout.Label("Resolution", EditorStyles.boldLabel);

                    int newHeightmapResolution = EditorGUILayout.IntPopup(TerrainSettings.heightmapResolutionContent, firstTerrainData.heightmapResolution, TerrainSettings.heightmapResolutionsContents,
                            TerrainSettings.heightmapResolutions);
                    if(firstTerrainData.heightmapResolution != newHeightmapResolution && 
                        EditorUtility.DisplayDialog("Terrain Former", "Changing the heightmap resolution will reset the heightmap.", "Change Anyway", "Cancel")) {
                        foreach(TerrainInformation ti in terrainInformations) {
                            ti.terrainData.heightmapResolution = newHeightmapResolution;
                            ti.terrainData.size = terrainSize;
                        }
                        heightmapResolution = newHeightmapResolution;
                        OnEnable();
                    }

                    int newAlphamapResolution = EditorGUILayout.IntPopup(TerrainSettings.alphamapResolutionContent, firstTerrainData.alphamapResolution, TerrainSettings.powersOfTwoFrom16To2048GUIContents,
                            TerrainSettings.powersOfTwoFrom16To2048);
                    if(firstTerrainData.alphamapResolution != newAlphamapResolution &&
                        EditorUtility.DisplayDialog("Terrain Former", "Changing the alphamap resolution will reset the alphamap.", "Change Anyway", "Cancel")) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrainData.alphamapResolution = newAlphamapResolution;
                        alphamapResolution = newAlphamapResolution;
                        OnEnable();
                    }

                    int newBaseMapResolution = EditorGUILayout.IntPopup(TerrainSettings.baseMapResolutionContent, firstTerrainData.baseMapResolution, TerrainSettings.powersOfTwoFrom16To2048GUIContents,
                            TerrainSettings.powersOfTwoFrom16To2048);
                    if(firstTerrainData.baseMapResolution != newBaseMapResolution) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrainData.baseMapResolution = newBaseMapResolution;
                    }


                    float newBasemapDistance = EditorGUILayout.Slider("Basemap Distance", firstTerrain.basemapDistance, 0f, 2000f);
                    if(firstTerrain.basemapDistance != newBasemapDistance) {
                        foreach(TerrainInformation ti in terrainInformations) ti.terrain.basemapDistance = newBasemapDistance;
                    }

                    // Detail Resolution
                    int newDetailResolution = Utilities.RoundToNearestAndClamp(GUIUtilities.DelayedIntField(TerrainSettings.detailResolutionContent, firstTerrainData.detailResolution),
                        8, 0, 4048);
                    // Update all detail layers if the detail resolution has changed.
                    if(newDetailResolution != firstTerrainData.detailResolution &&
                        EditorUtility.DisplayDialog("Terrain Former", "Changing the detail map resolution will clear all details.", "Change Anyway", "Cancel")) {
                        List<int[,]> detailLayers = new List<int[,]>();
                        for(int i = 0; i < firstTerrainData.detailPrototypes.Length; i++) {
                            detailLayers.Add(firstTerrainData.GetDetailLayer(0, 0, firstTerrainData.detailWidth, firstTerrainData.detailHeight, i));
                        }
                        foreach(TerrainInformation terrainInformation in terrainInformations) {
                            terrainInformation.terrainData.SetDetailResolution(newDetailResolution, 8);
                            for(int i = 0; i < detailLayers.Count; i++) {
                                terrainInformation.terrainData.SetDetailLayer(0, 0, i, detailLayers[i]);
                            }
                        }
                    }

                    // Detail Resolution Per Patch
                    int currentDetailResolutionPerPatch = TerrainSettings.GetDetailResolutionPerPatch(firstTerrainData);
                    int newDetailResolutionPerPatch = Mathf.Clamp(GUIUtilities.DelayedIntField(TerrainSettings.detailResolutionPerPatchContent, currentDetailResolutionPerPatch), 8, 128);
                    if(newDetailResolutionPerPatch != currentDetailResolutionPerPatch) {
                        foreach(TerrainInformation terrainInformation in terrainInformations) {
                            terrainInformation.terrainData.SetDetailResolution(firstTerrainData.detailResolution, newDetailResolutionPerPatch);
                        }
                    }

                    if(firstTerrain.materialType != Terrain.MaterialType.Custom) {
                        firstTerrain.materialTemplate = null;
                    }

                    // Draw the terrain informations as visuall representations
                    if(terrainInformations.Count > 1) {
                        GUILayout.Space(5f);
                        neighboursFoldout = GUIUtilities.LargeClickRegionFoldout("Neighbours", neighboursFoldout, showBehaviourFoldoutStyle);
                        if(neighboursFoldout) {
                            Rect hoverRect = new Rect();
                            string hoverText = null;

                            const int neighboursCellSize = 30;
                            const int neighboursCellSizeMinusOne = neighboursCellSize - 1;

                            Rect neighboursGridRect = GUILayoutUtility.GetRect(Screen.width - 35f, numberOfTerrainsVertically * neighboursCellSize + 15f);
                            int neighboursGridRectWidth = neighboursCellSizeMinusOne * numberOfTerrainsHorizontally;
                            int neighboursGridRectHeight = neighboursCellSizeMinusOne * numberOfTerrainsVertically;
                            neighboursGridRect.yMin += 15f;
                            neighboursGridRect.xMin = Screen.width * 0.5f - neighboursGridRectWidth * 0.5f;
                            neighboursGridRect.width = neighboursGridRectWidth;

                            if(neighboursGridRect.Contains(Event.current.mousePosition)) Repaint();

                            GUIStyle boldLabelWithoutPadding = new GUIStyle(EditorStyles.boldLabel);
                            boldLabelWithoutPadding.padding = new RectOffset();
                            boldLabelWithoutPadding.alignment = TextAnchor.MiddleCenter;
                            // Axis Labels
                            GUI.Label(new Rect(Screen.width * 0.5f - 9f, neighboursGridRect.y - 15f, 20f, 10f), "Z", boldLabelWithoutPadding);
                            GUI.Label(new Rect(neighboursGridRect.xMax + 7f, neighboursGridRect.y + neighboursGridRectHeight * 0.5f - 6f, 10f, 10f), "X", boldLabelWithoutPadding);

                            foreach(TerrainInformation terrainInformation in terrainInformations) {
                                GUI.color = terrainInformation.terrain == firstTerrain && !isTerrainGridParentSelected ? new Color(0.4f, 0.4f, 0.75f) : Color.white;
                                Rect cellRect = new Rect(neighboursGridRect.x + terrainInformation.gridXCoordinate * neighboursCellSizeMinusOne, neighboursGridRect.y + 
                                    (numberOfTerrainsVertically - 1 - terrainInformation.gridYCoordinate) * neighboursCellSizeMinusOne, neighboursCellSize, neighboursCellSize);
                                
                                if(cellRect.Contains(Event.current.mousePosition)) {
                                    if(Event.current.type == EventType.MouseUp) {
                                        Selection.activeGameObject = terrainInformation.terrain.gameObject;
                                    } else {
                                        hoverText = terrainInformation.terrain.name;
                                        if(terrainInformation.terrain == firstTerrain && isTerrainGridParentSelected == false) hoverText += " (selected)";
                                        Vector2 calculatedSize = GUI.skin.box.CalcSize(new GUIContent(hoverText));
                                        hoverRect = new Rect(Mathf.Max(cellRect.x + 15f - calculatedSize.x * 0.5f, 0f), cellRect.y + calculatedSize.y + 5f, calculatedSize.x, calculatedSize.y);
                                    }
                                } 

                                GUI.Box(cellRect, (terrainInformation.gridXCoordinate + 1) + "x" + (terrainInformation.gridYCoordinate + 1), neighboursCellBoxStyle);
                            }

                            GUI.color = Color.white;

                            if(hoverText != null) {
                                GUI.Box(hoverRect, hoverText);
                            }
                        }
                    }

                    break;
            }

            float lastLabelWidth = EditorGUIUtility.labelWidth;

            EditorGUILayout.Space();
            
            if(CurrentTool == Tool.None || CurrentTool >= firstNonSculptiveTool) return;

            /**
            * Brush Selection
            */
            if(settings.AlwaysShowBrushSelection || isSelectingBrush) {
                Rect brushesTitleRect = EditorGUILayout.GetControlRect();
                GUI.Label(brushesTitleRect, settings.AlwaysShowBrushSelection ? "Brushes" : "Select Brush", EditorStyles.boldLabel);

                if(settings.AlwaysShowBrushSelection) {
                    brushesTitleRect.xMin = brushesTitleRect.xMax - 300f;
                    GUI.Label(brushesTitleRect, CurrentBrush is FalloffBrush ? "Procedural Brush" : CurrentBrush.name, brushNameAlwaysShowBrushSelectionStyle);
                }
                
                if(settings.brushSelectionDisplayType == BrushSelectionDisplayType.Tabbed) {
                    currentToolSettings.SelectedBrushTab = GUIUtilities.BrushTypeToolbar(currentToolSettings.SelectedBrushTab, brushCollection);
                    currentToolSettings.SelectedBrushId = GUIUtilities.BrushSelectionGrid(currentToolSettings.SelectedBrushId);
                } else {
                    currentToolSettings.SelectedBrushId = GUIUtilities.BrushSelectionGrid(currentToolSettings.SelectedBrushId);
                }
            }

            if(settings.AlwaysShowBrushSelection == false && isSelectingBrush == true) return;
            if(settings.AlwaysShowBrushSelection) {
                GUILayout.Space(6f);
            }
            if(CurrentTool != Tool.RaiseOrLower) {
                GUILayout.Label("Brush", EditorStyles.boldLabel);
            }

            // The width of the area used to show the button to select a brush. Only applicable when AlwaysShowBrushSelection is false.
            float brushSelectionWidth = Mathf.Clamp(settings.brushPreviewSize + 28f, 80f, 84f);

            GUILayout.BeginHorizontal(); // Brush Parameter Editor Horizontal Group

            // TODO: Remove this debug stuff
            GUIStyle unpaddedBoxStyle = new GUIStyle(GUI.skin.box);
            unpaddedBoxStyle.margin = new RectOffset();
            unpaddedBoxStyle.padding = new RectOffset();

            // Draw Brush Paramater Editor
            if(settings.AlwaysShowBrushSelection) {
                EditorGUILayout.BeginVertical();
            } else {
                EditorGUILayout.BeginVertical(GUILayout.Width(Screen.width - brushSelectionWidth - 15f));
            }

            bool isBrushProcedural = CurrentBrush is FalloffBrush;

            currentToolSettings.BrushSize = EditorGUILayout.Slider("Size", currentToolSettings.BrushSize, MinBrushSize, MaxBrushSize);

            if(CurrentTool == Tool.PaintTexture) {
                currentToolSettings.BrushSpeed = EditorGUILayout.Slider("Opacity", currentToolSettings.BrushSpeed, minBrushSpeed, 1f);
            } else if(CurrentTool == Tool.Smooth) {
                currentToolSettings.BrushSpeed = EditorGUILayout.Slider("Speed", currentToolSettings.BrushSpeed, minBrushSpeed, 1f);
            } else {
                currentToolSettings.BrushSpeed = EditorGUILayout.Slider("Speed", currentToolSettings.BrushSpeed, minBrushSpeed, maxBrushSpeed);
            }
            
            if(isBrushProcedural) {
                /**
                * I must create a new AnimationCurve based on the existing BrushFalloff keys; otherwise, 
                * Unity will automatically assign BrushFalloff to its new value, which would make the 
                * equality comparer always return true (meaning equal)
                */
                AnimationCurve newCurve = EditorGUILayout.CurveField("Falloff", new AnimationCurve(BrushFalloff.keys));
                if(AreAnimationCurvesEqual(newCurve, BrushFalloff) == false) {
                    BrushFalloff = newCurve;
                    BrushFalloffChanged();
                }
            } else {
                GUIUtilities.FillAndRightControl(
                    fillControl: (r) => {
                        Rect falloffToggleRect = new Rect(r);
                        falloffToggleRect.xMax = EditorGUIUtility.labelWidth;
                        currentToolSettings.UseFalloffForCustomBrushes = EditorGUI.Toggle(falloffToggleRect, currentToolSettings.UseFalloffForCustomBrushes);

                        Rect falloffToggleLabelRect = new Rect(falloffToggleRect);
                        falloffToggleLabelRect.xMin += 15f;
                        EditorGUI.PrefixLabel(falloffToggleLabelRect, new GUIContent("Falloff"));

                        Rect falloffAnimationCurveRect = new Rect(r);
                        falloffAnimationCurveRect.xMin = EditorGUIUtility.labelWidth + 14f;
                        AnimationCurve newCurve = EditorGUI.CurveField(falloffAnimationCurveRect, new AnimationCurve(BrushFalloff.keys));
                        
                        if(AreAnimationCurvesEqual(newCurve, BrushFalloff) == false) {
                            BrushFalloff = newCurve;
                            BrushFalloffChanged();
                        }
                    },
                    rightControl: (r) => {
                        using(new GUIUtilities.GUIEnabledBlock(currentToolSettings.UseFalloffForCustomBrushes)) {
                            Rect alphaFalloffLabelRect = new Rect(r);
                            alphaFalloffLabelRect.xMin += 14;
                            GUI.Label(alphaFalloffLabelRect, "Alpha");

                            Rect alphaFalloffRect = new Rect(r);
                            alphaFalloffRect.xMin--;
                            currentToolSettings.UseAlphaFalloff = EditorGUI.Toggle(alphaFalloffRect, currentToolSettings.UseAlphaFalloff);
                        }
                    },
                    rightControlWidth: 54
                );
            }

            // We need to delay updating brush samples while changing falloff until changes have stopped for at least one frame
            if(falloffChangeQueued && (EditorApplication.timeSinceStartup - lastTimeBrushSamplesWereUpdated) > 0.05d) {
                falloffChangeQueued = false;
                UpdateDirtyBrushSamples();
            }

            if(isBrushProcedural == false && currentToolSettings.UseFalloffForCustomBrushes == false) {
                GUI.enabled = false;
            }

            EditorGUI.indentLevel = 1;
            currentToolSettings.BrushRoundness = EditorGUILayout.Slider("Roundness", currentToolSettings.BrushRoundness, 0f, 1f);
            EditorGUI.indentLevel = 0;

            if(isBrushProcedural == false && currentToolSettings.UseFalloffForCustomBrushes == false) {
                GUI.enabled = true;
            }

            /**
            * Custom Brush Angle
            */
            currentToolSettings.BrushAngle = EditorGUILayout.Slider("Angle", currentToolSettings.BrushAngle, -180f, 180f);

            /**
            * Invert Brush (for custom brushes only)
            */
            if(settings.invertBrushTexturesGlobally) {
                GUI.enabled = false;
                EditorGUILayout.Toggle("Invert", true);
                GUI.enabled = true;
            } else {
                currentToolSettings.InvertBrushTexture = EditorGUILayout.Toggle("Invert", currentToolSettings.InvertBrushTexture);
            }
            
            /**
            * Behaviour
            */
            EditorGUILayout.LabelField("Behaviour", EditorStyles.boldLabel);
            
            GUIUtilities.ToggleAndMinMax(
                toggleControl: (r) => {
                    currentToolSettings.useBrushSpacing = EditorGUI.ToggleLeft(r, "Random Spacing", currentToolSettings.useBrushSpacing);
                },
                minMaxSliderControl: (r) => {
                    float minBrushSpacing = currentToolSettings.MinBrushSpacing;
                    float maxBrushSpacing = currentToolSettings.MaxBrushSpacing;
                    EditorGUI.MinMaxSlider(r, ref minBrushSpacing, ref maxBrushSpacing, minSpacingBounds, maxSpacingBounds);
                    currentToolSettings.SetMinBrushSpacing(minBrushSpacing, minSpacingBounds);
                    currentToolSettings.SetMaxBrushSpacing(maxBrushSpacing, maxSpacingBounds);
                },
                minFloatControl: (r) => {
                    currentToolSettings.SetMinBrushSpacing(Mathf.Clamp(EditorGUI.FloatField(r, currentToolSettings.MinBrushSpacing), minSpacingBounds, maxSpacingBounds),
                        minSpacingBounds);
                },
                maxFloatControl: (r) => {
                    currentToolSettings.SetMaxBrushSpacing(Mathf.Clamp(EditorGUI.FloatField(r, currentToolSettings.MaxBrushSpacing), minSpacingBounds, maxSpacingBounds),
                        maxSpacingBounds);
                },
                enableFillControl: true,
                enableToggle: true
            );

            float maxRandomOffset = Mathf.Min(firstTerrainData.heightmapWidth, firstTerrainData.heightmapHeight) * 0.1f;
            GUIUtilities.ToggleAndFill(
                toggleControl: (r) => {
                    currentToolSettings.useRandomOffset = EditorGUI.ToggleLeft(r, "Random Offset", currentToolSettings.useRandomOffset);
                },
                fillControl: (r) => {
                    currentToolSettings.RandomOffset = EditorGUI.Slider(r, currentToolSettings.RandomOffset, minRandomOffset, maxRandomOffset);
                },
                enableFillControl: true,
                enableToggle: true
            );

            GUIUtilities.ToggleAndMinMax(
                toggleControl: (r) => {
                    currentToolSettings.useRandomRotation = EditorGUI.ToggleLeft(r, "Random Rotation", currentToolSettings.useRandomRotation);
                },
                minMaxSliderControl: (r) => {
                    float minRandomRotation = currentToolSettings.MinRandomRotation;
                    float maxRandomRotation = currentToolSettings.MaxRandomRotation;
                    EditorGUI.MinMaxSlider(r, ref minRandomRotation, ref maxRandomRotation, minRandomRotationBounds, maxRandomRotationBounds);
                    currentToolSettings.SetMinRandomRotation(minRandomRotation, minRandomRotationBounds);
                    currentToolSettings.SetMaxRandomRotation(maxRandomRotation, maxRandomRotationBounds);
                },
                minFloatControl: (r) => {
                    currentToolSettings.SetMinRandomRotation(Mathf.Clamp(EditorGUI.FloatField(r, currentToolSettings.MinRandomRotation),
                        minRandomRotationBounds, maxRandomRotationBounds), minRandomRotationBounds);
                },
                maxFloatControl: (r) => {
                    currentToolSettings.SetMaxRandomRotation(Mathf.Clamp(EditorGUI.FloatField(r, currentToolSettings.MaxRandomRotation),
                        minRandomRotationBounds, maxRandomRotationBounds), maxRandomRotationBounds);
                },
                enableFillControl: true,
                enableToggle: true
            );

            EditorGUILayout.EndVertical();

            if(settings.AlwaysShowBrushSelection == false) {
                GUILayout.Space(-4f);

                GUILayout.BeginVertical(new GUILayoutOption[] { GUILayout.Width(brushSelectionWidth), GUILayout.Height(95f) });

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayoutOption[] brushNameLabelLayoutOptions = { GUILayout.Width(brushSelectionWidth - 17f), GUILayout.Height(24f) };
                GUILayout.Box(CurrentBrush is FalloffBrush ? "Procedural Brush" : CurrentBrush.name, miniBoldLabelCentered, brushNameLabelLayoutOptions);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // Draw Brush Preview
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if(GUILayout.Button(CurrentBrush.previewTexture, GUIStyle.none)) {
                    ToggleSelectingBrush();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                // Draw Select/Cancel Brush Selection Button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if(GUILayout.Button("Select", miniButtonWithoutMargin, new GUILayoutOption[] { GUILayout.Width(60f), GUILayout.Height(18f) })) {
                    ToggleSelectingBrush();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal(); // Brush Parameter Editor Horizontal Group

            terrainMismatchManager.Draw();

            EditorGUIUtility.labelWidth = lastLabelWidth;
        }
        
        // Updates the position of the projector in the scene view
        public void OnPreSceneGUI() {
            if(Initialize() == false) return;
            if(CurrentTool == Tool.None) return;

            if((Event.current.control && mouseIsDown) == false) {
                UpdateProjector();
            }
        }
        
        void OnSceneGUI() {
            if(Initialize() == false) return;
            
            // Get a unique ID for this editor so we can get events unique the editor's scope
            int controlId = GUIUtility.GetControlID(terrainEditorHash, FocusType.Passive);

            Event currentEvent = Event.current;
            EventType editorEventType = currentEvent.GetTypeForControl(controlId);
            
            CheckKeyboardShortcuts(currentEvent);

            if(CurrentTool == Tool.None || CurrentTool >= firstNonSculptiveTool) return;

            /**
            * Draw scene-view information
            */
            // TODO: Make displayX settings into a system so they aren't hard coded like they are now.
            if(settings.showSceneViewInformation && (settings.displayCurrentHeight || settings.displayCurrentTool || settings.displaySculptOntoMode || settings.displayBrushSizeIncrement)) {
                Handles.BeginGUI();
                if(sceneViewInformationAreaStyle == null) {
                    sceneViewInformationAreaStyle = new GUIStyle(GUI.skin.box);
                    sceneViewInformationAreaStyle.padding = new RectOffset(5, 0, 5, 0);
                }
                if(sceneViewInformationAreaStyle.normal.background == null || sceneViewInformationAreaStyle.normal.background.name == "OL box") {
                    sceneViewInformationAreaStyle.normal.background = (Texture2D)AssetDatabase.LoadAssetAtPath(settings.mainDirectory + "Textures/SceneInfoPanel.PSD", typeof(Texture2D));
                    sceneViewInformationAreaStyle.border = new RectOffset(12, 12, 12, 12);
               } 

                int lines = settings.displayCurrentHeight ? 1 : 0;
                lines += settings.displayCurrentTool ? 1 : 0;
                lines += settings.displaySculptOntoMode ? 1 : 0;
                lines += settings.displayBrushSizeIncrement ? 1 : 0;
                
                GUILayout.BeginArea(new Rect(5f, 5f, 265f, 15f * lines + 14f), sceneViewInformationAreaStyle);

                float yOffset = 6f;

                if(settings.displayCurrentTool) {
                    EditorGUI.LabelField(new Rect(7f, yOffset, 135f, 18f), "Tool:");
                    GUI.Label(new Rect(145f, yOffset, 135f, 18f), modeNames[currentTool.Value]);
                    yOffset += 15f;
                }
                if(settings.displayCurrentHeight) {
                    float height;
                    EditorGUI.LabelField(new Rect(7f, yOffset, 135f, 18f), "Height:");
                    if(currentEvent.control && mouseIsDown) {
                        EditorGUI.LabelField(new Rect(145f, yOffset, 135f, 18f), lastClickPosition.y.ToString("0.00"));
                    } else if(GetTerrainHeightAtMousePosition(out height)) {
                        EditorGUI.LabelField(new Rect(145f, yOffset, 135f, 18f), height.ToString("0.00"));
                    } else {
                        EditorGUI.LabelField(new Rect(145f, yOffset, 135f, 18f), "0.00");
                    }
                    yOffset += 15f;
                }
                if(settings.displaySculptOntoMode) {
                    EditorGUI.LabelField(new Rect(7f, yOffset, 135f, 18f), "Sculpt Onto:");
                    if(CurrentTool == Tool.SetHeight || CurrentTool == Tool.Flatten) {
                        EditorGUI.LabelField(new Rect(145f, yOffset, 135f, 18f), "Plane (locked)");
                    } else {
                        EditorGUI.LabelField(new Rect(145f, yOffset, 135f, 18f), raycastModes[settings.raycastOntoFlatPlane ? 0 : 1]);
                    }
                    yOffset += 15f;
                }
                if(settings.displayBrushSizeIncrement) {
                    EditorGUI.LabelField(new Rect(7f, yOffset, 135f, 18f), "Brush Size Increment:");
                    EditorGUI.LabelField(new Rect(145f, yOffset, 135f, 18f), string.Format("{0} ({1:F1} units)", brushSizeIncrementLabels[GetBrushSizeIncrementIndex()].text,
                        terrainSize.x * settings.brushSizeIncrementMultiplier));
                    yOffset += 15f;
                }
                                
                GUILayout.EndArea();
                Handles.EndGUI();
            }

            // Update mouse-related fields
            if(editorEventType == EventType.Repaint || currentEvent.isMouse) {
                if(mousePosition == Vector2.zero) {
                    lastMousePosition = currentEvent.mousePosition;
                } else {
                    lastMousePosition = mousePosition;
                }

                mousePosition = currentEvent.mousePosition;

                if(editorEventType == EventType.MouseDown) {
                    currentTotalMouseDelta = 0;
                } else {
                    currentTotalMouseDelta += mousePosition.y - lastMousePosition.y;
                }
            }

            // Only accept left clicks
            if(currentEvent.button != 0) return;

            switch(editorEventType) {
                // MouseDown will execute the same logic as MouseDrag
                case EventType.MouseDown:
                case EventType.MouseDrag:
                    /*
                    * Break if any of the following rules are true:
                    * 1) The event happening for this window is a MouseDrag event and the hotControl isn't this window
                    * 2) Alt + Click have been executed
                    * 3) The HandleUtllity finds a control closer to this control
                    */
                    if(editorEventType == EventType.MouseDrag &&
                        GUIUtility.hotControl != controlId ||
                        (currentEvent.alt || currentEvent.button != 0) ||
                        HandleUtility.nearestControl != controlId) {
                        break;
                    }
                    if(currentEvent.type == EventType.MouseDown) {
                        /**
                        * To make sure the initial press down always sculpts the terrain while spacing is active, set 
                        * the mouseSpacingDistance to a high value to always activate it straight away
                        */
                        mouseSpacingDistance = float.MaxValue;
                        UpdateRandomSpacing();
                        GUIUtility.hotControl = controlId;
                    }

                    // Update the lastClickPosition when the mouse has been pressed down
                    if(mouseIsDown == false) {
                        Vector3 hitPosition;
                        Vector2 uv;
                        if(Raycast(out hitPosition, out uv)) {
                            lastWorldspaceMousePosition = hitPosition;
                            lastClickPosition = hitPosition;
                            mouseIsDown = true;
                        }
                    }

                    currentEvent.Use();
                    break;
                case EventType.MouseUp:
                    // Reset the hotControl to nothing as long as it matches the TerrainEditor controlID
                    if(GUIUtility.hotControl != controlId) break;

                    GUIUtility.hotControl = 0;
                    
                    foreach(TerrainInformation terrainInformation in terrainInformations) {
                        // Render all aspects of terrain (heightmap, trees and details)
                        terrainInformation.terrain.editorRenderFlags = TerrainRenderFlags.All;

                        if(settings.alwaysUpdateTerrainLODs == false) continue;

                        if(CurrentTool == Tool.PaintTexture) {
                            terrainDataSetBasemapDirtyMethodInfo.Invoke(terrainInformation.terrainData, new object[] { true });
                        }

#if UNITY_5_0 || UNITY_5_1_0 || UNITY_5_1_1
                        applyDelayedHeightmapModificationMethod.Invoke(terrainInformation.terrain, null);
#else
                        terrainInformation.terrain.ApplyDelayedHeightmapModification();
#endif
                    }

                    gridPlane.SetActive(false);

                    // Reset the flatten height tool's value after the mouse has been released
                    flattenHeight = -1f;

                    mouseIsDown = false;
                    currentCommand = null;
                    currentTotalMouseDelta = 0f;
                    lastClickPosition = Vector3.zero;

                    currentEvent.Use();
                    break;
                case EventType.Repaint:
                    SetCursorEnabled(false);
                    break;
                case EventType.Layout:
                    if(CurrentTool == Tool.None) break;

                    // Sets the ID of the default control. If there is no other handle being hovered over, it will choose this value
                    HandleUtility.AddDefaultControl(controlId);
                    break;
            }

            // Apply the current terrain tool
            if(editorEventType == EventType.Repaint && mouseIsDown) {
                Vector3 mouseWorldspacePosition;
                if(GetMousePositionInWorldSpace(out mouseWorldspacePosition)) {
                    if(currentToolSettings.useBrushSpacing) {
                        mouseSpacingDistance += (new Vector2(lastWorldspaceMousePosition.x, lastWorldspaceMousePosition.z) -
                            new Vector2(mouseWorldspacePosition.x, mouseWorldspacePosition.z)).magnitude;
                    }

                    // A random point within a circle that is used for the random offset feature
                    Vector2 randomCirclePoint;
                    if(currentEvent.control == false && currentToolSettings.useRandomOffset) {
                        randomCirclePoint = UnityEngine.Random.insideUnitCircle * currentToolSettings.RandomOffset;
                    } else {
                        randomCirclePoint = Vector2.zero;
                    }

                    Vector3 mousePosition;
                    if(CurrentTool < firstNonSculptiveTool && currentEvent.control) {
                        mousePosition = lastClickPosition;
                    } else {
                        mousePosition = mouseWorldspacePosition;
                    }

                    /**
                    * Calculate the command coordinates for each terrain information which determines which area of a given terrain (if at all) 
                    * will have the current command applied to it
                    */
                    foreach(TerrainInformation terrainInformation in terrainInformations) {
                        terrainInformation.paintInfo = CalculateCommandCoordinatesForTerrain(terrainInformation, mousePosition, randomCirclePoint);
                    }
                    
                    CommandCoordinates terrainGridCommandCoordinates = CalculateCommandCoordinatesForTerrainGrid(mousePosition, randomCirclePoint);

                    /**
                    * Update the grid position
                    */
                    if(settings.showSculptingGridPlane == true) {
                        if(gridPlane.activeSelf == false) {
                            gridPlane.SetActive(true);
                        }

                        Vector3 gridPosition;
                        // If the current tool is interactive, keep the grid at the lastGridPosition
                        if(currentEvent.control) {
                            gridPosition = new Vector3(lastClickPosition.x, lastClickPosition.y + 0.001f, lastClickPosition.z);
                        } else {
                            gridPosition = new Vector3(mouseWorldspacePosition.x, lastClickPosition.y + 0.001f, mouseWorldspacePosition.z);
                        }
                        float gridPlaneDistance = Mathf.Abs(lastClickPosition.y - SceneView.currentDrawingSceneView.camera.transform.position.y);
                        float gridPlaneSize = currentToolSettings.BrushSize * 1.2f;
                        gridPlane.transform.position = gridPosition;
                        gridPlane.transform.localScale = Vector3.one * gridPlaneSize;

                        // Get the Logarithm of base 10 from the distance to get a power to mutliple the grid scale by
                        float power = Mathf.Round(Mathf.Log10(gridPlaneDistance) - 1);

                        // Make the grid appear as if it's being illuminated by the cursor but keeping the grids remain within unit size tiles
                        gridPlaneMaterial.mainTextureOffset = new Vector2(gridPosition.x, gridPosition.z) / Mathf.Pow(10f, power);

                        gridPlaneMaterial.mainTextureScale = new Vector2(gridPlaneSize, gridPlaneSize) / Mathf.Pow(10f, power);
                    }

                    if(currentCommand == null) {
                        // Update the "currentHeights" arrays of all terrains in case they will be used with a command
                        UpdateAllUnmodifiedHeights();
                        
                        switch(CurrentTool) {
                            case Tool.RaiseOrLower:
                                currentCommand = new RaiseOrLowerCommand(BrushSamplesWithSpeed);
                                break;
                            case Tool.Smooth:
                                SmoothCommand smoothCommand = new SmoothCommand(BrushSamplesWithSpeed, settings.boxFilterSize, totalHeightmapSamplesHorizontally, totalHeightmapSamplesVertically);
                                currentCommand = smoothCommand;
                                break;
                            case Tool.SetHeight:
                                SetHeightCommand setHeightCommand = new SetHeightCommand(BrushSamplesWithSpeed);
                                setHeightCommand.normalizedHeight = settings.setHeight / terrainSize.y;
                                currentCommand = setHeightCommand;
                                break;
                            case Tool.Flatten:
                                // Update the flatten height if it was reset before
                                if(flattenHeight == -1f) {
                                    flattenHeight = (mouseWorldspacePosition.y) / terrainSize.y;
                                }
                                FlattenCommand flattenCommand = new FlattenCommand(BrushSamplesWithSpeed);
                                flattenCommand.mode = settings.flattenMode;
                                flattenCommand.flattenHeight = flattenHeight;
                                currentCommand = flattenCommand;
                                break;
                            case Tool.PaintTexture:
                                currentCommand = new TexturePaintCommand(BrushSamplesWithSpeed);
                                break;
                        }
                    } else {
                        /**
                        * Only allow the various Behaviours to be active when control isn't pressed to make these behaviours 
                        * not occur while using interactive tools
                        */
                        if(currentEvent.control == false) {
                            float spacing = currentToolSettings.BrushSize * randomSpacing;

                            // If brush spacing is enabled, do not update the current command until the cursor has exceeded the required distance
                            if(currentToolSettings.useBrushSpacing && mouseSpacingDistance < spacing) {
                                lastWorldspaceMousePosition = mouseWorldspacePosition;
                                return;
                            } else {
                                UpdateRandomSpacing();
                                mouseSpacingDistance = 0f;
                            }

                            if(currentToolSettings.useRandomRotation && (CurrentBrush is FalloffBrush && currentToolSettings.BrushRoundness == 1f) == false) {
                                RotateTemporaryBrushSamples();
                                currentCommand.brushSamples = temporarySamples;
                            }
                        }

                        if(samplesDirty != SamplesDirty.None) {
                            UpdateDirtyBrushSamples();
                        }
                        
                        currentCommand.Execute(currentEvent, terrainGridCommandCoordinates);
                    }
                    
                    brushProjectorGameObject.SetActive(true);
                    
                    float[,,] newAlphamaps = null;
                    float[,] newHeights = null;
                    // Update each terrainInfo's updated terrain region
                    foreach(TerrainInformation terrainInfo in terrainInformations) {
                        if(terrainInfo.paintInfo == null || terrainInfo.hasChangedSinceLastSetHeights == false) continue;

                        terrainInfo.hasChangedSinceLastSetHeights = false;
                        
                        if(currentCommand is TexturePaintCommand) {
                            newAlphamaps = new float[terrainInfo.paintInfo.clippedHeight, terrainInfo.paintInfo.clippedWidth, firstTerrainData.alphamapLayers];
                            for(int l = 0; l < firstTerrainData.alphamapLayers; l++) {
                                for(int x = 0; x < terrainInfo.paintInfo.clippedWidth; x++) {
                                    for(int y = 0; y < terrainInfo.paintInfo.clippedHeight; y++) {
                                        newAlphamaps[y, x, l] = allTextureSamples[terrainInfo.toolCentricYOffset + y + terrainInfo.paintInfo.worldBottom, 
                                            terrainInfo.toolCentricXOffset + x + terrainInfo.paintInfo.worldLeft, l];
                                    }
                                }
                            }

                            terrainInfo.terrain.editorRenderFlags = TerrainRenderFlags.Heightmap;

                            terrainInfo.terrainData.SetAlphamaps(terrainInfo.paintInfo.worldLeft, terrainInfo.paintInfo.worldBottom, newAlphamaps);
                            terrainDataSetBasemapDirtyMethodInfo.Invoke(terrainInfo.terrainData, new object[] { false });

                        } else {
                            newHeights = new float[terrainInfo.paintInfo.clippedHeight, terrainInfo.paintInfo.clippedWidth];
                            for(int x = 0; x < terrainInfo.paintInfo.clippedWidth; x++) {
                                for(int y = 0; y < terrainInfo.paintInfo.clippedHeight; y++) {
                                    newHeights[y, x] = allTerrainHeights[terrainInfo.toolCentricYOffset + y + terrainInfo.paintInfo.worldBottom, terrainInfo.toolCentricXOffset + x + terrainInfo.paintInfo.worldLeft];
                                }
                            }

                            terrainInfo.terrain.editorRenderFlags = TerrainRenderFlags.Heightmap;

                            if(settings.alwaysUpdateTerrainLODs) {
                                terrainInfo.terrainData.SetHeights(terrainInfo.paintInfo.worldLeft, terrainInfo.paintInfo.worldBottom, newHeights);
                            } else {
#if UNITY_5_0 || UNITY_5_1_0 || UNITY_5_1_1
                                setHeightsDelayedLODMethod.Invoke(terrainInfo.terrainData, new object[] { terrainInfo.paintInfo.worldLeft, terrainInfo.paintInfo.worldBottom, newHeights });
#else
                                terrainInfo.terrainData.SetHeightsDelayLOD(terrainInfo.paintInfo.worldLeft, terrainInfo.paintInfo.worldBottom, newHeights);
#endif
                            }
                        }
                    }
                }

                lastWorldspaceMousePosition = mouseWorldspacePosition;

                // While the mouse is down, always repaint
                SceneView.RepaintAll();
            }
        }
        
        private static Vector2 preferencesItemScrollPosition;
        [PreferenceItem("Terrain Former")]
        private static void DrawPreferences() {
            if(settings == null) {
                InitializeSettings();
            }

            if(settings == null) {
                EditorGUILayout.HelpBox("There was a problem in initializing Terrain Former's settings.", MessageType.Warning);
                return;
            }

            EditorGUIUtility.labelWidth = 195f;

            preferencesItemScrollPosition = EditorGUILayout.BeginScrollView(preferencesItemScrollPosition);
            GUILayout.Label("General", EditorStyles.boldLabel);

            // Raycast Onto Plane
            Rect raycastModeRect = EditorGUILayout.GetControlRect();
            Rect raycastModeToolbarRect = EditorGUI.PrefixLabel(raycastModeRect, raycastModeLabelContent);
            
            int raycastModeIndex = settings.raycastOntoFlatPlane == true ? 0 : 1;
            int selectedRaycastMode = GUI.Toolbar(raycastModeToolbarRect, raycastModeIndex, raycastModes, EditorStyles.radioButton);
            settings.raycastOntoFlatPlane = selectedRaycastMode == 0;

            // Brush Size Increment
            int brushSizeIncrementIndex = EditorGUILayout.Popup(brushSizeIncrementContent, GetBrushSizeIncrementIndex(), brushSizeIncrementLabels);
            settings.brushSizeIncrementMultiplier = brushSizeIncrementValues[brushSizeIncrementIndex];

            // Show Sculpting Grid Plane
            EditorGUI.BeginChangeCheck();
            settings.showSculptingGridPlane = EditorGUILayout.Toggle(showSculptingGridPlaneContent, settings.showSculptingGridPlane);
            if(EditorGUI.EndChangeCheck()) {
                SceneView.RepaintAll();
            }

            EditorGUIUtility.fieldWidth += 5f;
            settings.brushColour.Value = EditorGUILayout.ColorField("Brush Colour", settings.brushColour);
            EditorGUIUtility.fieldWidth -= 5f;

            settings.alwaysUpdateTerrainLODs = EditorGUILayout.Toggle(alwaysUpdateTerrainLODsContent, settings.alwaysUpdateTerrainLODs);

            bool newInvertBrushTexturesGlobally = EditorGUILayout.Toggle("Invert Brush Textures Globally", settings.invertBrushTexturesGlobally);
            if(newInvertBrushTexturesGlobally != settings.invertBrushTexturesGlobally) {
                settings.invertBrushTexturesGlobally = newInvertBrushTexturesGlobally;
                if(Instance != null) {
                    Instance.UpdateAllNecessaryPreviewTextures();
                    Instance.Repaint();
                }
            }

            GUILayout.Label("User Interface", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            settings.AlwaysShowBrushSelection = EditorGUILayout.Toggle(alwaysShowBrushSelectionContent, settings.AlwaysShowBrushSelection);

            settings.brushSelectionDisplayType = (BrushSelectionDisplayType)EditorGUILayout.Popup("Brush Selection Display Type",
                (int)settings.brushSelectionDisplayType, brushSelectionDisplayTypeLabels);

            Rect previewSizeRect = EditorGUILayout.GetControlRect();
            Rect previewSizeToolbarRect = EditorGUI.PrefixLabel(previewSizeRect, new GUIContent("Brush Preview Size"));
            previewSizeToolbarRect.xMax -= 2;
            int newBrushPreviewSize = EditorGUI.IntPopup(previewSizeToolbarRect, settings.brushPreviewSize, previewSizesContent, previewSizeValues);
            if(newBrushPreviewSize != settings.brushPreviewSize) {
                settings.brushPreviewSize = newBrushPreviewSize;
                if(Instance != null) Instance.UpdateAllNecessaryPreviewTextures();
            }
            if(EditorGUI.EndChangeCheck()) {
                if(Instance != null) Instance.Repaint();
            }

            GUILayout.Space(2f);

            EditorGUI.BeginChangeCheck();
            settings.showSceneViewInformation = EditorGUILayout.BeginToggleGroup("Show Scene View Information", settings.showSceneViewInformation);
            EditorGUI.indentLevel = 1;
            GUI.enabled = settings.showSceneViewInformation;
            settings.displayCurrentTool = EditorGUILayout.Toggle("Display Current Tool", settings.displayCurrentTool);
            settings.displayCurrentHeight = EditorGUILayout.Toggle("Display Current Height", settings.displayCurrentHeight);
            settings.displaySculptOntoMode = EditorGUILayout.Toggle("Display Sculpt Onto", settings.displaySculptOntoMode);
            settings.displayBrushSizeIncrement = EditorGUILayout.Toggle("Display Brush Size Increment", settings.displayBrushSizeIncrement);
            EditorGUILayout.EndToggleGroup();
            EditorGUI.indentLevel = 0;
            GUI.enabled = true;
            if(EditorGUI.EndChangeCheck()) {
                SceneView.RepaintAll();
            }
            
            GUILayout.Label("Shortcuts", EditorStyles.boldLabel);
            foreach(Shortcut shortcut in Shortcut.Shortcuts.Values) {
                shortcut.DoShortcutField();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // If all the settings are at their default value, disable the "Restore Defaults"
            bool shortcutsNotDefault = false;
            foreach(Shortcut shortcut in Shortcut.Shortcuts.Values) {
                if(shortcut.Binding != shortcut.defaultBinding) {
                    shortcutsNotDefault = true;
                    break;
                }
            }

            if(settings.AreSettingsDefault() && shortcutsNotDefault == false) {
                GUI.enabled = false;
            }
            if(GUILayout.Button("Restore Defaults", GUILayout.Width(120f), GUILayout.Height(20))) {
                if(EditorUtility.DisplayDialog("Restore Defaults", "Are you sure you want to restore all settings to their defaults?", "Restore Defaults", "Cancel")) {
                    settings.RestoreDefaultSettings();

                    // Reset shortcuts to defaults
                    foreach(Shortcut shortcut in Shortcut.Shortcuts.Values) {
                        shortcut.waitingForInput = false;
                        shortcut.Binding = shortcut.defaultBinding;
                    }
                }
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private static int GetBrushSizeIncrementIndex() {
            int brushSizeIncrementIndex = 0;
            for(int i = 0; i < brushSizeIncrementValues.Length; i++) {
                if(settings.brushSizeIncrementMultiplier == brushSizeIncrementValues[i]) {
                    brushSizeIncrementIndex = i;
                    break;
                }
            }
            return brushSizeIncrementIndex;
        }

        private CommandCoordinates CalculateCommandCoordinatesForTerrain(TerrainInformation terrainInformation, Vector3 mousePosition, Vector2 randomCirclePoint) {
            Vector2 uv = new Vector2((mousePosition.x - terrainInformation.transform.position.x + randomCirclePoint.x) / terrainInformation.terrainData.size.x,
                (mousePosition.z - terrainInformation.transform.position.z + randomCirclePoint.y) / terrainInformation.terrainData.size.z);
            
            return CalculateCommandCoordinates(uv, terrainInformation.gridXCoordinate, terrainInformation.gridYCoordinate, currentToolsResolution, currentToolsResolution);
        }

        private CommandCoordinates CalculateCommandCoordinatesForTerrainGrid(Vector3 mousePosition, Vector2 randomCirclePoint) {
            float terrainGridHorizontalSize = numberOfTerrainsHorizontally * terrainSize.x;
            float terrainGridVerticalSize = numberOfTerrainsVertically * terrainSize.z;

            Vector2 mouseDeltaWithRandomCirclePoint = new Vector2(mousePosition.x - bottomLeftMostTerrainTransform.position.x + randomCirclePoint.x,
                mousePosition.z - bottomLeftMostTerrainTransform.position.z + randomCirclePoint.y);

            Vector2 uv = new Vector2(mouseDeltaWithRandomCirclePoint.x / terrainGridHorizontalSize, mouseDeltaWithRandomCirclePoint.y / terrainGridVerticalSize);
            
            int gridXCoordinate = Mathf.Max(Mathf.FloorToInt(mouseDeltaWithRandomCirclePoint.x / terrainSize.x), 0);
            int gridYCoordinate = Mathf.Max(Mathf.FloorToInt(mouseDeltaWithRandomCirclePoint.y / terrainSize.z), 0);

            int totalSamplesHorizontally = numberOfTerrainsHorizontally * currentToolsResolution - gridXCoordinate;
            int totalSamplesVertically = numberOfTerrainsVertically * currentToolsResolution - gridYCoordinate;

            return CalculateCommandCoordinates(uv, gridXCoordinate, gridYCoordinate, totalSamplesHorizontally, totalSamplesVertically);
        }

        private CommandCoordinates CalculateCommandCoordinates(Vector2 uv, int gridXCoordinate, int gridYCoordinate, int totalHorizontalSamples, int totalVerticalSamples) {
            // The heightmap/alphamap samples that the cursor currently is pointing to
            int cursorHorizontal = Mathf.RoundToInt(uv.x * totalHorizontalSamples) - gridXCoordinate;
            int cursorVertical = Mathf.RoundToInt(uv.y * totalVerticalSamples) - gridYCoordinate;

            if(CurrentTool != Tool.PaintTexture) {
                cursorHorizontal -= gridXCoordinate;
                cursorVertical -= gridYCoordinate;
            }
            
            // The bottom-left segments of where the brush samples will start.
            int worldLeft = Mathf.Max(Mathf.RoundToInt(cursorHorizontal - halfBrushSizeInSamples), 0);
            int worldBottom = Mathf.Max(Mathf.RoundToInt(cursorVertical - halfBrushSizeInSamples), 0);
            
            if(CurrentTool != Tool.PaintTexture) {
                worldLeft = Mathf.Max(worldLeft - gridXCoordinate, 0);
                worldBottom = Mathf.Max(worldBottom - gridYCoordinate, 0);
            }

            // Check if there aren't any segments that will even be painted
            if(worldLeft > totalHorizontalSamples || worldBottom > totalVerticalSamples || cursorHorizontal + halfBrushSizeInSamples < 0 || cursorVertical + halfBrushSizeInSamples < 0) {
                return null;
            }

            /** 
            * Create a paint patch used for offsetting the terrain samples.
            * Clipped left contains how many segments are being clipped to the left side of the terrain. The value is 0 if there 
            * are no segments being clipped. This same pattern applies to clippedBottom, clippedWidth, and clippedHeight respectively.
            */

            int clippedLeft = 0;
            if(cursorHorizontal - halfBrushSizeInSamples < 0) {
                clippedLeft = Mathf.RoundToInt(Mathf.Abs(cursorHorizontal - halfBrushSizeInSamples));
            }

            int clippedBottom = 0;
            if(cursorVertical - halfBrushSizeInSamples < 0) {
                clippedBottom = Mathf.RoundToInt(Mathf.Abs(cursorVertical - halfBrushSizeInSamples));
            }

            int clippedWidth = brushSizeInSamples - clippedLeft;
            if(worldLeft + brushSizeInSamples > totalHorizontalSamples) {
                clippedWidth = totalHorizontalSamples - worldLeft - clippedLeft;
            }

            int clippedHeight = brushSizeInSamples - clippedBottom;
            if(worldBottom + brushSizeInSamples > totalVerticalSamples) {
                clippedHeight = totalVerticalSamples - worldBottom - clippedBottom;
            }

            return new CommandCoordinates(clippedLeft, clippedBottom, clippedWidth, clippedHeight, worldLeft, worldBottom);
        }

        private void UpdateRandomSpacing() {
            randomSpacing = UnityEngine.Random.Range(currentToolSettings.MinBrushSpacing, currentToolSettings.MaxBrushSpacing);
        }

        private void RotateTemporaryBrushSamples() {
            cachedTerrainBrush = brushCollection.brushes[currentToolSettings.SelectedBrushId];

            if(temporarySamples == null || temporarySamples.GetLength(0) != brushSizeInSamples) {
                temporarySamples = new float[brushSizeInSamples, brushSizeInSamples];
            }

            Vector2 midPoint = new Vector2(brushSizeInSamples * 0.5f, brushSizeInSamples * 0.5f);
            float angle = currentToolSettings.BrushAngle + UnityEngine.Random.Range(currentToolSettings.MinRandomRotation, currentToolSettings.MaxRandomRotation);
            float sineOfAngle = Mathf.Sin(angle * Mathf.Deg2Rad);
            float cosineOfAngle = Mathf.Cos(angle * Mathf.Deg2Rad);
            Vector2 newPoint;

            for(int x = 0; x < brushSizeInSamples; x++) {
                for(int y = 0; y < brushSizeInSamples; y++) {
                    newPoint = Utilities.RotatePointAroundPoint(new Vector2(x, y), midPoint, angle, sineOfAngle, cosineOfAngle);
                    temporarySamples[x, y] = GetInteropolatedBrushSample(newPoint.x, newPoint.y) * currentToolSettings.BrushSpeed;
                }
            }
        }

        private float GetInteropolatedBrushSample(float x, float y) {
            int flooredX = Mathf.FloorToInt(x);
            int flooredY = Mathf.FloorToInt(y);
            int flooredXPlus1 = flooredX + 1;
            int flooredYPlus1 = flooredY + 1;

            if(flooredX < 0 || flooredX >= brushSizeInSamples || flooredY < 0 || flooredY >= brushSizeInSamples) return 0f;

            float topLeftSample = cachedTerrainBrush.samples[flooredX, flooredY];
            float topRightSample = 0f;
            float bottomLeftSample = 0f;
            float bottomRightSample = 0f;

            if(flooredXPlus1 < brushSizeInSamples) {
                topRightSample = cachedTerrainBrush.samples[flooredXPlus1, flooredY];
            }

            if(flooredYPlus1 < brushSizeInSamples) {
                bottomLeftSample = cachedTerrainBrush.samples[flooredX, flooredYPlus1];

                if(flooredXPlus1 < brushSizeInSamples) {
                    bottomRightSample = cachedTerrainBrush.samples[flooredXPlus1, flooredYPlus1];
                }
            }

            return Mathf.Lerp(Mathf.Lerp(topLeftSample, topRightSample, x % 1f), Mathf.Lerp(bottomLeftSample, bottomRightSample, x % 1f), y % 1f);
        }

        private void UpdateDirtyBrushSamples() {
            if(CurrentBrush == null) return;

            if(samplesDirty == SamplesDirty.None) return;

            // Update only the brush samples, and don't even update the projector texture
            if((samplesDirty & SamplesDirty.BrushSamples) == SamplesDirty.BrushSamples) {
                CurrentBrush.UpdateSamplesWithSpeed(brushSizeInSamples);
            }
            if((samplesDirty & SamplesDirty.ProjectorTexture) == SamplesDirty.ProjectorTexture) {
                UpdateBrushProjectorTextureAndSamples();
            }
            if((samplesDirty & SamplesDirty.InspectorTexture) == SamplesDirty.InspectorTexture) {
                UpdateBrushInspectorTexture();
            }

            brushProjector.transform.eulerAngles = new Vector3(90f, 0f, 0f);

            samplesDirty = SamplesDirty.None;
        }

        /**
        * TODO: It should be possible to eliminate the need to do Repaint, currentEvent.Use() and to not be required to enter the 
        * shortcut name as a string.
        */
        private void CheckKeyboardShortcuts(Event currentEvent) {
            if(GUIUtility.hotControl != 0) return;
            if(currentEvent.type != EventType.KeyDown) return;

            // Only check for shortcuts when no terrain command is active
            if(currentCommand != null) return;

            /**
            * Check to make sure there is no textField focused. This will ensure that shortcut strokes will not override
            * typing in text fields. Through testing however, all textboxes seem to mark the event as Used. This is simply
            * here as a precaution.
            */
            if((bool)guiUtilityTextFieldInput.GetValue(null, null)) return;
            
            // Z - Set mode to Raise/Lower
            if(Shortcut.Shortcuts["Select Raise/Lower Tool"].WasExecuted(currentEvent)) {
                CurrentTool = Tool.RaiseOrLower;
                Repaint();
                currentEvent.Use();
            }
            // X - Set mode to Smooth
            else if(Shortcut.Shortcuts["Select Smooth Tool"].WasExecuted(currentEvent)) {
                CurrentTool = Tool.Smooth;
                Repaint();
                currentEvent.Use();
            }
            // C - Set mode to Set Height
            else if(Shortcut.Shortcuts["Select Set Height Tool"].WasExecuted(currentEvent)) {
                CurrentTool = Tool.SetHeight;
                Repaint();
                currentEvent.Use();
            }
            // V - Set mode to Flatten
            else if(Shortcut.Shortcuts["Select Flatten Tool"].WasExecuted(currentEvent)) {
                CurrentTool = Tool.Flatten;
                Repaint();
                currentEvent.Use();
            }
            // B - Set mode to Paint Texture
            else if(Shortcut.Shortcuts["Select Paint Texture Tool"].WasExecuted(currentEvent)) {
                CurrentTool = Tool.PaintTexture;
                Repaint();
                currentEvent.Use();
            }
            // N - Set mode to Generate
            else if(Shortcut.Shortcuts["Select Generate Tool"].WasExecuted(currentEvent)) {
                CurrentTool = Tool.Generate;
                Repaint();
                currentEvent.Use();
            }
            // M - Set mode to Settings
            else if(Shortcut.Shortcuts["Select Settings Tab"].WasExecuted(currentEvent)) {
                CurrentTool = Tool.Settings;
                Repaint();
                currentEvent.Use();
            }
            
            // Tool centric shortcuts
            if(CurrentTool == Tool.None || CurrentTool >= firstNonSculptiveTool) return;

            // Left Bracket - decrease brush size
            if(Shortcut.Shortcuts["Decrease Brush Size"].WasExecuted(currentEvent)) {
                currentToolSettings.BrushSize = Mathf.Clamp(currentToolSettings.BrushSize - terrainSize.x * settings.brushSizeIncrementMultiplier, MinBrushSize, MaxBrushSize);
                Repaint();
                currentEvent.Use();
                return;
            }
            // Right Bracket - increase brush size
            else if(Shortcut.Shortcuts["Increase Brush Size"].WasExecuted(currentEvent)) {
                currentToolSettings.BrushSize = Mathf.Clamp(currentToolSettings.BrushSize + terrainSize.x * settings.brushSizeIncrementMultiplier, MinBrushSize, MaxBrushSize);
                Repaint();
                currentEvent.Use();
                return;
            }
            // Minus - decrease brush speed
            else if(Shortcut.Shortcuts["Decrease Brush Speed"].WasExecuted(currentEvent)) {
                currentToolSettings.BrushSpeed = Mathf.Clamp(Mathf.Round((currentToolSettings.BrushSpeed - 0.1f) / 0.1f) * 0.1f, minBrushSpeed, maxBrushSpeed);
                Repaint();
                currentEvent.Use();
                return;
            }
            // Equals - increase brush speed
            else if(Shortcut.Shortcuts["Increase Brush Speed"].WasExecuted(currentEvent)) {
                currentToolSettings.BrushSpeed = Mathf.Clamp(Mathf.Round((currentToolSettings.BrushSpeed + 0.1f) / 0.1f) * 0.1f, minBrushSpeed, maxBrushSpeed);
                Repaint();
                currentEvent.Use();
                return;
            }
            // P - next brush
            else if(Shortcut.Shortcuts["Next Brush"].WasExecuted(currentEvent)) {
                IncrementSelectedBrush(1);
                Repaint();
                currentEvent.Use();
            }
            // O - previous brush
            else if(Shortcut.Shortcuts["Previous Brush"].WasExecuted(currentEvent)) {
                IncrementSelectedBrush(-1);
                Repaint();
                currentEvent.Use();
            }

            // Brush angle only applies to custom brushes
            if(CurrentBrush != null && CurrentBrush is FalloffBrush == false) {
                // 0 - reset brush angle
                if(Shortcut.Shortcuts["Reset Brush Rotation"].WasExecuted(currentEvent)) {
                    currentToolSettings.BrushAngle = 0f;
                    Repaint();
                    currentEvent.Use();
                }
                // ; - rotate brush anticlockwise
                else if(Shortcut.Shortcuts["Rotate Brush Anticlockwise"].WasExecuted(currentEvent)) {
                    currentToolSettings.BrushAngle += 2f;
                    Repaint();
                    currentEvent.Use();
                    return;
                }
                // ' - rotate brush right
                else if(Shortcut.Shortcuts["Rotate Brush Clockwise"].WasExecuted(currentEvent)) {
                    currentToolSettings.BrushAngle -= 2f;
                    Repaint();
                    currentEvent.Use();
                    return;
                }
            }

            // I - Toggle projection mode
            if(Shortcut.Shortcuts["Toggle Sculpt Onto Mode"].WasExecuted(currentEvent)) {
                settings.raycastOntoFlatPlane = !settings.raycastOntoFlatPlane;
                Repaint();
                currentEvent.Use();
            }

            // Shift+G - Flatten Terrain Shortcut
            else if(Shortcut.Shortcuts["Flatten Terrain"].WasExecuted(currentEvent)) {
                FlattenTerrain(0f);
                currentEvent.Use();
            }
        }

        private void IncrementSelectedBrush(int v) {
            if(terrainBrushesOfCurrentType.Count == 0) return;

            /**
            * Select the first brush in the list if the currently selected brush is not in the terrainBrushesOfCurrentType list because that means the
            * idea of incrementing/decrementing the brush doesn't make any sense since there's no starting point to increment/decrement from.
            */
            bool selectedBrushIsInGroup = false;
            for(int i = 0; i < terrainBrushesOfCurrentType.Count; i++) {
                if(terrainBrushesOfCurrentType[i] != brushCollection.brushes[currentToolSettings.SelectedBrushId]) continue;

                selectedBrushIsInGroup = true;
                break;
            }
            if(selectedBrushIsInGroup == false) {
                currentToolSettings.SelectedBrushId = terrainBrushesOfCurrentType[0].id;
                return;
            }

            for(int i = 0; i < terrainBrushesOfCurrentType.Count; i++) {
                if(terrainBrushesOfCurrentType[i] != brushCollection.brushes[currentToolSettings.SelectedBrushId]) continue;

                currentToolSettings.SelectedBrushId = terrainBrushesOfCurrentType[Math.Min(Math.Max(i + v, 0), terrainBrushesOfCurrentType.Count - 1)].id;
                return;
            }
        }

        private void BrushFalloffChanged() {
            ClampAnimationCurve(BrushFalloff);

            samplesDirty |= SamplesDirty.ProjectorTexture;

            if(settings.AlwaysShowBrushSelection) {
                brushCollection.UpdatePreviewTextures();
            } else {
                UpdateBrushInspectorTexture();
            }
        }

        private void ToggleSelectingBrush() {
            isSelectingBrush = !isSelectingBrush;

            // Update the brush previews if the user is now selecting brushes
            if(isSelectingBrush) {
                brushCollection.UpdatePreviewTextures();
            }
        }

        private void CurrentToolChanged(int previousValue = -2) {
            if(previousValue != -2 && (Tool)previousValue == Tool.None) Initialize(true);

            if(CurrentTool == Tool.None || CurrentTool >= firstNonSculptiveTool) {
                currentToolSettings = null;
                return;
            }

            splatPrototypes = firstTerrainData.splatPrototypes;

            currentToolSettings = settings.modeSettings[CurrentTool];

            switch(CurrentTool) {
                case Tool.PaintTexture:
                    currentToolsResolution = firstTerrainData.alphamapResolution;
                    break;
                default:
                    currentToolsResolution = heightmapResolution;
                    break;
            }

            totalToolSamplesHorizontally = currentToolsResolution * numberOfTerrainsHorizontally;
            totalToolSamplesVertically = currentToolsResolution * numberOfTerrainsVertically;
            
            foreach(TerrainInformation terrainInfo in terrainInformations) {
                terrainInfo.toolCentricXOffset = terrainInfo.gridXCoordinate * currentToolsResolution;
                terrainInfo.toolCentricYOffset = terrainInfo.gridYCoordinate * currentToolsResolution;
                if(CurrentTool != Tool.PaintTexture) {
                    terrainInfo.toolCentricXOffset -= terrainInfo.gridXCoordinate;
                    terrainInfo.toolCentricYOffset -= terrainInfo.gridYCoordinate;
                }
            }

            if(CurrentTool == Tool.PaintTexture) {
                UpdateAllAlphamapSamplesFromSourceAssets();
            } else {
                allTextureSamples = null;
            }

            brushProjector.orthographicSize = currentToolSettings.BrushSize * 0.5f;
            topPlaneGameObject.transform.localScale = new Vector3(currentToolSettings.BrushSize, currentToolSettings.BrushSize, currentToolSettings.BrushSize);
            BrushSizeInSamples = GetSegmentsFromUnits(currentToolSettings.BrushSize);

            UpdateAllNecessaryPreviewTextures();

            if(settings.brushSelectionDisplayType == BrushSelectionDisplayType.Tabbed) {
                SelectedBrushTabChanged();
            } else {
                terrainBrushesOfCurrentType = brushCollection.brushes.Values.ToList();
            }

            UpdateBrushProjectorTextureAndSamples();
        }
        
        private void SelectedBrushChanged() {
            UpdateBrushTextures();
        }

        private void RandomSpacingChanged() {
            currentToolSettings.useBrushSpacing = true;
        }

        private void RandomOffsetChanged() {
            currentToolSettings.useRandomOffset = true;
        }

        private void RandomRotationChanged() {
            currentToolSettings.useRandomRotation = true;
        }

        private void InvertBrushTextureChanged() {
            UpdateBrushTextures();

            if(settings.AlwaysShowBrushSelection) brushCollection.UpdatePreviewTextures();
        }

        private void BrushSpeedChanged() {
            samplesDirty |= SamplesDirty.BrushSamples;
        }

        private void BrushColourChanged() {
            brushProjector.material.color = settings.brushColour;
            topPlaneMaterial.color = settings.brushColour.Value * 0.9f;
        }

        private void BrushSizeChanged() {
            if(CurrentTool == Tool.None || CurrentTool >= firstNonSculptiveTool) return;

            BrushSizeInSamples = GetSegmentsFromUnits(currentToolSettings.BrushSize);

            /**
            * HACK: Another spot where objects are seemingly randomly destroyed. The top plane and projector are (seemingly) destroyed between
            * switching from one terrain with Terrain Former to another.
            */
            if(topPlaneGameObject == null || brushProjector == null) {
                CreateProjector();
            }

            topPlaneGameObject.transform.localScale = new Vector3(currentToolSettings.BrushSize, currentToolSettings.BrushSize, currentToolSettings.BrushSize);
            brushProjector.orthographicSize = currentToolSettings.BrushSize * 0.5f;

            samplesDirty |= SamplesDirty.ProjectorTexture;
        }

        private void BrushRoundnessChanged() {
            samplesDirty |= SamplesDirty.ProjectorTexture;

            UpdateAllNecessaryPreviewTextures();
        }

        private void BrushAngleDeltaChanged(float delta) {
            UpdateAllNecessaryPreviewTextures();

            brushProjector.transform.eulerAngles = new Vector3(90f, brushProjector.transform.eulerAngles.y + delta, 0f);

            samplesDirty = SamplesDirty.BrushSamples | SamplesDirty.ProjectorTexture;
        }

        private void AlwaysShowBrushSelectionValueChanged() {
            /**
            * If the brush selection should always be shown, make sure isSelectingBrush is set to false because
            * when changing to AlwaysShowBrushSelection while the brush selection was active, it will return back to
            * selecting a brush.
            */
            if(settings.AlwaysShowBrushSelection == true) {
                isSelectingBrush = false;
            }
        }
        
        private void UpdatePreviewTexturesAndBrushSamples() {
            UpdateAllNecessaryPreviewTextures();
            UpdateBrushProjectorTextureAndSamples();
        }

        private void UpdateAllNecessaryPreviewTextures() {
            if(CurrentTool == Tool.None || CurrentTool >= firstNonSculptiveTool) return;

            if(settings.AlwaysShowBrushSelection || isSelectingBrush) {
                brushCollection.UpdatePreviewTextures();
            } else {
                UpdateBrushInspectorTexture();
            }
        }
        
        private void SelectedBrushTabChanged() {
            if(CurrentTool >= firstNonSculptiveTool) return;

            UpdateCurrentBrushesOfType();
        }

        private void UpdateCurrentBrushesOfType() {
            Type typeToDisplay;
            if(string.IsNullOrEmpty(currentToolSettings.SelectedBrushTab)) {
                typeToDisplay = null;
            } else {
                typeToDisplay = terrainBrushTypes[currentToolSettings.SelectedBrushTab];
            }

            if(typeToDisplay == null) {
                terrainBrushesOfCurrentType = brushCollection.brushes.Values.ToList();
                return;
            }

            terrainBrushesOfCurrentType.Clear();

            foreach(TerrainBrush terrainBrush in brushCollection.brushes.Values) {
                if(typeToDisplay != null && terrainBrush.GetType() != typeToDisplay) continue;

                terrainBrushesOfCurrentType.Add(terrainBrush);
            }
        }

        internal void UpdateSplatPrototypes() {
            splatPrototypes = firstTerrainData.splatPrototypes;
        }

        internal void ApplySplatPrototypes() {
            for(int i = 0; i < terrainInformations.Count; i++) {
                terrainInformations[i].terrainData.splatPrototypes = splatPrototypes;
            }
        }

        /**
        * Update the heights and alphamaps every time an Undo or Redo occurs - since we must rely on storing and managing the 
        * heights data manually for better editing performance.
        */
        private void UndoRedoPerformed() {
            if(target == null) return;

            UpdateAllHeightsFromSourceAssets();

            if(CurrentTool == Tool.PaintTexture) {
                splatPrototypes = firstTerrainData.splatPrototypes;
                UpdateAllAlphamapSamplesFromSourceAssets();
            }
        }

        private void RegisterUndoForTerrainGrid(string description, bool includeAlphamapTextures = false, List<UnityEngine.Object> secondaryObjectsToUndo = null) {
            List<UnityEngine.Object> objectsToRegister = new List<UnityEngine.Object>();
            if(secondaryObjectsToUndo != null) objectsToRegister.AddRange(secondaryObjectsToUndo);

            for(int i = 0; i < terrainInformations.Count; i++) {
                objectsToRegister.Add(terrainInformations[i].terrainData);

                if(includeAlphamapTextures) {
#if UNITY_5_0_0 || UNITY_5_0_1
                    objectsToRegister.AddRange((Texture2D[])terrainDataAlphamapTexturesPropertyInfo.GetValue(terrainInformations[i].terrainData, null));
#else
                    objectsToRegister.AddRange(terrainInformations[i].terrainData.alphamapTextures);
#endif
                }
            }
            Undo.RegisterCompleteObjectUndo(objectsToRegister.ToArray(), description);
        }

        private void CreateGridPlane() {
            gridPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            gridPlane.name = "GridPlane";
            gridPlane.transform.Rotate(90f, 0f, 0f);
            gridPlane.transform.localScale = Vector3.one * 20f;
            gridPlane.hideFlags = HideFlags.HideAndDontSave;
            gridPlane.SetActive(false);

            Shader gridShader = Shader.Find("Hidden/TerrainFormer/Grid");
            if(gridShader == null) {
                Debug.LogError("Terrain Former couldn't find its grid shader.");
                return;
            }

            gridPlaneMaterial = new Material(gridShader);
            gridPlaneMaterial.mainTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(settings.mainDirectory + "Textures/Tile.psd", typeof(Texture2D));
            gridPlaneMaterial.mainTexture.wrapMode = TextureWrapMode.Repeat;
            gridPlaneMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
            gridPlaneMaterial.hideFlags = HideFlags.HideAndDontSave;
            gridPlaneMaterial.mainTextureScale = new Vector2(8f, 8f); // Set texture scale to create 8x8 tiles
            gridPlane.GetComponent<Renderer>().sharedMaterial = gridPlaneMaterial;
        }

        private void CreateProjector() {
            /**
            * Create the brush projector
            */
            brushProjectorGameObject = new GameObject("TerrainFormerProjector");
            brushProjectorGameObject.hideFlags = HideFlags.HideAndDontSave;
            
            brushProjector = brushProjectorGameObject.AddComponent<Projector>();
            brushProjector.nearClipPlane = -1000f;
            brushProjector.farClipPlane = 1000f;
            brushProjector.orthographic = true;
            brushProjector.orthographicSize = 10f;
            brushProjector.transform.Rotate(90f, 0.0f, 0.0f);

            brushProjectorMaterial = new Material(Shader.Find("Hidden/TerrainFormer/Terrain Brush Preview"));
            brushProjectorMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
            brushProjectorMaterial.hideFlags = HideFlags.HideAndDontSave;
            brushProjectorMaterial.color = settings.brushColour;
            brushProjector.material = brushProjectorMaterial;

            Texture2D outlineTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(settings.mainDirectory + "Textures/BrushOutline.png", typeof(Texture2D));
            outlineTexture.wrapMode = TextureWrapMode.Clamp;
            brushProjectorMaterial.SetTexture("_OutlineTex", outlineTexture);

            /**
            * Create the top plane
            */
            topPlaneGameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            topPlaneGameObject.name = "Top Plane";
            topPlaneGameObject.hideFlags = HideFlags.HideAndDontSave;
            DestroyImmediate(topPlaneGameObject.GetComponent<MeshCollider>());
            topPlaneGameObject.transform.Rotate(90f, 0f, 0f);

            topPlaneMaterial = new Material(Shader.Find("Hidden/TerrainFormer/BrushPlaneTop"));
            topPlaneMaterial.shader.hideFlags = HideFlags.HideAndDontSave;
            topPlaneMaterial.hideFlags = HideFlags.HideAndDontSave;
            topPlaneMaterial.color = settings.brushColour.Value * 0.9f;
            topPlaneMaterial.SetTexture("_OutlineTex", outlineTexture);

            topPlaneGameObject.GetComponent<MeshRenderer>().sharedMaterial = topPlaneMaterial;

            SetCursorEnabled(false);
        }

        private void UpdateProjector() {
            if(brushProjector == null) return;

            if(CurrentTool == Tool.None || CurrentTool >= firstNonSculptiveTool) {
                SetCursorEnabled(false);
                return;
            }

            Vector3 position;
            if(GetMousePositionInWorldSpace(out position)) {
                // Always make sure the projector is positioned as high as necessary
                brushProjectorGameObject.transform.position = position + new Vector3(0f, firstTerrainData.size.y + 1f, 0f); 
                brushProjectorGameObject.SetActive(true);

                if(CurrentTool == Tool.Flatten) {
                    topPlaneGameObject.SetActive(position.y >= MinHeightDifferenceToShowTopPlane);
                    topPlaneGameObject.transform.position = new Vector3(position.x, position.y, position.z);
                } else if(CurrentTool == Tool.SetHeight) {
                    topPlaneGameObject.SetActive(settings.setHeight >= MinHeightDifferenceToShowTopPlane);
                    topPlaneGameObject.transform.position = new Vector3(position.x, settings.setHeight, position.z);
                } else {
                    topPlaneGameObject.SetActive(false);
                }
            } else {
                SetCursorEnabled(false);
            }

            HandleUtility.Repaint();
        }

        private void UpdateBrushTextures() {
            UpdateBrushInspectorTexture();
            UpdateBrushProjectorTextureAndSamples();
        }

        private void UpdateBrushProjectorTextureAndSamples() {
            lastTimeBrushSamplesWereUpdated = EditorApplication.timeSinceStartup;
            
            CurrentBrush.UpdateSamplesAndMainTexture(brushSizeInSamples);

            // HACK: Projector objects are destroyed (seemingly randomly), so recreate them if necessary
            if(brushProjectorGameObject == null || brushProjectorMaterial == null) {
                CreateProjector();
            }

            brushProjectorMaterial.mainTexture = brushProjectorTexture;
            topPlaneMaterial.mainTexture = brushProjectorTexture;

            if(currentCommand != null) {
                currentCommand.brushSamples = BrushSamplesWithSpeed;
            }
        }

        private void UpdateBrushInspectorTexture() {
            CurrentBrush.CreatePreviewTexture();
        }

        internal void DeleteSplatTexture(int indexToDelete) {
            RegisterUndoForTerrainGrid("Delete Splat Texture", true);
            
            int allTextureSamplesHorizontally = allTextureSamples.GetLength(0);
            int allTextureSamplesVertically = allTextureSamples.GetLength(1);
            int textureCount = allTextureSamples.GetLength(2);
            int newTextureCount = textureCount - 1;

            float[,,] oldTextureSamples = new float[allTextureSamplesVertically, allTextureSamplesHorizontally, textureCount];
            Array.Copy(allTextureSamples, oldTextureSamples, allTextureSamples.Length);
            
            // Duplicate the alphamaps array, except the part of the 3rd dimension whose index is the one to be deleted
            allTextureSamples = new float[allTextureSamplesVertically, allTextureSamplesHorizontally, newTextureCount];
            
            for(int x = 0; x < allTextureSamplesHorizontally; x++) {
                for(int y = 0; y < allTextureSamplesVertically; y++) {
                    for(int l = 0; l < indexToDelete; l++) {
                        allTextureSamples[y, x, l] = oldTextureSamples[y, x, l];
                    }
                    for(int l = indexToDelete + 1; l < textureCount; l++) {
                        allTextureSamples[y, x, l - 1] = oldTextureSamples[y, x, l];
                    }
                }
            }
            
            for(int x = 0; x < allTextureSamplesHorizontally; x++) {
                for(int y = 0; y < allTextureSamplesVertically; y++) {
                    float sum = 0f;

                    for(int l = 0; l < newTextureCount; l++) {
                        sum += allTextureSamples[y, x, l];
                    }

                    if(sum >= 0.01f) {
                        float sumCoefficient = 1f / sum;
                        for(int l = 0; l < newTextureCount; l++) {
                            allTextureSamples[y, x, l] *= sumCoefficient;
                        }
                    } else {
                        for(int l = 0; l < newTextureCount; l++) {
                            allTextureSamples[y, x, l] = l != 0 ? 0f : 1f;
                        }
                    }
                }
            }

            List<SplatPrototype> splatPrototypesList = new List<SplatPrototype>(splatPrototypes);
            splatPrototypesList.RemoveAt(indexToDelete);
            splatPrototypes = splatPrototypesList.ToArray();
            ApplySplatPrototypes();
            UpdateAllAlphamapSamplesInSourceAssets();
        }
        
#region GlobalTerrainModifications
        private void CreateRampCurve(float maxHeight) {
            RegisterUndoForTerrainGrid("Created Linear Ramp");
            
            float heightCoefficient = maxHeight / terrainSize.y;
            float height;
            for(int x = 0; x < totalHeightmapSamplesHorizontally; x++) {
                height = settings.generateRampCurve.Evaluate((float)x / totalHeightmapSamplesHorizontally) * heightCoefficient;
                for(int y = 0; y < totalHeightmapSamplesVertically; y++) {
                    allTerrainHeights[y, x] = height;
                }
            }
            
            UpdateAllHeightsInSourceAssets();
        }

        
        private void CreateCircularRampCurve(float maxHeight) {
            RegisterUndoForTerrainGrid("Created Circular Ramp");
            
            float heightCoefficient = maxHeight / terrainSize.y;
            float halfTotalTerrainSize = Mathf.Min(totalHeightmapSamplesHorizontally, totalHeightmapSamplesVertically) * 0.5f;
            float distance;
            for(int x = 0; x < totalHeightmapSamplesHorizontally; x++) {
                for(int y = 0; y < totalHeightmapSamplesVertically; y++) {
                    distance = CalculateDistance(x, y, halfTotalTerrainSize, halfTotalTerrainSize);
                    allTerrainHeights[y, x] = settings.generateRampCurve.Evaluate(1f - (distance / halfTotalTerrainSize)) * heightCoefficient;
                }
            }
            
            UpdateAllHeightsInSourceAssets();
        }
        
        private void FlattenTerrain(float setHeight) {
            RegisterUndoForTerrainGrid("Flatten Terrain");

            for(int x = 0; x < totalHeightmapSamplesHorizontally; x++) {
                for(int y = 0; y < totalHeightmapSamplesVertically; y++) {
                    allTerrainHeights[y, x] = setHeight;
                }
            }

            // Create the silly array that we must send every terrain as we don't have any other choice
            float[,] newHeights = new float[heightmapResolution, heightmapResolution];
            for(int x = 0; x < heightmapResolution; x++) {
                for(int y = 0; y < heightmapResolution; y++) {
                    newHeights[x, y] = setHeight;
                }
            }

            foreach(TerrainInformation ti in terrainInformations) {
                ti.terrainData.SetHeights(0, 0, newHeights);
            }
        }
        
        private void SmoothAll() {
            RegisterUndoForTerrainGrid("Smooth All");
            
            float[,] newHeights = new float[totalHeightmapSamplesHorizontally, totalHeightmapSamplesVertically];

            float heightSum;
            int neighbourCount, positionX, positionY;

            float totalOperations = settings.smoothingIterations * totalHeightmapSamplesHorizontally;
            float currentOperation = 0;

            for(int i = 0; i < settings.smoothingIterations; i++) {
                for(int x = 0; x < totalHeightmapSamplesHorizontally; x++) {
                    currentOperation++;

                    // Only update the progress bar every width segment, otherwise it will be called way too many times
                    if(EditorUtility.DisplayCancelableProgressBar("Smooth All", "Smoothing entire terrain…", currentOperation / totalOperations) == true) {
                        EditorUtility.ClearProgressBar();
                        return;
                    }

                    for(int y = 0; y < totalHeightmapSamplesVertically; y++) {
                        heightSum = 0f;
                        neighbourCount = 0;

                        for(int x2 = -settings.boxFilterSize; x2 <= settings.boxFilterSize; x2++) {
                            positionX = x + x2;
                            if(positionX < 0 || positionX >= totalHeightmapSamplesHorizontally) continue;
                            for(int y2 = -settings.boxFilterSize; y2 <= settings.boxFilterSize; y2++) {
                                positionY = y + y2;
                                if(positionY < 0 || positionY >= totalHeightmapSamplesVertically) continue;

                                heightSum += allTerrainHeights[positionY, positionX];
                                neighbourCount++;
                            }
                        }

                        newHeights[y, x] = heightSum / neighbourCount;
                    }
                }

                allTerrainHeights = newHeights;
            }

            EditorUtility.ClearProgressBar();

            UpdateAllHeightsInSourceAssets();
        }

        private void ResetAllCachedHeightmapSamples() {
            for(int x = 0; x < totalHeightmapSamplesHorizontally; x++) {
                for(int y = 0; y < totalHeightmapSamplesVertically; y++) {
                    allTerrainHeights[y, x] = 0f;
                }
            }
        }

        private void ImportHeightmap() {
            TextureImporter heightmapTextureImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(heightmapTexture));
            if(heightmapTextureImporter.isReadable == false) {
                heightmapTextureImporter.isReadable = true;
                heightmapTextureImporter.SaveAndReimport();
            }

            float uPosition = 0f;
            float vPosition;
            Color bilinearSample;
            const float oneThird = 1f / 3f;
            for(int x = 0; x < totalHeightmapSamplesHorizontally; x++) {
                for(int y = 0; y < totalHeightmapSamplesVertically; y++) {
                    uPosition = (float)x / totalHeightmapSamplesHorizontally;
                    vPosition = (float)y / totalHeightmapSamplesVertically;
                    if(settings.heightmapSourceIsAlpha) {
                        allTerrainHeights[y, x] = heightmapTexture.GetPixelBilinear(uPosition, vPosition).a;
                    } else {
                        bilinearSample = heightmapTexture.GetPixelBilinear(uPosition, vPosition);
                        allTerrainHeights[y, x] = (bilinearSample.r + bilinearSample.g + bilinearSample.b) * oneThird;
                    }
                }

                if(EditorUtility.DisplayCancelableProgressBar("Terrain Former", "Applying heightmap to terrain", uPosition * 0.9f)) {
                    EditorUtility.ClearProgressBar();
                    return;
                }
            }
            UpdateAllHeightsInSourceAssets();

            EditorUtility.ClearProgressBar();
        }
#endregion

        // If there have been changes to a given terrain in Terrain Former, don't reimport its heights on OnAssetsImported.
        private void OnWillSaveAssets(string[] assetPaths) {
            if(settings != null) settings.Save();

            foreach(TerrainInformation ti in terrainInformations) {
                foreach(string assetPath in assetPaths) {
                    if(ti.terrainAssetPath != assetPath || ti.hasChangedSinceLastSave) continue;

                    ti.ignoreOnAssetsImported = true;
                    ti.hasChangedSinceLastSave = false;
                }
            }
        }
        
        private void OnAssetsImported(string[] assetPaths) {
            List<string> customBrushPaths = new List<string>();
            
            foreach(string path in assetPaths) {
                /**
                * Check if the terrain has been modified externally. If this terrain's path matches this any terrain grid terrain,
                * update the heights array.
                */
                foreach(TerrainInformation terrainInformation in terrainInformations) {
                    if(terrainInformation.ignoreOnAssetsImported) {
                        terrainInformation.ignoreOnAssetsImported = false;
                        continue;
                    }

                    if(terrainInformation.terrainData == null) continue;
                    
                    float[,] temporaryHeights;
                    if(terrainInformation.terrainAssetPath == path && terrainInformation.terrainData.heightmapResolution == heightmapResolution) {
                        temporaryHeights = terrainInformation.terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);
                        for(int x = 0; x < heightmapResolution; x++) {
                            for(int y = 0; y < heightmapResolution; y++) {
                                allTerrainHeights[terrainInformation.heightmapYOffset + y, terrainInformation.heightmapXOffset + x] = temporaryHeights[y, x];
                            }
                        }
                    }
                    /**
                    * If there are custom textures that have been update, keep a list of which onces have changed and update the brushCollection.
                    */
                    // TODO: Shouldn't we be verifying they actually are brushes?
                    else if(path.StartsWith(BrushCollection.localCustomBrushPath)) {
                        customBrushPaths.Add(path);
                    }
                }
            }

            if(customBrushPaths.Count > 0) {
                brushCollection.RefreshCustomBrushes(customBrushPaths.ToArray());
                brushCollection.UpdatePreviewTextures();
                UpdateCurrentBrushesOfType();
            }
        }
        
        // Check if the terrain asset has been moved.
        private void OnAssetsMoved(string[] sourcePaths, string[] destinationPaths) {
            for(int i = 0; i < sourcePaths.Length; i++) {
                foreach(TerrainInformation terrainInfo in terrainInformations) {
                    if(sourcePaths[i] == terrainInfo.terrainAssetPath) {
                        terrainInfo.terrainAssetPath = destinationPaths[i];
                    }
                }
            }
        }

        private void OnAssetsDeleted(string[] paths) {
            List<string> deletedCustomBrushPaths = new List<string>();

            foreach(string path in paths) {
                if(path.StartsWith(BrushCollection.localCustomBrushPath)) {
                    deletedCustomBrushPaths.Add(path);
                }
            }

            if(deletedCustomBrushPaths.Count > 0) {
                brushCollection.RemoveDeletedBrushes(deletedCustomBrushPaths.ToArray());
                brushCollection.UpdatePreviewTextures();
                UpdateCurrentBrushesOfType();
            }
        }

#region Utlities
        private static Keyframe[] curveAKeys;
        private static Keyframe[] curveBKeys;

        private static bool AreAnimationCurvesEqual(AnimationCurve curveA, AnimationCurve curveB) {
            curveAKeys = curveA.keys;
            curveBKeys = curveB.keys;

            if(curveAKeys.Length != curveBKeys.Length) return false;
            
            for(int i = 0; i < curveAKeys.Length; i++) {
                if(curveAKeys[i].inTangent != curveBKeys[i].inTangent) return false;
                if(curveAKeys[i].outTangent != curveBKeys[i].outTangent) return false;
                if(curveAKeys[i].tangentMode != curveBKeys[i].tangentMode) return false;
                if(curveAKeys[i].time != curveBKeys[i].time) return false;
                if(curveAKeys[i].value != curveBKeys[i].value) return false;
            }

            return true;
        }

        // Clamp the falloff curve's values from time 0-1 and value 0-1
        private static void ClampAnimationCurve(AnimationCurve curve) {
            for(int i = 0; i < curve.keys.Length; i++) {
                Keyframe keyframe = curve.keys[i];
                curve.MoveKey(i, new Keyframe(Mathf.Clamp01(keyframe.time), Mathf.Clamp01(keyframe.value), keyframe.inTangent, keyframe.outTangent));
            }
        }
                
        /**
        * A modified version of the LinePlaneIntersection method from the 3D Math Functions script found on the Unify site 
        * Credit to Bit Barrel Media: http://wiki.unity3d.com/index.php?title=3d_Math_functions
        * This code has been modified to fit my needs and coding style
        * Get the intersection between a line and a plane. 
        * If the line and plane are not parallel, the function outputs true, otherwise false.
        */
        private bool LinePlaneIntersection(out Vector3 intersectingPoint) {
            Vector3 planePoint = new Vector3(0f, lastClickPosition.y, 0f);

            Ray mouseRay = HandleUtility.GUIPointToWorldRay(mousePosition);

            // Calculate the distance between the linePoint and the line-plane intersection point
            float dotNumerator = Vector3.Dot((planePoint - mouseRay.origin), Vector3.up);
            float dotDenominator = Vector3.Dot(mouseRay.direction, Vector3.up);

            // Check if the line and plane are not parallel
            if(dotDenominator != 0.0f) {
                float length = dotNumerator / dotDenominator;

                // Create a vector from the linePoint to the intersection point and set the vector length by normalizing and multiplying by the length
                Vector3 vector = Vector3.Normalize(mouseRay.direction) * length;

                // Get the coordinates of the line-plane intersection point
                intersectingPoint = mouseRay.origin + vector;

                return true;
            } else {
                intersectingPoint = Vector3.zero;
                return false;
            }
        }

        // Checks if the cursor is hovering over the terrain
        private bool Raycast() {
            RaycastHit hitInfo;
            foreach(TerrainInformation terrainInformation in terrainInformations) {
                if(terrainInformation.collider.Raycast(HandleUtility.GUIPointToWorldRay(mousePosition), out hitInfo, float.PositiveInfinity)) {
                    return true;
                }
            }
            return false;
        }

        // Checks if the cursor is hovering over the terrain
        private bool Raycast(out Vector3 pos, out Vector2 uv) {
            RaycastHit hitInfo;
            foreach(TerrainInformation terrainInformation in terrainInformations) {
                if(terrainInformation.collider.Raycast(HandleUtility.GUIPointToWorldRay(mousePosition), out hitInfo, float.PositiveInfinity)) {
                    pos = hitInfo.point;
                    uv = hitInfo.textureCoord;
                    return true;
                }
            }
            pos = Vector3.zero;
            uv = Vector2.zero;
            return false;
        }

        private static float CalculateDistance(float x1, float y1, float x2, float y2) {
            float deltaX = x1 - x2;
            float deltaY = y1 - y2;

            float magnitude = deltaX * deltaX + deltaY * deltaY;

            return Mathf.Sqrt(magnitude);
        }

        internal void UpdateSetHeightAtMousePosition() {
            float height;
            if(GetTerrainHeightAtMousePosition(out height)) {
                settings.setHeight = height;
                Repaint();
            }
        }

        private bool GetTerrainHeightAtMousePosition(out float height) {
            RaycastHit hitInfo;
            foreach(TerrainInformation terrainInformation in terrainInformations) {
                if(terrainInformation.collider.Raycast(HandleUtility.GUIPointToWorldRay(mousePosition), out hitInfo, float.PositiveInfinity)) {
                    height = hitInfo.point.y - terrainInformation.transform.position.y;
                    return true;
                }
            }
            
            height = 0f;
            return false;
        }

        /**
        * Gets the mouse position in world space. This is a utlity method used to automatically get the position of 
        * the mouse depending on if it's being held down or not. Returns true if the terrain or plane was hit, 
        * returns false otherwise.
        */
        private bool GetMousePositionInWorldSpace(out Vector3 position) {
            // If the user is sampling height while in Set Height with Shift, only use a Raycast.
            if(mouseIsDown && CurrentTool == Tool.SetHeight && Event.current.shift) {
                Vector2 uv;
                if(Raycast(out position, out uv) == false) {
                    SetCursorEnabled(false);
                    return false;
                }
            // SetHeight and Flatten tools will always use plane projection.
            } else if(mouseIsDown && (settings.raycastOntoFlatPlane || CurrentTool == Tool.SetHeight || CurrentTool == Tool.Flatten)) {
                if(LinePlaneIntersection(out position) == false) {
                    SetCursorEnabled(false);
                    return false;
                }
            } else {
                Vector2 uv;
                if(Raycast(out position, out uv) == false) {
                    SetCursorEnabled(false);
                    return false;
                }
            }

            return true;
        }

        private void SetCursorEnabled(bool enabled) {
            brushProjectorGameObject.SetActive(enabled);
            topPlaneGameObject.SetActive(enabled);
        }

        private int GetSegmentsFromUnits(float units) {
            float segmentDensity = currentToolsResolution / terrainSize.x;

            return Mathf.RoundToInt(units * segmentDensity);
        }
        
        internal void UpdateAllAlphamapSamplesFromSourceAssets() {
            allTextureSamples = new float[firstTerrainData.alphamapHeight * numberOfTerrainsVertically, firstTerrainData.alphamapWidth * numberOfTerrainsHorizontally, firstTerrainData.alphamapLayers];
            float[,,] currentAlphamapSamples = new float[firstTerrainData.alphamapWidth, firstTerrainData.alphamapHeight, firstTerrainData.alphamapLayers];

            foreach(TerrainInformation terrainInfo in terrainInformations) {
                currentAlphamapSamples = terrainInfo.terrainData.GetAlphamaps(0, 0, firstTerrainData.alphamapWidth, firstTerrainData.alphamapHeight);

                for(int l = 0; l < firstTerrainData.alphamapLayers; l++) {
                    for(int x = 0; x < firstTerrainData.alphamapWidth; x++) {
                        for(int y = 0; y < firstTerrainData.alphamapHeight; y++) {
                            allTextureSamples[terrainInfo.alphamapsYOffset + y, terrainInfo.alphamapsXOffset + x, l] =
                                currentAlphamapSamples[y, x, l];
                        }
                    }
                }
            }
        }

        private void UpdateAllAlphamapSamplesInSourceAssets() {
            float[,,] newAlphamaps = new float[alphamapResolution, alphamapResolution, firstTerrainData.alphamapLayers];

            foreach(TerrainInformation terrainInfo in terrainInformations) {
                for(int l = 0; l < firstTerrainData.alphamapLayers; l++) {
                    for(int x = 0; x < alphamapResolution; x++) {
                        for(int y = 0; y < alphamapResolution; y++) {
                            newAlphamaps[x, y, l] = allTextureSamples[x + terrainInfo.alphamapsXOffset, y + terrainInfo.alphamapsYOffset, l];
                        }
                    }
                }
                terrainInfo.terrainData.SetAlphamaps(0, 0, newAlphamaps);
            }
        }

        private void UpdateAllHeightsFromSourceAssets() {
            float[,] temporaryHeights;
            foreach(TerrainInformation terrainInformation in terrainInformations) {
                temporaryHeights = terrainInformation.terrainData.GetHeights(0, 0, heightmapResolution, heightmapResolution);
                for(int x = 0; x < heightmapResolution; x++) {
                    for(int y = 0; y < heightmapResolution; y++) {
                        allTerrainHeights[terrainInformation.heightmapYOffset + y, terrainInformation.heightmapXOffset + x] = temporaryHeights[y, x];
                    }
                }
            }
        }

        private void UpdateAllHeightsInSourceAssets() {
            float[,] temporaryHeights = new float[heightmapResolution, heightmapResolution];
            foreach(TerrainInformation terrainInformation in terrainInformations) {
                for(int x = 0; x < heightmapResolution; x++) {
                    for(int y = 0; y < heightmapResolution; y++) {
                        temporaryHeights[y, x] = allTerrainHeights[y + terrainInformation.heightmapYOffset, x + terrainInformation.heightmapXOffset];
                    }
                }

                terrainInformation.terrainData.SetHeights(0, 0, temporaryHeights);
            }
        }

        private void UpdateAllUnmodifiedHeights() {
            allUnmodifiedTerrainHeights = (float[,])allTerrainHeights.Clone();
        }
#endregion
    }
}