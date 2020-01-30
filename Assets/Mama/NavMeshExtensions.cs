using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class NavMeshExtensions
{
    public static void ResumeAgent(NavMeshAgent nva)
    {
        nva.isStopped = false;
    }

    public static void StopAgent(NavMeshAgent nva)
    {
        nva.isStopped = true;
    }

    public static void GoToSetPosition(Vector3 tp, NavMeshAgent nva)
    {
        ResumeAgent(nva);
        nva.SetDestination(tp);
    }

    public static void StopMoving(NavMeshAgent nva)
    {
        nva.ResetPath();

    }
}
