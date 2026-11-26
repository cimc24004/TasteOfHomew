using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// オブジェクトを揺らす汎用コンポーネント。位置・回転・スケールのどれを揺らすかと、揺れ方を選べる。
///
/// 揺れ方（ShakeMode）:
///   ・Garagara … 一定間隔でランダムな姿勢に切り替える（ガタガタ）
///   ・Perlin   … パーリンノイズによる滑らかで不規則な揺れ
///   ・Impulse  … 最初に強く動いて、だんだん収まる
///   ・Sine     … 規則的な往復
/// </summary>
[DisallowMultipleComponent]
public class ObjectShaker : SerializedMonoBehaviour
{
    public enum ShakeMode { Garagara, Perlin, Impulse, Sine }
    public enum ShakeTarget { Position, Rotation, Scale, All }

    /// <summary>1 フレーム分の揺れ（初期姿勢からのずれ）。</summary>
    private struct ShakeOffsets
    {
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
    }

    // ===== Inspector =====

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
    public float garagaraInterval = 0.15f;
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

    // ===== 実行時の状態 =====

    // 揺らす前の姿勢（useLocal ならローカル、そうでなければワールド座標系）
    private Vector3 initialPosition;
    private Vector3 initialEulerAngles;
    private Vector3 initialScale;
    private Coroutine shakeRoutine;

    // Garagara: 「ひとつ前の姿勢 → 次の姿勢」へ補間しながら切り替える
    private ShakeOffsets garagaraFrom;
    private ShakeOffsets garagaraTo;
    private float garagaraStepTimer;
    private float garagaraLerpTimer;

    // Impulse: 開始時に決めたランダムな方向へ動き、時間とともに減衰する
    private Vector3 impulseDirectionPosition;
    private Vector3 impulseDirectionRotation;
    private Vector3 impulseDirectionScale;
    private float impulseElapsed;

    private float TimeNow => useUnscaledTime ? Time.unscaledTime : Time.time;
    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    // ===== Unity ライフサイクル =====

    private void Reset()
    {
        if (target == null) target = this.transform;
    }

    private void Awake()
    {
        CacheInitial();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (target == null) target = this.transform;
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
        {
            CacheInitial();
        }
    }
#endif

    // ===== 公開 API =====

    /// <summary>現在の姿勢を「揺らす前の姿勢」として覚える。</summary>
    [Button(ButtonSizes.Medium)]
    public void CacheInitial()
    {
        if (target == null) target = this.transform;

        if (useLocal)
        {
            initialPosition = target.localPosition;
            initialEulerAngles = target.localEulerAngles;
        }
        else
        {
            initialPosition = target.position;
            initialEulerAngles = target.eulerAngles;
        }
        initialScale = target.localScale;
    }

    [Button(ButtonSizes.Large)]
    public void StartShakeButton() => StartShake();

    [Button]
    public void StopShakeButton() => StopShake();

    public void StartShake()
    {
        CacheInitial();
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);

        garagaraFrom = default;
        garagaraTo = default;
        garagaraLerpTimer = 0f;

        shakeRoutine = StartCoroutine(ShakeCoroutine());
    }

    /// <summary>揺れを止めて、揺らす前の姿勢に戻す。</summary>
    public void StopShake()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }
        RestoreInitial();
    }

    // ===== 揺れの本体 =====

    private IEnumerator ShakeCoroutine()
    {
        float startTime = TimeNow;
        float elapsed = 0f;

        garagaraStepTimer = 0f;
        impulseDirectionPosition = Random.onUnitSphere;
        impulseDirectionRotation = Random.onUnitSphere;
        impulseDirectionScale = Random.onUnitSphere;
        impulseElapsed = 0f;

        float totalDuration = GetTotalDuration();

        // loop 指定、または持続時間が 0 以下のときは、StopShake されるまで揺らし続ける
        while (loop || totalDuration <= 0f || elapsed < totalDuration)
        {
            elapsed = TimeNow - startTime;
            ApplyOffsets(ComputeOffsets());
            yield return null;
        }

        RestoreInitial();
        shakeRoutine = null;
    }

    /// <summary>揺れの長さ。回数指定が有効なら「1 ステップの長さ × 回数」、そうでなければ duration。</summary>
    private float GetTotalDuration()
    {
        if (!useCount || shakeCount <= 0) return duration;

        float stepSeconds = mode == ShakeMode.Garagara ? garagaraInterval : stepDuration;
        return stepSeconds * shakeCount;
    }

    private ShakeOffsets ComputeOffsets()
    {
        switch (mode)
        {
            case ShakeMode.Garagara: return ComputeGaragaraOffsets();
            case ShakeMode.Perlin:   return ComputePerlinOffsets();
            case ShakeMode.Impulse:  return ComputeImpulseOffsets();
            case ShakeMode.Sine:     return ComputeSineOffsets();
            default:                 return default;
        }
    }

    private ShakeOffsets ComputeGaragaraOffsets()
    {
        // 一定間隔ごとに、次のランダムな姿勢を決める
        garagaraStepTimer += DeltaTime;
        if (garagaraStepTimer >= garagaraInterval)
        {
            garagaraStepTimer = 0f;
            garagaraLerpTimer = 0f;
            garagaraFrom = garagaraTo;

            garagaraTo.position = Vector3.Scale(RandomVector3(-1f, 1f), positionIntensity);
            garagaraTo.rotation = Vector3.Scale(RandomVector3(-1f, 1f), rotationIntensity);
            garagaraTo.scale = Vector3.Scale(RandomVector3(-0.5f, 0.5f), scaleIntensity);
        }
        else
        {
            garagaraLerpTimer += DeltaTime;
        }

        // 補間なし: 次の姿勢へ瞬時に切り替える
        if (!garagaraSmooth || garagaraInterval <= 0f) return garagaraTo;

        float stepProgress = Mathf.Clamp01(garagaraLerpTimer / Mathf.Max(garagaraInterval, 0.0001f));
        float lerpT = Mathf.Clamp01(stepProgress * garagaraLerpSpeed);

        ShakeOffsets offsets;
        offsets.position = Vector3.Lerp(garagaraFrom.position, garagaraTo.position, lerpT);
        offsets.rotation = Vector3.Lerp(garagaraFrom.rotation, garagaraTo.rotation, lerpT);
        offsets.scale = Vector3.Lerp(garagaraFrom.scale, garagaraTo.scale, lerpT);
        return offsets;
    }

    private ShakeOffsets ComputePerlinOffsets()
    {
        // 軸ごとにノイズの読み取り位置と速さをずらして、動きが揃わないようにしている
        float t = TimeNow * frequency;

        ShakeOffsets offsets;
        offsets.position = new Vector3(
            SignedPerlin(t, 0) * positionIntensity.x,
            SignedPerlin(0, t) * positionIntensity.y,
            SignedPerlin(t * 0.5f, t * 0.4f) * positionIntensity.z
        );
        offsets.rotation = new Vector3(
            SignedPerlin(t * 1.3f, 10) * rotationIntensity.x,
            SignedPerlin(10, t * 1.6f) * rotationIntensity.y,
            SignedPerlin(t * 0.9f, t * 1.1f) * rotationIntensity.z
        );
        offsets.scale = new Vector3(
            SignedPerlin(t * 2.1f, 5) * scaleIntensity.x,
            SignedPerlin(7, t * 2.3f) * scaleIntensity.y,
            SignedPerlin(t * 1.7f, 3) * scaleIntensity.z
        );
        return offsets;
    }

    private ShakeOffsets ComputeImpulseOffsets()
    {
        float decayFactor = Mathf.Exp(-impulseDecay * impulseElapsed);
        impulseElapsed += DeltaTime;

        ShakeOffsets offsets;
        offsets.position = Vector3.Scale(impulseDirectionPosition, positionIntensity) * impulseStrength * decayFactor;
        offsets.rotation = Vector3.Scale(impulseDirectionRotation, rotationIntensity) * impulseStrength * decayFactor;
        offsets.scale = Vector3.Scale(impulseDirectionScale, scaleIntensity) * impulseStrength * decayFactor;
        return offsets;
    }

    private ShakeOffsets ComputeSineOffsets()
    {
        // 軸ごとに周波数と位相をずらして、動きが揃わないようにしている
        float t = TimeNow;

        ShakeOffsets offsets;
        offsets.position = new Vector3(
            Mathf.Sin(t * sineFreq.x) * positionIntensity.x,
            Mathf.Sin(t * sineFreq.y + 1.2f) * positionIntensity.y,
            Mathf.Sin(t * sineFreq.z + 2.8f) * positionIntensity.z
        );
        offsets.rotation = new Vector3(
            Mathf.Sin(t * (sineFreq.x * 0.8f)) * rotationIntensity.x,
            Mathf.Sin(t * (sineFreq.y * 1.1f) + 0.3f) * rotationIntensity.y,
            Mathf.Sin(t * (sineFreq.z * 0.9f) + 1.7f) * rotationIntensity.z
        );
        offsets.scale = new Vector3(
            Mathf.Sin(t * (sineFreq.x * 1.4f)) * scaleIntensity.x,
            Mathf.Sin(t * (sineFreq.y * 1.2f)) * scaleIntensity.y,
            Mathf.Sin(t * (sineFreq.z * 1.0f)) * scaleIntensity.z
        );
        return offsets;
    }

    /// <summary>揺れを、選択されている対象（位置 / 回転 / スケール）にだけ反映する。</summary>
    private void ApplyOffsets(ShakeOffsets offsets)
    {
        if (shakeTarget == ShakeTarget.Position || shakeTarget == ShakeTarget.All)
        {
            if (useLocal) target.localPosition = initialPosition + offsets.position;
            else target.position = initialPosition + offsets.position;
        }

        if (shakeTarget == ShakeTarget.Rotation || shakeTarget == ShakeTarget.All)
        {
            if (useLocal) target.localEulerAngles = initialEulerAngles + offsets.rotation;
            else target.eulerAngles = initialEulerAngles + offsets.rotation;
        }

        if (shakeTarget == ShakeTarget.Scale || shakeTarget == ShakeTarget.All)
        {
            target.localScale = initialScale + offsets.scale;
        }
    }

    private void RestoreInitial()
    {
        if (target == null) return;

        if (useLocal)
        {
            target.localPosition = initialPosition;
            target.localEulerAngles = initialEulerAngles;
        }
        else
        {
            target.position = initialPosition;
            target.eulerAngles = initialEulerAngles;
        }
        target.localScale = initialScale;
    }

    // ===== 小さなヘルパー =====

    /// <summary>パーリンノイズ（0 〜 1）を -1 〜 1 に引き伸ばしたもの。</summary>
    private static float SignedPerlin(float x, float y)
    {
        return Mathf.PerlinNoise(x, y) * 2f - 1f;
    }

    private Vector3 RandomVector3(float min, float max)
    {
        return new Vector3(
            Random.Range(min, max),
            Random.Range(min, max),
            Random.Range(min, max)
        );
    }
}
