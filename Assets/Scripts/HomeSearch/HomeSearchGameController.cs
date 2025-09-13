using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

public class HomeSearchGameController : Singleton<HomeSearchGameController>
{
    [Header("Player")]
    [SerializeField] private Transform player; // 未指定なら MainCamera を拾う

    [Header("Nodes")]
    [SerializeField] private bool autoCollectChildren = true;
    [SerializeField] private List<HomeSearchNode> nodes = new List<HomeSearchNode>();
    [SerializeField] private int startIndex = 0;

    [Header("Flow")]
    [SerializeField] private bool autoStartOnEnable = true;
    [SerializeField] public UnityEvent2 onGameStart;
    [SerializeField] public UnityEvent2 onNodeActivated;
    [SerializeField] public UnityEvent2 onAllCompleted;

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

        // いったん全部停止＆非表示
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (!n) continue;
            n.Deactivate();
            if (n.gameObject.activeSelf) n.gameObject.SetActive(false);
        }

        onGameStart?.Invoke();
        ActivateCurrent();
    }

    private void ActivateCurrent()
    {
        var n = Current;
        if (n == null)
        {
            onAllCompleted?.Invoke();
            return;
        }

        // ★ 排他表示：現在ノードだけ true、他は false
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (!node) continue;

            bool shouldActive = (i == currentIndex);

            if (shouldActive)
            {
                if (!node.gameObject.activeSelf) node.gameObject.SetActive(true);
            }
            else
            {
                // 一応 Deactivate を呼んでから隠す（音ループ等の停止用）
                node.Deactivate();
                if (node.gameObject.activeSelf) node.gameObject.SetActive(false);
            }
        }

        // 現在ノードを起動
        n.Activate(player);
        onNodeActivated?.Invoke();
    }

    [Button]
    public void AdvanceToNextNode()
    {
        var n = Current;
        if (n != null) n.Deactivate(); // 念のため停止

        currentIndex++;
        if (currentIndex < nodes.Count) ActivateCurrent();
        else onAllCompleted?.Invoke();
    }

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
