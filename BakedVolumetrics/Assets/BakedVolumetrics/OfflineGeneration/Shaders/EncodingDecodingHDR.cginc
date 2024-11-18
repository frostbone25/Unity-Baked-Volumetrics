//|||||||||||||||||||||||||||||||| RGBM ||||||||||||||||||||||||||||||||
//|||||||||||||||||||||||||||||||| RGBM ||||||||||||||||||||||||||||||||
//|||||||||||||||||||||||||||||||| RGBM ||||||||||||||||||||||||||||||||
//SOURCE - https://graphicrants.blogspot.com/2009/04/rgbm-color-encoding.html

float _RGBMRange;

float4 EncodeRGBM(float3 rgb)
{
    float maxRGB = max(rgb.x, max(rgb.g, rgb.b));
    float M = maxRGB / _RGBMRange;
    M = ceil(M * 255.0) / 255.0;
    return float4(rgb / (M * _RGBMRange), M);
}

float3 DecodeRGBM(float4 rgbm)
{
    return rgbm.rgb * (rgbm.a * _RGBMRange);
}

//|||||||||||||||||||||||||||||||| RGBD ||||||||||||||||||||||||||||||||
//|||||||||||||||||||||||||||||||| RGBD ||||||||||||||||||||||||||||||||
//|||||||||||||||||||||||||||||||| RGBD ||||||||||||||||||||||||||||||||
//SOURCE - https://iwasbeingirony.blogspot.com/2010/06/difference-between-rgbm-and-rgbd.html

float _RGBDRange;

float4 EncodeRGBD(float3 rgb)
{
    float maxRGB = max(rgb.x, max(rgb.g, rgb.b));
    float D = max(_RGBDRange / maxRGB, 1);
    D = saturate(floor(D) / 255.0);
    return float4(rgb.rgb * (D * (255.0 / _RGBDRange)), D);
}

float3 DecodeRGBD(float4 rgbd)
{
    return rgbd.rgb * ((_RGBDRange / 255.0) / rgbd.a);
}

//|||||||||||||||||||||||||||||||| RGBE ||||||||||||||||||||||||||||||||
//|||||||||||||||||||||||||||||||| RGBE ||||||||||||||||||||||||||||||||
//|||||||||||||||||||||||||||||||| RGBE ||||||||||||||||||||||||||||||||
//SOURCE - https://www.malteclasen.de/zib/index4837.html?p=37

float4 EncodeRGBE(float3 value)
{
    value = value / 65536.0;
    float3 exponent = clamp(ceil(log2(value)), -128.0, 127.0);
    float commonExponent = max(max(exponent.r, exponent.g), exponent.b);
    float range = exp2(commonExponent);
    float3 mantissa = clamp(value / range, 0.0, 1.0);
    return float4(mantissa, (commonExponent + 128.0) / 256.0);
}

float3 DecodeRGBE(float4 encoded)
{
    float exponent = encoded.a * 256.0 - 128.0;
    float3 mantissa = encoded.rgb;
    return exp2(exponent) * mantissa * 65536.0;
}

// SOURCE - https://github.com/microsoft/DirectXShaderCompiler/blob/main/tools/clang/test/CodeGenHLSL/Samples/MiniEngine/PixelPacking.hlsli
// RGBE packs 9 bits per color channel while encoding the multiplier as a perfect power of 2 (just the exponent)
// What's nice about this is that it gives you a lot more range than RGBM.  This isn't proven to be bitwise
// compatible with DXGI_FORMAT_R9B9G9E5_SHAREDEXP, but if it's not, it could be made so.
uint EncodeRGBE_ToUINT32(float3 rgb) //32 BIT FINAL
{
    float MaxChannel = max(rgb.r, max(rgb.g, rgb.b));

	// NextPow2 has to have the biggest exponent plus 1 (and nothing in the mantissa)
    float NextPow2 = asfloat((asuint(MaxChannel) + 0x800000) & 0x7F800000);

	// By adding NextPow2, all channels have the same exponent, shifting their mantissa bits
	// to the right to accomodate it.  This also shifts in the implicit '1' bit of all channels.
	// The largest channel will always have the high bit set.
    rgb += NextPow2;

    uint R = (asuint(rgb.r) << 9) >> 23;
    uint G = (asuint(rgb.g) << 9) >> 23;
    uint B = (asuint(rgb.b) << 9) >> 23;
    uint E = f32tof16(NextPow2) << 17;
    return R | G << 9 | B << 18 | E;
}

float3 UnpackRGBE_FromUINT32(uint p)
{
    float Pow2 = f16tof32((p >> 27) << 10);
    float R = asfloat(asuint(Pow2) | (p << 14) & 0x7FC000);
    float G = asfloat(asuint(Pow2) | (p << 5) & 0x7FC000);
    float B = asfloat(asuint(Pow2) | (p >> 4) & 0x7FC000);
    return float3(R, G, B) - Pow2;
}

// SOURCE - https://github.com/DigitalRune/DigitalRune/blob/master/Source/DigitalRune.Graphics.Content/DigitalRune/Encoding.fxh
/// Encodes the given color to RGBE 8-bit format.
/// \param[in] color    The original color.
/// \return The color encoded as RGBE.
float4 EncodeRGBE_2(float3 color)
{
  // Get the largest component.
    float maxValue = max(max(color.r, color.g), color.b);
  
    float exponent = ceil(log2(maxValue));
  
    float4 result;
  
  // Store the exponent in the alpha channel.
    result.a = (exponent + 128) / 255;
  
  // Convert the color channels.
    result.rgb = color / exp2(exponent);
  
    return result;
}


/// Decodes the given color from RGBE 8-bit format.
/// \param[in] rgbe   The color encoded as RGBE.
/// \return The orginal color.
float3 DecodeRGBE_2(float4 rgbe)
{
  // Get exponent from alpha channel.
    float exponent = rgbe.a * 255 - 128;
  
    return rgbe.rgb * exp2(exponent);
}

//|||||||||||||||||||||||||||||||| LOG LUV 32 BIT ||||||||||||||||||||||||||||||||
//|||||||||||||||||||||||||||||||| LOG LUV 32 BIT ||||||||||||||||||||||||||||||||
//|||||||||||||||||||||||||||||||| LOG LUV 32 BIT ||||||||||||||||||||||||||||||||
//SOURCE 1 - https://graphicrants.blogspot.com/2009/04/rgbm-color-encoding.html
//SOURCE 2 - https://realtimecollisiondetection.net/blog/?p=15

// M matrix, for encoding
const static float3x3 M = float3x3(
    0.2209, 0.3390, 0.4184,
    0.1138, 0.6780, 0.7319,
    0.0102, 0.1130, 0.2969);

// Inverse M matrix, for decoding
const static float3x3 InverseM = float3x3(
    6.0014, -2.7008, -1.7996,
   -1.3320, 3.1029, -5.7721,
    0.3008, -1.0882, 5.6268);

float4 EncodeLogLuv32(float3 vRGB)
{
    float4 vResult;
    float3 Xp_Y_XYZp = mul(vRGB, M);
    Xp_Y_XYZp = max(Xp_Y_XYZp, float3(1e-6, 1e-6, 1e-6));
    vResult.xy = Xp_Y_XYZp.xy / Xp_Y_XYZp.z;
    float Le = 2 * log2(Xp_Y_XYZp.y) + 127;
    vResult.w = frac(Le);
    vResult.z = (Le - (floor(vResult.w * 255.0f)) / 255.0f) / 255.0f;
    return vResult;
}

float3 DecodeLogLuv32(float4 vLogLuv)
{
    float Le = vLogLuv.z * 255 + vLogLuv.w;
    float3 Xp_Y_XYZp;
    Xp_Y_XYZp.y = exp2((Le - 127) / 2);
    Xp_Y_XYZp.z = Xp_Y_XYZp.y / vLogLuv.y;
    Xp_Y_XYZp.x = vLogLuv.x * Xp_Y_XYZp.z;
    float3 vRGB = mul(Xp_Y_XYZp, InverseM);
    return max(vRGB, 0);
}