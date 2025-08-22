using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

public class NpcDialogueCaller : SerializedMonoBehaviour, IInteractable
{
    public enum StartMode
    {
        Single,         // 単純に1つのIDから開始
        RandomFromList  // 複数IDの中からランダムに1つ
    }

    [Title("Mode")]
    [EnumToggleButtons] public StartMode mode = StartMode.Single;

    [ShowIf(nameof(mode), StartMode.Single)]
    [LabelText("Single ID")]
    public string singleId;

    [ShowIf(nameof(mode), StartMode.RandomFromList)]
    [LabelText("候補IDs"), ListDrawerSettings(Expanded = true)]
    public List<string> candidateIds = new();

    [Title("Options")]
    [Tooltip("一度だけ起動して以降は無効にする")]
    public bool once = false;

    private bool consumed = false;
    public void Interact() => Talk();
    public string GetDisplayText() => "話す";
    public bool ShouldHighlight() => true;

    /// <summary>外部（ボタンや他スクリプト）から呼んで会話開始。</summary>
    [Button("Talk()")]
    public void Talk()
    {
        if (once && consumed) return;

        string id = ResolveId();
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning($"{name}: 有効な dialogueId がありません。");
            return;
        }

        var ctrl = DialogueController.instance;
        if (!ctrl)
        {
            Debug.LogError("DialogueController.Instance が見つかりません。シーンに配置してください。");
            return;
        }

        ctrl.StartDialogue(id);

        if (once) consumed = true;
    }

    /// <summary>外部からIDを指定して開始したい場合に。</summary>
    public void TalkWithId(string id)
    {
        if (once && consumed) return;
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning($"{name}: TalkWithId に空のIDが渡されました。");
            return;
        }
        var ctrl = DialogueController.instance;
        if (!ctrl)
        {
            Debug.LogError("DialogueController.Instance が見つかりません。");
            return;
        }
        ctrl.StartDialogue(id);
        if (once) consumed = true;
    }

    public bool CanTalk => !once || !consumed;

    /// <summary>モードに応じて開始IDを決定。</summary>
    private string ResolveId()
    {
        switch (mode)
        {
            case StartMode.Single:
                return string.IsNullOrWhiteSpace(singleId) ? null : singleId;

            case StartMode.RandomFromList:
                if (candidateIds == null || candidateIds.Count == 0) return null;

                // 空でないIDのみ抽出
                var pool = candidateIds.FindAll(s => !string.IsNullOrWhiteSpace(s));
                if (pool.Count == 0) return null;

                int idx = Random.Range(0, pool.Count);
                return pool[idx];

            default:
                return null;
        }
    }

    /// <summary>一度きりフラグを解除したい場合に。</summary>
    public void ResetConsumed() => consumed = false;
}
