using System.Collections;
using UnityEngine;

namespace MyGame
{
    public enum ProceduralParticleEffectKind
    {
        GatlingHit = 0,
        MissileHit = 1,
        MissileSmoke = 2,
        MuzzleFlash = 3,
        CannonHit = 4,
        CannonSmoke = 5,
        DeathExplosion = 6,
        MissileLaunchSmoke = 7,
        AirModeTransition = 8
    }

    public class ProceduralParticleEffect : MonoBehaviour
    {
        [SerializeField] private ProceduralParticleEffectKind effectKind;
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private float destroyDelay = 0.5f;

        private bool m_built;
        private Coroutine m_releaseCoroutine;
        private static Texture2D s_softCircleTexture;

        private void Awake()
        {
            BuildEffect();
        }

        private void OnEnable()
        {
            BuildEffect();
            if (playOnAwake)
                Play();

            if (destroyDelay > 0f)
                m_releaseCoroutine = StartCoroutine(ReleaseAfterDelay());
        }

        private void OnDisable()
        {
            if (m_releaseCoroutine != null)
            {
                StopCoroutine(m_releaseCoroutine);
                m_releaseCoroutine = null;
            }

            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Play()
        {
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Clear(true);
                systems[i].Play(true);
            }
        }

        private IEnumerator ReleaseAfterDelay()
        {
            yield return new WaitForSeconds(destroyDelay);
            m_releaseCoroutine = null;

            if (!RuntimeObjectPool.TryRelease(gameObject))
                Destroy(gameObject);
        }

        private void BuildEffect()
        {
            if (m_built) return;
            m_built = true;

            switch (effectKind)
            {
                case ProceduralParticleEffectKind.GatlingHit:
                    CreateFlash("Flash", new Color(1f, 0.9f, 0.42f, 0.75f), 0.12f, 0.34f, 2);
                    CreateSparks("Sparks", new Color(1f, 0.62f, 0.08f, 1f), 20, 0.22f, 1.25f, 0.075f);
                    CreateSmoke("ImpactSmoke", false, 0.35f, 0.45f, 10f, 0.55f, new Color(0.72f, 0.68f, 0.58f, 0.42f));
                    break;
                case ProceduralParticleEffectKind.MissileHit:
                    CreateFlash("ExplosionCore", new Color(1f, 0.78f, 0.2f, 0.72f), 0.13f, 1.1f, 6);
                    CreateFireballs("ExplosionHotFireballs", new Color(1f, 0.72f, 0.12f, 0.88f), new Color(0.9f, 0.25f, 0.03f, 0.78f), 20, 0.38f, 2.7f, 0.95f, 0.75f);
                    CreateFireballs("ExplosionOuterFireballs", new Color(0.95f, 0.3f, 0.04f, 0.72f), new Color(0.35f, 0.05f, 0.02f, 0.62f), 18, 0.55f, 2.4f, 1.2f, 1.05f);
                    CreateSparks("ExplosionSparks", new Color(1f, 0.62f, 0.08f, 0.9f), 52, 0.22f, 4.2f, 0.11f);
                    CreateSmoke("ExplosionSmoke", false, 2.8f, 13.0f, 32f, 1.8f, new Color(0.22f, 0.21f, 0.2f, 0.4f));
                    break;
                case ProceduralParticleEffectKind.MissileSmoke:
                    CreateThrustCone("ThrustGlow", new Color(1f, 0.25f, 0.02f, 0.72f), 2.25f, 1.18f, 0.92f, 18);
                    CreateThrustCone("ThrustCore", new Color(1f, 0.86f, 0.22f, 0.92f), 1.35f, 0.4f, 0.94f, 14);
                    CreateSmoke("SmokeTrail", true, 0.7f, 1.15f, 55f, 0.45f, new Color(0.48f, 0.48f, 0.46f, 0.62f));
                    break;
                case ProceduralParticleEffectKind.MissileLaunchSmoke:
                    CreateFlash("LaunchFlash", new Color(1f, 0.72f, 0.24f, 0.55f), 0.08f, 0.5f, 2);
                    CreateSparks("LaunchSparks", new Color(1f, 0.6f, 0.1f, 0.9f), 18, 0.16f, 2.2f, 0.06f);
                    ParticleSystem launchSmoke = CreateSmoke("LaunchSmoke", false, 1.25f, 3.15f, 32f, 2.15f, new Color(0.42f, 0.4f, 0.36f, 0.78f), 64f, 0.5f);
                    launchSmoke.transform.localRotation = Quaternion.LookRotation(Vector3.back);
                    break;
                case ProceduralParticleEffectKind.MuzzleFlash:
                    CreateFlash("MuzzleFlash", new Color(1f, 0.82f, 0.32f, 1f), 0.055f, 0.32f, 1);
                    CreateSparks("MuzzleSparks", new Color(1f, 0.52f, 0.08f, 1f), 8, 0.09f, 1.1f, 0.035f);
                    break;
                case ProceduralParticleEffectKind.CannonHit:
                    CreateFlash("CannonFlash", new Color(1f, 0.72f, 0.22f, 0.78f), 0.18f, 0.72f, 2);
                    CreateSparks("CannonSparks", new Color(1f, 0.55f, 0.06f, 1f), 36, 0.36f, 2.45f, 0.1f);
                    CreateSmoke("CannonImpactSmoke", false, 0.55f, 0.85f, 22f, 0.95f, new Color(0.7f, 0.66f, 0.58f, 0.5f));
                    break;
                case ProceduralParticleEffectKind.CannonSmoke:
                    ParticleSystem cannonSmoke = CreateSmoke("CannonSmoke", false, 1.2f, 0.95f, 22f, 1.15f, new Color(0.92f, 0.92f, 0.88f, 0.5f));
                    cannonSmoke.transform.rotation = Quaternion.LookRotation(Vector3.up);
                    break;
                case ProceduralParticleEffectKind.DeathExplosion:
                    CreateFlash("ExplosionCore", new Color(1f, 0.9f, 0.28f, 0.95f), 0.28f, 4.8f, 14);
                    CreateFireballs("ExplosionHotFireballs", new Color(1f, 0.92f, 0.18f, 0.95f), new Color(1f, 0.42f, 0.04f, 0.9f), 42, 0.85f, 7.2f, 2.7f, 2.2f);
                    CreateFireballs("ExplosionOuterFireballs", new Color(1f, 0.36f, 0.04f, 0.9f), new Color(0.5f, 0.08f, 0.02f, 0.75f), 56, 1.2f, 8.6f, 3.8f, 4.2f);
                    CreateSparks("ExplosionSparks", new Color(1f, 0.78f, 0.16f, 1f), 180, 0.75f, 12f, 0.22f);
                    CreateSmoke("ExplosionSmoke", false, 4.2f, 36f, 120f, 5.8f, new Color(0.24f, 0.22f, 0.2f, 0.25f));
                    break;
                case ProceduralParticleEffectKind.AirModeTransition:
                    ParticleSystem blueWhiteSmoke = CreateSmoke("BlueWhiteSmoke", false, 1.25f, 15f, 85f, 2.4f, new Color(0.52f, 0.42f, 1f, 0.72f), 52f, 0.8f);
                    blueWhiteSmoke.transform.localRotation = Quaternion.LookRotation(Vector3.up);

                    ParticleSystem outerMist = CreateSmoke( "OuterWhiteMist", false, 1.55f, 21f, 68f, 1.5f, new Color(0.82f, 0.68f, 1f, 0.5f), 78f, 1.4f);
                    outerMist.transform.localRotation = Quaternion.LookRotation(Vector3.up);
                    break;
            }
        }

        private ParticleSystem CreateBaseSystem(string objectName, Material material)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(transform, false);

            ParticleSystem particleSystem = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;

            return particleSystem;
        }

        private void CreateFlash(string objectName, Color color, float lifetime, float size, short burstCount)
        {
            ParticleSystem particleSystem = CreateBaseSystem(objectName, CreateParticleMaterial(color, true));

            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = lifetime;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, burstCount)
            });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;
        }

        private void CreateSparks(string objectName, Color color, short count, float lifetime, float speed, float size)
        {
            ParticleSystem particleSystem = CreateBaseSystem(objectName, CreateParticleMaterial(color, true));

            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = lifetime;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.45f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.8f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, count)
            });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.04f;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.5f;
            renderer.velocityScale = 0.7f;
        }

        private void CreateFireballs(
            string objectName,
            Color innerColor,
            Color outerColor,
            short count,
            float lifetime,
            float speed,
            float size,
            float radius)
        {
            ParticleSystem particleSystem = CreateBaseSystem(objectName, CreateParticleMaterial(outerColor, true));

            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = lifetime;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.45f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.25f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size * 1.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                innerColor,
                outerColor);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, count)
            });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.minParticleSize = 0.02f;
            renderer.maxParticleSize = 1.8f;
        }

        private void CreateThrustCone(
            string objectName,
            Color color,
            float length,
            float radius,
            float localBackOffset,
            int segments)
        {
            segments = Mathf.Max(8, segments);

            GameObject child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.back * localBackOffset;

            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 6];
            vertices[0] = Vector3.zero;
            vertices[segments + 1] = Vector3.back * length;

            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f);
            }

            int triangleIndex = 0;
            for (int i = 0; i < segments; i++)
            {
                int current = i + 1;
                int next = i == segments - 1 ? 1 : current + 1;

                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current;

                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = segments + 1;
            }

            Mesh mesh = new Mesh();
            mesh.name = objectName + "Mesh";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter meshFilter = child.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.material = CreateUnlitMaterial(color);
        }

        private ParticleSystem CreateSmoke(
            string objectName,
            bool loop,
            float lifetime,
            float size,
            float rate,
            float speed,
            Color smokeColor,
            float angle = 18f,
            float radius = 0.08f)
        {
            ParticleSystem particleSystem = CreateBaseSystem(objectName, CreateParticleMaterial(smokeColor, false));

            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.loop = loop;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.65f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size);
            main.startColor = smokeColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
                new Gradient
                {
                    colorKeys = new[]
                    {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(Color.white, 1f)
                    },
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, 0.12f),
                        new GradientAlphaKey(0f, 1f)
                    }
                });

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = rate;
            if (!loop)
            {
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(rate))
                });
                emission.rateOverTime = 0f;
            }

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.minParticleSize = 0.02f;
            renderer.maxParticleSize = 1.5f;

            return particleSystem;
        }

        private Material CreateUnlitMaterial(Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogError("[ProceduralParticleEffect] Required shader 'Unlit/Color' was not found.", this);
                return null;
            }

            Material material = new Material(shader);
            material.color = color;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color);

            return material;
        }

        private Material CreateParticleMaterial(Color color, bool additive)
        {
            Shader shader = additive
                ? Shader.Find("Particles/Standard Unlit")
                : Shader.Find("Sprites/Default");
            if (shader == null)
            {
                string shaderName = additive
                    ? "Particles/Standard Unlit"
                    : "Sprites/Default";
                Debug.LogError(
                    $"[ProceduralParticleEffect] Required shader '{shaderName}' was not found.",
                    this);
                return null;
            }

            Material material = new Material(shader);
            material.color = color;

            if (material.HasProperty("_MainTex"))
                material.mainTexture = GetSoftCircleTexture();

            if (additive && material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 2f);

            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color);

            return material;
        }

        private Texture2D GetSoftCircleTexture()
        {
            if (s_softCircleTexture != null)
                return s_softCircleTexture;

            const int textureSize = 64;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.name = "ProceduralSoftCircleSmoke";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (textureSize - 1) * 0.5f;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f - Mathf.SmoothStep(0.2f, 1f, distance);
                    alpha *= alpha;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            s_softCircleTexture = texture;
            return s_softCircleTexture;
        }
    }
}
