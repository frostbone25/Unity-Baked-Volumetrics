#if UNITY_EDITOR
namespace BakedVolumetrics
{
    public enum RaymarchSamples
    {
        _8,
        _16,
        _24,
        _32,
        _48,
        _64,
        _128
    }

    public enum VolumeBitDepth
    {
        RGB8,
        RGBA8,
        RGBA16,
        RGBA32
    }

    public enum CombineColorType
    {
        Additive,
        Lerp
    }

    public enum VoxelCalculation
    {
        Custom,
        Automatic
    }
    public enum LightingSource
    {
        LightProbes,
        //VoxelRaytrace,
        CPU_Raytrace,
        IBL
    }

    public enum AttenuationType
    {
        Linear,
        InverseSquare
    }

    public enum DensityType
    {
        Constant,
        Luminance,
        HeightBased,
        HeightBasedLuminance
    }

    public enum VoxelPreviewAxis
    {
        X,
        Y,
        Z
    }

    public enum AmbientLightingType
    {
        Flat,
        Skylight
    }
}
#endif