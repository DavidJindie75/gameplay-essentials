using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class AssetWatcher : AssetPostprocessor {
        public static Action<string[]> OnAssetsImported;
        public static Action<string[], string[]> OnAssetsMoved;
        public static Action<string[]> OnAssetsDeleted;
        public static Action<string[]> OnWillSaveAssetsAction;
        
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssetsDestination, string[] movedAssetsSource) {
            if(OnAssetsImported != null && importedAssets != null && importedAssets.Length != 0) {
                OnAssetsImported(importedAssets);
            }

            if(OnAssetsMoved != null && movedAssetsSource != null && movedAssetsSource.Length != 0) {
                OnAssetsMoved(movedAssetsSource, movedAssetsDestination);
            }

            if(OnAssetsDeleted != null && deletedAssets != null && deletedAssets.Length != 0) {
                OnAssetsDeleted(deletedAssets);
            }
        }

        private static string[] OnWillSaveAssets(string[] paths) {
            if(OnWillSaveAssetsAction != null) {
                OnWillSaveAssetsAction(paths);
            }

            return paths;
        }

        private void OnPreprocessTexture() {
            // Return if the BrushCollection hasn't been initialized prior to this method being called
            if(string.IsNullOrEmpty(BrushCollection.localCustomBrushPath)) return;

            if(assetPath.StartsWith(BrushCollection.localCustomBrushPath, StringComparison.Ordinal)) {
                TextureImporter textureImporter = (TextureImporter)assetImporter;
#if UNITY_5_5_OR_NEWER
                if(textureImporter.isReadable == false || textureImporter.wrapMode != TextureWrapMode.Clamp || textureImporter.textureCompression != TextureImporterCompression.Uncompressed) {
                    textureImporter.isReadable = true;
                    textureImporter.wrapMode = TextureWrapMode.Clamp;
                    textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                }
#else
                if(textureImporter.textureType != TextureImporterType.Advanced || textureImporter.isReadable == false ||
                    textureImporter.wrapMode != TextureWrapMode.Clamp || textureImporter.textureFormat != TextureImporterFormat.AutomaticTruecolor) 
                {
                    textureImporter.textureType = TextureImporterType.Advanced;
                    textureImporter.isReadable = true;
                    textureImporter.wrapMode = TextureWrapMode.Clamp;
                    textureImporter.textureFormat = TextureImporterFormat.AutomaticTruecolor;
                }
#endif
            }
        }
    }
}
