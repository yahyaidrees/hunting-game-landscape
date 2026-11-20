using UnityEngine;

// One lighting look for an environment scene (day, night), applied by EnvTimeOfDay.
// The environment art carries its sunlight baked into the textures and draws with unlit shaders, so most of the look
// is a colour grade of that art plus the sky, both inside the SavannaEnv shaders (Shaders/EnvNight.cginc). Fog applies
// to everything; the ambient colours and the directional light only reach what is lit in real time (animals, rifle).
[CreateAssetMenu(menuName = "Environment/Lighting Preset", fileName = "LightingPreset")]
public class EnvLightingPreset : ScriptableObject
{
    [Header("Grade of the baked art (0 = the untouched day look)")]
    [Range(0, 1)] public float night;
    [Tooltip("Multiplies the graded colour.")]
    public Color gradeTint = Color.white;
    [Range(0, 1)] public float desaturate;
    [Tooltip("Pushes dark colours down more than light ones (blend toward colour squared).")]
    [Range(0, 1)] public float deepenShadows;
    [Tooltip("How far the mountain panorama, which has no fog of its own, sinks into the night sky behind it.")]
    [Range(0, 1)] public float backdropHaze;
    [Tooltip("At night the fog takes the sky's colour in each direction (1), so far hills fade into the sky behind them " +
             "and the mist under the moon glows; 0 = the plain fog colour below.")]
    [Range(0, 1)] public float fogFromSky;
    [Tooltip("Brightness of the ground and props at the hunter's feet (1 = no change); back to full at Near Shade Distance.")]
    [Range(0, 1)] public float nearShade = 1f;
    [Min(1)] public float nearShadeDistance = 40f;

    [Header("Night sky (shown when night > 0)")]
    public Color skyZenith = new Color(0.035f, 0.075f, 0.085f);
    public Color skyHorizon = new Color(0.13f, 0.18f, 0.18f);
    [Tooltip("Elevation in degrees where the sky reaches the zenith colour.")]
    [Range(5, 90)] public float skyGradientHeight = 35f;
    [Tooltip("Degrees from +Z (the hunter's main view) toward +X.")]
    public float moonAzimuth = 8f;
    [Range(0, 89)] public float moonElevation = 22f;
    [Tooltip("Disc radius in degrees (the real moon is about 0.26; bigger reads better on a phone).")]
    [Range(0.1f, 3f)] public float moonRadius = 0.75f;
    public Color moonColor = new Color(0.8f, 0.84f, 0.82f);
    [Tooltip("Tight halo around the disc: colour at the moon and falloff width in degrees.")]
    public Color haloColor = new Color(0.3f, 0.34f, 0.32f);
    [Range(0.1f, 10f)] public float haloWidth = 1.2f;
    [Tooltip("Wide glow lighting the sky around the moon: colour at the moon and falloff width in degrees.")]
    public Color glowColor = new Color(0.12f, 0.18f, 0.18f);
    [Range(1f, 60f)] public float glowWidth = 9f;
    [Tooltip("Spreads halo and glow sideways (falls off faster up and down). 0 = round.")]
    [Range(0, 8)] public float glowSquash = 1.5f;

    [Header("Fog (linear: keep the scene's fog mode, other modes are stripped from builds). The colour is what lit objects fog to")]
    public Color fogColor = new Color(0.66f, 0.76f, 0.88f);
    public float fogStart = 120f;
    public float fogEnd = 1800f;

    [Header("Real-time lighting (animals, rifle)")]
    public Color ambientSky = new Color(0.80f, 0.88f, 1f);
    public Color ambientEquator = new Color(0.66f, 0.72f, 0.62f);
    public Color ambientGround = new Color(0.42f, 0.48f, 0.28f);
    public Color lightColor = new Color(1f, 0.96f, 0.88f);
    public float lightIntensity = 1f;
    [Tooltip("Euler angles of the directional light. Keep it where the baked sun was, so live shadows agree with the art.")]
    public Vector3 lightRotation = new Vector3(52f, 238f, 0f);
    [Tooltip("Camera clear colour (only seen through gaps in the sky).")]
    public Color cameraBackground = new Color(0.44f, 0.60f, 0.86f);

    public Vector3 MoonDirection
    {
        get
        {
            float a = moonAzimuth * Mathf.Deg2Rad, e = moonElevation * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
        }
    }

    void OnValidate()
    {
        // live tuning: re-apply when this preset is the one on screen
        var tod = EnvTimeOfDay.Active;
        if (tod && tod.Current == this) tod.Apply();
    }
}
