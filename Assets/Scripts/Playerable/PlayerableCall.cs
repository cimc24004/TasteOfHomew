using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;
using UnityEngine.Playables;
using UnityEngine.Localization;

[DisallowMultipleComponent]
public class PlayerableCall : MonoBehaviour, IInteractable
{
    [Title("Refs")]
    [Required, LabelText("GameFlow")]
    private GameFlowController flow;

    [Required, LabelText("TimelinePlayback")]
    private TimelinePlayback playback; // さっき作った再生コンポ

    [Title("Target")]
    [LabelText("このPhaseでのみ実行")]
    public GameFlowController.Phase requiredPhase;

    [LabelText("Timeline Id（Phase内のID）")]
    public string timelineId;

    [Title("UI")]
    [LabelText("提示テキスト (Old)")]
    [Tooltip("移行用：新しい Localized Prompt Text が空の場合、これが「翻訳キー」として使われます")]
    public string promptText = "再生する";

    [LabelText("Phase不一致ならUIを出さない")]
    public bool hideWhenLocked = true;

    [ShowIf(nameof(hideWhenLocked), false)]
    [LabelText("不一致のときに出す文言 (Old)")]
    [Tooltip("移行用：新しい Localized Locked Text が空の場合、これが「翻訳キー」として使われます")]
    public string lockedText = "今は使えない";

    // 新しい LocalizedString フィールド
    [Tooltip("↑の interactText が空の場合に使われる、デフォルトの翻訳キー（String Table内）")]
    [SerializeField] private string defaultInteractKey = "";

    [Title("Rules")]
    [LabelText("一度だけ実行")]
    public bool once = false;

    [LabelText("成功時に自身を無効化")]
    public bool disableOnPlayed = false;

    [Title("Events")]
    public UnityEvent onPlayed;
    public UnityEvent onPhaseMismatch;   // 現在Phase≠requiredPhase
    public UnityEvent onNotFound;        // Timeline未登録 or 再生失敗

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasExecuted { get; private set; }

    void Reset()
    {
        if (!flow) flow = GameFlowController.instance;
        if (!playback) playback = GameObject.FindWithTag("MainPlayableDirector").GetComponent<TimelinePlayback>();
    }

    // ===== IInteractable =====
    void Start()
    {
        if (!flow) flow = GameFlowController.instance;
        Debug.Log("flow: " + flow);
        if (!playback) playback = GameObject.FindWithTag("MainPlayableDirector").GetComponent<TimelinePlayback>();
    }

    public void Interact()
    {
        if (!flow) flow = GameFlowController.instance;
        if (!playback) playback = GameObject.FindWithTag("MainPlayableDirector").GetComponent<TimelinePlayback>();
        Debug.Log("flow: " + flow + "playback: " + playback + "once: " + once + "HasExecuted: " + HasExecuted);
        if (once && HasExecuted) return;
        if (!ValidateBasics())   return;
        Debug.Log("Flow: " + flow);
        var current = flow.CurrentPhase;
        if (current != requiredPhase)
        {
            Debug.Log("current != requiredPhase");
            Debug.Log("current: " + current + "requiredPhase: " + requiredPhase);
            onPhaseMismatch?.Invoke();
            return;
        }
        Debug.Log("playback: " + playback + "再生されまーす");
        // 再生
        var ok = playback.Play(requiredPhase, timelineId);
        if (!ok)
        {
            onNotFound?.Invoke();
            return;
        }

        HasExecuted = true;
        onPlayed?.Invoke();

        if (disableOnPlayed) gameObject.SetActive(false);
    }

    // 移行ロジックを GetDisplayText に実装
    public string GetDisplayText()
    {
        if (!ValidateBasics(silent:true)) return "";

        // --- 1. Phase が不一致の場合 ---
        if (flow.CurrentPhase != requiredPhase)
        {
            if (hideWhenLocked)
            {
                return ""; // 非表示
            }

            // (優先) 新しい localizedLockedText が設定されていれば、そのキーを返す

            // (フォールバック) 古い lockedText の文字列を「キー」として返す
            return lockedText;
        }

        if (string.IsNullOrEmpty(timelineId)) return "";

        // そのIDのTimelineが登録されてなければUI出さない（誤案内防止）
        var reg = playback ? playback.registry : null;
        if (reg != null && reg.GetTimeline(requiredPhase, timelineId) == null) return "";
        if (defaultInteractKey != null)
        {
            return defaultInteractKey;
        }
        // (フォールBACK) 古い promptText の文字列を「キー」として返す
        return promptText;
    }

    public bool ShouldHighlight()
    {
        if (!ValidateBasics(silent:true)) return false;
        if (flow.CurrentPhase != requiredPhase) return false;
        if (string.IsNullOrEmpty(timelineId))   return false;

        var reg = playback ? playback.registry : null;
        if (reg != null && reg.GetTimeline(requiredPhase, timelineId) == null) return false;

        return true;
    }

    // ===== 内部 =====

    bool ValidateBasics(bool silent = false)
    {
        // if (!flow)
        // {
        //     if (!silent) Debug.LogWarning("[PlayerableCall] GameFlowController 未設定", this);
        //     return false;
        // }
        // if (!playback)
        // {
        //     if (!silent) Debug.LogWarning("[PlayerableCall] TimelinePlayback 未設定", this);
        //     return false;
        // }
        return true;
    }
}
