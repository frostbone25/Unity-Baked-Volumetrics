//||||||||||||||||||||||||||||| UTILITY FUNCTIONS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| UTILITY FUNCTIONS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| UTILITY FUNCTIONS |||||||||||||||||||||||||||||

float3 GetRandomDirection(float3 direction)
{
    return float3(rand(direction.x), rand(direction.y), rand(direction.z));
}

float CalculateAttenuation(float distanceToSource, float range)
{
#if defined (_ATTENUATION_INVERSE_SQUARE)
    return 1.0f / (distanceToSource * distanceToSource);
#elif defined (_ATTENUATION_LINEAR)
    return 1.0f / distanceToSource;
#elif defined (_ATTENUATION_UNITY) //REFERENCE - https://geom.io/bakery/wiki/index.php?title=Point_Light_Attenuation
    float term = (distanceToSource / range) * 5.0f;
    return 1.0f / (term * term + 1.0f);
#else
    return 1.0f / distanceToSource;
#endif
}

// Function to estimate the surface normal at a given 3D texture position
float3 EstimateSurfaceNormal(Texture3D<float4> sceneColor, float3 texCoord, float3 volumeResolution)
{
    // Compute the step size between neighboring 3D texture samples
    float3 delta = 1.0 / volumeResolution.xyz;

    // Sample the 3D texture at the current position and its neighboring positions
    float center = TEX3D_SHARP(sceneColor, texCoord, 0).a;
    float dx = TEX3D_SHARP(sceneColor, texCoord + float3(delta.x, 0.0, 0.0), 0).a;
    float dy = TEX3D_SHARP(sceneColor, texCoord + float3(0.0, delta.y, 0.0), 0).a;
    float dz = TEX3D_SHARP(sceneColor, texCoord + float3(0.0, 0.0, delta.z), 0).a;

    // Compute the gradient by subtracting neighboring samples
    float3 gradient = float3(dx - center, dy - center, dz - center);

    // Calculate the surface normal by normalizing the gradient
    float3 normal = normalize(gradient);

    return normal;
}

bool PositionInVolumeBounds(float3 worldPosition, float3 volumePosition, float3 volumeSize)
{
    if (worldPosition.x > volumePosition.x + volumeSize.x)
        return false;

    if (worldPosition.x < volumePosition.x - volumeSize.x)
        return false;

    if (worldPosition.y > volumePosition.y + volumeSize.x)
        return false;

    if (worldPosition.y < volumePosition.y - volumeSize.x)
        return false;

    return true;
}