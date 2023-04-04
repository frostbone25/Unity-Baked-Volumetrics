using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace BakedVolumetrics
{
    [Serializable]
    [PostProcess(typeof(BakedVolumetricsV3Renderer), PostProcessEvent.BeforeStack, "Custom/BakedVolumetricsV3")]
    public sealed class BakedVolumetricsV3 : PostProcessEffectSettings
    {
        [Header("Rendering")]
        public TextureParameter jitter = new TextureParameter();
        public FloatParameter density = new FloatParameter() { value = 0.0f };
        public FloatParameter stepSize = new FloatParameter() { value = 25.0f };
        public FloatParameter jitterStrength = new FloatParameter() { value = 2.0f };

        [Header("Volume")]
        public BakedVolumetricsV3VolumeArrayParameter volumes = new BakedVolumetricsV3VolumeArrayParameter();
    }
}