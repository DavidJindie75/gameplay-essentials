using System;
using System.Collections.Generic;
using System.IO;
using TinyJSON;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class Settings : BaseSettings {
        [Exclude]
        internal string path;

        [Include]
        internal Dictionary<Tool, ModeSettings> modeSettings;

        private static readonly Color brushColourDefault = new Color(0.2f, 0.6980392f, 1f, 0.6980392f);
        internal SavedColor brushColour;

        // Generate
        [Exclude]
        internal AnimationCurve generateRampCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 0f, 1f));
        [Include]
        internal FauxKeyframe[] generateRampCurveFaux;
        [Include]
        internal float generateHeight = 5f;

        // Flatten
        [Include]
        internal FlattenMode flattenMode = FlattenMode.Flatten;

        // Set Height
        [Include]
        internal float setHeight = 10f;

        // Smooth
        [Include]
        internal int boxFilterSize = 3;
        [Include]
        internal int smoothingIterations = 1;
        
        // Paint Texture
        internal float targetOpacity = 1f;
        internal int selectedTextureIndex = 0;

        // Paint Details
        internal int selectedDetailIndex = 0;

        // Place Trees
        internal int selectedTreeIndex = 0;

        // Heightmap
        internal bool heightmapSourceIsAlpha = false;

        [Include]
        internal string mainDirectory; // The path to the main folder of Terrain Former
        
        public static Settings Create(string path) {
            TerrainFormerEditor.exceptionUponLoadingSettings = false;
            Settings newSettings = new Settings();
            newSettings.modeSettings = new Dictionary<Tool, ModeSettings>();
            
            if(File.Exists(path)) {
                try {
                    JSON.MakeInto(JSON.Load(File.ReadAllText(path)), out newSettings);
                } catch(Exception e) {
                    TerrainFormerEditor.exceptionUponLoadingSettings = true;
                    Debug.LogError(e.InnerException);
                    return null;
                }

                // Most (if not all) malformed JSON problems don't raise an exception
                if(newSettings == null) return null;
            }
            
            // Add the following mode settings as needed.
            if(newSettings.modeSettings.ContainsKey(Tool.RaiseOrLower) == false) {
                newSettings.modeSettings = new Dictionary<Tool, ModeSettings>();
                newSettings.modeSettings.Add(Tool.RaiseOrLower, new ModeSettings());
            }

            if(newSettings.modeSettings.ContainsKey(Tool.Smooth) == false) {
                newSettings.modeSettings.Add(Tool.Smooth, new ModeSettings());
                newSettings.modeSettings[Tool.Smooth].BrushSpeed = 2f;
            }

            if(newSettings.modeSettings.ContainsKey(Tool.SetHeight) == false) {
                newSettings.modeSettings.Add(Tool.SetHeight, new ModeSettings());
                newSettings.modeSettings[Tool.SetHeight].BrushSpeed = 2f;
            }

            if(newSettings.modeSettings.ContainsKey(Tool.Flatten) == false) {
                newSettings.modeSettings.Add(Tool.Flatten, new ModeSettings());
                newSettings.modeSettings[Tool.Flatten].BrushSpeed = 2f;
            }

            if(newSettings.modeSettings.ContainsKey(Tool.PaintTexture) == false) {
                newSettings.modeSettings.Add(Tool.PaintTexture, new ModeSettings());
            }
                        
            newSettings.path = path;
            newSettings.brushColour = new SavedColor("TerrainFormer/BrushColour", brushColourDefault);

            return newSettings;
        }

        public void Save() {
            try {
                // If the the setting's directory doesn't exist, return since we assume this means that Terrain Former has been moved.
                if(Directory.Exists(Path.GetDirectoryName(path)) == false) return;

                string json = JSON.Dump(this, EncodeOptions.PrettyPrint);
                File.WriteAllText(path, json);
            } catch(Exception e) {
                Debug.LogError(e.Message);
            }
        }

        internal override bool AreSettingsDefault() {
            return brushColour.Value == brushColourDefault && base.AreSettingsDefault();
        }

        internal override void RestoreDefaultSettings() {
            brushColour.Value = brushColourDefault;
            base.RestoreDefaultSettings();
        }

        [BeforeEncode]
        public void BeforeEncode() {
            // Update all fake represnetations of keyframes for serialization
            generateRampCurveFaux = CopyKeyframesToFauxKeyframes(generateRampCurve.keys);
            foreach(ModeSettings brushSetting in modeSettings.Values) {
                brushSetting.brushFalloffFauxFrames = CopyKeyframesToFauxKeyframes(brushSetting.brushFalloff.keys);
            }
        }

        /**
        * NOTE: If there is a new AnimationCurve that's not been saved yet, there will likely be a NullReferenceException. In the 
        * future we need to check for this, otherwise shipping the update to users will result in them being forced to delete their
        * settings file or to manually update it themselves.
        */
        [AfterDecode]
        public void AfterDecode() {
            // Copy all fake representations of keyframes and change them into AnimationCurves
            generateRampCurve = new AnimationCurve(CopyFauxKeyframesToKeyframes(generateRampCurveFaux));
            foreach(ModeSettings brushSetting in modeSettings.Values) {
                brushSetting.brushFalloff = new AnimationCurve(CopyFauxKeyframesToKeyframes(brushSetting.brushFalloffFauxFrames));
            }
        }
        
        private static FauxKeyframe[] CopyKeyframesToFauxKeyframes(Keyframe[] keyframes) {
            FauxKeyframe[] newKeyframes = new FauxKeyframe[keyframes.Length];
            for(int i = 0; i < keyframes.Length; i++) {
                newKeyframes[i] = new FauxKeyframe(keyframes[i]);
            }
            return newKeyframes;
        }

        private static Keyframe[] CopyFauxKeyframesToKeyframes(FauxKeyframe[] fauxKeyframes) {
            Keyframe[] newKeyframes = new Keyframe[fauxKeyframes.Length];
            for(int i = 0; i < fauxKeyframes.Length; i++) {
                newKeyframes[i] = new Keyframe(fauxKeyframes[i].time, fauxKeyframes[i].value);
                newKeyframes[i].inTangent = fauxKeyframes[i].inTangent;
                newKeyframes[i].outTangent = fauxKeyframes[i].outTangent;
                newKeyframes[i].tangentMode = fauxKeyframes[i].tangentMode;
            }
            return newKeyframes;
        }
    }

    internal class FauxKeyframe {
        public float inTangent;
        public float outTangent;
        public int tangentMode;
        public float time;
        public float value;

        public FauxKeyframe() { }

        public FauxKeyframe(Keyframe keyframe) {
            time = keyframe.time;
            value = keyframe.value;
            tangentMode = keyframe.tangentMode;
            inTangent = keyframe.inTangent;
            outTangent = keyframe.outTangent;
        }
    }
}