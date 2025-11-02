using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System.IO;

[AddComponentMenu("Game/Managers/SceneManagerController")]
public class SceneManagerController : MonoBehaviour
{
    public static SceneManagerController Instance { get; private set; }

    // 最後に明転が完了した時刻（リアルタイム）
    public static float LastFadeOutCompletedTime { get; private set; } = -1f;

    // ロード中フラグ
    public bool IsLoading { get; private set; } = false;

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

    // ▼▼▼ ここから追加 ▼▼▼
    [Header("自己破棄設定")]
    [Tooltip("このリストに含まれるシーンに移動した際、このオブジェクト自体を破棄します")]
    public List<string> destroyOnLoadScenes = new List<string>();
    // ▲▲▲ ここまで追加 ▲▲▲

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

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = true; // 最初のフレームでの誤操作を防ぐ
        }
        
        // 起動直後はすぐにポーズできるよう、十分に過去の時刻で初期化
        LastFadeOutCompletedTime = Time.realtimeSinceStartup - 1.0f;
        
        // <<< ここが修正点 >>>
        // 起動時、最初の明転処理が完了するまではロード中とする
        IsLoading = true; 
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // OnDestroyなどで呼ばれる可能性があるので、nullチェックを追加
        if (Instance == this) 
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ▼▼▼ ここから修正・追加 ▼▼▼
        // 破棄リストに現在のシーン名が含まれているかチェック
        if (destroyOnLoadScenes.Contains(scene.name))
        {
            Debug.Log($"[SceneManagerController] 破棄対象シーン '{scene.name}' に移動したため、自身を破棄します。");
            
            // イベント購読を解除 (Destroy前に必ず行う)
            SceneManager.sceneLoaded -= OnSceneLoaded; 
            
            // シングルトン参照をクリア
            if (Instance == this)
            {
                Instance = null;
            }

            // このゲームオブジェクトを破棄
            Destroy(gameObject);
            
            // このオブジェクトは破棄されるため、以降の処理（フェードアウトなど）は行わない
            return; 
        }
        // ▲▲▲ ここまで修正・追加 ▲▲▲


        Debug.Log($"[SceneManagerController] OnSceneLoaded: '{scene.name}' がロードされました。");
        if (fadeCanvasGroup.alpha >= 1.0f)
        {
            Debug.Log("[SceneManagerController] 画面が暗転状態のため、明転処理を開始します。");
            StartCoroutine(CheckForTimelineFadeAndFadeOut());
        }
        else
        {
            // LoadSceneImmediateなどでフェードが1fでない場合（同期ロードなど）
            IsLoading = false;
        }
    }

    public void LoadScene(string sceneName)
    {
        IsLoading = true; // ロード開始
        Debug.Log($"[SceneManagerController] LoadScene: '{sceneName}' のロードをリクエストされました。");
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneManagerController] ロードするシーン名が空です。");
            onLoadFailed?.Invoke();
            IsLoading = false; // ロード失敗
            return;
        }

        bool sceneExists = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (Path.GetFileNameWithoutExtension(path) == sceneName)
            {
                sceneExists = true;
                break;
            }
        }
        if (!sceneExists)
        {
            Debug.LogError($"[SceneManagerController] シーン '{sceneName}' がBuild Settingsに存在しません。");
            onLoadFailed?.Invoke();
            IsLoading = false; // ロード失敗
            return;
        }

        StopAllCoroutines();
        StartCoroutine(DoLoadSceneAsync(sceneName));
    }

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
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (op == null)
        {
            Debug.LogError("[SceneManagerController] SceneManager.LoadSceneAsyncがnullを返しました。");
            onLoadFailed?.Invoke();
            IsLoading = false; // ロード失敗
            yield break;
        }

        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            yield return null;
        }
        Debug.Log($"[SceneManagerController] DoLoadSceneAsync: '{sceneName}' のロードが90%完了しました。");

        loadedSceneHistory.Add(sceneName);
        op.allowSceneActivation = true;
        Debug.Log($"[SceneManagerController] DoLoadSceneAsync: '{sceneName}' を有効化します。明転処理はOnSceneLoadedに引き継ぎます。");
        onLoadCompleted?.Invoke();
    }

    private IEnumerator CheckForTimelineFadeAndFadeOut()
    {
        Debug.Log("[SceneManagerController] CheckForTimelineFade: 連携チェックを開始します。");
        yield return new WaitForSeconds(checkDelay);

        GameObject timelineCanvasObj = GameObject.Find("fadeInOutCanvas");
        Animator timelineAnimator = null;

        if (timelineCanvasObj != null)
        {
            timelineAnimator = timelineCanvasObj.GetComponent<Animator>();
        }

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
        
        // 明転が完了した時刻を記録
        LastFadeOutCompletedTime = Time.realtimeSinceStartup;
        IsLoading = false; // すべてのロード処理が完了

        Debug.Log("[SceneManagerController] CheckForTimelineFade: 明転が完了しました。シーン遷移完了。");
    }
    
    public void LoadSceneImmediate(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        IsLoading = true; // ロード開始
        if (string.IsNullOrEmpty(sceneName))
        {
            onLoadFailed?.Invoke();
            IsLoading = false; // ロード失敗
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
            IsLoading = false; // ロード失敗
        }
    }

    public void ReloadCurrentScene()
    {
        var cur = SceneManager.GetActiveScene();
        LoadScene(cur.name);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}