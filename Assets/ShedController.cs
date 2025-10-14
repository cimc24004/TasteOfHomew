using UnityEngine;

public class ShedController : Singleton<ShedController>
{
    [SerializeField] private GameObject FootStepGhostWhenDiary;
    [SerializeField] private GameObject Diary;

    [SerializeField] private AudioSource horrorBGM;

    [SerializeField] private ReadablePickup diaryReadablePickup;

    private bool diaryReadingNow;
    private void Start()
    {
        diaryReadablePickup.onOpenedEveryTime.AddListener(OnDiaryOpened);
    }

    private void OnDiaryOpened()
    {
        diaryReadingNow = true;
        
    }
}
