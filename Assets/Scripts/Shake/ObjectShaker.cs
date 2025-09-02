using System.Collections;
using UnityEngine;
using Sirenix.OdinInspector;

[DisallowMultipleComponent]
public class ObjectShaker : SerializedMonoBehaviour
{
    public enum ShakeMode { Garagara, Perlin, Impulse, Sine }
    public enum ShakeTarget { Position, Rotation, Scale, All }

    [Header("Target")]
    public Transform target;
    public bool useLocal = true;

    [Header("基本設定")]
    public ShakeTarget shakeTarget = ShakeTarget.Position;
    [LabelText("揺れタイプ"), EnumToggleButtons]
    public ShakeMode mode = ShakeMode.Garagara;
    [LabelText("持続時間(0で無限・useCountが有効ならそちらが優先)")]
    public float duration = 0.5f;
    public bool loop = false;
    public bool useUnscaledTime = false;

    [Header("強さ(位置 / 回転(deg) / スケール)")]
    public Vector3 positionIntensity = new Vector3(0.1f, 0.1f, 0.1f);
    public Vector3 rotationIntensity = new Vector3(5f, 5f, 5f);
    public Vector3 scaleIntensity = new Vector3(0.05f, 0.05f, 0.05f);

    [Header("Garagara (ランダムステップ)")]
    [LabelText("ステップ間隔")]
    public float garagaraInterval = 0.15f; // 間隔（秒）
    [LabelText("ステップを補間して切替を滑らかにする")]
    public bool garagaraSmooth = true;
    [LabelText("補間の速さ（Inspectorで調整）")]
    [InfoBox("0 = 瞬間切替, 1 = 等速補間, >1 = 早く到達, <1 = ゆっくり")]
    [Range(0f, 5f)]
    public float garagaraLerpSpeed = 1f;

    [Header("Perlin / Sine")]
    public float frequency = 6f;
    public Vector3 sineFreq = new Vector3(6f, 5f, 4f);

    [Header("Impulse")]
    public float impulseStrength = 1f;
    public float impulseDecay = 1.5f;

    [Header("回数指定オプション")]
    public bool useCount = false;
    public int shakeCount = 1;
    [LabelText("Garagara以外の1ステップの長さ(秒)")]
    public float stepDuration = 0.12f;

    // 内部
    private Vector3 initialLocalPos;
    private Vector3 initialLocalRot;
    private Vector3 initialLocalScale;
    private Coroutine shakeRoutine;

    // Garagara 補間状態
    private Vector3 garagaraCurrentPos = Vector3.zero;
    private Vector3 garagaraTargetPos = Vector3.zero;
    private Vector3 garagaraCurrentRot = Vector3.zero;
    private Vector3 garagaraTargetRot = Vector3.zero;
    private Vector3 garagaraCurrentScale = Vector3.zero;
    private Vector3 garagaraTargetScale = Vector3.zero;
    private float garagaraTimerForLerp = 0f;

    void Reset()
    {
        if (target == null) target = this.transform;
    }

    void Awake()
    {
        if (target == null) target = this.transform;
        CacheInitial();
    }

    [Button(ButtonSizes.Medium)]
    public void CacheInitial()
    {
        if (target == null) target = this.transform;
        if (useLocal)
        {
            initialLocalPos = target.localPosition;
            initialLocalRot = target.localEulerAngles;
            initialLocalScale = target.localScale;
        }
        else
        {
            initialLocalPos = target.position;
            initialLocalRot = target.eulerAngles;
            initialLocalScale = target.localScale;
        }
    }

    [Button(ButtonSizes.Large)]
    public void StartShakeButton() => StartShake();
    [Button] public void StopShakeButton() => StopShake();

    public void StartShake()
    {
        if (target == null) target = this.transform;
        CacheInitial();
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);

        // reset garagara interpolation state
        garagaraCurrentPos = garagaraTargetPos = Vector3.zero;
        garagaraCurrentRot = garagaraTargetRot = Vector3.zero;
        garagaraCurrentScale = garagaraTargetScale = Vector3.zero;
        garagaraTimerForLerp = 0f;

        shakeRoutine = StartCoroutine(ShakeCoroutine());
    }

    public void StopShake()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }
        RestoreInitial();
    }

    private void RestoreInitial()
    {
        if (target == null) return;
        if (useLocal)
        {
            target.localPosition = initialLocalPos;
            target.localEulerAngles = initialLocalRot;
            target.localScale = initialLocalScale;
        }
        else
        {
            target.position = initialLocalPos;
            target.eulerAngles = initialLocalRot;
            target.localScale = initialLocalScale;
        }
    }

    private float TimeNow => useUnscaledTime ? Time.unscaledTime : Time.time;
    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    private IEnumerator ShakeCoroutine()
    {
        float startTime = TimeNow;
        float elapsed = 0f;
        float garagaraTimer = 0f;

        Vector3 impulseDirPos = Random.onUnitSphere;
        Vector3 impulseDirRot = Random.onUnitSphere;
        Vector3 impulseDirScale = Random.onUnitSphere;
        float impulseElapsed = 0f;

        float effectiveDuration = duration;
        if (useCount && shakeCount > 0)
        {
            effectiveDuration = (mode == ShakeMode.Garagara) ? garagaraInterval * shakeCount : stepDuration * shakeCount;
        }

        while (loop || effectiveDuration <= 0f || elapsed < effectiveDuration)
        {
            elapsed = TimeNow - startTime;
            Vector3 posOffset = Vector3.zero;
            Vector3 rotOffset = Vector3.zero;
            Vector3 scaleOffset = Vector3.zero;

            switch (mode)
            {
                case ShakeMode.Garagara:
                {
                    // 既存の処理（そのまま）
                    garagaraTimer += DeltaTime;
                    if (garagaraTimer >= garagaraInterval)
                    {
                        garagaraTimer = 0f;
                        garagaraTimerForLerp = 0f;
                        garagaraCurrentPos = garagaraTargetPos;
                        garagaraCurrentRot = garagaraTargetRot;
                        garagaraCurrentScale = garagaraTargetScale;

                        garagaraTargetPos = Vector3.Scale(RandomVector3(-1f, 1f), positionIntensity);
                        garagaraTargetRot = Vector3.Scale(RandomVector3(-1f, 1f), rotationIntensity);
                        garagaraTargetScale = Vector3.Scale(RandomVector3(-0.5f, 0.5f), scaleIntensity);
                    }
                    else
                    {
                        garagaraTimerForLerp += DeltaTime;
                    }

                    if (garagaraSmooth && garagaraInterval > 0f)
                    {
                        float rawT = Mathf.Clamp01(garagaraTimerForLerp / Mathf.Max(garagaraInterval, 0.0001f));
                        float lerpT = Mathf.Clamp01(rawT * garagaraLerpSpeed);
                        posOffset = Vector3.Lerp(garagaraCurrentPos, garagaraTargetPos, lerpT);
                        rotOffset = Vector3.Lerp(garagaraCurrentRot, garagaraTargetRot, lerpT);
                        scaleOffset = Vector3.Lerp(garagaraCurrentScale, garagaraTargetScale, lerpT);
                    }
                    else
                    {
                        posOffset = garagaraTargetPos;
                        rotOffset = garagaraTargetRot;
                        scaleOffset = garagaraTargetScale;
                    }
                    break;
                }

                case ShakeMode.Perlin:
                {
                    // 変数名を perlinT にして衝突回避
                    float perlinT = TimeNow * frequency;
                    posOffset = new Vector3(
                        (Mathf.PerlinNoise(perlinT, 0) * 2f - 1f) * positionIntensity.x,
                        (Mathf.PerlinNoise(0, perlinT) * 2f - 1f) * positionIntensity.y,
                        (Mathf.PerlinNoise(perlinT * 0.5f, perlinT * 0.4f) * 2f - 1f) * positionIntensity.z
                    );
                    rotOffset = new Vector3(
                        (Mathf.PerlinNoise(perlinT * 1.3f, 10) * 2f - 1f) * rotationIntensity.x,
                        (Mathf.PerlinNoise(10, perlinT * 1.6f) * 2f - 1f) * rotationIntensity.y,
                        (Mathf.PerlinNoise(perlinT * 0.9f, perlinT * 1.1f) * 2f - 1f) * rotationIntensity.z
                    );
                    scaleOffset = new Vector3(
                        (Mathf.PerlinNoise(perlinT * 2.1f, 5) * 2f - 1f) * scaleIntensity.x,
                        (Mathf.PerlinNoise(7, perlinT * 2.3f) * 2f - 1f) * scaleIntensity.y,
                        (Mathf.PerlinNoise(perlinT * 1.7f, 3) * 2f - 1f) * scaleIntensity.z
                    );
                    break;
                }

                case ShakeMode.Impulse:
                {
                    float decayFactor = Mathf.Exp(-impulseDecay * impulseElapsed);
                    posOffset = Vector3.Scale(impulseDirPos, positionIntensity) * impulseStrength * decayFactor;
                    rotOffset = Vector3.Scale(impulseDirRot, rotationIntensity) * impulseStrength * decayFactor;
                    scaleOffset = Vector3.Scale(impulseDirScale, scaleIntensity) * impulseStrength * decayFactor;
                    impulseElapsed += DeltaTime;
                    break;
                }

                case ShakeMode.Sine:
                {
                    // 変数名を sineT にして衝突回避
                    float sineT = TimeNow;
                    posOffset = new Vector3(
                        Mathf.Sin(sineT * sineFreq.x) * positionIntensity.x,
                        Mathf.Sin(sineT * sineFreq.y + 1.2f) * positionIntensity.y,
                        Mathf.Sin(sineT * sineFreq.z + 2.8f) * positionIntensity.z
                    );
                    rotOffset = new Vector3(
                        Mathf.Sin(sineT * (sineFreq.x * 0.8f)) * rotationIntensity.x,
                        Mathf.Sin(sineT * (sineFreq.y * 1.1f) + 0.3f) * rotationIntensity.y,
                        Mathf.Sin(sineT * (sineFreq.z * 0.9f) + 1.7f) * rotationIntensity.z
                    );
                    scaleOffset = new Vector3(
                        Mathf.Sin(sineT * (sineFreq.x * 1.4f)) * scaleIntensity.x,
                        Mathf.Sin(sineT * (sineFreq.y * 1.2f)) * scaleIntensity.y,
                        Mathf.Sin(sineT * (sineFreq.z * 1.0f)) * scaleIntensity.z
                    );
                    break;
                }
            }

            // Apply only selected targets
            if (shakeTarget == ShakeTarget.Position || shakeTarget == ShakeTarget.All)
            {
                if (useLocal) target.localPosition = initialLocalPos + posOffset;
                else target.position = initialLocalPos + posOffset;
            }

            if (shakeTarget == ShakeTarget.Rotation || shakeTarget == ShakeTarget.All)
            {
                if (useLocal) target.localEulerAngles = initialLocalRot + rotOffset;
                else target.eulerAngles = initialLocalRot + rotOffset;
            }

            if (shakeTarget == ShakeTarget.Scale || shakeTarget == ShakeTarget.All)
            {
                target.localScale = initialLocalScale + scaleOffset;
            }

            yield return null;
        }

        RestoreInitial();
        shakeRoutine = null;
    }

    private Vector3 RandomVector3(float min, float max)
    {
        return new Vector3(
            Random.Range(min, max),
            Random.Range(min, max),
            Random.Range(min, max)
        );
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (target == null) target = this.transform;
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
        {
            CacheInitial();
        }
    }
#endif
}
