using UnityEngine;
using DG.Tweening; // DOTweenのnamespace

/// <summary>
/// DOTweenを使用して、Y軸（上下）にオブジェクトをループ移動させます。
/// Play()メソッドで開始でき、playOnStartフラグで自動開始も制御できます。
/// </summary>
public class YAxisLoopMover : MonoBehaviour
{
    [Header("移動設定")]
    [SerializeField]
    [Tooltip("開始位置からY軸（上）にどれだけ移動するか")]
    private float moveAmount = 1f;

    [SerializeField]
    [Tooltip("片道（例：下から上まで）にかかる時間（秒）")]
    private float duration = 2f;

    [Header("挙動設定")]
    [SerializeField]
    [Tooltip("移動のイージング（加減速のカーブ）")]
    private Ease easeType = Ease.InOutSine;

    [SerializeField]
    [Tooltip("開始時に遅延させる時間（秒）")]
    private float startDelay = 0f;

    [Header("自動再生")]
    [SerializeField]
    [Tooltip("trueの場合、Start()時に自動でアニメーションを開始します")]
    private bool playOnStart = true;

    private Tween moveTween; // トゥイーンを管理する変数
    private float startY; // アニメーションの基準となるY座標

    // Start()よりも先に呼ばれます
    void Awake()
    {
        // 起動時のY座標を「開始位置」として保存しておきます
        // これにより、Play()がいつ呼ばれても基準位置がズレません
        startY = transform.position.y;
    }

    void Start()
    {
        // playOnStart が true の場合のみ、自動で Play() を呼び出します
        if (playOnStart)
        {
            Play();
        }
    }

    /// <summary>
    /// Y軸のループ移動アニメーションを開始します。
    /// このメソッドは public なので、他のスクリプトやUnityEventから呼び出せます。
    /// </summary>
    public void Play()
    {
        // 既に実行中のTweenがある場合は、一度停止（Kill）します
        // (二重に実行されるのを防ぐため)
        if (moveTween != null && moveTween.IsActive())
        {
            moveTween.Kill();
        }

        // 1. 目標のY座標を計算 (保存しておいた開始時のY + moveAmount)
        float targetY = startY + moveAmount;

        // 2. DOMoveYでトゥイーンを作成
        moveTween = transform.DOMoveY(targetY, duration)
            .SetEase(easeType)
            .SetDelay(startDelay)
            .SetLoops(-1, LoopType.Yoyo); // 無限往復
    }

    /// <summary>
    /// 実行中のループ移動アニメーションを停止します。
    /// </summary>
    public void Stop()
    {
        if (moveTween != null && moveTween.IsActive())
        {
            moveTween.Kill();
        }
    }

    // オブジェクトが破棄されるときに呼ばれます
    private void OnDestroy()
    {
        // シーン遷移時などに安全にトゥイーンを停止します
        Stop();
    }
}
