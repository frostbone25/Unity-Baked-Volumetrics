#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;

namespace BakedVolumetricsOffline
{
    public class RenderTextureConverter
    {
        private Texture2D convertedTexture2D;
        private Texture3D convertedTexture3D;

        public Texture2D ConvertRenderTexture2DToTexture2D(RenderTexture renderTexture2D, bool generateMips = false, bool readable = false, bool releaseRenderTexture = false)
        {
            int width = renderTexture2D.width;
            int height = renderTexture2D.height;
            //int renderTextureMemorySize = (int)Profiler.GetRuntimeMemorySizeLong(renderTexture2D);
            int renderTextureMemorySize = (int)RenderTextureSize.GetRenderTextureMemorySize(renderTexture2D);

            NativeArray<byte> nativeArray = new NativeArray<byte>(renderTextureMemorySize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            AsyncGPUReadbackRequest request = AsyncGPUReadback.RequestIntoNativeArray(ref nativeArray, renderTexture2D, 0, (request) =>
            {
                convertedTexture2D = new Texture2D(width, height, renderTexture2D.graphicsFormat, generateMips ? TextureCreationFlags.MipChain : TextureCreationFlags.None);
                convertedTexture2D.filterMode = convertedTexture2D.filterMode;
                convertedTexture2D.SetPixelData(nativeArray, 0);
                convertedTexture2D.Apply(generateMips, !readable);

                nativeArray.Dispose();

                if(releaseRenderTexture)
                    renderTexture2D.Release();
            });

            request.WaitForCompletion();

            return convertedTexture2D;
        }

        public void SaveRenderTexture2DAsTexture2D(RenderTexture renderTexture2D, string assetRealtivePath, bool generateMips = false, bool readable = false, bool releaseRenderTexture = false)
        {
            Texture2D converted = ConvertRenderTexture2DToTexture2D(renderTexture2D, generateMips, readable, releaseRenderTexture);
            AssetDatabase.CreateAsset(converted, assetRealtivePath);
            AssetDatabase.SaveAssetIfDirty(converted);
        }

        public void SaveAsyncRenderTexture2DAsTexture2D(RenderTexture renderTexture2D, string assetRealtivePath, bool generateMips = false, bool readable = false)
        {
            int width = renderTexture2D.width;
            int height = renderTexture2D.height;
            //int renderTextureMemorySize = (int)Profiler.GetRuntimeMemorySizeLong(renderTexture2D);
            int renderTextureMemorySize = (int)RenderTextureSize.GetRenderTextureMemorySize(renderTexture2D);

            NativeArray<byte> nativeArray = new NativeArray<byte>(renderTextureMemorySize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            AsyncGPUReadbackRequest request = AsyncGPUReadback.RequestIntoNativeArray(ref nativeArray, renderTexture2D, 0, (request) =>
            {
                convertedTexture2D = new Texture2D(width, height, renderTexture2D.graphicsFormat, generateMips ? TextureCreationFlags.MipChain : TextureCreationFlags.None);
                convertedTexture2D.filterMode = convertedTexture2D.filterMode;
                convertedTexture2D.SetPixelData(nativeArray, 0);
                convertedTexture2D.Apply(generateMips, !readable);

                nativeArray.Dispose();
                renderTexture2D.Release();

                AssetDatabase.CreateAsset(convertedTexture2D, assetRealtivePath);
                AssetDatabase.SaveAssetIfDirty(convertedTexture2D);
            });
        }

        public Texture3D ConvertRenderTexture3DToTexture3D(RenderTexture renderTexture3D, bool generateMips = false, bool readable = false, bool releaseRenderTexture = false)
        {
            int width = renderTexture3D.width;
            int height = renderTexture3D.height;
            int depth = renderTexture3D.volumeDepth;
            //int renderTextureMemorySize = (int)Profiler.GetRuntimeMemorySizeLong(renderTexture3D);
            int renderTextureMemorySize = (int)RenderTextureSize.GetRenderTextureMemorySize(renderTexture3D);

            NativeArray<byte> nativeArray = new NativeArray<byte>(renderTextureMemorySize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            AsyncGPUReadbackRequest request = AsyncGPUReadback.RequestIntoNativeArray(ref nativeArray, renderTexture3D, 0, (request) =>
            {
                convertedTexture3D = new Texture3D(width, height, depth, renderTexture3D.graphicsFormat, generateMips ? TextureCreationFlags.MipChain : TextureCreationFlags.None);
                convertedTexture3D.filterMode = renderTexture3D.filterMode;
                convertedTexture3D.SetPixelData(nativeArray, 0);
                convertedTexture3D.Apply(generateMips, !readable);

                nativeArray.Dispose();

                if(releaseRenderTexture)
                    renderTexture3D.Release();
            });

            request.WaitForCompletion();

            return convertedTexture3D;
        }

        public void SaveRenderTexture3DAsTexture3D(RenderTexture renderTexture3D, string assetRealtivePath, bool generateMips = false, bool readable = false)
        {
            Texture3D converted = ConvertRenderTexture3DToTexture3D(renderTexture3D, generateMips, readable);
            AssetDatabase.CreateAsset(converted, assetRealtivePath);
            AssetDatabase.SaveAssetIfDirty(converted);
        }

        public void SaveRenderTexture3DAsTexture3D(RenderTexture renderTexture3D, string assetRealtivePath, TextureFormat newTextureFormat, bool generateMips = false, bool readable = false)
        {
            Texture3D converted = ConvertRenderTexture3DToTexture3D(renderTexture3D, generateMips, readable);
            Texture3D convertedNewFormat = new Texture3D(converted.width, converted.height, converted.depth, newTextureFormat, false);
            convertedNewFormat.filterMode = converted.filterMode;
            convertedNewFormat.wrapMode = converted.wrapMode;

            Graphics.ConvertTexture(converted, convertedNewFormat);
            AssetDatabase.CreateAsset(convertedNewFormat, assetRealtivePath);
            AssetDatabase.SaveAssetIfDirty(convertedNewFormat);
        }

        public void SaveRenderTexture3DAsTexture3D(RenderTexture renderTexture3D, string assetRealtivePath, GraphicsFormat newTextureFormat, bool generateMips = false, bool readable = false)
        {
            Texture3D converted = ConvertRenderTexture3DToTexture3D(renderTexture3D, generateMips, readable);
            Texture3D convertedNewFormat = new Texture3D(converted.width, converted.height, converted.depth, newTextureFormat, generateMips ? TextureCreationFlags.MipChain : TextureCreationFlags.None);
            convertedNewFormat.filterMode = converted.filterMode;
            convertedNewFormat.wrapMode = converted.wrapMode;

            Graphics.ConvertTexture(converted, convertedNewFormat);
            AssetDatabase.CreateAsset(convertedNewFormat, assetRealtivePath);
            AssetDatabase.SaveAssetIfDirty(convertedNewFormat);
        }
    }
}

#endif