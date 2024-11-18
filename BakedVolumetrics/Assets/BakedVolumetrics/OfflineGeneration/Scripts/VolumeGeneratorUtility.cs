#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BakedVolumetricsOffline
{
    public static class VolumeGeneratorUtility
    {
        public static void UpdateProgressBar(string description, float progress) => EditorUtility.DisplayProgressBar("Baked Volumetrics", description, progress);

        public static void CloseProgressBar() => EditorUtility.ClearProgressBar();

        public static bool ContainBounds(Bounds bounds, Bounds target) => bounds.Contains(target.center) || bounds.Contains(target.min) || bounds.Contains(target.max);

        public static void SetComputeKeyword(ComputeShader computeShader, string keyword, bool value)
        {
            if (value)
                computeShader.EnableKeyword(keyword);
            else
                computeShader.DisableKeyword(keyword);
        }

        public static void SetMaterialKeyword(Material material, string keyword, bool value)
        {
            if (value)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }
    }
}
#endif