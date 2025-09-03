using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.Events;
using Cysharp.Threading.Tasks; // ← 重要

/// <summary>
/// ただ Dialogue を呼び出すだけの軽量コンポーネント。
/// - グローバルイベントに購読しない（自分の開始/終了のみローカルで発火）
/// - Call() は同期APIだが内部で非同期実行（await）
/// </summary>
public class DialogueCaller : SerializedMonoBehaviour
{
    [Title("Local Events (このCaller専用)")]
    [SerializeField] private UnityEvent StartUnityEvent;
    [SerializeField] private UnityEvent EndUnityEvent;

    public enum GroupRefMode { ByKey, ByAsset }

    [Title("Target")]
    [EnumToggleButtons] public GroupRefMode groupRef = GroupRefMode.ByAsset;

    [ShowIf(nameof(groupRef), GroupRefMode.ByKey)]
    [LabelText("Group Key")] public string groupKey;

    [ShowIf(nameof(groupRef), GroupRefMode.ByAsset)]
    [LabelText("Group Asset")] public DialogueSequenceGroupAsset groupAsset;

    [LabelText("Sequence ID")]
    public string sequenceId;

    private bool busy; // 連打防止

    // ========= Public API =========

    [Button("Call()")]
    public void Call() => CallAsync().Forget();

    private async UniTaskVoid CallAsync()
    {
        if (busy || !this.gameObject.activeSelf) return;

        var ctrl = DialogueController.instance;
        if (!ctrl)
        {
            Debug.LogError("[DialogueCaller] DialogueController.instance が見つかりません。", this);
            return;
        }
        if (string.IsNullOrWhiteSpace(sequenceId))
        {
            Debug.LogWarning("[DialogueCaller] Sequence ID が未設定です。", this);
            return;
        }

        string keyToUse = null;
        if (groupRef == GroupRefMode.ByAsset)
        {
            if (!groupAsset || string.IsNullOrWhiteSpace(groupAsset.groupKey))
            {
                Debug.LogWarning("[DialogueCaller] Group Asset が未設定、または groupKey が空です。", this);
                return;
            }
            keyToUse = groupAsset.groupKey;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                Debug.LogWarning("[DialogueCaller] Group Key が未設定です。", this);
                return;
            }
            keyToUse = groupKey;
        }

        busy = true;
        try
        {
            // この Caller の開始イベント（自分だけ）
            StartUnityEvent?.Invoke();

            // 自分が投げた会話が「閉じるまで」待つ
            await ctrl.StartDialogueAsync(keyToUse, sequenceId);

            // この Caller の終了イベント（自分だけ）
            EndUnityEvent?.Invoke();
        }
        finally
        {
            busy = false;
        }
    }

    // UnityEvent から使いやすいオーバーロード（文字列指定）
    public void CallWith(string explicitGroupKey, string explicitSequenceId)
        => CallWithAsync(explicitGroupKey, explicitSequenceId).Forget();

    private async UniTaskVoid CallWithAsync(string explicitGroupKey, string explicitSequenceId)
    {
        if (busy) return;

        var ctrl = DialogueController.instance;
        if (!ctrl)
        {
            Debug.LogError("[DialogueCaller] DialogueController.instance が見つかりません。", this);
            return;
        }
        if (string.IsNullOrWhiteSpace(explicitGroupKey) || string.IsNullOrWhiteSpace(explicitSequenceId))
        {
            Debug.LogWarning("[DialogueCaller] CallWith: 引数が不正です。", this);
            return;
        }

        busy = true;
        try
        {
            StartUnityEvent?.Invoke();
            await ctrl.StartDialogueAsync(explicitGroupKey, explicitSequenceId);
            EndUnityEvent?.Invoke();
        }
        finally
        {
            busy = false;
        }
    }

    // UnityEvent から使いやすいオーバーロード（アセット指定）
    public void CallWithAsset(DialogueSequenceGroupAsset explicitGroupAsset, string explicitSequenceId)
        => CallWithAssetAsync(explicitGroupAsset, explicitSequenceId).Forget();

    private async UniTaskVoid CallWithAssetAsync(DialogueSequenceGroupAsset explicitGroupAsset, string explicitSequenceId)
    {
        if (busy) return;

        var ctrl = DialogueController.instance;
        if (!ctrl)
        {
            Debug.LogError("[DialogueCaller] DialogueController.instance が見つかりません。", this);
            return;
        }
        if (!explicitGroupAsset || string.IsNullOrWhiteSpace(explicitSequenceId))
        {
            Debug.LogWarning("[DialogueCaller] CallWithAsset: 引数が不正です。", this);
            return;
        }

        busy = true;
        try
        {
            StartUnityEvent?.Invoke();
            await ctrl.StartDialogueAsync(explicitGroupAsset.groupKey, explicitSequenceId);
            EndUnityEvent?.Invoke();
        }
        finally
        {
            busy = false;
        }
    }
}
