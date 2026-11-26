using UnityEngine;
using UnityEngine.Animations.Rigging; // Riggingパッケージ
using DG.Tweening;                  // DOTween (無料版)
using Sirenix.OdinInspector;        // Odin Inspector
using System;                       // PlayerStatus.instance のため

/// <summary>
/// NPCの行動（Rig制御やLookAt）関数を提供するコンポーネント。
/// DOTween(無料版)とOdin Inspectorが必要です。
/// (Update関数内での処理は行いません)
/// </summary>
public class NPCActions : MonoBehaviour
{
    [Title("Rig Settings")]
    // ↓↓↓ 説明文をここに追加しました ↓↓↓
    [InfoBox("頭のWeight制御するためのRigをセット")]
    [SerializeField]
    [Required("Weightを操作したいRigコンポーネントを指定してください")]
    private Rig rig;
    // ↑↑↑ 説明文をここまで追加しました ↑↑↑

    [Title("Target Settings")]
    [Tooltip("参照するプレイヤーのTransform。Startで自動取得します。")]
    [ReadOnly]
    public Transform playerTransform;

    [Title("Parameter Settings (Inspector Button用)")]
    [SerializeField]
    [Tooltip("RigのWeightが変化するのにかかる時間（秒）")]
    private float weightFadeDuration = 0.5f;

    [SerializeField]
    [Tooltip("プレイヤーの方向を向くのにかかる時間（秒）")]
    private float lookAtDuration = 0.3f;
    // ↓↓↓ 元の向きを保存する変数を追加 ↓↓↓
    private Quaternion defaultRotation;

    // --- 内部状態 ---
    private Tweener weightTween; // Weight変更用Tween
    private Tweener lookAtTween; // LookAt用Tween

    void Start()
    {
        // PlayerStatus.instanceからTransformをキャッシュ
        // ※ PlayerStatusクラスは別途プロジェクト内に存在する必要があります
        if (PlayerStatus.instance != null)
        {
            playerTransform = PlayerStatus.instance.transform;
        }
        else
        {
            Debug.LogError("PlayerStatus.instanceが見つかりません。", this);
        }
        defaultRotation = transform.rotation;
    }

    /// <summary>
    /// (関数1) RigのWeightを指定した値に徐々に変更します (DOTween使用)。
    /// （インスペクターで指定した 'rig' フィールドが対象です）
    /// </summary>
    /// <param name="targetWeight">目標のWeight (0または1)</param>
    /// <param name="duration">フェードにかかる時間</param>
    [Button("Set Rig Weight (DOTween)", ButtonSizes.Medium)]
    [BoxGroup("Odin Functions")]
    [PropertyTooltip("RigのWeightを指定した値に徐々に変更します")]
    public void FadeRigWeight(float targetWeight, float duration)
    {
        if (rig == null)
        {
            Debug.LogWarning("Rigが設定されていません。インスペクターでアタッチしてください。", this);
            return;
        }

        // 既存のWeight Tweenがあれば停止（上書き）
        // これにより、関数が連続で呼ばれてもTweenが重複しません。
        weightTween?.Kill();

        // DOTweenを使ってWeightを滑らかに変更
        // DOTween.To( [getter], [setter], [目標値], [所要時間] )
        weightTween = DOTween.To(() => rig.weight, // 現在の値を取得
                                 x => rig.weight = x, // 値をセット
                                 targetWeight,         // 目標値
                                 duration)             // 所要時間
                             .SetEase(Ease.OutQuad) // イージング（滑らかな変化）
                             .OnKill(() => weightTween = null); // 完了時またはKill時にTweenerをnullに
    }

    [Button("LookAt Default Rotation (DOTween)", ButtonSizes.Medium)]
    [BoxGroup("Odin Functions")]
    [PropertyTooltip("Start時に記憶した元の向きに徐々に戻ります (DOTween)")]
    public void LookAtDefaultRotation(float duration)
    {
        // 既存のLookAt Tweenがあれば停止（上書き）
        lookAtTween?.Kill();

        // DOTweenのDORotateQuaternionを使用して、保存しておいた初期の向き(defaultRotation)に戻す
        // （DORotate(オイラー角)より Quaternion を直接使うこちらが確実です）
        lookAtTween = transform.DORotateQuaternion(defaultRotation, duration)
                               .SetEase(Ease.OutQuad)
                               .OnKill(() => lookAtTween = null);
    }
    /// <summary>
    /// (関数2) プレイヤーの方向をY軸（水平方向）のみ、徐々に向きます (DOTween使用)。
    /// （このオブジェクト自体のTransform.rotationを操作します）
    /// </summary>
    /// <param name="duration">方向転換にかかる時間</param>
    [Button("LookAt Player (Y-Only DOTween)", ButtonSizes.Medium)]
    [BoxGroup("Odin Functions")]
    [PropertyTooltip("プレイヤーの方向をY軸のみ徐々に向きます (DOTween)")]
    public void LookAtPlayerYOnly(float duration)
    {
        if (playerTransform == null)
        {
            if (PlayerStatus.instance != null) playerTransform = PlayerStatus.instance.transform;
            else
            {
                Debug.LogWarning("playerTransformが設定されていません。", this);
                return;
            }
        }

        // 既存のLookAt Tweenがあれば停止（上書き）
        lookAtTween?.Kill();

        // プレイヤーへの方向ベクトルを計算
        Vector3 direction = playerTransform.position - transform.position;

        // Y軸の差を無視して、水平方向の回転のみに
        direction.y = 0;

        // ほぼ真上または真下にいる場合（directionがゼロベクトルに近い場合）は回転しない
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        // 目標の回転（Quaternion）を計算
        Quaternion targetRotation = Quaternion.LookRotation(direction);

        // DOTweenのDORotateを使用して、Y軸のみを目標の回転に合わせる
        // targetRotation.eulerAngles.y で目標のY軸角度を取得
        // （現在のX, Z軸の回転は維持する）
        Vector3 targetEuler = new Vector3(transform.eulerAngles.x, targetRotation.eulerAngles.y, transform.eulerAngles.z);

        lookAtTween = transform.DORotate(targetEuler, duration)
                               .SetEase(Ease.OutQuad)
                               .OnKill(() => lookAtTween = null);
    }

    // --- Odin Buttonのテスト用（インスペクターで設定した時間を使う） ---

    [Button("Fade Weight to 1.0 (Default Duration)")]
    [BoxGroup("Odin Test Buttons")]
    public void FadeWeightOne()
    {
        FadeRigWeight(1.0f, weightFadeDuration);
    }

    [Button("Fade Weight to 0.0 (Default Duration)")]
    [BoxGroup("Odin Test Buttons")]
    public void FadeWeightZero()
    {
        FadeRigWeight(0.0f, weightFadeDuration);
    }

    [Button("LookAt Player (Default Duration)")]
    [BoxGroup("Odin Test Buttons")]
    public void LookAtPlayerY()
    {
        LookAtPlayerYOnly(lookAtDuration);
    }

    // ↓↓↓ テストボタンを追加 ↓↓↓
    [Button("LookAt Default (Default Duration)")]
    [BoxGroup("Odin Test Buttons")]
    public void LookAtDefaultY()
    {
        LookAtDefaultRotation(lookAtDuration);
    }
    void OnDestroy()
    {
        // シーン終了時にTweenを確実に破棄（メモリリーク防止）
        weightTween?.Kill();
        lookAtTween?.Kill();
    }
}
