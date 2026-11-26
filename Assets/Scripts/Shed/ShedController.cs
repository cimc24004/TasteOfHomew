using DG.Tweening;
using UnityEngine;
using UnityEngine.Assertions;

public class ShedController : Singleton<ShedController>
{
    [SerializeField] private GameObject FootStepGhostWhenDiary;
    [SerializeField] private GameObject Diary;

    [SerializeField] private AudioSource horrorBGM;

    [SerializeField] private ReadablePickup diaryReadablePickup;
    private AudioSource footstepAudioSource;

    private bool diaryReadingNow;

    // 変更点①: ゲーム全体で一度だけ再生するためのフラグ
    private bool hasHorrorSequencePlayedOnce = false;

    private Tween footstepTween;

    private void Start()
    {
        // 明示的アサート（設定漏れは開発中にすぐ気づける）
        Assert.IsNotNull(diaryReadablePickup, "diaryReadablePickup is not assigned on ShedController");
        Assert.IsNotNull(horrorBGM, "horrorBGM is not assigned on ShedController");
        Assert.IsNotNull(FootStepGhostWhenDiary, "FootStepGhostWhenDiary is not assigned on ShedController");
        Assert.IsNotNull(Diary, "Diary is not assigned on ShedController");

        footstepAudioSource = FootStepGhostWhenDiary.GetComponent<AudioSource>();
        diaryReadablePickup.onOpenedEveryTime.AddListener(OnDiaryOpened);
        diaryReadablePickup.onPageChanged.AddListener(OnPageChanged);
        diaryReadablePickup.onClosedEveryTime?.AddListener(OnDiaryClosed);
    }

    private void OnDestroy()
    {
        // 解除
        if (diaryReadablePickup != null)
        {
            diaryReadablePickup.onOpenedEveryTime.RemoveListener(OnDiaryOpened);
            diaryReadablePickup.onPageChanged.RemoveListener(OnPageChanged);
            if (diaryReadablePickup.onClosedEveryTime != null)
                diaryReadablePickup.onClosedEveryTime.RemoveListener(OnDiaryClosed);
        }
    }

    private void OnDiaryOpened()
    {
        diaryReadingNow = true;
        // 変更点②: 日記を開くたびにフラグをリセットする処理を削除
        // horrorStarted = false; // この行を削除
    }

    private void OnDiaryClosed()
    {
        diaryReadingNow = false;
        // BGMを止める
        horrorBGM.DOFade(0f, 1f).OnComplete(() => horrorBGM.Stop());
        // 足音を止める
        footstepAudioSource.DOFade(0f, 1f).OnComplete(() => { footstepAudioSource.gameObject.SetActive(false); });
        footstepTween?.Kill();
    }

    private void OnPageChanged(int _pageNow)
    {
        if (!diaryReadingNow) return;

        int maxPage = diaryReadablePickup.readable.pages.Count;
        if (maxPage <= 0) return;

        int mid = maxPage / 2;

        // 変更点③: 「まだ一度も再生されていない」かつ「中間ページを超えた」場合にのみ実行
        if (!hasHorrorSequencePlayedOnce && _pageNow >= mid)
        {
            // フラグをtrueにして、このブロックが二度と実行されないようにする
            hasHorrorSequencePlayedOnce = true;

            // BGM再生開始
            if (!horrorBGM.isPlaying)
            {
                 // 最初は音量0で再生開始し、ゆっくりフェードインさせる
                horrorBGM.volume = 0f;
                horrorBGM.Play();
            }

            // 足音ゴーストを有効化し、移動と音のフェードインを開始
            FootStepGhostWhenDiary.SetActive(true);
            footstepTween?.Kill(); // 念のため既存のTweenを止める
            footstepAudioSource.volume = 0f;
            footstepAudioSource.DOFade(1f, 30f); // 30秒かけて足音を大きくする
            footstepTween = FootStepGhostWhenDiary.transform.DOMove(Diary.transform.position, 50f)
                .SetEase(Ease.Linear).OnComplete(() =>
                {
                    FootStepGhostWhenDiary.SetActive(false);
                });
        }

        // 演出が開始された後、ページをめくるごとにBGM音量を上げていく
        if (hasHorrorSequencePlayedOnce)
        {
            float pageRatio = Mathf.Clamp01((float)_pageNow / (float)maxPage);
            float targetVolume = Mathf.Clamp01(pageRatio);
            horrorBGM.DOFade(targetVolume, 3f).SetEase(Ease.Linear);
        }
    }
}
