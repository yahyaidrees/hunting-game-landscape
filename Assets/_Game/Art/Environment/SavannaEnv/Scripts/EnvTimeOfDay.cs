using UnityEngine;
using UnityEngine.Rendering;

// Switches an environment scene between its lighting presets (day / night), in play or in the editor.
// Lives on the environment root. A switch only sets a few shader globals, the fog, the ambient colours and one
// light: nothing is loaded, created or rebuilt, so it costs nothing measurable and can happen at any moment.
//
// From game code:
//   EnvTimeOfDay.NextMode = EnvTimeOfDay.Mode.Night;          // before loading the scene: it starts at night
//   EnvTimeOfDay.Active?.SetMode(EnvTimeOfDay.Mode.Night);    // or switch while it is running
//
// Shader globals outlive scenes, so disabling this (e.g. unloading the scene) puts the shaders back to the day look.
[ExecuteAlways]
public class EnvTimeOfDay : MonoBehaviour
{
    public enum Mode { Day, Night }

    public Mode mode = Mode.Day;
    public EnvLightingPreset day;
    public EnvLightingPreset night;
    [Tooltip("The scene's directional light (sun by day, moonlight by night). Uses RenderSettings.sun when empty.")]
    public Light sun;

    // Set by game code before loading an environment scene; consumed by the next one that starts playing.
    public static Mode? NextMode;

    public static EnvTimeOfDay Active { get; private set; }
    public EnvLightingPreset Current => mode == Mode.Night ? night : day;

    static readonly int NightId = Shader.PropertyToID("_EnvNight");
    static readonly int GradeId = Shader.PropertyToID("_EnvGrade");
    static readonly int GradeParamsId = Shader.PropertyToID("_EnvGradeParams");
    static readonly int NearId = Shader.PropertyToID("_EnvNear");
    static readonly int MoonDirId = Shader.PropertyToID("_EnvMoonDir");
    static readonly int MoonColorId = Shader.PropertyToID("_EnvMoonColor");
    static readonly int SkyZenithId = Shader.PropertyToID("_EnvSkyZenith");
    static readonly int SkyHorizonId = Shader.PropertyToID("_EnvSkyHorizon");
    static readonly int HaloId = Shader.PropertyToID("_EnvHalo");
    static readonly int GlowId = Shader.PropertyToID("_EnvGlow");
    static readonly int SkyParamsId = Shader.PropertyToID("_EnvSkyParams");
    static readonly int DayLightId = Shader.PropertyToID("_EnvDayLight");
    static readonly int DayAmbSkyId = Shader.PropertyToID("_EnvDayAmbSky");
    static readonly int DayAmbEquatorId = Shader.PropertyToID("_EnvDayAmbEquator");
    static readonly int DayAmbGroundId = Shader.PropertyToID("_EnvDayAmbGround");

    public void SetMode(Mode newMode)
    {
        mode = newMode;
        Apply();
    }

    void OnEnable()
    {
        Active = this;
        if (Application.isPlaying && NextMode.HasValue) { mode = NextMode.Value; NextMode = null; }
        Apply();
    }

    void Start()
    {
        // the player's camera may not have existed at OnEnable
        ApplyCamera(Current, Camera.main);
    }

    void OnDisable()
    {
        if (Active == this) Active = null;
        Shader.SetGlobalFloat(NightId, 0f);
    }

    void OnValidate()
    {
        if (isActiveAndEnabled) Apply();
    }

    public void Apply()
    {
        var p = Current;
        if (!p) { Shader.SetGlobalFloat(NightId, 0f); return; }

        Shader.SetGlobalFloat(NightId, p.night);
        Shader.SetGlobalColor(GradeId, p.gradeTint);
        Shader.SetGlobalVector(GradeParamsId, new Vector4(p.desaturate, p.deepenShadows, p.backdropHaze, p.fogFromSky));
        Shader.SetGlobalVector(NearId, new Vector4(p.nearShade, 1f / Mathf.Max(1f, p.nearShadeDistance), 0f, 0f));
        var md = p.MoonDirection;
        Shader.SetGlobalVector(MoonDirId, new Vector4(md.x, md.y, md.z, p.moonRadius * Mathf.Deg2Rad));
        Shader.SetGlobalColor(MoonColorId, p.moonColor);
        Shader.SetGlobalColor(SkyZenithId, p.skyZenith);
        Shader.SetGlobalColor(SkyHorizonId, p.skyHorizon);
        Shader.SetGlobalColor(HaloId, p.haloColor);
        Shader.SetGlobalColor(GlowId, p.glowColor);
        Shader.SetGlobalVector(SkyParamsId, new Vector4(
            1f / (p.haloWidth * Mathf.Deg2Rad), 1f / (p.glowWidth * Mathf.Deg2Rad), p.glowSquash,
            1f / Mathf.Sin(p.skyGradientHeight * Mathf.Deg2Rad)));
        // shaders lit in real time keep the day lighting at night (EnvNight.cginc: ENV_SunColor, ENV_Ambient)
        var d = day ? day : p;
        Shader.SetGlobalColor(DayLightId, d.lightColor * d.lightIntensity);
        Shader.SetGlobalColor(DayAmbSkyId, d.ambientSky);
        Shader.SetGlobalColor(DayAmbEquatorId, d.ambientEquator);
        Shader.SetGlobalColor(DayAmbGroundId, d.ambientGround);

        RenderSettings.fogColor = p.fogColor;
        RenderSettings.fogStartDistance = p.fogStart;
        RenderSettings.fogEndDistance = p.fogEnd;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = p.ambientSky;
        RenderSettings.ambientEquatorColor = p.ambientEquator;
        RenderSettings.ambientGroundColor = p.ambientGround;

        var light = sun ? sun : RenderSettings.sun;
        if (light)
        {
            light.color = p.lightColor;
            light.intensity = p.lightIntensity;
            light.transform.rotation = Quaternion.Euler(p.lightRotation);
        }
        ApplyCamera(p, Camera.main);
    }

    static void ApplyCamera(EnvLightingPreset p, Camera cam)
    {
        if (p && cam && cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = p.cameraBackground;
    }
}
