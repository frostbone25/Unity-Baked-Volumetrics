using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.XR;
//using UnityEngine.XR.Management;
//using UnityEngine.Experimental.XR.Interaction;
//using Unity.XR.Oculus;
//using System.Linq;

namespace BakedVolumetrics
{
    [ExecuteInEditMode]
    public class CameraDepth : MonoBehaviour
    {
        public DepthTextureMode mode;

        //public bool enableDynamicFFR = false;
        //[Range(0, 4)] public int foveationLevel = 0;

        private Camera camera;

        /*
        private void SetOculusSettings()
        {
            if (Unity.XR.Oculus.Performance.TryGetDisplayRefreshRate(out var rate))
            {
                float newRate = 120f; // fallback to this value if the query fails.
                                      //float newRate = 90f; // fallback to this value if the query fails.
                if (Unity.XR.Oculus.Performance.TryGetAvailableDisplayRefreshRates(out var rates))
                {
                    newRate = rates.Max();
                }
                if (rate < newRate)
                {
                    if (Unity.XR.Oculus.Performance.TrySetDisplayRefreshRate(newRate))
                    {
                        Time.fixedDeltaTime = 1f / newRate;
                        Time.maximumDeltaTime = 1f / newRate;
                    }
                }
            }

            //https://developer.oculus.com/documentation/native/android/mobile-ffr/
            Unity.XR.Oculus.Utils.EnableDynamicFFR(enableDynamicFFR);
            Unity.XR.Oculus.Utils.SetFoveationLevel(foveationLevel);
        }
        */

        private void Awake()
        {
            camera = GetComponent<Camera>();
            camera.depthTextureMode = mode;

            //SetOculusSettings();
        }

        private void Update()
        {
            camera.depthTextureMode = mode;
            //SetOculusSettings();
        }

        private void OnEnable()
        {
            camera.depthTextureMode = mode;
            //SetOculusSettings();
        }
    }
}