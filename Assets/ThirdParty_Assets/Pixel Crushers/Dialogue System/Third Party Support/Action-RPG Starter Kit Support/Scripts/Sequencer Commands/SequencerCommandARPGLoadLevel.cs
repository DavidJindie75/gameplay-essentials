using UnityEngine;
using PixelCrushers.DialogueSystem;

namespace PixelCrushers.DialogueSystem.SequencerCommands {

	/// <summary>
	/// Adds sequencer command LoadLevel(levelName, spawnPointName).
	/// 
	/// This is a variation of the LoadLevel() sequencer command that works
	/// with ARPG. This command changes levels with full Dialogue System
	/// data persistence.
	/// </summary>
	public class SequencerCommandARPGLoadLevel : SequencerCommand {
		
		public void Start() {
			string levelName = GetParameter(0);
			string spawnPointName = GetParameter(1);
			if (string.IsNullOrEmpty(levelName)) {
				if (DialogueDebug.LogWarnings) Debug.LogWarning(string.Format("{0}: Sequencer: ARPGLoadLevel() level name is an empty string. Can't teleport.", DialogueDebug.Prefix));
			}
			else if (string.IsNullOrEmpty(spawnPointName)) {
				if (DialogueDebug.LogWarnings) Debug.LogWarning(string.Format("{0}: Sequencer: ARPGLoadLevel() spawn point name is an empty string. Can't teleport.", DialogueDebug.Prefix));
			} else {
				if (DialogueDebug.LogInfo) Debug.Log(string.Format("{0}: Sequencer: ARPGLoadLevel({1}, {2})", new object[] { DialogueDebug.Prefix, levelName, spawnPointName }));
				var player = GameObject.FindWithTag("Player");
				var statusC = (player == null) ? null : player.GetComponent<StatusC>();
				if (statusC != null) statusC.spawnPointName = spawnPointName;
				var levelManager = FindObjectOfType<LevelManager>();
				if (levelManager != null) {
					levelManager.LoadLevel(levelName);
				} else {
					PersistentDataManager.Record();
					PersistentDataManager.LevelWillBeUnloaded();
                    Tools.LoadLevel(levelName); //---Was: Application.LoadLevel(levelName);
					PersistentDataManager.Apply();
				}
			}
			Stop();
		}
	}
}
