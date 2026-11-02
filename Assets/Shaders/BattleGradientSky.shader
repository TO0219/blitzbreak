Shader "Skybox/Battle Gradient Sky"
{
    Properties
    {
        _ZenithColor ("Zenith Color", Color) = (0.035, 0.12, 0.28, 1)
        _UpperColor ("Upper Sky Color", Color) = (0.08, 0.35, 0.58, 1)
        _HorizonColor ("Horizon Color", Color) = (0.46, 0.82, 0.88, 1)
        _GroundColor ("Lower Sky Color", Color) = (0.12, 0.2, 0.25, 1)
        _CloudColor ("Cloud Color", Color) = (0.82, 0.94, 0.96, 1)
        _CloudStrength ("Cloud Strength", Range(0, 0.8)) = 0.14
        _SunColor ("Sun Color", Color) = (1, 0.82, 0.55, 1)
        _SunSize ("Sun Size", Range(0.001, 0.08)) = 0.018
        _Exposure ("Exposure", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float4 _ZenithColor;
            float4 _UpperColor;
            float4 _HorizonColor;
            float4 _GroundColor;
            float4 _CloudColor;
            float _CloudStrength;
            float4 _SunColor;
            float _SunSize;
            float _Exposure;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.55;
                for (int i = 0; i < 5; i++)
                {
                    value += Noise(p) * amplitude;
                    p = p * 2.04 + float2(17.2, 8.3);
                    amplitude *= 0.5;
                }

                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = input.vertex.xyz;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float height = saturate(direction.y);
                float lower = saturate(-direction.y);

                float3 upperSky = lerp(
                    _UpperColor.rgb,
                    _ZenithColor.rgb,
                    smoothstep(0.08, 0.92, height));

                float horizonBlend = exp(-abs(direction.y) * 7.5);
                float3 color = lerp(upperSky, _HorizonColor.rgb, horizonBlend);
                color = lerp(color, _GroundColor.rgb, smoothstep(0.02, 0.65, lower));

                float3 sunDirection = normalize(float3(-0.35, 0.48, -0.8));
                float sunDot = saturate(dot(direction, sunDirection));
                float sunDisk = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.35, sunDot);
                float sunGlow = pow(sunDot, 96.0) * 0.28;

                float2 projectedUv = direction.xz / max(direction.y + 0.26, 0.12);
                projectedUv += float2(4.7, -2.1);
                float broadNoise = Fbm(projectedUv * 1.15);
                float detailNoise = Fbm(projectedUv * 3.1 + 31.7);

                float horizonCloudBand =
                    smoothstep(-0.02, 0.07, direction.y) *
                    (1.0 - smoothstep(0.26, 0.48, direction.y));
                float cloudMass = broadNoise * 0.78 + detailNoise * 0.22;
                float cloudMask = smoothstep(0.42, 0.74, cloudMass) * horizonCloudBand;

                float thinCloudBand =
                    smoothstep(0.08, 0.22, direction.y) *
                    (1.0 - smoothstep(0.5, 0.8, direction.y));
                float wisps = smoothstep(0.56, 0.76, Fbm(projectedUv * float2(2.3, 0.72) + 12.4)) *
                    thinCloudBand * 0.32;

                float clouds = saturate((cloudMask + wisps) * _CloudStrength);
                float3 cloudLitColor = lerp(_CloudColor.rgb * 0.58, _CloudColor.rgb * 0.92, saturate(direction.y * 2.0 + 0.25));
                cloudLitColor += _SunColor.rgb * pow(sunDot, 10.0) * 0.16;
                color = lerp(color, cloudLitColor, clouds);

                float atmosphericGlow = horizonBlend * 0.08 + pow(sunDot, 18.0) * 0.12;
                color += _SunColor.rgb * (sunDisk + sunGlow + atmosphericGlow);

                return float4(color * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
