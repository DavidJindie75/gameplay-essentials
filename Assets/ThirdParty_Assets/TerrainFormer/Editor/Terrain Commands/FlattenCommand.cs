using System;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class FlattenCommand : TerrainCommand {
        public FlattenMode mode;
        public float flattenHeight;

        private const string name = "Flatten";
        protected override string Name {
            get { return name; }
        }

        internal FlattenCommand(float[,] brushSamples) : base(brushSamples) { }

        protected override void OnClick(int x, int y, float brushSample) {
            if((mode == FlattenMode.Flatten && TerrainFormerEditor.allTerrainHeights[y, x] <= flattenHeight) || 
                (mode == FlattenMode.Extend && TerrainFormerEditor.allTerrainHeights[y, x] >= flattenHeight)) {
                return;
            }
            
            TerrainFormerEditor.allTerrainHeights[y, x] = Mathf.Clamp01(TerrainFormerEditor.allTerrainHeights[y, x] + (flattenHeight - TerrainFormerEditor.allTerrainHeights[y, x]) * brushSample * 0.5f);
        }

        protected override void OnControlClick(int x, int y, float brushSample) {
            switch(mode) {
                case FlattenMode.Flatten:
                    if(TerrainFormerEditor.allTerrainHeights[y, x] < flattenHeight)
                        return;
                    break;
                case FlattenMode.Extend:
                    if(TerrainFormerEditor.allTerrainHeights[y, x] > flattenHeight)
                        return;
                    break;
            }
            TerrainFormerEditor.allTerrainHeights[y, x] = Mathf.Clamp01(Mathf.Lerp(TerrainFormerEditor.allUnmodifiedTerrainHeights[y, x], flattenHeight, -TerrainFormerEditor.Instance.CurrentTotalMouseDelta * brushSample * 0.02f));
        }

        protected override void OnShiftClick(int globalX, int globalY, float brushSample) { }

        protected override void OnShiftClickDown() { }
    }
}