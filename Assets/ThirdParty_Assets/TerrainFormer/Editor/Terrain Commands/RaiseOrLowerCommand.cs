using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class RaiseOrLowerCommand : TerrainCommand {
        private const float controlClickSpeed = 0.005f;

        private const string name = "Raise/Lower";
        protected override string Name {
            get { return name; }
        }

        internal RaiseOrLowerCommand(float[,] brushSamples) : base(brushSamples) { }

        protected override void OnClick(int x, int y, float brushSample) {
            TerrainFormerEditor.allTerrainHeights[y, x] = TerrainFormerEditor.allTerrainHeights[y, x] + brushSample * 0.01f;
        }

        protected override void OnControlClick(int x, int y, float brushSample) {
            TerrainFormerEditor.allTerrainHeights[y, x] = Mathf.Clamp01(TerrainFormerEditor.allUnmodifiedTerrainHeights[y, x] + brushSample * -TerrainFormerEditor.Instance.CurrentTotalMouseDelta * controlClickSpeed);
        }
        
        protected override void OnShiftClick(int x, int y, float brushSample) {
            TerrainFormerEditor.allTerrainHeights[y, x] = Mathf.Clamp01(TerrainFormerEditor.allTerrainHeights[y, x] - brushSample * 0.01f);
        }

        protected override void OnShiftClickDown() { }
    }
}