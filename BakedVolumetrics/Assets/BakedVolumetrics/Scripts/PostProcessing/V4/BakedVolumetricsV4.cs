using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace BakedVolumetrics
{
    [Serializable]
    [PostProcess(typeof(BakedVolumetricsV4Renderer), PostProcessEvent.BeforeStack, "Custom/BakedVolumetricsV4")]
    public sealed class BakedVolumetricsV4 : PostProcessEffectSettings
    {
        public TextureParameter jitter = new TextureParameter();
        public FloatParameter density = new FloatParameter() { value = 0.0f };
        public FloatParameter stepSize = new FloatParameter() { value = 25.0f };
        public FloatParameter jitterStrength = new FloatParameter() { value = 2.0f };
    }
}