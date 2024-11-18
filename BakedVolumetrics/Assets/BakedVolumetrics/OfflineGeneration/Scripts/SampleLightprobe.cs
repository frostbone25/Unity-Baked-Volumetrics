#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using BakedVolumetrics;
using System.Diagnostics.Contracts;
using System;

namespace BakedVolumetricsOffline
{
    public class SampleLightprobe
    {
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES ||||||||||||||||||||||||||||||||||||||||||

        public float occlusionLeakFactor = 1.0f;
        public bool occlusionPreventLeaks = false;
        public bool indoorOnlySamples = false;

        [Range(0, 64)] public int gaussianBlurSamples = 4;

        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||

        private RenderTextureConverter renderTextureConverter;
        private VolumeGenerator volumeGenerator;
        private VolumeGeneratorAssets volumeGeneratorAssets;

        private string volumeName => volumeGenerator.volumeName;
        private Vector3 volumePosition => volumeGenerator.transform.position;
        private Vector3Int volumeResolution => volumeGenerator.GetVoxelResolution();
        private Vector3 volumeSize => volumeGenerator.volumeSize;

        private string lightprobeVolumeAssetPath => string.Format("{0}/{1}_LightProbe.asset", volumeGeneratorAssets.localAssetSceneDataFolder, volumeName);

        //Size of the thread groups for compute shaders.
        //These values should match the #define ones in the compute shaders.
        private static uint THREAD_GROUP_SIZE_X = 4;
        private static uint THREAD_GROUP_SIZE_Y = 4;
        private static uint THREAD_GROUP_SIZE_Z = 4;

        private ComputeShader adjustments => volumeGeneratorAssets.adjustments;
        private ComputeShader gaussianBlur => volumeGeneratorAssets.gaussianBlur;
        private ComputeShader density => volumeGeneratorAssets.density;

        private static string sceneStaticCollidersName = "TEMP_SceneStaticColliders";
        private GameObject sceneStaticColliders;

        //|||||||||||||||||||||||||||||||||||||||||| CONSTRUCTOR ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CONSTRUCTOR ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CONSTRUCTOR ||||||||||||||||||||||||||||||||||||||||||

        public SampleLightprobe(VolumeGenerator volumeGenerator, VolumeGeneratorAssets volumeGeneratorAssets)
        {
            this.volumeGenerator = volumeGenerator;
            this.volumeGeneratorAssets = volumeGeneratorAssets;

            renderTextureConverter = new RenderTextureConverter();
        }

        //|||||||||||||||||||||||||||||||||||||||||| GETTERS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| GETTERS ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| GETTERS ||||||||||||||||||||||||||||||||||||||||||

        public Texture3D GetFinalGeneratedVolume() => AssetDatabase.LoadAssetAtPath<Texture3D>(lightprobeVolumeAssetPath);

        //|||||||||||||||||||||||||||||||||||||||||| GENERATE LIGHTPROBE VOLUME ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| GENERATE LIGHTPROBE VOLUME ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| GENERATE LIGHTPROBE VOLUME ||||||||||||||||||||||||||||||||||||||||||

        public void GenerateVolume()
        {
            volumeGeneratorAssets.GetResources();

            //double timeBeforeBake = Time.realtimeSinceStartupAsDouble;
            double timeBeforeBake = Time.realtimeSinceStartup;

            VolumeGeneratorUtility.UpdateProgressBar("Preparing to generate Light Probe volume...", 0.5f);

            //|||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SETUP ||||||||||||||||||||||||||||||||||||||||||

            SetupSceneColliders();

            Texture3D newVolume = new Texture3D(volumeResolution.x, volumeResolution.y, volumeResolution.z, volumeGenerator.GetTextureFormat(), false);
            newVolume.wrapMode = TextureWrapMode.Clamp;
            newVolume.filterMode = FilterMode.Bilinear;

            //|||||||||||||||||||||||||||||||||||||||||| SAMPLING LIGHTPROBES ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAMPLING LIGHTPROBES ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAMPLING LIGHTPROBES ||||||||||||||||||||||||||||||||||||||||||

            VolumeGeneratorUtility.UpdateProgressBar("Sampling Light Probes...", 0.5f);

            for (int x = -volumeResolution.x / 2; x <= volumeResolution.x / 2; x++)
            {
                float x_offset = volumeSize.x / volumeResolution.x;

                for (int y = -volumeResolution.y / 2; y <= volumeResolution.y / 2; y++)
                {
                    float y_offset = volumeSize.y / volumeResolution.y;

                    for (int z = -volumeResolution.z / 2; z <= volumeResolution.z / 2; z++)
                    {
                        float z_offset = volumeSize.z / volumeResolution.z;

                        Vector3 probePosition = new Vector3(volumePosition.x + (x * x_offset), volumePosition.y + (y * y_offset), volumePosition.z + (z * z_offset));
                        Vector3 voxelWorldSize = new Vector3(x_offset, y_offset, z_offset);

                        //|||||||||||||||||||| COLOR (RGB) ||||||||||||||||||||||||
                        //|||||||||||||||||||| COLOR (RGB) ||||||||||||||||||||||||
                        //|||||||||||||||||||| COLOR (RGB) ||||||||||||||||||||||||
                        Color colorResult = Color.black;

                        bool test_leak = occlusionPreventLeaks ? (Physics.CheckBox(probePosition, voxelWorldSize * occlusionLeakFactor) == false) : true;
                        bool test_indoor = true;

                        if (indoorOnlySamples)
                        {
                            bool hit_up = Physics.Raycast(probePosition, Vector3.up, float.MaxValue);
                            bool hit_down = Physics.Raycast(probePosition, Vector3.down, float.MaxValue);
                            bool hit_left = Physics.Raycast(probePosition, Vector3.left, float.MaxValue);
                            bool hit_right = Physics.Raycast(probePosition, Vector3.right, float.MaxValue);
                            bool hit_forward = Physics.Raycast(probePosition, Vector3.forward, float.MaxValue);
                            bool hit_back = Physics.Raycast(probePosition, Vector3.back, float.MaxValue);

                            test_indoor = hit_up && hit_down && hit_left && hit_right && hit_forward && hit_back;
                        }

                        if (!test_indoor || !test_leak)
                        {
                            colorResult = Color.black;
                        }
                        else
                        {
                            SphericalHarmonicsL2 sphericalHarmonicsL2 = new SphericalHarmonicsL2();
                            Renderer renderer = new Renderer();

                            LightProbes.GetInterpolatedProbe(probePosition, renderer, out sphericalHarmonicsL2);

                            Vector3[] sampledDirections = new Vector3[1]
                            {
                                Vector3.zero
                            };

                            Color[] resultingColors = new Color[1];

                            sphericalHarmonicsL2.Evaluate(sampledDirections, resultingColors);

                            colorResult = resultingColors[0];
                        }

                        colorResult = new Color(colorResult.r, colorResult.g, colorResult.b, 1.0f);

                        //|||||||||||||||||||| FINAL ||||||||||||||||||||||||
                        //|||||||||||||||||||| FINAL ||||||||||||||||||||||||
                        //|||||||||||||||||||| FINAL ||||||||||||||||||||||||
                        Vector3Int volumeTexturePosition = new Vector3Int(x + (volumeResolution.x / 2), y + (volumeResolution.y / 2), z + (volumeResolution.z / 2));
                        newVolume.SetPixel(volumeTexturePosition.x, volumeTexturePosition.y, volumeTexturePosition.z, colorResult);
                    }
                }
            }

            newVolume.Apply();

            RemoveSceneColliders();

            //|||||||||||||||||||||||||||||||||||||||||| COMPUTE SHADER SETUP ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMPUTE SHADER SETUP ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| COMPUTE SHADER SETUP ||||||||||||||||||||||||||||||||||||||||||

            //consruct our render texture that we will write into
            RenderTexture volumeWrite = new RenderTexture(volumeResolution.x, volumeResolution.y, 0, volumeGenerator.GetRenderTextureFormat());
            volumeWrite.dimension = TextureDimension.Tex3D;
            volumeWrite.volumeDepth = volumeResolution.z;
            volumeWrite.enableRandomWrite = true;
            volumeWrite.Create();

            //|||||||||||||||||||||||||||||||||||||||||| APPLYING POST GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| APPLYING POST GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| APPLYING POST GAUSSIAN BLUR ||||||||||||||||||||||||||||||||||||||||||

            VolumeGeneratorUtility.UpdateProgressBar("Applying Gaussian Blur...", 0.5f);

            if (gaussianBlurSamples > 0)
            {
                //fetch our main gaussian blur function kernel in the compute shader
                int ComputeShader_GaussianBlur = gaussianBlur.FindKernel("ComputeShader_GaussianBlur");

                //make sure the compute shader knows the following parameters.
                gaussianBlur.SetVector("VolumeResolution", new Vector4(volumeResolution.x, volumeResolution.y, volumeResolution.z, 0));
                gaussianBlur.SetInt("BlurSamples", gaussianBlurSamples);

                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //|||||||||||||||||||||||||||||||||||||||||| BLUR X PASS ||||||||||||||||||||||||||||||||||||||||||
                //set the gaussian blur direction for this pass.
                gaussianBlur.SetVector("BlurDirection", new Vector4(1, 0, 0, 0));

                //feed our compute shader the appropriate textures.
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Read", newVolume);
                gaussianBlur.SetTexture(ComputeShader_GaussianBlur, "Write", volumeWrite);

                //let the GPU perform a gaussian blur along the given axis.
                gaussianBlur.GetKernelThreadGroupSizes(ComputeShader_GaussianBlur, out THREAD_GROUP_SIZE_X, out THREAD_GROUP_SIZE_Y, out THREAD_GROUP_SIZE_Z);
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(volumeResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(volumeResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(volumeResolution.z / THREAD_GROUP_SIZE_Z));

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
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(volumeResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(volumeResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(volumeResolution.z / THREAD_GROUP_SIZE_Z));

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
                gaussianBlur.Dispatch(ComputeShader_GaussianBlur, Mathf.CeilToInt(volumeResolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(volumeResolution.y / THREAD_GROUP_SIZE_Y), Mathf.CeilToInt(volumeResolution.z / THREAD_GROUP_SIZE_Z));
            }

            //|||||||||||||||||||||||||||||||||||||||||| SAVING RESULTS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAVING RESULTS ||||||||||||||||||||||||||||||||||||||||||
            //|||||||||||||||||||||||||||||||||||||||||| SAVING RESULTS ||||||||||||||||||||||||||||||||||||||||||

            VolumeGeneratorUtility.UpdateProgressBar("Saving to disk...", 0.5f);

            //save it!
            renderTextureConverter.SaveRenderTexture3DAsTexture3D(volumeWrite, lightprobeVolumeAssetPath);

            //we are done with this, so clean up.
            volumeWrite.Release();

            //double timeAfterBake = Time.realtimeSinceStartupAsDouble - timeBeforeBake;
            double timeAfterBake = Time.realtimeSinceStartup - timeBeforeBake;
            Debug.Log(string.Format("'{0}' took {1} seconds to bake.", volumeName, timeAfterBake));

            VolumeGeneratorUtility.CloseProgressBar();
        }

        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SCENE COLLIDERS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SCENE COLLIDERS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| SCENE COLLIDERS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        /// <summary>
        /// Spawns mesh colliders for all objects in the scene marked with the 'ContributeGI' static flag.
        /// <para>This is used in general for features (if they are enabled) like preventing light leaks, doing occlusion, or getting indoor only samples.</para>
        /// </summary>
        public void SetupSceneColliders()
        {
            sceneStaticColliders = new GameObject(sceneStaticCollidersName);

            MeshFilter[] meshes = MonoBehaviour.FindObjectsOfType<MeshFilter>();

            for (int i = 0; i < meshes.Length; i++)
            {
                GameObject meshGameObject = meshes[i].gameObject;
                MeshRenderer meshRenderer = meshGameObject.GetComponent<MeshRenderer>();

                StaticEditorFlags staticEditorFlags = GameObjectUtility.GetStaticEditorFlags(meshGameObject);

                if (meshRenderer != null && staticEditorFlags.HasFlag(StaticEditorFlags.ContributeGI))
                {
                    GameObject sceneColliderChild = new GameObject("collider");
                    sceneColliderChild.transform.SetParent(sceneStaticColliders.transform);

                    sceneColliderChild.transform.position = meshGameObject.transform.position;
                    sceneColliderChild.transform.rotation = meshGameObject.transform.rotation;
                    sceneColliderChild.transform.localScale = meshGameObject.transform.localScale;

                    MeshCollider meshCollider = sceneColliderChild.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshes[i].sharedMesh;
                }
            }
        }

        public void RemoveSceneColliders()
        {
            if (sceneStaticColliders != null)
                MonoBehaviour.DestroyImmediate(sceneStaticColliders);
            else
            {
                sceneStaticColliders = GameObject.Find(sceneStaticCollidersName);

                if (sceneStaticColliders != null)
                    MonoBehaviour.DestroyImmediate(sceneStaticColliders);
            }
        }
    }
}
#endif