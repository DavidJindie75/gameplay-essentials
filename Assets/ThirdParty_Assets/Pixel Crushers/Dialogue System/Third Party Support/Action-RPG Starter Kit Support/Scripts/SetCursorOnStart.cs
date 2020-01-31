using UnityEngine;
using System.Collections;
using System.Reflection;

namespace PixelCrushers.DialogueSystem.ARPGSupport {

	/// <summary>
	/// Shows or hides the mouse cursor on start.
	/// </summary>
	[AddComponentMenu("Pixel Crushers/Dialogue System/Third Party/Action-RPG Starter Kit/Set Cursor On Start")]
	public class SetCursorOnStart : MonoBehaviour {

		public bool show = true;

		public void Start() {
            //Screen.lockCursor = !show;
            Tools.SetCursorActive(show);
		}

	}

}
