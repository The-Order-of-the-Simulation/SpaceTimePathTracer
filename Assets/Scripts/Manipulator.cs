using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Manipulator : MonoBehaviour
{
    // Start is called before the first frame update
    public float Force = 1.0f;
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
