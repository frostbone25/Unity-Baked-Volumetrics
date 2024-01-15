#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BakedVolumetrics
{
    public static class NoiseLibrary
    {
        private static int jitterResolution = 64;

        public static Texture2D GetRandomNoise()
        {
            if (AssetDatabase.IsValidFolder("Assets/BakedVolumetrics/Data") == false)
                AssetDatabase.CreateFolder("Assets/BakedVolumetrics", "Data");

            string sharedVolumetricsFolder = "Assets/BakedVolumetrics/Data/Shared";

            if (AssetDatabase.IsValidFolder(sharedVolumetricsFolder) == false)
                AssetDatabase.CreateFolder("Assets/BakedVolumetrics/Data", "Shared");

            string jitterAssetName = "WhiteNoiseJitter.asset";
            string jitterAssetPath = sharedVolumetricsFolder + "/" + jitterAssetName;

            Texture2D jitter = AssetDatabase.LoadAssetAtPath<Texture2D>(jitterAssetPath);

            if (jitter == null)
            {
                jitter = new Texture2D(jitterResolution, jitterResolution, TextureFormat.RGBA4444, false);
                jitter.filterMode = FilterMode.Bilinear;
                jitter.anisoLevel = 0;

                for (int x = 0; x < jitterResolution; x++)
                {
                    for (int y = 0; y < jitterResolution; y++)
                    {
                        Color randomColor = new Color(UnityEngine.Random.Range(0.0f, 1.0f), 0.0f, 0.0f, 1.0f);
                        jitter.SetPixel(x, y, randomColor);
                    }
                }

                AssetDatabase.CreateAsset(jitter, jitterAssetPath);
            }

            return jitter;
        }

        public static Texture2D GetBlueNoise()
        {
            if (AssetDatabase.IsValidFolder("Assets/BakedVolumetrics/Data/Shared/BlueNoise") == false)
            {
                Debug.LogWarning("Baked Volumetrics Error! Original folder structure has been changed and cant find blue noise textures, resorting to generating random noise.");
                return GetRandomNoise();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/BakedVolumetrics/Data/Shared/BlueNoise/128_128/HDR_L_0.png");
        }
    }

}
#endif