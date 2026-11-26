using System.Collections;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// シーン遷移を担当するシングルトン（通常はシーンをまたいで生き残る）。
///
/// 遷移の流れ:
///   1. 画面を暗転
///   2. 次のシーンを非同期ロード
///   3. ロード完了後、シーン側のタイムラインによるフェード演出が終わるのを待つ
///   4. 画面を明転
/// </summary>
[AddComponentMenu("Game/Managers/SceneManagerController")]
public class SceneManagerController : MonoBehaviour
{
    /// <summary>シーン側のタイムラインがフェード演出に使う Canvas の名前。</summary>
    private const string TimelineFadeCanvasName = "fadeInOutCanvas";

    /// <summary>非同期ロードは、シーンの有効化を止めている間 progress が 0.9 で止まる。</summary>
    private const float LoadReadyProgress = 0.9f;

    public static SceneManagerController Instance { get; private set; }

    /// <summary>最後に明転が完了した時刻（Time.realtimeSinceStartup）。</summary>
    public static float LastFadeOutCompletedTime { get; private set; } = -1f;

    /// <summary>暗転の開始から明転の完了までの間 true。</summary>
    public bool IsLoading { get; private set; } = false;

    // ===== Inspector =====

    [Header("フェード設定")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;
    [Tooltip("シーンロード後、TimelineのAnimatorを探しに行くまでの待機時間")]
    public float checkDelay = 2f;

    [Header("シーン履歴")]
    [Tooltip("ロードしたシーンの履歴（最新が末尾）")]
    public List<string> loadedSceneHistory = new List<string>();

    [Header("ロード開始 / 完了のフック")]
    public UnityEvent onLoadStarted;
    public UnityEvent onLoadCompleted;
    public UnityEvent onLoadFailed;

    [Header("動作設定")]
    [Tooltip("非同期ロード中にアクティブシーンを即時アンロードする（通常は true でOK）")]
    public bool unloadPreviousImmediately = true;
    public bool dontDestroyOnLoad = true;

    [Header("自己破棄設定")]
    [Tooltip("このリストに含まれるシーンに移動した際、このオブジェクト自体を破棄します")]
    public List<string> destroyOnLoadScenes = new List<string>();

    // ===== Unity ライフサイクル =====

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        // 起動直後は暗転した状態から始め、最初の明転が終わるまではロード中として扱う
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = true;
        }
        IsLoading = true;

        // 起動直後でもすぐポーズできるよう、少し過去の時刻で初期化しておく
        LastFadeOutCompletedTime = Time.realtimeSinceStartup - 1.0f;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (destroyOnLoadScenes.Contains(scene.name))
        {
            DestroySelf(scene.name);
            return;
        }

        Debug.Log($"[SceneManagerController] OnSceneLoaded: '{scene.name}' がロードされました。");

        if (fadeCanvasGroup.alpha >= 1.0f)
        {
            Debug.Log("[SceneManagerController] 画面が暗転状態のため、明転処理を開始します。");
            StartCoroutine(CheckForTimelineFadeAndFadeOut());
        }
        else
        {
            // 暗転を挟まないロード（LoadSceneImmediate など）は、ここで完了とする
            IsLoading = false;
        }
    }

    /// <summary>破棄対象のシーンに入ったとき、イベント購読とシングルトン参照を片付けて自分を破棄する。</summary>
    private void DestroySelf(string sceneName)
    {
        Debug.Log($"[SceneManagerController] 破棄対象シーン '{sceneName}' に移動したため、自身を破棄します。");

        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
        {
            Instance = null;
        }

        Destroy(gameObject);
    }

    // ===== 公開 API =====

    /// <summary>暗転 → 非同期ロード → 明転 の流れでシーンを切り替える。</summary>
    public void LoadScene(string sceneName)
    {
        IsLoading = true;
        Debug.Log($"[SceneManagerController] LoadScene: '{sceneName}' のロードをリクエストされました。");

        if (string.IsNullOrEmpty(sceneName))
        {
            FailLoad("[SceneManagerController] ロードするシーン名が空です。");
            return;
        }

        if (!IsSceneInBuildSettings(sceneName))
        {
            FailLoad($"[SceneManagerController] シーン '{sceneName}' がBuild Settingsに存在しません。");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(DoLoadSceneAsync(sceneName));
    }

    /// <summary>フェードなしで、その場でシーンを切り替える。</summary>
    public void LoadSceneImmediate(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        IsLoading = true;

        if (string.IsNullOrEmpty(sceneName))
        {
            onLoadFailed?.Invoke();
            IsLoading = false;
            return;
        }

        onLoadStarted?.Invoke();
        try
        {
            SceneManager.LoadScene(sceneName, mode);
            loadedSceneHistory.Add(sceneName);
            onLoadCompleted?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SceneManagerController] LoadSceneImmediate failed: {e.Message}");
            onLoadFailed?.Invoke();
            IsLoading = false;
        }
    }

    public void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ===== ロード処理 =====

    private static bool IsSceneInBuildSettings(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (Path.GetFileNameWithoutExtension(path) == sceneName) return true;
        }
        return false;
    }

    private void FailLoad(string errorMessage)
    {
        Debug.LogError(errorMessage);
        onLoadFailed?.Invoke();
        IsLoading = false;
    }

    /// <summary>暗転してから次のシーンをロードする。明転は OnSceneLoaded 側に引き継ぐ。</summary>
    private IEnumerator DoLoadSceneAsync(string sceneName)
    {
        onLoadStarted?.Invoke();

        Debug.Log("[SceneManagerController] DoLoadSceneAsync: 暗転を開始します。");
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            yield return fadeCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();
        }
        Debug.Log("[SceneManagerController] DoLoadSceneAsync: 暗転が完了しました。");

        Debug.Log($"[SceneManagerController] DoLoadSceneAsync: '{sceneName}' の非同期ロードを開始します。");
        var loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            FailLoad("[SceneManagerController] SceneManager.LoadSceneAsyncがnullを返しました。");
            yield break;
        }

        // 読み込みが終わるまでは、シーンの切り替え（有効化）を止めておく
        loadOperation.allowSceneActivation = false;
        while (loadOperation.progress < LoadReadyProgress)
        {
            yield return null;
        }
        Debug.Log($"[SceneManagerController] DoLoadSceneAsync: '{sceneName}' のロードが90%完了しました。");

        loadedSceneHistory.Add(sceneName);
        loadOperation.allowSceneActivation = true;
        Debug.Log($"[SceneManagerController] DoLoadSceneAsync: '{sceneName}' を有効化します。明転処理はOnSceneLoadedに引き継ぎます。");
        onLoadCompleted?.Invoke();
    }

    /// <summary>
    /// ロード後の明転。シーン側のタイムラインがフェード演出（fadeInOutCanvas の Animator）を
    /// 再生している場合は、それが終わるのを待ってから明転する。
    /// </summary>
    private IEnumerator CheckForTimelineFadeAndFadeOut()
    {
        Debug.Log("[SceneManagerController] CheckForTimelineFade: 連携チェックを開始します。");
        yield return new WaitForSeconds(checkDelay);

        GameObject timelineCanvas = GameObject.Find(TimelineFadeCanvasName);
        Animator timelineAnimator = timelineCanvas != null ? timelineCanvas.GetComponent<Animator>() : null;

        if (timelineAnimator != null && timelineAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
        {
            Debug.Log("[SceneManagerController] CheckForTimelineFade: 'fadeInOutCanvas'のAnimatorが再生中です。終了を待機します。");
            yield return new WaitUntil(() => timelineAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f);
            Debug.Log("[SceneManagerController] CheckForTimelineFade: Animatorの再生が完了しました。");
        }
        else
        {
            Debug.Log("[SceneManagerController] CheckForTimelineFade: 連携対象のAnimatorがないか、再生中ではありません。");
        }

        Debug.Log("[SceneManagerController] CheckForTimelineFade: 明転を開始します。");
        if (fadeCanvasGroup != null)
        {
            yield return fadeCanvasGroup.DOFade(0f, fadeDuration).WaitForCompletion();
            fadeCanvasGroup.blocksRaycasts = false;
        }

        LastFadeOutCompletedTime = Time.realtimeSinceStartup;
        IsLoading = false;

        Debug.Log("[SceneManagerController] CheckForTimelineFade: 明転が完了しました。シーン遷移完了。");
    }
}
