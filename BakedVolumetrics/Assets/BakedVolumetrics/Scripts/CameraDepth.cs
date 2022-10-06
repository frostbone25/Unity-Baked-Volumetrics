using UnityEngine;

namespace BakedVolumetrics
{
    [ExecuteInEditMode]
    public class CameraDepth : MonoBehaviour
    {
        private Camera camera;

        private void Awake()
        {
            camera = GetComponent<Camera>();
            camera.depthTextureMode = DepthTextureMode.Depth;
        }

        private void OnEnable()
        {
            camera.depthTextureMode = DepthTextureMode.Depth;
        }
    }
}