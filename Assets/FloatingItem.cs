using UnityEngine;
using System;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class FloatingItem : MonoBehaviour
{

    public float BobStrength = 0.125f;
    float GroundHeight = 0.5f;
    public bool canRotate = false;
    private Transform transItem;

    void Start()
    {
       transItem=transform.GetChild(0);

    }


    void Update() {
		//place 1 unity from ground		
	//	RaycastHit rHit;		
	//	if(Physics.Raycast(transform.position,-Vector3.up,out rHit,10.0f))
	//	{
			//Debug.Log(rHit.point);
		//	transform.position = rHit.point  + ((Vector3.up*0.75f )* GroundHeight);			
	//	}

        //rotate

			transItem.localPosition = new Vector3(0, 1.0f+ ((float)Math.Sin(Time.time) * BobStrength),0);
			if(canRotate)
			{
				transItem.Rotate(Vector3.up * 60*Time.deltaTime, Space.Self);
			}
		
	}

}
