#if UNITY_EDITOR
namespace BakedVolumetrics
{
    public enum LightingSource
    {
        LightProbes,
        LightProbeProxyVolume,
        VoxelRaytrace,
        CPU_Raytrace,
        IBL
    }
}
#endif