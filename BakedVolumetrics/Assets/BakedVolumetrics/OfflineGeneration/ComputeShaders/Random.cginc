//||||||||||||||||||||||||||||| RANDOM FUNCTIONS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| RANDOM FUNCTIONS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| RANDOM FUNCTIONS |||||||||||||||||||||||||||||

float RandomSeed;

// A single iteration of Bob Jenkins' One-At-A-Time hashing algorithm.
uint JenkinsHash(uint x)
{
    x += (x << 10u);
    x ^= (x >> 6u);
    x += (x << 3u);
    x ^= (x >> 11u);
    x += (x << 15u);
    return x;
}

// Compound versions of the hashing algorithm.
uint JenkinsHash(uint2 v)
{
    return JenkinsHash(v.x ^ JenkinsHash(v.y));
}

uint JenkinsHash(uint3 v)
{
    return JenkinsHash(v.x ^ JenkinsHash(v.yz));
}

uint JenkinsHash(uint4 v)
{
    return JenkinsHash(v.x ^ JenkinsHash(v.yzw));
}

// Construct a float with half-open range [0, 1) using low 23 bits.
// All zeros yields 0, all ones yields the next smallest representable value below 1.
float ConstructFloat(int m) 
{
    const int ieeeMantissa = 0x007FFFFF; // Binary FP32 mantissa bitmask
    const int ieeeOne = 0x3F800000; // 1.0 in FP32 IEEE

    m &= ieeeMantissa;                   // Keep only mantissa bits (fractional part)
    m |= ieeeOne;                        // Add fractional part to 1.0

    float  f = asfloat(m);               // Range [1, 2)
    return f - 1;                        // Range [0, 1)
}

float ConstructFloat(uint m)
{
    return ConstructFloat(asint(m));
}

// Pseudo-random value in half-open range [0, 1). The distribution is reasonably uniform.
// Ref: https://stackoverflow.com/a/17479300
float GenerateHashedRandomFloat(uint x)
{
    return ConstructFloat(JenkinsHash(x));
}

float GenerateHashedRandomFloat(uint2 v)
{
    return ConstructFloat(JenkinsHash(v));
}

float GenerateHashedRandomFloat(uint3 v)
{
    return ConstructFloat(JenkinsHash(v));
}

float GenerateHashedRandomFloat(uint4 v)
{
    return ConstructFloat(JenkinsHash(v));
}

float GenerateRandomFloat(float2 screenUV)
{
    RandomSeed += 1.0;
    //return GenerateHashedRandomFloat(uint3(screenUV * _ScreenParams.xy, RandomSeed));
    return GenerateHashedRandomFloat(uint3(screenUV, RandomSeed));
}

float GenerateRandomFloat(float3 vec3)
{
    RandomSeed += 1.0;
    return GenerateHashedRandomFloat(uint4(vec3, RandomSeed));
}

//||||||||||||||||||||||||||||| CLASSIC RANDOM FUNCTIONS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| CLASSIC RANDOM FUNCTIONS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| CLASSIC RANDOM FUNCTIONS |||||||||||||||||||||||||||||

float hash(float2 p)  // replace this by something better
{
    p = 50.0 * frac(p * 0.3183099 + float2(0.71, 0.113));
    return -1.0 + 2.0 * frac(p.x * p.y * (p.x + p.y));
}

float rand(float co) 
{ 
    return frac(sin(co * (91.3458)) * 47453.5453); 
}

float rand(float2 co) 
{ 
    return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453); 
}

float rand(float3 co) 
{ 
    return rand(co.xy + rand(co.z)); 
}