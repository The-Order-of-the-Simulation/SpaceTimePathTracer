using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

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

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        switch (type)
        {
            case SDFType.Sphere:
                Gizmos.DrawWireSphere(new Vector3(0, 0, 0), 0.5f);
                break;
            case SDFType.Box:
                Gizmos.DrawWireCube(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
                break;
            case SDFType.Capsule:
                GizmosHelper.DrawWireCapsule(transform.position, transform.rotation, 0.5f * transform.lossyScale.x,
                                                       0.5f * transform.lossyScale.y, Color.cyan);
                break;
            case SDFType.Torus:
                GizmosHelper.DrawWireTorus(transform.position, transform.rotation, 0.5f * transform.lossyScale.x, transform.lossyScale.y,
                                                     Color.cyan);
                break;
            case SDFType.Cylinder:
                GizmosHelper.DrawWireCylinder(transform.position, transform.rotation, 0.5f * transform.lossyScale.x, transform.lossyScale.y,
                                                        Color.cyan);
                break;
        }
    }

    void OnDrawGizmos()
    {
        OnDrawGizmosSelected();
    }
#endif

    // Update is called once per frame
    void Update()
    {
        
    }
}
