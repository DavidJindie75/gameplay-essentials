using System;
using TinyJSON;
using UnityEngine;

namespace JesseStiller.TerrainFormerExtension {
    internal class ModeSettings {
        public static Action BrushSizeChanged;
        public static Action BrushSpeedChanged;
        public static Action BrushRoundnessChanged;
        public static Action<float> BrushAngleDeltaChanged;
        public static Action UseFalloffForCustomBrushesChanged;
        public static Action UseAlphaFalloffChanged;
        public static Action SelectedBrushChanged;
        public static Action InvertBrushTextureChanged;
        public static Action SelectedBrushTabChanged;

        // The random changed events are only fired if the current value was not outside of the boundary value.
        public static Action RandomSpacingChanged;
        public static Action RandomOffsetChanged;
        public static Action RandomRotationChanged;

        [Include]
        private bool useFalloffForCustomBrushes = false;
        [Exclude]
        internal bool UseFalloffForCustomBrushes {
            get {
                return useFalloffForCustomBrushes;
            }
            set {
                if(useFalloffForCustomBrushes == value) return;
                useFalloffForCustomBrushes = value;
                if(UseFalloffForCustomBrushesChanged != null) UseFalloffForCustomBrushesChanged();
            }
        }

        [Include]
        private bool useAlphaFalloff = false;
        [Exclude]
        internal bool UseAlphaFalloff {
            get {
                return useAlphaFalloff;
            }
            set {
                if(useAlphaFalloff == value) return;
                useAlphaFalloff = value;
                if(UseAlphaFalloffChanged != null) UseAlphaFalloffChanged();
            }
        }
        
        [Include]
        private string selectedBrushTab = "All";
        [Exclude]
        internal string SelectedBrushTab {
            get {
                return selectedBrushTab;
            }
            set {
                if(selectedBrushTab == value) return;
                selectedBrushTab = value;
                if(SelectedBrushTabChanged != null) SelectedBrushTabChanged();
            }
        }

        [Include]
        private string selectedBrushId;
        [Exclude]
        internal string SelectedBrushId {
            get {
                return selectedBrushId;
            }
            set {
                if(selectedBrushId == value) return;
                selectedBrushId = value;
                if(SelectedBrushChanged != null) SelectedBrushChanged();
            }
        }

        [Include]
        private float brushSize = 35f;
        [Exclude]
        internal float BrushSize {
            get {
                return brushSize;
            }
            set {
                if(brushSize == value) return;
                brushSize = value;
                if(BrushSizeChanged != null) BrushSizeChanged();
            }
        }

        [Include]
        private float brushSpeed = 1f;
        [Exclude]
        internal float BrushSpeed {
            get {
                return brushSpeed;
            }
            set {
                if(brushSpeed == value) return;
                brushSpeed = value;
                if(BrushSpeedChanged != null) BrushSpeedChanged();
            }
        }

        [Include]
        private float brushRoundness = 1f;
        [Exclude]
        internal float BrushRoundness {
            get {
                return brushRoundness;
            }
            set {
                if(brushRoundness == value) return;
                brushRoundness = value;
                if(BrushRoundnessChanged != null) BrushRoundnessChanged();
            }
        }

        [Include]
        private float brushAngle = 0f;
        [Exclude]
        internal float BrushAngle {
            get {
                return brushAngle;
            }
            set {
                if(brushAngle == value) return;
                float delta = value - brushAngle;
                brushAngle = value;
                if(BrushAngleDeltaChanged != null) BrushAngleDeltaChanged(delta);
            }
        }

        [Exclude]
        internal AnimationCurve brushFalloff = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 0f, 1f));
        [Include]
        internal FauxKeyframe[] brushFalloffFauxFrames;
        
        // Random Spacing
        [Include]
        internal bool useBrushSpacing = false;
        [Include]
        internal float MinBrushSpacing { get; private set; }
        internal void SetMinBrushSpacing(float value, float min) {
            if(MinBrushSpacing == value) return;
            float oldMinBrushSpacing = MinBrushSpacing;
            MinBrushSpacing = value;
            if(oldMinBrushSpacing >= min && RandomSpacingChanged != null) RandomSpacingChanged();
        }
        [Include]
        internal float MaxBrushSpacing { get; private set; }
        internal void SetMaxBrushSpacing(float value, float max) {
            if(MaxBrushSpacing == value) return;
            float oldMaxBrushSpacing = MaxBrushSpacing;
            MaxBrushSpacing = value;
            if(oldMaxBrushSpacing <= max && RandomSpacingChanged != null) RandomSpacingChanged();
        }

        // Random Rotation
        [Include]
        internal bool useRandomRotation = false;
        [Include]
        internal float MinRandomRotation { get; private set; }
        internal void SetMinRandomRotation(float value, float min) {
            if(MinRandomRotation == value) return;
            float oldMinRandomRotation = MinRandomRotation;
            MinRandomRotation = value;
            if(oldMinRandomRotation >= min && RandomRotationChanged != null) RandomRotationChanged();
        }
        [Include]
        internal float MaxRandomRotation { get; private set; }
        internal void SetMaxRandomRotation(float value, float max) {
            if(MaxRandomRotation == value) return;
            float oldMaxRandomRotation = MaxRandomRotation;
            MaxRandomRotation = value;
            if(oldMaxRandomRotation <= max && RandomRotationChanged != null) RandomRotationChanged();
        }

        // Random Offset
        [Include]
        internal bool useRandomOffset = false;

        [Include]
        private bool invertBrushTexture = false;
        [Exclude]
        internal bool InvertBrushTexture {
            get {
                return invertBrushTexture;
            }
            set {
                if(value == invertBrushTexture) return;
                invertBrushTexture = value;
                if(InvertBrushTextureChanged != null) InvertBrushTextureChanged();
            }
        }

        [Include]
        private float randomOffset = 10f;
        internal float RandomOffset {
            get {
                return randomOffset;
            }
            set {
                if(randomOffset == value) return;
                randomOffset = value;
                RandomOffsetChanged();
            }
        }

        public ModeSettings() {
            selectedBrushTab = "All";
            MinBrushSpacing = 1f;
            MaxBrushSpacing = 50f;
            MinRandomRotation = -180f;
            MaxRandomRotation = 180f;
            randomOffset = 10f;
        }
    }
}
