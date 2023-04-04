using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * NOTE: Using ShaderIDs are more efficent...
 * Since internally Material.SetFloat("name", value) is doing Material.SetFloat(Shader.PropertyToID("name"), value)
*/

namespace BakedVolumetrics
{
    public static class ShaderIDs
    {
        internal static readonly int ClipToView = Shader.PropertyToID("_ClipToView");
        internal static readonly int ViewProjInv = Shader.PropertyToID("_ViewProjInv");
        internal static readonly int VolumeDensity = Shader.PropertyToID("_VolumeDensity");
        internal static readonly int RaymarchStepSize = Shader.PropertyToID("_RaymarchStepSize");
        internal static readonly int RaymarchJitterStrength = Shader.PropertyToID("_RaymarchJitterStrength");
        internal static readonly int VolumeResolution = Shader.PropertyToID("_VolumeResolution");
        internal static readonly int VolumePos = Shader.PropertyToID("_VolumePos");
        internal static readonly int VolumeSize = Shader.PropertyToID("_VolumeSize");
        internal static readonly int VolumeTexture = Shader.PropertyToID("_VolumeTexture");
        internal static readonly int JitterTexture = Shader.PropertyToID("_JitterTexture");
        internal static readonly int FogColor = Shader.PropertyToID("_FogColor");
        internal static readonly int LowResDepth = Shader.PropertyToID("_LowResDepth");
        internal static readonly int DownsampleFactor = Shader.PropertyToID("_DownsampleFactor");
    }
}