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
    public NavMeshAgent pathFinderAgent;
    public bool enemyTargeted;
    public bool closeToEnemy;
    public GameObject target;

    Ray theRay;
    RaycastHit rayCastInfo;

    void Awake()
    {
        pathFinderAgent = GetComponent<NavMeshAgent>();
    }

    void FixedUpdate()
    {
        New_Input();
        CloseToEnemyCheck();
    }

    void New_Input()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0) || Input.GetMouseButton(0))
        {

            theRay = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(theRay, out rayCastInfo, 1000f))
            {
                target = rayCastInfo.collider.gameObject;

                if (target.tag == StaticTags.Enemy)
                {
                    enemyTargeted = true;
                }
                else
                {
                    if (target.tag != StaticTags.Enemy)
                    {
                        isMoving = true;
                        NavMeshExtensions.GoToSetPosition(rayCastInfo.point, pathFinderAgent);
                        pathFinderAgent.destination = rayCastInfo.point;

                        if (enemyTargeted)
                            enemyTargeted = false;

                        if (closeToEnemy)
                            closeToEnemy = false;
                    }
                    else
                    {
                        isMoving = false;
                        NavMeshExtensions.StopAgent(pathFinderAgent);
                    }
                }
            }
        }

        if (enemyTargeted)
        {
            MoveToAttack();
        }

        if (pathFinderAgent.remainingDistance <= pathFinderAgent.stoppingDistance)
            NavMeshExtensions.StopAgent(pathFinderAgent);
        else
            NavMeshExtensions.ResumeAgent(pathFinderAgent);

        if (pathFinderAgent.remainingDistance <= pathFinderAgent.stoppingDistance) { isMoving = false; }
    }

    void MoveToAttack()
    {
        pathFinderAgent.destination = target.transform.position;

        if (pathFinderAgent.remainingDistance > GetComponent<AttackTriggerC>().requiredDistanceForAttack && !closeToEnemy)
        {
            isMoving = true;
            NavMeshExtensions.ResumeAgent(pathFinderAgent);
        }
        else if (pathFinderAgent.remainingDistance <= GetComponent<AttackTriggerC>().requiredDistanceForAttack && closeToEnemy)
        {
            isMoving = false;
            transform.LookAt(target.transform);
            Vector3 attackDirection = target.transform.position - transform.position;
        }
    }

    void CloseToEnemyCheck()
    {
        if (!enemyTargeted)
            return;

        if (pathFinderAgent.remainingDistance > GetComponent<AttackTriggerC>().requiredDistanceForAttack)
        {
            closeToEnemy = false;
        }
        else
        {
            closeToEnemy = true;
        }
    }

    void Old_Input()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0) || Input.GetMouseButton(0))
        {
            //Debug.Log("you clicked");

            theRay = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(theRay, out rayCastInfo))
            {
                //Debug.Log("ray was casted");

                pathFinderAgent.SetDestination(rayCastInfo.point);

                isMoving = true;
                //Debug.Log(pathFinderAgent.steeringTarget);
            }

        }
        if (pathFinderAgent.remainingDistance <= pathFinderAgent.stoppingDistance) { isMoving = false; }
    }
}
