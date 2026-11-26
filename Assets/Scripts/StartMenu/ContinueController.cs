using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// タイトル画面のコンティニューメニュー。
///
/// チャプターごとのボタンに「そこから再開するためのデータ」を紐づけておき、
/// 訪問済み（セーブ履歴にある）のチャプターだけを選べるようにする。
/// まだ到達していないチャプターは "???" 表示で選択不可になる。
/// </summary>
[ExecuteAlways]
public class ContinueController : Singleton<ContinueController>
{
    private const string LockedChapterLabel = "???";

    // ===== Inspector =====

    [Title("コンティニューデータスロット")]
    [DictionaryDrawerSettings(KeyLabel = "Continue Button", ValueLabel = "Data")]
    [OnValueChanged("SetupContinueButtons")]
    public Dictionary<Button, ContinueData> continueSlots = new Dictionary<Button, ContinueData>();

    // ===== 実行時の状態 =====

    /// <summary>リストの一番下にある「戻る」ボタン。チャプターボタンとの上下移動をつなぐのに使う。</summary>
    private Button backButton;

    // ===== Unity ライフサイクル =====

    private void Start()
    {
        if (Application.isPlaying) InitializeAsync().Forget();
        else SetupContinueButtons();

        backButton = GameStartMenuController.instance.ContinueToTopBackButton;
    }

    private async UniTaskVoid InitializeAsync()
    {
        // セーブデータ側の初期化を 1 フレーム待つ
        await UniTask.NextFrame();
        SetupContinueButtons();
    }

    // ===== 公開 API =====

    /// <summary>
    /// 各チャプターボタンの表示と有効/無効を更新する。
    /// 再生中はセーブ履歴を見て有効/無効を決め、エディタ編集中はラベルの表示だけ更新する。
    /// </summary>
    [Button("セットアップ実行", ButtonSizes.Large)]
    public void SetupContinueButtons()
    {
        if (continueSlots == null || continueSlots.Count == 0) return;

        if (Application.isPlaying)
        {
            SetupSlotsFromSaveData();
        }
        else
        {
            RefreshLabelsForEditMode();
        }
    }

    /// <summary>コンティニューデータをゲームの共有状態に書き込み、そのチャプターのシーンへ移動する。</summary>
    public void LoadDataAndChangeScene(ContinueData data)
    {
        if (data == null) return;

        LoadData(data);
        if (!string.IsNullOrEmpty(data.SceneName))
        {
            SceneManagerController.Instance.LoadScene(data.SceneName);
        }
    }

    /// <summary>コンティニューデータ（フェーズ・ステップ・フラグ・開始位置）を DataShareController に書き込む。</summary>
    public void LoadData(ContinueData data, DataShareController dataManager = null)
    {
        if (dataManager == null) dataManager = DataShareController.instance;
        if (dataManager == null) return;

        dataManager.CurrentPhase = data.CurrentPhase;
        dataManager.CurrentPhaseStep = data.CurrentPhaseStepKey;
        if (!string.IsNullOrEmpty(data.EndingShareDictionaryKey)) dataManager.SetFlagToTrue(data.EndingShareDictionaryKey);
        if (!string.IsNullOrEmpty(data.TeleportKey)) dataManager.SetNextTeleportKey(data.TeleportKey);
    }

    // ===== スロットのセットアップ =====

    private void SetupSlotsFromSaveData()
    {
        var saveData = LoadVisitedHistory();
        var unlockedButtons = new List<Button>();

        foreach (var (button, data) in continueSlots)
        {
            if (button == null || data == null) continue;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (IsVisited(saveData, data))
            {
                UnlockSlot(button, label, data);
                unlockedButtons.Add(button);
            }
            else
            {
                LockSlot(button, label);
            }
        }

        SetupNavigation(unlockedButtons);
    }

    private void RefreshLabelsForEditMode()
    {
        foreach (var (button, data) in continueSlots)
        {
            if (button == null) continue;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = data.chapterName.GetLocalizedString();
        }
    }

    /// <summary>PhaseStepController が保存した訪問履歴を読み込む。セーブデータがなければ空の履歴を返す。</summary>
    private static PhaseStepController.PhaseStepSaveData LoadVisitedHistory()
    {
        var saveController = SaveDataBaseController.instance;
        if (saveController != null && saveController.HasKey(PhaseStepController.VisitedHistorySaveKey))
        {
            var loaded = saveController.LoadData<PhaseStepController.PhaseStepSaveData>(PhaseStepController.VisitedHistorySaveKey);
            if (loaded != null) return loaded;
        }

        return new PhaseStepController.PhaseStepSaveData();
    }

    /// <summary>このチャプターの開始地点（フェーズ + ステップ）に、プレイヤーが到達したことがあるか。</summary>
    private static bool IsVisited(PhaseStepController.PhaseStepSaveData saveData, ContinueData data)
    {
        string phaseName = data.CurrentPhase.ToString();
        string stepKey = data.CurrentPhaseStepKey;

        var phaseEntry = saveData.visitedHistory?.FirstOrDefault(e => e.phaseName == phaseName);
        if (phaseEntry == null || phaseEntry.visitedStepKeys == null) return false;

        if (phaseEntry.visitedStepKeys.Contains(stepKey)) return true;

        // "1 " と "1" のように、前後の空白だけが違うキーも一致とみなす
        foreach (var savedKey in phaseEntry.visitedStepKeys)
        {
            if (string.IsNullOrEmpty(savedKey)) continue;

            if (savedKey.Trim() == stepKey.Trim())
            {
                Debug.LogWarning($"[ContinueController] 空白を含むキーを許容しました: JSON='{savedKey}' / Inspector='{stepKey}'");
                return true;
            }
        }

        return false;
    }

    private void UnlockSlot(Button button, TextMeshProUGUI label, ContinueData data)
    {
        if (label) label.text = data.chapterName.GetLocalizedString();

        button.interactable = true;
        button.enabled = true;
        if (button.image) button.image.raycastTarget = true;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            LoadDataAndChangeScene(data);
            button.enabled = false; // 二重クリック防止
        });
    }

    private static void LockSlot(Button button, TextMeshProUGUI label)
    {
        if (label) label.text = LockedChapterLabel;

        // 選択もクリックもできないようにする
        button.interactable = false;
        button.enabled = false;
        if (button.image) button.image.raycastTarget = false;

        button.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// 上下キーでの移動先を、選べるチャプターだけを順につなぐように設定する。
    /// 最後のチャプターの下は「戻る」ボタン、「戻る」ボタンの上は最後のチャプターにつなぐ
    /// （間にある "???" のボタンは飛ばされる）。
    /// </summary>
    private void SetupNavigation(List<Button> unlockedButtons)
    {
        // Dictionary の列挙順ではなく、画面上の並び（上から下）で扱う
        var buttons = unlockedButtons.OrderBy(b => b.transform.GetSiblingIndex()).ToList();

        for (int i = 0; i < buttons.Count; i++)
        {
            var navigation = new Navigation { mode = Navigation.Mode.Explicit };

            if (i > 0)
            {
                navigation.selectOnUp = buttons[i - 1];
            }

            if (i < buttons.Count - 1)
            {
                navigation.selectOnDown = buttons[i + 1];
            }
            else if (backButton != null)
            {
                navigation.selectOnDown = backButton;
            }

            buttons[i].navigation = navigation;
        }

        if (backButton != null)
        {
            // 左右移動などの既存設定は残したまま、上方向だけ差し替える
            Navigation backNavigation = backButton.navigation;
            backNavigation.mode = Navigation.Mode.Explicit;
            backNavigation.selectOnUp = buttons.Count > 0 ? buttons[buttons.Count - 1] : null;
            backButton.navigation = backNavigation;
        }

        Debug.Log($"[ContinueController] ナビゲーション再構築完了: 有効チャプター {buttons.Count} 個");
    }

    // ===== データ定義 =====

    /// <summary>1 チャプター分の「ここから再開する」ためのデータ。</summary>
    [System.Serializable]
    public class ContinueData
    {
        public LocalizedString chapterName;
        public string SceneName;
        public string TeleportKey;
        public string EndingShareDictionaryKey;
        public string CurrentPhaseStepKey;
        public GameFlowController.Phase CurrentPhase;
    }
}
