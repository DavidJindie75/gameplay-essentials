using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ClickBasedMove : MonoBehaviour
{

    [SerializeField] 
    private LayerMask moveableground;

    [SerializeField]
    private Camera cam;

    private NavMeshAgent pathFinderAgent;

    Ray theRay;

    RaycastHit rayCastInfo;


    void Awake()
    {
        pathFinderAgent = GetComponent<NavMeshAgent>();
    }

    void FixedUpdate()
    {
        if (Input.GetMouseButton(0))
        {
            Debug.Log("you clicked");

            theRay = cam.ScreenPointToRay(Input.mousePosition);

            if(Physics.Raycast(theRay, out rayCastInfo))
            {
                Debug.Log("ray was casted");

                pathFinderAgent.SetDestination(rayCastInfo.point);

                Debug.Log(pathFinderAgent.steeringTarget);
            }
        }
    }
}
