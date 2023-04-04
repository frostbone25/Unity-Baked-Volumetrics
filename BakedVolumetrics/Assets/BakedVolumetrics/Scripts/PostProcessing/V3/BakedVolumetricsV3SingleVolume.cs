using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace BakedVolumetrics
{
    [Serializable]
    public sealed class BakedVolumetricsV3SingleVolume
    {
        public Texture3D volume;
        public Vector3Int volumeResolution;
        public Vector3 volumePosition;
        public Vector3 volumeSize;
    }
}