using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BassPostProcessPulse : MonoBehaviour
{
    // things for the music and the visual profile
    public AudioSource music;
    public Volume volume;

    [Header("Bloom")]
    public bool bloomEnabled = true;
    public float bloomMultiplier = 40f;
    public float bloomThreshold = 0.09f;
    public float bloomExponent = 1.2f;

    [Header("Color Blur")]
    public bool chromaticEnabled = true;
    public float chromaticMultiplier = 1.4f;
    public float chromaticThreshold = 0.01f;
    public float chromaticExponent = 0.9f;

    [Header("Fish Eye")]
    public bool lensDistortionEnabled = true;
    public float lensDistortionMultiplier = 0.3f;
    public float lensThreshold = 0.07f;
    public float lensExponent = 1f;

    [Header("Shaking")]
    public bool shakeEnabled = true;
    public Camera targetCamera;
    public float shakeMultiplier = 0.02f;
    public float shakeThreshold = 0.02f;
    public float shakeExponent = 1.2f;
    public float shakeSpeed = 12f;

    // which parts of the music we care about
    public int deepBassCount = 3;
    public int midBassCount = 4;
    public int highMidCount = 5;

    public float audioMultiplier = 0.5f;
    public float smoothSpeed = 8f;

    private float[] spectrum = new float[64];
    private float pulse;

    private Bloom bloom;
    private ChromaticAberration chromatic;
    private LensDistortion lensDistortion;

    private float bloomDefault;
    private float chromaticDefault;
    private float lensDefault;

    [HideInInspector]
    public bool freezeEffects = false;
    public bool vignetteEnabled = true;
    public bool filmGrainEnabled = true;

    // let other scripts see the lens value
    public float LensIntensity
    {
        get
        {
            return lensDistortion != null ? lensDistortion.intensity.value : 0f;
        }
    }

    void Awake()
    {
        // setup the post process stuff at the start
        if (volume != null)
        {
            volume.profile.TryGet(out bloom);
            volume.profile.TryGet(out chromatic);
            volume.profile.TryGet(out lensDistortion);
        }

        if (bloom != null) bloomDefault = bloom.intensity.value;
        if (chromatic != null) chromaticDefault = chromatic.intensity.value;
        if (lensDistortion != null) lensDefault = lensDistortion.intensity.value;
    }

    void Update()
    {
        if (freezeEffects) return;

        if (music == null || !music.isPlaying)
        {
            ResetEffects();
            return;
        }

        // grab the audio data from the song
        music.GetSpectrumData(spectrum, 0, FFTWindow.Blackman);

        float deepBass = SumRange(spectrum, 0, deepBassCount);
        float midBass = SumRange(spectrum, deepBassCount, midBassCount);
        float highMid = SumRange(spectrum, deepBassCount + midBassCount, highMidCount);

        // combine the bass and mids for a single pulse value
        float combinedPulse = (deepBass * 1f + midBass * 0.7f + highMid * 0.5f) * audioMultiplier;
        pulse = Mathf.Lerp(pulse, combinedPulse, Time.deltaTime * smoothSpeed);

        // shove the pulse value into the post processing
        ApplyEffect(bloom, bloomEnabled, pulse, bloomThreshold, bloomExponent, bloomMultiplier, val => bloom.intensity.value = val, bloomDefault);
        ApplyChromatic(chromatic, chromaticEnabled, pulse, chromaticThreshold, chromaticExponent, chromaticMultiplier, chromaticDefault);
        ApplyEffect(lensDistortion, lensDistortionEnabled, pulse, lensThreshold, lensExponent, lensDistortionMultiplier, val => lensDistortion.intensity.value = val, lensDefault);

        if (targetCamera != null && shakeEnabled)
            ApplyCameraShake(pulse);
    }

    private float SumRange(float[] data, int start, int count)
    {
        float sum = 0f;
        for (int i = start; i < start + count && i < data.Length; i++)
            sum += data[i];
        return sum;
    }

    private void ApplyEffect<T>(T effect, bool enabled, float pulse, float threshold, float exponent, float multiplier, System.Action<float> applyValue, float defaultValue)
    {
        if (effect != null && enabled)
        {
            float value = Mathf.Max(pulse - threshold, 0f);
            value = Mathf.Pow(value, exponent);
            applyValue(defaultValue + value * multiplier);
        }
    }

    private void ApplyChromatic(ChromaticAberration effect, bool enabled, float pulse, float threshold, float exponent, float multiplier, float defaultValue)
    {
        if (effect != null && enabled)
        {
            float value = Mathf.Max(pulse - threshold, 0f);
            value = Mathf.Pow(value, exponent);
            float finalValue = Mathf.Max(value * multiplier, 0.01f);
            effect.intensity.value = defaultValue + Mathf.PingPong(finalValue, 1f);
        }
    }

    private void ApplyCameraShake(float pulse)
    {
        float shakePulse = Mathf.Max(pulse - shakeThreshold, 0f);
        if (shakePulse <= 0f) return;

        float shakeAmount = Mathf.Pow(shakePulse, shakeExponent) * shakeMultiplier;
        Vector3 shakeOffset = new Vector3(
                  (Mathf.PerlinNoise(Time.time * shakeSpeed, 0f) - 0.5f) * 2f,
                  (Mathf.PerlinNoise(0f, Time.time * shakeSpeed) - 0.5f) * 2f,
                  0f
        ) * shakeAmount;

        targetCamera.transform.localPosition += shakeOffset;
    }

    void ResetEffects()
    {
        if (bloom != null) bloom.intensity.value = bloomDefault;
        if (chromatic != null) chromatic.intensity.value = chromaticDefault;
        if (lensDistortion != null) lensDistortion.intensity.value = lensDefault;
    }
}