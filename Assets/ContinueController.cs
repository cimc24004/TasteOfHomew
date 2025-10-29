using UnityEngine;
using UnityEngine.UI; // Button を使うために追加
using TMPro; // TextMeshPro を使うために追加
using Sirenix.OdinInspector; // Odin Inspector を使うために追加
using System.Collections.Generic; // Dictionary を使うために追加
using System.Linq; // ★ 追加： .FirstOrDefault() , .Contains() を使うために必要

/// <summary>
/// コンティニューボタンと、それに紐づくロード用データを管理するクラス
/// ★ 変更：訪問履歴をチェックし、未訪問のスロットは無効化する機能を追加
/// </summary>
[ExecuteAlways]
public class ContinueController : SerializedMonoBehaviour
{
    /// <summary>
    /// コンティニュー（セーブ）スロットごとの詳細データ
    /// </summary>
    [System.Serializable]
    public class ContinueData
    {
        [Tooltip("ボタンに表示するチャプター名")]
        public string ChapterName;
        
        [Tooltip("ロード時に遷移するシーン名")]
        public string SceneName;

        [Tooltip("ロード時にDataShareControllerに予約するテレポートキー")]
        public string TeleportKey;

        [Tooltip("ロード時にDataShareControllerに設定するEndingShareDictionaryのキー名")]
        public string EndingShareDictionaryKey;
        
        [Tooltip("ロード時にDataShareControllerに設定するCurrentPhaseStepの値")]
        public string CurrentPhaseStepKey; 

        [Tooltip("ロード時にDataShareControllerに設定するCurrentPhase")]
        public GameFlowController.Phase CurrentPhase;
    }
    [Title("コンティニューデータスロット")]
    [InfoBox("Key (Button): データをロードするUnityのButtonコンポーネント\nValue (ContinueData): そのボタンに紐づくセーブデータ")]
    [DictionaryDrawerSettings(KeyLabel = "Continue Button", ValueLabel = "Data")]
    
    [OnValueChanged("SetupContinueButtons")] // Inspectorでの値変更時に自動でこの関数を呼ぶ
    
    public Dictionary<Button, ContinueData> continueSlots = new Dictionary<Button, ContinueData>();
    
    // ★ 変更：Start()はSetupContinueButtons()を呼ぶだけ（[ExecuteAlways]のため両モードで実行）
    void Start()
    {
        SetupContinueButtons();
    }
    
    /// <summary>
    /// ★ 全面的に書き換え ★
    /// Dictionaryを走査し、各ボタンをセットアップします。
    /// [Playモード時] 訪問履歴をチェックし、テキスト(ChapterName/???)、interactable、クリックイベントを設定します。
    /// [Editモード時] デザイナーが見やすいよう、テキストをChapterNameに設定するだけです。
    /// </summary>
    [Button("コンティニューボタンのセットアップ（テキスト更新とクリックイベント設定）", ButtonSizes.Large)]
    public void SetupContinueButtons()
    {
        if (continueSlots == null || continueSlots.Count == 0)
        {
            return;
        }

        // ★ 変更：ランタイム（Playモード）かどうかで処理を完全に分岐
        if (Application.isPlaying)
        {
            // === プレイモード時の処理 ===
            
            // 1. ★ セーブコントローラーと履歴データを取得
            var saveController = SaveDataBaseController.instance;
            if (saveController == null)
            {
                Debug.LogError("[ContinueController] SaveDataBaseController.Instance が見つかりません！");
                return;
            }
            
            // 履歴データをロード
            var visitedHistoryData = saveController.LoadData<PhaseStepController.PhaseStepSaveData>("PhaseStepController_VisitedHistory");
            if (visitedHistoryData == null)
            {
                // まだセーブデータがない場合、ヌルチェックを避けるため空のインスタンスを生成
                visitedHistoryData = new PhaseStepController.PhaseStepSaveData(); 
            }

            // 2. ★ 訪問済みかをチェックするための内部ヘルパー関数
            bool CheckIfVisited(GameFlowController.Phase phase, string stepKey)
            {
                if (visitedHistoryData.visitedHistory == null) return false;
                
                // Phaseを文字列名に変換
                string phaseName = phase.ToString();
                
                // セーブデータから該当するPhaseのエントリを探す
                var phaseEntry = visitedHistoryData.visitedHistory.FirstOrDefault(entry => entry.phaseName == phaseName);

                if (phaseEntry == null)
                {
                    // このPhaseの履歴がまだない
                    return false; 
                }

                // 該当Phaseの履歴リストに、指定のstepKeyが含まれているかチェック
                return phaseEntry.visitedStepKeys.Contains(stepKey);
            }

            // 3. ★ 各ボタンをセットアップ
            foreach (var (button, data) in continueSlots)
            {
                if (button == null || data == null) continue;

                TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                
                // 3-1. 訪問履歴をチェック
                bool isVisited = CheckIfVisited(data.CurrentPhase, data.CurrentPhaseStepKey);

                if (isVisited)
                {
                    // 訪問済み
                    if (buttonText != null) buttonText.text = data.ChapterName;
                    button.interactable = true; // ★ クリック可能に
                    
                    // リスナー設定
                    button.onClick.RemoveAllListeners(); // 念のためクリア
                    ContinueData dataToLoad = data; // ループ変数キャプチャ対策
                    button.onClick.AddListener(() => LoadDataAndChangeScene(dataToLoad));
                }
                else
                {
                    // 未訪問
                    if (buttonText != null) buttonText.text = "???"; // ★ テキストを"???"に
                    button.interactable = false; // ★ クリック不可に
                    
                    // リスナーを削除
                    button.onClick.RemoveAllListeners();
                }
            }
            
            Debug.Log("[ContinueController] コンティニューボタンのセットアップ（ランタイム・履歴チェック完了）");
        }
        else
        {
            // === エディットモード時の処理 ===
            // (OnValueChangedや[ExecuteAlways]のStartで呼ばれる)
            // デザイナーが見やすいように、テキストだけ設定する
            foreach (var (button, data) in continueSlots)
            {
                if (button == null || data == null) continue;
                
                TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    // 履歴チェックはせず、設定されたChapterNameをそのまま表示
                    buttonText.text = data.ChapterName;
                }
                // エディットモードではリスナーやinteractableは設定しない
            }
        }
    }


    /// <summary>
    /// 特定のContinueDataを使ってDataShareControllerを更新します。(変更なし)
    /// </summary>
    public void LoadData(ContinueData data, DataShareController dataManager = null)
    {
        if (data == null)
        {
            Debug.LogError("ロードするデータがnullです。");
            return;
        }

        if (dataManager == null)
        {
            dataManager = DataShareController.instance; // .instance
            if (dataManager == null)
            {
                Debug.LogError("DataShareController.instance が見つかりません！ データロードに失敗しました。");
                return;
            }
        }

        Debug.Log($"データ設定を適用: {data.ChapterName}");
                
        // 進行度データを設定
        dataManager.CurrentPhase = data.CurrentPhase;
        dataManager.CurrentPhaseStep = data.CurrentPhaseStepKey;

        // フラグを設定
        if (!string.IsNullOrEmpty(data.EndingShareDictionaryKey))
        {
            dataManager.SetFlagToTrue(data.EndingShareDictionaryKey);
        }

        // テレポートキーを予約
        if (!string.IsNullOrEmpty(data.TeleportKey))
        {
            dataManager.SetNextTeleportKey(data.TeleportKey);
        }
    }

    /// <summary>
    /// データロードとシーン遷移を実行する関数 (変更なし)
    /// </summary>
    public void LoadDataAndChangeScene(ContinueData data)
    {
        if (data == null)
        {
            Debug.LogError("ロードするデータがnullです。");
            return;
        }
        
        Debug.Log($"ボタンクリック: {data.ChapterName}");

        // --- 1. データをロード（テレポート予約もここに含まれる） ---
        LoadData(data); 

        // --- 2. シーンを遷移 ---
        if (!string.IsNullOrEmpty(data.SceneName))
        {
            SceneManagerController sm = SceneManagerController.Instance; 
            if (sm != null)
            {
                Debug.Log($"シーン '{data.SceneName}' に遷移します。");
                sm.LoadScene(data.SceneName); // .LoadScene() を呼び出す
            }
            else
            {
                Debug.LogError("SceneManagerController.Instance が見つかりません！ シーン遷移できません。");
            }
        }
        else
        {
            Debug.LogWarning($"ロードデータ '{data.ChapterName}' に SceneName が設定されていません。");
        }
    }
}