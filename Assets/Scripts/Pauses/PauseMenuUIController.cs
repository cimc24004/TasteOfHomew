using UnityEngine;
using UnityEngine.EventSystems;
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
    
    private ConfigController configController;

    void Start()
    {
        // ボタンにリスナーを登録
        resumeButton.onClick.AddListener(OnResumeButtonClicked);
        settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        backToTitleButton.onClick.AddListener(OnBackToTitleButtonClicked);
        
        // ▼▼▼ ★★★ 修正箇所 (ラムダ式で引数 false を渡す) ★★★ ▼▼▼
        // backToPauseButton.onClick.AddListener(HideConfigPanel);
        backToPauseButton.onClick.AddListener(() => HideConfigPanel(false)); // false = 設定を適用しない
        // ▲▲▲ ★★★ 修正箇所 ★★★ ▲▲▲

        // 最初は非表示にしておく
        pauseMenuPanel.SetActive(false);
        configPanel.SetActive(false);
        
        configController = ConfigController.Instance;
    }

    // --- PauseMenuControllerから呼び出されるメソッド ---

    public void ShowPauseMenu()
    {
        //最初に選択させる
        EventSystem.current.SetSelectedGameObject(this.resumeButton.gameObject);
        pauseMenuPanel.SetActive(true);
        configPanel.SetActive(false); // 設定が開いていたら閉じる
    }

    public void HidePauseMenu()
    {
        // もし設定パネルが開いている状態でポーズ解除（ESCキーなど）された場合
        if (configPanel.activeSelf)
        {
            // 「閉じた」イベントを呼ぶ
            if (configController != null)
            {
                configController.OnCloseConfig(null); // 引数はnullのまま
            }
            // ▼▼▼ ★★★ 修正箇所 (false を渡す) ★★★ ▼▼▼
            // 変更を破棄する
            // configController.RevertPendingSettings();
            HideConfigPanel(false); // false = 設定を適用しない
            // ▲▲▲ ★★★ 修正箇所 ★★★ ▲▲▲
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
        
        // (変更なし)
        if (configController != null)
        {
            configController.OnOpenConfig();
        }
    }

    private void OnBackToTitleButtonClicked()
    {
        pauseController.BackToTitle();
    }

    // ▼▼▼ ★★★ 修正箇所 (引数を追加) ★★★ ▼▼▼
    /// <summary>
    /// 設定画面からポーズメニューに戻る
    /// </summary>
    /// <param name="settingsWereApplied">trueの場合、設定を適用する（リバートしない）</param>
    public void HideConfigPanel(bool settingsWereApplied)
    {
        // 1. ConfigControllerに「閉じた」ことを通知し、戻り先のフォーカス対象を渡す
        if (configController != null)
        {
            configController.OnCloseConfig(settingsButton.gameObject);
        }

        // 設定パネルを非表示にし、ポーズメニューを再表示する
        configPanel.SetActive(false);
        pauseMenuPanel.SetActive(true);

        // 2. 「適用」ボタンが押された *以外* の場合、変更を破棄(リバート)する
        if (!settingsWereApplied && configController != null) // configControllerのnullチェック追加
        {
            configController.RevertPendingSettings();
        }

        // 3. コントローラーフォーカスは ConfigController の OnCloseConfig で制御される
    }
    // ▲▲▲ ★★★ 修正箇所 ★★★ ▲▲▲
}