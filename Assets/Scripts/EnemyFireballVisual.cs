using UnityEngine;

[DisallowMultipleComponent]
public class EnemyFireballVisual : MonoBehaviour
{
    [SerializeField] private float coreRadius = 0.28f;
    [SerializeField] private Texture coreTexture = null;
    [SerializeField] private Texture trailTexture = null;
    [SerializeField] private Texture sparkTexture = null;
    [SerializeField] private Color coreColor = new Color(1f, 0.28f, 0.02f, 1f);
    [SerializeField] private Color flameColor = new Color(1f, 0.55f, 0.04f, 0.85f);
    [SerializeField] private Color sparkColor = new Color(1f, 0.9f, 0.25f, 1f);
    [SerializeField] private float emissionIntensity = 3f;
    [SerializeField] private float lightRange = 2.8f;
    [SerializeField] private float lightIntensity = 2f;

    private void Awake()
    {
        BuildCore();
        BuildFlameShell();
        BuildTrail();
        BuildSparks();
        BuildLight();
    }

    private void BuildCore()
    {
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Fireball Core";
        core.transform.SetParent(transform, false);
        core.transform.localScale = Vector3.one * (coreRadius * 1.35f);

        Collider coreCollider = core.GetComponent<Collider>();
        if (coreCollider != null)
            Destroy(coreCollider);

        MeshRenderer renderer = core.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateFireMaterial(coreColor, coreTexture, false);
    }

    private void BuildFlameShell()
    {
        ParticleSystem flames = CreateParticleSystem("Fireball Flames", coreTexture, flameColor);
        ParticleSystem.MainModule main = flames.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(coreRadius * 0.9f, coreRadius * 1.7f);
        main.startColor = new ParticleSystem.MinMaxGradient(flameColor, sparkColor);

        ParticleSystem.EmissionModule emission = flames.emission;
        emission.rateOverTime = 70f;

        ParticleSystem.ShapeModule shape = flames.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = coreRadius * 0.45f;

        flames.Play();
    }

    private void BuildTrail()
    {
        ParticleSystem trail = CreateParticleSystem("Fireball Trail", trailTexture, flameColor);
        ParticleSystem.MainModule main = trail.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(coreRadius * 0.45f, coreRadius * 1.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(flameColor, new Color(0.85f, 0.08f, 0f, 0.25f));

        ParticleSystem.EmissionModule emission = trail.emission;
        emission.rateOverTime = 90f;

        ParticleSystem.ShapeModule shape = trail.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = coreRadius * 0.35f;

        trail.Play();
    }

    private void BuildSparks()
    {
        ParticleSystem sparks = CreateParticleSystem("Fireball Sparks", sparkTexture, sparkColor);
        ParticleSystem.MainModule main = sparks.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(coreRadius * 0.12f, coreRadius * 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(sparkColor, new Color(1f, 0.35f, 0.04f, 0.2f));

        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.rateOverTime = 18f;

        ParticleSystem.ShapeModule shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = coreRadius * 0.55f;

        sparks.Play();
    }

    private ParticleSystem CreateParticleSystem(string objectName, Texture texture, Color fallbackColor)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(transform, false);

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = CreateFireMaterial(fallbackColor, texture, true);
        }

        return particles;
    }

    private void BuildLight()
    {
        Light fireLight = gameObject.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = flameColor;
        fireLight.range = lightRange;
        fireLight.intensity = lightIntensity;
    }

    private Material CreateFireMaterial(Color color, Texture texture, bool transparent)
    {
        Shader shader = FindCompatibleShader();

        Material material = new Material(shader);
        material.name = "Runtime Fireball Visual Material";
        material.renderQueue = transparent ? 3000 : -1;

        if (transparent)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            SetFloatIfPresent(material, "_SurfaceType", 1f);
            SetFloatIfPresent(material, "_BlendMode", 0f);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            SetFloatIfPresent(material, "_AlphaCutoffEnable", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_BLENDMODE_ALPHA");
        }

        SetColorIfPresent(material, "_BaseColor", color);
        SetColorIfPresent(material, "_Color", color);
        SetColorIfPresent(material, "_UnlitColor", color);
        SetColorIfPresent(material, "_EmissiveColor", color * emissionIntensity);
        SetColorIfPresent(material, "_EmissiveColorLDR", color);
        SetColorIfPresent(material, "_EmissionColor", color * emissionIntensity);
        SetFloatIfPresent(material, "_EmissiveIntensity", emissionIntensity);
        SetFloatIfPresent(material, "_UseEmissiveIntensity", 1f);

        if (texture != null)
        {
            SetTextureIfPresent(material, "_BaseColorMap", texture);
            SetTextureIfPresent(material, "_MainTex", texture);
            SetTextureIfPresent(material, "_EmissiveColorMap", texture);
        }

        material.EnableKeyword("_EMISSION");
        return material;
    }

    private static Shader FindCompatibleShader()
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        return shader;
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, value);
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static void SetTextureIfPresent(Material material, string propertyName, Texture value)
    {
        if (material.HasProperty(propertyName))
            material.SetTexture(propertyName, value);
    }

    private void OnValidate()
    {
        coreRadius = Mathf.Max(0.05f, coreRadius);
        emissionIntensity = Mathf.Max(0f, emissionIntensity);
        lightRange = Mathf.Max(0f, lightRange);
        lightIntensity = Mathf.Max(0f, lightIntensity);
    }
}
