#if UNITY_EDITOR

using UnityEngine;

public struct VoxelLightSpot
{
    public Vector3 lightPosition;
    public Vector3 lightDirection;
    public Vector3 lightColor;
    public float lightIntensity;
    public float lightRange;
    public float lightAngle;

    public static int GetByteSize()
    {
        int size = 0;

        size += 3 * 4; //lightPosition (12 bytes)
        size += 3 * 4; //lightDirection (12 bytes)
        size += 3 * 4; //lightColor (12 bytes)

        size += 4; //lightIntensity (4 bytes)
        size += 4; //lightRange (4 bytes)
        size += 4; //lightAngle (4 bytes)

        return size;
    }

    public VoxelLightSpot(Light spotLight)
    {
        lightColor = new Vector3(spotLight.color.r, spotLight.color.g, spotLight.color.b);
        lightIntensity = spotLight.intensity;
        lightPosition = spotLight.transform.position;
        lightDirection = spotLight.transform.forward;
        lightRange = spotLight.range;
        lightAngle = spotLight.spotAngle;
    }
}

#endif