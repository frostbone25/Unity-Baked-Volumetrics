using UnityEngine;

namespace BakedVolumetrics
{
    [ExecuteInEditMode]
    public class CameraDepth : MonoBehaviour
    {
        public DepthTextureMode mode;
        private Camera camera;

        private void Awake()
        {
            camera = GetComponent<Camera>();
            camera.depthTextureMode = mode;
        }

        private void OnEnable()
        {
            camera.depthTextureMode = mode;
        }
    }
}