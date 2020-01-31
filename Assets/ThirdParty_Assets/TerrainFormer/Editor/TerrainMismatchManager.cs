using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;
using UnityEditor;

// TODO: Some kind of smart thing needs to be done to shrink down this class.
namespace JesseStiller.TerrainFormerExtension {
    internal class TerrainMismatchManager {
        private List<TerrainInformation> terrainInformations;
        
        private bool heightmapResolutionsAreIdentical;
        private int heightmapResolution = 1025;
        
        private int[] terrainIndexesWithSplatPrototypes;
        private string[] terrainNamesWithSplatPrototypes;
        private bool splatPrototypesAreIdentical;
        private int splatPrototypesIndex = -1;
        private Texture2D[] splatPrototypePreviews;
        
        internal bool IsMismatched { get; private set; }
        
        internal void Initialize(List<TerrainInformation> terrainInfoAndStates) {
            if(IsMismatched) return;
            
            this.terrainInformations = terrainInfoAndStates;
            IsMismatched = false;
            splatPrototypesAreIdentical = true;
            heightmapResolutionsAreIdentical = true;
            
            TerrainData terrainData = terrainInfoAndStates[0].terrainData;
            heightmapResolution = terrainData.heightmapResolution;
            SplatPrototype[] otherSplatPrototypes = terrainData.splatPrototypes;
            
            for(int i = 1; i < terrainInfoAndStates.Count; i++) {
                terrainData = terrainInfoAndStates[i].terrainData;
                // Heightmap Resolution check
                if(heightmapResolutionsAreIdentical && heightmapResolution != terrainData.heightmapResolution) {
                    SetMismatch(ref heightmapResolutionsAreIdentical);
                }
                
                // Splat Prototypes check
                if(splatPrototypesAreIdentical) {
                    if(otherSplatPrototypes.Length != terrainData.splatPrototypes.Length) {
                        SetMismatch(ref splatPrototypesAreIdentical);
                    } else {
                        // TODO: Mismatches with splat prototype parameters should have its own dialog
                        for(int s = 0; s < otherSplatPrototypes.Length; s++) {
                            if(otherSplatPrototypes[s].metallic != terrainData.splatPrototypes[s].metallic || otherSplatPrototypes[s].normalMap != terrainData.splatPrototypes[s].normalMap ||
                                otherSplatPrototypes[s].specular != terrainData.splatPrototypes[s].specular || otherSplatPrototypes[s].texture != terrainData.splatPrototypes[s].texture || 
                                otherSplatPrototypes[s].tileOffset != terrainData.splatPrototypes[s].tileOffset || otherSplatPrototypes[s].tileSize != terrainData.splatPrototypes[s].tileSize
#if !UNITY_5_0_0
                                || otherSplatPrototypes[s].smoothness != terrainData.splatPrototypes[s].smoothness) {
#else
                                ) {
#endif
                                SetMismatch(ref splatPrototypesAreIdentical);
                                break;
                            }
                        }
                    }
                }
            }
            
            List<string> terrainNamesWithSplatPrototypesList = new List<string>();
            List<int> terrainIndexesWithSplatPrototypesList = new List<int>();
            for(int i = 0; i < terrainInfoAndStates.Count; i++) {
                string terrainName = terrainInfoAndStates[i].terrain.name;
                terrainData = terrainInfoAndStates[i].terrainData;
                if(terrainData.splatPrototypes.Length != 0) {
                    terrainIndexesWithSplatPrototypesList.Add(i);
                    terrainNamesWithSplatPrototypesList.Add(terrainName);

                    if(splatPrototypesIndex == -1) {
                        splatPrototypesIndex = i;
                    }
                }
            }

            terrainNamesWithSplatPrototypes = terrainNamesWithSplatPrototypesList.ToArray();
            terrainIndexesWithSplatPrototypes = terrainIndexesWithSplatPrototypesList.ToArray();
            
            UpdateSplatPrototypesPreviews();
        }

        private void SetMismatch(ref bool paramater) {
            paramater = false;
            IsMismatched = true;
        }

        private void UpdateSplatPrototypesPreviews() {
            if(splatPrototypesIndex == -1) return;

            splatPrototypePreviews = new Texture2D[terrainInformations[splatPrototypesIndex].terrainData.splatPrototypes.Length];
            Texture2D splatTexture;
            for(int i = 0; i < splatPrototypePreviews.Length; i++) {
                splatTexture = terrainInformations[splatPrototypesIndex].terrainData.splatPrototypes[i].texture;
                splatPrototypePreviews[i] = AssetPreview.GetAssetPreview(splatTexture) ?? splatTexture;
            }
        }
        
        internal void Draw() {
            if(IsMismatched == false) return;
            GUIUtilities.ActionableHelpBox("There are differences between the terrains in the current terrain grid which must be fixed before sculpting and painting is allowed.", MessageType.Warning, 
                () => {
                    EditorGUILayout.LabelField("Terrain Grid Settings", EditorStyles.boldLabel);
                    if(heightmapResolutionsAreIdentical == false) {
                        heightmapResolution = EditorGUILayout.IntPopup(TerrainSettings.heightmapResolutionContent, heightmapResolution, TerrainSettings.heightmapResolutionsContents, TerrainSettings.heightmapResolutions);
                    }
                    
                    if(splatPrototypesAreIdentical == false) {
                        int newIndex = EditorGUILayout.IntPopup("Splat Prototypes", splatPrototypesIndex, terrainNamesWithSplatPrototypes, terrainIndexesWithSplatPrototypes);
                        if(newIndex != splatPrototypesIndex) {
                            splatPrototypesIndex = newIndex;
                            UpdateSplatPrototypesPreviews();
                        }
                        DrawPreviewGrid(splatPrototypePreviews);
                    }
                    EditorGUILayout.Space();
                    if(GUILayout.Button("Apply to Terrain Grid")) {
                        Apply();
                    }
                }
            );
        }
        
        private void Apply() {
            List<TerrainData> allModifiedTerrainDatas = new List<TerrainData>();
            for(int i = 0; i < terrainInformations.Count; i++) {
                if(terrainInformations[i].terrainData.heightmapResolution == heightmapResolution && 
                    splatPrototypesAreIdentical) continue;
                allModifiedTerrainDatas.Add(terrainInformations[i].terrainData);
            }
            Undo.RegisterCompleteObjectUndo(allModifiedTerrainDatas.ToArray(), "Fixed terrain grid settings mismatch");

            Vector3 originalSize = terrainInformations[0].terrainData.size;

            for(int i = 0; i < allModifiedTerrainDatas.Count; i++) {
                if(heightmapResolutionsAreIdentical == false) {
                    allModifiedTerrainDatas[i].heightmapResolution = heightmapResolution;
                    allModifiedTerrainDatas[i].size = originalSize; // Unity changes the size if the heightmapResolution has changed
                }
                if(splatPrototypesAreIdentical == false) {
                    allModifiedTerrainDatas[i].splatPrototypes = allModifiedTerrainDatas[splatPrototypesIndex].splatPrototypes;
                }
            }
            
            IsMismatched = false;
            splatPrototypesAreIdentical = true;
            heightmapResolutionsAreIdentical = true;

            TerrainFormerEditor.Instance.OnEnable();
        }

        private void DrawPreviewGrid(Texture2D[] previews) {
            float size = 70f;
            int columnsPerRow = Mathf.FloorToInt((Screen.width - 30f) / size);
            int rows = Math.Max(Mathf.CeilToInt((float)previews.Length / columnsPerRow), 1);
            int currentRow = 0;
            int currentColumn = 0;
            GUI.BeginGroup(GUILayoutUtility.GetRect(Screen.width - 42f, rows * size), GUI.skin.box);
            for(int i = 0; i < previews.Length; i++) {
                GUI.DrawTexture(new Rect(currentColumn * 67f + 3f, currentRow * 64f + 3f, 64f, 64f), previews[i]);
                if(++currentColumn >= columnsPerRow) {
                    currentColumn = 0;
                    currentRow++;
                }
            }
            GUI.EndGroup();
        }
    }
}
