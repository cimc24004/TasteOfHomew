// Assets/Editor/LODGroupRemover.cs
// 対象スコープ内の LODGroup を削除し、各 LOD に割り当てられている Renderer の GameObject も削除する
// メニュー: Tools/LOD Group/Remove (Delete LOD Objects) ...

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

#if UNITY_2019_1_OR_NEWER
using UnityEditor.SceneManagement;
using PrefabStage = UnityEditor.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.SceneManagement.PrefabStageUtility;
#else
using UnityEditor.Experimental.SceneManagement;
using PrefabStage = UnityEditor.Experimental.SceneManagement.PrefabStage;
using PrefabStageUtility = UnityEditor.Experimental.SceneManagement.PrefabStageUtility;
#endif

public static class LODGroupRemover
{
    // ===== Prefab モード配下 =====
    [MenuItem("Tools/LOD Group/Remove (Delete LOD Objects) in Current Prefab Stage")]
    public static void RemoveInCurrentPrefabStage()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null)
        {
            EditorUtility.DisplayDialog("LODGroup Remover", "Prefabモードで開いてから実行してね。", "OK");
            return;
        }

        var root = stage.prefabContentsRoot;
        ProcessScope(
            roots: new[] { root },
            scopeLabel: $"Prefab: {System.IO.Path.GetFileName(stage.assetPath)}",
            excludeScopeRootsFromDeletion: true,   // 予期せぬルート全消し防止
            onDone: () =>
            {
                AssetDatabase.SaveAssets();
            }
        );
    }

    [MenuItem("Tools/LOD Group/Remove (Delete LOD Objects) in Current Prefab Stage", true)]
    private static bool Validate_RemoveInCurrentPrefabStage()
        => PrefabStageUtility.GetCurrentPrefabStage() != null;

    // ===== 選択オブジェクト配下 =====
    [MenuItem("Tools/LOD Group/Remove (Delete LOD Objects) in Selection")]
    public static void RemoveInSelection()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("LODGroup Remover", "オブジェクトを選択してね。", "OK");
            return;
        }

        // 選択配下に LODGroup が1つも無いなら何もしない
        bool any = false;
        foreach (var r in selected)
        {
            if (!r) continue;
            if (r.GetComponentInChildren<LODGroup>(true))
            {
                any = true; break;
            }
        }
        if (!any)
        {
            EditorUtility.DisplayDialog("LODGroup Remover", "選択配下に LODGroup が見つからない。", "OK");
            return;
        }

        ProcessScope(
            roots: selected,
            scopeLabel: "Selection",
            excludeScopeRootsFromDeletion: true,   // 選択ルートそのものは削除しない（安全策）
            onDone: AssetDatabase.SaveAssets
        );
    }

    [MenuItem("Tools/LOD Group/Remove (Delete LOD Objects) in Selection", true)]
    private static bool Validate_RemoveInSelection()
        => Selection.gameObjects != null && Selection.gameObjects.Length > 0;

    // ===== 実体 =====
    private static void ProcessScope(GameObject[] roots, string scopeLabel, bool excludeScopeRootsFromDeletion, System.Action onDone)
    {
        // スコープ内の LODGroup を収集
        var lodGroups = new List<LODGroup>();
        var scopeRootSet = new HashSet<GameObject>(roots);
        foreach (var r in roots)
        {
            if (!r) continue;
            lodGroups.AddRange(r.GetComponentsInChildren<LODGroup>(true));
        }

        if (lodGroups.Count == 0)
        {
            EditorUtility.DisplayDialog("LODGroup Remover", "対象の LODGroup が見つからなかった。", "OK");
            return;
        }

        // 各 LODGroup が参照している Renderer の GameObject を収集（重複除去）
        var toDelete = new HashSet<GameObject>();
        foreach (var g in lodGroups)
        {
            if (!g) continue;

            try
            {
                var lods = g.GetLODs();
                foreach (var lod in lods)
                {
                    var renderers = lod.renderers;
                    if (renderers == null) continue;
                    foreach (var r in renderers)
                    {
                        if (!r) continue;

                        var go = r.gameObject;
                        if (!go) continue;

                        // スコープ外のオブジェクトは無視（万が一、参照が外に飛んでいる場合）
                        if (!IsInsideAnyScope(go.transform, roots)) continue;

                        // 安全策：スコープのルート（Prefab ルートや選択ルート）は削除対象から除外
                        if (excludeScopeRootsFromDeletion && scopeRootSet.Contains(go)) continue;

                        toDelete.Add(go);
                    }
                }
            }
            catch { /* 取れないケースはそのグループはスキップ */ }
        }

        // 深い階層から削除するため、Transform 深度で降順ソート
        var listToDelete = new List<GameObject>(toDelete);
        listToDelete.Sort((a, b) => GetDepth(b).CompareTo(GetDepth(a)));

        Undo.IncrementCurrentGroup();

        int removedGroups = 0;
        int deletedObjects = 0;

        try
        {
            // 1) 先に LODGroup コンポーネントを削除（ホストGOは残す）
            for (int i = 0; i < lodGroups.Count; i++)
            {
                var g = lodGroups[i];
                if (!g) continue;

                if (i % 20 == 0)
                    EditorUtility.DisplayProgressBar("Removing LODGroup", g.name, (float)i / Mathf.Max(1, lodGroups.Count));

                Undo.DestroyObjectImmediate(g);
                removedGroups++;
            }

            // 2) LOD に割り当てられていたオブジェクト（Renderer のある GO）を削除
            for (int i = 0; i < listToDelete.Count; i++)
            {
                var go = listToDelete[i];
                if (!go) continue;

                if (i % 30 == 0)
                    EditorUtility.DisplayProgressBar("Deleting LOD Objects", go.name, (float)i / Mathf.Max(1, listToDelete.Count));

                Undo.DestroyObjectImmediate(go);
                deletedObjects++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        onDone?.Invoke();

        EditorUtility.DisplayDialog(
            "LODGroup Remover",
            $"{scopeLabel}\n" +
            $"削除した LODGroup: {removedGroups}\n" +
            $"削除した LOD オブジェクト: {deletedObjects}",
            "OK");
    }

    private static bool IsInsideAnyScope(Transform t, GameObject[] roots)
    {
        foreach (var r in roots)
        {
            if (!r) continue;
            if (t == r.transform || t.IsChildOf(r.transform)) return true;
        }
        return false;
    }

    private static int GetDepth(GameObject go)
    {
        int d = 0;
        var t = go ? go.transform : null;
        while (t && t.parent)
        {
            d++;
            t = t.parent;
        }
        return d;
    }
}
#endif
