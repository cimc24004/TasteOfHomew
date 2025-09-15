using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// プレイヤーのいる場所を定義します。
/// </summary>
public enum PlayerLocation
{
    InHouse, // 家の中
    Outside  // 外
}

/// <summary>
/// 場所と時間帯に応じて環境BGMの再生を制御します。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class EnvironmentBGMController : Singleton<EnvironmentBGMController>
{
    [Title("必須コンポーネント")]
    [SerializeField, Required("BGM再生用のAudioSourceをアサインしてください")]
    private AudioSource environmentBGM;

    [SerializeField, Required("時間帯を管理するTimeOfDayRendererをアサインしてください")]
    private TimeOfDayRenderer timeOfDayRenderer;

    [Title("BGMクリップ設定")]
    [InfoBox("第一キー：場所 (家の中/外), 第二キー：時間帯 (朝/夕/夜) でBGMクリップを指定します。")]
    [DictionaryDrawerSettings(KeyLabel = "場所", ValueLabel = "時間帯別クリップ")]
    public Dictionary<PlayerLocation, Dictionary<TimeOfDayRenderer.TimeOfDay, AudioClip>> environmentBGMClips = new();

    [Title("フェード設定")]
    [SerializeField, Range(0f, 5f), Tooltip("BGMが切り替わる際のフェード時間（秒）")]
    private float fadeDuration = 1.5f;

    [Title("現在の状態")]
    [ShowInInspector,LabelText("現在の場所")]
    private PlayerLocation currentLocation = PlayerLocation.InHouse;

    private Coroutine fadeCoroutine;
    private float maxVolume;

    private void Reset()
    {
        // コンポーネント追加時にAudioSourceを自動で取得
        environmentBGM = GetComponent<AudioSource>();
    }

    void Start()
    {
        // BGMがループするように設定し、初期音量を最大音量として記憶
        if (environmentBGM != null)
        {
            environmentBGM.loop = true;
            maxVolume = environmentBGM.volume;
        }
        
        // ゲーム開始時に現在の状態に合わせたBGMを再生（フェードなしで即時再生）
        PlayInitialBGM();
    }
    
    /// <summary>
    /// ゲーム開始時専用のBGM再生処理。
    /// </summary>
    private void PlayInitialBGM()
    {
        AudioClip clipToPlay = GetCurrentClip();
        if (clipToPlay != null)
        {
            environmentBGM.clip = clipToPlay;
            environmentBGM.volume = maxVolume;
            environmentBGM.Play();
        }
    }

    /// <summary>
    /// 現在の場所と時間帯に応じたBGMをフェード付きで再生・更新します。
    /// </summary>
    [Button("現在のBGMを再生/更新", ButtonSizes.Large), GUIColor(0.2f, 0.8f, 0.2f)]
    public void PlayCurrentBGM()
    {
        if (timeOfDayRenderer == null || environmentBGM == null)
        {
            Debug.LogError("[EnvironmentBGM] 必須コンポーネントが設定されていません。", this);
            return;
        }

        AudioClip clipToPlay = GetCurrentClip();

        // 再生しようとしているクリップが既に再生中の場合は何もしない
        if (environmentBGM.clip == clipToPlay && environmentBGM.isPlaying)
        {
            return;
        }

        // 以前のフェード処理が実行中なら停止
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        // 新しいフェード処理を開始
        fadeCoroutine = StartCoroutine(FadeBGMCoroutine(clipToPlay));
    }
    
    /// <summary>
    /// BGMのフェードアウト・フェードインを行うコルーチン。
    /// </summary>
    private IEnumerator FadeBGMCoroutine(AudioClip newClip)
    {
        // === フェードアウト処理 ===
        if (environmentBGM.isPlaying)
        {
            float startVolume = environmentBGM.volume;
            float timer = 0f;
            
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                environmentBGM.volume = Mathf.Lerp(startVolume, 0f, timer / fadeDuration);
                yield return null;
            }
            environmentBGM.Stop();
        }

        // === 新しいクリップの再生とフェードイン処理 ===
        if (newClip != null)
        {
            environmentBGM.clip = newClip;
            environmentBGM.Play();

            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                environmentBGM.volume = Mathf.Lerp(0f, maxVolume, timer / fadeDuration);
                yield return null;
            }
            // 最後に音量を最大値にきっちり設定
            environmentBGM.volume = maxVolume;
        }
        else
        {
            // 再生するクリップがない場合は再生しない
            environmentBGM.clip = null;
        }
    }

    /// <summary>
    /// プレイヤーの現在地を設定し、BGMを更新します。
    /// 家の内外に入るトリガーなどからこの関数を呼び出してください。
    /// </summary>
    /// <param name="newLocation">新しい場所</param>
    public void SetLocation(PlayerLocation newLocation)
    {
        if (currentLocation != newLocation)
        {
            Debug.Log($"[EnvironmentBGM] 場所が変更されました: {currentLocation} -> {newLocation}");
            currentLocation = newLocation;
            PlayCurrentBGM(); // 場所が変わったのでBGMを更新
        }
    }
    
    /// <summary>
    /// 現在の状況に応じたAudioClipを取得します。
    /// </summary>
    /// <returns>再生すべきAudioClip。見つからなければnull。</returns>
    private AudioClip GetCurrentClip()
    {
        TimeOfDayRenderer.TimeOfDay currentTime = timeOfDayRenderer.Current;
        if (environmentBGMClips.TryGetValue(currentLocation, out var timeClips))
        {
            if (timeClips != null && timeClips.TryGetValue(currentTime, out var clip))
            {
                return clip;
            }
        }
        
        // クリップが見つからなかったことを警告
        Debug.LogWarning($"[EnvironmentBGM] Location: {currentLocation}, Time: {currentTime} に対応するBGMクリップが見つかりません。");
        return null;
    }
}