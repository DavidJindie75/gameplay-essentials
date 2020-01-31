using System;
using System.Reflection;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal static class Utilities {
        internal static GameObject DuplicateTerrain(string name, Terrain sourceTerrain, TerrainData sourceTerrainData) {
            int detailResolutionPerPatch = TerrainSettings.GetDetailResolutionPerPatch(sourceTerrainData);

            int sourceheightmapResolution = sourceTerrainData.heightmapResolution;

            GameObject destinationTerrainGameObject = Terrain.CreateTerrainGameObject(null);

            destinationTerrainGameObject.name = name;
            if(name == sourceTerrain.name) {
                destinationTerrainGameObject.name += " (Copy)";
            }

            Terrain destinationTerrain = destinationTerrainGameObject.GetComponent<Terrain>();
            TerrainCollider terrainCollider = destinationTerrainGameObject.GetComponent<TerrainCollider>();
            TerrainData destinationTerrainData = new TerrainData();

            // Paint Texture
            destinationTerrainData.alphamapResolution = sourceTerrainData.alphamapResolution;
            destinationTerrainData.splatPrototypes = sourceTerrainData.splatPrototypes;
            destinationTerrainData.SetAlphamaps(0, 0, sourceTerrainData.GetAlphamaps(0, 0, sourceTerrainData.alphamapWidth, sourceTerrainData.alphamapHeight));

            // Trees
            destinationTerrainData.treePrototypes = sourceTerrainData.treePrototypes;
            destinationTerrainData.treeInstances = sourceTerrainData.treeInstances;

            // Details
            destinationTerrainData.SetDetailResolution(sourceTerrainData.detailResolution, detailResolutionPerPatch);
            destinationTerrainData.detailPrototypes = sourceTerrainData.detailPrototypes;
            for(int d = 0; d < sourceTerrainData.detailPrototypes.Length; d++) {
                destinationTerrainData.SetDetailLayer(0, 0, d, sourceTerrainData.GetDetailLayer(0, 0, sourceTerrainData.detailWidth, sourceTerrainData.detailHeight, d));
            }

            // Base Terrain
            destinationTerrain.drawHeightmap = sourceTerrain.drawHeightmap;
            destinationTerrain.heightmapPixelError = sourceTerrain.heightmapPixelError;
            destinationTerrain.basemapDistance = sourceTerrain.basemapDistance;
            destinationTerrain.castShadows = sourceTerrain.castShadows;
            destinationTerrain.materialType = sourceTerrain.materialType;
            destinationTerrain.reflectionProbeUsage = sourceTerrain.reflectionProbeUsage;
            destinationTerrainData.thickness = sourceTerrainData.thickness;

            // Tree & Detail Objects
            destinationTerrain.drawTreesAndFoliage = sourceTerrain.drawTreesAndFoliage;
            destinationTerrain.bakeLightProbesForTrees = sourceTerrain.bakeLightProbesForTrees;
            destinationTerrain.detailObjectDistance = sourceTerrain.detailObjectDistance;
            destinationTerrain.collectDetailPatches = sourceTerrain.collectDetailPatches;
            destinationTerrain.detailObjectDensity = sourceTerrain.detailObjectDensity;
            destinationTerrain.treeDistance = sourceTerrain.treeDistance;
            destinationTerrain.treeBillboardDistance = sourceTerrain.treeBillboardDistance;
            destinationTerrain.treeCrossFadeLength = sourceTerrain.treeCrossFadeLength;
            destinationTerrain.treeMaximumFullLODCount = sourceTerrain.treeMaximumFullLODCount;

            // Wind Settings for Grass
            destinationTerrainData.wavingGrassStrength = sourceTerrainData.wavingGrassStrength;
            destinationTerrainData.wavingGrassSpeed = sourceTerrainData.wavingGrassSpeed;
            destinationTerrainData.wavingGrassAmount = sourceTerrainData.wavingGrassAmount;
            destinationTerrainData.wavingGrassTint = sourceTerrainData.wavingGrassTint;

            // Resolution
            destinationTerrainData.heightmapResolution = sourceTerrainData.heightmapResolution;
            destinationTerrainData.baseMapResolution = sourceTerrainData.baseMapResolution;
            destinationTerrainData.size = sourceTerrainData.size;
            destinationTerrainData.SetHeights(0, 0, sourceTerrainData.GetHeights(0, 0, sourceheightmapResolution, sourceheightmapResolution));

            destinationTerrain.terrainData = destinationTerrainData;
            terrainCollider.terrainData = destinationTerrainData;

            return destinationTerrainGameObject;
        }

        internal static Vector2 RotatePointAroundPoint(Vector2 point, Vector2 pivotPoint, float angle, float sineOfAngle, float cosineOfAngle) {
            point -= pivotPoint;

            return new Vector2((point.x * cosineOfAngle - point.y * sineOfAngle) + pivotPoint.x, (point.x * sineOfAngle + point.y * cosineOfAngle) + pivotPoint.y);
        }
        
        internal static int RoundToNearestAndClamp(int currentNumber, int desiredNearestNumber, int minimum, int maximum) {
            int roundedNumber = Mathf.RoundToInt((float)currentNumber / desiredNearestNumber) * desiredNearestNumber;
            return Math.Min(Math.Max(roundedNumber, minimum), maximum);
        }
    }
}
