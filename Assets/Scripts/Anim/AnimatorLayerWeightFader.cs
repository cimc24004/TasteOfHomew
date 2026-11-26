using UnityEngine;
using DG.Tweening; // DOTweenをインポート
using System.Collections.Generic; // Dictionaryのために必要

/// <summary>
/// AnimatorのレイヤーウェイトをDOTweenでフェードさせます。
/// DOTween が必要です。
/// </summary>
public class AnimatorLayerWeightFader : MonoBehaviour
{
    [Tooltip("ウェイトを操作するAnimatorコンポーネント")]
    [SerializeField]
    private Animator animator;

    [Tooltip("フェードイン/アウトにかかるデフォルトの秒数")]
    [SerializeField]
    private float defaultFadeDuration = 0.5f;

    /// <summary>
    /// 実行中のレイヤーTweenを管理（キーはLayerIndex）
    /// </summary>
    private Dictionary<int, Tween> activeLayerTweens = new Dictionary<int, Tween>();

    private void Start()
    {
        // Animatorがセットされていない場合は警告
        if (animator == null)
        {
            Debug.LogError("Animatorがアタッチされていません。", this);
        }
    }

    // --------------------------------------------------------------------
    //  公開メソッド (FadeIn / FadeOut)
    // --------------------------------------------------------------------

    /// <summary>
    /// 指定したレイヤーのウェイトを 1.0 にします（デフォルト時間）。
    /// </summary>
    /// <param name="layerIndex">レイヤーのインデックス</param>
    public void FadeIn(int layerIndex)
    {
        SetLayerWeight(layerIndex, 1f, defaultFadeDuration);
    }

    /// <summary>
    /// 指定したレイヤーのウェイトを 0.0 にします（デフォルト時間）。
    /// </summary>
    /// <param name="layerIndex">レイヤーのインデックス</param>
    public void FadeOut(int layerIndex)
    {
        SetLayerWeight(layerIndex, 0f, defaultFadeDuration);
    }

    /// <summary>
    /// (オーバーロード) フェード時間を指定してFadeInします。
    /// </summary>
    public void FadeIn(int layerIndex, float duration)
    {
        SetLayerWeight(layerIndex, 1f, duration);
    }

    /// <summary>
    /// (オーバーロード) フェード時間を指定してFadeOutします。
    /// </summary>
    public void FadeOut(int layerIndex, float duration)
    {
        SetLayerWeight(layerIndex, 0f, duration);
    }

    // --------------------------------------------------------------------
    //  公開メソッド (メイン処理)
    // --------------------------------------------------------------------

    /// <summary>
    /// 指定したAnimatorレイヤーのウェイトを、指定した時間かけて目標値に変更します。
    /// </summary>
    /// <param name="layerIndex">操作するレイヤーのインデックス</param>
    /// <param name="targetWeight">目標のウェイト (0.0 ～ 1.0)</param>
    /// <param name="duration">フェードにかかる時間（秒）</param>
    public void SetLayerWeight(int layerIndex, float targetWeight, float duration)
    {
        if (animator == null)
        {
            Debug.LogError("Animatorがセットされていません。", this);
            return;
        }

        if (layerIndex < 0 || layerIndex >= animator.layerCount)
        {
            Debug.LogError($"無効なレイヤーインデックスです: {layerIndex}", this);
            return;
        }

        // 既にそのレイヤーで実行中のTweenがあれば停止
        if (activeLayerTweens.TryGetValue(layerIndex, out Tween existingTween))
        {
            existingTween.Kill();
            activeLayerTweens.Remove(layerIndex);
        }

        // 瞬時に変更する場合 (DOTweenのオーバーヘッドを避ける)
        if (duration <= 0f)
        {
            animator.SetLayerWeight(layerIndex, targetWeight);
            return;
        }

        // DOTween.To() を使ってウェイトを変更
        Tween newTween = DOTween.To(
            () => animator.GetLayerWeight(layerIndex),      // ゲッター
            (x) => animator.SetLayerWeight(layerIndex, x), // セッター
            targetWeight,                                 // ターゲット値
            duration                                      // 変化にかかる時間
        )
        .SetEase(Ease.OutQuad)
        .OnComplete(() => {
            // 完了したらDictionaryから削除
            if (activeLayerTweens.ContainsKey(layerIndex))
            {
                activeLayerTweens.Remove(layerIndex);
            }
        });

        // 新しいTweenをDictionaryに追加して管理
        activeLayerTweens[layerIndex] = newTween;
    }

    /// <summary>
    /// このコンポーネントが破棄される際に、関連するTweenをすべて停止します。
    /// </summary>
    private void OnDestroy()
    {
        // すべてのアクティブなTweenを停止
        foreach (var pair in activeLayerTweens)
        {
            pair.Value.Kill();
        }
        activeLayerTweens.Clear();
    }
}
