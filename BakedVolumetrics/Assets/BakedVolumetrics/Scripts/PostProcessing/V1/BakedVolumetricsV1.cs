using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace BakedVolumetrics
{
	[Serializable]
	[PostProcess(typeof(BakedVolumetricsV1Renderer), PostProcessEvent.BeforeStack, "Custom/BakedVolumetricsV1")]
	public sealed class BakedVolumetricsV1 : PostProcessEffectSettings
	{
		public TextureParameter volume = new TextureParameter();
		public TextureParameter jitter = new TextureParameter();
		public FloatParameter density = new FloatParameter() { value = 0.0f };
		public FloatParameter stepSize = new FloatParameter() { value = 25.0f };
		public FloatParameter jitterStrength = new FloatParameter() { value = 2.0f };

		public Vector3Parameter volumeResolution = new Vector3Parameter();
		public Vector3Parameter volumePosition = new Vector3Parameter();
		public Vector3Parameter volumeSize = new Vector3Parameter();
	}
}