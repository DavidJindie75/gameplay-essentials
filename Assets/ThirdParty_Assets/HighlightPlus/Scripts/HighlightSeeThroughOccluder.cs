using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HighlightPlus {

	[RequireComponent (typeof(HighlightEffect))]
	[HelpURL ("https://kronnect.freshdesk.com/support/solutions/42000065090")]
	[ExecuteInEditMode]
	public class HighlightSeeThroughOccluder : MonoBehaviour {

		HighlightEffect effect;

		void Awake () {
			effect = GetComponent<HighlightEffect> ();
		}

		void Update () {
			effect.RenderOccluder ();
		}
	}

}