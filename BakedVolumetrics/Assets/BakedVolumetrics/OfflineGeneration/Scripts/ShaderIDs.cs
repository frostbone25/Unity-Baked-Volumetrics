#if UNITY_EDITOR
using UnityEngine;

namespace BakedVolumetricsOffline
{
    public static class ShaderIDs
    {
        public static int Write => Shader.PropertyToID("Write");
        public static int AlbedoBuffer => Shader.PropertyToID("AlbedoBuffer");
        public static int EmissiveBuffer => Shader.PropertyToID("EmissiveBuffer");
        public static int NormalBuffer => Shader.PropertyToID("NormalBuffer");
        public static int PackedBuffer => Shader.PropertyToID("PackedBuffer");
        public static int _MainTex => Shader.PropertyToID("_MainTex");
        public static int KernelSize => Shader.PropertyToID("KernelSize");
        public static int unity_LightmapST => Shader.PropertyToID("unity_LightmapST");
        public static int unity_MetaVertexControl => Shader.PropertyToID("unity_MetaVertexControl");
        public static int unity_MetaFragmentControl => Shader.PropertyToID("unity_MetaFragmentControl");
        public static int unity_OneOverOutputBoost => Shader.PropertyToID("unity_OneOverOutputBoost");
        public static int unity_MaxOutputValue => Shader.PropertyToID("unity_MaxOutputValue");
        public static int unity_UseLinearSpace => Shader.PropertyToID("unity_UseLinearSpace");
        public static int Write2D => Shader.PropertyToID("Write2D");
        public static int Write3D => Shader.PropertyToID("Write3D");
        public static int AlbedoVoxelBuffer => Shader.PropertyToID("AlbedoVoxelBuffer");
        public static int EmissiveVoxelBuffer => Shader.PropertyToID("EmissiveVoxelBuffer");
        public static int NormalVoxelBuffer => Shader.PropertyToID("NormalVoxelBuffer");
        public static int PackedVoxelBuffer => Shader.PropertyToID("PackedVoxelBuffer");
        public static int AddBufferA => Shader.PropertyToID("AddBufferA");
        public static int AddBufferB => Shader.PropertyToID("AddBufferB");
        public static int BlurSamples => Shader.PropertyToID("BlurSamples");
        public static int BlurDirection => Shader.PropertyToID("BlurDirection");
        public static int VolumeResolution => Shader.PropertyToID("VolumeResolution");
        public static int Read => Shader.PropertyToID("Read");
        public static int Source3D => Shader.PropertyToID("Source3D");
        public static int Destination2D => Shader.PropertyToID("Destination2D");
        public static int SourceIndexZ => Shader.PropertyToID("SourceIndexZ");
        public static int DirectLightSurface => Shader.PropertyToID("DirectLightSurface");
        public static int SceneAlbedo => Shader.PropertyToID("SceneAlbedo");
        public static int SceneNormal => Shader.PropertyToID("SceneNormal");
        public static int VolumePosition => Shader.PropertyToID("VolumePosition");
        public static int VolumeSize => Shader.PropertyToID("VolumeSize");
        public static int MaxBounceSamples => Shader.PropertyToID("MaxBounceSamples");
        public static int IndirectIntensity => Shader.PropertyToID("IndirectIntensity");
        public static int AlbedoBoost => Shader.PropertyToID("AlbedoBoost");
        public static int DummyComputeBuffer => Shader.PropertyToID("DummyComputeBuffer");
        public static int MaxDirectSamples => Shader.PropertyToID("MaxDirectSamples");
        public static int EmissiveIntensity => Shader.PropertyToID("EmissiveIntensity");
        public static int EnvironmentMap => Shader.PropertyToID("EnvironmentMap");
        public static int MaxEnvironmentSamples => Shader.PropertyToID("MaxEnvironmentSamples");
        public static int EnvironmentIntensity => Shader.PropertyToID("EnvironmentIntensity");
        public static int CameraVoxelRender => Shader.PropertyToID("CameraVoxelRender");
        public static int AxisIndex => Shader.PropertyToID("AxisIndex");
        public static int _Cutoff => Shader.PropertyToID("_Cutoff");
    }
}
#endif