using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using Sirenix.OdinInspector;
// using UTimelineAsset = UnityEngine.Timeline.TimelineAsset; // 必要なら
// ↑基本は UnityEngine.Timeline.TimelineAsset をそのまま使う

[DisallowMultipleComponent]
public class TimelinePlayback : Singleton<TimelinePlayback>
{
    // ===== Refs =====
    [Title("Refs")]
    [SerializeField] private GameFlowController flow;
    [SerializeField] public TimelineController registry;
    [SerializeField] private PlayableDirector director;

    // ===== Play Options =====
    [Title("Play Options")]
    [LabelText("再生時に先頭へ巻き戻す")]
    public bool rewindOnPlay = true;

    [LabelText("WrapMode")]
    public DirectorWrapMode wrapMode = DirectorWrapMode.None;

    [LabelText("RebuildGraphしてから再生")]
    public bool rebuildGraphOnPlay = true;

    [LabelText("TimeUpdateMode（GameTime/Unscaled/Manual）")]
    public DirectorUpdateMode updateMode = DirectorUpdateMode.GameTime;

    [LabelText("Play直前にTime.timeScale=1を強制")]
    public bool forceTimeScaleOne = false;

    [LabelText("RootPlayableのSpeedを毎回1へリセット")]
    public bool resetRootSpeed = true;

    [LabelText("Playをnフレ遅らせる（競合回避用）"), Min(0)]
    public int delayFramesBeforePlay = 0;

    [LabelText("Play直後に自動Pause（必要な人だけON）")]
    public bool autoPauseAfterPlay = false;

    [ShowIf(nameof(autoPauseAfterPlay)), LabelText("Pauseまでの遅延フレーム数"), Min(0)]
    public int pauseDelayFrames = 1;

    [LabelText("Play時に状態ログを出す")]
    public bool logStateOnPlay = false;

    // ===== State =====
    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public GameFlowController.Phase LastPhase { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public string LastTimelineId { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsPlaying => director && director.state == PlayState.Playing;

    // コールバック（必要なら外部で購読）
    public event Action<string> OnTimelineStarted;
    public event Action<string> OnTimelineStopped;

    // ===== Unity =====
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

    // ===== Public API =====

    /// <summary> 現在のPhaseからidで再生 </summary>
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

    /// <summary> 明示Phase指定で再生 </summary>
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

    /// <summary> 再生完了まで待機 </summary>
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

        // 設定適用順：Asset→Graph→巻戻し→モード/スピード→Play
        director.extrapolationMode = wrapMode;
        director.playableAsset = asset;

        if (rebuildGraphOnPlay) director.RebuildGraph();

        if (rewindOnPlay) director.time = 0;

        director.timeUpdateMode = updateMode;

        if (forceTimeScaleOne && Application.isPlaying) Time.timeScale = 1f;

        if (resetRootSpeed)
        {
            var root = director.playableGraph.GetRootPlayable(0);
            if (root.IsValid()) root.SetSpeed(1);
        }

        if (logStateOnPlay) LogStateInternal("[Play] before");

        if (delayFramesBeforePlay > 0 && Application.isPlaying)
            StartCoroutine(Co_PlayDelayed());
        else
            DoPlay();

        // 自動Pauseを使う人向け
        if (autoPauseAfterPlay && Application.isPlaying)
            StartCoroutine(Co_PauseAfterFrames(pauseDelayFrames));
    }

    IEnumerator Co_PlayDelayed()
    {
        for (int i = 0; i < delayFramesBeforePlay; i++) yield return null;
        DoPlay();
    }

    void DoPlay()
    {
        director.Play();
        OnTimelineStarted?.Invoke(LastTimelineId);
        if (logStateOnPlay) LogStateInternal("[Play] after");
    }

    IEnumerator Co_PauseAfterFrames(int frames)
    {
        for (int i = 0; i < Mathf.Max(0, frames); i++) yield return null;
        if (IsPlaying) director.Pause();
    }

    // ===== Debug / Odin =====
    [FoldoutGroup("Debug"), LabelText("テストID")]
    public string debugId;

    [FoldoutGroup("Debug"), Button(ButtonSizes.Medium)]
    public void DebugPlay_CurrentPhase() => Play(debugId);

    [FoldoutGroup("Debug"), Button(ButtonSizes.Medium)]
    public void DebugPlay_PauseResume()
    {
        if (Play(debugId))
        {
            StartCoroutine(Co_DebugPauseResume());
        }
    }

    IEnumerator Co_DebugPauseResume()
    {
        // 0.5秒後にPause → 0.5秒後にResume
        yield return null; yield return null;
        Pause();
        yield return new WaitForSeconds(0.5f);
        Resume();
    }

    [FoldoutGroup("Debug"), Button(ButtonSizes.Small)]
    public void LogState() => LogStateInternal("[Manual Log]");

    void LogStateInternal(string tag)
    {
        if (!director)
        {
            Debug.Log($"{tag} director=null");
            return;
        }
        var root = director.playableGraph.GetRootPlayable(0);
        var spd = root.IsValid() ? root.GetSpeed() : double.NaN;
        Debug.Log($"{tag} state={director.state} time={director.time:F3} mode={director.timeUpdateMode} timescale={Time.timeScale} speed={spd}");
    }
}
