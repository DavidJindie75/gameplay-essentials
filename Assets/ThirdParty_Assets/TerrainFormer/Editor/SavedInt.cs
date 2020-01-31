using System;
using UnityEditor;

namespace JesseStiller.TerrainFormerExtension { 
    internal class SavedInt {
        // Sends the previous value as a parameter while the new value is simply this object's value.
        internal Action<int> ValueChanged;

        internal readonly string prefsKey;
        internal readonly int defaultValue;

        private int value;
        internal int Value {
            get {
                return value;
            }
            set {
                if(this.value == value) return;
                int previousValue = this.Value;
                this.value = value;
                EditorPrefs.SetInt(prefsKey, value);

                if(ValueChanged != null) ValueChanged(previousValue);
            }
        }

        public SavedInt(string prefsKey, int defaultValue) {
            this.prefsKey = prefsKey;
            this.defaultValue = defaultValue;
            value = EditorPrefs.GetInt(prefsKey, defaultValue);
        }

        public static implicit operator int(SavedInt s) {
            return s.Value;
        }
    }
}