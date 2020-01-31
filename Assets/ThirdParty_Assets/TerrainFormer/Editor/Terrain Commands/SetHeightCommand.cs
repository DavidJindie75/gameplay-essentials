using System;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class SetHeightCommand : TerrainCommand {
        internal float normalizedHeight;

        private const string name = "Set Height";
        protected override string Name {
            get { return name; }
        }

        internal SetHeightCommand(float[,] brushSamples) : base(brushSamples) { }

        protected override void OnClick(int x, int y, float brushSample) {
            TerrainFormerEditor.allTerrainHeights[y, x] += (normalizedHeight - TerrainFormerEditor.allTerrainHeights[y, x]) * brushSample * 0.5f;
        }
        
        protected override void OnControlClick(int x, int y, float brushSample) {
            TerrainFormerEditor.allTerrainHeights[y, x] = Mathf.Clamp01(Mathf.Lerp(TerrainFormerEditor.allUnmodifiedTerrainHeights[y, x], normalizedHeight, 
                -TerrainFormerEditor.Instance.CurrentTotalMouseDelta * brushSample * 0.02f));
        }

        protected override void OnShiftClick(int globalX, int globalY, float brushSample) { }
        
        protected override void OnShiftClickDown() {
            TerrainFormerEditor.Instance.UpdateSetHeightAtMousePosition();
        }
    }
}