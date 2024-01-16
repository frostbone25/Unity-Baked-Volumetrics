//||||||||||||||||||||||||||||| UNITY LIGHTS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| UNITY LIGHTS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| UNITY LIGHTS |||||||||||||||||||||||||||||

#if defined (DIRECTIONAL_LIGHTS)
struct LightDirectional //256 BITS | 32 BYTES
{
    float3 lightDirection; //96 BITS | 12 BYTES
    float3 lightColor; //96 BITS | 12 BYTES
    float shadowAngle; //32 BITS | 4 BYTES

    //https://developer.nvidia.com/content/understanding-structured-buffer-performance
    //Additional padding to the structure so that it stays divisible by 128 bits.
    float UNUSED_0; //32 BITS | 4 BYTES
};

RWStructuredBuffer<LightDirectional> DirectionalLights;
//StructuredBuffer<LightDirectional> DirectionalLights;
#endif

#if defined (POINT_LIGHTS)
struct LightPoint //256 BITS | 32 BYTES
{
    float3 lightPosition; //96 BITS | 12 BYTES
    float3 lightColor; //96 BITS | 12 BYTES
    float lightRange; //32 BITS | 4 BYTES
    float shadowRadius; //32 BITS | 4 BYTES
};

RWStructuredBuffer<LightPoint> PointLights;
//StructuredBuffer<LightPoint> PointLights;
#endif

#if defined (SPOT_LIGHTS)
struct LightSpot //384 BITS | 48 BYTES
{
    float3 lightPosition; //96 BITS | 12 BYTES
    float3 lightDirection; //96 BITS | 12 BYTES
    float3 lightColor; //96 BITS | 12 BYTES
    float lightRange; //32 BITS | 4 BYTES
    float lightAngle; //32 BITS | 4 BYTES
    float shadowRadius; //32 BITS | 4 BYTES
};

RWStructuredBuffer<LightSpot> SpotLights;
//StructuredBuffer<LightSpot> SpotLights;
#endif

#if defined (AREA_LIGHTS)
struct LightArea //640 BITS | 80 BYTES
{
    float3 lightPosition; //96 BITS | 12 BYTES
    float3 lightForwardDirection; //96 BITS | 12 BYTES
    float3 lightRightDirection; //96 BITS | 12 BYTES
    float3 lightUpwardDirection; //96 BITS | 12 BYTES
    float2 lightSize; //64 BITS | 8 BYTES
    float3 lightColor; //96 BITS | 12 BYTES
    float lightRange; //32 BITS | 4 BYTES

    //https://developer.nvidia.com/content/understanding-structured-buffer-performance
    //Additional padding to the structure so that it stays divisible by 128 bits.
    float UNUSED_0; //32 BITS | 4 BYTES
    float UNUSED_1; //32 BITS | 4 BYTES
};

RWStructuredBuffer<LightArea> AreaLights;
//StructuredBuffer<LightArea> AreaLights;
#endif