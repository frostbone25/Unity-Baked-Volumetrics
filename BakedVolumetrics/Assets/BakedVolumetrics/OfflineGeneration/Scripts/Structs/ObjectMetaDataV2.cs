#if UNITY_EDITOR
using UnityEngine;

namespace BakedVolumetricsOffline
{
    /// <summary>
    /// Object that holds the raw albedo/emissive colors of a given mesh and it's materials.
    /// </summary>
    public struct ObjectMetaDataV2
    {
        public Mesh mesh;
        public Matrix4x4 transformMatrix;
        public Bounds bounds;
        public MaterialMetaDataV2[] materials;

        public void CleanUp()
        {
            mesh = null;

            if (materials != null)
            {
                for (int j = 0; j < materials.Length; j++)
                    materials[j].ReleaseTextures();
            }

            materials = null;
        }

        public long GetDebugMemorySize()
        {
            long totalSize = 0;

            for (int i = 0; i < materials.Length; i++)
                totalSize += materials[i].GetDebugMemorySize();

            return totalSize;
        }
    }
}
#endif