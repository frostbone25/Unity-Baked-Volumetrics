#if UNITY_EDITOR

using UnityEngine;

public struct VoxelLightArea
{
    public Vector3 lightPosition;
    public Vector3 lightForwardDirection;
    public Vector3 lightRightDirection;
    public Vector3 lightUpwardDirection;
    public Vector2 lightSize;
    public Vector3 lightColor;
    public float lightIntensity;
    public float lightRange;

    public static int GetByteSize()
    {
        int size = 0;

        size += 3 * 4; //lightPosition (12 bytes)
        size += 3 * 4; //lightForwardDirection (12 bytes)
        size += 3 * 4; //lightRightDirection (12 bytes)
        size += 3 * 4; //lightUpwardDirection (12 bytes)
        size += 3 * 4; //lightColor (12 bytes)

        size += 2 * 4; //lightSize (8 bytes)

        size += 4; //lightIntensity (4 bytes)
        size += 4; //lightRange (4 bytes)

        return size;
    }

    public VoxelLightArea(Light areaLight)
    {
        lightColor = new Vector3(areaLight.color.r, areaLight.color.g, areaLight.color.b);
        lightIntensity = areaLight.intensity;
        lightForwardDirection = areaLight.transform.forward;
        lightRightDirection = areaLight.transform.right;
        lightUpwardDirection = areaLight.transform.up;
        lightPosition = areaLight.transform.position;
        lightRange = areaLight.range;
        lightSize = areaLight.areaSize;
    }
}

#endif