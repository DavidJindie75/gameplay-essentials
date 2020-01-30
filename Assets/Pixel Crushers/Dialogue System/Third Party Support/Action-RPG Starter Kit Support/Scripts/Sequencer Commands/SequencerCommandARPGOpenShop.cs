using UnityEngine;
using System.Collections;
using System.Reflection;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.SequencerCommands;
using PixelCrushers.DialogueSystem.ARPGSupport;

namespace PixelCrushers.DialogueSystem.SequencerCommands {

	/// <summary>
	/// Sequencer command ARPGOpenShop([subject])
	/// 
	/// - subject: The name of the merchant GameObject. Default: speaker.
	/// 
	/// To use this sequencer command, you *must* add these lines 
	/// to `ActionRPGKit/CSharpExample/ScriptCSharp/ShopC.cs`:
	/// <code>public void OpenShop() {
	///		shopMain = true;
	///		OnOffMenu ();
	/// }</code>
	///	After line 39 is a good place.
	/// </summary>
	public class SequencerCommandARPGOpenShop : SequencerCommand {

		public void Start() {
			Transform subject = GetSubject(0, Sequencer.Speaker);
			if (subject == null) {
				if (DialogueDebug.LogWarnings) Debug.LogWarning(string.Format("{0}: Sequencer: ARPGOpenShop({1}): Can't find '{1}'", DialogueDebug.Prefix, GetParameter(0)));
			} else {
				ShopC shop = subject.GetComponent<ShopC>();
				if (shop == null) {
					if (DialogueDebug.LogWarnings) {
						if (subject.GetComponentInChildren<ShopC>() == null) {
							Debug.LogWarning(string.Format("{0}: Sequencer: ARPGOpenShop({1}): Can't find ShopC on {1}", DialogueDebug.Prefix, subject.name));
						} else {
							Debug.LogWarning(string.Format("{0}: Sequencer: ARPGOpenShop({1}): ShopC must be on {1}, not a child GameObject", DialogueDebug.Prefix, subject.name));
						}
					}
				} else {
					if (DialogueDebug.LogInfo) Debug.Log(string.Format("{0}: Sequencer: ARPGOpenShop({1}): sending 'OpenShop' message to shop component", DialogueDebug.Prefix, subject.name));
					DialogueManager.StopConversation();
					Time.timeScale = 1;
					shop.SendMessage("OpenShop");
				}
			}
			Stop();
		}
		
	}
	 
}
