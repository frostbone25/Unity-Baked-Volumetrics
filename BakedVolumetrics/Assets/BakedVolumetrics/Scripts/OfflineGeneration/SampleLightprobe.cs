#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;

namespace BakedVolumetrics
{
    public class SampleLightprobe : MonoBehaviour
    {
        public float occlusionLeakFactor = 1.0f;
        public bool occlusionPreventLeaks = false;
        public bool indoorOnlySamples = false;

        //private
        [HideInInspector] public bool showUI;

        public Color SampleVolumetricColor(Vector3 probePosition, Vector3 voxelWorldSize)
        {
            bool test_leak = occlusionPreventLeaks ? (Physics.CheckBox(probePosition, voxelWorldSize * occlusionLeakFactor) == false) : true;
            bool test_indoor = true;

            if(indoorOnlySamples)
            {
                bool hit_up = Physics.Raycast(probePosition, Vector3.up, float.MaxValue);
                bool hit_down = Physics.Raycast(probePosition, Vector3.down, float.MaxValue);
                bool hit_left = Physics.Raycast(probePosition, Vector3.left, float.MaxValue);
                bool hit_right = Physics.Raycast(probePosition, Vector3.right, float.MaxValue);
                bool hit_forward = Physics.Raycast(probePosition, Vector3.forward, float.MaxValue);
                bool hit_back = Physics.Raycast(probePosition, Vector3.back, float.MaxValue);

                test_indoor = hit_up && hit_down && hit_left && hit_right && hit_forward && hit_back;
            }

            if (!test_indoor || !test_leak)
            {
                return Color.black;
            }

            SphericalHarmonicsL2 sphericalHarmonicsL2 = new SphericalHarmonicsL2();
            Renderer renderer = new Renderer();

            LightProbes.GetInterpolatedProbe(probePosition, renderer, out sphericalHarmonicsL2);

            Vector3[] sampledDirections = new Vector3[1]
            {
                Vector3.zero
            };

            Color[] resultingColors = new Color[1];

            sphericalHarmonicsL2.Evaluate(sampledDirections, resultingColors);

            return resultingColors[0];
        }
    }
}
#endif