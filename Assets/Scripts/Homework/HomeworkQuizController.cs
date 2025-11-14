using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.Localization; // ← 【追加】
using UnityEngine.Localization.Components; // ← 【追加】

[DisallowMultipleComponent]
public class HomeworkQuizController : MonoBehaviour
{
    // ... (UI, Feedback, SFX Fields are omitted) ...
    [Header("UI (SetActive 切替)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform choicesRoot;
    [SerializeField] private Button choiceButtonPrefab;
    [SerializeField] private TextMeshProUGUI homeWorkBodyText;

    [Header("Feedback UI")]
    [SerializeField] private Image correctFeedbackImage; 
    [SerializeField] private Image wrongFeedbackImage;   

    [Header("Feedback Animation Settings")]
    [SerializeField] private float feedbackDuration = 0.3f; 
    [SerializeField] private float correctScaleFactor = 1.5f; 
    [SerializeField] private float wrongScaleFactor = 1.2f;  
    [SerializeField] private Ease feedbackEase = Ease.OutBack; 

    [Header("問題セット")]
    [SerializeField] private List<QuizQuestionAsset> questions = new();

    [Header("Dialogue 設定")]
    [SerializeField] private string questionGroupKey = "HWQ";
    [SerializeField] private string prelineGroupKey = "HW";
    [SerializeField] private string prelineSequenceId = "Preline";

    [Header("SFX")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip defaultCorrectSfx;
    [SerializeField] private AudioClip defaultWrongSfx;

    [Header("BGM")]
    [SerializeField] private AudioSource horrorBgmSource;
    [SerializeField] private float bgmFadeDuration = 1.0f; 

    [Header("進行")]
    [SerializeField] private bool autoProceedNext = true;
    [SerializeField, Min(0f)] private float proceedDelaySeconds = 0.6f;

    [Header("クリック時の挙動")]
    [Tooltip("true: 押した瞬間に全ての選択肢ボタンを非表示 / false: 押したボタンだけ非表示")]
    [SerializeField] private bool hideAllChoicesOnClick = true;

    [Header("既存のクリック受け（Inspectorでアサイン）")]
    [SerializeField] private Button revealClickCatcher;

    [Header("Events")]
    public UnityEvent onQuizStarted;
    public UnityEvent<int> onQuestionStarted;
    public UnityEvent<int, bool> onAnswered;
    public UnityEvent onQuizCompleted;
    [Tooltip("全ての問題に回答し終えた瞬間に発火（即クローズ後）")]
    public UnityEvent onAllQuestionsAnswered;

    private int currentIndex = -1;
    private readonly List<Button> spawned = new();
    private Tween currentFeedbackTween;
    private Tween currentBgmTween;

    void Awake()
    {
        if (!panelRoot) Debug.LogError("[HomeworkQuiz] panelRoot 未設定");
        if (!choicesRoot) Debug.LogError("[HomeworkQuiz] choicesRoot 未設定");
        if (!choiceButtonPrefab) Debug.LogError("[HomeworkQuiz] choiceButtonPrefab 未設定");

        if (revealClickCatcher)
        {
            if (revealClickCatcher.targetGraphic) revealClickCatcher.targetGraphic.raycastTarget = true;
            revealClickCatcher.gameObject.SetActive(false);
        }

        SetupFeedbackImage(correctFeedbackImage);
        SetupFeedbackImage(wrongFeedbackImage);

        ShowPanel(false);
    }

    private void SetupFeedbackImage(Image image)
    {
        if (image == null) return;
        image.gameObject.SetActive(false);
        image.color = new Color(image.color.r, image.color.g, image.color.b, 0f);
        image.transform.localScale = Vector3.one;
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
        ResetFeedbackImages();

        currentIndex++;
        if (currentIndex >= questions.Count)
        {
            CompleteQuiz();
            return;
        }

        var q = questions[currentIndex];
        onQuestionStarted?.Invoke(currentIndex);

        currentBgmTween?.Kill();

        // --- BGM ロジックは省略 (変更なし) ---
        if (q.horrorDirection)
        {
            if (horrorBgmSource)
            {
                if (q.horrorSound != null && horrorBgmSource.clip != q.horrorSound)
                {
                    horrorBgmSource.clip = q.horrorSound;
                    horrorBgmSource.volume = 0;
                    horrorBgmSource.Play();
                }
                
                if (!horrorBgmSource.isPlaying && horrorBgmSource.clip != null)
                {
                    horrorBgmSource.volume = 0;
                    horrorBgmSource.Play();
                }

                currentBgmTween = horrorBgmSource.DOFade(q.horrorVolume, bgmFadeDuration)
                    .SetEase(Ease.Linear);
            }
        }
        else
        {
            if (horrorBgmSource && horrorBgmSource.isPlaying)
            {
                currentBgmTween = horrorBgmSource.DOFade(0, bgmFadeDuration)
                    .SetEase(Ease.Linear)
                    .OnComplete(() => {
                        horrorBgmSource.Stop();
                        horrorBgmSource.clip = null;
                    });
            }
        }
        // --- BGM ロジックここまで ---

        var dlg = DialogueController.instance;
        
        // ▼▼▼ 【修正】 DialogueController に任せるため、この行を削除またはコメントアウト ▼▼▼
        dlg.TryGetSequence(questionGroupKey, q.questionSequenceId, out var seqAsset);
        homeWorkBodyText.text = seqAsset.steps[0].text;
        // ▲▲▲ 【修正】 ▲▲▲

        if (dlg != null && !string.IsNullOrWhiteSpace(q.questionSequenceId))
        {
            // 問題文の表示 (DialogueControllerが翻訳済みテキストを流してくれる)
            await dlg.StartDialogueAsync(questionGroupKey, q.questionSequenceId);
        }

        if (dlg != null && !string.IsNullOrWhiteSpace(prelineGroupKey) && !string.IsNullOrWhiteSpace(prelineSequenceId))
        {
            dlg.PauseAdvance(true);
            dlg.StartDialogueAsync(prelineGroupKey, prelineSequenceId).Forget();
        }

        if (!revealClickCatcher)
        {
            Debug.LogError("[HomeworkQuiz] RevealClickCatcher が未アサインです");
        }
        else
        {
            revealClickCatcher.onClick.RemoveAllListeners();
            revealClickCatcher.onClick.AddListener(OnRevealChoicesClicked);
            revealClickCatcher.gameObject.SetActive(true);
            revealClickCatcher.transform.SetAsLastSibling();
        }
    }

    // ... (Feedback Images and OnRevealChoicesClicked are omitted) ...
    private void ResetFeedbackImages()
    {
        currentFeedbackTween?.Kill();

        if (correctFeedbackImage)
        {
            correctFeedbackImage.gameObject.SetActive(false);
            correctFeedbackImage.color = new Color(correctFeedbackImage.color.r, correctFeedbackImage.color.g, correctFeedbackImage.color.b, 0f);
            correctFeedbackImage.transform.localScale = Vector3.one;
        }
        if (wrongFeedbackImage)
        {
            wrongFeedbackImage.gameObject.SetActive(false);
            wrongFeedbackImage.color = new Color(wrongFeedbackImage.color.r, wrongFeedbackImage.color.g, wrongFeedbackImage.color.b, 0f);
            wrongFeedbackImage.transform.localScale = Vector3.one;
        }
    }

    private void OnRevealChoicesClicked()
    {
        if (revealClickCatcher) revealClickCatcher.gameObject.SetActive(false);
        var q = questions[currentIndex];
        BuildChoices(q);
    }
    // ... (omitted) ...

    private void BuildChoices(QuizQuestionAsset q)
    {
        ClearChoices();

        // ▼▼▼ 【移行ロジック】 LocalizedString リストを使うか、古い string リストを使うか判断 ▼▼▼
        List<LocalizedString> locChoices = q.localizedChoices;
        List<string> oldChoices = q.choices;
        
        bool useLocalized = (locChoices != null && locChoices.Count > 0);

        if (!useLocalized && (oldChoices == null || oldChoices.Count == 0))
        {
            Debug.LogError("[HomeworkQuiz] choices が空");
            return;
        }
        
        int count = useLocalized ? locChoices.Count : oldChoices.Count;

        for (int i = 0; i < count; i++)
        {
            var btn = Instantiate(choiceButtonPrefab, choicesRoot);
            if (!btn.gameObject.activeSelf) btn.gameObject.SetActive(true);
            btn.interactable = true;
            if (btn.targetGraphic) btn.targetGraphic.raycastTarget = true;
            if(i == 0) 
                EventSystem.current.SetSelectedGameObject(btn.gameObject);
            
            // TextMeshProUGUI と LocalizeStringEvent を取得
            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            var localizeEvent = btn.GetComponentInChildren<LocalizeStringEvent>(true); 

            if (useLocalized)
            {
                // New system: LocalizedStringを Localize Event に渡す
                if (localizeEvent)
                {
                    localizeEvent.StringReference = locChoices[i];
                }
                else if (label)
                {
                    label.text = "[LOCALIZE COMPONENT MISSING]"; // Error fallback
                }
            }
            else
            {
                // Old system: 古い string をそのまま代入
                if (localizeEvent) localizeEvent.StringReference = null; // Localizerを無効化
                if (label) label.text = oldChoices[i]; // 生の文字列を代入
            }

            int idx = i;
            btn.onClick.AddListener(() => OnClickChoice(idx, btn));
            spawned.Add(btn);
        }
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
        HideChoices(clickedButton);
        
        var q = questions[currentIndex];
        bool isCorrect = (choiceIndex == q.correctIndex);
        EventSystem.current.SetSelectedGameObject(null);
        
        // --- Feedback and SFX logic is omitted (no change) ---
        if (!q.horrorDirection)
        {
            Image targetImage = null;
            float targetScale = 1f;

            if (isCorrect)
            {
                targetImage = correctFeedbackImage;
                targetScale = correctScaleFactor;
            }
            else
            {
                targetImage = wrongFeedbackImage;
                targetScale = wrongScaleFactor;
            }

            if (targetImage != null)
            {
                currentFeedbackTween?.Kill();

                targetImage.gameObject.SetActive(true);
                targetImage.transform.localScale = Vector3.one;
                targetImage.color = new Color(targetImage.color.r, targetImage.color.g, targetImage.color.b, 0f);

                currentFeedbackTween = DOTween.Sequence()
                    .Append(targetImage.DOFade(1f, feedbackDuration))
                    .Join(targetImage.transform.DOScale(targetScale, feedbackDuration).SetEase(feedbackEase))
                    .SetLink(targetImage.gameObject)
                    .Play();
            }
        }

        if (!q.muteSfxForThisQuestion && sfxSource)
        {
            var clip = isCorrect
                ? (q.correctSfxOverride ? q.correctSfxOverride : defaultCorrectSfx)
                : (q.wrongSfxOverride ? q.wrongSfxOverride : defaultWrongSfx);
            if (clip) sfxSource.PlayOneShot(clip);
        }

        onAnswered?.Invoke(currentIndex, isCorrect);

        var dlg = DialogueController.instance;
        if (dlg != null)
        {
            dlg.PauseAdvance(false);
            dlg.StopCurrent();
        }

        bool isLast = (currentIndex == questions.Count - 1);
        if (isLast)
        {
            CompleteQuiz();
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
        if (revealClickCatcher) revealClickCatcher.gameObject.SetActive(false);
        
        currentBgmTween?.Kill();

        if (horrorBgmSource && horrorBgmSource.isPlaying)
        {
            horrorBgmSource.DOFade(0, bgmFadeDuration).OnComplete(() => horrorBgmSource.Stop());
        }
        
        ResetFeedbackImages();
        ShowPanel(false);

        onAllQuestionsAnswered?.Invoke();
        onQuizCompleted?.Invoke();
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

        if (!show && revealClickCatcher) revealClickCatcher.gameObject.SetActive(false);
    }
}