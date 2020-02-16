using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiabloStyleCam : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    [Header("Distances")]
    [Range(2f,7f)]public float distance = 5f;
    public float minDistance = 1f;
    public float maxDistance = 7f;
    public Vector3 offset;
    [Header("Speeds")]
    public float smoothSpeed = 5f;
    public float scrollSensitivity = 5;

    void LateUpdate()
    {
        if(!target)
        {
            print("NO TARGET SET FOR THE MAIN CAMERA");
            return;
        }

        float num = Input.GetAxis("Mouse ScrollWheel");
        distance -= num * scrollSensitivity;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        Vector3 pos = target.position + offset;
        pos -= transform.forward * distance;

        transform.position = Vector3.Lerp(transform.position,pos,smoothSpeed * Time.deltaTime);

    }
}
