// SceneManagerController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[AddComponentMenu("Game/Managers/SceneManagerController")]
public class SceneManagerController : MonoBehaviour
{
    public static SceneManagerController Instance { get; private set; }

    [Header("ロード開始 / 完了のフック（Inspectorでアサインして使って）")]
    public UnityEvent onLoadStarted;
    public UnityEvent onLoadCompleted;
    public UnityEvent onLoadFailed; // シーン名が不正なとき等に呼ぶ

    [Header("動作設定")]
    [Tooltip("非同期ロード中にアクティブシーンを即時アンロードする（通常は true でOK）")]
    public bool unloadPreviousImmediately = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// シーン名でシーンをロードする。非同期でロードして完了時に onLoadCompleted を呼ぶ。
    /// シーン名が Build Settings に登録されていなければ onLoadFailed を呼ぶだけ。
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SceneManagerController] LoadScene: sceneName is null or empty.");
            onLoadFailed?.Invoke();
            return;
        }

        // 簡易チェック：ビルドに存在するか（名前ベース）
        bool existsInBuild = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
            {
                existsInBuild = true;
                break;
            }
        }

        if (!existsInBuild)
        {
            Debug.LogWarning($"[SceneManagerController] LoadScene: scene '{sceneName}' not found in Build Settings.");
            onLoadFailed?.Invoke();
            return;
        }

        StopAllCoroutines();
        StartCoroutine(DoLoadSceneAsync(sceneName));
    }

    /// <summary>
    /// 同期で即ロードしたい場合はこれを使う（基本は非同期版を推奨）
    /// </summary>
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
            onLoadCompleted?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SceneManagerController] LoadSceneImmediate failed: {e.Message}");
            onLoadFailed?.Invoke();
        }
    }

    private IEnumerator DoLoadSceneAsync(string sceneName)
    {
        onLoadStarted?.Invoke();

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (op == null)
        {
            Debug.LogWarning("[SceneManagerController] LoadSceneAsync returned null.");
            onLoadFailed?.Invoke();
            yield break;
        }

        // Optionally prevent scene activation until ready:
        // op.allowSceneActivation = false;
        // (if you want a loading screen and to control activation, manage allowSceneActivation)

        // wait until finished
        while (!op.isDone)
        {
            // 進捗は op.progress (0.0 - 0.9) / op.isDone
            yield return null;
        }

        // 完了
        onLoadCompleted?.Invoke();
    }

    /// <summary>
    /// 現在のシーンを再読み込みする簡易ヘルパ
    /// </summary>
    public void ReloadCurrentScene()
    {
        var cur = SceneManager.GetActiveScene();
        LoadScene(cur.name);
    }

    /// <summary>
    /// ゲーム終了（エディタ中は停止）
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
