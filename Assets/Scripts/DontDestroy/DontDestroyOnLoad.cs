using UnityEngine;
using UnityEngine.SceneManagement; // シーン管理のために追加
using System.Collections.Generic; // Listを使うために追加

public class DODontDestroyOnLoad : MonoBehaviour
{
    // ▼▼▼ ここから追加 ▼▼▼
    [Header("自己破棄設定")]
    [Tooltip("このリストに含まれるシーンに移動した際、このオブジェクト自体を破棄します")]
    public List<string> destroyOnLoadScenes = new List<string>();
    // ▲▲▲ ここまで追加 ▲▲▲

    void Start()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    // ▼▼▼ ここから追加 ▼▼▼
    private void OnEnable()
    {
        // シーンがロードされた時のイベントに関数を登録
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // オブジェクトが無効になった時（破棄される時など）にイベントから関数を解除
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// シーンがロードされたときに呼び出されるメソッド
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ロードされたシーンの名前が、破棄リストに含まれているかチェック
        if (destroyOnLoadScenes.Contains(scene.name))
        {
            Debug.Log($"[{gameObject.name}] 破棄対象シーン '{scene.name}' に移動したため、自身を破棄します。");
            
            // ※重要※
            // このオブジェクトを破棄する前に、必ずイベント購読を解除します
            SceneManager.sceneLoaded -= OnSceneLoaded; 
            
            // このゲームオブジェクトを破棄
            Destroy(gameObject);
        }
    }
    // ▲▲▲ ここまで追加 ▲▲▲

    // void Update()
    // {
        
    // }
}