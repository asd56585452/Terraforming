Shader "Custom/Terrain_URP"
{
    Properties
    {
        _RockInnerShallow ("Rock Inner Shallow", Color) = (1,1,1,1)
        _RockInnerDeep ("Rock Inner Deep", Color) = (1,1,1,1)
        _RockLight ("Rock Light", Color) = (1,1,1,1)
        _RockDark ("Rock Dark", Color) = (1,1,1,1)
        _GrassLight ("Grass Light", Color) = (1,1,1,1)
        _GrassDark ("Grass Dark", Color) = (1,1,1,1)

        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Test ("Test", Float) = 0.0

        _NoiseTex("Noise Texture", 2D) = "White" {}
        _DensityTex("Density Texture", 3D) = "white" {}
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            // URP lighting features
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTERED_RENDERING

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            // DensityTex is set via material.SetTexture("DensityTex", ...) in GenTest.cs
            // In URP, we need to declare it with the underscore prefix for TEXTURE3D
            TEXTURE3D(_DensityTex);
            SAMPLER(sampler_DensityTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _RockInnerShallow;
                float4 _RockInnerDeep;
                float4 _RockLight;
                float4 _RockDark;
                float4 _GrassLight;
                float4 _GrassDark;
                float _Glossiness;
                float _Test;
                float3 planetBoundsSize;
                float oceanRadius;
            CBUFFER_END
            
            // Density texture is set via material.SetTexture, so we declare it separately
            // Note: In URP, textures set via SetTexture need to be declared as TEXTURE3D
            // The actual texture name in code is "DensityTex" but shader property should match

            float4 triplanarOffset(float3 vertPos, float3 normal, float3 scale, float2 offset)
            {
                float3 scaledPos = vertPos / scale;
                float4 colX = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, scaledPos.zy + offset);
                float4 colY = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, scaledPos.xz + offset);
                float4 colZ = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, scaledPos.xy + offset);
                
                // Square normal to make all values positive + increase blend sharpness
                float3 blendWeight = normal * normal;
                // Divide blend weight by the sum of its components. This will make x + y + z = 1
                blendWeight /= dot(blendWeight, 1.0);
                return colX * blendWeight.x + colY * blendWeight.y + colZ * blendWeight.z;
            }

            float3 worldToTexPos(float3 worldPos)
            {
                return worldPos / planetBoundsSize + 0.5;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample density texture
                // Note: GenTest.cs sets texture as "DensityTex", but URP shader property is "_DensityTex"
                // We need to use the shader property name here
                float3 t = worldToTexPos(input.positionWS);
                float density = SAMPLE_TEXTURE3D(_DensityTex, sampler_DensityTex, t).r;
                
                // Calculate steepness (0 = flat, 0.5 = vertical, 1 = flat but upside down)
                float steepness = 1.0 - input.normalWS.y;
                float dstFromCentre = length(input.positionWS);

                // Sample noise textures
                float4 noise = triplanarOffset(input.positionWS, input.normalWS, 30.0, 0.0);
                float4 noise2 = triplanarOffset(input.positionWS, input.normalWS, 50.0, 0.0);

                float metallic = 0.0;
                float rockMetalStrength = 0.4;
                
                float3 albedo;
                float threshold = 0.005;
                
                if (density < -threshold)
                {
                    // Rock inner (cave interior)
                    float rockDepthT = saturate(abs(density + threshold) * 20.0);
                    albedo = lerp(_RockInnerShallow.rgb, _RockInnerDeep.rgb, rockDepthT);
                    metallic = lerp(rockMetalStrength, 1.0, rockDepthT);
                }
                else
                {
                    // Surface (grass/rock blend)
                    float4 grassCol = lerp(_GrassLight, _GrassDark, noise.r);
                    int r = 10;
                    float4 rockCol = lerp(_RockLight, _RockDark, (int)(noise2.r * r) / (float)r);
                    float n = (noise.r - 0.4) * _Test;

                    float rockWeight = smoothstep(0.24 + n, 0.24 + 0.001 + n, steepness);
                    albedo = lerp(grassCol.rgb, rockCol.rgb, rockWeight);
                    metallic = lerp(0.0, rockMetalStrength, rockWeight);
                }

                // Normalize normal for lighting calculations
                float3 normalWS = normalize(input.normalWS);
                
                // Create surface data for URP - pass unlit albedo (UniversalFragmentPBR will apply lighting)
                SurfaceData surfaceData;
                surfaceData.albedo = albedo * _Color.rgb;
                surfaceData.metallic = metallic;
                surfaceData.specular = half3(0.0, 0.0, 0.0);
                surfaceData.smoothness = _Glossiness;
                surfaceData.normalTS = half3(0.0, 0.0, 1.0);
                surfaceData.emission = half3(0.0, 0.0, 0.0);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;
                surfaceData.clearCoatMask = 0.0;
                surfaceData.clearCoatSmoothness = 0.0;

                InputData inputData;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                // Get shadow coordinates for proper shadow casting/receiving
                #if defined(_MAIN_LIGHT_SHADOWS) && !defined(_RECEIVE_SHADOWS_OFF)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                inputData.fogCoord = ComputeFogFactor(input.positionCS.z);
                inputData.vertexLighting = half3(0.0, 0.0, 0.0);
                inputData.bakedGI = SAMPLE_GI(input.positionWS, normalWS, inputData.shadowCoord);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
        
        // Shadow pass - simplified to avoid URP version issues
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma target 3.0
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float4 positionCS = TransformWorldToHClip(positionWS);

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}
