/*
 * ----------------------------------------------------------
 * Baked Volumetrics Post Process V2
 * 
 * This is the second implementation for the baked volumetrics effect.
 * 
 * Just like V1...
 * We sample the generated 3D texture volume and raymarch through it against scene depth.
 * Then the result is combined with the regular scene color render target.
 * 
 * V2 Additions
 * - Raymarching is done at Half Res.
 * - A depth aware upsample is performed.
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
	[PostProcess(typeof(BakedVolumetricsV2Renderer), PostProcessEvent.BeforeStack, "Custom/BakedVolumetricsV2")]
	public sealed class BakedVolumetricsV2 : PostProcessEffectSettings
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