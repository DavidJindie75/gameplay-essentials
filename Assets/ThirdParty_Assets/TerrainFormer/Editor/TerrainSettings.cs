using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal static class TerrainSettings {
        internal static readonly GUIContent alphamapResolutionContent = new GUIContent("Alphamap Resolution", "Sets the resolution of the \"splatmap\" that controls the blending of the different terrain textures.");
        internal static readonly int[] powersOfTwoFrom16To2048 = new int[] { 16, 32, 64, 128, 256, 512, 1024, 2048 };
        internal static readonly GUIContent[] powersOfTwoFrom16To2048GUIContents = new GUIContent[] {
            new GUIContent("16"), new GUIContent("32"), new GUIContent("64"), new GUIContent("128"), new GUIContent("256"), new GUIContent("512"), new GUIContent("1024"), new GUIContent("2048")
        };

        internal static readonly GUIContent heightmapResolutionContent = new GUIContent("Heightmap Resolution", "Sets the number of points across each axis used to represent varying heights.");
        internal static readonly int[] heightmapResolutions = new int[] { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
        internal static readonly GUIContent[] heightmapResolutionsContents = new GUIContent[] {
            new GUIContent("33"), new GUIContent("65"), new GUIContent("129"), new GUIContent("257"), new GUIContent("513"), new GUIContent("1025"), new GUIContent("2049"), new GUIContent("4097")
        };

        internal static readonly GUIContent nameGUIContent = new GUIContent("Parent Name", "Sets the name of the parent object that will have all of the created terrain objects as children.");
        internal static readonly GUIContent terrainTemplateGUIContent = new GUIContent("Terrain Template", "Sets an optional existing terrain object that will be used as a basis for all terrain settings. This includes the paint textures, trees, details, and other terrain settings.");
        internal static readonly GUIContent detailResolutionContent = new GUIContent("Detail Resolution", "Sets the resolution of the map that determines the separate patches of details/grass. Higher resolutions give smaller and more detailed patches.");
        internal static readonly GUIContent detailResolutionPerPatchContent = new GUIContent("Detail Resolution per Patch", "Sets the length/width of the square of patches renderered with a single draw call.");
        internal static readonly GUIContent controlTextureResolutionContent = new GUIContent("Control Texture Resolution", "Sets the resolution of the \"splatmap\" that controls the blending of the different terrain textures.");
        internal static readonly GUIContent basemapDistanceContent = new GUIContent("Basemap Distance", "Sets the maximum distance at which terrain textures will be displayed at full resolution. Beyond this distance, a lower-resolution composite image will be used for efficiency.");
        internal static readonly GUIContent baseTextureResolutionContent = new GUIContent("Base Texture Resolution", "Sets the resolution of the composite texture used on the terrain when viewed from a distance greater than the basemap distance.");
        
        internal static readonly GUIContent baseMapResolutionContent = new GUIContent("Basemap Resolution", "Sets the resolution of the composite texture used on the terrain when viewed from a distance greater than the basemap distance.");

        private static MethodInfo shaderUtilHasTangentChannelMethodInfo;
        private static PropertyInfo detailResolutionPerPatchPropertyInfo;
        static TerrainSettings() {
            detailResolutionPerPatchPropertyInfo = typeof(TerrainData).GetProperty("detailResolutionPerPatch", BindingFlags.Instance | BindingFlags.NonPublic);
            shaderUtilHasTangentChannelMethodInfo = typeof(ShaderUtil).GetMethod("HasTangentChannel", BindingFlags.Static | BindingFlags.NonPublic);
        }
        
        internal static bool ShaderHasTangentChannel(Shader shader) {
            return (bool)shaderUtilHasTangentChannelMethodInfo.Invoke(null, new object[] { shader });
        }

        internal static int GetDetailResolutionPerPatch(TerrainData instance) {
            return (int)detailResolutionPerPatchPropertyInfo.GetValue(instance, null);
        }
    }
}