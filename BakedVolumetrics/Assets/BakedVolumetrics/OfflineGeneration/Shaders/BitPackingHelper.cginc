#define BITS_32 32
#define BITS_24 24
#define BITS_20 20
#define BITS_18 18
#define BITS_16 16
#define BITS_15 15
#define BITS_12 12
#define BITS_11 11
#define BITS_10 10
#define BITS_9 9
#define BITS_8 8
#define BITS_7 7
#define BITS_6 6
#define BITS_5 5
#define BITS_4 4
#define BITS_3 3
#define BITS_2 2
#define BITS_1 1

#define BITS_32_MAX 2147483647
#define BITS_24_MAX 16777216
#define BITS_20_MAX 1048576
#define BITS_18_MAX 262144
#define BITS_16_MAX 65536
#define BITS_15_MAX 32767
#define BITS_12_MAX 4096
#define BITS_10_MAX 1024
#define BITS_9_MAX 512
#define BITS_8_MAX 256
#define BITS_7_MAX 128
#define BITS_6_MAX 64
#define BITS_5_MAX 32
#define BITS_4_MAX 16
#define BITS_3_MAX 8
#define BITS_2_MAX 4
#define BITS_1_MAX 2

#define BITS_32_MAX_VALUE BITS_32_MAX - 1
#define BITS_24_MAX_VALUE BITS_24_MAX - 1
#define BITS_20_MAX_VALUE BITS_20_MAX - 1
#define BITS_18_MAX_VALUE BITS_18_MAX - 1
#define BITS_16_MAX_VALUE BITS_16_MAX - 1
#define BITS_15_MAX_VALUE BITS_15_MAX - 1
#define BITS_12_MAX_VALUE BITS_12_MAX - 1
#define BITS_10_MAX_VALUE BITS_10_MAX - 1
#define BITS_9_MAX_VALUE BITS_9_MAX - 1
#define BITS_8_MAX_VALUE BITS_8_MAX - 1
#define BITS_7_MAX_VALUE BITS_7_MAX - 1
#define BITS_6_MAX_VALUE BITS_6_MAX - 1
#define BITS_5_MAX_VALUE BITS_5_MAX - 1
#define BITS_4_MAX_VALUE BITS_4_MAX - 1
#define BITS_3_MAX_VALUE BITS_3_MAX - 1
#define BITS_2_MAX_VALUE BITS_2_MAX - 1
#define BITS_1_MAX_VALUE BITS_1_MAX - 1

uint GetMaskOfBitsToKeep(uint bitOffsetStart, uint bitsToKeep)
{
    uint mask = (1u << bitsToKeep) - 1;
    return mask << bitOffsetStart;
}

uint KeepBitsOfValue(uint sourceData, uint bitsToKeepOffsetStart, uint amountOfBitsToKeep)
{
    return sourceData & GetMaskOfBitsToKeep(bitsToKeepOffsetStart, amountOfBitsToKeep);
}

uint ExtractBits(uint sourceData, uint bitOffsetStart, uint bitsToExtract)
{
    uint bitmask = (1u << bitsToExtract) - 1u;
    return (sourceData >> bitOffsetStart) & bitmask;
}

bool IsBitAtOffsetSet(uint sourceData, uint bitOffsetLocation)
{
    return ExtractBits(sourceData, bitOffsetLocation, 1u) != 0;
}

uint SetBitAtOffset(uint sourceData, uint bitOffsetLocation)
{
    return sourceData |= 1u << bitOffsetLocation;
}

uint ClearBitAtOffset(uint sourceData, uint bitOffsetLocation)
{
    return sourceData &= ~(1u << bitOffsetLocation);
}

uint CombineBits(uint sourceData, uint sourceDataBitSize, uint newData, uint newDataBitSize)
{
    uint bitsA = KeepBitsOfValue(sourceData, 0, sourceDataBitSize);
    uint bitsB = KeepBitsOfValue(newData, 0, newDataBitSize);
    return bitsA | bitsB << sourceDataBitSize;
}

//|||||||||||||||||||||||||||||||||||||| SRP CORE PACKING ||||||||||||||||||||||||||||||||||||||
//|||||||||||||||||||||||||||||||||||||| SRP CORE PACKING ||||||||||||||||||||||||||||||||||||||
//|||||||||||||||||||||||||||||||||||||| SRP CORE PACKING ||||||||||||||||||||||||||||||||||||||

// Unsigned integer bit field extraction.
// Note that the intrinsic itself generates a vector instruction.
// Wrap this function with WaveReadLaneFirst() to get scalar output.
uint BitFieldExtract(uint data, uint offset, uint numBits)
{
	uint mask = (1u << numBits) - 1u;
	return (data >> offset) & mask;
}

//-----------------------------------------------------------------------------
// Integer packing
//-----------------------------------------------------------------------------

// Packs an integer stored using at most 'numBits' into a [0..1] float.
float PackInt(uint i, uint numBits)
{
    uint maxInt = (1u << numBits) - 1u;
    return saturate(i * rcp(maxInt));
}

// Unpacks a [0..1] float into an integer of size 'numBits'.
uint UnpackInt(float f, uint numBits)
{
    uint maxInt = (1u << numBits) - 1u;
    return (uint)(f * maxInt + 0.5); // Round instead of truncating
}

// Packs a [0..255] integer into a [0..1] float.
float PackByte(uint i)
{
    return PackInt(i, 8);
}

// Unpacks a [0..1] float into a [0..255] integer.
uint UnpackByte(float f)
{
    return UnpackInt(f, 8);
}

// Packs a [0..65535] integer into a [0..1] float.
float PackShort(uint i)
{
    return PackInt(i, 16);
}

// Unpacks a [0..1] float into a [0..65535] integer.
uint UnpackShort(float f)
{
    return UnpackInt(f, 16);
}

// Packs 8 lowermost bits of a [0..65535] integer into a [0..1] float.
float PackShortLo(uint i)
{
    uint lo = BitFieldExtract(i, 0u, 8u);
    return PackInt(lo, 8);
}

// Packs 8 uppermost bits of a [0..65535] integer into a [0..1] float.
float PackShortHi(uint i)
{
    uint hi = BitFieldExtract(i, 8u, 8u);
    return PackInt(hi, 8);
}

float Pack2Byte(float2 inputs)
{
    float2 temp = inputs * float2(255.0, 255.0);
    temp.x *= 256.0;
    temp = round(temp);
    float combined = temp.x + temp.y;
    return combined * (1.0 / 65535.0);
}

float2 Unpack2Byte(float inputs)
{
    float temp = round(inputs * 65535.0);
    float ipart;
    float fpart = modf(temp / 256.0, ipart);
    float2 result = float2(ipart, round(256.0 * fpart));
    return result * (1.0 / float2(255.0, 255.0));
}

// Encode a float in [0..1] and an int in [0..maxi - 1] as a float [0..1] to be store in log2(precision) bit
// maxi must be a power of two and define the number of bit dedicated 0..1 to the int part (log2(maxi))
// Example: precision is 256.0, maxi is 2, i is [0..1] encode on 1 bit. f is [0..1] encode on 7 bit.
// Example: precision is 256.0, maxi is 4, i is [0..3] encode on 2 bit. f is [0..1] encode on 6 bit.
// Example: precision is 256.0, maxi is 8, i is [0..7] encode on 3 bit. f is [0..1] encode on 5 bit.
// ...
// Example: precision is 1024.0, maxi is 8, i is [0..7] encode on 3 bit. f is [0..1] encode on 7 bit.
//...
float PackFloatInt(float f, uint i, float maxi, float precision)
{
    // Constant
    float precisionMinusOne = precision - 1.0;
    float t1 = ((precision / maxi) - 1.0) / precisionMinusOne;
    float t2 = (precision / maxi) / precisionMinusOne;

    return t1 * f + t2 * float(i);
}

void UnpackFloatInt(float val, float maxi, float precision, out float f, out uint i)
{
    // Constant
    float precisionMinusOne = precision - 1.0;
    float t1 = ((precision / maxi) - 1.0) / precisionMinusOne;
    float t2 = (precision / maxi) / precisionMinusOne;

    // extract integer part
    i = int((val / t2) + rcp(precisionMinusOne)); // + rcp(precisionMinusOne) to deal with precision issue (can't use round() as val contain the floating number
    // Now that we have i, solve formula in PackFloatInt for f
    //f = (val - t2 * float(i)) / t1 => convert in mads form
    f = saturate((-t2 * float(i) + val) / t1); // Saturate in case of precision issue
}

// Define various variante for ease of read
float PackFloatInt8bit(float f, uint i, float maxi)
{
    return PackFloatInt(f, i, maxi, 256.0);
}

void UnpackFloatInt8bit(float val, float maxi, out float f, out uint i)
{
    UnpackFloatInt(val, maxi, 256.0, f, i);
}

float PackFloatInt10bit(float f, uint i, float maxi)
{
    return PackFloatInt(f, i, maxi, 1024.0);
}

void UnpackFloatInt10bit(float val, float maxi, out float f, out uint i)
{
    UnpackFloatInt(val, maxi, 1024.0, f, i);
}

float PackFloatInt16bit(float f, uint i, float maxi)
{
    return PackFloatInt(f, i, maxi, 65536.0);
}

void UnpackFloatInt16bit(float val, float maxi, out float f, out uint i)
{
    UnpackFloatInt(val, maxi, 65536.0, f, i);
}

//-----------------------------------------------------------------------------
// Float packing
//-----------------------------------------------------------------------------

// src must be between 0.0 and 1.0
uint PackFloatToUInt(float src, uint offset, uint numBits)
{
    return UnpackInt(src, numBits) << offset;
}

float UnpackUIntToFloat(uint src, uint offset, uint numBits)
{
    uint maxInt = (1u << numBits) - 1u;
    return float(BitFieldExtract(src, offset, numBits)) * rcp(maxInt);
}

uint PackToR10G10B10A2(float4 rgba)
{
    return (PackFloatToUInt(rgba.x, 0, 10) |
        PackFloatToUInt(rgba.y, 10, 10) |
        PackFloatToUInt(rgba.z, 20, 10) |
        PackFloatToUInt(rgba.w, 30, 2));
}

float4 UnpackFromR10G10B10A2(uint rgba)
{
    float4 output;
    output.x = UnpackUIntToFloat(rgba, 0, 10);
    output.y = UnpackUIntToFloat(rgba, 10, 10);
    output.z = UnpackUIntToFloat(rgba, 20, 10);
    output.w = UnpackUIntToFloat(rgba, 30, 2);
    return output;
}

// Both the input and the output are in the [0, 1] range.
float2 PackFloatToR8G8(float f)
{
    uint i = UnpackShort(f);
    return float2(PackShortLo(i), PackShortHi(i));
}

// Both the input and the output are in the [0, 1] range.
float UnpackFloatFromR8G8(float2 f)
{
    uint lo = UnpackByte(f.x);
    uint hi = UnpackByte(f.y);
    uint cb = (hi << 8) + lo;
    return PackShort(cb);
}

// Pack float2 (each of 12 bit) in 888
float3 PackFloat2To888(float2 f)
{
    uint2 i = (uint2)(f * 4095.5);
    uint2 hi = i >> 8;
    uint2 lo = i & 255;
    // 8 bit in lo, 4 bit in hi
    uint3 cb = uint3(lo, hi.x | (hi.y << 4));

    return cb / 255.0;
}

// Unpack 2 float of 12bit packed into a 888
float2 Unpack888ToFloat2(float3 x)
{
    uint3 i = (uint3)(x * 255.5); // +0.5 to fix precision error on iOS 
    // 8 bit in lo, 4 bit in hi
    uint hi = i.z >> 4;
    uint lo = i.z & 15;
    uint2 cb = i.xy | uint2(lo << 8, hi << 8);

    return cb / 4095.0;
}

// Pack 2 float values from the [0, 1] range, to an 8 bits float from the [0, 1] range
float PackFloat2To8(float2 f)
{
    float x_expanded = f.x * 15.0;                        // f.x encoded over 4 bits, can have 2^4 = 16 distinct values mapped to [0, 1, ..., 15]
    float y_expanded = f.y * 15.0;                        // f.y encoded over 4 bits, can have 2^4 = 16 distinct values mapped to [0, 1, ..., 15]
    float x_y_expanded = x_expanded * 16.0 + y_expanded;  // f.x encoded over higher bits, f.y encoded over the lower bits - x_y values in range [0, 1, ..., 255]
    return x_y_expanded / 255.0;

    // above 4 lines equivalent to:
    //return (16.0 * f.x + f.y) / 17.0; 
}

// Unpack 2 float values from the [0, 1] range, packed in an 8 bits float from the [0, 1] range
float2 Unpack8ToFloat2(float f)
{
    float x_y_expanded = 255.0 * f;
    float x_expanded = floor(x_y_expanded / 16.0);
    float y_expanded = x_y_expanded - 16.0 * x_expanded;
    float x = x_expanded / 15.0;
    float y = y_expanded / 15.0;
    return float2(x, y);
}

//
// Hue, Saturation, Value
// Ranges:
//  Hue [0.0, 1.0]
//  Sat [0.0, 1.0]
//  Lum [0.0, HALF_MAX]
//
#define EPSILON 1.0e-4
#define Epsilon 1e-10

//https://www.chilliant.com/rgb2hsv.html
float3 RgbToHsv(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);

    float hue = abs(q.z + (q.w - q.y) / (6.0 * d + EPSILON));
    float saturation = d / (q.x + EPSILON);
    float value = q.x;

    return float3(hue, saturation, value);
}

float3 HsvToRgb(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

// Convert rgb to luminance
// with rgb in linear space with sRGB primaries and D65 white point
float Luminance(float3 linearRgb)
{
    return dot(linearRgb, float3(0.2126729, 0.7151522, 0.0721750));
}