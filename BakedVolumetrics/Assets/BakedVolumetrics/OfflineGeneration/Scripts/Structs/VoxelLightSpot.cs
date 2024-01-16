#if UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

namespace BakedVolumetrics
{
    /// <summary>
    /// Spot Light (384 BITS | 48 BYTES)
    /// 
    /// <para>Gets the necessary data from a Unity Spot Light, to be used by the voxel tracer. </para>
    /// </summary>
    public struct VoxelLightSpot
    {
        public Vector3 lightPosition; //96 BITS | 12 BYTES
        public Vector3 lightDirection; //96 BITS | 12 BYTES
        public Vector3 lightColor; //96 BITS | 12 BYTES
        public float lightRange; //32 BITS | 4 BYTES
        public float lightAngle; //32 BITS | 4 BYTES
        public float shadowRadius; //32 BITS | 4 BYTES

        /// <summary>
        /// Returns the total size, in bytes, occupied by an instance of this struct in memory.
        /// </summary>
        /// <returns></returns>
        public static int GetByteSize() => Marshal.SizeOf(typeof(VoxelLightSpot));

        /// <summary>
        /// Constructor that initializes the VoxelLightSpot instance using a Unity Light component.
        /// </summary>
        /// <param name="spotLight"></param>
        public VoxelLightSpot(Light spotLight)
        {
            lightColor = new Vector3(spotLight.color.r, spotLight.color.g, spotLight.color.b);

            //Multiply color by light intensity, we are working in HDR anyway so this saves a bit of extra data that we don't have to pass to the compute shader.
            lightColor *= spotLight.intensity;

            //Do a color space conversion on the CPU side, saves a bit of extra unecessary computation in the compute shader.
            //[Gamma -> Linear] 2.2
            //[Linear -> Gamma] 0.454545
            lightColor.x = Mathf.Pow(lightColor.x, 2.2f);
            lightColor.y = Mathf.Pow(lightColor.y, 2.2f);
            lightColor.z = Mathf.Pow(lightColor.z, 2.2f);

            lightPosition = spotLight.transform.position;
            lightDirection = spotLight.transform.forward;
            lightRange = spotLight.range;
            lightAngle = spotLight.spotAngle;
            shadowRadius = spotLight.shadowRadius;
        }
    }
}
#endif