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
    public bool isMoving;
    private NavMeshAgent pathFinderAgent;

    Ray theRay;

    RaycastHit rayCastInfo;


    void Awake()
    {
        pathFinderAgent = GetComponent<NavMeshAgent>();
    }

    void FixedUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0) || Input.GetMouseButton(0))
        {
            Debug.Log("you clicked");

            theRay = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(theRay, out rayCastInfo))
            {
                Debug.Log("ray was casted");

                pathFinderAgent.SetDestination(rayCastInfo.point);

                isMoving = true;
                Debug.Log(pathFinderAgent.steeringTarget);
            }

        }
        if (Input.GetKeyUp(KeyCode.Mouse0)) { isMoving = false; }
    }
}
