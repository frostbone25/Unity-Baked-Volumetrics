/*
 * ----------------------------------------------------------
 * Baked Volumetrics Post Process V1
 * 
 * This is the first and simplest implementation for the baked volumetrics effect.
 * All that is done is to sample the generated 3D texture volume and raymarch through it against scene depth.
 * Then the result is combined with the regular scene color render target.
 * ----------------------------------------------------------
*/

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
		[Header("Rendering")]
		public TextureParameter jitter = new TextureParameter();
		public FloatParameter density = new FloatParameter() { value = 0.0f };
		public FloatParameter stepSize = new FloatParameter() { value = 25.0f };
		public FloatParameter jitterStrength = new FloatParameter() { value = 2.0f };

        [Header("Volume")]
        public TextureParameter volume = new TextureParameter();
        public Vector3Parameter volumeResolution = new Vector3Parameter();
		public Vector3Parameter volumePosition = new Vector3Parameter();
		public Vector3Parameter volumeSize = new Vector3Parameter();
	}
}