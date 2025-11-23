using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections.Generic;
using DG.Tweening; // DOTweenをインポート
using System; // [Serializable]のために必要

/// <summary>
/// 複数のRigのWeightをキー(string)で管理し、DOTweenでフェードイン/アウトさせます。
/// Animation Rigging パッケージと DOTween が必要です。
/// </summary>
public class RigWeightsFade : MonoBehaviour
{
    // --------------------------------------------------------------------
    //  Inspector表示用の設定
    // --------------------------------------------------------------------

    [Serializable]
    public class RigEntry
    {
        public string key;
        public Rig rig;
    }

    [Tooltip("InspectorでRigとそのキーを登録します。")]
    [SerializeField]
    private List<RigEntry> rigEntries = new List<RigEntry>();

    [Tooltip("フェードイン/アウトにかかるデフォルトの秒数")]
    [SerializeField]
    private float defaultFadeDuration = 0.5f;

    // --------------------------------------------------------------------
    //  ランタイムのDictionary
    // --------------------------------------------------------------------

    /// <summary>
    /// Rigをキーで管理するDictionary。AwakeでrigEntriesから生成されます。
    /// </summary>
    private Dictionary<string, Rig> rigDictionary = new Dictionary<string, Rig>();

    /*
    // 【Odin Inspector ユーザー向け】
    // もしOdin Inspectorを確実にお持ちの場合、上記のListとAwake()を削除し、
    // 代わりに以下の1行のコメントを外してください。より直接的に編集できます。
    [ShowInInspector]
    public Dictionary<string, Rig> rigDictionary = new Dictionary<string, Rig>();
    */


    // --------------------------------------------------------------------
    //  初期化
    // --------------------------------------------------------------------

    private void Awake()
    {
        // Inspectorで設定されたListを、高速アクセスのためにDictionaryに変換
        foreach (var entry in rigEntries)
        {
            if (entry.rig == null || string.IsNullOrEmpty(entry.key))
            {
                Debug.LogWarning($"キーまたはRigが未設定の項目があります。", this);
                continue;
            }

            if (!rigDictionary.ContainsKey(entry.key))
            {
                rigDictionary.Add(entry.key, entry.rig);
            }
            else
            {
                Debug.LogWarning($"Rigのキー '{entry.key}' が重複しています。無視されました。", this);
            }
        }
    }

    // --------------------------------------------------------------------
    //  公開メソッド (FadeIn / FadeOut)
    // --------------------------------------------------------------------

    /// <summary>
    /// 指定したキーのRigのWeightを 1.0 にします。
    /// </summary>
    /// <param name="key">フェードインさせたいRigのキー</param>
    public void FadeIn(string key)
    {
        FadeWeight(key, 1f, defaultFadeDuration);
    }

    /// <summary>
    /// 指定したキーのRigのWeightを 0.0 にします。
    /// </summary>
    /// <param name="key">フェードアウトさせたいRigのキー</param>
    public void FadeOut(string key)
    {
        FadeWeight(key, 0f, defaultFadeDuration);
    }

    /// <summary>
    /// (オーバーロード) フェード時間を指定してFadeInします。
    /// </summary>
    public void FadeIn(string key, float duration)
    {
        FadeWeight(key, 1f, duration);
    }

    /// <summary>
    /// (オーバーロード) フェード時間を指定してFadeOutします。
    /// </summary>
    public void FadeOut(string key, float duration)
    {
        FadeWeight(key, 0f, duration);
    }

    // --------------------------------------------------------------------
    //  内部処理
    // --------------------------------------------------------------------

    /// <summary>
    /// RigのWeightをTwwenさせる共通処理
    /// </summary>
    private void FadeWeight(string key, float targetWeight, float duration)
    {
        if (rigDictionary.TryGetValue(key, out Rig targetRig))
        {
            if (targetRig == null)
            {
                Debug.LogError($"キー '{key}' のRigがNullです。", this);
                return;
            }

            // 既存のTweenを停止（連打された場合などに備える）
            // Rigコンポーネント自体をIDとして使います
            DOTween.Kill(targetRig);

            // DOTween.To() を使って weight プロパティを直接Tween
            DOTween.To(
                () => targetRig.weight,      // ゲッター (現在の値)
                (x) => targetRig.weight = x, // セッター (値を設定)
                targetWeight,                // ターゲットの値
                duration                     // 変化にかかる時間
            )
            .SetEase(Ease.OutQuad) // スムーズなイージング
            .SetId(targetRig);     // このTweenのIDをRigコンポーネント自体に設定
        }
        else
        {
            Debug.LogWarning($"キー '{key}' に一致するRigが見つかりません。", this);
        }
    }

    /// <summary>
    /// このコンポーネントが破棄される際に、関連するTweenをすべて停止します。
    /// </summary>
    private void OnDestroy()
    {
        foreach (var pair in rigDictionary)
        {
            if (pair.Value != null)
            {
                DOTween.Kill(pair.Value);
            }
        }
    }
}