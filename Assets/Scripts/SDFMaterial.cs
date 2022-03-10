using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SDFMaterial : MonoBehaviour
{
    public Color Color = new Color(1.0f, 1.0f, 1.0f, 1.0f);

    [ColorUsage(true, true)]
    public Color Glow = new Color(0.0f, 0.0f, 0.0f, 0.0f);

    public float Roughness = 0.5f;
    public float Metal = 0.0f;
}
