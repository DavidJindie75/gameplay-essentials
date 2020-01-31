using UnityEngine;
using System.Collections;
using PixelCrushers.DialogueSystem;
using PixelCrushers.DialogueSystem.ARPGSupport;

namespace PixelCrushers.DialogueSystem.SequencerCommands {

	/// <summary>
	/// Sequencer command ARPGAdjustHealth([subject, [amount, [element]]])
	/// </summary>
	public class SequencerCommandARPGAdjustHealth : SequencerCommand {

		public void Start() {
			Transform subject = GetSubject(0, Sequencer.Speaker);
			int amount = GetParameterAsInt(1, 99999);
			int element = GetParameterAsInt(2, 0);

			if (subject == null) {
				if (DialogueDebug.LogWarnings) Debug.LogWarning(string.Format("{0}: Sequencer: ARPGAdjustHealth({1}, {2}, {3}): Can't find '{1}'", DialogueDebug.Prefix, GetParameter(0), GetParameter(1), GetParameter(2)));
			} else if (string.Equals(subject.tag, "Player")) {
				ARPGBridge.AdjustPlayerHealth(amount, element);
			} else {
				StatusC status = subject.GetComponentInChildren<StatusC>();
				if (status == null) {
					if (DialogueDebug.LogWarnings) Debug.LogWarning(string.Format("{0}: Sequencer: ARPGAdjustHealth({1}, {2}, {3}): Can't find StatusC on {1}", DialogueDebug.Prefix, subject.name, amount, element));
				} else {
					if (DialogueDebug.LogInfo) Debug.Log(string.Format("{0}: Sequencer: ARPGAdjustHealth({1}, {2}, {3})", DialogueDebug.Prefix, subject.name, amount, element));
					if (amount < 0) {
						status.OnDamage(Mathf.Abs(amount), element);
					} else {
						status.Heal(amount, element);
					}
				}
			}
			Stop();
		}
		
	}
	 
}
