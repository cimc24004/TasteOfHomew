using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[System.Serializable]
public class KeyUnityEvent
{
    [Tooltip("チェックするキー名")]
    public string key;
    [Tooltip("エンディング名")]
    public string EndingName = "Ending1";
    [Tooltip("キーが true のときに呼ばれるイベント")]
    public UnityEvent onTrue;

    [Tooltip("一度実行したら以降は実行しない (必要なければオフに)")]
    public bool invokeOnlyOnce = false;

    [Tooltip("優先度（大きいほど優先）。同点はリスト順で早い方が勝ち")]
    public int priority = 0;

    [HideInInspector] public bool invoked = false;
}

public class EndController : MonoBehaviour
{
    [SerializeField] private Button ClickedJudgeButton;
    [SerializeField] private bool WaitForPlayerInputClick;
    public enum ResolvePolicy
    {
        HighestPriorityOnly, // 最も高い priority の1件だけ実行（同点はリスト順）
        InvokeAllTrue        // true のものは全部実行
    }
    [SerializeField] private TextMeshProUGUI EndingNameTextUI;
    [SerializeField] private ResolvePolicy resolvePolicy = ResolvePolicy.HighestPriorityOnly;

    [SerializeField] private List<KeyUnityEvent> keyEvents = new List<KeyUnityEvent>();

    private DataShareController dataShareController;
    [SerializeField] private TimelinePlayback EndingTimelinePlayback;
    private async void Start()
    {
        // 必要に応じて待機（君の現行仕様を尊重して1秒のまま）
        // await UniTask.WaitForSeconds(1f);
        CheckKeys();

        ClickedJudgeButton.onClick.AddListener(() =>
        {
            if (WaitForPlayerInputClick)
            {
                EndingTimelinePlayback.Resume();
                WaitForPlayerInputClick = false;
            }
        });
    }

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

        // true かつ 実行可能（invokeOnlyOnceが立っていたら未実行）な候補を集める
        var candidates = new List<KeyUnityEvent>();
        for (int i = 0; i < keyEvents.Count; i++)
        {
            var ke = keyEvents[i];
            if (string.IsNullOrEmpty(ke.key)) continue;

            bool val = dataShareController.GetFlag(ke.key);
            if (!val) continue;

            if (ke.invokeOnlyOnce && ke.invoked) continue;

            candidates.Add(ke);
        }

        if (candidates.Count == 0) return;

        if (resolvePolicy == ResolvePolicy.InvokeAllTrue)
        {
            // すべて実行
            foreach (var ke in candidates)
            {
                ke.onTrue?.Invoke();
                ke.invoked = true;
            }
        }
        else // HighestPriorityOnly
        {
            // 最高 priority を選ぶ（同点は keyEvents の並びが早い方を優先）
            KeyUnityEvent winner = null;
            int bestPriority = int.MinValue;

            // リストの順番＝Inspectorの並び順を優先に使いたいので keyEvents を基準に走査
            for (int i = 0; i < keyEvents.Count; i++)
            {
                var ke = keyEvents[i];
                if (!candidates.Contains(ke)) continue;

                if (ke.priority > bestPriority)
                {
                    bestPriority = ke.priority;
                    winner = ke;
                }
            }

            // 実行
            if (winner != null)
            {
                winner.onTrue?.Invoke();
                winner.invoked = true;
            }
        }
    }
    public void EndingNameSet()
    {
        var candidates = new List<KeyUnityEvent>();
        for (int i = 0; i < keyEvents.Count; i++)
        {
            var ke = keyEvents[i];
            bool val = dataShareController.GetFlag(ke.key);
            if (val)
            {
                EndingNameTextUI.SetText(ke.EndingName);
            }
        }
    }
    public void WaitForPlayerClick()
    {
        this.WaitForPlayerInputClick = true;
    }
}
