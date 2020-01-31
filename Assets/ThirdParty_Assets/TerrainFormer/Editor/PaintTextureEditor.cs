using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class PaintTextureEditor : EditorWindow {
        private const float defaultWidth = 280f;
        private const float defaultHeight = 245f;

#if !UNITY_5_0_0
        private static MethodInfo hasAlphaTextureFormatMethod;
#endif

        private int selectedTextureIndex;
        
        private Texture2D mainTexture;
        private Texture2D normalMap;
        private Vector2 tileSize;
        private Vector2 tileOffset;
        private Color specularColour;
        private float metallicness;
        
        /**
        * NOTE: Release notes of Unity 5.0.1:
        *   Terrain: Removed 'Override Smoothness' option from terrain settings. A new per-splat smoothness property is available when the splat texture has no alpha channel.
        */
#if !UNITY_5_0_0
        private float smoothness;
#endif

        private GUIStyle centeredLabel;

        private bool isAddingNewSplatPrototype = false;

#if !UNITY_5_0_0
        static PaintTextureEditor() {
            Type textureUtil = Assembly.GetAssembly(typeof(Editor)).GetType("UnityEditor.TextureUtil");
            hasAlphaTextureFormatMethod = textureUtil.GetMethod("HasAlphaTextureFormat", BindingFlags.Static | BindingFlags.Public);
        }
#endif

        private static PaintTextureEditor InitializeWindow() {
            PaintTextureEditor paintTextureEditor = GetWindow<PaintTextureEditor>(true, "Edit Texture", true);
            paintTextureEditor.minSize = new Vector2(defaultWidth, defaultHeight);
            paintTextureEditor.maxSize = new Vector2(defaultWidth, defaultHeight);
            return paintTextureEditor;
        }

        public static void CreateAndShowForAdditions() {
            PaintTextureEditor paintTextureEditor = InitializeWindow();
            paintTextureEditor.isAddingNewSplatPrototype = true;
            paintTextureEditor.tileSize = new Vector2(1f, 1f);
        }

        public static void CreateAndShow(int selectedTextureIndex) {
            PaintTextureEditor paintTextureEditor = InitializeWindow();
            
            paintTextureEditor.selectedTextureIndex = selectedTextureIndex;
            
            paintTextureEditor.mainTexture = TerrainFormerEditor.splatPrototypes[selectedTextureIndex].texture;
            paintTextureEditor.normalMap = TerrainFormerEditor.splatPrototypes[selectedTextureIndex].normalMap;
            paintTextureEditor.tileSize = TerrainFormerEditor.splatPrototypes[selectedTextureIndex].tileSize;
            paintTextureEditor.tileOffset = TerrainFormerEditor.splatPrototypes[selectedTextureIndex].tileOffset;
            paintTextureEditor.specularColour = TerrainFormerEditor.splatPrototypes[selectedTextureIndex].specular;
            paintTextureEditor.metallicness = TerrainFormerEditor.splatPrototypes[selectedTextureIndex].metallic;


#if !UNITY_5_0_0
            paintTextureEditor.smoothness = TerrainFormerEditor.splatPrototypes[selectedTextureIndex].smoothness;
#endif

            paintTextureEditor.Show();
        }

        void OnGUI() {
            // If the terrain data is gone, automatically close this window
            if(TerrainFormerEditor.Instance == null || TerrainFormerEditor.splatPrototypes == null || TerrainFormerEditor.splatPrototypes.Length < selectedTextureIndex) {
                EditorGUILayout.HelpBox("Terrain Former is not currently active or there are no splat prototypes to work with.", MessageType.Warning);
                return;
            }

            if(centeredLabel == null) {
                centeredLabel = new GUIStyle(GUI.skin.label);
                centeredLabel.alignment = TextAnchor.MiddleCenter;
            }

            EditorGUIUtility.labelWidth = Mathf.Clamp(55f + Screen.width * 0.1f, 65f, 95f);

            GUILayout.BeginVertical(GUIStyle.none);

            /**
            * Main/Albedo/Diffuse Texture
            */
            Rect mainTextureLabelRect = new Rect(0f, 2f, Screen.width * 0.5f, 29f);
            GUI.Label(mainTextureLabelRect, "Albedo (RGB)\nSmoothness (A)", centeredLabel);

            Rect mainTextureRect = new Rect(Screen.width * 0.25f - 32f, 33f, 64f, 64f);
            mainTexture = (Texture2D)EditorGUI.ObjectField(mainTextureRect, GUIContent.none, mainTexture, typeof(Texture2D), false);

            /**
            * Normal Texture
            */
            Rect normalLabelRect = new Rect(Screen.width * 0.5f, 2f, Screen.width * 0.5f, 29f);
            GUI.Label(normalLabelRect, "Normal", centeredLabel);

            Rect normalTextureRect = new Rect(Screen.width * 0.75f - 32f, 33f, 64f, 64f);
            normalMap = (Texture2D)EditorGUI.ObjectField(normalTextureRect, GUIContent.none, normalMap, typeof(Texture2D), false);

            GUILayout.Space(102f);

            tileSize = EditorGUILayout.Vector2Field("Tile Size", tileSize);
            tileOffset = EditorGUILayout.Vector2Field("Tile Offset", tileOffset);
            metallicness = EditorGUILayout.Slider("Metallic", metallicness, 0f, 1f);
#if !UNITY_5_0_0
            if(mainTexture != null && (bool)hasAlphaTextureFormatMethod.Invoke(null, new object[] { mainTexture.format }) == false) {
                smoothness = EditorGUILayout.Slider("Smoothness", smoothness, 0f, 1f);
            }
#endif

            GUILayout.Space(10f);

            using(new GUIUtilities.GUIEnabledBlock(ValidateMainTexture())) {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if(GUILayout.Button("OK", new GUILayoutOption[] { GUILayout.Width(75f), GUILayout.Height(22f) })) {
                    Apply();
                    Close();
                    if(TerrainFormerEditor.Instance != null) TerrainFormerEditor.Instance.Repaint();
                }
                if(GUILayout.Button("Cancel", new GUILayoutOption[] { GUILayout.Width(75f), GUILayout.Height(22f) })) {
                    Close();
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            if(Event.current.type != EventType.Layout && Event.current.type != EventType.Used) {
                Rect lastRect = GUILayoutUtility.GetLastRect();
                minSize = new Vector2(defaultWidth, lastRect.height + 5f);
                maxSize = new Vector2(defaultWidth, lastRect.height + 5f);
            }
        }

        private void Apply() {
            SplatPrototype splatPrototype;
            if(isAddingNewSplatPrototype) {
                // Make a copy of the splatPrototypes array since the array won't be allowed to be resized if it's a property like terrainData.splatPrototypes
                Array.Resize(ref TerrainFormerEditor.splatPrototypes, TerrainFormerEditor.splatPrototypes.Length + 1);
                splatPrototype = new SplatPrototype();
            } else {
                splatPrototype = TerrainFormerEditor.splatPrototypes[selectedTextureIndex];
            }
            splatPrototype.texture = mainTexture;
            splatPrototype.normalMap = normalMap;
            splatPrototype.metallic = metallicness;
#if !UNITY_5_0_0
            splatPrototype.smoothness = smoothness;
#endif
            splatPrototype.specular = specularColour;
            splatPrototype.tileOffset = tileOffset;
            splatPrototype.tileSize = tileSize;

            if(isAddingNewSplatPrototype) {
                TerrainFormerEditor.splatPrototypes[TerrainFormerEditor.splatPrototypes.Length - 1] = splatPrototype;   
            }

            TerrainFormerEditor.Instance.ApplySplatPrototypes();
            TerrainFormerEditor.Instance.UpdateAllAlphamapSamplesFromSourceAssets();
        }

        private StringBuilder invalidationDescription;
        private bool ValidateMainTexture() {
            if(mainTexture == null) { 
                EditorGUILayout.HelpBox("A main texture must be assigned.", MessageType.Warning);
                return false;
            }

            bool isValid = true;
            if(mainTexture.wrapMode != TextureWrapMode.Repeat || mainTexture.width != Mathf.ClosestPowerOfTwo(mainTexture.width) || mainTexture.height != Mathf.ClosestPowerOfTwo(mainTexture.height) ||
                mainTexture.mipmapCount <= 1) {
                isValid = false;
                invalidationDescription = new StringBuilder();
            }
            
            if(mainTexture.wrapMode != TextureWrapMode.Repeat) {
                invalidationDescription.AppendLine("  • The main texture must have wrap mode set to \"Repeat\".");
            }
            if(mainTexture.width != Mathf.ClosestPowerOfTwo(mainTexture.width) || mainTexture.height != Mathf.ClosestPowerOfTwo(mainTexture.height)) {
                invalidationDescription.AppendLine("  • The main texture's size must be a power of two (eg, 512x512, 1024x1024).");
            }
            if(mainTexture.mipmapCount <= 1) {
                invalidationDescription.AppendLine("  • The main texture must have mipmaps.");
            }

            if(isValid == false) {
                invalidationDescription.Insert(0, "The following issues must be resolved in order to apply any changes:\n");

                GUIUtilities.ActionableHelpBox(invalidationDescription.ToString(), MessageType.Warning, () => {
                    if(GUILayout.Button("Fix All", new GUILayoutOption[] { GUILayout.Width(70f), GUILayout.Height(20f) })) {
                        TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mainTexture));
                        textureImporter.wrapMode = TextureWrapMode.Repeat;

#if UNITY_5_5_OR_NEWER
                        textureImporter.npotScale = TextureImporterNPOTScale.ToNearest;
#else
                        if(textureImporter.textureType == TextureImporterType.Advanced) {
                            textureImporter.npotScale = TextureImporterNPOTScale.ToNearest;
                        } else if(textureImporter.textureType != TextureImporterType.Image) {
                            textureImporter.textureType = TextureImporterType.Image;
                        }
#endif

                        textureImporter.mipmapEnabled = true;

                        textureImporter.SaveAndReimport();
                    }
                });
            }

            return isValid;
        }
    }
}