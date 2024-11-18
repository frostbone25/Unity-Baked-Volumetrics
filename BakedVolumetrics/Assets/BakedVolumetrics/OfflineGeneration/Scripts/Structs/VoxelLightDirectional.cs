#if UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

namespace BakedVolumetricsOffline
{
    /// <summary>
    /// Directional Light (256 BITS | 32 BYTES)
    /// 
    /// <para>Gets the necessary data from a Unity Directional Light, to be used by the voxel tracer. </para>
    /// </summary>
    public struct VoxelLightDirectional
    {
        public Vector3 lightDirection; //96 BITS | 12 BYTES
        public Vector3 lightColor; //96 BITS | 12 BYTES
        public float shadowAngle; //32 BITS | 4 BYTES

        //https://developer.nvidia.com/content/understanding-structured-buffer-performance
        //Additional padding to the structure so that it stays divisible by 128 bits.
        public float UNUSED_0; //32 BITS | 4 BYTES

        /// <summary>
        /// Returns the total size, in bytes, occupied by an instance of this struct in memory.
        /// </summary>
        /// <returns></returns>
        public static int GetByteSize() => Marshal.SizeOf(typeof(VoxelLightDirectional));

        /// <summary>
        /// Constructor that initializes the VoxelLightDirectional instance using a Unity Light component.
        /// </summary>
        /// <param name="directionalLight"></param>
        public VoxelLightDirectional(Light directionalLight)
        {
            lightColor = new Vector3(directionalLight.color.r, directionalLight.color.g, directionalLight.color.b);

            //Multiply color by light intensity, we are working in HDR anyway so this saves a bit of extra data that we don't have to pass to the compute shader.
            lightColor *= directionalLight.intensity;

            //Do a color space conversion on the CPU side, saves a bit of extra unecessary computation in the compute shader.
            //[Gamma -> Linear] 2.2
            //[Linear -> Gamma] 0.454545
            lightColor.x = Mathf.Pow(lightColor.x, 2.2f);
            lightColor.y = Mathf.Pow(lightColor.y, 2.2f);
            lightColor.z = Mathf.Pow(lightColor.z, 2.2f);

            lightDirection = directionalLight.transform.forward;
            shadowAngle = directionalLight.shadowAngle;

            UNUSED_0 = 0;
        }
    }
}
#endif