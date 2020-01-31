using System;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class TexturePaintCommand : TerrainCommand {
        private const string name = "Texture Paint";
        protected override string Name {
            get { return name; }
        }

        internal TexturePaintCommand(float[,] brushSamples) : base(brushSamples) { }

        protected override void OnClick(int globalX, int globalY, float brushSample) {
            int selectedTextureIndex = TerrainFormerEditor.settings.selectedTextureIndex;
            int layerCount = TerrainFormerEditor.allTextureSamples.GetLength(2);

            // Apply paint to currently selected texture type
            if(TerrainFormerEditor.settings.targetOpacity > brushSample) {
                TerrainFormerEditor.allTextureSamples[globalY, globalX, selectedTextureIndex] =
                    Mathf.Min(TerrainFormerEditor.allTextureSamples[globalY, globalX, selectedTextureIndex] + brushSample, TerrainFormerEditor.settings.targetOpacity);
            } else {
                TerrainFormerEditor.allTextureSamples[globalY, globalX, selectedTextureIndex] =
                    Mathf.Max(TerrainFormerEditor.allTextureSamples[globalY, globalX, selectedTextureIndex] - brushSample, TerrainFormerEditor.settings.targetOpacity);
            }

            float sum = 0f;
            for(int l = 0; l < layerCount; l++) {
                if(l != selectedTextureIndex) sum += TerrainFormerEditor.allTextureSamples[globalY, globalX, l];
            }

            if(sum > 0.01f) {
                float sumCoefficient = (1f - TerrainFormerEditor.allTextureSamples[globalY, globalX, selectedTextureIndex]) / sum;
                for(int l = 0; l < layerCount; ++l) {
                    if(l != selectedTextureIndex) TerrainFormerEditor.allTextureSamples[globalY, globalX, l] *= sumCoefficient;
                }
            } else {
                for(int l = 0; l < layerCount; ++l) {
                    TerrainFormerEditor.allTextureSamples[globalY, globalX, l] = l != selectedTextureIndex ? 0f : 1f;
                }
            }
        }

        protected override void OnControlClick(int globalX, int globalY, float brushSample) { }

        protected override void OnShiftClick(int globalX, int globalY, float brushSample) { }

        protected override void OnShiftClickDown() { }
    }
}
