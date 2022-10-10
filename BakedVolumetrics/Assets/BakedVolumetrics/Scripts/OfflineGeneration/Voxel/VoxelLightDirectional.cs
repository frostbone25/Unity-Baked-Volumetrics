#if UNITY_EDITOR

using UnityEngine;

public struct VoxelLightDirectional
{
    public Vector3 lightDirection;
    public Vector3 lightColor;
    public float lightIntensity;

    public static int GetByteSize()
    {
        int size = 0;

        size += 3 * 4; //lightDirection (12 bytes)
        size += 3 * 4; //lightColor (12 bytes)

        size += 4; //lightIntensity (4 bytes)

        return size;
    }
}

#endif