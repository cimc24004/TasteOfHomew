using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

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
    [LabelText("提示テキスト")]
    public string promptText = "再生する";

    [LabelText("Phase不一致ならUIを出さない")]
    public bool hideWhenLocked = true;

    [ShowIf(nameof(hideWhenLocked), false)]
    [LabelText("不一致のときに出す文言")]
    public string lockedText = "今は使えない";

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
        if (!playback) playback = TimelinePlayback.instance;
    }

    // ===== IInteractable =====
    void Start()
    {
        if (!flow) flow = GameFlowController.instance;
        Debug.Log("flow: " + flow);
        if (!playback) playback = TimelinePlayback.instance;

    }
    public void Interact()
    {
        if (!flow) flow = GameFlowController.instance;
        if (!playback) playback = TimelinePlayback.instance;
        if (once && HasExecuted) return;
        if (!ValidateBasics())   return;
        Debug.Log("Flow: " + flow);
        var current = flow.CurrentPhase;
        if (current != requiredPhase)
        {
            onPhaseMismatch?.Invoke();
            return;
        }

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

    public string GetDisplayText()
    {
        if (!ValidateBasics(silent:true)) return "";
        if (flow.CurrentPhase != requiredPhase)
            return hideWhenLocked ? "" : lockedText;

        if (string.IsNullOrEmpty(timelineId)) return "";

        // そのIDのTimelineが登録されてなければUI出さない（誤案内防止）
        var reg = playback ? playback.registry : null;
        if (reg != null && reg.GetTimeline(requiredPhase, timelineId) == null) return "";

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
