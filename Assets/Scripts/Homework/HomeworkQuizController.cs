using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// 宿題クイズのミニゲームを進行する。
///
/// 1 問の流れ:
///   1. 問題文を会話（DialogueController）として流す
///   2. 前置きのセリフを表示したまま、画面クリックを待つ
///   3. クリックされたら選択肢ボタンを並べる
///   4. 回答されたら ○× の演出と効果音を出し、次の問題へ進む
/// 問題ごとの設定で、ホラー演出（専用 BGM、○× 演出なし）にも切り替わる。
/// </summary>
[DisallowMultipleComponent]
public class HomeworkQuizController : MonoBehaviour
{
    // ===== Inspector =====

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

    // ===== 実行時の状態 =====

    private int currentIndex = -1;
    private readonly List<Button> spawnedChoiceButtons = new();
    private Tween currentFeedbackTween;
    private Tween currentBgmTween;

    // ===== Unity ライフサイクル =====

    private void Awake()
    {
        if (!panelRoot) Debug.LogError("[HomeworkQuiz] panelRoot 未設定");
        if (!choicesRoot) Debug.LogError("[HomeworkQuiz] choicesRoot 未設定");
        if (!choiceButtonPrefab) Debug.LogError("[HomeworkQuiz] choiceButtonPrefab 未設定");

        if (revealClickCatcher)
        {
            if (revealClickCatcher.targetGraphic) revealClickCatcher.targetGraphic.raycastTarget = true;
            revealClickCatcher.gameObject.SetActive(false);
        }

        HideFeedbackImage(correctFeedbackImage);
        HideFeedbackImage(wrongFeedbackImage);

        ShowPanel(false);
    }

    // ===== 公開 API =====

    [ContextMenu("StartQuiz")]
    public void StartQuiz() => StartQuizAsync().Forget();

    // ===== クイズ全体の進行 =====

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

        var question = questions[currentIndex];
        onQuestionStarted?.Invoke(currentIndex);

        UpdateHorrorBgm(question);
        await PresentQuestionAsync(question);
        WaitForClickToRevealChoices();
    }

    private async UniTaskVoid ProceedToNextQuestionAfterDelay()
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

    // ===== 1 問の出題 =====

    /// <summary>問題文を宿題の紙面に書き、会話として読み上げたあと、前置きのセリフを出したままにする。</summary>
    private async UniTask PresentQuestionAsync(QuizQuestionAsset question)
    {
        var dialogue = DialogueController.instance;

        dialogue.TryGetSequence(questionGroupKey, question.questionSequenceId, out var questionSequence);
        homeWorkBodyText.text = questionSequence.steps[0].localizedText.GetLocalizedString();

        if (dialogue != null && !string.IsNullOrWhiteSpace(question.questionSequenceId))
        {
            await dialogue.StartDialogueAsync(questionGroupKey, question.questionSequenceId);
        }

        // 前置きのセリフは、選択肢が選ばれるまで表示したままにする（「進む」入力を止めておく）
        if (dialogue != null && !string.IsNullOrWhiteSpace(prelineGroupKey) && !string.IsNullOrWhiteSpace(prelineSequenceId))
        {
            dialogue.PauseAdvance(true);
            dialogue.StartDialogueAsync(prelineGroupKey, prelineSequenceId).Forget();
        }
    }

    /// <summary>画面全体を覆うクリック受けを有効にし、クリックされたら選択肢を出す。</summary>
    private void WaitForClickToRevealChoices()
    {
        if (!revealClickCatcher)
        {
            Debug.LogError("[HomeworkQuiz] RevealClickCatcher が未アサインです");
            return;
        }

        revealClickCatcher.onClick.RemoveAllListeners();
        revealClickCatcher.onClick.AddListener(OnRevealChoicesClicked);
        revealClickCatcher.gameObject.SetActive(true);
        revealClickCatcher.transform.SetAsLastSibling();
        EventSystem.current.SetSelectedGameObject(revealClickCatcher.gameObject);
    }

    private void OnRevealChoicesClicked()
    {
        if (revealClickCatcher) revealClickCatcher.gameObject.SetActive(false);

        BuildChoices(questions[currentIndex]);
    }

    // ===== 選択肢 =====

    private void BuildChoices(QuizQuestionAsset question)
    {
        ClearChoices();

        // ローカライズ済みの選択肢があればそちらを、なければ旧データ（生の文字列）を使う
        List<LocalizedString> localizedChoices = question.localizedChoices;
        List<string> plainChoices = question.choices;
        bool useLocalized = localizedChoices != null && localizedChoices.Count > 0;

        if (!useLocalized && (plainChoices == null || plainChoices.Count == 0))
        {
            Debug.LogError("[HomeworkQuiz] choices が空");
            return;
        }

        int choiceCount = useLocalized ? localizedChoices.Count : plainChoices.Count;
        for (int i = 0; i < choiceCount; i++)
        {
            var button = Instantiate(choiceButtonPrefab, choicesRoot);
            if (!button.gameObject.activeSelf) button.gameObject.SetActive(true);
            button.interactable = true;
            if (button.targetGraphic) button.targetGraphic.raycastTarget = true;

            // 先頭の選択肢を選択状態にしておく（キーボード/ゲームパッド操作用）
            if (i == 0) EventSystem.current.SetSelectedGameObject(button.gameObject);

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            var localizeEvent = button.GetComponentInChildren<LocalizeStringEvent>(true);

            if (useLocalized)
            {
                if (localizeEvent)
                {
                    localizeEvent.StringReference = localizedChoices[i];
                }
                else if (label)
                {
                    label.text = "[LOCALIZE COMPONENT MISSING]";
                }
            }
            else
            {
                // 生の文字列を出すときは、ローカライズ側に上書きされないよう参照を外す
                if (localizeEvent) localizeEvent.StringReference = null;
                if (label) label.text = plainChoices[i];
            }

            int choiceIndex = i;
            button.onClick.AddListener(() => OnClickChoice(choiceIndex, button));
            spawnedChoiceButtons.Add(button);
        }

        choicesRoot.SetAsLastSibling();
    }

    private void OnClickChoice(int choiceIndex, Button clickedButton)
    {
        HideChoices(clickedButton);

        var question = questions[currentIndex];
        bool isCorrect = choiceIndex == question.correctIndex;
        EventSystem.current.SetSelectedGameObject(null);

        // ホラー演出の問題では ○× を出さない
        if (!question.horrorDirection)
        {
            PlayFeedbackAnimation(isCorrect);
        }
        PlayAnswerSfx(question, isCorrect);

        onAnswered?.Invoke(currentIndex, isCorrect);

        // 表示したままにしていた前置きのセリフを片付ける
        var dialogue = DialogueController.instance;
        if (dialogue != null)
        {
            dialogue.PauseAdvance(false);
            dialogue.StopCurrent();
        }

        bool isLastQuestion = currentIndex == questions.Count - 1;
        if (isLastQuestion)
        {
            CompleteQuiz();
        }
        else if (autoProceedNext)
        {
            ProceedToNextQuestionAfterDelay().Forget();
        }
    }

    private void HideChoices(Button clicked = null)
    {
        if (!hideAllChoicesOnClick)
        {
            if (clicked) clicked.gameObject.SetActive(false);
            return;
        }

        foreach (var button in spawnedChoiceButtons)
        {
            if (button) button.gameObject.SetActive(false);
        }
    }

    private void ClearChoices()
    {
        for (int i = spawnedChoiceButtons.Count - 1; i >= 0; i--)
        {
            if (spawnedChoiceButtons[i]) Destroy(spawnedChoiceButtons[i].gameObject);
        }
        spawnedChoiceButtons.Clear();
    }

    // ===== ○× の演出・効果音 =====

    /// <summary>○ または × の画像を、フェードインしながら拡大して見せる。</summary>
    private void PlayFeedbackAnimation(bool isCorrect)
    {
        Image image = isCorrect ? correctFeedbackImage : wrongFeedbackImage;
        float targetScale = isCorrect ? correctScaleFactor : wrongScaleFactor;
        if (image == null) return;

        currentFeedbackTween?.Kill();

        image.gameObject.SetActive(true);
        image.transform.localScale = Vector3.one;
        image.color = new Color(image.color.r, image.color.g, image.color.b, 0f);

        currentFeedbackTween = DOTween.Sequence()
            .Append(image.DOFade(1f, feedbackDuration))
            .Join(image.transform.DOScale(targetScale, feedbackDuration).SetEase(feedbackEase))
            .SetLink(image.gameObject)
            .Play();
    }

    private void PlayAnswerSfx(QuizQuestionAsset question, bool isCorrect)
    {
        if (question.muteSfxForThisQuestion || !sfxSource) return;

        // 問題ごとの上書き指定があればそれを、なければデフォルトの効果音を鳴らす
        var clip = isCorrect
            ? (question.correctSfxOverride ? question.correctSfxOverride : defaultCorrectSfx)
            : (question.wrongSfxOverride ? question.wrongSfxOverride : defaultWrongSfx);
        if (clip) sfxSource.PlayOneShot(clip);
    }

    private void ResetFeedbackImages()
    {
        currentFeedbackTween?.Kill();

        HideFeedbackImage(correctFeedbackImage);
        HideFeedbackImage(wrongFeedbackImage);
    }

    /// <summary>○× の画像を非表示・透明・等倍の初期状態に戻す。</summary>
    private void HideFeedbackImage(Image image)
    {
        if (image == null) return;

        image.gameObject.SetActive(false);
        image.color = new Color(image.color.r, image.color.g, image.color.b, 0f);
        image.transform.localScale = Vector3.one;
    }

    // ===== ホラー BGM =====

    /// <summary>ホラー演出の問題なら専用 BGM をフェードインし、通常の問題ならフェードアウトして止める。</summary>
    private void UpdateHorrorBgm(QuizQuestionAsset question)
    {
        currentBgmTween?.Kill();

        if (!question.horrorDirection)
        {
            if (horrorBgmSource && horrorBgmSource.isPlaying)
            {
                currentBgmTween = horrorBgmSource.DOFade(0, bgmFadeDuration)
                    .SetEase(Ease.Linear)
                    .OnComplete(() =>
                    {
                        horrorBgmSource.Stop();
                        horrorBgmSource.clip = null;
                    });
            }
            return;
        }

        if (!horrorBgmSource) return;

        // 曲が変わるとき、または止まっているときは、無音から再生を始める
        if (question.horrorSound != null && horrorBgmSource.clip != question.horrorSound)
        {
            horrorBgmSource.clip = question.horrorSound;
            horrorBgmSource.volume = 0;
            horrorBgmSource.Play();
        }

        if (!horrorBgmSource.isPlaying && horrorBgmSource.clip != null)
        {
            horrorBgmSource.volume = 0;
            horrorBgmSource.Play();
        }

        currentBgmTween = horrorBgmSource.DOFade(question.horrorVolume, bgmFadeDuration)
            .SetEase(Ease.Linear);
    }

    // ===== パネル表示 =====

    private void ShowPanel(bool show)
    {
        if (!panelRoot) return;

        panelRoot.SetActive(show);
        if (!show && revealClickCatcher) revealClickCatcher.gameObject.SetActive(false);
    }
}
