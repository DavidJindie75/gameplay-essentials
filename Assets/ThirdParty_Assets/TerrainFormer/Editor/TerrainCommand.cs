using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace JesseStiller.TerrainFormerExtension {
    internal abstract class TerrainCommand {
        protected abstract string Name { get; }
        
        internal float[,] brushSamples;

        private List<Object> objectsToRegisterForUndo = new List<Object>();

        internal TerrainCommand(float[,] brushSamples) {
            this.brushSamples = brushSamples;
        }
        
        internal void Execute(Event currentEvent, CommandCoordinates terrainGridCommandCoordinates) {
            if(terrainGridCommandCoordinates == null) return;
            if(this is TexturePaintCommand && TerrainFormerEditor.splatPrototypes.Length == 0) return;

            objectsToRegisterForUndo.Clear();
            foreach(TerrainInformation terrainInformation in TerrainFormerEditor.Instance.terrainInformations) {
                if(terrainInformation.paintInfo == null) continue;

                objectsToRegisterForUndo.Add(terrainInformation.terrainData);

                if(this is TexturePaintCommand) {
#if UNITY_5_0_0 || UNITY_5_0_1
                    objectsToRegisterForUndo.AddRange((Texture2D[])TerrainFormerEditor.terrainDataAlphamapTexturesPropertyInfo.GetValue(terrainInformation.terrainData, null));
#else
                    objectsToRegisterForUndo.AddRange(terrainInformation.terrainData.alphamapTextures);
#endif
                }
            }

            if(objectsToRegisterForUndo.Count == 0) return;
            
            Undo.RegisterCompleteObjectUndo(objectsToRegisterForUndo.ToArray(), Name);

            foreach(TerrainInformation terrainInformation in TerrainFormerEditor.Instance.terrainInformations) {
                if(terrainInformation.paintInfo == null) continue;

                terrainInformation.hasChangedSinceLastSetHeights = true;
                terrainInformation.hasChangedSinceLastSave = true;
            }
            int globalTerrainX, globalTerrainY;
            float brushSample;
            // OnControlClick
            if(currentEvent.control) {
                for(int x = 0; x < terrainGridCommandCoordinates.clippedWidth; x++) {
                    for(int y = 0; y < terrainGridCommandCoordinates.clippedHeight; y++) {
                        brushSample = brushSamples[x + terrainGridCommandCoordinates.clippedLeft, y + terrainGridCommandCoordinates.clippedBottom];
                        if(brushSample == 0f) continue;

                        globalTerrainX = x + terrainGridCommandCoordinates.worldLeft;
                        globalTerrainY = y + terrainGridCommandCoordinates.worldBottom;

                        OnControlClick(globalTerrainX, globalTerrainY, brushSample);
                    }
                }
            } 
            // OnShiftClick and OnShiftClickDown
            else if(currentEvent.shift) {
                OnShiftClickDown();
                for(int x = 0; x < terrainGridCommandCoordinates.clippedWidth; x++) {
                    for(int y = 0; y < terrainGridCommandCoordinates.clippedHeight; y++) {
                        brushSample = brushSamples[x + terrainGridCommandCoordinates.clippedLeft, y + terrainGridCommandCoordinates.clippedBottom];
                        if(brushSample == 0f) continue;

                        globalTerrainX = x + terrainGridCommandCoordinates.worldLeft;
                        globalTerrainY = y + terrainGridCommandCoordinates.worldBottom;

                        OnShiftClick(globalTerrainX, globalTerrainY, brushSample);
                    }
                }
            } 
            // OnClick
            else {
                for(int x = 0; x < terrainGridCommandCoordinates.clippedWidth; x++) {
                    for(int y = 0; y < terrainGridCommandCoordinates.clippedHeight; y++) {
                        brushSample = brushSamples[x + terrainGridCommandCoordinates.clippedLeft, y + terrainGridCommandCoordinates.clippedBottom];
                        if(brushSample == 0f) continue;

                        globalTerrainX = x + terrainGridCommandCoordinates.worldLeft;
                        globalTerrainY = y + terrainGridCommandCoordinates.worldBottom;

                        OnClick(globalTerrainX, globalTerrainY, brushSample);
                    }
                }
            }
        }

        protected abstract void OnClick(int globalX, int globalY, float brushSample);
        protected abstract void OnShiftClick(int globalX, int globalY, float brushSample);
        protected abstract void OnShiftClickDown();
        protected abstract void OnControlClick(int globalX, int globalY, float brushSample);
    }
}