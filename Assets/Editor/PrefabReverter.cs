// Assets/Editor/RevertRemovedComponents.cs
#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RevertRemovedComponentsTool
{
    [MenuItem("Tools/Prefab/Revert Removed Components in Selection %#&c")] // Ctrl+Shift+Alt+C
    public static void RevertRemovedComponentsInSelection()
    {
        var targets = Selection.gameObjects;
        if (targets == null || targets.Length == 0)
        {
            EditorUtility.DisplayDialog("Revert Removed Components", "オブジェクトを選択してね。", "OK");
            return;
        }

        int revertedCount = 0;
        Undo.IncrementCurrentGroup();

        try
        {
            foreach (var goRoot in targets)
            {
                if (!goRoot) continue;

                // 自身と子供を走査
                foreach (var t in goRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (!t) continue;
                    var obj = t.gameObject;

                    // GetRemovedComponents は List<RemovedComponent> 等を返す（環境差あり）。
                    var removed = PrefabUtility.GetRemovedComponents(obj);
                    if (removed == null) continue;

                    // removed は IEnumerable なので foreach で回す（要素は struct/object）
                    foreach (var rc in (IEnumerable)removed)
                    {
                        if (rc == null) continue;

                        // rc の中から Component 型のフィールド/プロパティを探す（リフレクションで安全に取得）
                        Component comp = null;

                        var rcType = rc.GetType();

                        // 1) public/non-public フィールド
                        var fields = rcType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        foreach (var f in fields)
                        {
                            if (typeof(Component).IsAssignableFrom(f.FieldType))
                            {
                                comp = f.GetValue(rc) as Component;
                                if (comp != null) break;
                            }
                        }

                        // 2) 見つからなければプロパティもチェック
                        if (comp == null)
                        {
                            var props = rcType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            foreach (var p in props)
                            {
                                if (!p.CanRead) continue;
                                if (typeof(Component).IsAssignableFrom(p.PropertyType))
                                {
                                    try
                                    {
                                        comp = p.GetValue(rc, null) as Component;
                                    }
                                    catch { comp = null; }
                                    if (comp != null) break;
                                }
                            }
                        }

                        // 見つかった Component を使って Revert
                        if (comp != null)
                        {
                            Undo.RecordObject(obj, "Revert Removed Component");

                            // API の違いに備えて2パターン試す（存在しない場合は例外をキャッチ）
                            try
                            {
                                // 多くのバージョンで有るシグネチャ： (GameObject, Component, InteractionMode)
                                PrefabUtility.RevertRemovedComponent(obj, comp, InteractionMode.UserAction);
                                revertedCount++;
                            }
                            catch (MissingMethodException)
                            {
                                // まれに別シグネチャの場合があるので、その場合は別の呼び出しを試す（古い/新しいAPIに合わせる）
                                try
                                {
                                    // もし (RemovedComponent, InteractionMode) みたいなのがあるならそれを探して呼ぶ（リフレクション）
                                    var mi = typeof(PrefabUtility).GetMethod("RevertRemovedComponent", BindingFlags.Static | BindingFlags.Public);
                                    if (mi != null)
                                    {
                                        // 何か別のシグネチャでも呼べる可能性があるので、パラメータ数に合わせて invoke を試す
                                        var ps = mi.GetParameters();
                                        if (ps.Length == 2)
                                        {
                                            // assume: (RemovedComponent, InteractionMode)
                                            mi.Invoke(null, new object[] { rc, InteractionMode.UserAction });
                                            revertedCount++;
                                        }
                                        else if (ps.Length == 1)
                                        {
                                            // unlikely, but try with rc only
                                            mi.Invoke(null, new object[] { rc });
                                            revertedCount++;
                                        }
                                        // else: give up for this rc
                                    }
                                }
                                catch
                                {
                                    // 無視して次へ
                                }
                            }
                            catch (Exception ex)
                            {
                                // 何か別エラーが出ても続行（ログだけ出す）
                                Debug.LogWarning($"RevertRemovedComponent failed for {obj.name}: {ex.Message}");
                            }
                        }
                        else
                        {
                            // comp が見つからないケースはログ（デバッグ用）
                            // Debug.Log($"No Component field found in RemovedComponent struct for object {obj.name}");
                        }
                    } // foreach rc
                } // foreach transform
            } // foreach root
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog(
            "Revert Removed Components",
            $"Reverted Removed Components: {revertedCount}",
            "OK");
    }

    [MenuItem("Tools/Prefab/Revert Removed Components in Selection %#&c", true)]
    public static bool ValidateRevertRemovedComponentsInSelection()
    {
        var targets = Selection.gameObjects;
        if (targets == null || targets.Length == 0) return false;

        foreach (var goRoot in targets)
        {
            if (!goRoot) continue;
            foreach (var t in goRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!t) continue;
                var obj = t.gameObject;
                var removed = PrefabUtility.GetRemovedComponents(obj);
                if (removed != null)
                {
                    // ICollection として要素があれば true
                    if (removed is ICollection coll && coll.Count > 0) return true;

                    // IEnumerable として列挙して空でないか確認
                    foreach (var _ in (IEnumerable)removed) return true;
                }
            }
        }
        return false;
    }
}
#endif
