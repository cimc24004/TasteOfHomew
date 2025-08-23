using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using Sirenix.OdinInspector;
// Unity純正 Timeline を明示（型衝突回避用。必要なら使ってOK）
// using UTimelineAsset = UnityEngine.Timeline.TimelineAsset;

[DisallowMultipleComponent]
public class TimelinePlayback : Singleton<TimelinePlayback>
{
    [Title("Refs")]
    private GameFlowController flow;
    public TimelineController registry;
    private PlayableDirector director;

    [Title("Play Options")]
    [LabelText("再生時に先頭へ巻き戻す")]
    public bool rewindOnPlay = true;

    [LabelText("WrapMode")]
    public DirectorWrapMode wrapMode = DirectorWrapMode.None;

    [LabelText("RebuildGraphしてから再生")]
    public bool rebuildGraphOnPlay = true;

    // 直近の再生命
    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public GameFlowController.Phase LastPhase { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public string LastTimelineId { get; private set; }

    // コールバック（必要なら外部で購読）
    public event Action<string> OnTimelineStarted;
    public event Action<string> OnTimelineStopped;

    void Reset()
    {
        if (!flow) flow = GameFlowController.instance;
        if (!registry) registry = TimelineController.instance;
        if (!director) director = GetComponent<PlayableDirector>();
    }
    void Start()
    {
        if (!flow) flow = GameFlowController.instance;
        if (!registry) registry = TimelineController.instance;
        if (!director) director = GetComponent<PlayableDirector>();

    }
    void OnEnable()
    {
        if (director) director.stopped += HandleDirectorStopped;
    }

    void OnDisable()
    {
        if (director) director.stopped -= HandleDirectorStopped;
    }

    void HandleDirectorStopped(PlayableDirector _)
    {
        OnTimelineStopped?.Invoke(LastTimelineId);
    }

    /// <summary>
    /// 現在のPhaseをキーに、指定IDのTimelineをセット → 再生。
    /// </summary>
    /// <param name="timelineId">Phase内で登録したID</param>
    /// <returns>再生開始できたら true</returns>
    [Button(ButtonSizes.Medium), LabelText("Play(CurrentPhase, Id)")]
    public bool Play(string timelineId)
    {
        if (!ValidateBasics()) return false;

        var phase = flow.CurrentPhase;
        var asset = registry.GetTimeline(phase, timelineId);
        if (!asset)
        {
            Debug.LogWarning($"[TimelinePlayback] Timeline not found. phase={phase}, id={timelineId}");
            return false;
        }

        ApplyAndPlay(phase, timelineId, asset);
        return true;
    }

    /// <summary>
    /// 明示的にPhaseを指定して再生したい場合のオーバーロード。
    /// </summary>
    [Button(ButtonSizes.Medium), LabelText("Play(Phase, Id)")]
    public bool Play(GameFlowController.Phase phase, string timelineId)
    {
        if (!ValidateBasics(requireFlow:false)) return false;

        var asset = registry.GetTimeline(phase, timelineId);
        if (!asset)
        {
            Debug.LogWarning($"[TimelinePlayback] Timeline not found. phase={phase}, id={timelineId}");
            return false;
        }

        ApplyAndPlay(phase, timelineId, asset);
        return true;
    }

    /// <summary> 再生完了まで待つコルーチン（必要なら使用） </summary>
    public IEnumerator PlayAndWait(string timelineId)
    {
        if (!Play(timelineId)) yield break;
        while (director && director.state == PlayState.Playing) yield return null;
    }

    /// <summary> 停止 </summary>
    [Button(ButtonSizes.Small)]
    public void Stop()
    {
        if (director) director.Stop();
    }

    /// <summary> 一時停止 </summary>
    [Button(ButtonSizes.Small)]
    public void Pause()
    {
        if (director) director.Pause();
    }

    /// <summary> 再開 </summary>
    [Button(ButtonSizes.Small)]
    public void Resume()
    {
        if (director) director.Resume();
    }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsPlaying => director && director.state == PlayState.Playing;

    // ===== 内部 =====

    bool ValidateBasics(bool requireFlow = true)
    {
        if (!registry) { Debug.LogWarning("[TimelinePlayback] Registry (TimelineController) が未設定"); return false; }
        if (!director) { Debug.LogWarning("[TimelinePlayback] PlayableDirector が未設定"); return false; }
        if (requireFlow && !flow) { Debug.LogWarning("[TimelinePlayback] GameFlowController が未設定"); return false; }
        return true;
    }

    void ApplyAndPlay(GameFlowController.Phase phase, string id, UnityEngine.Timeline.TimelineAsset asset)
    {
        LastPhase = phase;
        LastTimelineId = id;

        if (rewindOnPlay) director.time = 0;
        director.extrapolationMode = wrapMode;
        director.playableAsset = asset;
        if (rebuildGraphOnPlay) director.RebuildGraph();
        director.Play();

        OnTimelineStarted?.Invoke(id);
    }

    // Odin用の簡易テストUI
    [FoldoutGroup("Debug"), LabelText("テストID")]
    public string debugId;

    [FoldoutGroup("Debug"), Button(ButtonSizes.Medium)]
    public void DebugPlay() => Play(debugId);
}
