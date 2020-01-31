using UnityEngine;
using PixelCrushers.DialogueSystem;

namespace PixelCrushers.DialogueSystem.ARPGSupport {

	/// <summary>
	/// Pauses ARPG camera control during conversations.
	/// </summary>
	[AddComponentMenu("Pixel Crushers/Dialogue System/Third Party/Action-RPG Starter Kit/Pause ARPG Camera On Conversation (Player prefab)")]
	public class PauseARPGCameraOnConversation : MonoBehaviour {

		private ARPGcameraC arpgCamera = null;

		public void OnConversationStart(Transform actor) {
			arpgCamera = (Camera.main != null) ? Camera.main.GetComponent<ARPGcameraC>() : null;
			if (arpgCamera != null) arpgCamera.enabled = false;
		}

		public void OnConversationEnd(Transform actor) {
			if (arpgCamera != null) arpgCamera.enabled = true;
		}

	}

}
