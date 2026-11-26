using UnityEngine;
using UnityEditor;
using UnityEngine.Audio; // AudioMixerGroup を扱うために必要
using System.Collections.Generic;
using UnityEngine.SceneManagement; // シーン操作に必要
using UnityEditor.SceneManagement; // シーンの変更をマークするために必要
using System.Linq; // フォルダ検索で使用

// Odin Inspector を使うための using
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;

/// <summary>
/// シーン上またはPrefab内のすべての AudioSource に、
/// 指定した AudioMixerGroup を一括で割り当てるエディタ拡張ウィンドウ。
/// (Odin Inspector 版)
/// </summary>
// EditorWindow から OdinEditorWindow に変更
public class AudioMixerAssignerWindow : OdinEditorWindow
{
    // --- フィールド ---

    [Title("1. 割り当てる Group", "この Group がすべての AudioSource に割り当てられます。")]
    [Required(ErrorMessage = "割り当てる Audio Mixer Group を指定してください。")] // 必須項目
    [AssetsOnly] // プロジェクトのアセットのみ許可
    [SerializeField]
    private AudioMixerGroup targetGroup; // 割り当てたいMixer Group

    // --- シーン上の処理 ---

    [Title("2. シーン上の AudioSource")]
    [InfoBox("現在のシーンの全ての AudioSource に Group を割り当てます。\n(非アクティブなオブジェクトも含む)")]
    // [Button] 属性で、このメソッドをボタンとしてUIに描画する
    [Button("現在のシーン上の全 AudioSource に適用", ButtonSizes.Large)]
    [GUIColor(0.7f, 1f, 0.7f)] // ボタンの色を緑っぽく
    [PropertyOrder(1)] // 描画順序
    [EnableIf("targetGroup")] // targetGroup がセットされている時だけ押せる
    private void ProcessSceneAudioSourcesButton()
    {
        if (EditorUtility.DisplayDialog("最終確認 (シーン)",
            $"現在のシーンの全ての AudioSource に '{targetGroup.name}' を割り当てます。\n(非アクティブなオブジェクトも含む)\n\nこの操作は元に戻せます (Ctrl+Z)。実行しますか？",
            "実行", "キャンセル"))
        {
            ProcessSceneAudioSources();
        }
    }

    [Title("", "--- または ---", horizontalLine: true)]
    [PropertyOrder(2)]
    private readonly int separator = 0; // タイトル表示用のダミー

    // --- Prefabリストの処理 ---

    [Title("3. Prefabリスト上の AudioSource")]
    [InfoBox("ここにドラッグしたPrefabアセット内の全 AudioSource に Group を割り当てます。")]
    [SerializeField]
    [AssetsOnly] // プロジェクトのアセットのみ許可
    [ListDrawerSettings(Expanded = true, DraggableItems = true, ShowPaging = false)] // リストのUIを改善
    [PropertyOrder(3)]
    private List<GameObject> targetPrefabs = new List<GameObject>();

    [Button("リスト内の全Prefabに適用", ButtonSizes.Large)]
    [GUIColor(0.7f, 0.7f, 1f)] // ボタンの色を青っぽく
    [PropertyOrder(5)] // フォルダ機能より下に表示
    [EnableIf("CanProcessPrefabs")] // targetGroup があり、リストに1件以上ある場合
    private void ProcessPrefabAudioSourcesButton()
    {
         if (EditorUtility.DisplayDialog("最終確認 (Prefab)",
            $"リストにある {targetPrefabs.Count} 個のPrefabに含まれる AudioSource に '{targetGroup.name}' を割り当てます。\n*注意: Prefabアセットが直接変更されます！*\nこの操作は元に戻せます。実行しますか？",
            "実行", "キャンセル"))
        {
            ProcessPrefabAudioSources();
        }
    }

    // --- (新機能) フォルダから一括追加 ---

    [Title("4. フォルダからPrefabを一括追加 (オプション)")]
    [InfoBox("指定したフォルダ(とサブフォルダ)内の全Prefabを検索し、上のリストに追加します。")]
    [FolderPath] // フォルダパス専用の入力フィールド
    [SerializeField]
    [PropertyOrder(4)]
    private string targetFolder;

    [Button("フォルダ内のPrefabをリストに追加", ButtonSizes.Medium)]
    [PropertyOrder(4.1f)]
    [EnableIf("FolderIsSelected")] // フォルダが選択されている時だけ押せる
    private void AddPrefabsFromFolder()
    {
        if (string.IsNullOrEmpty(targetFolder) || !AssetDatabase.IsValidFolder(targetFolder))
        {
            Debug.LogError("有効なフォルダパスを指定してください。");
            return;
        }

        // フォルダ内の全PrefabのGUIDを検索
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { targetFolder });
        int addedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null && !targetPrefabs.Contains(prefab))
            {
                targetPrefabs.Add(prefab);
                addedCount++;
            }
        }

        Debug.Log($"'{targetFolder}' から {addedCount} 個のPrefabをリストに追加しました。");
    }

    // --- EnableIf (ボタンの有効化) 用のヘルパー ---
    private bool FolderIsSelected() => !string.IsNullOrEmpty(targetFolder) && AssetDatabase.IsValidFolder(targetFolder);
    private bool CanProcessPrefabs() => targetGroup != null && targetPrefabs != null && targetPrefabs.Count > 0;

    // --- ウィンドウの初期化 ---

    [MenuItem("Tools/Utilities/Audio Mixer Assigner (Odin)")] // メニュー名を変更
    public static void ShowWindow()
    {
        // ウィンドウを表示
        GetWindow<AudioMixerAssignerWindow>("Audio Mixer Assigner");
    }

    // OnGUI() や OnEnable() は OdinEditorWindow が自動で処理するため不要
    // protected override void OnGUI()
    // {
    //     base.OnGUI(); // これだけでOdinが↑の属性を解釈して描画してくれる
    // }

    // --- 処理ロジック (ここの中身は前回のスクリプトと同一です) ---

    /// <summary>
    /// 現在のシーン上のすべてのAudioSourceを処理する
    /// </summary>
    private void ProcessSceneAudioSources()
    {
        if (targetGroup == null)
        {
            Debug.LogError("Target Audio Mixer Group が設定されていません。");
            return;
        }

        int count = 0;
        List<AudioSource> allSourcesInScene = new List<AudioSource>();

        // 現在のシーンの全ルートオブジェクトを取得
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject root in rootObjects)
        {
            // true を指定して、非アクティブなものも含め、すべての子孫からAudioSourceを取得
            allSourcesInScene.AddRange(root.GetComponentsInChildren<AudioSource>(true));
        }

        if (allSourcesInScene.Count == 0)
        {
             Debug.Log("シーン上で AudioSource は見つかりませんでした。");
             return;
        }

        // Undo（元に戻す）操作のために、変更する可能性のある全オブジェクトを記録
        Undo.RecordObjects(allSourcesInScene.ToArray(), "Assign Audio Mixer Group in Scene");

        foreach (AudioSource source in allSourcesInScene)
        {
            // 既に設定されている場合はスキップ
            if (source.outputAudioMixerGroup != targetGroup)
            {
                source.outputAudioMixerGroup = targetGroup;
                // オブジェクトが変更されたことをUnityに通知
                EditorUtility.SetDirty(source);
                count++;
            }
        }

        // シーンが変更されたことをマーク（* がつく）
        if (count > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        Debug.Log($"シーン上の {count} 個の AudioSource に {targetGroup.name} を割り当てました。 (合計 {allSourcesInScene.Count} 個をスキャン)");
    }

    /// <summary>
    /// Prefabリスト内のすべてのAudioSourceを処理する
    /// </summary>
    private void ProcessPrefabAudioSources()
    {
        if (targetGroup == null)
        {
            Debug.LogError("Target Audio Mixer Group が設定されていません。");
            return;
        }

        int prefabCount = 0;
        int sourceCount = 0;
        List<Object> objectsToRecord = new List<Object>(); // Undo用リスト

        // --- Undo対象の事前スキャン ---
        foreach (var prefab in targetPrefabs)
        {
            if (prefab == null) continue;
            // Prefabアセットかどうかをチェック (シーン上のインスタンスは無視)
            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                // 非アクティブなものも含め、すべての子孫からAudioSourceを取得
                objectsToRecord.AddRange(prefab.GetComponentsInChildren<AudioSource>(true));
            }
        }

        if (objectsToRecord.Count == 0)
        {
             Debug.Log("Prefabリスト内で AudioSource は見つかりませんでした。");
             return;
        }

        // Undoのために変更する全オブジェクトを記録
        Undo.RecordObjects(objectsToRecord.ToArray(), "Assign Mixer Group to Prefabs");

        // --- 実際の処理 ---
        foreach (GameObject prefab in targetPrefabs)
        {
            if (prefab == null) continue;

            // 再度チェック (シーン上のインスタンスは無視)
            if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                Debug.LogWarning($"{prefab.name} はPrefabアセットではありません。シーン上のインスタンスは無視されます。");
                continue;
            }

            AudioSource[] audioSources = prefab.GetComponentsInChildren<AudioSource>(true);

            if (audioSources.Length > 0)
            {
                bool modified = false;
                foreach (AudioSource source in audioSources)
                {
                    if (source.outputAudioMixerGroup != targetGroup)
                    {
                        source.outputAudioMixerGroup = targetGroup;
                        // アセット内のコンポーネントが変更されたことを通知
                        EditorUtility.SetDirty(source);
                        modified = true;
                        sourceCount++;
                    }
                }

                if (modified)
                {
                    prefabCount++;
                    // Prefabアセット自体も変更されたことを通知
                    EditorUtility.SetDirty(prefab);
                }
            }
        }

        // Prefabアセットの変更をディスクに保存
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"リスト内の {prefabCount} 個のPrefabに含まれる {sourceCount} 個の AudioSource に {targetGroup.name} を割り当てました。");
    }
}
