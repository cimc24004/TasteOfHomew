using UnityEngine;
using UnityEngine.EventSystems; // EventSystemを扱うために必須

/// <summary>
/// EventSystemで現在選択されているUIオブジェクトを監視し、
/// 変更があった場合にのみコンソールにログを出力します。
/// ログはクリック可能です。
/// </summary>
public class CurrentSelectedLogger : MonoBehaviour
{
    // 1フレーム前の選択オブジェクトを記憶する変数
    private GameObject lastSelectedObject;

    void Update()
    {
        // 1. 現在のEventSystemを取得
        EventSystem currentEventSystem = EventSystem.current;
        if (currentEventSystem == null)
        {
            // EventSystemがシーンにない場合は何もしない
            return; 
        }

        // 2. 現在選択されているオブジェクトを取得
        GameObject currentSelected = currentEventSystem.currentSelectedGameObject;

        // 3. 1フレーム前と選択が変わったかチェック
        if (currentSelected != lastSelectedObject)
        {
            // 4. 選択対象が null (選択解除) の場合
            if (currentSelected == null)
            {
                Debug.Log($"[UI Selection] -> null (選択が解除されました)");
            }
            // 5. 新しいオブジェクトが選択された場合
            else
            {
                // ★最重要★
                // Debug.Log() の第2引数に context (GameObject) を渡すことで、
                // コンソールログがクリック可能になり、HierarchyでPingが飛ぶようになります。
                Debug.Log($"[UI Selection] -> {currentSelected.name}", currentSelected);
            }

            // 6. 「前のオブジェクト」の情報を更新
            lastSelectedObject = currentSelected;
        }
    }
}