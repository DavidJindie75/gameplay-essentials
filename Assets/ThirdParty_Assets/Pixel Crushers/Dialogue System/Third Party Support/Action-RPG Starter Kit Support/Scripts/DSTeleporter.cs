using UnityEngine;
using System.Collections;
using PixelCrushers.DialogueSystem;

namespace PixelCrushers.DialogueSystem.ARPGSupport {

	/// <summary>
	/// Replaces ARPG's teleporter to also tie in the Dialogue System. You can
	/// set it to activate when the player enters the trigger collider (like
	/// ARPG's teleporter) or when the player uses it by pressing the 
	/// ProximitySelector's "Use" key ("E" by default). In this case, add a
	/// Usable component to the teleporter.
	/// </summary>
	[AddComponentMenu("Pixel Crushers/Dialogue System/Third Party/Action-RPG Starter Kit/DS Teleporter")]
	public class DSTeleporter : MonoBehaviour {

		[Tooltip("Teleport to this level")]
		public string teleportToMap = "Level1";

		[Tooltip("In the new level, spawn the player at the GameObject with this name")]
		public string spawnPointName = "PlayerSpawn1"; // Use to move player to the SpawnPoint position

		public enum ActivationMethod { OnTriggerEnter, OnUse }

		[Tooltip("Specify how the teleporter is triggered")]
		public ActivationMethod activationMethod = ActivationMethod.OnTriggerEnter;

		/// <summary>
		/// If the player enters the trigger collider and the activation method is
		/// OnTriggerEnter, then teleport to the new level.
		/// </summary>
		/// <param name="other">Other.</param>
		public void OnTriggerEnter(Collider other) {
			if ((activationMethod == ActivationMethod.OnTriggerEnter) && other.CompareTag("Player")) {
				Teleport(other.GetComponent<StatusC>());
			}
		}

		/// <summary>
		/// If the player "uses" the teleporter and the activation method is
		/// OnUse, then teleport to the new level. The teleporter should have
		/// a Usable component.
		/// </summary>
		/// <param name="user">User.</param>
		public void OnUse(Transform user) {
			if ((activationMethod == ActivationMethod.OnUse) && (user != null) && user.CompareTag("Player")) {
				Teleport(user.GetComponent<StatusC>());
			}
		}

		/// <summary>
		/// This method is similar to ARPG's Teleport script, but it also uses the
		/// Dialogue System's LevelManager component to maintain persistent data.
		/// </summary>
		/// <param name="statusC">The player's StatusC component.</param>
		private void Teleport(StatusC statusC) {
			if (statusC == null) return;
			statusC.spawnPointName = spawnPointName;
			if (Debug.isDebugBuild) Debug.Log("Teleporting to: " + teleportToMap);
			var levelManager = FindObjectOfType<LevelManager>();
			if (levelManager != null) {
				levelManager.LoadLevel(teleportToMap);
			} else {
				PersistentDataManager.Record();
				PersistentDataManager.LevelWillBeUnloaded();
                Tools.LoadLevel(teleportToMap); //---Was: Application.LoadLevel(teleportToMap);
				PersistentDataManager.Apply();
			}
		}
	}
	
}