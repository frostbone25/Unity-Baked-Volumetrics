#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;

namespace BakedVolumetrics
{
    public class RenderTextureConverter
    {
        public struct TextureObjectSettings
        {
            public TextureWrapMode wrapMode;
            public FilterMode filterMode;
            public int anisoLevel;
        }

        private ComputeShader computeShader;

        private RenderTextureFormat rtFormat;
        private TextureFormat assetFormat;

        public RenderTextureConverter(ComputeShader computeShader, RenderTextureFormat rtFormat, TextureFormat assetFormat)
        {
            this.computeShader = computeShader;
            this.rtFormat = rtFormat;
            this.assetFormat = assetFormat;
        }

        public RenderTextureConverter(RenderTextureFormat rtFormat, TextureFormat assetFormat)
        {
            this.rtFormat = rtFormat;
            this.assetFormat = assetFormat;
        }

        /// <summary>
        /// Captures a single slice of the volume we are capturing.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="layer"></param>
        /// <returns></returns>
        private RenderTexture GetRenderTextureSlice(RenderTexture source, int layer)
        {
            //create a SLICE of the render texture
            RenderTexture render = new RenderTexture(source.width, source.height, 0, rtFormat);

            //set our options for the render texture SLICE
            render.dimension = TextureDimension.Tex2D;
            render.enableRandomWrite = true;
            render.wrapMode = TextureWrapMode.Clamp;
            render.Create();

            //find the main function in the slicer shader and start displaying each slice
            int kernelIndex = computeShader.FindKernel("CSMain");
            computeShader.SetTexture(kernelIndex, "voxels", source);
            computeShader.SetInt("layer", layer);
            computeShader.SetTexture(kernelIndex, "Result", render);
            computeShader.Dispatch(kernelIndex, source.width, source.height, 1);

            return render;
        }

        /// <summary>
        /// Converts a 2D render texture to a Texture2D object.
        /// </summary>
        /// <param name="rt"></param>
        /// <returns></returns>
        private Texture2D ConvertFromRenderTexture2D(RenderTexture rt)
        {
            //create our texture2D object to store the slice
            Texture2D output = new Texture2D(rt.width, rt.height, assetFormat, false);

            //make sure the render texture slice is active so we can read from it
            RenderTexture.active = rt;

            //read the texture and store the data in the texture2D object
            output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            output.Apply();

            return output;
        }

        /// <summary>
        /// Converts a 2D render texture to a Texture2D object.
        /// </summary>
        /// <param name="rt"></param>
        /// <returns></returns>
        public static Texture2D ConvertFromRenderTexture2D(RenderTexture rt, TextureFormat assetFormat, bool mipChain = false)
        {
            //create our texture2D object to store the slice
            Texture2D output = new Texture2D(rt.width, rt.height, assetFormat, mipChain);

            //make sure the render texture slice is active so we can read from it
            RenderTexture.active = rt;

            //read the texture and store the data in the texture2D object
            output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            output.Apply();

            return output;
        }

        private Texture3D ConvertFromRenderTexture3D(RenderTexture rt)
        {
            RenderTexture[] layers = new RenderTexture[rt.volumeDepth]; //create an array that matches in length the "depth" of the volume
            Texture2D[] finalSlices = new Texture2D[rt.volumeDepth]; //create another array to store the texture2D versions of the layers array

            for (int i = 0; i < rt.volumeDepth; i++)
            {
                layers[i] = GetRenderTextureSlice(rt, i);
            }

            for (int i = 0; i < rt.volumeDepth; i++)
            {
                finalSlices[i] = ConvertFromRenderTexture2D(layers[i]);
            }

            Texture3D output = new Texture3D(rt.width, rt.height, rt.volumeDepth, assetFormat, false);
            Color[] outputColors = new Color[rt.width * rt.height * rt.volumeDepth];

            for (int z = 0; z < rt.volumeDepth; z++)
            {
                Texture2D slice = finalSlices[z];
                Color[] sliceColors = slice.GetPixels();

                int startIndex = z * rt.width * rt.height;
                Array.Copy(sliceColors, 0, outputColors, startIndex, rt.width * rt.height);
            }

            output.SetPixels(outputColors);
            output.Apply();

            return output;
        }

        /// <summary>
        /// Saves a 3D Render Texture to the disk
        /// </summary>
        /// <param name="rt"></param>
        /// <param name="directory">Realtive to the Assets/, make sure there is a / after. Like 'Textures/'</param>
        public void Save3D(RenderTexture rt, string assetRealtivePath, TextureObjectSettings settings)
        {
            Texture3D output = ConvertFromRenderTexture3D(rt);
            output.anisoLevel = settings.anisoLevel;
            output.wrapMode = settings.wrapMode;
            output.filterMode = settings.filterMode;

            //AssetDatabase.DeleteAsset(assetRealtivePath);
            AssetDatabase.CreateAsset(output, assetRealtivePath);
        }

        public static Texture3D Duplicate3DTexture(Texture3D source)
        {
            Texture3D duplicate = new Texture3D(source.width, source.height, source.depth, source.format, source.mipmapCount);
            duplicate.wrapMode = source.wrapMode;
            duplicate.anisoLevel = source.anisoLevel;
            duplicate.filterMode = source.filterMode;

            for (int i = 0; i < source.mipmapCount; i++)
            {
                duplicate.SetPixels(source.GetPixels(i), i);
            }

            return duplicate;
        }
    }
}

#endif