using UnityEngine;

namespace BakedVolumetrics
{
    public interface SampleLightInterface
    {
        public Color SampleVolumetricColor(Vector3 probePosition, Vector3 voxelWorldSize);
    }
}