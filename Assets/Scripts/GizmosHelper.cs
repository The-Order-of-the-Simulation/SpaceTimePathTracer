#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class GizmosHelper
{
    public static void DrawWireCapsule(Vector3 pos, Quaternion rot, float radius, float height,
                                        Color color = default(Color))
    {
        if (color != default(Color))
            Handles.color = color;
        Matrix4x4 angleMatrix = Matrix4x4.TRS(pos, rot, Handles.matrix.lossyScale);
        using (new Handles.DrawingScope(angleMatrix))
        {
            var pointOffset = height;

            // draw sideways
            Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.left, Vector3.back, -180, radius);
            Handles.DrawLine(new Vector3(0, pointOffset, -radius), new Vector3(0, -pointOffset, -radius));
            Handles.DrawLine(new Vector3(0, pointOffset, radius), new Vector3(0, -pointOffset, radius));
            Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.left, Vector3.back, 180, radius);
            // draw frontways
            Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.back, Vector3.left, 180, radius);
            Handles.DrawLine(new Vector3(-radius, pointOffset, 0), new Vector3(-radius, -pointOffset, 0));
            Handles.DrawLine(new Vector3(radius, pointOffset, 0), new Vector3(radius, -pointOffset, 0));
            Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.back, Vector3.left, -180, radius);
            // draw center
            Handles.DrawWireDisc(Vector3.up * pointOffset, Vector3.up, radius);
            Handles.DrawWireDisc(Vector3.down * pointOffset, Vector3.up, radius);
        }
    }

    public static void DrawWireCylinder(Vector3 pos, Quaternion rot, float radius, float height,
                                        Color color = default(Color))
    {
        if (color != default(Color))
            Handles.color = color;
        Matrix4x4 angleMatrix = Matrix4x4.TRS(pos, rot, Handles.matrix.lossyScale);
        using (new Handles.DrawingScope(angleMatrix))
        {
            var pointOffset = height;
            Handles.DrawLine(new Vector3(0, pointOffset, -radius), new Vector3(0, -pointOffset, -radius));
            Handles.DrawLine(new Vector3(0, pointOffset, radius), new Vector3(0, -pointOffset, radius));
            Handles.DrawLine(new Vector3(-radius, pointOffset, 0), new Vector3(-radius, -pointOffset, 0));
            Handles.DrawLine(new Vector3(radius, pointOffset, 0), new Vector3(radius, -pointOffset, 0));
            Handles.DrawWireDisc(Vector3.up * pointOffset, Vector3.up, radius);
            Handles.DrawWireDisc(Vector3.down * pointOffset, Vector3.up, radius);
        }
    }

    public static void DrawWireTorus(Vector3 pos, Quaternion rot, float radiusSmall, float radiusLarge,
                                        Color color = default(Color))
    {
        if (color != default(Color))
            Handles.color = color;
        Matrix4x4 angleMatrix = Matrix4x4.TRS(pos, rot, Handles.matrix.lossyScale);
        using (new Handles.DrawingScope(angleMatrix))
        {
            // small circles
            for (float ang = 0.0f; ang < 360.0f; ang += 15.0f)
            {
                float radians = Mathf.Deg2Rad * ang;
                Vector3 direction = Vector3.left * Mathf.Sin(radians) + Vector3.forward * Mathf.Cos(radians);
                Vector3 normal = Vector3.left * Mathf.Cos(radians) - Vector3.forward * Mathf.Sin(radians);
                Handles.DrawWireDisc(direction * radiusLarge, normal, radiusSmall);
            }

            // large circles
            for (float ang = 0.0f; ang < 360.0f; ang += 15.0f)
            {
                float radians = Mathf.Deg2Rad * ang;
                float radius = radiusLarge + radiusSmall * Mathf.Sin(radians);
                Vector3 center = radiusSmall * Vector3.up * Mathf.Cos(radians);
                Handles.DrawWireDisc(center, Vector3.up, radius);
            }
        }
    }
}


#endif