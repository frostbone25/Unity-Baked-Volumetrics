#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BakedVolumetrics
{
    public class SampleIBL : MonoBehaviour
    {
        public int sampleResolution = 32;

        public float occlusionLeakFactor = 1.0f;
        public bool occlusionPreventLeaks = false;
        public bool indoorOnlySamples = false;

        //private
        [HideInInspector] public bool showUI;

        private int IBLComputeKernel;
        private ComputeShader IBLCompute;
        private Shader IBLView;
        private Camera IBLCamera;

        private RenderTexture IBLCameraRender;
        private RenderTexture IBLComputeResult;
        private Texture2D IBLComputeProxy;

        private int dispatchX = 0;
        private int dispatchY = 0;

        private static readonly RenderTextureFormat rtFormat = RenderTextureFormat.ARGBHalf;
        private static readonly TextureFormat textureFormat = TextureFormat.RGBAHalf;

        public void Setup()
        {
            if (IBLCompute == null)
                IBLCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BakedVolumetrics/ComputeShaders/IBL.compute");

            if (IBLCamera == null)
            {
                GameObject IBLCameraGameObject = new GameObject("IBL_CAMERA");
                IBLCamera = IBLCameraGameObject.AddComponent<Camera>();
                IBLCamera.forceIntoRenderTexture = true;
                IBLCamera.nearClipPlane = 0.01f;
                IBLCamera.farClipPlane = 1000.0f;
                IBLCamera.backgroundColor = Color.black;
                IBLCamera.clearFlags = CameraClearFlags.SolidColor;
            }

            if(IBLView == null)
            {
                IBLView = Shader.Find("Hidden/IBLView");
            }

            //setup compute shader
            IBLComputeKernel = IBLCompute.FindKernel("CSMain");
            IBLCompute.SetVector("TextureResolution", new Vector4(sampleResolution, sampleResolution, 0, 0));
            dispatchX = sampleResolution / 8;
            dispatchY = sampleResolution / 8;
        }

        public Color SampleVolumetricColor(Vector3 probePosition, Vector3 voxelWorldSize)
        {
            //||||||||||||||||||||||||||||| OCCLUSION AND LEAK TESTS |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| OCCLUSION AND LEAK TESTS |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| OCCLUSION AND LEAK TESTS |||||||||||||||||||||||||||||
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

            if (!test_indoor)
                return Color.black;

            if (!test_leak)
                return Color.black;

            //||||||||||||||||||||||||||||| SETUP RENDERING |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| SETUP RENDERING |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| SETUP RENDERING |||||||||||||||||||||||||||||

            //setup the render texture for the compute shader
            IBLComputeResult = new RenderTexture(sampleResolution, sampleResolution, 0, rtFormat);
            IBLComputeResult.enableRandomWrite = true;
            IBLComputeResult.Create();

            //set the camera rotation and position
            IBLCamera.transform.rotation = Quaternion.Euler(0, 0, 0);
            IBLCamera.transform.position = probePosition;

            //IBLCamera.SetReplacementShader(IBLView, "");

            //||||||||||||||||||||||||||||| RENDER EACH FACE |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| RENDER EACH FACE |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| RENDER EACH FACE |||||||||||||||||||||||||||||

            //front (+X)  
            if(IBLCameraRender == null)
            {
                IBLCameraRender = new RenderTexture(sampleResolution, sampleResolution, 0, rtFormat);
                IBLCameraRender.Create();
            }

            IBLCamera.targetTexture = IBLCameraRender;

            //front (+X)  
            IBLCamera.transform.Rotate(0, 90, 0);
            IBLCamera.Render();
            IBLCompute.SetTexture(IBLComputeKernel, "Texture1", IBLCameraRender);
            IBLCompute.SetTexture(IBLComputeKernel, "Result", IBLComputeResult);
            IBLCompute.Dispatch(IBLComputeKernel, dispatchX, dispatchY, 1);

            //right (-Z)
            IBLCamera.transform.Rotate(0, 90, 0);
            IBLCamera.Render();
            IBLCompute.Dispatch(IBLComputeKernel, dispatchX, dispatchY, 1);

            //back (-X)
            IBLCamera.transform.Rotate(0, 90, 0);
            IBLCamera.Render();
            IBLCompute.Dispatch(IBLComputeKernel, dispatchX, dispatchY, 1);

            //left (+Z)
            IBLCamera.transform.Rotate(0, 90, 0);
            IBLCamera.Render();
            IBLCompute.Dispatch(IBLComputeKernel, dispatchX, dispatchY, 1);

            //+Y  (up) (left then up)
            IBLCamera.transform.Rotate(90, 0, 0);
            IBLCamera.Render();
            IBLCompute.Dispatch(IBLComputeKernel, dispatchX, dispatchY, 1);

            //-Y (down) (left then down)
            IBLCamera.transform.Rotate(180, 0, 0);
            IBLCamera.Render();
            IBLCompute.Dispatch(IBLComputeKernel, dispatchX, dispatchY, 1);
            IBLComputeProxy = RTConverter.ConvertFromRenderTexture2D(IBLComputeResult, textureFormat, true);

            //||||||||||||||||||||||||||||| FINAL COLOR |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| FINAL COLOR |||||||||||||||||||||||||||||
            //||||||||||||||||||||||||||||| FINAL COLOR |||||||||||||||||||||||||||||

            return IBLComputeProxy.GetPixelBilinear(0.5f, 0.5f, IBLComputeResult.mipmapCount - 1) / 6.0f;
        }

        public void Cleanup()
        {
            IBLComputeResult.Release();
            IBLCameraRender.Release();

            if (IBLComputeProxy != null)
            {
                DestroyImmediate(IBLComputeProxy);
                IBLComputeProxy = null;
            }

            if (IBLCamera != null)
            {
                DestroyImmediate(IBLCamera.gameObject);
                IBLCamera = null;
            }
        }
    }
}
#endif