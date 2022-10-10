#if UNITY_EDITOR

using UnityEngine;

public struct VoxelLightArea
{
    public Vector3 lightPosition;
    public Vector3 lightDirection;
    public Vector2 lightSize;
    public Vector3 lightColor;
    public float lightIntensity;
    public float lightRange;

    public static int GetByteSize()
    {
        int size = 0;

        size += 3 * 4; //lightPosition (12 bytes)
        size += 3 * 4; //lightDirection (12 bytes)
        size += 3 * 4; //lightColor (12 bytes)

        size += 2 * 4; //lightSize (8 bytes)

        size += 4; //lightIntensity (4 bytes)
        size += 4; //lightRange (4 bytes)

        return size;
    }
}

#endif