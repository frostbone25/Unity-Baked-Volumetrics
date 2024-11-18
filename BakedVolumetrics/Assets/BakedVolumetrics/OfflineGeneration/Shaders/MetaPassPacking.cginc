#include "BitPackingHelper.cginc"
#include "EncodingDecodingHDR.cginc"
#include "EncodingDecodingNormal.cginc"

//Pack 3 Buffers into a 64 Bit UNorm format.
//Albedo (RGBA5551)
//Emissive (RGB565 + Half16)
//Normal (RGB565)
float4 PackMetaBuffer(float4 albedoColor, float3 emissiveColor, float3 normalColor)
{
    albedoColor = saturate(albedoColor);
    normalColor = saturate(normalColor);

    //||||||||||||||||| ALBEDO BUFFER 16 BITS (RGBA5551) |||||||||||||||||
    //||||||||||||||||| ALBEDO BUFFER 16 BITS (RGBA5551) |||||||||||||||||
    //||||||||||||||||| ALBEDO BUFFER 16 BITS (RGBA5551) |||||||||||||||||
    uint ALBEDO_R5 = albedoColor.r * BITS_5_MAX_VALUE;
    uint ALBEDO_G5 = albedoColor.g * BITS_5_MAX_VALUE;
    uint ALBEDO_B5 = albedoColor.b * BITS_5_MAX_VALUE;
    uint ALBEDO_A1 = any(albedoColor.a) ? 1 : 0;

    ALBEDO_R5 = KeepBitsOfValue(ALBEDO_R5, 0, 5);
    ALBEDO_G5 = KeepBitsOfValue(ALBEDO_G5, 0, 5);
    ALBEDO_B5 = KeepBitsOfValue(ALBEDO_B5, 0, 5);
    ALBEDO_A1 = KeepBitsOfValue(ALBEDO_A1, 0, 1);

    uint combinedAlbedo_16bit_R5G5B5A1 = ALBEDO_R5 | ALBEDO_G5 << BITS_5 | ALBEDO_B5 << BITS_10 | ALBEDO_A1 << BITS_15;
    float packedAlbedo_16bit_R5G5B5A1 = combinedAlbedo_16bit_R5G5B5A1 / float(BITS_16_MAX_VALUE);

    //||||||||||||||||| NORMAL BUFFER 16 BITS (RGB565) |||||||||||||||||
    //||||||||||||||||| NORMAL BUFFER 16 BITS (RGB565) |||||||||||||||||
    //||||||||||||||||| NORMAL BUFFER 16 BITS (RGB565) |||||||||||||||||

    uint NORMAL_R5 = normalColor.r * BITS_5_MAX_VALUE;
    uint NORMAL_G6 = normalColor.g * BITS_6_MAX_VALUE;
    uint NORMAL_B5 = normalColor.b * BITS_5_MAX_VALUE;

    uint combinedNormal_16bit_R5G6B5 = NORMAL_R5 | NORMAL_G6 << BITS_5 | NORMAL_B5 << BITS_11;
    float packedNormal_16bit_R5G5B5 = combinedNormal_16bit_R5G6B5 / float(BITS_16_MAX_VALUE);
    
    //float2 encodedNormal = EncodeNormal(normalColor * 2.0f - 1.0f);
    //float2 encodedNormal = EncodeNormal(normalColor);
    //uint NORMAL_R8 = normalColor.r * BITS_8_MAX;
    //uint NORMAL_G8 = normalColor.g * BITS_8_MAX;
    //uint NORMAL_R8 = normalColor.r * BITS_8_MAX_VALUE;
    //uint NORMAL_G8 = normalColor.g * BITS_8_MAX_VALUE;
    //uint combinedNormal_16bit_R5G6B5 = NORMAL_R8 | NORMAL_G8 << BITS_8;
    //float packedNormal_16bit_R5G5B5 = combinedNormal_16bit_R5G6B5 / float(BITS_16_MAX_VALUE);

    //||||||||||||||||| EMISSIVE BUFFER 32 BITS (RGB565 + Half16) |||||||||||||||||
    //||||||||||||||||| EMISSIVE BUFFER 32 BITS (RGB565 + Half16) |||||||||||||||||
    //||||||||||||||||| EMISSIVE BUFFER 32 BITS (RGB565 + Half16) |||||||||||||||||

#if defined (EMISSION_HDR_RGBM)
    float4 encodedEmissionColor = EncodeRGBM(emissiveColor.rgb);

    uint EMISSION_R8 = encodedEmissionColor.r * BITS_8_MAX_VALUE;
    uint EMISSION_G8 = encodedEmissionColor.g * BITS_8_MAX_VALUE;
    uint EMISSION_B8 = encodedEmissionColor.b * BITS_8_MAX_VALUE;
    uint EMISSION_A8 = encodedEmissionColor.a * BITS_8_MAX_VALUE;
    
    EMISSION_R8 = KeepBitsOfValue(EMISSION_R8, 0, 8);
    EMISSION_G8 = KeepBitsOfValue(EMISSION_G8, 0, 8);
    EMISSION_B8 = KeepBitsOfValue(EMISSION_B8, 0, 8);
    EMISSION_A8 = KeepBitsOfValue(EMISSION_A8, 0, 8);

    uint combinedEmissionPartA = EMISSION_R8 | EMISSION_G8 << BITS_8;
    uint combinedEmissionPartB = EMISSION_B8 | EMISSION_A8 << BITS_8;

    float packedEmissionPartA = combinedEmissionPartA / float(BITS_16_MAX_VALUE);
    float packedEmissionPartB = combinedEmissionPartB / float(BITS_16_MAX_VALUE);
#elif defined (EMISSION_HDR_RGBD)
    float4 encodedEmissionColor = EncodeRGBD(emissiveColor.rgb);

    uint EMISSION_R8 = encodedEmissionColor.r * BITS_8_MAX_VALUE;
    uint EMISSION_G8 = encodedEmissionColor.g * BITS_8_MAX_VALUE;
    uint EMISSION_B8 = encodedEmissionColor.b * BITS_8_MAX_VALUE;
    uint EMISSION_A8 = encodedEmissionColor.a * BITS_8_MAX_VALUE;
    
    EMISSION_R8 = KeepBitsOfValue(EMISSION_R8, 0, 8);
    EMISSION_G8 = KeepBitsOfValue(EMISSION_G8, 0, 8);
    EMISSION_B8 = KeepBitsOfValue(EMISSION_B8, 0, 8);
    EMISSION_A8 = KeepBitsOfValue(EMISSION_A8, 0, 8);

    uint combinedEmissionPartA = EMISSION_R8 | EMISSION_G8 << BITS_8;
    uint combinedEmissionPartB = EMISSION_B8 | EMISSION_A8 << BITS_8;

    float packedEmissionPartA = combinedEmissionPartA / float(BITS_16_MAX_VALUE);
    float packedEmissionPartB = combinedEmissionPartB / float(BITS_16_MAX_VALUE);
#elif defined (EMISSION_HDR_RGBE)
    float4 encodedEmissionColor = EncodeRGBE(emissiveColor.rgb);

    uint EMISSION_R8 = encodedEmissionColor.r * BITS_8_MAX_VALUE;
    uint EMISSION_G8 = encodedEmissionColor.g * BITS_8_MAX_VALUE;
    uint EMISSION_B8 = encodedEmissionColor.b * BITS_8_MAX_VALUE;
    uint EMISSION_A8 = encodedEmissionColor.a * BITS_8_MAX_VALUE;
    
    EMISSION_R8 = KeepBitsOfValue(EMISSION_R8, 0, 8);
    EMISSION_G8 = KeepBitsOfValue(EMISSION_G8, 0, 8);
    EMISSION_B8 = KeepBitsOfValue(EMISSION_B8, 0, 8);
    EMISSION_A8 = KeepBitsOfValue(EMISSION_A8, 0, 8);

    uint combinedEmissionPartA = EMISSION_R8 | EMISSION_G8 << BITS_8;
    uint combinedEmissionPartB = EMISSION_B8 | EMISSION_A8 << BITS_8;

    float packedEmissionPartA = combinedEmissionPartA / float(BITS_16_MAX_VALUE);
    float packedEmissionPartB = combinedEmissionPartB / float(BITS_16_MAX_VALUE);
#elif defined (EMISSION_HDR_LOG_LUV_32)
    float4 encodedEmissionColor = EncodeLogLuv32(emissiveColor.rgb);

    uint EMISSION_R8 = encodedEmissionColor.r * BITS_8_MAX_VALUE;
    uint EMISSION_G8 = encodedEmissionColor.g * BITS_8_MAX_VALUE;
    uint EMISSION_B8 = encodedEmissionColor.b * BITS_8_MAX_VALUE;
    uint EMISSION_A8 = encodedEmissionColor.a * BITS_8_MAX_VALUE;
    
    EMISSION_R8 = KeepBitsOfValue(EMISSION_R8, 0, 8);
    EMISSION_G8 = KeepBitsOfValue(EMISSION_G8, 0, 8);
    EMISSION_B8 = KeepBitsOfValue(EMISSION_B8, 0, 8);
    EMISSION_A8 = KeepBitsOfValue(EMISSION_A8, 0, 8);

    uint combinedEmissionPartA = EMISSION_R8 | EMISSION_G8 << BITS_8;
    uint combinedEmissionPartB = EMISSION_B8 | EMISSION_A8 << BITS_8;

    float packedEmissionPartA = combinedEmissionPartA / float(BITS_16_MAX_VALUE);
    float packedEmissionPartB = combinedEmissionPartB / float(BITS_16_MAX_VALUE);
#else
    float packedEmissionPartA = 0;
    float packedEmissionPartB = 0;
#endif

    //||||||||||||||||| FINAL PACKED BUFFER 64 BITS |||||||||||||||||
    //||||||||||||||||| FINAL PACKED BUFFER 64 BITS |||||||||||||||||
    //||||||||||||||||| FINAL PACKED BUFFER 64 BITS |||||||||||||||||

    return float4(packedAlbedo_16bit_R5G5B5A1, packedEmissionPartA, packedNormal_16bit_R5G5B5, packedEmissionPartB);
}

void UnpackMetaBuffer(float4 packedBuffer64, out float4 albedoColor, out float4 emissiveColor, out float4 normalColor)
{
    //||||||||||||||||| ALBEDO BUFFER 16 BITS (RGBA5551) |||||||||||||||||
    //||||||||||||||||| ALBEDO BUFFER 16 BITS (RGBA5551) |||||||||||||||||
    //||||||||||||||||| ALBEDO BUFFER 16 BITS (RGBA5551) |||||||||||||||||
    uint ALBEDO_R5G5B5A1 = packedBuffer64.r * BITS_16_MAX;

    uint ALBEDO_R5 = ExtractBits(ALBEDO_R5G5B5A1, 0u, 5u) << BITS_11;
    uint ALBEDO_G5 = ExtractBits(ALBEDO_R5G5B5A1, 5u, 5u) << BITS_11;
    uint ALBEDO_B5 = ExtractBits(ALBEDO_R5G5B5A1, 10u, 5u) << BITS_11;
    uint ALBEDO_A1 = ExtractBits(ALBEDO_R5G5B5A1, 15u, 1u);

    float4 unpackedAlbedo = float4(0, 0, 0, 0);
    unpackedAlbedo.rgb = float3(ALBEDO_R5, ALBEDO_G5, ALBEDO_B5) / float(BITS_16_MAX_VALUE);
    unpackedAlbedo.a = any(ALBEDO_A1) ? 1 : 0;
    
    //||||||||||||||||| NORMAL BUFFER 16 BITS (RGB565) |||||||||||||||||
    //||||||||||||||||| NORMAL BUFFER 16 BITS (RGB565) |||||||||||||||||
    //||||||||||||||||| NORMAL BUFFER 16 BITS (RGB565) |||||||||||||||||
    uint NORMAL_R5G6B5 = packedBuffer64.b * BITS_16_MAX;

    uint NORMAL_R5 = ExtractBits(NORMAL_R5G6B5, 0u, 5u) << BITS_11;
    uint NORMAL_G6 = ExtractBits(NORMAL_R5G6B5, 5u, 6u) << BITS_10;
    uint NORMAL_B5 = ExtractBits(NORMAL_R5G6B5, 11u, 5u) << BITS_11;

    float3 unpackedNormal = float3(NORMAL_R5, NORMAL_G6, NORMAL_B5) / float(BITS_16_MAX_VALUE);
    
    //uint NORMAL_R8 = ExtractBits(NORMAL_R5G6B5, 0u, 8u) << BITS_8;
    //uint NORMAL_G8 = ExtractBits(NORMAL_R5G6B5, 8u, 8u) << BITS_8;
    //float2 encodedNormal = float2(NORMAL_R8, NORMAL_G8) / float(BITS_16_MAX_VALUE);
    //float3 unpackedNormal = DecodeNormal(encodedNormal);
    //unpackedNormal = unpackedNormal * 0.5f + 0.5f;
    
    //||||||||||||||||| EMISSIVE BUFFER 32 BITS (RGB565 + Half16) |||||||||||||||||
    //||||||||||||||||| EMISSIVE BUFFER 32 BITS (RGB565 + Half16) |||||||||||||||||
    //||||||||||||||||| EMISSIVE BUFFER 32 BITS (RGB565 + Half16) |||||||||||||||||

#if defined (EMISSION_HDR_RGBM)
    uint EMISSIVE_R8G8 = packedBuffer64.g * BITS_16_MAX;
    uint EMISSIVE_B8A8 = packedBuffer64.a * BITS_16_MAX;

    uint EMISSIVE_R8 = ExtractBits(EMISSIVE_R8G8, 0u, 8u) << BITS_8;
    uint EMISSIVE_G8 = ExtractBits(EMISSIVE_R8G8, 8u, 8u) << BITS_8;
    uint EMISSIVE_B8 = ExtractBits(EMISSIVE_B8A8, 0u, 8u) << BITS_8;
    uint EMISSIVE_A8 = ExtractBits(EMISSIVE_B8A8, 8u, 8u) << BITS_8;

    float4 unpackedEncodedEmissiveColor = float4(EMISSIVE_R8, EMISSIVE_G8, EMISSIVE_B8, EMISSIVE_A8) / float(BITS_16_MAX_VALUE);
    float3 unpackedEmissive = DecodeRGBM(unpackedEncodedEmissiveColor) * 2; // * (BITS_16_MAX / BITS_16_MAX + 1)
#elif defined (EMISSION_HDR_RGBD)
    uint EMISSIVE_R8G8 = packedBuffer64.g * BITS_16_MAX;
    uint EMISSIVE_B8A8 = packedBuffer64.a * BITS_16_MAX;

    uint EMISSIVE_R8 = ExtractBits(EMISSIVE_R8G8, 0u, 8u) << BITS_8;
    uint EMISSIVE_G8 = ExtractBits(EMISSIVE_R8G8, 8u, 8u) << BITS_8;
    uint EMISSIVE_B8 = ExtractBits(EMISSIVE_B8A8, 0u, 8u) << BITS_8;
    uint EMISSIVE_A8 = ExtractBits(EMISSIVE_B8A8, 8u, 8u) << BITS_8;

    float4 unpackedEncodedEmissiveColor = float4(EMISSIVE_R8, EMISSIVE_G8, EMISSIVE_B8, EMISSIVE_A8) / float(BITS_16_MAX_VALUE);
    float3 unpackedEmissive = DecodeRGBD(unpackedEncodedEmissiveColor);
#elif defined (EMISSION_HDR_RGBE)
    uint EMISSIVE_R8G8 = packedBuffer64.g * BITS_16_MAX;
    uint EMISSIVE_B8A8 = packedBuffer64.a * BITS_16_MAX;

    uint EMISSIVE_R8 = ExtractBits(EMISSIVE_R8G8, 0u, 8u) << BITS_8;
    uint EMISSIVE_G8 = ExtractBits(EMISSIVE_R8G8, 8u, 8u) << BITS_8;
    uint EMISSIVE_B8 = ExtractBits(EMISSIVE_B8A8, 0u, 8u) << BITS_8;
    uint EMISSIVE_A8 = ExtractBits(EMISSIVE_B8A8, 8u, 8u) << BITS_8;

    float4 unpackedEncodedEmissiveColor = float4(EMISSIVE_R8, EMISSIVE_G8, EMISSIVE_B8, EMISSIVE_A8) / float(BITS_16_MAX_VALUE);
    float3 unpackedEmissive = DecodeRGBE(unpackedEncodedEmissiveColor);
#elif defined (EMISSION_HDR_LOG_LUV_32)
    uint EMISSIVE_R8G8 = packedBuffer64.g * BITS_16_MAX;
    uint EMISSIVE_B8A8 = packedBuffer64.a * BITS_16_MAX;

    uint EMISSIVE_R8 = ExtractBits(EMISSIVE_R8G8, 0u, 8u) << BITS_8;
    uint EMISSIVE_G8 = ExtractBits(EMISSIVE_R8G8, 8u, 8u) << BITS_8;
    uint EMISSIVE_B8 = ExtractBits(EMISSIVE_B8A8, 0u, 8u) << BITS_8;
    uint EMISSIVE_A8 = ExtractBits(EMISSIVE_B8A8, 8u, 8u) << BITS_8;

    float4 unpackedEncodedEmissiveColor = float4(EMISSIVE_R8, EMISSIVE_G8, EMISSIVE_B8, EMISSIVE_A8) / float(BITS_16_MAX_VALUE);
    float3 unpackedEmissive = DecodeLogLuv32(unpackedEncodedEmissiveColor);
#else
    float3 unpackedEmissive = float3(0, 0, 0);
#endif

    //||||||||||||||||| FINAL PACKED BUFFER 64 BITS (ARGB64) |||||||||||||||||
    //||||||||||||||||| FINAL PACKED BUFFER 64 BITS (ARGB64) |||||||||||||||||
    //||||||||||||||||| FINAL PACKED BUFFER 64 BITS (ARGB64) |||||||||||||||||

    albedoColor = unpackedAlbedo;
    normalColor = float4(unpackedNormal, unpackedAlbedo.a);
    emissiveColor = float4(unpackedEmissive, unpackedAlbedo.a);
}