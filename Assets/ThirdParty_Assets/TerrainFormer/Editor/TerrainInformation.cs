using UnityEditor;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class TerrainInformation {
        internal Transform transform;
        internal Terrain terrain;
        internal TerrainData terrainData;
        internal TerrainCollider collider;
        internal string terrainAssetPath;
        internal CommandCoordinates paintInfo;
        internal int gridXCoordinate, gridYCoordinate; // Co-ordinates of the terrain in respect to a terrain grid
        internal int heightmapXOffset, heightmapYOffset;
        internal int alphamapsXOffset, alphamapsYOffset;
        internal int toolCentricXOffset, toolCentricYOffset; // The samples' offsets based on the current tool selected
        internal bool hasChangedSinceLastSetHeights = false;
        internal bool hasChangedSinceLastSave = false;
        internal bool ignoreOnAssetsImported = false;

        internal TerrainInformation(Terrain terrain) {
            this.terrain = terrain;
            transform = terrain.transform;
            collider = transform.GetComponent<TerrainCollider>();
            terrainData = terrain.terrainData;
            
            terrainAssetPath = AssetDatabase.GetAssetPath(terrainData);
        }
    }

    internal class CommandCoordinates {
        /**
        * Clipped refers to the brush area (how many units have been clipped on a given side, or the spans taking into account clipping from 
        * the brush hanging off edge(s) of the terrain.
        */
        public int clippedLeft, clippedBottom, clippedWidth, clippedHeight, worldLeft, worldBottom;

        public CommandCoordinates(int clippedLeft, int clippedBottom, int clippedWidth, int clippedHeight, int worldLeft, int worldBottom) {
            this.clippedLeft = clippedLeft;
            this.clippedBottom = clippedBottom;
            this.clippedWidth = clippedWidth;
            this.clippedHeight = clippedHeight;
            this.worldLeft = worldLeft;
            this.worldBottom = worldBottom;
        }

        public override string ToString() {
            return string.Format("Clipped (left: {0}, bottom: {1}, width: {2}, height: {3}), offset (left: {4}, bottom: {5})", clippedLeft, clippedBottom, 
                clippedWidth, clippedHeight, worldLeft, worldBottom);
        }
    }
}
