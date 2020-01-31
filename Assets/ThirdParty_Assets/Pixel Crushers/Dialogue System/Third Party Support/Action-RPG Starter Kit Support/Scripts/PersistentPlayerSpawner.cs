using UnityEngine;
using PixelCrushers.DialogueSystem;

namespace PixelCrushers.DialogueSystem.ARPGSupport {
	
	/// <summary>
	/// This script handles player spawning in the correct place when changing levels.
	/// Add it to the Dialogue Manager.
	/// </summary>
	[AddComponentMenu("Pixel Crushers/Dialogue System/Third Party/Action-RPG Starter Kit/Persistent Player Spawner")]
	public class PersistentPlayerSpawner : MonoBehaviour {
		
		public string spawnPointName;
		
		public void OnRecordPersistentData() {
			var currentPlayer = GameObject.FindWithTag("Player");
			var statusC = (currentPlayer == null) ? null : currentPlayer.GetComponent<StatusC>();
			if (statusC != null) {
				spawnPointName = statusC.spawnPointName;
			}
		}
		
		public void OnApplyPersistentData() {
			var currentPlayer = GameObject.FindWithTag("Player");
			if (currentPlayer == null) Debug.LogWarning("Can't find player", this);
			var statusC = (currentPlayer == null) ? null : currentPlayer.GetComponent<StatusC>();
			if (statusC == null) Debug.LogWarning("Can't find StatusC on player", currentPlayer);
			if ((statusC == null) || string.IsNullOrEmpty(spawnPointName)) return;
			GameObject spawnPoint = GameObject.Find(spawnPointName);
			if (spawnPoint == null) {
				Debug.LogWarning("Can't find spawn point: " + spawnPointName, this);
				return;
			}
			if (Debug.isDebugBuild) Debug.Log("Moving player to: " + spawnPointName + " (" + spawnPoint.transform.position + ")");
			currentPlayer.transform.position = spawnPoint.transform.position;
			currentPlayer.transform.rotation = spawnPoint.transform.rotation;
			spawnPointName = string.Empty;
		}
		
		private StatusC GetPlayerStatusC() {
			var currentPlayer = GameObject.FindWithTag("Player");
			return (currentPlayer == null) ? null : currentPlayer.GetComponent<StatusC>();
		}
		
	}
	
}
