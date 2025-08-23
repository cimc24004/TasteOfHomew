using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Sirenix.OdinInspector;
// Unity純正 Timeline を明示
using UTimelineAsset = UnityEngine.Timeline.TimelineAsset;

[DisallowMultipleComponent]
public class TimelineController : Singleton<TimelineController>
{
    [Serializable]
    public struct Binding
    {
        [TableColumnWidth(140, Resizable = false)]
        [LabelText("フェーズ")]
        public GameFlowController.Phase phase;

        [LabelText("Phase Timeline Asset"), AssetsOnly, Required, InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        public PhaseTimelineAsset asset;
    }

    [Title("Phases（各PhaseにSOを割り当て）")]
    [TableList(ShowIndexLabels = false)]
    public List<Binding> phases = new();

    [Title("（任意）再生ヘルパ")]
    [LabelText("デフォルトDirector")]
    public PlayableDirector defaultDirector;

    // キャッシュ：phase → (id → TimelineAsset)
    private readonly Dictionary<GameFlowController.Phase, Dictionary<string, UTimelineAsset>> map = new();

    void Start()      => RebuildCache();
    void OnValidate() => RebuildCache();

    [Button(ButtonSizes.Medium)]
    public void RebuildCache()
    {
        map.Clear();
        if (phases == null) return;

        foreach (var b in phases)
        {
            if (b.asset == null) continue;

            // SOの中身を展開してキャッシュ
            var dict = new Dictionary<string, UTimelineAsset>(StringComparer.Ordinal);
            if (b.asset.timelines != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var t in b.asset.timelines)
                {
                    if (t == null || string.IsNullOrEmpty(t.id) || t.asset == null) continue;
                    if (!seen.Add(t.id))
                    {
                        Debug.LogWarning($"[TimelineController] Duplicate id '{t.id}' in Phase '{b.phase}' (asset:{b.asset.name}). First will be used.");
                        continue;
                    }
                    dict[t.id] = t.asset;
                }
            }
            map[b.phase] = dict;

#if UNITY_EDITOR
            // PhaseとSO内のphaseが食い違ってたら軽く警告（任意）
            if (b.asset.phase != b.phase)
            {
                Debug.LogWarning($"[TimelineController] Phase mismatch: Binding={b.phase}, Asset={b.asset.name} has phase={b.asset.phase}");
            }
#endif
        }
    }

    // ===== 取得API =====

    /// <summary> phase＋id で Unity TimelineAsset を取得（無ければ null） </summary>
    public UTimelineAsset GetTimeline(GameFlowController.Phase phase, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return (map.TryGetValue(phase, out var d) && d.TryGetValue(id, out var asset)) ? asset : null;
    }

    /// <summary> phase に割り当てた PhaseTimelineAsset 自体を取得（インスペクタ相当）</summary>
    public PhaseTimelineAsset GetPhaseAsset(GameFlowController.Phase phase)
    {
        var idx = phases.FindIndex(p => p.phase == phase);
        return idx >= 0 ? phases[idx].asset : null;
    }

    // ===== （任意）再生ヘルパ =====

    public bool Play(GameFlowController.Phase phase, string id, PlayableDirector director = null)
    {
        var asset = GetTimeline(phase, id);
        if (!asset) return false;

        var dir = director ? director : defaultDirector;
        if (!dir) { Debug.LogWarning("[TimelineController] Play failed: PlayableDirector not assigned."); return false; }

        dir.time = 0;
        dir.extrapolationMode = DirectorWrapMode.None;
        dir.playableAsset = asset;
        dir.RebuildGraph();
        dir.Play();
        return true;
    }

    // 編集楽ちんボタン（空バインディングを自動で追加）
    [Button(ButtonSizes.Small)]
    public void EnsureAllPhaseBindings()
    {
        if (phases == null) phases = new List<Binding>();
        var have = new HashSet<GameFlowController.Phase>();
        foreach (var b in phases) have.Add(b.phase);

        foreach (GameFlowController.Phase p in Enum.GetValues(typeof(GameFlowController.Phase)))
            if (!have.Contains(p)) phases.Add(new Binding { phase = p, asset = null });

        phases.Sort((a, b) => a.phase.CompareTo(b.phase));
    }
}
