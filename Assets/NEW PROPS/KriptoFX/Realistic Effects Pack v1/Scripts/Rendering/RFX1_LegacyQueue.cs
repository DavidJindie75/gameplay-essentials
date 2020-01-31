using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RFX1_LegacyQueue : MonoBehaviour
{

    public int Queue = 3001;
	// Use this for initialization
	void Awake () {
#if !KRIPTO_FX_LWRP_RENDERING && !KRIPTO_FX_HDRP_RENDERING
	    GetComponent<Renderer>().material.renderQueue = Queue;
#endif
	}

    // Update is called once per frame
    void Update () {
		
	}
}