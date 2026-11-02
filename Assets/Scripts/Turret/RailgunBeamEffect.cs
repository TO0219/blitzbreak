using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    public sealed class RailgunBeamEffect : MonoBehaviour
    {
        private const float DefaultDuration = 1f;
        private const int ElectricArcCount = 5;
        private const int ElectricArcPointCount = 9;

        private LineRenderer m_outerBeam;
        private LineRenderer m_coreBeam;
        private readonly List<LineRenderer> m_electricArcs = new();
        private Renderer m_muzzleFlash;
        private Renderer m_impactFlash;
        private Material m_outerMaterial;
        private Material m_coreMaterial;
        private Material m_electricMaterial;
        private Material m_flashMaterial;
        private Vector3 m_muzzleBaseScale;
        private Vector3 m_impactBaseScale;

        public static void Play(Vector3 start, Vector3 end, float duration = DefaultDuration)
        {
            GameObject effect = new GameObject("RailgunBeamEffect");
            RailgunBeamEffect beam = effect.AddComponent<RailgunBeamEffect>();
            beam.Initialize(start, end, duration);
        }

        private void Initialize(Vector3 start, Vector3 end, float duration)
        {
            Vector3 beamEnd = Vector3.Distance(start, end) > 0.1f
                ? end
                : start + Vector3.forward;

            m_outerMaterial = CreateEffectMaterial(new Color(0.14f, 0.86f, 1f, 0.78f));
            m_coreMaterial = CreateEffectMaterial(new Color(1f, 1f, 1f, 1f));
            m_electricMaterial = CreateEffectMaterial(new Color(0.64f, 0.94f, 1f, 1f));
            m_flashMaterial = CreateEffectMaterial(new Color(0.46f, 0.95f, 1f, 0.92f));

            m_outerBeam = CreateBeamLine(
                "OuterBeam",
                start,
                beamEnd,
                1.6f,
                m_outerMaterial,
                new Color(0.12f, 0.72f, 1f, 0.14f),
                new Color(0.54f, 0.96f, 1f, 0.95f));
            m_coreBeam = CreateBeamLine(
                "CoreBeam",
                start,
                beamEnd,
                0.42f,
                m_coreMaterial,
                new Color(1f, 1f, 1f, 0.15f),
                Color.white);

            CreateElectricArcs(start, beamEnd);

            m_muzzleFlash = CreateFlash("MuzzleFlash", start, 1.35f);
            m_impactFlash = CreateFlash("ImpactFlash", beamEnd, 1.75f);

            if (m_muzzleFlash != null)
                m_muzzleBaseScale = m_muzzleFlash.transform.localScale;
            if (m_impactFlash != null)
                m_impactBaseScale = m_impactFlash.transform.localScale;

            StartCoroutine(FadeAndDestroy(Mathf.Max(0.02f, duration)));
        }

        private IEnumerator FadeAndDestroy(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = 1f - t;
                SetBeamWidth(m_outerBeam, Mathf.Lerp(1.6f, 0.12f, t));
                SetBeamWidth(m_coreBeam, Mathf.Lerp(0.42f, 0.035f, t));
                for (int i = 0; i < m_electricArcs.Count; i++)
                    SetBeamWidth(m_electricArcs[i], Mathf.Lerp(0.14f, 0.01f, t));

                SetMaterialAlpha(m_outerMaterial, 0.78f * alpha);
                SetMaterialAlpha(m_coreMaterial, alpha);
                SetMaterialAlpha(m_electricMaterial, alpha);
                SetMaterialAlpha(m_flashMaterial, 0.92f * alpha);

                if (m_muzzleFlash != null)
                    m_muzzleFlash.transform.localScale =
                        m_muzzleBaseScale * Mathf.Lerp(1f, 0.25f, t);
                if (m_impactFlash != null)
                    m_impactFlash.transform.localScale =
                        m_impactBaseScale * Mathf.Lerp(1f, 0.15f, t);

                yield return null;
            }

            Destroy(gameObject);
        }

        private LineRenderer CreateBeamLine(
            string name,
            Vector3 start,
            Vector3 end,
            float width,
            Material material,
            Color edgeColor,
            Color centerColor)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = width;
            line.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f);
            line.numCapVertices = 8;
            line.numCornerVertices = 2;
            line.material = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(edgeColor, 0f),
                    new GradientColorKey(centerColor, 0.5f),
                    new GradientColorKey(edgeColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(1f, 0.9f),
                    new GradientAlphaKey(0f, 1f)
                });
            line.colorGradient = gradient;
            return line;
        }

        private void CreateElectricArcs(Vector3 start, Vector3 end)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length <= 0.1f)
                return;

            Vector3 forward = direction / length;
            Vector3 reference = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.92f
                ? Vector3.right
                : Vector3.up;
            Vector3 right = Vector3.Cross(reference, forward).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            float arcRadius = Mathf.Clamp(length * 0.035f, 0.55f, 2.2f);

            int seed = Mathf.RoundToInt(
                start.sqrMagnitude * 17f +
                end.sqrMagnitude * 31f +
                Time.frameCount * 13f);
            System.Random random = new System.Random(seed);

            for (int arcIndex = 0; arcIndex < ElectricArcCount; arcIndex++)
            {
                GameObject arcObject = new GameObject("ElectricArc");
                arcObject.transform.SetParent(transform, false);
                LineRenderer arc = arcObject.AddComponent<LineRenderer>();
                arc.positionCount = ElectricArcPointCount;
                arc.useWorldSpace = true;
                arc.alignment = LineAlignment.View;
                arc.textureMode = LineTextureMode.Stretch;
                arc.widthMultiplier = 0.14f;
                arc.numCapVertices = 4;
                arc.numCornerVertices = 2;
                arc.material = m_electricMaterial;
                arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arc.receiveShadows = false;

                float phase = arcIndex / (float)ElectricArcCount * Mathf.PI * 2f;
                for (int pointIndex = 0; pointIndex < ElectricArcPointCount; pointIndex++)
                {
                    float t = pointIndex / (float)(ElectricArcPointCount - 1);
                    float taper = Mathf.Sin(t * Mathf.PI);
                    float angle = phase + t * Mathf.PI * 5f;
                    float noiseA = ((float)random.NextDouble() - 0.5f) * arcRadius;
                    float noiseB = ((float)random.NextDouble() - 0.5f) * arcRadius;
                    Vector3 offset =
                        right * (Mathf.Cos(angle) * arcRadius + noiseA) +
                        up * (Mathf.Sin(angle) * arcRadius + noiseB);
                    arc.SetPosition(pointIndex, Vector3.Lerp(start, end, t) + offset * taper);
                }

                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(0.18f, 0.72f, 1f), 0f),
                        new GradientColorKey(Color.white, 0.5f),
                        new GradientColorKey(new Color(0.18f, 0.72f, 1f), 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, 0.15f),
                        new GradientAlphaKey(1f, 0.85f),
                        new GradientAlphaKey(0f, 1f)
                    });
                arc.colorGradient = gradient;
                m_electricArcs.Add(arc);
            }
        }

        private Renderer CreateFlash(string name, Vector3 position, float size)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = name;
            flash.transform.SetParent(transform, false);
            flash.transform.position = position;
            flash.transform.localScale = Vector3.one * size;

            Collider collider = flash.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = flash.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = m_flashMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return renderer;
        }

        private static Material CreateEffectMaterial(Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogError("[RailgunBeamEffect] Required shader 'Unlit/Color' was not found.");
                return null;
            }

            Material material = new Material(shader);
            SetMaterialColor(material, color);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.5f);
            }
        }

        private static void SetMaterialAlpha(Material material, float alpha)
        {
            if (material == null)
                return;

            Color color = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color")
                    ? material.GetColor("_Color")
                    : Color.white;
            color.a = Mathf.Clamp01(alpha);
            SetMaterialColor(material, color);
        }

        private static void SetBeamWidth(LineRenderer line, float width)
        {
            if (line != null)
                line.widthMultiplier = Mathf.Max(0.001f, width);
        }
    }
}
