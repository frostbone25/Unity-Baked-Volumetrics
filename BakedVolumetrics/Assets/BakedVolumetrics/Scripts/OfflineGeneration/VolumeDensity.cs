using BakedVolumetrics;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class VolumeDensity
{
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| MAIN ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

    public static float ComputeDensity(
        DensityType densityType, 
        Vector3 probePosition, 
        Color colorResult, 
        float densityConstant, 
        float densityHeight, 
        float densityHeightFallof, 
        float densityBottom, 
        float densityTop, 
        bool densityInvertLuminance)
    {
        float alphaResult = 1.0f;

        if (densityType == DensityType.Constant)
        {
            alphaResult = GetConstantDensity(densityConstant);
        }
        else if (densityType == DensityType.Luminance)
        {
            alphaResult = GetLuminanceDensity(colorResult, densityInvertLuminance);
        }
        else if (densityType == DensityType.HeightBased)
        {
            alphaResult = GetHeightBasedDensity(probePosition, densityHeight, densityHeightFallof, densityBottom, densityTop);
        }
        else if (densityType == DensityType.HeightBasedLuminance)
        {
            alphaResult = GetHeightBasedLuminanceDensity(colorResult, probePosition, densityHeight, densityHeightFallof, densityBottom, densityTop, densityInvertLuminance);
        }

        return alphaResult;
    }

    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| DENSITY FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| DENSITY FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| DENSITY FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

    public static float GetConstantDensity(float densityValue)
    {
        //return a constant value
        return densityValue;
    }

    public static float GetLuminanceDensity(Color colorResult, bool invert)
    {
        //remap the resulting volumetric color as a vector3
        Vector3 colorAsVector = new Vector3(colorResult.r, colorResult.g, colorResult.b);

        //compute the dot product, so we can get the resulting luminance of the volumeric color
        float densityValue = Vector3.Dot(colorAsVector, GetLuminance());

        //invert the density value if the user wants to
        if (invert)
        {
            densityValue = 1 - densityValue;
        }

        return densityValue;
    }

    public static float GetHeightBasedDensity(Vector3 probePosition, float height, float falloff, float densityBottom, float densityTop)
    {
        //compute the height factor that will fade from the density at the bottom, with the density at the top
        float heightLerpFactor = Mathf.Clamp((probePosition.y - height) / falloff, 0.0f, 1.0f);

        //compute the density, blending the top and bottom density according to the lerp factor
        float densityValue = Mathf.Lerp(densityBottom, densityTop, heightLerpFactor);

        return densityValue;
    }

    public static float GetHeightBasedLuminanceDensity(Color colorResult, Vector3 probePosition, float height, float falloff, float densityBottom, float densityTop, bool invert)
    {
        //remap the resulting volumetric color as a vector3
        Vector3 colorAsVector = new Vector3(colorResult.r, colorResult.g, colorResult.b);

        //compute the dot product, so we can get the resulting luminance of the volumeric color
        float lumaResult = Vector3.Dot(colorAsVector, GetLuminance());

        //invert the density value if the user wants to
        if (invert)
        {
            lumaResult = 1 - lumaResult;
        }

        //compute the height factor that will fade from the density at the bottom, with the density at the top
        float heightLerpFactor = Mathf.Clamp((probePosition.y - height) / falloff, 0.0f, 1.0f);

        //compute the density, blending the top and bottom density according to the lerp factor
        float densityValue = Mathf.Lerp(lumaResult * densityBottom, lumaResult * densityTop, heightLerpFactor);

        return densityValue;
    }

    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| DENSITY UTILITY FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| DENSITY UTILITY FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||||||||||||||||| DENSITY UTILITY FUNCTIONS ||||||||||||||||||||||||||||||||||||||||||||||||||||||||

    //Based off of the UnityCG.cginc Luminance definition
    private static Vector3 GetLuminance()
    {
        if (PlayerSettings.colorSpace == ColorSpace.Gamma)
        {
            return new Vector3(0.22f, 0.707f, 0.071f);
        }
        else if (PlayerSettings.colorSpace == ColorSpace.Linear)
        {
            return new Vector3(0.0396819152f, 0.45802179f, 0.00609653955f);
        }
        else
        {
            return Vector3.one;
        }
    }
}
