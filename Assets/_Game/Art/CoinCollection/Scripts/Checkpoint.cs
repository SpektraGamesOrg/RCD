using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WindTurbine : MonoBehaviour
{
    public Vector3 rotationAngle = Vector3.zero;
    private readonly float _rotationMultiplier = 40f;

    private void LateUpdate()
    {
        transform.Rotate(rotationAngle * (Time.deltaTime * _rotationMultiplier), Space.Self);
    }

}