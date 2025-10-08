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

    [Header("フェード設定")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;
    [Tooltip("シーンロード後、TimelineのAnimatorを探しに行くまでの待機時間")]
    public float checkDelay = 0.1f;

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

        // ▼▼▼ ここを修正 ▼▼▼
        // Awakeは初期状態（真っ暗）を設定するだけにする。
        // 実際の明転処理はOnSceneLoadedに任せる。
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
            fadeCanvasGroup.blocksRaycasts = true; // 最初のフレームでの誤操作を防ぐ
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[SceneManagerController] OnSceneLoaded: '{scene.name}' がロードされました。");
        // このチェックはゲーム起動時とシーン遷移後の両方で機能する
        if (fadeCanvasGroup.alpha >= 1.0f)
        {
            Debug.Log("[SceneManagerController] 画面が暗転状態のため、明転処理を開始します。");
            StartCoroutine(CheckForTimelineFadeAndFadeOut());
        }
    }

    public void LoadScene(string sceneName)
    {
        Debug.Log($"[SceneManagerController] LoadScene: '{sceneName}' のロードをリクエストされました。");
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneManagerController] ロードするシーン名が空です。");
            onLoadFailed?.Invoke();
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
        Debug.Log("[SceneManagerController] CheckForTimelineFade: 明転が完了しました。シーン遷移完了。");
    }
    
    public void LoadSceneImmediate(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            onLoadFailed?.Invoke();
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