using UnityEngine;
using UnityEngine.EventSystems; // ★ EventSystem を使うために追加
using UnityEngine.UI;

/// <summary>
/// ポーズメニューのUI要素を管理し、ユーザーの入力をPauseMenuControllerに伝える。
/// </summary>
public class PauseMenuUIController : MonoBehaviour
{
    [Header("Controller Reference")]
    [SerializeField] private PauseMenuController pauseController;

    [Header("UI Panels")]
    [SerializeField] private GameObject pauseMenuPanel; // ポーズメニュー全体のパネル
    [SerializeField] private GameObject configPanel;    // 設定画面のパネル

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button backToTitleButton;
    [SerializeField] private Button backToPauseButton; // 設定画面から戻るボタン
    
    [Header("Config Panel Focus")]
    [Tooltip("設定パネルを開いたときに最初にフォーカスするUI要素（例：Graphicsタブボタン）")]
    [SerializeField] private GameObject firstConfigElement; 
    
    private ConfigController configController;

    void Start()
    {
        // ボタンにリスナーを登録
        resumeButton.onClick.AddListener(OnResumeButtonClicked);
        settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        backToTitleButton.onClick.AddListener(OnBackToTitleButtonClicked);
        backToPauseButton.onClick.AddListener(HideConfigPanel);

        // 最初は非表示にしておく
        pauseMenuPanel.SetActive(false);
        configPanel.SetActive(false);
        
        configController = ConfigController.Instance;
    }

    // --- PauseMenuControllerから呼び出されるメソッド ---

    public void ShowPauseMenu()
    {
        // 最初に選択させる (これはポーズメニュー自身の制御なのでここ)
        EventSystem.current.SetSelectedGameObject(this.resumeButton.gameObject);
        pauseMenuPanel.SetActive(true);
        configPanel.SetActive(false); // 設定が開いていたら閉じる
    }

    public void HidePauseMenu()
    {
        if (configPanel.activeSelf)
        {
            if (configController != null)
            {
                // ★ 引数(null) を渡して呼ぶ
                configController.OnCloseConfig(null); 
            }
            configController.RevertPendingSettings();
        }

        pauseMenuPanel.SetActive(false);
        configPanel.SetActive(false);
    }

    // --- ボタンが押された時の処理 ---

    private void OnResumeButtonClicked()
    {
        pauseController.ResumeGame();
    }

    private void OnSettingsButtonClicked()
    {
        // ポーズメニューを非表示にし、設定パネルを表示する
        pauseMenuPanel.SetActive(false);
        configPanel.SetActive(true);
        
        // ▼▼▼ ★★★ ここを修正 ★★★ ▼▼▼
        // 1. ConfigControllerに「開いた」ことを通知し、フォーカス対象を渡す
        if (configController != null)
        {
            configController.OnOpenConfig(firstConfigElement);
        }
        // 2. このスクリプトでのフォーカス設定は削除
        // ▲▲▲ ★★★ ここを修正 ★★★ ▲▲▲
    }

    private void OnBackToTitleButtonClicked()
    {
        pauseController.BackToTitle();
    }

    // 設定画面からポーズメニューに戻るためのpublicメソッド
    public void HideConfigPanel()
    {
        // ▼▼▼ ★★★ ここを修正 ★★★ ▼▼▼
        // 1. ConfigControllerに「閉じた」ことを通知し、戻り先のフォーカス対象を渡す
        if (configController != null)
        {
            configController.OnCloseConfig(settingsButton.gameObject);
        }
        // ▲▲▲ ★★★ ここを修正 ★★★ ▲▲▲

        // 設定パネルを非表示にし、ポーズメニューを再表示する
        configPanel.SetActive(false);
        pauseMenuPanel.SetActive(true);
        configController.RevertPendingSettings();

        // 3. このスクリプトでのフォーカス設定は削除
    }
}