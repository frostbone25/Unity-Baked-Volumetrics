//||||||||||||||||||||||||||||| UTILITY FUNCTIONS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| UTILITY FUNCTIONS |||||||||||||||||||||||||||||
//||||||||||||||||||||||||||||| UTILITY FUNCTIONS |||||||||||||||||||||||||||||

float3 GetRandomDirection(float3 direction)
{
    return float3(rand(direction.x), rand(direction.y), rand(direction.z));
}

float CalculateAttenuation(float distanceToSource)
{
    //return 1.0f / (distanceToSource * distanceToSource); //inverse square falloff
    return 1.0f / distanceToSource; //linear falloff;
    //return 1.0f / pow(distanceToSource, 4.0);
}

// Function to estimate the surface normal at a given 3D texture position
float3 EstimateSurfaceNormal(Texture3D<float4> sceneColor, float3 texCoord, float3 volumeResolution)
{
    // Compute the step size between neighboring 3D texture samples
    float3 delta = 1.0 / volumeResolution.xyz;

    // Sample the 3D texture at the current position and its neighboring positions
    float center = TEX3D_SHARP(sceneColor, texCoord).a;
    float dx = TEX3D_SHARP(sceneColor, texCoord + float3(delta.x, 0.0, 0.0)).a;
    float dy = TEX3D_SHARP(sceneColor, texCoord + float3(0.0, delta.y, 0.0)).a;
    float dz = TEX3D_SHARP(sceneColor, texCoord + float3(0.0, 0.0, delta.z)).a;

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