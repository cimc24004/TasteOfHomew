using UnityEngine;
using UnityEngine.UI; // CanvasGroup を使うために必要
using DG.Tweening;  // DOTween を使うために必要
using Cysharp.Threading.Tasks; // UniTask (async/await) を使うために必要
using System; // Exception のために必要
using System.Threading; // CancellationToken のために必要

/// <summary>
/// CanvasGroupを指定時間だけフェードイン・表示・フェードアウトさせる
/// (DOTween連携パッケージ不要版)
/// </summary>
public class TutorialFader : MonoBehaviour
{
    [Header("ターゲットUI")]
    [Tooltip("フェードさせる対象の CanvasGroup")]
    [SerializeField] private CanvasGroup tutorialCanvasGroup;

    [Header("アニメーション設定")]
    [Tooltip("フェードインにかかる時間（秒）")]
    [SerializeField] private float fadeInDuration = 0.5f;
    
    [Tooltip("表示し続ける時間（秒）")]
    [SerializeField] private float displayDuration = 3.0f;
    
    [Tooltip("フェードアウトにかかる時間（秒）")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    // 実行中のアニメーションをキャンセルするためのトークン
    private CancellationTokenSource cts;

    // 実行中のTweenを外部からKillするための参照
    private Tween activeFadeInTween;
    private Tween activeFadeOutTween;

    private void Awake()
    {
        // CancellationTokenSource を初期化
        cts = new CancellationTokenSource();

        // 初期状態は非表示
        tutorialCanvasGroup.alpha = 0;
        tutorialCanvasGroup.blocksRaycasts = false;
        tutorialCanvasGroup.interactable = false;
    }

    private void OnDestroy()
    {
        // オブジェクトが破棄される時に、実行中のタスクとTweenをキャンセル
        cts?.Cancel();
        cts?.Dispose();
        activeFadeInTween?.Kill();
        activeFadeOutTween?.Kill();
    }

    /// <summary>
    /// チュートリアルUIを表示し、指定時間後に自動で隠します。
    /// </summary>
    [ContextMenu("Test Show Tutorial")]
    public void ShowTutorial()
    {
        // 既に実行中のフェードがあれば、それをキャンセルして殺す
        cts?.Cancel();
        cts?.Dispose();
        activeFadeInTween?.Kill();
        activeFadeOutTween?.Kill();
        
        // 新しいCancellationTokenSourceを作成
        cts = new CancellationTokenSource();

        // 非同期処理を開始（呼び出し元は待機しない）
        ShowTutorialAsync(cts.Token).Forget();
    }

    /// <summary>
    /// 実際のフェード処理を行う非同期メソッド
    /// </summary>
    private async UniTaskVoid ShowTutorialAsync(CancellationToken token)
    {
        // CancellationToken が発火した時に Tween も Kill するように登録
        var registration = token.Register(() => {
            activeFadeInTween?.Kill();
            activeFadeOutTween?.Kill();
        });

        try
        {
            // 1. 念のためアルファ値を0にし、オブジェクトを有効化
            tutorialCanvasGroup.alpha = 0f;
            tutorialCanvasGroup.gameObject.SetActive(true);
            tutorialCanvasGroup.interactable = true;
            tutorialCanvasGroup.blocksRaycasts = true;

            // 2. フェードイン
            // UniTaskCompletionSourceを使って、DOTweenのOnCompleteをawait可能にする
            var fadeInTcs = new UniTaskCompletionSource();
            
            activeFadeInTween = tutorialCanvasGroup.DOFade(1f, fadeInDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => fadeInTcs.TrySetResult())  // 完了したらTCSに通知
                .OnKill(() => fadeInTcs.TrySetCanceled()); // KillされたらTCSをキャンセル

            await fadeInTcs.Task; // TCSが完了（OnComplete）するのを待つ

            // 3. 一定時間待機 (これは UniTask 標準の機能)
            await UniTask.Delay(System.TimeSpan.FromSeconds(displayDuration), ignoreTimeScale: false, cancellationToken: token);

            // 4. フェードアウト
            var fadeOutTcs = new UniTaskCompletionSource();
            
            activeFadeOutTween = tutorialCanvasGroup.DOFade(0f, fadeOutDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() => fadeOutTcs.TrySetResult())
                .OnKill(() => fadeOutTcs.TrySetCanceled());
                
            await fadeOutTcs.Task; // TCSが完了（OnComplete）するのを待つ

            // 5. 非表示にして操作も受け付けないようにする
            tutorialCanvasGroup.interactable = false;
            tutorialCanvasGroup.blocksRaycasts = false;
        }
        catch (OperationCanceledException)
        {
            // ShowTutorial が再度呼ばれてキャンセルされた場合
            // Debug.Log("チュートリアル表示がキャンセルされました");
        }
        catch (Exception ex)
        {
            if (this == null) return; // オブジェクト破棄による例外は無視
            Debug.LogError($"チュートリアル表示中にエラー: {ex.Message}", this);
        }
        finally
        {
            // 登録したCancellationTokenのコールバックを解除（重要）
            registration.Dispose();
        }
    }
}