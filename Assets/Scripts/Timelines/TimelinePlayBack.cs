using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Sirenix.OdinInspector;

[DisallowMultipleComponent]
public class TimelinePlayback : MonoBehaviour
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

    // ルート速度の再試行回数／待機フレーム
    [Title("内部：RootReset Retry")]
    [LabelText("再試行回数")]
    public int rootResetRetryCount = 3;
    [LabelText("再試行フレーム間隔（フレーム）")]
    public int rootResetRetryIntervalFrames = 1;

    // ===== State =====
    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public GameFlowController.Phase LastPhase { get; private set; }
    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public string LastTimelineId { get; private set; }
    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsPlaying => director && director.state == PlayState.Playing;

    // コールバック
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
        if (director != null) director.stopped += HandleDirectorStopped;
    }

    void OnDisable()
    {
        if (director != null) director.stopped -= HandleDirectorStopped;
    }

    void HandleDirectorStopped(PlayableDirector _)
    {
        OnTimelineStopped?.Invoke(LastTimelineId);
    }

    // ===== Public API =====

    /// <summary> 現在のPhaseからidで再生 </summary>
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
    
    /// <summary> TimelineAssetを直接指定して再生 </summary>
    public bool Play(TimelineAsset timelineAsset)
    {
        if (!ValidateBasics(requireFlow: false)) return false;
        
        if (timelineAsset == null)
        {
            Debug.LogWarning("[TimelinePlayback] 引数で渡されたTimelineAssetがnullです。");
            return false;
        }
        
        LastPhase = (flow != null) ? flow.CurrentPhase : default(GameFlowController.Phase);
        LastTimelineId = timelineAsset.name;
        ExecutePlayback(timelineAsset);
        
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
        if (!director) return;
        var graph = director.playableGraph;
        if (graph.IsValid())
        {
            var root = graph.GetRootPlayable(0);
            if (root.IsValid()) root.SetSpeed(0);
        }
    }

    /// <summary> 再開 </summary>
    [Button(ButtonSizes.Small)]
    public void Resume()
    {
        if (!director) return;
        var graph = director.playableGraph;
        if (graph.IsValid())
        {
            var root = graph.GetRootPlayable(0);
            if (root.IsValid()) root.SetSpeed(1);
        }
    }

    // ===== 内部 =====

    bool ValidateBasics(bool requireFlow = true)
    {
        if (!registry) { Debug.LogWarning("[TimelinePlayback] Registry (TimelineController) が未設定"); return false; }
        if (!director) { Debug.LogWarning("[TimelinePlayback] PlayableDirector が未設定"); return false; }
        if (requireFlow && !flow) { Debug.LogWarning("[TimelinePlayback] GameFlowController が未設定"); return false; }
        return true;
    }

    /// <summary>
    /// PhaseとIDを記録し、再生実行メソッドを呼び出す役割。
    /// </summary>
    void ApplyAndPlay(GameFlowController.Phase phase, string id, TimelineAsset asset)
    {
        LastPhase = phase;
        LastTimelineId = id;
        ExecutePlayback(asset);
    }

    /// <summary>
    /// TimelineAssetの再生実行を担当する本体メソッド。PhaseやIDには依存しない。
    /// </summary>
    void ExecutePlayback(TimelineAsset asset)
    {
        director.extrapolationMode = wrapMode;
        director.playableAsset = asset;

        if (rebuildGraphOnPlay)
        {
            try { director.RebuildGraph(); }
            catch (Exception ex) { Debug.LogWarning($"[TimelinePlayback] RebuildGraph threw: {ex.Message}"); }
        }

        if (rewindOnPlay) director.time = 0;

        director.timeUpdateMode = updateMode;

        if (forceTimeScaleOne && Application.isPlaying) Time.timeScale = 1f;

        if (logStateOnPlay) LogStateInternal("[Play] before");

        if (delayFramesBeforePlay > 0 && Application.isPlaying)
            StartCoroutine(Co_PlayDelayed());
        else
            DoPlay();

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
        if (!director) return;
        director.Play();
        TryResetRootSpeedImmediate();
        if (resetRootSpeed)
            StartCoroutine(TryResetRootSpeedWithRetries(rootResetRetryCount, rootResetRetryIntervalFrames));
        OnTimelineStarted?.Invoke(LastTimelineId);
        if (logStateOnPlay) LogStateInternal("[Play] after");
    }

    IEnumerator Co_PauseAfterFrames(int frames)
    {
        for (int i = 0; i < Mathf.Max(0, frames); i++) yield return null;
        if (IsPlaying) director.Pause();
    }

    void TryResetRootSpeedImmediate()
    {
        if (!resetRootSpeed || director == null) return;
        var graph = director.playableGraph;
        if (graph.IsValid())
        {
            try
            {
                var root = graph.GetRootPlayable(0);
                if (root.IsValid()) root.SetSpeed(1);
            }
            catch (Exception ex)
            {
                if (logStateOnPlay) Debug.LogWarning($"[TimelinePlayback] TryResetRootSpeedImmediate failed: {ex.Message}");
            }
        }
    }

    IEnumerator TryResetRootSpeedWithRetries(int retries, int intervalFrames)
    {
        if (!resetRootSpeed || director == null) yield break;

        for (int attempt = 0; attempt < Mathf.Max(0, retries); attempt++)
        {
            var graph = director.playableGraph;
            if (graph.IsValid())
            {
                try
                {
                    var root = graph.GetRootPlayable(0);
                    if (root.IsValid())
                    {
                        root.SetSpeed(1);
                        yield break;
                    }
                }
                catch (Exception ex)
                {
                    if (logStateOnPlay) Debug.LogWarning($"[TimelinePlayback] Root reset attempt threw: {ex.Message}");
                }
            }

            for (int f = 0; f < Mathf.Max(1, intervalFrames); f++) yield return null;
        }

        if (logStateOnPlay) Debug.LogWarning("[TimelinePlayback] Failed to reset root speed after retries");
    }

    // ===== Debug / Odin =====
    [FoldoutGroup("Debug"), LabelText("テストID")]
    public string debugId;

    [FoldoutGroup("Debug"), Button(ButtonSizes.Medium)]
    public void DebugPlay_CurrentPhase() => Play(debugId);

    [FoldoutGroup("Debug"), Button(ButtonSizes.Small)]
    public void LogState() => LogStateInternal("[Manual Log]");

    void LogStateInternal(string tag)
    {
        if (!director)
        {
            Debug.Log($"{tag} director=null");
            return;
        }

        var graph = director.playableGraph;
        double spd = double.NaN;
        if (graph.IsValid())
        {
            try
            {
                var root = graph.GetRootPlayable(0);
                if (root.IsValid()) spd = root.GetSpeed();
            }
            catch (Exception ex)
            {
                if (logStateOnPlay) Debug.LogWarning($"[TimelinePlayback] LogStateInternal failed: {ex.Message}");
            }
        }

        Debug.Log($"{tag} state={director.state} time={director.time:F3} mode={director.timeUpdateMode} timescale={Time.timeScale} speed={spd}");
    }
}