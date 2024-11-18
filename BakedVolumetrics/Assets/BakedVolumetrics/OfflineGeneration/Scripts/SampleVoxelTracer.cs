#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

/*
 * NOTE: Might be worth investing time into writing a voxel normal estimator, and a dynamically changing sample type... I'll explain
 * 
 * While generating a voxel buffer of scene normals do work, and is rather trivial there are issues with it.
 * When they are used to orient hemispheres for importance sampling, if a voxel normal is facing the wrong direction, the hemisphere will be oriented incorrectly.
 * As a result sometimes objects will appear to be just purely black or incorrect.
 * So in that case it might be better just to estimate them with the surface albedo to help alleviate this and better align hemispheres with voxels.
 * 
 * In addition to that, sometimes geometry can be only one voxel thin.
 * In that case hemisphere sampling doesn't work, and we should be switching to full sphere sampling so we can get everything around correctly.
*/

namespace BakedVolumetricsOffline
{
    public class SampleVoxelTracer
    {
        private VolumeGenerator volumeGenerator;
        private VolumeGeneratorAssets volumeGeneratorAssets;

        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - SCENE VOXELIZATION ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - SCENE VOXELIZATION ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - SCENE VOXELIZATION ||||||||||||||||||||||||||||||||||||||||||

        //this is the name of the volume used by the voxel tracer.
        //textures saved to the data folder will be prefixed with this name.
        public string voxelName => volumeGenerator.volumeName;

        //this defines the size of the volume for the voxel tracer in the scene.
        //the bigger the volume, the more data required for storage/memory, and more computation time needed for generating textures.
        public Vector3 voxelSize => volumeGenerator.volumeSize;

        //this controls the resolution of the voxels used in the voxel tracer. Default value is 1.
        //LARGER VALUES: lower voxel resolution/accuracy | faster baking times | less storage/memory required.
        //SMALLER VALUES: better voxel resolution/accuracy | longer baking times | more storage/memory required.
        public float voxelDensitySize => volumeGenerator.voxelDensitySize;

        //this determines whether during rendering we do one pass or multiple passes.
        //META EXTRACTION 1 PASS: worse buffer color accuracy | faster baking times | less memory used during voxelization
        //META EXTRACTION 3 PASS: better buffer color accuracy | slower baking times | more memory used during voxelization
        public SceneVoxelizerType sceneVoxelizerType = SceneVoxelizerType.MetaExtraction1Pass;

        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - META PASS PROPERTIES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - META PASS PROPERTIES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - META PASS PROPERTIES ||||||||||||||||||||||||||||||||||||||||||

        //this controls how many "pixels" per unit an object will have.
        //this is for "meta" textures representing the different buffers of an object (albedo, normal, emissive)
        //LARGER VALUES: more pixels allocated | better quality/accuracy | more memory usage (bigger meta textures for objects)
        //SMALLER VALUES: less pixels allocated | worse quality/accuracy | less memory usage (smaller meta textures for objects)
        public float texelDensityPerUnit = 4;

        //minimum resolution for meta textures captured from objects in the scene (so objects too small will be capped to this value resolution wise)
        //LARGER VALUES: more pixels allocated at minimum for object meta textures | better quality/accuracy | more memory usage (bigger meta textures for objects)
        //SMALLER VALUES: less pixels allocated at minimum for object meta textures | worse quality/accuracy | less memory usage (smaller meta textures for objects)
        public int minimumBufferResolution = 16;

        //this controls whether or not pixel dilation will be performed for each meta texture buffer.
        //this is done for "meta" tetures representing diferent buffers of an object (albedo, normal, emissive)
        //this is highly recomended because meta textures will be low resolution inherently, and without it the textures won't fit perfectly into the UV space due to pixlation.
        //as a result you will get black outlines on the borders of the UV atlases which will pollute the results of each buffer
        //ENABLED: this will perform dilation on meta textures | slightly slower voxelization
        //DISABLED: this will NOT do dilation on meta textures | slightly faster voxelization
        public bool performDilation = true;

        //max dilation size for the dilation radius, the higher it is the broader the dilation filter will cover.
        //LARGER VALUES: larger dilation radius | better dilation quality/accuracy
        //SMALLER VALUES: smaller dilation radius | worse dilation quality/accuracy
        public int dilationPixelSize = 128;

        //[NOTE]: (META EXTRACTION 3 PASS ONLY) Use bilinear filtering when rendering meta textures.
        //This can smooth out meta textures and improve quality, as well as reduce black outlines.
        //ENABLED: Bilinear filtering used on meta textures during rendering
        //DISABLED: Point filtering used on meta textures during rendering
        public bool useBilinearFiltering = true;

        //[NOTE]: (META EXTRACTION 3 PASS ONLY) Use half precison for emission format.
        //Saves additional memory/storage space at the cost of precison.
        //ENABLED: Emisison Precision drops to 16 bit | less accuracy | less memory/storage
        //DISABLED: Emisison Precision at 32 bit | more accuracy | more memory/storage
        public bool emissionHalfPrecision = true;

        //[NOTE]: (META EXTRACTION 1 PASS ONLY) Encoding Type used to compress emission HDR colors.
        //Emission HDR is packed into 8 bit per channel to save on rendering passes, at the cost of accuracy.
        public HDREncoding emissionHDREncoding = HDREncoding.RGBM;

        //[NOTE]: (META EXTRACTION 1 PASS ONLY) Value range for RGBM/RGBD to compress emission HDR colors.
        //LARGER VALUES: larger brightness range | less accuracy/quality
        //SMALLER VALUES: smaller brightness range | more accuracy/quality
        public float emissionHDREncodingRange = 6.0f;

        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - VOXEL RENDERING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - VOXEL RENDERING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - VOXEL RENDERING ||||||||||||||||||||||||||||||||||||||||||

        //this will perform blending with multiple captured voxel slices of the scene albedo buffer
        //the scene is captured in multiple slices in 6 different axis's, "overdraw" happens for alot of pixels.
        //so during voxelization if a pixel already has data written, we write again but blend with the original result, in theory this should lead to better accuracy of the buffer because each slice depending on the axis is not the exact same every time.
        //ENABLED: averages multiple slices if there is overdraw of pixels, potentially better accuracy.
        //DISABLED: on each slice, only the first instance of the color is written, if the same pixel is drawn then it's ignored.
        public bool blendAlbedoVoxelSlices = true;

        //this will perform blending with multiple captured voxel slices of the scene emissive buffer
        //the scene is captured in multiple slices in 6 different axis's, "overdraw" happens for alot of pixels.
        //so during voxelization if a pixel already has data written, we write again but blend with the original result, in theory this should lead to better accuracy of the buffer because each slice depending on the axis is not the exact same every time.
        //ENABLED: averages multiple slices if there is overdraw of pixels, potentially better accuracy.
        //DISABLED: on each slice, only the first instance of the color is written, if the same pixel is drawn then it's ignored.
        //NOTE: This could lead to inaccuracy on some surfaces and could create skewed results since some surfaces depending on how they are captured, will have their vectors altered.
        public bool blendEmissiveVoxelSlices = true;

        //this will perform blending with multiple captured voxel slices of the scene albedo buffer
        //the scene is captured in multiple slices in 6 different axis's, "overdraw" happens for alot of pixels.
        //so during voxelization if a pixel already has data written, we write again but blend with the original result, in theory this should lead to better accuracy of the buffer because each slice depending on the axis is not the exact same every time.
        //ENABLED: averages multiple slices if there is overdraw of pixels, potentially better accuracy.
        //DISABLED: on each slice, only the first instance of the color is written, if the same pixel is drawn then it's ignored.
        public bool blendNormalVoxelSlices = false;

        //this determines whether or not geometry in the scene can be seen from both sides.
        //this is on by default because its good at thickening geometry in the scene and reducing holes/cracks.
        //ENABLED: scene is voxelized with geometry visible on all sides with no culling.
        //DISABLED: scene is voxelized with geometry visible only on the front face, back faces are culled and invisible.
        public bool doubleSidedGeometry = true;

        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - VOXEL OPTIMIZATIONS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - VOXEL OPTIMIZATIONS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - VOXEL OPTIMIZATIONS ||||||||||||||||||||||||||||||||||||||||||

        //this will only use mesh renderers that are marked "Contribute Global Illumination".
        //ENABLED: this will only use meshes in the scene marked for GI | faster voxelization | less memory usage (less objects needing meta textures)
        //DISABLED: every mesh renderer in the scene will be used | slower voxelization | more memory usage (more objects needing meta textures)
        public bool onlyUseGIContributors = true;

        //this will only use mesh renderers that have shadow casting enabled.
        //ENABLED: this will only use meshes in the scene marked for GI | faster voxelization | less memory usage (less objects needing meta textures)
        //DISABLED: every mesh renderer in the scene will be used | slower voxelization | more memory usage (more objects needing meta textures)
        public bool onlyUseShadowCasters = true;

        //only use meshes that are within voxelization bounds
        //ENABLED: only objects within voxelization bounds will be used | faster voxelization | less memory usage (less objects needing meta textures)
        //DISABLED: all objects in the scene will be used for voxelization | slower voxelization | more memory usage (more objects needing meta textures)
        public bool onlyUseMeshesWithinBounds = true;

        //use the bounding boxes on meshes during "voxelization" to render only what is visible
        //ENABLED: renders objects only visible in each voxel slice | much faster voxelization
        //DISABLED: renders all objects | much slower voxelization |
        public bool useBoundingBoxCullingForRendering = true;

        //only use objects that match the layer mask requirements
        public LayerMask objectLayerMask = 1;

        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - ENVIRONMENT OPTIONS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - ENVIRONMENT OPTIONS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - ENVIRONMENT OPTIONS ||||||||||||||||||||||||||||||||||||||||||

        //Should we calculate environment lighting?
        public bool enableEnvironmentLighting = true;

        //Resolution of the captured environment cubemap.
        [Range(32, 4096)] public int environmentResolution = 128;

        //Custom environment cubemap if the user wants to input their own.
        public Cubemap customEnvironmentMap;

        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - BAKE OPTIONS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - BAKE OPTIONS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES - BAKE OPTIONS ||||||||||||||||||||||||||||||||||||||||||

        public LayerMask lightLayerMask = 1;

        [Range(1, 8192)] public int directSurfaceSamples = 128;
        [Range(1, 8192)] public int directVolumetricSamples = 128;
        [Range(1, 8192)] public int environmentSurfaceSamples = 64;
        [Range(1, 8192)] public int environmentVolumetricSamples = 64;
        [Range(1, 8192)] public int bounceSurfaceSamples = 128;
        [Range(1, 8192)] public int bounceVolumetricSamples = 64;

        //Amount of surface shading bounces to do.
        [Range(1, 8)] public int bounces = 4;

        //Improve surface shading quality by using a cosine hemisphere oriented with the surface normal.
        //Results in better ray allocation at lower sample counts (though at the moment there are issues with scene normals)
        public bool normalOrientedHemisphereSampling = true;

        //[Header("Artistic Controls")]

        [Range(1, 10)] public float albedoBoost = 1; //1 is default, physically accurate.
        [Range(0, 5)] public float indirectIntensity = 1; //1 is default, physically accurate.
        [Range(0, 8)] public float environmentIntensity = 1; //1 is default, physically accurate.
        [Range(0, 8)] public float emissiveIntensity = 1; //1 is default, physically accurate.

        public LightAttenuationType lightAttenuationType = LightAttenuationType.UnityFalloff;

        //[Header("Misc")]

        public bool halfPrecisionLighting = false;

        //Enables an intentional CPU staller.
        //This is a bit of a hack, but a necessary one that will intentionally stall the CPU after X amount of compute shader dispatches.
        //The compute shaders we use can get rather expensive, and issuing too many expensive workloads to the GPU can cause TDR/Driver timeouts and crash the editor.
        //To get around it, we deliberaly stall the CPU by issuing a GPU Readback call to get data back from the GPU.
        //If we get data back from the GPU that means the GPU is ready for more work and it completed whatever prior task it had.
        public bool enableGPU_Readback_Limit = true;

        //(If enabled) This adjusts the limit to how many times we do a GPU readback to stall the CPU after X amount of samples.
        [Range(1, 32)] public int GPU_Readback_Limit = 4;

        //[Header("Post Bake Options")]
        //Applies a 3D gaussian blur to volumetric light terms to smooth results out.
        //High samples though means that leaks can occur as this is not voxel/geometry aware.

        [Range(0, 64)] public int volumetricDirectGaussianSamples = 0;
        [Range(0, 64)] public int volumetricBounceGaussianSamples = 0;
        [Range(0, 64)] public int volumetricEnvironmentGaussianSamples = 0;

        //[Header("Gizmos")]
        public bool previewBounds = true;

        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||

        public Vector3Int voxelResolution => volumeGenerator.GetVoxelResolution();
        private Bounds voxelBounds => volumeGenerator.voxelBounds;

        private uint THREAD_GROUP_SIZE_X = 0;
        private uint THREAD_GROUP_SIZE_Y = 0;
        private uint THREAD_GROUP_SIZE_Z = 0;
        private RenderTextureConverter renderTextureConverter;
        private MetaPassRenderingV1 metaPassRendererV1;
        private MetaPassRenderingV2 metaPassRendererV2;

        private ComputeShader voxelDirectSurfaceLight => volumeGeneratorAssets.voxelDirectSurfaceLight;
        private ComputeShader voxelDirectVolumetricLight => volumeGeneratorAssets.voxelDirectVolumetricLight;
        private ComputeShader voxelBounceSurfaceLight => volumeGeneratorAssets.voxelBounceSurfaceLight;
        private ComputeShader voxelBounceVolumetricLight => volumeGeneratorAssets.voxelBounceVolumetricLight;
        private ComputeShader voxelEnvironmentSurfaceLight => volumeGeneratorAssets.voxelEnvironmentSurfaceLight;
        private ComputeShader voxelEnvironmentVolumetricLight => volumeGeneratorAssets.voxelEnvironmentVolumetricLight;
        private ComputeShader combineBuffers => volumeGeneratorAssets.combineBuffers;
        private ComputeShader gaussianBlur => volumeGeneratorAssets.gaussianBlur;
        private ComputeShader voxelizeScene => volumeGeneratorAssets.voxelizeScene;
        private ComputeShader dilate => volumeGeneratorAssets.dilate;

        private Texture3D voxelAlbedoBuffer;
        private Texture3D voxelNormalBuffer;
        private Texture3D voxelEmissiveBuffer;
        private Texture3D voxelDirectLightSurfaceBuffer;
        private Texture3D voxelDirectLightSurfaceAlbedoBuffer;
        private Texture3D voxelDirectLightVolumeBuffer;
        private Texture3D voxelEnvironmentLightSurfaceBuffer;
        private Texture3D voxelEnvironmentLightSurfaceAlbedoBuffer;
        private Texture3D voxelEnvironmentLightVolumeBuffer;
        private Texture3D voxelCombinedDirectLightSurfaceBuffer;
        private Texture3D voxelCombinedDirectLightSurfaceAlbedoBuffer;
        private Texture3D voxelBounceLightSurfaceBuffer;
        private Texture3D voxelBounceLightSurfaceAlbedoBuffer;
        private Texture3D voxelBounceLightVolumeBuffer;

        private Cubemap environmentMap;

        private string voxelAlbedoBufferAssetPath => string.Format("{0}/{1}_albedo.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);
        private string voxelNormalBufferAssetPath => string.Format("{0}/{1}_normal.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);
        private string voxelEmissiveBufferAssetPath => string.Format("{0}/{1}_emissive.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);

        private string voxelDirectLightSurfaceBufferAssetPath => string.Format("{0}/{1}_directSurface.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);
        private string voxelDirectLightSurfaceAlbedoBufferAssetPath => string.Format("{0}/{1}_directSurfaceAlbedo.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);
        private string voxelDirectLightVolumeBufferAssetPath => string.Format("{0}/{1}_directVolumetric.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);

        private string voxelEnvironmentLightSurfaceBufferAssetPath => string.Format("{0}/{1}_environmentSurface.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);
        private string voxelEnvironmentLightSurfaceAlbedoBufferAssetPath => string.Format("{0}/{1}_environmentSurfaceAlbedo.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);
        private string voxelEnvironmentLightVolumetricBufferAssetPath => string.Format("{0}/{1}_environmentVolumetric.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);

        private string voxelCombinedDirectLightSurfaceBufferAssetPath => string.Format("{0}/{1}_combinedDirectLightSurface.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);
        private string voxelCombinedDirectLightSurfaceAlbedoBufferAssetPath => string.Format("{0}/{1}_combinedDirectLightSurfaceAlbedo.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);

        private string voxelBounceLightSurfaceBufferAssetPath => string.Format("{0}/{1}_bounceSurface.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);
        private string voxelBounceLightSurfaceAlbedoBufferAssetPath => string.Format("{0}/{1}_bounceSurfaceAlbedo.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);
        private string voxelBounceLightVolumeBufferAssetPath => string.Format("{0}/{1}_bounceVolumetric.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);

        private string voxelCombinedVolumetricBufferAssetPath => string.Format("{0}/{1}_combinedVolumetric.asset", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);
        private string environmentMapAssetPath => string.Format("{0}/{1}_environment.exr", volumeGeneratorAssets.localAssetSceneDataFolder, voxelName);

        private GameObject voxelCameraGameObject;
        private Camera voxelCamera;

        private ComputeBuffer directionalLightsBuffer = null;
        private ComputeBuffer pointLightsBuffer = null;
        private ComputeBuffer spotLightsBuffer = null;
        private ComputeBuffer areaLightsBuffer = null;

        //private static RenderTextureFormat metaPackedFormat = RenderTextureFormat.ARGB64;
        private static GraphicsFormat metaPackedFormat = GraphicsFormat.R16G16B16A16_UNorm;

        private static RenderTextureFormat unpackedAlbedoBufferFormat = RenderTextureFormat.ARGB32; //NOTE: ARGB1555 is unsupported for random writes
        private RenderTextureFormat unpackedEmissiveBufferFormat => emissionHalfPrecision ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGBFloat;
        private static RenderTextureFormat unpackedNormalBufferFormat = RenderTextureFormat.ARGB32; //NOTE: ARGB1555 is unsupported for random writes

        private TextureFormat textureFormat => halfPrecisionLighting ? TextureFormat.RGBAHalf : TextureFormat.RGBAFloat;
        private RenderTextureFormat renderTextureFormat => halfPrecisionLighting ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGBFloat;

        //|||||||||||||||||||||||||||||||||||||||||| CONSTRUCTOR ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CONSTRUCTOR ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CONSTRUCTOR ||||||||||||||||||||||||||||||||||||||||||

        public SampleVoxelTracer(VolumeGenerator volumeGenerator, VolumeGeneratorAssets volumeGeneratorAssets)
        {
            this.volumeGenerator = volumeGenerator;
            this.volumeGeneratorAssets = volumeGeneratorAssets;

            renderTextureConverter = new RenderTextureConverter();
        }

        public Texture3D GetFinalGeneratedVolume() => AssetDatabase.LoadAssetAtPath<Texture3D>(voxelCombinedVolumetricBufferAssetPath);

        /// <summary>
        /// Loads in all of the generated textures from the voxel tracer.
        /// <para>If some don't exist, they are just simply null.</para>
        /// </summary>
        public void GetGeneratedContent()
        {
            voxelAlbedoBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelAlbedoBufferAssetPath);
            voxelNormalBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelNormalBufferAssetPath);
            voxelEmissiveBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelEmissiveBufferAssetPath);
            voxelDirectLightSurfaceBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelDirectLightSurfaceBufferAssetPath);
            voxelDirectLightSurfaceAlbedoBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelDirectLightSurfaceAlbedoBufferAssetPath);
            voxelDirectLightVolumeBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelDirectLightVolumeBufferAssetPath);

            if (enableEnvironmentLighting)
            {
                voxelEnvironmentLightSurfaceBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelEnvironmentLightSurfaceBufferAssetPath);
                voxelEnvironmentLightSurfaceAlbedoBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelEnvironmentLightSurfaceAlbedoBufferAssetPath);
                voxelEnvironmentLightVolumeBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelEnvironmentLightVolumetricBufferAssetPath);
            }

            voxelCombinedDirectLightSurfaceBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelCombinedDirectLightSurfaceBufferAssetPath);
            voxelCombinedDirectLightSurfaceAlbedoBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelCombinedDirectLightSurfaceAlbedoBufferAssetPath);
            voxelBounceLightSurfaceBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelBounceLightSurfaceBufferAssetPath);
            voxelBounceLightSurfaceAlbedoBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelBounceLightSurfaceAlbedoBufferAssetPath);
            voxelBounceLightVolumeBuffer = AssetDatabase.LoadAssetAtPath<Texture3D>(voxelBounceLightVolumeBufferAssetPath);
        }

        public void CleanUpGeneratedContent()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelAlbedoBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelAlbedoBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelNormalBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelNormalBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelEmissiveBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelEmissiveBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelDirectLightSurfaceBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelDirectLightSurfaceBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelDirectLightSurfaceAlbedoBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelDirectLightSurfaceAlbedoBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelDirectLightVolumeBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelDirectLightVolumeBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelEnvironmentLightSurfaceBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelEnvironmentLightSurfaceBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelEnvironmentLightSurfaceAlbedoBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelEnvironmentLightSurfaceAlbedoBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelEnvironmentLightVolumetricBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelEnvironmentLightVolumetricBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelCombinedDirectLightSurfaceBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelCombinedDirectLightSurfaceBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelCombinedDirectLightSurfaceAlbedoBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelCombinedDirectLightSurfaceAlbedoBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelBounceLightSurfaceBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelBounceLightSurfaceBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelBounceLightSurfaceAlbedoBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelBounceLightSurfaceAlbedoBufferAssetPath);
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(voxelBounceLightVolumeBufferAssetPath) != null) AssetDatabase.DeleteAsset(voxelBounceLightVolumeBufferAssetPath);

            if (voxelAlbedoBuffer != null) MonoBehaviour.DestroyImmediate(voxelAlbedoBuffer);
            if (voxelNormalBuffer != null) MonoBehaviour.DestroyImmediate(voxelNormalBuffer);
            if (voxelEmissiveBuffer != null) MonoBehaviour.DestroyImmediate(voxelEmissiveBuffer);
            if (voxelDirectLightSurfaceBuffer != null) MonoBehaviour.DestroyImmediate(voxelDirectLightSurfaceBuffer);
            if (voxelDirectLightSurfaceAlbedoBuffer != null) MonoBehaviour.DestroyImmediate(voxelDirectLightSurfaceAlbedoBuffer);
            if (voxelDirectLightVolumeBuffer != null) MonoBehaviour.DestroyImmediate(voxelDirectLightVolumeBuffer);
            if (voxelEnvironmentLightSurfaceBuffer != null) MonoBehaviour.DestroyImmediate(voxelEnvironmentLightSurfaceBuffer);
            if (voxelEnvironmentLightSurfaceAlbedoBuffer != null) MonoBehaviour.DestroyImmediate(voxelEnvironmentLightSurfaceAlbedoBuffer);
            if (voxelEnvironmentLightVolumeBuffer != null) MonoBehaviour.DestroyImmediate(voxelEnvironmentLightVolumeBuffer);
            if (voxelCombinedDirectLightSurfaceBuffer != null) MonoBehaviour.DestroyImmediate(voxelCombinedDirectLightSurfaceBuffer);
            if (voxelCombinedDirectLightSurfaceAlbedoBuffer != null) MonoBehaviour.DestroyImmediate(voxelCombinedDirectLightSurfaceAlbedoBuffer);
            if (voxelBounceLightSurfaceBuffer != null) MonoBehaviour.DestroyImmediate(voxelBounceLightSurfaceBuffer);
            if (voxelBounceLightSurfaceAlbedoBuffer != null) MonoBehaviour.DestroyImmediate(voxelBounceLightSurfaceAlbedoBuffer);
            if (voxelBounceLightVolumeBuffer != null) MonoBehaviour.DestroyImmediate(voxelBounceLightVolumeBuffer);

            voxelAlbedoBuffer = null;
            voxelNormalBuffer = null;
            voxelEmissiveBuffer = null;
            voxelDirectLightSurfaceBuffer = null;
            voxelDirectLightSurfaceAlbedoBuffer = null;
            voxelDirectLightVolumeBuffer = null;
            voxelEnvironmentLightSurfaceBuffer = null;
            voxelEnvironmentLightSurfaceAlbedoBuffer = null;
            voxelEnvironmentLightVolumeBuffer = null;
            voxelCombinedDirectLightSurfaceBuffer = null;
            voxelCombinedDirectLightSurfaceAlbedoBuffer = null;
            voxelBounceLightSurfaceBuffer = null;
            voxelBounceLightSurfaceAlbedoBuffer = null;
            voxelBounceLightVolumeBuffer = null;
        }

        /// <summary>
        /// Gets all Unity Lights in the scene, and builds compute buffers of them.
        /// <para>This is used only when doing Direct Light tracing.</para>
        /// </summary>
        public void BuildLightComputeBuffers()
        {
            //|||||||||||||||||||||||||||||||||||||||||| GET SCENE LIGHTS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| GET SCENE LIGHTS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| GET SCENE LIGHTS ||||||||||||||||||||||||||||||||||||||||||

            List<VoxelLightDirectional> voxelLightDirectionals = new List<VoxelLightDirectional>();
            List<VoxelLightPoint> voxelLightPoints = new List<VoxelLightPoint>();
            List<VoxelLightSpot> voxelLightSpots = new List<VoxelLightSpot>();
            List<VoxelLightArea> voxelLightAreas = new List<VoxelLightArea>();

            foreach (Light sceneLight in MonoBehaviour.FindObjectsOfType<Light>())
            {
                if (lightLayerMask == (lightLayerMask | (1 << sceneLight.gameObject.layer)) == false)
                    continue;

                if (sceneLight.type == LightType.Directional)
                {
                    VoxelLightDirectional voxelLightDirectional = new VoxelLightDirectional(sceneLight);
                    voxelLightDirectionals.Add(voxelLightDirectional);
                }
                else if (sceneLight.type == LightType.Point)
                {
                    VoxelLightPoint voxelLightPoint = new VoxelLightPoint(sceneLight);
                    voxelLightPoints.Add(voxelLightPoint);
                }
                else if (sceneLight.type == LightType.Spot)
                {
                    VoxelLightSpot voxelLightSpot = new VoxelLightSpot(sceneLight);
                    voxelLightSpots.Add(voxelLightSpot);
                }
                else if (sceneLight.type == LightType.Area)
                {
                    VoxelLightArea voxelLightArea = new VoxelLightArea(sceneLight);
                    voxelLightAreas.Add(voxelLightArea);
                }
            }

            //|||||||||||||||||||||||||||||||||||||||||| CLEAR LIGHT BUFFERS (If Used) ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| CLEAR LIGHT BUFFERS (If Used) ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| CLEAR LIGHT BUFFERS (If Used) ||||||||||||||||||||||||||||||||||||||||||
            //just making sure that these are absolutely cleared and cleaned up.

            if (directionalLightsBuffer != null)
            {
                directionalLightsBuffer.Release();
                directionalLightsBuffer.Dispose();
            }

            if (pointLightsBuffer != null)
            {
                pointLightsBuffer.Release();
                pointLightsBuffer.Dispose();
            }

            if (spotLightsBuffer != null)
            {
                spotLightsBuffer.Release();
                spotLightsBuffer.Dispose();
            }

            if (areaLightsBuffer != null)
            {
                areaLightsBuffer.Release();
                areaLightsBuffer.Dispose();
            }

            directionalLightsBuffer = null;
            pointLightsBuffer = null;
            spotLightsBuffer = null;
            areaLightsBuffer = null;

            //|||||||||||||||||||||||||||||||||||||||||| BUILD SCENE LIGHT BUFFERS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| BUILD SCENE LIGHT BUFFERS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| BUILD SCENE LIGHT BUFFERS ||||||||||||||||||||||||||||||||||||||||||

            //build directional light buffer
            if (voxelLightDirectionals.Count > 0)
            {
                directionalLightsBuffer = new ComputeBuffer(voxelLightDirectionals.Count, VoxelLightDirectional.GetByteSize());
                directionalLightsBuffer.SetData(voxelLightDirectionals.ToArray());
            }

            //build point light buffer
            if (voxelLightPoints.Count > 0)
            {
                pointLightsBuffer = new ComputeBuffer(voxelLightPoints.Count, VoxelLightPoint.GetByteSize());
                pointLightsBuffer.SetData(voxelLightPoints.ToArray());
            }

            //build spot light buffer
            if (voxelLightSpots.Count > 0)
            {
                spotLightsBuffer = new ComputeBuffer(voxelLightSpots.Count, VoxelLightSpot.GetByteSize());
                spotLightsBuffer.SetData(voxelLightSpots.ToArray());
            }

            //build area light buffer
            if (voxelLightAreas.Count > 0)
            {
                areaLightsBuffer = new ComputeBuffer(voxelLightAreas.Count, VoxelLightArea.GetByteSize());
                areaLightsBuffer.SetData(voxelLightAreas.ToArray());
            }

            Debug.Log(string.Format("[Directional: {0}] [Spot: {1}] [Point: {2}] [Area: {3}]", voxelLightDirectionals.Count, voxelLightSpots.Count, voxelLightPoints.Count, voxelLightAreas.Count));
        }

        //|||||||||||||||||||||||||||||||||||||||||| STEP 0: SETUP VOXEL CAPTURE ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 0: SETUP VOXEL CAPTURE ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 0: SETUP VOXEL CAPTURE ||||||||||||||||||||||||||||||||||||||||||
        //This is where we construct our voxel capture rig that will be used when we voxelize the scene in step 1.

        /// <summary>
        /// Creates a GameObject with a Camera for Voxel Capture.
        /// </summary>
        public void CreateVoxelCamera()
        {
            if (voxelCameraGameObject == null)
                voxelCameraGameObject = new GameObject("VoxelizeSceneCamera");

            if (voxelCamera == null)
                voxelCamera = voxelCameraGameObject.AddComponent<Camera>();

            voxelCamera.enabled = false;
            voxelCamera.forceIntoRenderTexture = true;
            voxelCamera.useOcclusionCulling = false;
            voxelCamera.allowMSAA = false;
            voxelCamera.orthographic = true;
            voxelCamera.nearClipPlane = 0.0f;
            voxelCamera.farClipPlane = voxelDensitySize;
            voxelCamera.clearFlags = CameraClearFlags.Color;
            voxelCamera.backgroundColor = new Color(0, 0, 0, 0);
            voxelCamera.depthTextureMode = DepthTextureMode.None;
            voxelCamera.renderingPath = RenderingPath.Forward;
        }

        /// <summary>
        /// Destroys the Voxel Camera.
        /// </summary>
        public void CleanupVoxelCamera()
        {
            if (voxelCameraGameObject != null)
                MonoBehaviour.DestroyImmediate(voxelCameraGameObject);

            if (voxelCamera != null)
                MonoBehaviour.DestroyImmediate(voxelCamera);

            voxelCameraGameObject = null;
            voxelCamera = null;
        }

        //|||||||||||||||||||||||||||||||||||||||||| STEP 1: CAPTURE ALBEDO/EMISSIVE VOXEL BUFFERS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 1: CAPTURE ALBEDO/EMISSIVE VOXEL BUFFERS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 1: CAPTURE ALBEDO/EMISSIVE VOXEL BUFFERS ||||||||||||||||||||||||||||||||||||||||||
        //This is where we voxelize the current scene within the volume bounds.
        // - [Albedo Buffer]
        // This is used for the main scene color (RGB), but it is also used importantly for occlusion checking (A) when tracing.
        //
        // - [Emissive Buffer]
        // This is used to capture any emissive materials in the scene.
        // This is added in the direct light pass, and it's actual emission of light is calculated in the bounce lighting phase.
        //
        // - [Normal Buffer]
        // This is used only when 'normalOrientedHemisphereSampling' is enabled to orient cosine hemispheres when calculating bounce surface lighting.

        public void GenerateAlbedoEmissiveNormalBuffers()
        {
            volumeGeneratorAssets.PrepareAssetFolders();
            volumeGeneratorAssets.GetResources();

            if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass)
            {
                metaPassRendererV1 = new MetaPassRenderingV1(volumeGeneratorAssets);
                metaPassRendererV1.dilationPixelSize = dilationPixelSize;
                metaPassRendererV1.minimumBufferResolution = minimumBufferResolution;
                metaPassRendererV1.objectLayerMask = objectLayerMask;
                metaPassRendererV1.onlyUseGIContributors = onlyUseGIContributors;
                metaPassRendererV1.onlyUseMeshesWithinBounds = onlyUseMeshesWithinBounds;
                metaPassRendererV1.onlyUseShadowCasters = onlyUseShadowCasters;
                metaPassRendererV1.performDilation = performDilation;
                metaPassRendererV1.texelDensityPerUnit = texelDensityPerUnit;
                metaPassRendererV1.useBoundingBoxCullingForRendering = useBoundingBoxCullingForRendering;
                metaPassRendererV1.sceneObjectsBounds = voxelBounds;
                metaPassRendererV1.doubleSidedGeometry = doubleSidedGeometry;
                metaPassRendererV1.useBilinearFiltering = useBilinearFiltering;
                metaPassRendererV1.emissionHalfPrecision = emissionHalfPrecision;
            }
            else if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction1Pass)
            {
                metaPassRendererV2 = new MetaPassRenderingV2(volumeGeneratorAssets);
                metaPassRendererV2.dilationPixelSize = dilationPixelSize;
                metaPassRendererV2.minimumBufferResolution = minimumBufferResolution;
                metaPassRendererV2.objectLayerMask = objectLayerMask;
                metaPassRendererV2.onlyUseGIContributors = onlyUseGIContributors;
                metaPassRendererV2.onlyUseMeshesWithinBounds = onlyUseMeshesWithinBounds;
                metaPassRendererV2.onlyUseShadowCasters = onlyUseShadowCasters;
                metaPassRendererV2.performDilation = performDilation;
                metaPassRendererV2.texelDensityPerUnit = texelDensityPerUnit;
                metaPassRendererV2.useBoundingBoxCullingForRendering = useBoundingBoxCullingForRendering;
                metaPassRendererV2.sceneObjectsBounds = voxelBounds;
                metaPassRendererV2.doubleSidedGeometry = doubleSidedGeometry;
                metaPassRendererV2.emissionHDREncoding = emissionHDREncoding;
                metaPassRendererV2.emissionHDREncodingRange = emissionHDREncodingRange;
            }

            VolumeGeneratorUtility.UpdateProgressBar("Preparing to generate albedo/normal/emissive...", 0.5f);

            float timeBeforeFunction = Time.realtimeSinceStartup;

            CreateVoxelCamera(); //Create our voxel camera rig

            int renderTextureDepthBits = 32; //bits for the render texture used by the voxel camera (technically 16 does just fine?)

            VolumeGeneratorUtility.UpdateProgressBar("Building object meta buffers...", 0.5f);

            List<ObjectMetaDataV1> objectMetaDataV1 = new List<ObjectMetaDataV1>();
            List<ObjectMetaDataV2> objectMetaDataV2 = new List<ObjectMetaDataV2>();

            if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass)
                objectMetaDataV1 = metaPassRendererV1.ExtractSceneObjectMetaBuffers();
            else if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction1Pass)
                objectMetaDataV2 = metaPassRendererV2.ExtractSceneObjectMetaBuffers();

            //|||||||||||||||||||||||||||||||||||||| PREPARE SCENE VOXELIZATION ||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||| PREPARE SCENE VOXELIZATION ||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||| PREPARE SCENE VOXELIZATION ||||||||||||||||||||||||||||||||||||||

            VolumeGeneratorUtility.UpdateProgressBar("Rendering scene...", 0.5f);

            //compute per voxel position offset values.
            float xOffset = voxelSize.x / voxelResolution.x;
            float yOffset = voxelSize.y / voxelResolution.y;
            float zOffset = voxelSize.z / voxelResolution.z;

            //pre-fetch our voxelize kernel function in the compute shader.
            int ComputeShader_VoxelizeScene_X_POS = voxelizeScene.FindKernel("ComputeShader_VoxelizeScene_X_POS");
            int ComputeShader_VoxelizeScene_X_NEG = voxelizeScene.FindKernel("ComputeShader_VoxelizeScene_X_NEG");
            int ComputeShader_VoxelizeScene_Y_POS = voxelizeScene.FindKernel("ComputeShader_VoxelizeScene_Y_POS");
            int ComputeShader_VoxelizeScene_Y_NEG = voxelizeScene.FindKernel("ComputeShader_VoxelizeScene_Y_NEG");
            int ComputeShader_VoxelizeScene_Z_POS = voxelizeScene.FindKernel("ComputeShader_VoxelizeScene_Z_POS");
            int ComputeShader_VoxelizeScene_Z_NEG = voxelizeScene.FindKernel("ComputeShader_VoxelizeScene_Z_NEG");

            //make sure the voxelize shader knows our voxel resolution beforehand.
            voxelizeScene.SetVector(ShaderIDs.VolumeResolution, new Vector4(voxelResolution.x, voxelResolution.y, voxelResolution.z, 0));

            //create our 3D render texture, which will be accumulating 2D slices of the scene captured at various axis.
            RenderTexture sceneAlbedo = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, unpackedAlbedoBufferFormat);
            sceneAlbedo.dimension = TextureDimension.Tex3D;
            sceneAlbedo.filterMode = FilterMode.Point;
            sceneAlbedo.wrapMode = TextureWrapMode.Clamp;
            sceneAlbedo.volumeDepth = voxelResolution.z;
            sceneAlbedo.enableRandomWrite = true;
            sceneAlbedo.Create();

            RenderTexture sceneEmissive = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, unpackedEmissiveBufferFormat);
            sceneEmissive.dimension = TextureDimension.Tex3D;
            sceneEmissive.filterMode = FilterMode.Point;
            sceneEmissive.wrapMode = TextureWrapMode.Clamp;
            sceneEmissive.volumeDepth = voxelResolution.z;
            sceneEmissive.enableRandomWrite = true;
            sceneEmissive.Create();

            RenderTexture sceneNormal = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, unpackedNormalBufferFormat);
            sceneNormal.dimension = TextureDimension.Tex3D;
            sceneNormal.filterMode = FilterMode.Point;
            sceneNormal.wrapMode = TextureWrapMode.Clamp;
            sceneNormal.volumeDepth = voxelResolution.z;
            sceneNormal.enableRandomWrite = true;
            sceneNormal.Create();

            float timeBeforeRendering = Time.realtimeSinceStartup;

            //||||||||||||||||||||||||||||||||| X AXIS SETUP |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| X AXIS SETUP |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| X AXIS SETUP |||||||||||||||||||||||||||||||||
            //captures the scene on the X axis.

            using (CommandBuffer sceneAlbedoCommandBuffer = new CommandBuffer())
            {
                //create a 2D render texture based off our voxel resolution to capture the scene in the X axis.
                RenderTexture voxelPackedCameraSlice = new RenderTexture(voxelResolution.z, voxelResolution.y, renderTextureDepthBits, metaPackedFormat);
                voxelPackedCameraSlice.filterMode = FilterMode.Point;
                voxelPackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                voxelPackedCameraSlice.enableRandomWrite = true;
                voxelCamera.targetTexture = voxelPackedCameraSlice;
                voxelCamera.orthographicSize = voxelSize.y * 0.5f;

                RenderTexture albedoUnpackedCameraSlice = new RenderTexture(voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, renderTextureDepthBits, metaPackedFormat);
                albedoUnpackedCameraSlice.filterMode = FilterMode.Point;
                albedoUnpackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                albedoUnpackedCameraSlice.enableRandomWrite = true;
                albedoUnpackedCameraSlice.Create();

                RenderTexture emissiveUnpackedCameraSlice = new RenderTexture(voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, renderTextureDepthBits, metaPackedFormat);
                emissiveUnpackedCameraSlice.filterMode = FilterMode.Point;
                emissiveUnpackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                emissiveUnpackedCameraSlice.enableRandomWrite = true;
                emissiveUnpackedCameraSlice.Create();

                RenderTexture normalUnpackedCameraSlice = new RenderTexture(voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, renderTextureDepthBits, metaPackedFormat);
                normalUnpackedCameraSlice.filterMode = FilterMode.Point;
                normalUnpackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                normalUnpackedCameraSlice.enableRandomWrite = true;
                normalUnpackedCameraSlice.Create();

                //||||||||||||||||||||||||||||||||| X POSITIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| X POSITIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| X POSITIVE AXIS |||||||||||||||||||||||||||||||||
                //orient the voxel camera to face the positive X axis.
                voxelCameraGameObject.transform.eulerAngles = new Vector3(0, 90.0f, 0);

                for (int i = 0; i < voxelResolution.x; i++)
                {
                    //step through the scene on the X axis
                    voxelCameraGameObject.transform.position = voxelBounds.center - new Vector3(voxelSize.x / 2.0f, 0, 0) + new Vector3(xOffset * i, 0, 0);

                    if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass)
                    {
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, albedoUnpackedCameraSlice, 0);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, emissiveUnpackedCameraSlice, 1);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, normalUnpackedCameraSlice, 2);
                    }
                    else if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction1Pass)
                    {
                        metaPassRendererV2.RenderScene(objectMetaDataV2, voxelCamera, voxelPackedCameraSlice);
                        metaPassRendererV2.UnpackSceneRender(voxelPackedCameraSlice, albedoUnpackedCameraSlice, emissiveUnpackedCameraSlice, normalUnpackedCameraSlice);
                    }

                    //feed the compute shader the appropriate data, and do a dispatch so it can accumulate the scene slice into a 3D texture.
                    voxelizeScene.SetInt(ShaderIDs.AxisIndex, i);

                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "LINEAR_TO_GAMMA", sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass);

                    //albedo
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendAlbedoVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_POS, ShaderIDs.CameraVoxelRender, albedoUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_POS, ShaderIDs.Write, sceneAlbedo);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_X_POS, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //emissive
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendEmissiveVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_POS, ShaderIDs.CameraVoxelRender, emissiveUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_POS, ShaderIDs.Write, sceneEmissive);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_X_POS, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //normal
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendNormalVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_POS, ShaderIDs.CameraVoxelRender, normalUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_POS, ShaderIDs.Write, sceneNormal);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_X_POS, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);
                }

                //||||||||||||||||||||||||||||||||| X NEGATIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| X NEGATIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| X NEGATIVE AXIS |||||||||||||||||||||||||||||||||
                //orient the voxel camera to face the negative X axis.
                voxelCameraGameObject.transform.eulerAngles = new Vector3(0, -90.0f, 0);

                for (int i = 0; i < voxelResolution.x; i++)
                {
                    //step through the scene on the X axis
                    voxelCameraGameObject.transform.position = voxelBounds.center + new Vector3(voxelSize.x / 2.0f, 0, 0) - new Vector3(xOffset * i, 0, 0);

                    if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass)
                    {
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, albedoUnpackedCameraSlice, 0);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, emissiveUnpackedCameraSlice, 1);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, normalUnpackedCameraSlice, 2);
                    }
                    else if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction1Pass)
                    {
                        metaPassRendererV2.RenderScene(objectMetaDataV2, voxelCamera, voxelPackedCameraSlice);
                        metaPassRendererV2.UnpackSceneRender(voxelPackedCameraSlice, albedoUnpackedCameraSlice, emissiveUnpackedCameraSlice, normalUnpackedCameraSlice);
                    }

                    //feed the compute shader the appropriate data, and do a dispatch so it can accumulate the scene slice into a 3D texture.
                    voxelizeScene.SetInt(ShaderIDs.AxisIndex, i);

                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "LINEAR_TO_GAMMA", sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass);

                    //albedo
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendAlbedoVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_NEG, ShaderIDs.CameraVoxelRender, albedoUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_NEG, ShaderIDs.Write, sceneAlbedo);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_X_NEG, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //emissive
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendEmissiveVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_NEG, ShaderIDs.CameraVoxelRender, emissiveUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_NEG, ShaderIDs.Write, sceneEmissive);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_X_NEG, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //normal
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendNormalVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_NEG, ShaderIDs.CameraVoxelRender, normalUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_X_NEG, ShaderIDs.Write, sceneNormal);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_X_NEG, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);
                }

                //release the render texture slice, because we are going to create a new one with new dimensions for the next axis...
                voxelPackedCameraSlice.Release();
                albedoUnpackedCameraSlice.Release();
                emissiveUnpackedCameraSlice.Release();
                normalUnpackedCameraSlice.Release();

                //||||||||||||||||||||||||||||||||| Y AXIS SETUP |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Y AXIS SETUP |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Y AXIS SETUP |||||||||||||||||||||||||||||||||
                //captures the scene on the Y axis.

                //create a 2D render texture based off our voxel resolution to capture the scene in the Y axis.
                voxelPackedCameraSlice = new RenderTexture(voxelResolution.x, voxelResolution.z, renderTextureDepthBits, metaPackedFormat);
                voxelPackedCameraSlice.filterMode = FilterMode.Point;
                voxelPackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                voxelPackedCameraSlice.enableRandomWrite = true;
                voxelCamera.targetTexture = voxelPackedCameraSlice;
                voxelCamera.orthographicSize = voxelSize.z * 0.5f;

                albedoUnpackedCameraSlice = new RenderTexture(voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, renderTextureDepthBits, metaPackedFormat);
                albedoUnpackedCameraSlice.filterMode = FilterMode.Point;
                albedoUnpackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                albedoUnpackedCameraSlice.enableRandomWrite = true;
                albedoUnpackedCameraSlice.Create();

                emissiveUnpackedCameraSlice = new RenderTexture(voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, renderTextureDepthBits, metaPackedFormat);
                emissiveUnpackedCameraSlice.filterMode = FilterMode.Point;
                emissiveUnpackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                emissiveUnpackedCameraSlice.enableRandomWrite = true;
                emissiveUnpackedCameraSlice.Create();

                normalUnpackedCameraSlice = new RenderTexture(voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, renderTextureDepthBits, metaPackedFormat);
                normalUnpackedCameraSlice.filterMode = FilterMode.Point;
                normalUnpackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                normalUnpackedCameraSlice.enableRandomWrite = true;
                normalUnpackedCameraSlice.Create();

                //||||||||||||||||||||||||||||||||| Y POSITIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Y POSITIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Y POSITIVE AXIS |||||||||||||||||||||||||||||||||
                //orient the voxel camera to face the positive Y axis.
                voxelCameraGameObject.transform.eulerAngles = new Vector3(-90.0f, 0, 0);

                for (int i = 0; i < voxelResolution.y; i++)
                {
                    //step through the scene on the Y axis
                    voxelCameraGameObject.transform.position = voxelBounds.center - new Vector3(0, voxelSize.y / 2.0f, 0) + new Vector3(0, yOffset * i, 0);

                    if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass)
                    {
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, albedoUnpackedCameraSlice, 0);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, emissiveUnpackedCameraSlice, 1);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, normalUnpackedCameraSlice, 2);
                    }
                    else if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction1Pass)
                    {
                        metaPassRendererV2.RenderScene(objectMetaDataV2, voxelCamera, voxelPackedCameraSlice);
                        metaPassRendererV2.UnpackSceneRender(voxelPackedCameraSlice, albedoUnpackedCameraSlice, emissiveUnpackedCameraSlice, normalUnpackedCameraSlice);
                    }

                    //feed the compute shader the appropriate data, and do a dispatch so it can accumulate the scene slice into a 3D texture.
                    voxelizeScene.SetInt(ShaderIDs.AxisIndex, i);

                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "LINEAR_TO_GAMMA", sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass);

                    //albedo
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendAlbedoVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_POS, ShaderIDs.CameraVoxelRender, albedoUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_POS, ShaderIDs.Write, sceneAlbedo);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Y_POS, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //emissive
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendEmissiveVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_POS, ShaderIDs.CameraVoxelRender, emissiveUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_POS, ShaderIDs.Write, sceneEmissive);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Y_POS, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //normal
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendNormalVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_POS, ShaderIDs.CameraVoxelRender, normalUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_POS, ShaderIDs.Write, sceneNormal);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Y_POS, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);
                }

                //||||||||||||||||||||||||||||||||| Y NEGATIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Y NEGATIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Y NEGATIVE AXIS |||||||||||||||||||||||||||||||||
                //orient the voxel camera to face the negative Y axis.
                voxelCameraGameObject.transform.eulerAngles = new Vector3(90.0f, 0, 0);

                for (int i = 0; i < voxelResolution.y; i++)
                {
                    //step through the scene on the Y axis
                    voxelCameraGameObject.transform.position = voxelBounds.center + new Vector3(0, voxelSize.y / 2.0f, 0) - new Vector3(0, yOffset * i, 0);

                    if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass)
                    {
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, albedoUnpackedCameraSlice, 0);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, emissiveUnpackedCameraSlice, 1);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, normalUnpackedCameraSlice, 2);
                    }
                    else if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction1Pass)
                    {
                        metaPassRendererV2.RenderScene(objectMetaDataV2, voxelCamera, voxelPackedCameraSlice);
                        metaPassRendererV2.UnpackSceneRender(voxelPackedCameraSlice, albedoUnpackedCameraSlice, emissiveUnpackedCameraSlice, normalUnpackedCameraSlice);
                    }

                    //feed the compute shader the appropriate data, and do a dispatch so it can accumulate the scene slice into a 3D texture.
                    voxelizeScene.SetInt(ShaderIDs.AxisIndex, i);

                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "LINEAR_TO_GAMMA", sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass);

                    //albedo
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendAlbedoVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_NEG, ShaderIDs.CameraVoxelRender, albedoUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_NEG, ShaderIDs.Write, sceneAlbedo);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Y_NEG, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //emissive
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendEmissiveVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_NEG, ShaderIDs.CameraVoxelRender, emissiveUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_NEG, ShaderIDs.Write, sceneEmissive);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Y_NEG, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //normal
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendNormalVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_NEG, ShaderIDs.CameraVoxelRender, normalUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Y_NEG, ShaderIDs.Write, sceneNormal);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Y_NEG, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);
                }

                //release the render texture slice, because we are going to create a new one with new dimensions for the next axis...
                voxelPackedCameraSlice.Release();
                albedoUnpackedCameraSlice.Release();
                emissiveUnpackedCameraSlice.Release();
                normalUnpackedCameraSlice.Release();

                //||||||||||||||||||||||||||||||||| Z AXIS SETUP |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Z AXIS SETUP |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Z AXIS SETUP |||||||||||||||||||||||||||||||||
                //captures the scene on the Z axis.

                //create a 2D render texture based off our voxel resolution to capture the scene in the Z axis.
                voxelPackedCameraSlice = new RenderTexture(voxelResolution.x, voxelResolution.y, renderTextureDepthBits, metaPackedFormat);
                voxelPackedCameraSlice.filterMode = FilterMode.Point;
                voxelPackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                voxelPackedCameraSlice.enableRandomWrite = true;
                voxelCamera.targetTexture = voxelPackedCameraSlice;
                voxelCamera.orthographicSize = voxelSize.y * 0.5f;

                albedoUnpackedCameraSlice = new RenderTexture(voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, renderTextureDepthBits, metaPackedFormat);
                albedoUnpackedCameraSlice.filterMode = FilterMode.Point;
                albedoUnpackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                albedoUnpackedCameraSlice.enableRandomWrite = true;
                albedoUnpackedCameraSlice.Create();

                emissiveUnpackedCameraSlice = new RenderTexture(voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, renderTextureDepthBits, metaPackedFormat);
                emissiveUnpackedCameraSlice.filterMode = FilterMode.Point;
                emissiveUnpackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                emissiveUnpackedCameraSlice.enableRandomWrite = true;
                emissiveUnpackedCameraSlice.Create();

                normalUnpackedCameraSlice = new RenderTexture(voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, renderTextureDepthBits, metaPackedFormat);
                normalUnpackedCameraSlice.filterMode = FilterMode.Point;
                normalUnpackedCameraSlice.wrapMode = TextureWrapMode.Clamp;
                normalUnpackedCameraSlice.enableRandomWrite = true;
                normalUnpackedCameraSlice.Create();

                //||||||||||||||||||||||||||||||||| Z POSITIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Z POSITIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Z POSITIVE AXIS |||||||||||||||||||||||||||||||||
                //orient the voxel camera to face the positive Z axis.
                voxelCameraGameObject.transform.eulerAngles = new Vector3(0, 0, 0);

                for (int i = 0; i < voxelResolution.z; i++)
                {
                    //step through the scene on the Z axis
                    voxelCameraGameObject.transform.position = voxelBounds.center - new Vector3(0, 0, voxelSize.z / 2.0f) + new Vector3(0, 0, zOffset * i);

                    if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass)
                    {
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, albedoUnpackedCameraSlice, 0);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, emissiveUnpackedCameraSlice, 1);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, normalUnpackedCameraSlice, 2);
                    }
                    else if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction1Pass)
                    {
                        metaPassRendererV2.RenderScene(objectMetaDataV2, voxelCamera, voxelPackedCameraSlice);
                        metaPassRendererV2.UnpackSceneRender(voxelPackedCameraSlice, albedoUnpackedCameraSlice, emissiveUnpackedCameraSlice, normalUnpackedCameraSlice);
                    }

                    //feed the compute shader the appropriate data, and do a dispatch so it can accumulate the scene slice into a 3D texture.
                    voxelizeScene.SetInt(ShaderIDs.AxisIndex, i);

                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "LINEAR_TO_GAMMA", sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass);

                    //albedo
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendAlbedoVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_POS, ShaderIDs.CameraVoxelRender, albedoUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_POS, ShaderIDs.Write, sceneAlbedo);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Z_POS, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //emissive
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendEmissiveVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_POS, ShaderIDs.CameraVoxelRender, emissiveUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_POS, ShaderIDs.Write, sceneEmissive);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Z_POS, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //normal
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendNormalVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_POS, ShaderIDs.CameraVoxelRender, normalUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_POS, ShaderIDs.Write, sceneNormal);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Z_POS, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);
                }

                //||||||||||||||||||||||||||||||||| Z NEGATIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Z NEGATIVE AXIS |||||||||||||||||||||||||||||||||
                //||||||||||||||||||||||||||||||||| Z NEGATIVE AXIS |||||||||||||||||||||||||||||||||
                //orient the voxel camera to face the negative Z axis.
                voxelCameraGameObject.transform.eulerAngles = new Vector3(0, 180.0f, 0);

                for (int i = 0; i < voxelResolution.z; i++)
                {
                    //step through the scene on the Z axis
                    voxelCameraGameObject.transform.position = voxelBounds.center + new Vector3(0, 0, voxelSize.z / 2.0f) - new Vector3(0, 0, zOffset * i);

                    if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass)
                    {
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, albedoUnpackedCameraSlice, 0);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, emissiveUnpackedCameraSlice, 1);
                        metaPassRendererV1.RenderScene(objectMetaDataV1, voxelCamera, normalUnpackedCameraSlice, 2);
                    }
                    else if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction1Pass)
                    {
                        metaPassRendererV2.RenderScene(objectMetaDataV2, voxelCamera, voxelPackedCameraSlice);
                        metaPassRendererV2.UnpackSceneRender(voxelPackedCameraSlice, albedoUnpackedCameraSlice, emissiveUnpackedCameraSlice, normalUnpackedCameraSlice);
                    }

                    //feed the compute shader the appropriate data, and do a dispatch so it can accumulate the scene slice into a 3D texture.
                    voxelizeScene.SetInt(ShaderIDs.AxisIndex, i);

                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "LINEAR_TO_GAMMA", sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass);

                    //albedo
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendAlbedoVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_NEG, ShaderIDs.CameraVoxelRender, albedoUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_NEG, ShaderIDs.Write, sceneAlbedo);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Z_NEG, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //emissive
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendEmissiveVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_NEG, ShaderIDs.CameraVoxelRender, emissiveUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_NEG, ShaderIDs.Write, sceneEmissive);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Z_NEG, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);

                    //normal
                    VolumeGeneratorUtility.SetComputeKeyword(voxelizeScene, "BLEND_SLICES", blendNormalVoxelSlices);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_NEG, ShaderIDs.CameraVoxelRender, normalUnpackedCameraSlice);
                    voxelizeScene.SetTexture(ComputeShader_VoxelizeScene_Z_NEG, ShaderIDs.Write, sceneNormal);
                    voxelizeScene.Dispatch(ComputeShader_VoxelizeScene_Z_NEG, voxelPackedCameraSlice.width, voxelPackedCameraSlice.height, 1);
                }

                //release the render texture slice, because we are done with it...
                voxelPackedCameraSlice.Release();
                albedoUnpackedCameraSlice.Release();
                emissiveUnpackedCameraSlice.Release();
                normalUnpackedCameraSlice.Release();

                Debug.Log(string.Format("Rendering took {0} seconds.", Time.realtimeSinceStartup - timeBeforeRendering));
            }

            //||||||||||||||||||||||||||||||||| RENDER TEXTURE 3D ---> TEXTURE 3D CONVERSION |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| RENDER TEXTURE 3D ---> TEXTURE 3D CONVERSION |||||||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||||||| RENDER TEXTURE 3D ---> TEXTURE 3D CONVERSION |||||||||||||||||||||||||||||||||
            //final step, save our accumulated 3D texture to the disk.

            VolumeGeneratorUtility.UpdateProgressBar("Saving Volume...", 0.5f);

            float timeBeforeVolumeSaving = Time.realtimeSinceStartup;

            renderTextureConverter.SaveRenderTexture3DAsTexture3D(sceneAlbedo, voxelAlbedoBufferAssetPath, true);
            renderTextureConverter.SaveRenderTexture3DAsTexture3D(sceneEmissive, voxelEmissiveBufferAssetPath, false);
            renderTextureConverter.SaveRenderTexture3DAsTexture3D(sceneNormal, voxelNormalBufferAssetPath, false);

            Debug.Log(string.Format("Volume Saving took {0} seconds.", Time.realtimeSinceStartup - timeBeforeVolumeSaving));

            //|||||||||||||||||||||||||||||||||||||| CLEAN UP ||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||| CLEAN UP ||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||| CLEAN UP ||||||||||||||||||||||||||||||||||||||

            //get rid of this junk, don't need it no more.
            CleanupVoxelCamera();

            if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass)
                metaPassRendererV1.CleanUpSceneObjectMetaBuffers(objectMetaDataV1);
            else if (sceneVoxelizerType == SceneVoxelizerType.MetaExtraction3Pass)
                metaPassRendererV2.CleanUpSceneObjectMetaBuffers(objectMetaDataV2);

            Debug.Log(string.Format("Total Function Time: {0} seconds.", Time.realtimeSinceStartup - timeBeforeFunction));

            VolumeGeneratorUtility.CloseProgressBar();
        }

        //|||||||||||||||||||||||||||||||||||||||||| STEP 2: CAPTURE ENVIRONMENT ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 2: CAPTURE ENVIRONMENT ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 2: CAPTURE ENVIRONMENT ||||||||||||||||||||||||||||||||||||||||||

        public void CaptureEnvironment()
        {
            if (enableEnvironmentLighting == false)
                return;

            if (customEnvironmentMap != null)
                return;

            GameObject environmentCameraGameObject = new GameObject("EnvironmentProbe");
            ReflectionProbe environmentProbe = environmentCameraGameObject.AddComponent<ReflectionProbe>();

            environmentProbe.cullingMask = 0;
            environmentProbe.hdr = true;
            environmentProbe.resolution = environmentResolution;

            //use the lightmapping API to our advantage to simplify things.
            Lightmapping.BakeReflectionProbe(environmentProbe, environmentMapAssetPath);

            environmentMap = AssetDatabase.LoadAssetAtPath<Cubemap>(environmentMapAssetPath);

            MonoBehaviour.DestroyImmediate(environmentProbe);
            MonoBehaviour.DestroyImmediate(environmentCameraGameObject);
        }

        //|||||||||||||||||||||||||||||||||||||||||| STEP 3: TRACE DIRECT SURFACE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 3: TRACE DIRECT SURFACE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 3: TRACE DIRECT SURFACE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //This is where we perform direct SURFACE lighting on the voxelized scene.
        //
        //This is the among the lightest compute shader functions we have...

        public void TraceDirectSurfaceLighting()
        {
            VolumeGeneratorUtility.UpdateProgressBar(string.Format("Preparing direct surface lighting..."), 0.5f);

            float timeBeforeFunction = Time.realtimeSinceStartup;

            volumeGeneratorAssets.PrepareAssetFolders();
            volumeGeneratorAssets.GetResources();

            GetGeneratedContent(); //Load up all of our generated content so we can use it.

            BuildLightComputeBuffers(); //Get all unity scene lights ready to use in the compute shader.

            //consruct our render texture that we will write into
            RenderTexture directSurfaceTrace = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            directSurfaceTrace.dimension = TextureDimension.Tex3D;
            directSurfaceTrace.wrapMode = TextureWrapMode.Clamp;
            directSurfaceTrace.filterMode = FilterMode.Point;
            directSurfaceTrace.volumeDepth = voxelResolution.z;
            directSurfaceTrace.enableRandomWrite = true;
            directSurfaceTrace.Create();

            //fetch our main direct surface light function kernel in the compute shader
            int ComputeShader_TraceSurfaceDirectLight = voxelDirectSurfaceLight.FindKernel("ComputeShader_TraceSurfaceDirectLight");
            voxelDirectSurfaceLight.GetKernelThreadGroupSizes(ComputeShader_TraceSurfaceDirectLight, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            //make sure the compute shader knows the following parameters.
            voxelDirectSurfaceLight.SetInt("VolumeMipCount", voxelAlbedoBuffer.mipmapCount);
            voxelDirectSurfaceLight.SetVector("VolumeResolution", new Vector4(voxelResolution.x, voxelResolution.y, voxelResolution.z, 0));
            voxelDirectSurfaceLight.SetVector("VolumePosition", voxelBounds.center);
            voxelDirectSurfaceLight.SetVector("VolumeSize", voxelSize);
            voxelDirectSurfaceLight.SetFloat("AlbedoBoost", albedoBoost);
            voxelDirectSurfaceLight.SetInt("MaxDirectSamples", directSurfaceSamples);

            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectSurfaceLight, "_ATTENUATION_UNITY", lightAttenuationType == LightAttenuationType.UnityFalloff);
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectSurfaceLight, "_ATTENUATION_INVERSE_SQUARE", lightAttenuationType == LightAttenuationType.InverseSquareFalloff);
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectSurfaceLight, "_ATTENUATION_LINEAR", lightAttenuationType == LightAttenuationType.LinearFalloff);

            //make sure the compute shader knows what sets of lights we have.
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectSurfaceLight, "DIRECTIONAL_LIGHTS", directionalLightsBuffer != null);
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectSurfaceLight, "POINT_LIGHTS", pointLightsBuffer != null);
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectSurfaceLight, "SPOT_LIGHTS", spotLightsBuffer != null);
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectSurfaceLight, "AREA_LIGHTS", areaLightsBuffer != null);

            //build a small dummy compute buffer, so that we can use GetData later to perform a CPU stall.
            ComputeBuffer dummyComputeBuffer = new ComputeBuffer(1, 4);
            dummyComputeBuffer.SetData(new int[1]);

            for (int i = 0; i < directSurfaceSamples; i++)
            {
                //randomize the seed for noise sampling (THIS IS IMPORTANT)
                voxelDirectSurfaceLight.SetFloat("RandomSeed", UnityEngine.Random.value * 100000.0f);

                //feed the compute shader the constructed compute buffers of the unity lights we gathered if they exist.
                if (directionalLightsBuffer != null) voxelDirectSurfaceLight.SetBuffer(ComputeShader_TraceSurfaceDirectLight, "DirectionalLights", directionalLightsBuffer);
                if (pointLightsBuffer != null) voxelDirectSurfaceLight.SetBuffer(ComputeShader_TraceSurfaceDirectLight, "PointLights", pointLightsBuffer);
                if (spotLightsBuffer != null) voxelDirectSurfaceLight.SetBuffer(ComputeShader_TraceSurfaceDirectLight, "SpotLights", spotLightsBuffer);
                if (areaLightsBuffer != null) voxelDirectSurfaceLight.SetBuffer(ComputeShader_TraceSurfaceDirectLight, "AreaLights", areaLightsBuffer);

                //feed our compute shader the appropriate buffers so we can use them.
                voxelDirectSurfaceLight.SetTexture(ComputeShader_TraceSurfaceDirectLight, "SceneAlbedo", voxelAlbedoBuffer); //most important one, contains scene color and "occlusion".
                voxelDirectSurfaceLight.SetTexture(ComputeShader_TraceSurfaceDirectLight, "SceneNormal", voxelNormalBuffer); //this actually isn't needed and used at the moment.

                voxelDirectSurfaceLight.SetTexture(ComputeShader_TraceSurfaceDirectLight, "Write", directSurfaceTrace);

                //send a tiny dummy compute buffer so we can do a CPU stall later if enabled.
                voxelDirectSurfaceLight.SetBuffer(ComputeShader_TraceSurfaceDirectLight, "DummyComputeBuffer", dummyComputeBuffer);

                //let the GPU compute direct surface lighting, and hope it can manage it :D
                voxelDirectSurfaceLight.Dispatch(ComputeShader_TraceSurfaceDirectLight, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));
                //voxelDirectSurfaceLight.Dispatch(ComputeShader_TraceSurfaceDirectLight, THREAD_GROUP_SIZE_X, THREAD_GROUP_SIZE_Y, THREAD_GROUP_SIZE_Z);
                //voxelDirectSurfaceLight.Dispatch(ComputeShader_TraceSurfaceDirectLight, voxelResolution.x, voxelResolution.y, voxelResolution.z);

                //Perform a deliberate stall on the CPU so we can make sure that we don't issue too many dispatches to the GPU and overburden it.
                if (i % GPU_Readback_Limit == 0 && enableGPU_Readback_Limit)
                {
                    int[] dummyData = new int[1];
                    dummyComputeBuffer.GetData(dummyData);
                }

                VolumeGeneratorUtility.UpdateProgressBar(string.Format("Tracing Direct Surface Light... [SAMPLES: {0} / {1}]", i + 1, directSurfaceSamples), 0.5f);
            }

            dummyComputeBuffer.Release();

            //|||||||||||||||||||||||||||||||||||||||||| SAVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            renderTextureConverter.SaveRenderTexture3DAsTexture3D(directSurfaceTrace, voxelDirectLightSurfaceBufferAssetPath);

            //|||||||||||||||||||||||||||||||||||||||||| ALBEDO ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ALBEDO ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ALBEDO ||||||||||||||||||||||||||||||||||||||||||

            //consruct our render texture that we will write into
            RenderTexture directSurfaceAlbedoTrace = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            directSurfaceAlbedoTrace.dimension = TextureDimension.Tex3D;
            directSurfaceAlbedoTrace.wrapMode = TextureWrapMode.Clamp;
            directSurfaceAlbedoTrace.filterMode = FilterMode.Point;
            directSurfaceAlbedoTrace.volumeDepth = voxelResolution.z;
            directSurfaceAlbedoTrace.enableRandomWrite = true;
            directSurfaceAlbedoTrace.Create();

            GetGeneratedContent();

            int ComputeShader_CombineAlbedoWithLighting = combineBuffers.FindKernel("ComputeShader_CombineAlbedoWithLighting");
            combineBuffers.SetFloat("AlbedoBoost", albedoBoost);
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "BufferA", voxelAlbedoBuffer); //albedo
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "BufferB", voxelDirectLightSurfaceBuffer); //lighting
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "Write", directSurfaceAlbedoTrace);
            combineBuffers.Dispatch(ComputeShader_CombineAlbedoWithLighting, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

            renderTextureConverter.SaveRenderTexture3DAsTexture3D(directSurfaceAlbedoTrace, voxelDirectLightSurfaceAlbedoBufferAssetPath);

            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //after surviving the onslaught of computations, we will save our results to the disk.

            //we are done with the compute buffers of the unity lights, so clean them up.
            if (directionalLightsBuffer != null) directionalLightsBuffer.Release();
            if (pointLightsBuffer != null) pointLightsBuffer.Release();
            if (spotLightsBuffer != null) spotLightsBuffer.Release();
            if (areaLightsBuffer != null) areaLightsBuffer.Release();

            Debug.Log(string.Format("'TraceDirectSurfaceLighting' took {0} seconds.", Time.realtimeSinceStartup - timeBeforeFunction));
            VolumeGeneratorUtility.CloseProgressBar();
        }

        //|||||||||||||||||||||||||||||||||||||||||| STEP 4: TRACE DIRECT VOLUME LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 4: TRACE DIRECT VOLUME LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 4: TRACE DIRECT VOLUME LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //This is where we perform direct VOLUMETRIC lighting on the voxelized scene.
        //
        //This is definetly slightly more expensive than the surface tracing version.
        //It can definetly get intensive with a large amount of lights, or with a dense enough voxel resolution.
        //This should be optimized later with random importance sampling of lights just like the one before.
        //
        //But... for the time being compared to the bounce functions later this is relatively light and doesn't cause GPU driver timeouts. 

        public void TraceDirectVolumeLighting()
        {
            VolumeGeneratorUtility.UpdateProgressBar(string.Format("Tracing Direct Volume Lighting..."), 0.5f);

            float timeBeforeFunction = Time.realtimeSinceStartup;

            volumeGeneratorAssets.PrepareAssetFolders();
            volumeGeneratorAssets.GetResources();
            GetGeneratedContent(); //Load up all of our generated content so we can use it.
            BuildLightComputeBuffers(); //Get all unity scene lights ready to use in the compute shader.

            //consruct our render texture that we will write into
            RenderTexture volumeWrite = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            volumeWrite.dimension = TextureDimension.Tex3D;
            volumeWrite.wrapMode = TextureWrapMode.Clamp;
            volumeWrite.filterMode = FilterMode.Point;
            volumeWrite.volumeDepth = voxelResolution.z;
            volumeWrite.enableRandomWrite = true;
            volumeWrite.Create();

            //fetch our main direct volumetric light function kernel in the compute shader
            int ComputeShader_TraceVolumeDirectLight = voxelDirectVolumetricLight.FindKernel("ComputeShader_TraceVolumeDirectLight");
            voxelDirectVolumetricLight.GetKernelThreadGroupSizes(ComputeShader_TraceVolumeDirectLight, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            //make sure the compute shader knows the following parameters.
            voxelDirectVolumetricLight.SetVector("VolumeResolution", new Vector4(voxelResolution.x, voxelResolution.y, voxelResolution.z, 0));
            voxelDirectVolumetricLight.SetVector("VolumePosition", voxelBounds.center);
            voxelDirectVolumetricLight.SetVector("VolumeSize", voxelSize);
            voxelDirectVolumetricLight.SetFloat("AlbedoBoost", albedoBoost);
            voxelDirectVolumetricLight.SetInt("MaxDirectSamples", directVolumetricSamples);

            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectVolumetricLight, "_ATTENUATION_UNITY", lightAttenuationType == LightAttenuationType.UnityFalloff);
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectVolumetricLight, "_ATTENUATION_INVERSE_SQUARE", lightAttenuationType == LightAttenuationType.InverseSquareFalloff);
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectVolumetricLight, "_ATTENUATION_LINEAR", lightAttenuationType == LightAttenuationType.LinearFalloff);

            //make sure the compute shader knows what sets of lights we have.
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectVolumetricLight, "DIRECTIONAL_LIGHTS", directionalLightsBuffer != null);
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectVolumetricLight, "POINT_LIGHTS", pointLightsBuffer != null);
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectVolumetricLight, "SPOT_LIGHTS", spotLightsBuffer != null);
            VolumeGeneratorUtility.SetComputeKeyword(voxelDirectVolumetricLight, "AREA_LIGHTS", areaLightsBuffer != null);

            //build a small dummy compute buffer, so that we can use GetData later to perform a CPU stall.
            ComputeBuffer dummyComputeBuffer = new ComputeBuffer(1, 4);
            dummyComputeBuffer.SetData(new int[1]);

            for (int i = 0; i < directVolumetricSamples; i++)
            {
                //randomize the seed for noise sampling (THIS IS IMPORTANT)
                voxelDirectVolumetricLight.SetFloat("RandomSeed", UnityEngine.Random.value * 100000.0f);

                //feed the compute shader the constructed compute buffers of the unity lights we gathered if they exist.
                if (directionalLightsBuffer != null) voxelDirectVolumetricLight.SetBuffer(ComputeShader_TraceVolumeDirectLight, "DirectionalLights", directionalLightsBuffer);
                if (pointLightsBuffer != null) voxelDirectVolumetricLight.SetBuffer(ComputeShader_TraceVolumeDirectLight, "PointLights", pointLightsBuffer);
                if (spotLightsBuffer != null) voxelDirectVolumetricLight.SetBuffer(ComputeShader_TraceVolumeDirectLight, "SpotLights", spotLightsBuffer);
                if (areaLightsBuffer != null) voxelDirectVolumetricLight.SetBuffer(ComputeShader_TraceVolumeDirectLight, "AreaLights", areaLightsBuffer);

                //feed our compute shader the appropriate buffers so we can use them.
                voxelDirectVolumetricLight.SetTexture(ComputeShader_TraceVolumeDirectLight, "SceneAlbedo", voxelAlbedoBuffer); //most important one, contains scene color and "occlusion".
                voxelDirectVolumetricLight.SetTexture(ComputeShader_TraceVolumeDirectLight, "Write", volumeWrite);

                //send a tiny dummy compute buffer so we can do a CPU stall later if enabled.
                voxelDirectVolumetricLight.SetBuffer(ComputeShader_TraceVolumeDirectLight, "DummyComputeBuffer", dummyComputeBuffer);

                //let the GPU compute direct surface lighting, and hope it can manage it :D
                voxelDirectVolumetricLight.Dispatch(ComputeShader_TraceVolumeDirectLight, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                //Perform a deliberate stall on the CPU so we can make sure that we don't issue too many dispatches to the GPU and overburden it.
                if (i % GPU_Readback_Limit == 0 && enableGPU_Readback_Limit)
                {
                    int[] dummyData = new int[1];
                    dummyComputeBuffer.GetData(dummyData);
                }

                VolumeGeneratorUtility.UpdateProgressBar(string.Format("Tracing Direct Volumetric Light... [SAMPLES: {0} / {1}]", i + 1, directVolumetricSamples), 0.5f);
            }

            dummyComputeBuffer.Release();

            //|||||||||||||||||||||||||||||||||||||||||| POST 3D GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| POST 3D GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| POST 3D GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||
            //In an attempt to squeeze more out of less...
            //We will now perform a 3D gaussian blur to smooth out the results from the direct volumetric light.
            //(IF ITS ENABLED)

            if (volumetricDirectGaussianSamples > 0)
            {
                //fetch our main gaussian blur function kernel in the compute shader
                int ComputeShader_GaussianBlur = gaussianBlur.FindKernel("ComputeShader_GaussianBlur");
                gaussianBlur.GetKernelThreadGroupSizes(ComputeShader_GaussianBlur, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

                //convert the raw volumetric bounce light render texture into a texture3D so that it can be read.
                Texture3D tempRawVolumetricBounceLight = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite);

                //make sure the compute shader knows the following parameters.
                gaussianBlur.SetVector("VolumeResolution", new Vector4(voxelResolution.x, voxelResolution.y, voxelResolution.z, 0));
                gaussianBlur.SetInt("BlurSamples", volumetricDirectGaussianSamples);

                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //set the gaussian blur direction for this pass.
                gaussianBlur.SetVector("BlurDirection", new Vector4(1, 0, 0, 0));

                //feed our compute shader the appropriate textures.
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Read", tempRawVolumetricBounceLight);
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Write", volumeWrite);

                //let the GPU perform a gaussian blur along the given axis.
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                //|||||||||||||||||||||||||||||||||||||||||| BLUR Y PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Y PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Y PASS ||||||||||||||||||||||||||||||||||||||||||
                //get the result from the x pass and convert it into a texture3D so that it can be read again.
                Texture3D tempBlurX = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite);

                //set the gaussian blur direction for this pass.
                gaussianBlur.SetVector("BlurDirection", new Vector4(0, 1, 0, 0));

                //feed our compute shader the appropriate textures.
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Read", tempBlurX);
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Write", volumeWrite);

                //let the GPU perform a gaussian blur along the given axis.
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                //|||||||||||||||||||||||||||||||||||||||||| BLUR Z PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Z PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Z PASS ||||||||||||||||||||||||||||||||||||||||||
                //get the result from the y pass and convert it into a texture3D so that it can be read one more time.
                Texture3D tempBlurY = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite);

                //set the gaussian blur direction for this pass.
                gaussianBlur.SetVector("BlurDirection", new Vector4(0, 0, 1, 0));

                //feed our compute shader the appropriate textures.
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Read", tempBlurY);
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Write", volumeWrite);

                //let the GPU perform a gaussian blur along the given axis.
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));
            }

            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //after surviving the onslaught of computations, we will save our results to the disk.

            VolumeGeneratorUtility.UpdateProgressBar(string.Format("Saving to disk..."), 0.5f);

            //save it!
            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeWrite, voxelDirectLightVolumeBufferAssetPath);

            //we are done with the compute buffers of the unity lights, so clean them up.
            if (directionalLightsBuffer != null) directionalLightsBuffer.Release();
            if (pointLightsBuffer != null) pointLightsBuffer.Release();
            if (spotLightsBuffer != null) spotLightsBuffer.Release();
            if (areaLightsBuffer != null) areaLightsBuffer.Release();

            Debug.Log(string.Format("'TraceDirectVolumeLighting' took {0} seconds.", Time.realtimeSinceStartup - timeBeforeFunction));
            VolumeGeneratorUtility.CloseProgressBar();
        }

        //|||||||||||||||||||||||||||||||||||||||||| STEP 5: TRACE ENVIRONMENT SURFACE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 5: TRACE ENVIRONMENT SURFACE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 5: TRACE ENVIRONMENT SURFACE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //This is where we perform environment SURFACE lighting on the voxelized scene.

        public void TraceEnvironmentSurfaceLighting()
        {
            VolumeGeneratorUtility.UpdateProgressBar(string.Format("Preparing environment surface lighting..."), 0.5f);

            float timeBeforeFunction = Time.realtimeSinceStartup;

            volumeGeneratorAssets.PrepareAssetFolders();
            volumeGeneratorAssets.GetResources();
            GetGeneratedContent(); //Load up all of our generated content so we can use it.

            //fetch our main bounce surface light function kernel in the compute shader
            int ComputeShader_TraceSurfaceEnvironmentLight = voxelEnvironmentSurfaceLight.FindKernel("ComputeShader_TraceSurfaceEnvironmentLight");
            voxelEnvironmentSurfaceLight.GetKernelThreadGroupSizes(ComputeShader_TraceSurfaceEnvironmentLight, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            //make sure the compute shader knows the following parameters.
            voxelEnvironmentSurfaceLight.SetVector("VolumeResolution", new Vector4(voxelResolution.x, voxelResolution.y, voxelResolution.z, 0));
            voxelEnvironmentSurfaceLight.SetVector("VolumePosition", voxelBounds.center);
            voxelEnvironmentSurfaceLight.SetVector("VolumeSize", voxelSize);
            voxelEnvironmentSurfaceLight.SetInt("MaxEnvironmentSamples", environmentSurfaceSamples);
            voxelEnvironmentSurfaceLight.SetFloat("EnvironmentIntensity", environmentIntensity);
            voxelEnvironmentSurfaceLight.SetFloat("AlbedoBoost", albedoBoost);

            //if enabled, use a normal oriented cosine hemisphere for better ray allocation/quality (though it has some issues/quirks)
            VolumeGeneratorUtility.SetComputeKeyword(voxelEnvironmentSurfaceLight, "NORMAL_ORIENTED_HEMISPHERE_SAMPLING", normalOrientedHemisphereSampling);

            //consruct our render texture that we will write into
            RenderTexture volumeWrite = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            volumeWrite.dimension = TextureDimension.Tex3D;
            volumeWrite.wrapMode = TextureWrapMode.Clamp;
            volumeWrite.filterMode = FilterMode.Point;
            volumeWrite.volumeDepth = voxelResolution.z;
            volumeWrite.enableRandomWrite = true;
            volumeWrite.Create();

            //build a small dummy compute buffer, so that we can use GetData later to perform a CPU stall.
            ComputeBuffer dummyComputeBuffer = new ComputeBuffer(1, 4);
            dummyComputeBuffer.SetData(new int[1]);

            for (int i = 0; i < environmentSurfaceSamples; i++)
            {
                //randomize the seed for noise sampling (THIS IS IMPORTANT)
                voxelEnvironmentSurfaceLight.SetFloat("RandomSeed", UnityEngine.Random.value * 100000.0f);

                //feed our compute shader the appropriate buffers so we can use them.
                voxelEnvironmentSurfaceLight.SetTexture(ComputeShader_TraceSurfaceEnvironmentLight, "SceneAlbedo", voxelAlbedoBuffer); //important, used for "occlusion" checking.
                voxelEnvironmentSurfaceLight.SetTexture(ComputeShader_TraceSurfaceEnvironmentLight, "SceneNormal", voxelNormalBuffer); //important, used to help orient hemispheres when enabled.
                voxelEnvironmentSurfaceLight.SetTexture(ComputeShader_TraceSurfaceEnvironmentLight, "EnvironmentMap", environmentMap); //important, the main color that we will be bouncing around.
                voxelEnvironmentSurfaceLight.SetTexture(ComputeShader_TraceSurfaceEnvironmentLight, "Write", volumeWrite);

                //send a tiny dummy compute buffer so we can do a CPU stall later if enabled.
                voxelEnvironmentSurfaceLight.SetBuffer(ComputeShader_TraceSurfaceEnvironmentLight, "DummyComputeBuffer", dummyComputeBuffer);

                //let the GPU compute bounced surface lighting, and hope it can manage it :(
                voxelEnvironmentSurfaceLight.Dispatch(ComputeShader_TraceSurfaceEnvironmentLight, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                //Perform a deliberate stall on the CPU so we can make sure that we don't issue too many dispatches to the GPU and overburden it.
                if (i % GPU_Readback_Limit == 0 && enableGPU_Readback_Limit)
                {
                    int[] dummyData = new int[1];
                    dummyComputeBuffer.GetData(dummyData);
                }

                VolumeGeneratorUtility.UpdateProgressBar(string.Format("Environment Surface Light... [SAMPLES: {0} / {1}]", i + 1, environmentSurfaceSamples), 0.5f);
            }

            dummyComputeBuffer.Release();

            //|||||||||||||||||||||||||||||||||||||||||| SAVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAVE LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeWrite, voxelEnvironmentLightSurfaceBufferAssetPath);

            //|||||||||||||||||||||||||||||||||||||||||| ALBEDO ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ALBEDO ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ALBEDO ||||||||||||||||||||||||||||||||||||||||||

            //consruct our render texture that we will write into
            RenderTexture environmentSurfaceAlbedoTrace = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            environmentSurfaceAlbedoTrace.dimension = TextureDimension.Tex3D;
            environmentSurfaceAlbedoTrace.wrapMode = TextureWrapMode.Clamp;
            environmentSurfaceAlbedoTrace.filterMode = FilterMode.Point;
            environmentSurfaceAlbedoTrace.volumeDepth = voxelResolution.z;
            environmentSurfaceAlbedoTrace.enableRandomWrite = true;
            environmentSurfaceAlbedoTrace.Create();

            GetGeneratedContent();

            int ComputeShader_CombineAlbedoWithLighting = combineBuffers.FindKernel("ComputeShader_CombineAlbedoWithLighting");
            combineBuffers.SetFloat("AlbedoBoost", albedoBoost);
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "BufferA", voxelAlbedoBuffer); //albedo
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "BufferB", voxelEnvironmentLightSurfaceBuffer); //lighting
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "Write", environmentSurfaceAlbedoTrace);
            combineBuffers.Dispatch(ComputeShader_CombineAlbedoWithLighting, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

            renderTextureConverter.SaveRenderTexture3DAsTexture3D(environmentSurfaceAlbedoTrace, voxelEnvironmentLightSurfaceAlbedoBufferAssetPath);

            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //if we have survived the onslaught of computations... FANTASTIC! lets save our results to the disk before we lose it.

            //we are done with this, so clean up.
            volumeWrite.DiscardContents(true, true);
            volumeWrite.Release();

            Debug.Log(string.Format("'TraceEnvironmentSurfaceLighting' took {0} seconds.", Time.realtimeSinceStartup - timeBeforeFunction));
            VolumeGeneratorUtility.CloseProgressBar();
        }

        //|||||||||||||||||||||||||||||||||||||||||| STEP 6: TRACE ENVIRONMENT VOLUME LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 6: TRACE ENVIRONMENT VOLUME LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 6: TRACE ENVIRONMENT VOLUME LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //This is where we perform environment VOLUMETRIC lighting on the voxelized scene.
        //
        //This is definetly slightly more expensive than the surface tracing version.
        //It can definetly get intensive with a large amount of lights, or with a dense enough voxel resolution.
        //This should be optimized later with random importance sampling of lights just like the one before.
        //
        //But... for the time being compared to the bounce functions later this is relatively light and doesn't cause GPU driver timeouts. 

        public void TraceEnvironmentVolumeLighting()
        {
            VolumeGeneratorUtility.UpdateProgressBar(string.Format("Tracing Environment Volume Lighting..."), 0.5f);

            float timeBeforeFunction = Time.realtimeSinceStartup;

            volumeGeneratorAssets.PrepareAssetFolders();
            volumeGeneratorAssets.GetResources();
            GetGeneratedContent(); //Load up all of our generated content so we can use it.
            BuildLightComputeBuffers(); //Get all unity scene lights ready to use in the compute shader.

            //consruct our render texture that we will write into
            RenderTexture volumeWrite = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            volumeWrite.dimension = TextureDimension.Tex3D;
            volumeWrite.wrapMode = TextureWrapMode.Clamp;
            volumeWrite.filterMode = FilterMode.Point;
            volumeWrite.volumeDepth = voxelResolution.z;
            volumeWrite.enableRandomWrite = true;
            volumeWrite.Create();

            //fetch our main direct volumetric light function kernel in the compute shader
            int ComputeShader_TraceVolumetricEnvironmentLight = voxelEnvironmentVolumetricLight.FindKernel("ComputeShader_TraceVolumetricEnvironmentLight");
            voxelEnvironmentVolumetricLight.GetKernelThreadGroupSizes(ComputeShader_TraceVolumetricEnvironmentLight, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            //make sure the compute shader knows the following parameters.
            voxelEnvironmentVolumetricLight.SetVector("VolumeResolution", new Vector4(voxelResolution.x, voxelResolution.y, voxelResolution.z, 0));
            voxelEnvironmentVolumetricLight.SetVector("VolumePosition", voxelBounds.center);
            voxelEnvironmentVolumetricLight.SetVector("VolumeSize", voxelSize);
            voxelEnvironmentVolumetricLight.SetFloat("EnvironmentIntensity", environmentIntensity);
            voxelEnvironmentVolumetricLight.SetInt("MaxEnvironmentSamples", environmentVolumetricSamples);

            //build a small dummy compute buffer, so that we can use GetData later to perform a CPU stall.
            ComputeBuffer dummyComputeBuffer = new ComputeBuffer(1, 4);
            dummyComputeBuffer.SetData(new int[1]);

            for (int i = 0; i < environmentVolumetricSamples; i++)
            {
                //randomize the seed for noise sampling (THIS IS IMPORTANT)
                voxelEnvironmentVolumetricLight.SetFloat("RandomSeed", UnityEngine.Random.value * 100000.0f);

                //feed our compute shader the appropriate buffers so we can use them.
                voxelEnvironmentVolumetricLight.SetTexture(ComputeShader_TraceVolumetricEnvironmentLight, "SceneAlbedo", voxelAlbedoBuffer); //most important one, contains scene color and "occlusion".
                voxelEnvironmentVolumetricLight.SetTexture(ComputeShader_TraceVolumetricEnvironmentLight, "EnvironmentMap", environmentMap); //important, the main color that we will be bouncing around.
                voxelEnvironmentVolumetricLight.SetTexture(ComputeShader_TraceVolumetricEnvironmentLight, "Write", volumeWrite);

                //send a tiny dummy compute buffer so we can do a CPU stall later if enabled.
                voxelEnvironmentVolumetricLight.SetBuffer(ComputeShader_TraceVolumetricEnvironmentLight, "DummyComputeBuffer", dummyComputeBuffer);

                //let the GPU compute direct surface lighting, and hope it can manage it :D
                voxelEnvironmentVolumetricLight.Dispatch(ComputeShader_TraceVolumetricEnvironmentLight, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                //Perform a deliberate stall on the CPU so we can make sure that we don't issue too many dispatches to the GPU and overburden it.
                if (i % GPU_Readback_Limit == 0 && enableGPU_Readback_Limit)
                {
                    int[] dummyData = new int[1];
                    dummyComputeBuffer.GetData(dummyData);
                }

                VolumeGeneratorUtility.UpdateProgressBar(string.Format("Tracing Environment Volumetric Light... [SAMPLES: {0} / {1}]", i + 1, environmentVolumetricSamples), 0.5f);
            }

            dummyComputeBuffer.Release();

            //|||||||||||||||||||||||||||||||||||||||||| POST 3D GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| POST 3D GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| POST 3D GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||
            //In an attempt to squeeze more out of less...
            //We will now perform a 3D gaussian blur to smooth out the results from the direct volumetric light.
            //(IF ITS ENABLED)

            if (volumetricEnvironmentGaussianSamples > 0)
            {
                //fetch our main gaussian blur function kernel in the compute shader
                int ComputeShader_GaussianBlur = gaussianBlur.FindKernel("ComputeShader_GaussianBlur");
                gaussianBlur.GetKernelThreadGroupSizes(ComputeShader_GaussianBlur, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

                //convert the raw volumetric bounce light render texture into a texture3D so that it can be read.
                Texture3D tempRawVolumetricBounceLight = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite);

                //make sure the compute shader knows the following parameters.
                gaussianBlur.SetVector("VolumeResolution", new Vector4(voxelResolution.x, voxelResolution.y, voxelResolution.z, 0));
                gaussianBlur.SetInt("BlurSamples", volumetricEnvironmentGaussianSamples);

                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //set the gaussian blur direction for this pass.
                gaussianBlur.SetVector("BlurDirection", new Vector4(1, 0, 0, 0));

                //feed our compute shader the appropriate textures.
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Read", tempRawVolumetricBounceLight);
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Write", volumeWrite);

                //let the GPU perform a gaussian blur along the given axis.
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                //|||||||||||||||||||||||||||||||||||||||||| BLUR Y PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Y PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Y PASS ||||||||||||||||||||||||||||||||||||||||||
                //get the result from the x pass and convert it into a texture3D so that it can be read again.
                Texture3D tempBlurX = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite);

                //set the gaussian blur direction for this pass.
                gaussianBlur.SetVector("BlurDirection", new Vector4(0, 1, 0, 0));

                //feed our compute shader the appropriate textures.
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Read", tempBlurX);
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Write", volumeWrite);

                //let the GPU perform a gaussian blur along the given axis.
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                //|||||||||||||||||||||||||||||||||||||||||| BLUR Z PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Z PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Z PASS ||||||||||||||||||||||||||||||||||||||||||
                //get the result from the y pass and convert it into a texture3D so that it can be read one more time.
                Texture3D tempBlurY = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite);

                //set the gaussian blur direction for this pass.
                gaussianBlur.SetVector("BlurDirection", new Vector4(0, 0, 1, 0));

                //feed our compute shader the appropriate textures.
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Read", tempBlurY);
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Write", volumeWrite);

                //let the GPU perform a gaussian blur along the given axis.
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));
            }

            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //after surviving the onslaught of computations, we will save our results to the disk.

            VolumeGeneratorUtility.UpdateProgressBar(string.Format("Saving to disk..."), 0.5f);

            //save it!
            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeWrite, voxelEnvironmentLightVolumetricBufferAssetPath);

            Debug.Log(string.Format("'TraceEnvironmentVolumeLighting' took {0} seconds.", Time.realtimeSinceStartup - timeBeforeFunction));
            VolumeGeneratorUtility.CloseProgressBar();
        }

        //|||||||||||||||||||||||||||||||||||||||||| STEP 7: COMBINE SURFACE DIRECT LIGHTING TERMS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 7: COMBINE SURFACE DIRECT LIGHTING TERMS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 7: COMBINE SURFACE DIRECT LIGHTING TERMS ||||||||||||||||||||||||||||||||||||||||||
        //This is a light operation, so no worries here.

        public void CombineDirectSurfaceLightingTerms()
        {
            VolumeGeneratorUtility.UpdateProgressBar(string.Format("Combining Direct Surface Lighting Terms..."), 0.5f);

            float timeBeforeFunction = Time.realtimeSinceStartup;

            volumeGeneratorAssets.PrepareAssetFolders();
            volumeGeneratorAssets.GetResources();
            GetGeneratedContent(); //Load up all of our generated content so we can use it.

            //fetch our function kernel in the compute shader
            int ComputeShader_AddBuffers = combineBuffers.FindKernel("ComputeShader_AddBuffers");
            combineBuffers.GetKernelThreadGroupSizes(ComputeShader_AddBuffers, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            //|||||||||||||||||||||||||||||||||||||||||| ADD SURFACE ENVIRONMENT LIGHT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADD SURFACE ENVIRONMENT LIGHT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADD SURFACE ENVIRONMENT LIGHT ||||||||||||||||||||||||||||||||||||||||||

            Texture3D volumeLighting1 = voxelDirectLightSurfaceBuffer;

            if (enableEnvironmentLighting)
            {
                //consruct our render texture that we will write into
                RenderTexture volumeDirectAndEnvironmentLighting = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
                volumeDirectAndEnvironmentLighting.dimension = TextureDimension.Tex3D;
                volumeDirectAndEnvironmentLighting.wrapMode = TextureWrapMode.Clamp;
                volumeDirectAndEnvironmentLighting.filterMode = FilterMode.Point;
                volumeDirectAndEnvironmentLighting.volumeDepth = voxelResolution.z;
                volumeDirectAndEnvironmentLighting.enableRandomWrite = true;
                volumeDirectAndEnvironmentLighting.Create();

                //feed the compute shader the textures that will be added together
                combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferA", voxelDirectLightSurfaceBuffer);
                combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferB", voxelEnvironmentLightSurfaceBuffer);
                combineBuffers.SetTexture(ComputeShader_AddBuffers, "Write", volumeDirectAndEnvironmentLighting);

                //let the GPU add the textures together.
                combineBuffers.Dispatch(ComputeShader_AddBuffers, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                volumeLighting1 = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeDirectAndEnvironmentLighting);
            }

            //|||||||||||||||||||||||||||||||||||||||||| COMBINE LIGHT WITH ALBEDO ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMBINE LIGHT WITH ALBEDO ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMBINE LIGHT WITH ALBEDO ||||||||||||||||||||||||||||||||||||||||||

            RenderTexture volumeLightWithAlbedo = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            volumeLightWithAlbedo.dimension = TextureDimension.Tex3D;
            volumeLightWithAlbedo.wrapMode = TextureWrapMode.Clamp;
            volumeLightWithAlbedo.filterMode = FilterMode.Point;
            volumeLightWithAlbedo.volumeDepth = voxelResolution.z;
            volumeLightWithAlbedo.enableRandomWrite = true;
            volumeLightWithAlbedo.Create();

            int ComputeShader_CombineAlbedoWithLighting = combineBuffers.FindKernel("ComputeShader_CombineAlbedoWithLighting");
            combineBuffers.GetKernelThreadGroupSizes(ComputeShader_CombineAlbedoWithLighting, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            combineBuffers.SetFloat("AlbedoBoost", albedoBoost);
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "BufferA", voxelAlbedoBuffer); //albedo
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "BufferB", volumeLighting1); //lighting
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "Write", volumeLightWithAlbedo);
            combineBuffers.Dispatch(ComputeShader_CombineAlbedoWithLighting, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

            Texture3D volumeCombinedLightWithAlbedo = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeLightWithAlbedo);

            //|||||||||||||||||||||||||||||||||||||||||| ADD EMISSIVE LIGHT TO COMBINED LIGHT/ALBEDO ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADD EMISSIVE LIGHT TO COMBINED LIGHT/ALBEDO ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADD EMISSIVE LIGHT TO COMBINED LIGHT/ALBEDO ||||||||||||||||||||||||||||||||||||||||||

            RenderTexture volumeLightWithAlbedoAndEmissive = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            volumeLightWithAlbedoAndEmissive.dimension = TextureDimension.Tex3D;
            volumeLightWithAlbedoAndEmissive.wrapMode = TextureWrapMode.Clamp;
            volumeLightWithAlbedoAndEmissive.filterMode = FilterMode.Point;
            volumeLightWithAlbedoAndEmissive.volumeDepth = voxelResolution.z;
            volumeLightWithAlbedoAndEmissive.enableRandomWrite = true;
            volumeLightWithAlbedoAndEmissive.Create();

            combineBuffers.GetKernelThreadGroupSizes(ComputeShader_AddBuffers, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            //feed the compute shader the textures that will be added together
            combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferA", volumeCombinedLightWithAlbedo);
            combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferB", voxelEmissiveBuffer);
            combineBuffers.SetTexture(ComputeShader_AddBuffers, "Write", volumeLightWithAlbedoAndEmissive);

            //let the GPU add the textures together.
            combineBuffers.Dispatch(ComputeShader_AddBuffers, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

            //save results to the disk
            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeLightWithAlbedoAndEmissive, voxelCombinedDirectLightSurfaceAlbedoBufferAssetPath);

            //|||||||||||||||||||||||||||||||||||||||||| ADD EMISSIVE LIGHT TO COMBINED LIGHT ONLY ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADD EMISSIVE LIGHT TO COMBINED LIGHT ONLY ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADD EMISSIVE LIGHT TO COMBINED LIGHT ONLY ||||||||||||||||||||||||||||||||||||||||||

            RenderTexture volumeLightWithEmissive = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            volumeLightWithEmissive.dimension = TextureDimension.Tex3D;
            volumeLightWithEmissive.wrapMode = TextureWrapMode.Clamp;
            volumeLightWithEmissive.filterMode = FilterMode.Point;
            volumeLightWithEmissive.volumeDepth = voxelResolution.z;
            volumeLightWithEmissive.enableRandomWrite = true;
            volumeLightWithEmissive.Create();

            combineBuffers.GetKernelThreadGroupSizes(ComputeShader_AddBuffers, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            //feed the compute shader the textures that will be added together
            combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferA", volumeLighting1);
            combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferB", voxelEmissiveBuffer);
            combineBuffers.SetTexture(ComputeShader_AddBuffers, "Write", volumeLightWithEmissive);

            //let the GPU add the textures together.
            combineBuffers.Dispatch(ComputeShader_AddBuffers, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

            //save results to the disk
            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeLightWithEmissive, voxelCombinedDirectLightSurfaceBufferAssetPath);

            Debug.Log(string.Format("'CombineDirectSurfaceLightingTerms' took {0} seconds.", Time.realtimeSinceStartup - timeBeforeFunction));
            VolumeGeneratorUtility.CloseProgressBar();
        }

        //|||||||||||||||||||||||||||||||||||||||||| STEP 8: TRACE BOUNCE SURFACE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 8: TRACE BOUNCE SURFACE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 8: TRACE BOUNCE SURFACE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //This is where we perform bounced SURFACE lighting on the voxelized scene.
        //
        //This is the second most intensive operation we do.
        //Luckily it doesn't scale with the amount of lights we have, but it does obviously scale with voxel resolution and the amount of samples we do.

        public void TraceBounceSurfaceLighting()
        {
            VolumeGeneratorUtility.UpdateProgressBar("Preparing to bounce surface light...", 0.5f);

            float timeBeforeFunction = Time.realtimeSinceStartup;

            volumeGeneratorAssets.PrepareAssetFolders();
            volumeGeneratorAssets.GetResources();
            GetGeneratedContent(); //Load up all of our generated content so we can use it.

            //consruct our render texture that we will write into
            RenderTexture volumeWrite = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            volumeWrite.dimension = TextureDimension.Tex3D;
            volumeWrite.wrapMode = TextureWrapMode.Clamp;
            volumeWrite.filterMode = FilterMode.Point;
            volumeWrite.volumeDepth = voxelResolution.z;
            volumeWrite.enableRandomWrite = true;
            volumeWrite.Create();

            //the current bounce that we are on, we will start off with bouncing from the direct surface lighting.
            Texture3D bounceTemp = voxelCombinedDirectLightSurfaceAlbedoBuffer;

            //|||||||||||||||||||||||||||||||||||||||||| COMPUTING BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMPUTING BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMPUTING BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            //fetch our main bounce surface light function kernel in the compute shader
            int ComputeShader_TraceSurfaceBounceLight = voxelBounceSurfaceLight.FindKernel("ComputeShader_TraceSurfaceBounceLight");
            voxelBounceSurfaceLight.GetKernelThreadGroupSizes(ComputeShader_TraceSurfaceBounceLight, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            //make sure the compute shader knows the following parameters.
            voxelBounceSurfaceLight.SetVector("VolumeResolution", new Vector4(voxelResolution.x, voxelResolution.y, voxelResolution.z, 0));
            voxelBounceSurfaceLight.SetVector("VolumePosition", voxelBounds.center);
            voxelBounceSurfaceLight.SetVector("VolumeSize", voxelSize);
            voxelBounceSurfaceLight.SetInt("MaxBounceSamples", bounceSurfaceSamples);
            voxelBounceSurfaceLight.SetFloat("IndirectIntensity", indirectIntensity);
            voxelBounceSurfaceLight.SetFloat("AlbedoBoost", albedoBoost);
            voxelBounceSurfaceLight.SetFloat("EnvironmentIntensity", environmentIntensity);

            //if enabled, use a normal oriented cosine hemisphere for better ray allocation/quality (though it has some issues/quirks)
            VolumeGeneratorUtility.SetComputeKeyword(voxelBounceSurfaceLight, "NORMAL_ORIENTED_HEMISPHERE_SAMPLING", normalOrientedHemisphereSampling);

            //build a small dummy compute buffer, so that we can use GetData later to perform a CPU stall.
            ComputeBuffer dummyComputeBuffer = new ComputeBuffer(1, 4);
            dummyComputeBuffer.SetData(new int[1]);

            for (int i = 0; i < bounces; i++)
            {
                for (int j = 0; j < bounceSurfaceSamples; j++)
                {
                    //randomize the seed for noise sampling (THIS IS IMPORTANT)
                    voxelBounceSurfaceLight.SetFloat("RandomSeed", UnityEngine.Random.value * 100000.0f);

                    //feed our compute shader the appropriate buffers so we can use them.
                    voxelBounceSurfaceLight.SetTexture(ComputeShader_TraceSurfaceBounceLight, "SceneAlbedo", voxelAlbedoBuffer); //important, used for "occlusion" checking.
                    voxelBounceSurfaceLight.SetTexture(ComputeShader_TraceSurfaceBounceLight, "SceneNormal", voxelNormalBuffer); //important, used to help orient hemispheres when enabled.
                    voxelBounceSurfaceLight.SetTexture(ComputeShader_TraceSurfaceBounceLight, "DirectLightSurface", bounceTemp); //important, the main color that we will be bouncing around.
                    voxelBounceSurfaceLight.SetTexture(ComputeShader_TraceSurfaceBounceLight, "Write", volumeWrite);

                    //send a tiny dummy compute buffer so we can do a CPU stall later if enabled.
                    voxelBounceSurfaceLight.SetBuffer(ComputeShader_TraceSurfaceBounceLight, "DummyComputeBuffer", dummyComputeBuffer);

                    //let the GPU compute bounced surface lighting, and hope it can manage it :(
                    voxelBounceSurfaceLight.Dispatch(ComputeShader_TraceSurfaceBounceLight, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                    //Perform a deliberate stall on the CPU so we can make sure that we don't issue too many dispatches to the GPU and overburden it.
                    if (i % GPU_Readback_Limit == 0 && enableGPU_Readback_Limit)
                    {
                        int[] dummyData = new int[1];
                        dummyComputeBuffer.GetData(dummyData);
                    }

                    VolumeGeneratorUtility.UpdateProgressBar(string.Format("Bouncing Surface Light... [BOUNCES: {0} / {1}] [SAMPLES: {2} / {3}]", i + 1, bounces, j + 1, bounceSurfaceSamples), 0.5f);
                }

                //if we are doing more than 1 bounce
                if (i > 0)
                {
                    //convert our finished bounced lighting into a Texture3D so we can reuse it again for the next bounce
                    bounceTemp = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite, false, false, false);
                }
            }

            dummyComputeBuffer.Release();

            VolumeGeneratorUtility.UpdateProgressBar(string.Format("Saving to disk..."), 0.5f);

            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeWrite, voxelBounceLightSurfaceBufferAssetPath);

            GetGeneratedContent();

            //|||||||||||||||||||||||||||||||||||||||||| COMBINE BOUNCE LIGHT WITH ALBEDO ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMBINE BOUNCE LIGHT WITH ALBEDO ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMBINE BOUNCE LIGHT WITH ALBEDO ||||||||||||||||||||||||||||||||||||||||||

            RenderTexture volumeBounceLightWithAlbedo = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            volumeBounceLightWithAlbedo.dimension = TextureDimension.Tex3D;
            volumeBounceLightWithAlbedo.wrapMode = TextureWrapMode.Clamp;
            volumeBounceLightWithAlbedo.filterMode = FilterMode.Point;
            volumeBounceLightWithAlbedo.volumeDepth = voxelResolution.z;
            volumeBounceLightWithAlbedo.enableRandomWrite = true;
            volumeBounceLightWithAlbedo.Create();

            int ComputeShader_CombineAlbedoWithLighting = combineBuffers.FindKernel("ComputeShader_CombineAlbedoWithLighting");
            combineBuffers.GetKernelThreadGroupSizes(ComputeShader_CombineAlbedoWithLighting, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            combineBuffers.SetFloat("AlbedoBoost", albedoBoost);
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "BufferA", voxelAlbedoBuffer); //albedo
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "BufferB", voxelBounceLightSurfaceBuffer); //lighting
            combineBuffers.SetTexture(ComputeShader_CombineAlbedoWithLighting, "Write", volumeBounceLightWithAlbedo);
            combineBuffers.Dispatch(ComputeShader_CombineAlbedoWithLighting, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeBounceLightWithAlbedo, voxelBounceLightSurfaceAlbedoBufferAssetPath);

            Debug.Log(string.Format("'TraceBounceSurfaceLighting' took {0} seconds.", Time.realtimeSinceStartup - timeBeforeFunction));
            VolumeGeneratorUtility.CloseProgressBar();
        }

        //|||||||||||||||||||||||||||||||||||||||||| STEP 9: TRACE BOUNCE VOLUME LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 9: TRACE BOUNCE VOLUME LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 9: TRACE BOUNCE VOLUME LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //This is where we perform bounced VOLUMETRIC lighting on the voxelized scene.
        //
        //This is by far the most intensive operation we do.
        //Luckily it doesn't scale with the amount of lights we have, but it does obviously scale with voxel resolution and the amount of samples we do.

        public void TraceBounceVolumeLighting()
        {
            VolumeGeneratorUtility.UpdateProgressBar(string.Format("Preparing to bounce volumetric light..."), 0.5f);

            float timeBeforeFunction = Time.realtimeSinceStartup;

            volumeGeneratorAssets.PrepareAssetFolders();
            volumeGeneratorAssets.GetResources();
            GetGeneratedContent(); //Load up all of our generated content so we can use it.

            //consruct our render texture that we will write into
            RenderTexture volumeWrite = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            volumeWrite.dimension = TextureDimension.Tex3D;
            volumeWrite.wrapMode = TextureWrapMode.Clamp;
            volumeWrite.filterMode = FilterMode.Point;
            volumeWrite.volumeDepth = voxelResolution.z;
            volumeWrite.enableRandomWrite = true;
            volumeWrite.Create();

            //the current bounce that we are on, we will start off with bouncing from the direct surface lighting.
            Texture3D bounceTemp = voxelCombinedDirectLightSurfaceAlbedoBuffer;

            //|||||||||||||||||||||||||||||||||||||||||| ADDING FIRST BOUNCE ENVIRONMENT LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADDING FIRST BOUNCE ENVIRONMENT LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADDING FIRST BOUNCE ENVIRONMENT LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            if (enableEnvironmentLighting)
            {
                //fetch our function kernel in the compute shader
                int ComputeShader_AddBuffers = combineBuffers.FindKernel("ComputeShader_AddBuffers");
                combineBuffers.GetKernelThreadGroupSizes(ComputeShader_AddBuffers, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

                //feed the compute shader the textures that will be added together
                combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferA", bounceTemp);
                combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferB", voxelEnvironmentLightSurfaceBuffer);
                combineBuffers.SetTexture(ComputeShader_AddBuffers, "Write", volumeWrite);

                //let the GPU add the textures together.
                combineBuffers.Dispatch(ComputeShader_AddBuffers, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                bounceTemp = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite);
            }

            //|||||||||||||||||||||||||||||||||||||||||| COMPUTING BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMPUTING BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMPUTING BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||

            //fetch our main bounce volumetric light function kernel in the compute shader
            int ComputeShader_TraceVolumeBounceLight = voxelBounceVolumetricLight.FindKernel("ComputeShader_TraceVolumeBounceLight");
            voxelBounceVolumetricLight.GetKernelThreadGroupSizes(ComputeShader_TraceVolumeBounceLight, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            //make sure the compute shader knows the following parameters.
            voxelBounceVolumetricLight.SetVector("VolumeResolution", new Vector4(voxelResolution.x, voxelResolution.y, voxelResolution.z, 0));
            voxelBounceVolumetricLight.SetVector("VolumePosition", voxelBounds.center);
            voxelBounceVolumetricLight.SetVector("VolumeSize", voxelSize);
            voxelBounceVolumetricLight.SetInt("MaxBounceSamples", bounceVolumetricSamples);
            voxelBounceVolumetricLight.SetFloat("IndirectIntensity", indirectIntensity);
            voxelBounceVolumetricLight.SetFloat("AlbedoBoost", albedoBoost);

            //build a small dummy compute buffer, so that we can use GetData later to perform a CPU stall.
            ComputeBuffer dummyComputeBuffer = new ComputeBuffer(1, 4);
            dummyComputeBuffer.SetData(new int[1]);

            for (int i = 0; i < bounceVolumetricSamples; i++)
            {
                //randomize the seed for noise sampling (THIS IS IMPORTANT)
                voxelBounceVolumetricLight.SetFloat("RandomSeed", UnityEngine.Random.value * 100000.0f);

                //feed our compute shader the appropriate buffers so we can use them.
                voxelBounceVolumetricLight.SetTexture(ComputeShader_TraceVolumeBounceLight, "SceneAlbedo", voxelAlbedoBuffer); //important, used for "occlusion" checking.
                //voxelBounceVolumetricLight.SetTexture(ComputeShader_TraceVolumeBounceLight, "SceneNormal", voxelNormalBuffer); //this isn't used at all.
                voxelBounceVolumetricLight.SetTexture(ComputeShader_TraceVolumeBounceLight, "DirectLightSurface", bounceTemp); //important, the main color that we will be bouncing around.
                voxelBounceVolumetricLight.SetTexture(ComputeShader_TraceVolumeBounceLight, "Write", volumeWrite);

                //send a tiny dummy compute buffer so we can do a CPU stall later if enabled.
                voxelBounceVolumetricLight.SetBuffer(ComputeShader_TraceVolumeBounceLight, "DummyComputeBuffer", dummyComputeBuffer);

                voxelBounceVolumetricLight.Dispatch(ComputeShader_TraceVolumeBounceLight, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                //Perform a deliberate stall on the CPU so we can make sure that we don't issue too many dispatches to the GPU and overburden it.
                if (i % GPU_Readback_Limit == 0 && enableGPU_Readback_Limit)
                {
                    int[] dummyData = new int[1];
                    dummyComputeBuffer.GetData(dummyData);
                }

                VolumeGeneratorUtility.UpdateProgressBar(string.Format("Bouncing Volumetric Light... [SAMPLES: {0} / {1}]", i + 1, bounceVolumetricSamples), 0.5f);
            }

            dummyComputeBuffer.Release();

            //|||||||||||||||||||||||||||||||||||||||||| POST 3D GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| POST 3D GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| POST 3D GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||
            //In an attempt to squeeze more out of less...
            //We will now perform a 3D gaussian blur to smooth out the results from the bounced volumetric light.
            //(IF ITS ENABLED)

            if (volumetricBounceGaussianSamples > 0)
            {
                VolumeGeneratorUtility.UpdateProgressBar(string.Format("Performing Gaussian Blur..."), 0.5f);

                //fetch our main gaussian blur function kernel in the compute shader
                int ComputeShader_GaussianBlur = gaussianBlur.FindKernel("ComputeShader_GaussianBlur");
                gaussianBlur.GetKernelThreadGroupSizes(ComputeShader_GaussianBlur, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

                //convert the raw volumetric bounce light render texture into a texture3D so that it can be read.
                Texture3D tempRawVolumetricBounceLight = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite);

                //make sure the compute shader knows the following parameters.
                gaussianBlur.SetVector("VolumeResolution", new Vector4(voxelResolution.x, voxelResolution.y, voxelResolution.z, 0));
                gaussianBlur.SetInt("BlurSamples", volumetricBounceGaussianSamples);

                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //set the gaussian blur direction for this pass.
                gaussianBlur.SetVector("BlurDirection", new Vector4(1, 0, 0, 0));

                //feed our compute shader the appropriate textures.
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Read", tempRawVolumetricBounceLight);
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Write", volumeWrite);

                //let the GPU perform a gaussian blur along the given axis.
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                //|||||||||||||||||||||||||||||||||||||||||| BLUR Y PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Y PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Y PASS ||||||||||||||||||||||||||||||||||||||||||
                //get the result from the x pass and convert it into a texture3D so that it can be read again.
                Texture3D tempBlurX = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite);

                //set the gaussian blur direction for this pass.
                gaussianBlur.SetVector("BlurDirection", new Vector4(0, 1, 0, 0));

                //feed our compute shader the appropriate textures.
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Read", tempBlurX);
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Write", volumeWrite);

                //let the GPU perform a gaussian blur along the given axis.
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                //|||||||||||||||||||||||||||||||||||||||||| BLUR Z PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Z PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR Z PASS ||||||||||||||||||||||||||||||||||||||||||
                //get the result from the y pass and convert it into a texture3D so that it can be read one more time.
                Texture3D tempBlurY = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite);

                //set the gaussian blur direction for this pass.
                gaussianBlur.SetVector("BlurDirection", new Vector4(0, 0, 1, 0));

                //feed our compute shader the appropriate textures.
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Read", tempBlurY);
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Write", volumeWrite);

                //let the GPU perform a gaussian blur along the given axis.
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));
            }

            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| RESULT ||||||||||||||||||||||||||||||||||||||||||
            //if we have SOMEHOW survived the onslaught of computations... FANTASTIC! lets save our results to the disk before we lose it.

            VolumeGeneratorUtility.UpdateProgressBar(string.Format("Saving to disk..."), 0.5f);

            //SAVE IT!
            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeWrite, voxelBounceLightVolumeBufferAssetPath);

            Debug.Log(string.Format("'TraceBounceVolumeLighting' took {0} seconds.", Time.realtimeSinceStartup - timeBeforeFunction));
            VolumeGeneratorUtility.CloseProgressBar();
        }

        //|||||||||||||||||||||||||||||||||||||||||| STEP 10: COMBINE VOLUME DIRECT AND BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 10: COMBINE VOLUME DIRECT AND BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| STEP 10: COMBINE VOLUME DIRECT AND BOUNCE LIGHTING ||||||||||||||||||||||||||||||||||||||||||
        //This is where we simply combine the generated volumetric light buffers into one single texture.
        //This is a light operation, so no worries here.

        public void CombineVolumeLighting()
        {
            VolumeGeneratorUtility.UpdateProgressBar(string.Format("Combining Volume Lighting..."), 0.5f);

            float timeBeforeFunction = Time.realtimeSinceStartup;

            volumeGeneratorAssets.PrepareAssetFolders();
            volumeGeneratorAssets.GetResources();
            GetGeneratedContent(); //Load up all of our generated content so we can use it.

            //consruct our render texture that we will write into
            RenderTexture volumeWrite = new RenderTexture(voxelResolution.x, voxelResolution.y, 0, renderTextureFormat);
            volumeWrite.dimension = TextureDimension.Tex3D;
            volumeWrite.wrapMode = TextureWrapMode.Clamp;
            volumeWrite.filterMode = FilterMode.Point;
            volumeWrite.volumeDepth = voxelResolution.z;
            volumeWrite.enableRandomWrite = true;
            volumeWrite.Create();

            //fetch our function kernel in the compute shader
            int ComputeShader_AddBuffers = combineBuffers.FindKernel("ComputeShader_AddBuffers");
            combineBuffers.GetKernelThreadGroupSizes(ComputeShader_AddBuffers, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);

            //get the result from the y pass and convert it into a texture3D so that it can be read one more time.
            Texture3D addedColorsTemp = voxelDirectLightVolumeBuffer;

            //|||||||||||||||||||||||||||||||||||||||||| ADD VOLUMETRIC ENVIRONMENT LIGHT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADD VOLUMETRIC ENVIRONMENT LIGHT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADD VOLUMETRIC ENVIRONMENT LIGHT ||||||||||||||||||||||||||||||||||||||||||

            if (enableEnvironmentLighting)
            {
                //feed the compute shader the textures that will be added together
                combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferA", addedColorsTemp);
                combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferB", voxelEnvironmentLightVolumeBuffer);
                combineBuffers.SetTexture(ComputeShader_AddBuffers, "Write", volumeWrite);

                //let the GPU add the textures together.
                combineBuffers.Dispatch(ComputeShader_AddBuffers, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

                //get the result from the x pass and convert it into a texture3D so that it can be read again.
                addedColorsTemp = renderTextureConverter.ConvertRenderTexture3DToTexture3D(volumeWrite);
            }

            //|||||||||||||||||||||||||||||||||||||||||| ADD VOLUMETRIC BOUNCE LIGHT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADD VOLUMETRIC BOUNCE LIGHT ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| ADD VOLUMETRIC BOUNCE LIGHT ||||||||||||||||||||||||||||||||||||||||||

            //feed the compute shader the textures that will be added together
            combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferA", addedColorsTemp);
            combineBuffers.SetTexture(ComputeShader_AddBuffers, "BufferB", voxelBounceLightVolumeBuffer);
            combineBuffers.SetTexture(ComputeShader_AddBuffers, "Write", volumeWrite);

            //let the GPU add the textures together.
            combineBuffers.Dispatch(ComputeShader_AddBuffers, Mathf.CeilToInt(voxelResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(voxelResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(voxelResolution.z / THREAD_GROUP_SIZE_Z));

            //save it!
            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeWrite, voxelCombinedVolumetricBufferAssetPath);

            Debug.Log(string.Format("'CombineVolumeLighting' took {0} seconds.", Time.realtimeSinceStartup - timeBeforeFunction));
            VolumeGeneratorUtility.CloseProgressBar();
        }

        public void GenerateVolume()
        {
            CleanUpGeneratedContent();
            GenerateAlbedoEmissiveNormalBuffers();

            if (enableEnvironmentLighting)
                CaptureEnvironment();

            TraceDirectSurfaceLighting();
            TraceDirectVolumeLighting();

            if (enableEnvironmentLighting)
            {
                TraceEnvironmentSurfaceLighting();
                TraceEnvironmentVolumeLighting();
            }

            CombineDirectSurfaceLightingTerms();
            TraceBounceSurfaceLighting();
            TraceBounceVolumeLighting();
            CombineVolumeLighting();
        }
    }
}
#endif