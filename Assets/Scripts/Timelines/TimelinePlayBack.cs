using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// タイムライン（PlayableDirector）の再生を担当する。
///
/// ・「フェーズ + ID」で TimelineController（登録簿）からタイムラインを引いて再生する
/// ・一時停止 / 再開は、タイムライン全体の再生速度を 0 / 1 に切り替えて行う
///   （PlayableDirector.Pause と違い、止めている間も最後のフレームの状態が保たれる）
/// </summary>
[DisallowMultipleComponent]
public class TimelinePlayback : MonoBehaviour
{
    // ===== Inspector =====

    [Title("Refs")]
    [SerializeField] private GameFlowController flow;
    [SerializeField] public TimelineController registry;
    [SerializeField] public PlayableDirector director;

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

    [Title("内部：RootReset Retry")]
    [LabelText("再試行回数")]
    public int rootResetRetryCount = 3;
    [LabelText("再試行フレーム間隔（フレーム）")]
    public int rootResetRetryIntervalFrames = 1;

    // ===== 状態 =====

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public GameFlowController.Phase LastPhase { get; private set; }
    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public string LastTimelineId { get; private set; }
    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsPlaying => director && director.state == PlayState.Playing;

    /// <summary>再生中（Play / Resume 済み）なら true、Pause / Stop されていれば false。</summary>
    public bool IsPlayingFromSignal;

    public event Action<string> OnTimelineStarted;
    public event Action<string> OnTimelineStopped;

    // ===== Unity ライフサイクル =====

    private void Reset()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (director != null) director.stopped += HandleDirectorStopped;
    }

    private void OnDisable()
    {
        if (director != null) director.stopped -= HandleDirectorStopped;
    }

    private void ResolveReferences()
    {
        if (!flow) flow = GameFlowController.instance;
        if (!registry) registry = TimelineController.instance;
        if (!director) director = GetComponent<PlayableDirector>();
    }

    private void HandleDirectorStopped(PlayableDirector _)
    {
        OnTimelineStopped?.Invoke(LastTimelineId);
    }

    // ===== 公開 API: 再生 =====

    /// <summary>現在のフェーズに登録されているタイムラインを、ID で再生する。</summary>
    public bool Play(string timelineId)
    {
        if (!ValidateBasics()) return false;

        return PlayFromRegistry(flow.CurrentPhase, timelineId);
    }

    /// <summary>フェーズを明示して、そのフェーズに登録されているタイムラインを再生する。</summary>
    public bool Play(GameFlowController.Phase phase, string timelineId)
    {
        if (!ValidateBasics(requireFlow: false)) return false;

        return PlayFromRegistry(phase, timelineId);
    }

    /// <summary>登録簿を通さず、TimelineAsset を直接指定して再生する。</summary>
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

    /// <summary>再生して、終わるまで待つコルーチン。</summary>
    public IEnumerator PlayAndWait(string timelineId)
    {
        if (!Play(timelineId)) yield break;

        while (director && director.state == PlayState.Playing)
        {
            yield return null;
        }
    }

    // ===== 公開 API: 停止 / 一時停止 / 再開 =====

    [Button(ButtonSizes.Small)]
    public void Stop()
    {
        if (director) director.Stop();
        IsPlayingFromSignal = false;
    }

    /// <summary>一時停止（タイムライン全体の再生速度を 0 にする）。</summary>
    [Button(ButtonSizes.Small)]
    public void Pause()
    {
        if (!director) return;

        IsPlayingFromSignal = false;
        if (TryGetRootPlayable(out Playable root)) root.SetSpeed(0);
    }

    /// <summary>再開（タイムライン全体の再生速度を 1 に戻す）。</summary>
    [Button(ButtonSizes.Small)]
    public void Resume()
    {
        if (!director) return;

        IsPlayingFromSignal = true;
        if (TryGetRootPlayable(out Playable root)) root.SetSpeed(1);
    }

    // ===== 再生の内部処理 =====

    private bool ValidateBasics(bool requireFlow = true)
    {
        if (!registry)
        {
            Debug.LogWarning("[TimelinePlayback] Registry (TimelineController) が未設定");
            return false;
        }
        if (!director)
        {
            Debug.LogWarning("[TimelinePlayback] PlayableDirector が未設定");
            return false;
        }
        if (requireFlow && !flow)
        {
            Debug.LogWarning("[TimelinePlayback] GameFlowController が未設定");
            return false;
        }
        return true;
    }

    private bool PlayFromRegistry(GameFlowController.Phase phase, string timelineId)
    {
        var asset = registry.GetTimeline(phase, timelineId);
        if (!asset)
        {
            Debug.LogWarning($"[TimelinePlayback] Timeline not found. phase={phase}, id={timelineId}");
            return false;
        }

        IsPlayingFromSignal = true;
        LastPhase = phase;
        LastTimelineId = timelineId;
        ExecutePlayback(asset);
        return true;
    }

    /// <summary>Inspector の Play Options に従って PlayableDirector を設定し、再生する。</summary>
    private void ExecutePlayback(TimelineAsset asset)
    {
        director.extrapolationMode = wrapMode;
        director.playableAsset = asset;

        if (rebuildGraphOnPlay)
        {
            try
            {
                director.RebuildGraph();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TimelinePlayback] RebuildGraph threw: {ex.Message}");
            }
        }

        if (rewindOnPlay) director.time = 0;

        director.timeUpdateMode = updateMode;

        if (forceTimeScaleOne && Application.isPlaying) Time.timeScale = 1f;

        if (logStateOnPlay) LogStateInternal("[Play] before");

        if (delayFramesBeforePlay > 0 && Application.isPlaying)
        {
            StartCoroutine(PlayAfterDelay());
        }
        else
        {
            DoPlay();
        }

        if (autoPauseAfterPlay && Application.isPlaying)
        {
            StartCoroutine(PauseAfterFrames(pauseDelayFrames));
        }
    }

    private IEnumerator PlayAfterDelay()
    {
        for (int i = 0; i < delayFramesBeforePlay; i++)
        {
            yield return null;
        }
        DoPlay();
    }

    private void DoPlay()
    {
        if (!director) return;

        director.Play();

        // 前回 Pause（速度 0）のまま終わっていると動かないので、速度を 1 に戻しておく。
        // 再生直後はグラフがまだ有効になっていないことがあるため、数フレームかけて再試行もする。
        TryResetRootSpeedImmediate();
        if (resetRootSpeed)
        {
            StartCoroutine(TryResetRootSpeedWithRetries(rootResetRetryCount, rootResetRetryIntervalFrames));
        }

        OnTimelineStarted?.Invoke(LastTimelineId);
        if (logStateOnPlay) LogStateInternal("[Play] after");
    }

    private IEnumerator PauseAfterFrames(int frames)
    {
        for (int i = 0; i < Mathf.Max(0, frames); i++)
        {
            yield return null;
        }
        if (IsPlaying) director.Pause();
    }

    // ===== 再生速度（RootPlayable）の操作 =====

    /// <summary>タイムライン全体の再生速度を握っている RootPlayable を取得する。グラフが無効なら false。</summary>
    private bool TryGetRootPlayable(out Playable root)
    {
        root = default;

        var graph = director.playableGraph;
        if (!graph.IsValid()) return false;

        root = graph.GetRootPlayable(0);
        return root.IsValid();
    }

    private void TryResetRootSpeedImmediate()
    {
        if (!resetRootSpeed || director == null) return;

        try
        {
            if (TryGetRootPlayable(out Playable root)) root.SetSpeed(1);
        }
        catch (Exception ex)
        {
            if (logStateOnPlay) Debug.LogWarning($"[TimelinePlayback] TryResetRootSpeedImmediate failed: {ex.Message}");
        }
    }

    private IEnumerator TryResetRootSpeedWithRetries(int retries, int intervalFrames)
    {
        if (!resetRootSpeed || director == null) yield break;

        for (int attempt = 0; attempt < Mathf.Max(0, retries); attempt++)
        {
            try
            {
                if (TryGetRootPlayable(out Playable root))
                {
                    root.SetSpeed(1);
                    yield break;
                }
            }
            catch (Exception ex)
            {
                if (logStateOnPlay) Debug.LogWarning($"[TimelinePlayback] Root reset attempt threw: {ex.Message}");
            }

            for (int frame = 0; frame < Mathf.Max(1, intervalFrames); frame++)
            {
                yield return null;
            }
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

    private void LogStateInternal(string tag)
    {
        if (!director)
        {
            Debug.Log($"{tag} director=null");
            return;
        }

        double speed = double.NaN;
        try
        {
            if (TryGetRootPlayable(out Playable root)) speed = root.GetSpeed();
        }
        catch (Exception ex)
        {
            if (logStateOnPlay) Debug.LogWarning($"[TimelinePlayback] LogStateInternal failed: {ex.Message}");
        }

        Debug.Log($"{tag} state={director.state} time={director.time:F3} mode={director.timeUpdateMode} timescale={Time.timeScale} speed={speed}");
    }
}
