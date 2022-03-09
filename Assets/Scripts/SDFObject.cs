using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SDFObject : MonoBehaviour
{
    public enum SDFType
    {
        Sphere,
        Box,
        Capsule,
        Torus,
        Cylinder,
    }

    public SDFType type;

    void Start()
    {

    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;

        Gizmos.DrawWireSphere(transform.position, Vector3.Magnitude(transform.lossyScale) / Mathf.Sqrt(3.0f));
    }

    void OnDrawGizmos()
    {
        OnDrawGizmosSelected();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
