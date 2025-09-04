using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class KeyUnityEvent
{
    [Tooltip("チェックするキー名")]
    public string key;

    [Tooltip("キーが true のときに呼ばれるイベント")]
    public UnityEvent onTrue;

    [Tooltip("一度実行したら以降は実行しない (必要なければオフに)")]
    public bool invokeOnlyOnce = false;

    // 実行済みフラグ（Inspectorに見せたくなければ [HideInInspector] を付けて）
    [HideInInspector] public bool invoked = false;
}

public class EndController : MonoBehaviour
{
    [SerializeField]
    private List<KeyUnityEvent> keyEvents = new List<KeyUnityEvent>();

    private DataShareController dataShareController;

    void Start()
    {
        dataShareController = DataShareController.Instance;
        if (dataShareController == null)
        {
            Debug.LogWarning("[EndController] DataShareController.Instance が見つかりません。");
            return;
        }

        // 起動時にチェックしてイベントを呼ぶ
        CheckKeys();
    }

    // Inspector からもコンテキストメニューで呼べる
    [ContextMenu("Check Keys (Run)")]
    public void CheckKeys()
    {
        if (dataShareController == null)
        {
            dataShareController = DataShareController.Instance;
            if (dataShareController == null)
            {
                Debug.LogWarning("[EndController] DataShareController.Instance が見つかりません。");
                return;
            }
        }

        foreach (var ke in keyEvents)
        {
            if (string.IsNullOrEmpty(ke.key)) continue;

            // DataShareController の API に合わせて取得する（GetFlag / TryGetFlag どちらでもOK）
            bool value = dataShareController.GetFlag(ke.key); // 既存の GetFlag を使う想定

            if (value)
            {
                if (ke.invokeOnlyOnce && ke.invoked) continue;

                try
                {
                    ke.onTrue?.Invoke();
                    ke.invoked = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[EndController] UnityEvent 実行中に例外: key={ke.key} - {ex}");
                }
            }
        }
    }
}
