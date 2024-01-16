#if UNITY_EDITOR
using System.Runtime.InteropServices;
using UnityEngine;

namespace BakedVolumetrics
{
    /// <summary>
    /// Area Light (640 BITS | 80 BYTES)
    /// 
    /// <para>Gets the necessary data from a Unity Area Light, to be used by the voxel tracer. </para>
    /// </summary>
    public struct VoxelLightArea
    {
        public Vector3 lightPosition; //96 BITS | 12 BYTES
        public Vector3 lightForwardDirection; //96 BITS | 12 BYTES
        public Vector3 lightRightDirection; //96 BITS | 12 BYTES
        public Vector3 lightUpwardDirection; //96 BITS | 12 BYTES
        public Vector2 lightSize; //64 BITS | 8 BYTES
        public Vector3 lightColor; //96 BITS | 12 BYTES
        public float lightRange; //32 BITS | 4 BYTES

        //https://developer.nvidia.com/content/understanding-structured-buffer-performance
        //Additional padding to the structure so that it stays divisible by 128 bits.
        public float UNUSED_0; //32 BITS | 4 BYTES
        public float UNUSED_1; //32 BITS | 4 BYTES

        /// <summary>
        /// Returns the total size, in bytes, occupied by an instance of this struct in memory.
        /// </summary>
        /// <returns></returns>
        public static int GetByteSize() => Marshal.SizeOf(typeof(VoxelLightArea));

        /// <summary>
        /// Constructor that initializes the VoxelLightDirectional instance using a Unity Light component.
        /// </summary>
        /// <param name="areaLight"></param>
        public VoxelLightArea(Light areaLight)
        {
            lightColor = new Vector3(areaLight.color.r, areaLight.color.g, areaLight.color.b);

            //Multiply color by light intensity, we are working in HDR anyway so this saves a bit of extra data that we don't have to pass to the compute shader.
            lightColor *= areaLight.intensity;

            //Do a color space conversion on the CPU side, saves a bit of extra unecessary computation in the compute shader.
            //[Gamma -> Linear] 2.2
            //[Linear -> Gamma] 0.454545
            lightColor.x = Mathf.Pow(lightColor.x, 2.2f);
            lightColor.y = Mathf.Pow(lightColor.y, 2.2f);
            lightColor.z = Mathf.Pow(lightColor.z, 2.2f);

            lightForwardDirection = areaLight.transform.forward;
            lightRightDirection = areaLight.transform.right;
            lightUpwardDirection = areaLight.transform.up;
            lightPosition = areaLight.transform.position;
            lightRange = areaLight.range;
            lightSize = areaLight.areaSize;

            UNUSED_0 = 0;
            UNUSED_1 = 0;
        }
    }
}
#endif