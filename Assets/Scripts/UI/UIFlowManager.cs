using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// IDベースでUIパネルの表示・切り替え・スタック（重ね表示）を管理する
/// 非常に強力なUIフローマネージャー
/// </summary>
public class UIFlowManager : Singleton<UIFlowManager>
{
    [Header("パネルの登録")]
    [Tooltip("すべてのUIパネルをID（文字列）とCanvasGroupで関連付けます")]
    [SerializeField]
    private Dictionary<string, CanvasGroup> registeredPanels = new Dictionary<string, CanvasGroup>();

    [Header("初期設定")]
    [Tooltip("最初に表示するパネルのID")]
    [SerializeField]
    private string startingPanelID;

    [Header("フェード設定")]
    [SerializeField]
    private float defaultFadeDuration = 0.4f;

    // 現在アクティブなパネルをスタック（積み重ね）で管理
    private Stack<CanvasGroup> activePanelStack = new Stack<CanvasGroup>();

    void Start()
    {
        // 1. すべてのパネルを初期化（非表示・操作不可）
        foreach (var panel in registeredPanels.Values)
        {
            panel.alpha = 0;
            panel.interactable = false;
            panel.blocksRaycasts = false;
        }

        // 2. スタートパネルだけを表示
        if (!string.IsNullOrEmpty(startingPanelID))
        {
            SwitchToPanel(startingPanelID);
        }
    }

    /// <summary>
    /// 【全画面切り替え】
    /// 現在のパネルをすべて閉じ、指定したIDのパネルを新しく開きます。
    /// FadeOutがすべて完了してからFadeInが開始されます。
    /// </summary>
    public void SwitchToPanel(string panelID)
    {
        if (!ValidatePanelID(panelID)) return;

        CanvasGroup panelToShow = registeredPanels[panelID];

        int panelsToHideCount = activePanelStack.Count;

        // (A) 隠すパネルがない場合（初回起動時など）
        if (panelsToHideCount == 0)
        {
            FadeIn(panelToShow);
            activePanelStack.Push(panelToShow);
            return;
        }

        int panelsFadedOut = 0; // フェードアウト完了カウンター

        // (B) 隠すパネルがある場合
        // 1. スタックにあるパネルをすべてフェードアウト
        while (activePanelStack.Count > 0)
        {
            CanvasGroup panelToHide = activePanelStack.Pop();

            panelToHide.interactable = false;
            panelToHide.blocksRaycasts = false;

            // OnComplete でカウンターを増やす
            panelToHide.DOFade(0, defaultFadeDuration).OnComplete(() =>
            {
                panelsFadedOut++;
                // すべてのFadeOutが完了したら（カウンターが一致したら）
                if (panelsFadedOut == panelsToHideCount)
                {
                    // 2. 新しいパネルをフェードイン
                    FadeIn(panelToShow);
                    activePanelStack.Push(panelToShow); // スタックの新しい「底」になる
                }
            });
        }
    }

    /// <summary>
    /// 【モーダル（重ね）表示】
    /// 現在のパネルの上に、指定したIDのパネルを重ねて開きます。
    /// （この関数は修正なし）
    /// </summary>
    public void ShowPanel(string panelID)
    {
        if (!ValidatePanelID(panelID)) return;

        // 1. 現在のトップパネルを操作不可にする（モーダルの後ろをクリックさせないため）
        if (activePanelStack.Count > 0)
        {
            var currentTopPanel = activePanelStack.Peek();
            currentTopPanel.interactable = false;
        }

        // 2. 新しいパネルをフェードイン
        CanvasGroup panelToShow = registeredPanels[panelID];
        FadeIn(panelToShow);
        activePanelStack.Push(panelToShow); // スタックの一番上に積む
    }

    /// <summary>
    /// 【モーダルを閉じる】
    /// 一番手前にあるパネルを閉じます。
    /// FadeOutが完了してから下のパネルが操作可能になります。
    /// </summary>
    public void HideCurrentPanel()
    {
        if (activePanelStack.Count == 0) return;

        // 1. 一番上のパネルをフェードアウト
        CanvasGroup panelToHide = activePanelStack.Pop();

        panelToHide.interactable = false;
        panelToHide.blocksRaycasts = false;

        // OnComplete で下のパネルを操作可能にする
        panelToHide.DOFade(0, defaultFadeDuration).OnComplete(() =>
        {
            // FadeOut完了後
            // 2. 1つ下のパネル（新しいトップ）を操作可能に戻す
            if (activePanelStack.Count > 0)
            {
                var newTopPanel = activePanelStack.Peek();
                newTopPanel.interactable = true;
            }
        });
    }

    // --- 内部ヘルパー ---

    private void FadeIn(CanvasGroup cg)
    {
        // 念のためアルファを0にリセットしてからフェード開始
        cg.alpha = 0;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        cg.DOFade(1, defaultFadeDuration);
    }

    // FadeOutヘルパーはOnCompleteのロジックが複雑化したため削除し、
    // SwitchToPanel と HideCurrentPanel に直接（インラインで）記述しました。

    private bool ValidatePanelID(string panelID)
    {
        if (string.IsNullOrEmpty(panelID) || !registeredPanels.ContainsKey(panelID))
        {
            Debug.LogError($"[UIFlowManager] Panel ID '{panelID}' が登録されていません。");
            return false;
        }
        return true;
    }
}
