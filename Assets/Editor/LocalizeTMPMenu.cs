using UnityEditor;
using TMPro; // TextMeshPro のために必要
using UnityEngine;
using UnityEditor.Events; // SerializedProperty を操作するために必要
using UnityEngine.Events; // PersistentListenerMode のために必要

/// <summary>
/// TextMeshProUGUI のコンテキストメニュー（...）に
/// "Localize Font (Auto-Setup)" を追加するエディタ拡張
/// </summary>
public class LocalizeTMPFontMenu
{
    // "CONTEXT/" は、特定のコンポーネントのメニューに追加するおまじないです
    [MenuItem("CONTEXT/TextMeshProUGUI/Localize Font (Auto-Setup)")]
    private static void LocalizeFont(MenuCommand command)
    {
        // 1. ユーザーがクリックした TextMeshProUGUI コンポーネントを取得
        TextMeshProUGUI tmp = command.context as TextMeshProUGUI;
        if (tmp == null) return;

        GameObject go = tmp.gameObject;

        // 2. LocalizedFontAssetEvent コンポーネントを取得（なければ追加）
        LocalizedFontAssetEvent locEvent = go.GetComponent<LocalizedFontAssetEvent>();
        if (locEvent == null)
        {
            locEvent = Undo.AddComponent<LocalizedFontAssetEvent>(go);
        }

        // 3. SerializedObject 経由で m_UpdateAsset にアクセス
        SerializedObject so = new SerializedObject(locEvent);
        
        // LocalizedAssetEvent の正しいフィールド名は "m_UpdateAsset"
        SerializedProperty updateEventProperty = so.FindProperty("m_UpdateAsset");

        if (updateEventProperty == null)
        {
            Debug.LogError("Failed to find 'm_UpdateAsset' property in LocalizedFontAssetEvent. Maybe the Localization package version changed?");
            return;
        }

        // リスナーのリスト (m_PersistentCalls.m_Calls) を取得
        SerializedProperty persistentCalls = updateEventProperty.FindPropertyRelative("m_PersistentCalls.m_Calls");
        
        // 4. 既に紐付けられていないかチェック
        bool alreadyExists = false;
        for (int i = 0; i < persistentCalls.arraySize; i++)
        {
            SerializedProperty call = persistentCalls.GetArrayElementAtIndex(i);
            Object target = call.FindPropertyRelative("m_Target").objectReferenceValue;
            string methodName = call.FindPropertyRelative("m_MethodName").stringValue;

            // ▼▼▼ "set_font" が正しい（スクリーンショットの 'font' プロパティ） ▼▼▼
            if (target == tmp && methodName == "set_font")
            {
                alreadyExists = true;
                break;
            }
        }

        if (alreadyExists)
        {
            Debug.Log($"Localize event is already wired to {go.name}.", go);
            return; 
        }

        // 5. 新しいリスナーを手動で作成・設定
        
        int newListenerIndex = persistentCalls.arraySize;
        persistentCalls.arraySize++; 
        SerializedProperty newCall = persistentCalls.GetArrayElementAtIndex(newListenerIndex);

        newCall.FindPropertyRelative("m_Target").objectReferenceValue = tmp;
        
        // ▼▼▼ "set_font" が正しい（スクリーンショットの 'font' プロパティ） ▼▼▼
        newCall.FindPropertyRelative("m_MethodName").stringValue = "set_font";
        
        // ★★★ モードを「動的 (EventDefined)」に設定 ★★★
        newCall.FindPropertyRelative("m_Mode").enumValueIndex = (int)PersistentListenerMode.EventDefined;

        // 6. 変更を適用
        so.ApplyModifiedProperties();

        Debug.Log($"Successfully added and auto-wired LocalizedFontAssetEvent to {go.name}.", go);
    }
}