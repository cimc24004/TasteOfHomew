using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HomeworkQuizController : MonoBehaviour
{
    [Header("UI (SetActive 切替)")]
    [SerializeField] private GameObject panelRoot;        // 宿題UIルート
    [SerializeField] private Image sheetImage;            // 紙画像（Raycast無効に）
    [SerializeField] private Transform choicesRoot;       // 選択肢ボタン親（VerticalLayout推奨）
    [SerializeField] private Button choiceButtonPrefab;   // TMP_Text入りボタン

    [Header("問題セット")]
    [SerializeField] private List<QuizQuestionAsset> questions = new();

    [Header("Dialogue 設定")]
    [SerializeField] private string questionGroupKey  = "HWQ";   // 問題文グループ
    [SerializeField] private string prelineGroupKey   = "HW";    // 「正解は……？」グループ
    [SerializeField] private string prelineSequenceId = "Preline";

    [Header("SFX")]
    [SerializeField] private AudioSource sfxSource;      // PlayOnAwake OFF
    [SerializeField] private AudioClip defaultCorrectSfx;
    [SerializeField] private AudioClip defaultWrongSfx;

    [Header("進行")]
    [SerializeField] private bool autoProceedNext = true;         // 最終問以外は次の問題へ
    [SerializeField, Min(0f)] private float proceedDelaySeconds = 0.6f;

    [Header("クリック時の挙動")]
    [Tooltip("true: 押した瞬間に全ての選択肢ボタンを非表示 / false: 押したボタンだけ非表示")]
    [SerializeField] private bool hideAllChoicesOnClick = true;

    [Header("既存のクリック受け（Inspectorでアサイン）")]
    [SerializeField] private Button revealClickCatcher;  // 透明フルスクリーンの既存Button

    [Header("Events")]
    public UnityEvent onQuizStarted;
    public UnityEvent<int> onQuestionStarted;            // (index)
    public UnityEvent<int, bool> onAnswered;             // (index, isCorrect)
    public UnityEvent onQuizCompleted;                   // 互換用（従来イベント）
    [Tooltip("全ての問題に回答し終えた瞬間に発火（即クローズ後）")]
    public UnityEvent onAllQuestionsAnswered;            // ★ 追加

    // 内部
    private int currentIndex = -1;
    private readonly List<Button> spawned = new();

    void Awake()
    {
        if (!panelRoot) Debug.LogError("[HomeworkQuiz] panelRoot 未設定");
        if (!choicesRoot) Debug.LogError("[HomeworkQuiz] choicesRoot 未設定");
        if (!choiceButtonPrefab) Debug.LogError("[HomeworkQuiz] choiceButtonPrefab 未設定");

        // 紙はクリックをブロックしない
        if (sheetImage)
        {
            sheetImage.raycastTarget = false;
            sheetImage.transform.SetAsFirstSibling(); // 最背面
        }

        // 既存のクリック受けは最初はOFF
        if (revealClickCatcher)
        {
            if (revealClickCatcher.targetGraphic) revealClickCatcher.targetGraphic.raycastTarget = true;
            revealClickCatcher.gameObject.SetActive(false);
        }

        ShowPanel(false);
    }

    [ContextMenu("StartQuiz")]
    public void StartQuiz() => StartQuizAsync().Forget();

    private async UniTaskVoid StartQuizAsync()
    {
        if (questions == null || questions.Count == 0)
        {
            Debug.LogWarning("[HomeworkQuiz] 質問ゼロ");
            return;
        }

        currentIndex = -1;
        onQuizStarted?.Invoke();
        ShowPanel(true);
        await NextQuestionAsync();
    }

    private async UniTask NextQuestionAsync()
    {
        currentIndex++;

        // 全問終わっていれば即終了（保険）
        if (currentIndex >= questions.Count)
        {
            CompleteQuiz();
            return;
        }

        var q = questions[currentIndex];
        onQuestionStarted?.Invoke(currentIndex);

        // 紙更新（常に背面・Raycast無効）
        if (sheetImage)
        {
            sheetImage.sprite = q.sheetSprite;
            sheetImage.raycastTarget = false;
            sheetImage.transform.SetAsFirstSibling();
        }

        // ① 問題文（クリックで閉じるまで待つ）
        var dlg = DialogueController.instance;
        if (dlg != null && !string.IsNullOrWhiteSpace(q.questionSequenceId))
        {
            await dlg.StartDialogueAsync(questionGroupKey, q.questionSequenceId);
        }

        // ② 「正解は……？」を固定表示（ダイアログ進行は止める）
        if (dlg != null && !string.IsNullOrWhiteSpace(prelineGroupKey) && !string.IsNullOrWhiteSpace(prelineSequenceId))
        {
            dlg.PauseAdvance(true);                               // advance/skip無効。UIはそのまま。
            dlg.StartDialogueAsync(prelineGroupKey, prelineSequenceId).Forget();
        }

        // ③ “次のクリックで選択肢出現” → 既存の RevealClickCatcher を使う
        if (!revealClickCatcher)
        {
            Debug.LogError("[HomeworkQuiz] RevealClickCatcher が未アサインです（既存オブジェクトをInspectorで割り当ててください）");
        }
        else
        {
            revealClickCatcher.onClick.RemoveAllListeners();
            revealClickCatcher.onClick.AddListener(OnRevealChoicesClicked);
            revealClickCatcher.gameObject.SetActive(true);
            revealClickCatcher.transform.SetAsLastSibling(); // 最前面
        }
    }

    // === 「正解は……？」のあと、既存の RevealClickCatcher をクリックで呼ばれる ===
    private void OnRevealChoicesClicked()
    {
        // 受けはOFFにしてボタン操作を通す
        if (revealClickCatcher) revealClickCatcher.gameObject.SetActive(false);

        // 選択肢生成（この時点で「正解は……？」は表示されたまま）
        var q = questions[currentIndex];
        BuildChoices(q);
    }

    private void BuildChoices(QuizQuestionAsset q)
    {
        ClearChoices();

        if (q.choices == null || q.choices.Count == 0)
        {
            Debug.LogError("[HomeworkQuiz] choices が空");
            return;
        }

        for (int i = 0; i < q.choices.Count; i++)
        {
            var btn = Instantiate(choiceButtonPrefab, choicesRoot);

            // 念のためActive化
            if (!btn.gameObject.activeSelf) btn.gameObject.SetActive(true);
            btn.interactable = true;
            if (btn.targetGraphic) btn.targetGraphic.raycastTarget = true;

            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label) label.text = q.choices[i];

            int idx = i;
            btn.onClick.AddListener(() => OnClickChoice(idx, btn));
            spawned.Add(btn);
        }

        // 最前面に（紙より前に確実に出す）
        choicesRoot.SetAsLastSibling();
    }

    private void HideChoices(Button clicked = null)
    {
        if (hideAllChoicesOnClick)
        {
            foreach (var b in spawned)
                if (b) b.gameObject.SetActive(false);
        }
        else
        {
            if (clicked) clicked.gameObject.SetActive(false);
        }
    }

    private void OnClickChoice(int choiceIndex, Button clickedButton)
    {
        // 1) 押した瞬間にボタン非表示
        HideChoices(clickedButton);

        var q = questions[currentIndex];
        bool isCorrect = (choiceIndex == q.correctIndex);

        // 2) SFX
        if (!q.muteSfxForThisQuestion && sfxSource)
        {
            var clip = isCorrect
                ? (q.correctSfxOverride ? q.correctSfxOverride : defaultCorrectSfx)
                : (q.wrongSfxOverride   ? q.wrongSfxOverride   : defaultWrongSfx);
            if (clip) sfxSource.PlayOneShot(clip);
        }

        // 3) コールバック
        onAnswered?.Invoke(currentIndex, isCorrect);

        // 4) 「正解は……？」を閉じ、ダイアログ入力を元に戻す
        var dlg = DialogueController.instance;
        if (dlg != null)
        {
            dlg.PauseAdvance(false);
            dlg.StopCurrent();
        }

        // 5) 最終問なら即クローズ＆イベント、そうでなければ次へ
        bool isLast = (currentIndex == questions.Count - 1);
        if (isLast)
        {
            CompleteQuiz();               // ← 全問回答の瞬間に閉じてイベント発火
        }
        else if (autoProceedNext)
        {
            Proceed().Forget();
        }
    }

    private async UniTaskVoid Proceed()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, proceedDelaySeconds)));
        await NextQuestionAsync();
    }

    private void CompleteQuiz()
    {
        // クリック受けを確実にOFF
        if (revealClickCatcher) revealClickCatcher.gameObject.SetActive(false);

        // UIクローズ
        ShowPanel(false);

        // イベント発火（新しい方を先に、互換の onQuizCompleted も呼ぶ）
        onAllQuestionsAnswered?.Invoke();  // ★ 新イベント
        onQuizCompleted?.Invoke();         // 既存イベント
    }

    private void ClearChoices()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i]) Destroy(spawned[i].gameObject);
        }
        spawned.Clear();
    }

    private void ShowPanel(bool show)
    {
        if (!panelRoot) return;
        panelRoot.SetActive(show);

        // パネルOFF時はクリック受けもOFF
        if (!show && revealClickCatcher) revealClickCatcher.gameObject.SetActive(false);
    }
}
