//https://aras-p.info/texts/CompactNormalStorage.html
//Method #1: Store X & Y and Reconstruct Z
/*
float2 EncodeNormal(half3 normal)
{
    return normal.xy * 0.5 + 0.5;
}

float3 DecodeNormal(half2 encodedNormal)
{
    half3 normal;
    normal.xy = encodedNormal * 2 - 1;
    normal.z = sqrt(1 - dot(normal.xy, normal.xy));
    return normal;
}
*/

//https://aras-p.info/texts/CompactNormalStorage.html
//Method #3: Spherical Coordinates
/*
#define kPI 3.1415926536f
float2 EncodeNormal(float3 normal)
{
    return (float2(atan2(normal.y, normal.x) / kPI, normal.z) + 1.0) * 0.5;
}

float3 DecodeNormal(float2 encodedNormal)
{
    float2 ang = encodedNormal * 2 - 1;
    float2 scth;
    sincos(ang.x * kPI, scth.x, scth.y);
    float2 scphi = float2(sqrt(1.0 - ang.y * ang.y), ang.y);
    return float3(scth.y * scphi.x, scth.x * scphi.x, scphi.y);
}
*/

//https://aras-p.info/texts/CompactNormalStorage.html
//Method #4: Spheremap Transform (Used in Cry Engine 3)
/*
float2 EncodeNormal(float3 normal)
{
    float2 enc = normalize(normal.xy) * (sqrt(-normal.z * 0.5 + 0.5));
    enc = enc * 0.5 + 0.5;
    return enc;
}

float3 DecodeNormal(float2 encodedNormal)
{
    float4 nn = float4(encodedNormal.xy, 0, 0) * float4(2, 2, 0, 0) + float4(-1, -1, 1, -1);
    float l = dot(nn.xyz, -nn.xyw);
    nn.z = l;
    nn.xy *= sqrt(l);
    return nn.xyz * 2 + float3(0, 0, -1);
}
*/

//https://aras-p.info/texts/CompactNormalStorage.html
//Method #4: Spheremap Transform (Lambert Azimuthal Equal-Area projection)
/*
float2 EncodeNormal(float3 normal)
{
    float f = sqrt(8 * normal.z + 8);
    return normal.xy / f + 0.5;
}

float3 DecodeNormal(float2 encodedNormal)
{
    float2 fenc = encodedNormal * 4 - 2;
    float f = dot(fenc, fenc);
    float g = sqrt(1 - f / 4);
    float3 n;
    n.xy = fenc * g;
    n.z = 1 - f / 2;
    return n;
}
*/

//https://aras-p.info/texts/CompactNormalStorage.html
//Method #4: Spheremap Transform
/*
float2 EncodeNormal(float3 normal)
{
    half p = sqrt(normal.z * 8 + 8);
    return normal.xy / p + 0.5;
}

float3 DecodeNormal(float2 encodedNormal)
{
    float2 fenc = encodedNormal * 4 - 2;
    float f = dot(fenc, fenc);
    float g = sqrt(1 - f / 4);
    float3 n;
    n.xy = fenc * g;
    n.z = 1 - f / 2;
    return n;
}
*/

//======================= NOTE TO SELF: BEST ONE SO FAR =======================
//https://aras-p.info/texts/CompactNormalStorage.html
//Method #7: Stereographic Projection
/*
float2 EncodeNormal(float3 normal)
{
    float scale = 1.7777;
    float2 enc = normal.xy / (normal.z + 1);
    enc /= scale;
    enc = enc * 0.5 + 0.5;
    return enc;
}

float3 DecodeNormal(float2 encodedNormal)
{
    half scale = 1.7777;
    half3 nn = float3(encodedNormal.xy, 0) * half3(2 * scale, 2 * scale, 0) + half3(-scale, -scale, 1);
    half g = 2.0 / dot(nn.xyz, nn.xyz);
    half3 n;
    n.xy = g * nn.xy;
    n.z = g - 1;
    return n;
}
*/

//https://knarkowicz.wordpress.com/2014/04/16/octahedron-normal-vector-encoding/
//Spherical coordinates
/*
#define kPI 3.14159265359f
#define kINV_PI 0.31830988618f
float2 EncodeNormal(float3 n)
{
    float2 f;
    f.x = atan2(n.y, n.x) * kINV_PI;
    f.y = n.z;
 
    f = f * 0.5 + 0.5;
    return f;
}
 
float3 DecodeNormal(float2 f)
{
    float2 ang = f * 2.0 - 1.0;
 
    float2 scth;
    sincos(ang.x * kPI, scth.x, scth.y);
    float2 scphi = float2(sqrt(1.0 - ang.y * ang.y), ang.y);
 
    float3 n;
    n.x = scth.y * scphi.x;
    n.y = scth.x * scphi.x;
    n.z = scphi.y;
    return n;
}
*/

//https://knarkowicz.wordpress.com/2014/04/16/octahedron-normal-vector-encoding/
//Octahedron-normal vectors
/*
float2 OctWrap(float2 v)
{
    return (1.0 - abs(v.yx)) * (v.xy >= 0.0 ? 1.0 : -1.0);
}
 
float2 EncodeNormal(float3 n)
{
    n /= (abs(n.x) + abs(n.y) + abs(n.z));
    n.xy = n.z >= 0.0 ? n.xy : OctWrap(n.xy);
    n.xy = n.xy * 0.5 + 0.5;
    return n.xy;
}
 
float3 DecodeNormal(float2 f)
{
    f = f * 2.0 - 1.0;
 
    // https://twitter.com/Stubbesaurus/status/937994790553227264
    float3 n = float3(f.x, f.y, 1.0 - abs(f.x) - abs(f.y));
    float t = saturate(-n.z);
    n.xy += n.xy >= 0.0 ? -t : t;
    return normalize(n);
}
*/