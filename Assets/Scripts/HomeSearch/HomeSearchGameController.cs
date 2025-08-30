using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;
public class HomeSearchGameController : SerializedMonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player; // 未指定なら MainCamera を拾う

    [Header("Nodes")]
    [SerializeField] private bool autoCollectChildren = true;
    [SerializeField] private List<HomeSearchNode> nodes = new List<HomeSearchNode>();
    [SerializeField] private int startIndex = 0;

    [Header("Flow")]
    [SerializeField] private bool autoStartOnEnable = true;
    [SerializeField] private UnityEvent2 onGameStart;
    [SerializeField] private UnityEvent2 onNodeActivated;
    [SerializeField] private UnityEvent2 onAllCompleted;

    private int currentIndex = -1;
    private HomeSearchNode Current =>
        (currentIndex >= 0 && currentIndex < nodes.Count) ? nodes[currentIndex] : null;

    private void Reset()
    {
        TryAutoCollect();
        TryAutoAssignPlayer();
    }

    private void OnValidate()
    {
        if (autoCollectChildren) TryAutoCollect();
        if (player == null) TryAutoAssignPlayer();
    }

    private void OnEnable()
    {
        if (autoStartOnEnable) StartGame();
    }
    [Button]
    public void StartGame()
    {
        if (nodes == null || nodes.Count == 0)
        {
            Debug.LogWarning("[HomeSearch] Nodes が空です。子に HomeSearchNode を付けて並べて。");
            return;
        }

        currentIndex = Mathf.Clamp(startIndex, 0, nodes.Count - 1);
        foreach (var n in nodes) n.Deactivate();

        onGameStart?.Invoke();
        ActivateCurrent();
    }

    private void ActivateCurrent()
    {
        var n = Current;
        if (n == null) { onAllCompleted?.Invoke(); return; }

        n.Activate(player);        // ← ここでは進行を“監視”しない（自動で次へ行かない）
        onNodeActivated?.Invoke();
    }
    [Button]
    // ★ UnityEvent から呼ぶ：任意タイミングで次のノードへ
    public void AdvanceToNextNode()
    {
        var n = Current;
        if (n != null) n.Deactivate();

        currentIndex++;
        if (currentIndex < nodes.Count) ActivateCurrent();
        else onAllCompleted?.Invoke();
    }

    // 必要なら指定インデックスへ飛ぶ（任意）
    public void JumpToIndex(int index)
    {
        var n = Current;
        if (n != null) n.Deactivate();

        currentIndex = Mathf.Clamp(index, 0, nodes.Count);
        if (currentIndex < nodes.Count) ActivateCurrent();
        else onAllCompleted?.Invoke();
    }

    private void TryAutoCollect()
    {
        nodes.Clear();
        foreach (Transform c in transform)
        {
            var n = c.GetComponent<HomeSearchNode>();
            if (n != null) nodes.Add(n);
        }
    }

    private void TryAutoAssignPlayer()
    {
        if (player != null) return;
        var cam = Camera.main;
        if (cam != null) player = cam.transform;
    }
}
