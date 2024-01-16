//||||||||||||||||||||||||||||| SAMPLING FUNCTIONS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| SAMPLING FUNCTIONS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| SAMPLING FUNCTIONS |||||||||||||||||||||||||||||

// Assumes that (0 <= x <= Pi).
float SinFromCos(float cosX)
{
    return sqrt(saturate(1 - cosX * cosX));
}

// Transforms the unit vector from the spherical to the Cartesian (right-handed, Z up) coordinate.
float3 SphericalToCartesian(float cosPhi, float sinPhi, float cosTheta)
{
    float sinTheta = SinFromCos(cosTheta);

    return float3(float2(cosPhi, sinPhi) * sinTheta, cosTheta);
}

float3 SphericalToCartesian(float phi, float cosTheta)
{
    float sinPhi, cosPhi;
    sincos(phi, sinPhi, cosPhi);

    return SphericalToCartesian(cosPhi, sinPhi, cosTheta);
}

float3 SampleSphereUniform(float u1, float u2)
{
    float phi = UNITY_TWO_PI * u2;
    float cosTheta = 1.0 - 2.0 * u1;

    return SphericalToCartesian(phi, cosTheta);
}

float3 SampleHemisphereUniform(float u1, float u2)
{
    float phi = UNITY_TWO_PI * u2;
    float cosTheta = 1.0 - u1;

    return SphericalToCartesian(phi, cosTheta);
}

// Cosine-weighted sampling without the tangent frame.
// Ref: http://www.amietia.com/lambertnotangent.html
float3 SampleHemisphereCosine(float u1, float u2, float3 normal)
{
    // This function needs to used safenormalize because there is a probability
    // that the generated direction is the exact opposite of the normal and that would lead
    // to a nan vector otheriwse.
    float3 pointOnSphere = SampleSphereUniform(u1, u2);
    return normalize(normal + pointOnSphere);
}