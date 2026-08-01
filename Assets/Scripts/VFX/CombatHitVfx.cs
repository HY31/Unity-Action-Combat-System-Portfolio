using UnityEngine;

public sealed class CombatHitVfx : MonoBehaviour
{
    private const string ShaderResourcePath = "VFX/CombatHitAdditive";
    private const int TextureSize = 64;

    private static CombatHitVfx instance;

    private ParticleSystem flashParticles;
    private ParticleSystem ringParticles;
    private ParticleSystem sparkParticles;

    private Material flashMaterial;
    private Material ringMaterial;
    private Material sparkMaterial;

    private Texture2D flashTexture;
    private Texture2D ringTexture;
    private Texture2D sparkTexture;

    private uint randomState = 0x6D2B79F5u;
    private bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    public static void Play(Vector3 position, Vector3 travelDirection, CombatElement element)
    {
        if (instance == null)
        {
            instance = FindFirstObjectByType<CombatHitVfx>();

            if (instance == null)
            {
                GameObject runtimeRoot = new GameObject("Combat Hit VFX (Runtime)");
                instance = runtimeRoot.AddComponent<CombatHitVfx>();
            }
        }

        instance.EmitHit(position, travelDirection, element);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        DestroyRuntimeObject(flashMaterial);
        DestroyRuntimeObject(ringMaterial);
        DestroyRuntimeObject(sparkMaterial);
        DestroyRuntimeObject(flashTexture);
        DestroyRuntimeObject(ringTexture);
        DestroyRuntimeObject(sparkTexture);
    }

    private void EmitHit(Vector3 position, Vector3 travelDirection, CombatElement element)
    {
        EnsureInitialized();

        if (!initialized)
            return;

        Vector3 direction = travelDirection.sqrMagnitude > 0.0001f
            ? travelDirection.normalized
            : Vector3.forward;

        Color elementColor = ResolveElementColor(element);
        Color coreColor = Color.Lerp(Color.white, elementColor, 0.25f);

        EmitParticle(flashParticles, position, Vector3.zero, coreColor, 1.2f, 0.09f);
        EmitParticle(flashParticles, position, Vector3.zero, elementColor, 1.8f, 0.13f);
        EmitParticle(ringParticles, position, Vector3.zero, elementColor, 2.1f, 0.18f);

        for (int i = 0; i < 14; i++)
        {
            // 전투 AI가 사용하는 UnityEngine.Random 상태를 건드리지 않도록 VFX 전용 난수를 사용한다.
            Vector3 spread = NextUnitVector();
            Vector3 sparkDirection = (spread + direction * 0.4f).normalized;
            float speed = Mathf.Lerp(4.5f, 10.5f, NextFloat());
            float size = Mathf.Lerp(0.035f, 0.075f, NextFloat());
            float lifetime = Mathf.Lerp(0.1f, 0.22f, NextFloat());

            EmitParticle(
                sparkParticles,
                position,
                sparkDirection * speed,
                Color.Lerp(Color.white, elementColor, NextFloat() * 0.75f),
                size,
                lifetime);
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        Shader shader = Resources.Load<Shader>(ShaderResourcePath);

        if (shader == null)
        {
            Debug.LogError($"CombatHitVfx: Resources/{ShaderResourcePath} 셰이더를 찾을 수 없습니다.");
            return;
        }

        flashTexture = CreateFlashTexture();
        ringTexture = CreateRingTexture();
        sparkTexture = CreateSparkTexture();

        flashMaterial = CreateMaterial(shader, flashTexture, "Combat Hit Flash Material");
        ringMaterial = CreateMaterial(shader, ringTexture, "Combat Hit Ring Material");
        sparkMaterial = CreateMaterial(shader, sparkTexture, "Combat Hit Spark Material");

        flashParticles = CreateFlashParticles(flashMaterial);
        ringParticles = CreateRingParticles(ringMaterial);
        sparkParticles = CreateSparkParticles(sparkMaterial);
        initialized = true;
    }

    private ParticleSystem CreateFlashParticles(Material material)
    {
        ParticleSystem particles = CreateParticleSystem("Flash", 32, material);

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.12f, 1.08f),
                new Keyframe(1f, 0f)));

        ConfigureFadeOut(particles, 0.35f);
        return particles;
    }

    private ParticleSystem CreateRingParticles(Material material)
    {
        ParticleSystem particles = CreateParticleSystem("Ring", 24, material);

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.15f),
                new Keyframe(0.7f, 1f),
                new Keyframe(1f, 1.15f)));

        ConfigureFadeOut(particles, 0.1f);
        return particles;
    }

    private ParticleSystem CreateSparkParticles(Material material)
    {
        ParticleSystem particles = CreateParticleSystem("Sparks", 256, material);
        var main = particles.main;
        main.gravityModifier = 0.2f;

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.65f, 0.65f),
                new Keyframe(1f, 0f)));

        ConfigureFadeOut(particles, 0.55f);

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.velocityScale = 0.08f;
        particleRenderer.lengthScale = 2.4f;
        return particles;
    }

    private ParticleSystem CreateParticleSystem(string objectName, int maxParticles, Material material)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);

        ParticleSystem particles = child.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.startSpeed = 0f;
        main.startLifetime = 0.2f;
        main.startSize = 1f;
        main.startColor = Color.white;

        var emission = particles.emission;
        emission.enabled = false;

        var shape = particles.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer = child.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = 20;

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    private static void ConfigureFadeOut(ParticleSystem particles, float holdPoint)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, holdPoint),
                new GradientAlphaKey(0f, 1f)
            });

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void EmitParticle(
        ParticleSystem particles,
        Vector3 position,
        Vector3 velocity,
        Color color,
        float size,
        float lifetime)
    {
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startColor = color,
            startSize = size,
            startLifetime = lifetime
        };

        particles.Emit(emitParams, 1);
    }

    private static Material CreateMaterial(Shader shader, Texture texture, string materialName)
    {
        Material material = new Material(shader)
        {
            name = materialName,
            mainTexture = texture,
            hideFlags = HideFlags.HideAndDontSave
        };

        return material;
    }

    private static Texture2D CreateFlashTexture()
    {
        return CreateTexture("Combat Hit Flash Texture", (x, y, radius) =>
        {
            if (radius >= 1f)
                return 0f;

            float angle = Mathf.Atan2(y, x);
            float crossBeam = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 18f);
            float center = Mathf.Pow(1f - radius, 4f);
            float beamFalloff = crossBeam * Mathf.Pow(1f - radius, 1.5f);
            return Mathf.Clamp01(center + beamFalloff);
        });
    }

    private static Texture2D CreateRingTexture()
    {
        return CreateTexture("Combat Hit Ring Texture", (x, y, radius) =>
        {
            if (radius >= 1f)
                return 0f;

            float ringDistance = Mathf.Abs(radius - 0.62f);
            float ring = 1f - Mathf.Clamp01(ringDistance / 0.11f);
            return ring * ring * Mathf.Clamp01((1f - radius) * 4f);
        });
    }

    private static Texture2D CreateSparkTexture()
    {
        return CreateTexture("Combat Hit Spark Texture", (x, y, radius) =>
        {
            if (radius >= 1f)
                return 0f;

            return Mathf.Pow(1f - radius, 2.5f);
        });
    }

    private static Texture2D CreateTexture(string textureName, System.Func<float, float, float, float> alphaResolver)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, true)
        {
            name = textureName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float normalizedX = ((x + 0.5f) / TextureSize) * 2f - 1f;
                float normalizedY = ((y + 0.5f) / TextureSize) * 2f - 1f;
                float radius = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                float alpha = alphaResolver(normalizedX, normalizedY, radius);
                pixels[y * TextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private float NextFloat()
    {
        randomState ^= randomState << 13;
        randomState ^= randomState >> 17;
        randomState ^= randomState << 5;
        return (randomState & 0x00FFFFFFu) / 16777216f;
    }

    private Vector3 NextUnitVector()
    {
        float z = NextFloat() * 2f - 1f;
        float angle = NextFloat() * Mathf.PI * 2f;
        float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
        return new Vector3(radius * Mathf.Cos(angle), z, radius * Mathf.Sin(angle));
    }

    private static Color ResolveElementColor(CombatElement element)
    {
        return element switch
        {
            CombatElement.Fire => new Color(1f, 0.24f, 0.04f, 1f),
            CombatElement.Ice => new Color(0.25f, 0.9f, 1f, 1f),
            CombatElement.Physical => new Color(1f, 0.62f, 0.12f, 1f),
            CombatElement.Electric => new Color(0.62f, 0.35f, 1f, 1f),
            CombatElement.Wind => new Color(0.32f, 1f, 0.62f, 1f),
            CombatElement.Ether => new Color(0.95f, 0.25f, 1f, 1f),
            _ => new Color(1f, 0.72f, 0.22f, 1f)
        };
    }

    private static void DestroyRuntimeObject(Object runtimeObject)
    {
        if (runtimeObject != null)
            Destroy(runtimeObject);
    }
}
