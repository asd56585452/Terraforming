Shader "Hidden/Underwater"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

			struct Attributes
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float4 viewVector : TEXCOORD1;
			};

			Varyings vert (Attributes v)
			{
				Varyings o;
				o.vertex = TransformObjectToHClip(v.vertex.xyz);
				o.uv = v.uv;

				// Calculate view vector for URP post-processing
				// Convert UV to clip space, then to view space, then to world space
				float4 clipPos = float4(v.uv * 2.0 - 1.0, 0.0, -1.0);
				float4 viewPos = mul(UNITY_MATRIX_I_P, clipPos);
				viewPos.xyz /= viewPos.w; // Perspective divide
				float3 viewVector = mul((float3x3)UNITY_MATRIX_I_V, viewPos.xyz);
				o.viewVector = float4(viewVector, 0.0);

				return o;
			}

			CBUFFER_START(UnityPerMaterial)
				float3 oceanCentre;
				float oceanRadius;
				float maxVisibility;
				float density;
				float blurDistance;
				float4 underwaterNearCol;
				float4 underwaterFarCol;
				float3 params;
			CBUFFER_END

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			TEXTURE2D(_BlurredTexture);
			SAMPLER(sampler_BlurredTexture);
			TEXTURE2D(_BlueNoise);
			SAMPLER(sampler_BlueNoise);

			float2 squareUV(float2 uv) {
				float width = _ScreenParams.x;
				float height = _ScreenParams.y;
				//float minDim = min(width, height);
				float scale = 1000;
				float x = uv.x * width;
				float y = uv.y * height;
				return float2(x / scale, y / scale);
			}

			// Returns vector (dstToSphere, dstThroughSphere)
			// If ray origin is inside sphere, dstToSphere = 0
			// If ray misses sphere, dstToSphere = maxValue; dstThroughSphere = 0
			float2 raySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir) {
				float3 offset = rayOrigin - sphereCentre;
				float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
				float b = 2 * dot(offset, rayDir);
				float c = dot (offset, offset) - sphereRadius * sphereRadius;
				float d = b * b - 4 * a * c; // Discriminant from quadratic formula

				// Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
				if (d > 0) {
					float s = sqrt(d);
					float dstToSphereNear = max(0, (-b - s) / (2 * a));
					float dstToSphereFar = (-b + s) / (2 * a);

					// Ignore intersections that occur behind the ray
					if (dstToSphereFar >= 0) {
						return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
					}
				}
				// Ray did not intersect sphere
				static const float maxFloat = 3.402823466e+38;
				return float2(maxFloat, 0);
			}


			half4 frag (Varyings i) : SV_Target
			{
				float blueNoise = SAMPLE_TEXTURE2D(_BlueNoise, sampler_BlueNoise, squareUV(i.uv) * params.x).r;
				blueNoise = blueNoise * 2.0 - 1.0;
				blueNoise = sign(blueNoise) * (1.0 - sqrt(1.0 - abs(blueNoise)));
				//return blueNoise;

				half4 originalCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

				float3 rayPos = GetCameraPositionWS();
				float viewLength = length(i.viewVector.xyz);
				float3 rayDir = i.viewVector.xyz / viewLength;

				// Sample depth using URP's depth texture
				float sceneDepth = SampleSceneDepth(i.uv);
				sceneDepth = LinearEyeDepth(sceneDepth, _ZBufferParams) * viewLength;

				float2 hitInfo = raySphere(oceanCentre, oceanRadius, rayPos, rayDir);
				float dstToOcean = hitInfo.x;
				float dstThroughOceanShell = hitInfo.y;
				float3 rayOceanIntersectPos = rayPos + rayDir * dstToOcean - oceanCentre;

				// dst that view ray travels through ocean (before hitting terrain / exiting ocean)
				float oceanViewDepth = min(dstThroughOceanShell, sceneDepth - dstToOcean);


				if (oceanViewDepth > 0) {
					float3 clipPlanePos = rayPos + i.viewVector.xyz * UNITY_NEAR_CLIP_VALUE;

					float dstAboveWater = oceanRadius - length(clipPlanePos - oceanCentre);
					if (dstAboveWater > 0) {
						// Looking through water to top layer of ocean
						
						half4 blurredCol = SAMPLE_TEXTURE2D(_BlurredTexture, sampler_BlurredTexture, i.uv);
						half4 bgCol = lerp(originalCol, blurredCol, saturate(oceanViewDepth / blurDistance));

						float visibility = exp(-oceanViewDepth * density * 0.001);
						visibility *= maxVisibility;
						visibility = saturate(visibility + blueNoise * 0.025);

						half4 waterCol = lerp(underwaterFarCol, underwaterNearCol, visibility);
						half4 finalCol = lerp(waterCol, bgCol, visibility);
						
						return finalCol;
					}
				}

				return originalCol;
			}
			ENDHLSL
		}
	}
}