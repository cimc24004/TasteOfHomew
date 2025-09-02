// Assets/Editor/MissingScriptCleaner.cs
// Prefabモード中のPrefab配下から Missing Script を一括削除
// Ctrl+Shift+M で実行（メニュー：Tools/Missing Script/Remove in Current Prefab Stage）

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

// PrefabStage / PrefabStageUtility の名前空間はバージョンで違うので吸収
#if UNITY_2019_1_OR_NEWER
using UnityEditor.SceneManagement;
using PrefabStage = UnityEditor.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.SceneManagement.PrefabStageUtility;
#else
using UnityEditor.Experimental.SceneManagement;
using PrefabStage = UnityEditor.Experimental.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.Experimental.SceneManagement.PrefabStageUtility;
#endif

public static class MissingScriptCleaner
{
    [MenuItem("Tools/Missing Script/Remove in Current Prefab Stage %#m")]
    public static void RemoveInCurrentPrefabStage()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null)
        {
            EditorUtility.DisplayDialog("Missing Script Cleaner",
                "今は Prefab モードじゃないよ。Prefab をダブルクリックで開いた状態で実行してね。",
                "OK");
            return;
        }

        var root = stage.prefabContentsRoot;
        if (root == null)
        {
            EditorUtility.DisplayDialog("Missing Script Cleaner",
                "prefabContentsRoot が見つからなかった。", "OK");
            return;
        }

        // Undo と進捗バー
        Undo.RegisterFullObjectHierarchyUndo(root, "Remove Missing Scripts (Prefab Stage)");
        var transforms = root.GetComponentsInChildren<Transform>(true); // 非アクティブ含む
        int totalRemoved = 0;

        try
        {
            for (int i = 0; i < transforms.Length; i++)
            {
                var go = transforms[i].gameObject;
                // UnityEditor.GameObjectUtility にお任せ（Undo 対応 & 安全）
                totalRemoved += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

                if (i % 50 == 0)
                {
                    float p = (float)i / Mathf.Max(1, transforms.Length);
                    EditorUtility.DisplayProgressBar("Removing Missing Scripts", go.name, p);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // Prefab を保存（対応バージョンのみ）
        try
        {

        }
        catch { /* 失敗しても致命ではないので握りつぶし */ }

        EditorUtility.DisplayDialog(
            "Missing Script Cleaner",
            $"Prefab: {System.IO.Path.GetFileName(stage.assetPath)}\n" +
            $"対象オブジェクト数: {transforms.Length}\n" +
            $"削除した Missing Script: {totalRemoved}",
            "OK");
    }

    // メニュー有効化：Prefabモードのときだけ押せる
    [MenuItem("Tools/Missing Script/Remove in Current Prefab Stage %#m", true)]
    public static bool Validate_RemoveInCurrentPrefabStage()
    {
        return PrefabStageUtility.GetCurrentPrefabStage() != null;
    }
}
#endif
