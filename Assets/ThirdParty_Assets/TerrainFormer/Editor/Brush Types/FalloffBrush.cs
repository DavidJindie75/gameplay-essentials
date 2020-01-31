using UnityEditor;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class FalloffBrush : TerrainBrush {
        private const int typeSortOrder = 0;
        private const string prettyTypeName = "Falloff";
        private static Texture2D typeIcon;
        
        public FalloffBrush(string name, string id) {
            this.name = name;
            this.id = id;
        }
        
        internal override Texture2D GetTypeIcon() {
            if(typeIcon != null) return typeIcon;

            typeIcon = (Texture2D)AssetDatabase.LoadAssetAtPath(TerrainFormerEditor.texturesDirectory + "Icons/LinearProceduralBrushIcon.psd", typeof(Texture2D));

            return typeIcon;
        }
        
        internal override float[,] GenerateTextureSamples(int pixelsPerAxis) {
            float[,] samples = GenerateFalloff(pixelsPerAxis);
            bool invertBrush = TerrainFormerEditor.Instance.currentToolSettings.InvertBrushTexture || TerrainFormerEditor.settings.invertBrushTexturesGlobally;
            if(invertBrush == false) {
                return samples;
            }

            for(int x = 0; x < pixelsPerAxis; x++) {
                for(int y = 0; y < pixelsPerAxis; y++) {
                    samples[x, y] = 1f - samples[x, y];
                }
            }
            return samples;
        }
    }
}