#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace BakedVolumetricsOffline
{
    public static class RenderTextureSize
    {
        public static int GetRenderTextureFormatChannelCount(RenderTextureFormat renderTextureFormat)
        {
            switch (renderTextureFormat)
            {
                case RenderTextureFormat.ARGB1555:
                    return 4;
                case RenderTextureFormat.ARGB2101010:
                    return 4;
                case RenderTextureFormat.ARGB32:
                    return 4;
                case RenderTextureFormat.ARGB4444:
                    return 4;
                case RenderTextureFormat.ARGB64:
                    return 4;
                case RenderTextureFormat.ARGBFloat:
                    return 4;
                case RenderTextureFormat.ARGBHalf:
                    return 4;
                case RenderTextureFormat.ARGBInt:
                    return 4;
                case RenderTextureFormat.BGR101010_XR:
                    return 3;
                case RenderTextureFormat.BGRA10101010_XR:
                    return 4;
                case RenderTextureFormat.BGRA32:
                    return 4;
                case RenderTextureFormat.R16:
                    return 1;
                case RenderTextureFormat.R8:
                    return 1;
                case RenderTextureFormat.RFloat:
                    return 1;
                case RenderTextureFormat.RG16:
                    return 2;
                case RenderTextureFormat.RG32:
                    return 2;
                case RenderTextureFormat.RGB111110Float:
                    return 3;
                case RenderTextureFormat.RGB565:
                    return 3;
                case RenderTextureFormat.RGBAUShort:
                    return 4;
                case RenderTextureFormat.RGFloat:
                    return 2;
                case RenderTextureFormat.RGHalf:
                    return 2;
                case RenderTextureFormat.RGInt:
                    return 2;
                case RenderTextureFormat.RHalf:
                    return 1;
                case RenderTextureFormat.RInt:
                    return 1;

                case RenderTextureFormat.Depth:
                    return 1;
                case RenderTextureFormat.Shadowmap:
                    return 1;

                case RenderTextureFormat.Default:
                    return 4;
                case RenderTextureFormat.DefaultHDR:
                    return 4;

                default:
                    return 1;
            }
        }

        /// <summary>
        /// Get the byte size of a render texture.
        /// <para>This is done with manual calculations.</para>
        /// <para>NOTE 1: THIS DOES NOT FACTOR IN MIP MAPS</para>
        /// <para>NOTE 2: Default/DefaultHDR are potentially wrong because... who knows what it could be?</para>
        /// <para>NOTE 3: TextureDimension.Tex3D and TextureDimension.Tex2D are correct, however anything else could be wrong?</para>
        /// </summary>
        /// <param name="renderTexture"></param>
        /// <returns></returns>
        public static ulong GetRenderTextureMemorySize(RenderTexture renderTexture)
        {
            ulong renderTextureChannelCount = (ulong)GetRenderTextureFormatChannelCount(renderTexture.format);

            //this is equivalent to 1 byte (8 bits) of pixel data for 1 channel
            ulong renderTextureDataSize = 0;

            switch(renderTexture.dimension)
            {
                case UnityEngine.Rendering.TextureDimension.Tex3D:
                    renderTextureDataSize = (ulong)(renderTexture.width * renderTexture.height * renderTexture.volumeDepth);
                    break;

                default:
                    renderTextureDataSize = (ulong)(renderTexture.width * renderTexture.height);
                    break;
            }

            switch (renderTexture.format)
            {
                //Color render texture format, 1 bit for Alpha channel, 5 bits for Red, Green and Blue channels.
                //[TOTAL BYTES PER PIXEL]: 16 bits (2 bytes)
                case RenderTextureFormat.ARGB1555:
                    return renderTextureDataSize * 2;

                //Color render texture format. 10 bits for colors, 2 bits for alpha.
                //[TOTAL BYTES PER PIXEL]: 32 bits (4 bytes)
                case RenderTextureFormat.ARGB2101010:
                    return renderTextureDataSize * 4;

                //Color render texture format, 8 bits per channel.
                //[TOTAL BYTES PER PIXEL]: 32 bits (4 bytes)
                case RenderTextureFormat.ARGB32:
                    return renderTextureDataSize * 4;

                //Color render texture format, 4 bit per channel.
                //[TOTAL BYTES PER PIXEL]: 16 bits (2 bytes)
                case RenderTextureFormat.ARGB4444:
                    return renderTextureDataSize * 2;

                //Four color render texture format, 16 bits per channel, fixed point, unsigned normalized.
                //[TOTAL BYTES PER PIXEL]: 64 bits (8 bytes)
                case RenderTextureFormat.ARGB64:
                    return renderTextureDataSize * 8;

                //Color render texture format, 32 bit floating point per channel.
                //[TOTAL BYTES PER PIXEL]: 128 bits (16 bytes)
                case RenderTextureFormat.ARGBFloat:
                    return renderTextureDataSize * 16;

                //Color render texture format, 16 bit floating point per channel.
                //[TOTAL BYTES PER PIXEL]: 64 bits (8 bytes)
                case RenderTextureFormat.ARGBHalf:
                    return renderTextureDataSize * 8;

                //Four channel (ARGB) render texture format, 32 bit signed integer per channel.
                //[TOTAL BYTES PER PIXEL]: 128 bits (16 bytes)
                case RenderTextureFormat.ARGBInt:
                    return renderTextureDataSize * 16;

                //Color render texture format, 8 bits per channel.
                //[TOTAL BYTES PER PIXEL]: 32 bits (4 bytes)
                case RenderTextureFormat.BGRA32:
                    return renderTextureDataSize * 4;

                //Single channel (R) render texture format, 16 bit integer.
                //[TOTAL BYTES PER PIXEL]: 16 bits (2 bytes)
                case RenderTextureFormat.R16:
                    return renderTextureDataSize * 2;

                //Single channel (R) render texture format, 8 bit integer.
                //[TOTAL BYTES PER PIXEL]: 8 bits (1 byte)
                case RenderTextureFormat.R8:
                    return renderTextureDataSize;

                //Scalar (R) render texture format, 32 bit floating point.
                //[TOTAL BYTES PER PIXEL]: 32 bits (4 bytes)
                case RenderTextureFormat.RFloat:
                    return renderTextureDataSize * 4;

                //Two channel (RG) render texture format, 8 bits per channel.
                //[TOTAL BYTES PER PIXEL]: 16 bits (2 bytes)
                case RenderTextureFormat.RG16:
                    return renderTextureDataSize * 2;

                //Two color (RG) render texture format, 16 bits per channel, fixed point, unsigned normalized.
                //[TOTAL BYTES PER PIXEL]: 32 bits (4 bytes)
                case RenderTextureFormat.RG32:
                    return renderTextureDataSize * 4;

                //Color render texture format. R and G channels are 11 bit floating point, B channel is 10 bit floating point.
                //[TOTAL BYTES PER PIXEL]: 32 bits (4 bytes)
                case RenderTextureFormat.RGB111110Float:
                    return renderTextureDataSize * 4;

                //Color render texture format.
                //[TOTAL BYTES PER PIXEL]: 16 bits (2 bytes)
                case RenderTextureFormat.RGB565:
                    return renderTextureDataSize * 2;

                //Four channel (RGBA) render texture format, 16 bit unsigned integer per channel.
                //[TOTAL BYTES PER PIXEL]: 64 bits (8 bytes)
                case RenderTextureFormat.RGBAUShort:
                    return renderTextureDataSize * 8;

                //Two color (RG) render texture format, 32 bit floating point per channel.
                //[TOTAL BYTES PER PIXEL]: 64 bits (8 bytes)
                case RenderTextureFormat.RGFloat:
                    return renderTextureDataSize * 8;

                //Two color (RG) render texture format, 16 bit floating point per channel.
                //[TOTAL BYTES PER PIXEL]: 32 bits (4 bytes)
                case RenderTextureFormat.RGHalf:
                    return renderTextureDataSize * 4;

                //Two channel (RG) render texture format, 32 bit signed integer per channel.
                //[TOTAL BYTES PER PIXEL]: 64 bits (8 bytes)
                case RenderTextureFormat.RGInt:
                    return renderTextureDataSize * 8;

                //Scalar (R) render texture format, 16 bit floating point.
                //[TOTAL BYTES PER PIXEL]: 16 bits (2 bytes)
                case RenderTextureFormat.RHalf:
                    return renderTextureDataSize * 2;

                //Scalar (R) render texture format, 32 bit signed integer.
                //[TOTAL BYTES PER PIXEL]: 32 bits (4 bytes)
                case RenderTextureFormat.RInt:
                    return renderTextureDataSize * 4;

                //|||||||||||||||||||||||| UNSURE ABOUT THESE BELOW ||||||||||||||||||||||||
                //|||||||||||||||||||||||| UNSURE ABOUT THESE BELOW ||||||||||||||||||||||||
                //|||||||||||||||||||||||| UNSURE ABOUT THESE BELOW ||||||||||||||||||||||||

                //Color render texture format, 10 bit per channel, extended range.
                //[TOTAL BYTES PER PIXEL]: 30 bits
                case RenderTextureFormat.BGR101010_XR:
                    return (ulong)((renderTextureDataSize * 4.0f) * (30.0f / 32.0f));

                //Color render texture format, 10 bit per channel, extended range.
                //[TOTAL BYTES PER PIXEL]: 40 bits
                case RenderTextureFormat.BGRA10101010_XR:
                    return (ulong)((renderTextureDataSize * 4.0f) * (40.0f / 32.0f));

                //[TOTAL BYTES PER PIXEL]: 32 bits? (4 bytes?) honestly unknown because default is dependent on the frame buffer and platform
                case RenderTextureFormat.Default:
                    return renderTextureDataSize * 4;

                //[TOTAL BYTES PER PIXEL]: 32 bits? (4 bytes?) honestly unknown because default is dependent on the frame buffer and platform
                case RenderTextureFormat.DefaultHDR:
                    return renderTextureDataSize * 4;

                //[TOTAL BYTES PER PIXEL]: 32 bits? (4 bytes?) not to sure on this one
                case RenderTextureFormat.Depth:
                    return renderTextureDataSize * 4;

                //[TOTAL BYTES PER PIXEL]: 32 bits? (4 bytes?) not to sure on this one
                case RenderTextureFormat.Shadowmap:
                    return renderTextureDataSize * 4;

                default:
                    return renderTextureDataSize;
            }
        }
    }
}
#endif