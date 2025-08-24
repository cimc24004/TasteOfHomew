using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.Events;

/// <summary>
/// ただ Dialogue を呼び出すだけの軽量コンポーネント。
/// - インスペクタで Group を「Key or Asset」で指定
/// - SequenceId を指定
/// - Call() を叩くと DialogueController に委譲
/// </summary>
public class DialogueCaller : SerializedMonoBehaviour
{
    [SerializeField] private UnityEvent StartUnityEvent;
    [SerializeField] private UnityEvent EndUnityEvent;
    public enum GroupRefMode { ByKey, ByAsset }

    [Title("Target")]
    [EnumToggleButtons] public GroupRefMode groupRef = GroupRefMode.ByAsset;

    [ShowIf(nameof(groupRef), GroupRefMode.ByKey)]
    [LabelText("Group Key")]
    public string groupKey;

    [ShowIf(nameof(groupRef), GroupRefMode.ByAsset)]
    [LabelText("Group Asset")]
    public DialogueSequenceGroupAsset groupAsset;

    [LabelText("Sequence ID")]
    public string sequenceId;
    private DialogueController ctrl;
    void Start()
    {
        ctrl = DialogueController.instance;
        ctrl.onDialogueEnded.AddListener(StartUnityEvent.Invoke);
        ctrl.onDialogueStarted.AddListener(EndUnityEvent.Invoke);
    }
    // ========= Public API =========

    /// <summary>
    /// インスペクタの設定を使って会話開始。
    /// </summary>
    [Button("Call()")]
    public void Call()
    {
        if (!ctrl)
        {
            Debug.LogError("[DialogueCaller] DialogueController.instance が見つかりません。シーンに配置してください。", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(sequenceId))
        {
            Debug.LogWarning("[DialogueCaller] Sequence ID が未設定です。", this);
            return;
        }

        if (groupRef == GroupRefMode.ByAsset)
        {
            if (!groupAsset || string.IsNullOrWhiteSpace(groupAsset.groupKey))
            {
                Debug.LogWarning("[DialogueCaller] Group Asset が未設定、または groupKey が空です。", this);
                return;
            }
            ctrl.StartDialogue(groupAsset.groupKey, sequenceId);
        }
        else
        {
            Debug.Log("StartDialogue-------------------DialogueCaller");
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                Debug.LogWarning("[DialogueCaller] Group Key が未設定です。", this);
                return;
            }
            ctrl.StartDialogue(groupKey, sequenceId);
        }
        StartUnityEvent.Invoke();
    }

    /// <summary>
    /// UnityEvent から使いやすいオーバーロード（文字列指定）。
    /// </summary>
    public void CallWith(string explicitGroupKey, string explicitSequenceId)
    {
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
        ctrl.StartDialogue(explicitGroupKey, explicitSequenceId);
    }

    /// <summary>
    /// UnityEvent から使いやすいオーバーロード（アセット指定）。
    /// </summary>
    public void CallWithAsset(DialogueSequenceGroupAsset explicitGroupAsset, string explicitSequenceId)
    {
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
        ctrl.StartDialogue(explicitGroupAsset.groupKey, explicitSequenceId);
    }
}
