using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq; // ソートに必要
using Cysharp.Threading.Tasks;

[ExecuteAlways]
public class ContinueController : Singleton<ContinueController>
{
    [System.Serializable]
    public class ContinueData
    {
        public string ChapterName;
        public string SceneName;
        public string TeleportKey;
        public string EndingShareDictionaryKey;
        public string CurrentPhaseStepKey; 
        public GameFlowController.Phase CurrentPhase;
    }

    [Title("UI設定")]
    [Tooltip("一番下にある「戻る」ボタン（ナビゲーション連結用）")]
    private Button backButton; // ★ 追加：ここに「戻る」ボタンを入れてください

    [Title("コンティニューデータスロット")]
    [DictionaryDrawerSettings(KeyLabel = "Continue Button", ValueLabel = "Data")]
    [OnValueChanged("SetupContinueButtons")]
    public Dictionary<Button, ContinueData> continueSlots = new Dictionary<Button, ContinueData>();
    
    void Start()
    {
        if (Application.isPlaying) InitializeAsync().Forget();
        else SetupContinueButtons();
        backButton = GameStartMenuController.instance.ContinueToTopBackButton;
    }

    private async UniTaskVoid InitializeAsync()
    {
        await UniTask.NextFrame();
        SetupContinueButtons();
    }

    [Button("セットアップ実行", ButtonSizes.Large)]
    public void SetupContinueButtons()
    {
        if (continueSlots == null || continueSlots.Count == 0) return;

        if (Application.isPlaying)
        {
            // --- Playモード: 履歴チェックとナビゲーション再構築 ---
            
            // 1. データロード
            var saveController = SaveDataBaseController.instance;
            var visitedHistoryData = new PhaseStepController.PhaseStepSaveData();
            if (saveController != null && saveController.HasKey("PhaseStepController_VisitedHistory"))
            {
                var loaded = saveController.LoadData<PhaseStepController.PhaseStepSaveData>("PhaseStepController_VisitedHistory");
                if (loaded != null) visitedHistoryData = loaded;
            }

            // 2. 有効なボタンを管理するリスト
            List<Button> activeButtons = new List<Button>();

            foreach (var (button, data) in continueSlots)
            {
                if (button == null || data == null) continue;

                TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                
                // 訪問判定
                string phaseName = data.CurrentPhase.ToString();
                var phaseEntry = visitedHistoryData.visitedHistory?.FirstOrDefault(e => e.phaseName == phaseName);
                bool isVisited = phaseEntry != null && phaseEntry.visitedStepKeys.Contains(data.CurrentPhaseStepKey);

                if (isVisited)
                {
                    // === 有効 ===
                    if (buttonText) buttonText.text = data.ChapterName;
                    
                    button.interactable = true;
                    button.enabled = true; // コンポーネント復活
                    if (button.image) button.image.raycastTarget = true;

                    // ナビゲーション設定用にリストに追加
                    activeButtons.Add(button);

                    // クリックイベント設定
                    button.onClick.RemoveAllListeners();
                    ContinueData dataToLoad = data;
                    button.onClick.AddListener(() => LoadDataAndChangeScene(dataToLoad));
                }
                else
                {
                    // === 無効（???） ===
                    if (buttonText) buttonText.text = "???";
                    
                    button.interactable = false;
                    button.enabled = false; // ボタンとして死んでもらう
                    if (button.image) button.image.raycastTarget = false; // マウス無効
                    
                    button.onClick.RemoveAllListeners();
                }
            }

            // 3. ナビゲーションのリンクを繋ぎ直す（ここが解決策！）
            SetupDynamicNavigation(activeButtons);
        }
        else
        {
            // --- Editモード ---
            foreach (var (button, data) in continueSlots)
            {
                if (button == null) continue;
                TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null) buttonText.text = data.ChapterName;
            }
        }
    }

    /// <summary>
    /// 有効なボタンと「戻る」ボタンの間でナビゲーションを綺麗に繋ぐ
    /// </summary>
    private void SetupDynamicNavigation(List<Button> activeButtons)
    {
        // 画面上の順番（上から下）に並べ替える
        // これをしないとDictionaryの順序依存になり、ナビゲーションがぐちゃぐちゃになる恐れがある
        activeButtons = activeButtons.OrderBy(b => b.transform.GetSiblingIndex()).ToList();

        for (int i = 0; i < activeButtons.Count; i++)
        {
            Button currentBtn = activeButtons[i];
            Navigation nav = new Navigation();
            nav.mode = Navigation.Mode.Explicit; // 明示的に指定するモード

            // --- 上への移動 ---
            if (i > 0)
            {
                // 一つ上の有効なボタンへ
                nav.selectOnUp = activeButtons[i - 1];
            }
            else
            {
                // 一番上のボタンの場合の挙動（必要ならループさせるなど）
                nav.selectOnUp = null; 
            }

            // --- 下への移動 ---
            if (i < activeButtons.Count - 1)
            {
                // 一つ下の有効なボタンへ
                nav.selectOnDown = activeButtons[i + 1];
            }
            else
            {
                // 有効なチャプターの最後なら -> 「戻るボタン」へ
                if (backButton != null)
                {
                    nav.selectOnDown = backButton;
                }
            }

            currentBtn.navigation = nav;
        }

        // 「戻るボタン」側の設定（ここが重要！）
        if (backButton != null)
        {
            // 戻るボタンの既存設定を取得（左右移動などを維持したい場合のため）
            Navigation backNav = backButton.navigation;
            backNav.mode = Navigation.Mode.Explicit;

            if (activeButtons.Count > 0)
            {
                // 「戻る」の上は -> 「一番下の有効なチャプター」にする
                // これで間の "???" は完全にスキップされる
                backNav.selectOnUp = activeButtons.Last();
            }
            else
            {
                // 有効なチャプターが一つもない場合
                backNav.selectOnUp = null;
            }

            backButton.navigation = backNav;
        }
        
        Debug.Log($"[ContinueController] ナビゲーション再構築完了: 有効チャプター {activeButtons.Count} 個");
    }

    // ... LoadDataなどはそのまま ...
    public void LoadDataAndChangeScene(ContinueData data)
    {
        if (data == null) return;
        LoadData(data);
        if (!string.IsNullOrEmpty(data.SceneName))
            SceneManagerController.Instance.LoadScene(data.SceneName);
    }
    public void LoadData(ContinueData data, DataShareController dataManager = null)
    {
        if (dataManager == null) dataManager = DataShareController.instance;
        if (dataManager == null) return;

        dataManager.CurrentPhase = data.CurrentPhase;
        dataManager.CurrentPhaseStep = data.CurrentPhaseStepKey;
        if (!string.IsNullOrEmpty(data.EndingShareDictionaryKey)) dataManager.SetFlagToTrue(data.EndingShareDictionaryKey);
        if (!string.IsNullOrEmpty(data.TeleportKey)) dataManager.SetNextTeleportKey(data.TeleportKey);
    }
}