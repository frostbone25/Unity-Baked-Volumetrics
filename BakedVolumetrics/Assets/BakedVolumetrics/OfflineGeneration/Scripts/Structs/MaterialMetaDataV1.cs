#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Profiling;

namespace BakedVolumetricsOffline
{
    /// <summary>
    /// Contains two render textures that represent the raw albedo/emissive colors of an object.
    /// <para>These render textures are UV1 (Lightmap UVs) unwrapped. </para>
    /// </summary>
    public struct MaterialMetaDataV1
    {
        public RenderTexture albedoBuffer;
        public RenderTexture emissiveBuffer;

        public void ReleaseTextures()
        {
            if (albedoBuffer != null)
                albedoBuffer.Release();

            if (emissiveBuffer != null)
                emissiveBuffer.Release();
        }

        public bool isEmpty() => albedoBuffer == null || emissiveBuffer == null;

        public long GetDebugMemorySize()
        {
            long memorySize = 0;

            if(albedoBuffer != null)
                memorySize += Profiler.GetRuntimeMemorySizeLong(albedoBuffer);

            if (emissiveBuffer != null)
                memorySize += Profiler.GetRuntimeMemorySizeLong(emissiveBuffer);

            return memorySize;
        }
    }
}
#endif