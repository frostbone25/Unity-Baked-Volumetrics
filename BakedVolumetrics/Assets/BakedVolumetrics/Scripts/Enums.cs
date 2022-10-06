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
        Lightprobes,
        Raytraced,
        Combined,
    }

    public enum RenderingStyle
    {
        SceneObject,
        PostProcess
    }

    public enum AttenuationType
    {
        Linear,
        InverseSquare
    }
}
#endif