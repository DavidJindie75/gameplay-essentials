using System;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class SmoothCommand : TerrainCommand {
        private const float controlClickSpeed = 0.015f;
        
        private int boxFilterSize, totalTerrainWidth, totalTerrainHeight;

        private const string name = "Smooth";
        protected override string Name {
            get { return name; }
        }
        
        internal SmoothCommand(float[,] brushSamples, int boxFilterSize, int totalTerrainWidth, int totalTerrainHeight) : base(brushSamples) {
            this.boxFilterSize = boxFilterSize;
            this.totalTerrainWidth = totalTerrainWidth;
            this.totalTerrainHeight = totalTerrainHeight;
        }
        
        protected override void OnClick(int x, int y, float brushSample) {
            float heightSum = 0f;
            int neighbourCount = 0;
            int positionX, positionY;

            for(int x2 = -boxFilterSize; x2 <= boxFilterSize; x2++) {
                positionX = x + x2;
                if(positionX < 0 || positionX >= totalTerrainWidth) continue;

                for(int y2 = -boxFilterSize; y2 <= boxFilterSize; y2++) {
                    positionY = y + y2;
                    if(positionY < 0 || positionY >= totalTerrainHeight) continue;

                    heightSum += TerrainFormerEditor.allTerrainHeights[positionY, positionX];
                    neighbourCount++;
                }
            }

            /**
            * Apply the smoothed height by performing the following:
            * 1) Get the current height that is being smoothed
            * 2) Calculated the average by dividing neighbourCount by the heightSum
            * 3) Get the difference between the average value and the current value
            * 4) Multiply the difference by the terrain brush samples
            * 5) Add the result onto the existing height value
            *
            * By calculating the difference and multiplying it by a coefficient (the brush samples), this elimates the need for
            * a Lerp function, and makes the smoothing itself a bit quicker.
            */
            TerrainFormerEditor.allTerrainHeights[y, x] -= (TerrainFormerEditor.allTerrainHeights[y, x] - (heightSum / neighbourCount)) * brushSample;
        }
        
        protected override void OnControlClick(int x, int y, float brushSample) { }

        protected override void OnShiftClickDown() { }

        protected override void OnShiftClick(int globalX, int globalY, float brushSample) { }
    }
}