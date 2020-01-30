using UnityEngine;
using System.Collections;
using System.Reflection;

namespace PixelCrushers.DialogueSystem.ARPGSupport {

	/// <summary>
	/// Disables the "[E] check" key for ShopC and QuestTriggerC by sending
	/// fake "OnTriggerExit" messages to them. *Currently only disables ShopC.*
	/// </summary>
	[AddComponentMenu("Pixel Crushers/Dialogue System/Third Party/Action-RPG Starter Kit/Disable Check Key (Shop NPC)")]
	public class DisableCheckKey : MonoBehaviour {

		public void OnTriggerEnter(Collider other){
			if (other.gameObject.tag == "Player") {
				StartCoroutine(SendFakeOnTriggerExit(other));
			}
		}

		private IEnumerator SendFakeOnTriggerExit(Collider other) {
			ShopC shop = GetComponent<ShopC>();
			MethodInfo shopOnTriggerExit = (shop != null) 
				? shop.GetType().GetMethod("OnTriggerExit", BindingFlags.NonPublic | BindingFlags.Instance)
					: null;
			//--- Integration will use Dialogue System's quest system.
			//--- Don't disable QuestTriggerC for now:
			// QuestTriggerC questTrigger = GetComponent<QuestTriggerC>();
			//MethodInfo questTriggerOnTriggerExit = (questTrigger != null) 
			//	? questTrigger.GetType().GetMethod("OnTriggerExit", BindingFlags.NonPublic | BindingFlags.Instance)
			//		: null;
			InvokeOnTriggerExit(shop, shopOnTriggerExit, other);
			//InvokeOnTriggerExit(questTrigger, questTriggerOnTriggerExit, other);
			yield return null;
			InvokeOnTriggerExit(shop, shopOnTriggerExit, other);
			//InvokeOnTriggerExit(questTrigger, questTriggerOnTriggerExit, other);
		}

		private void InvokeOnTriggerExit(object obj, MethodInfo onTriggerExit, Collider other) {
			if ((obj != null) && (onTriggerExit != null)) {
				onTriggerExit.Invoke(obj, new object[] { other });
			}
		}


	}

}
