using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModelViewer : MonoBehaviour
{
    [SerializeField]
    Transform cameraPivot;
    [SerializeField]
    Transform modelRoot;
    [SerializeField]
    Camera modelCamera;
    [SerializeField]
    float rotationSpeed = 30f;

    [SerializeField]
    GameObject model;

    bool isRotating = true;

    void Update()
    {
        if (isRotating && cameraPivot != null)
        {
            model.transform.Rotate(Vector3.up,rotationSpeed * Time.deltaTime,Space.Self);
        }
    }
}
