using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

/// <summary>
/// 場所と時間帯に応じて環境BGMの再生を制御します。
/// </summary>
public class EnvironmentBGMController : Singleton<EnvironmentBGMController>
{
    [Title("必須コンポーネント")]
    [SerializeField, Required("時間帯を管理するTimeOfDayRendererをアサインしてください")]
    private TimeOfDayRenderer timeOfDayRenderer;

    [Title("BGMクリップ設定")]
    [InfoBox("第一キー：場所 (家の中/外), 第二キー：時間帯 (朝/夕/夜) で再生するAudioSourceのリストを指定します。")]
    [DictionaryDrawerSettings(KeyLabel = "場所", ValueLabel = "時間帯別AudioSourceリスト")]
    public Dictionary<PlayerLocation.Location, Dictionary<TimeOfDayRenderer.TimeOfDay, List<AudioSource>>> environmentBGMClips = new();

    [Title("フェード設定")]
    [SerializeField, Range(0f, 5f), Tooltip("BGMが切り替わる際のフェード時間（秒）")]
    private float fadeDuration = 1.5f;

    private Coroutine _fadeCoroutine;
    private PlayerLocation _playerLocation;
    
    // 現在再生中のAudioSourceを管理するリスト
    private List<AudioSource> _currentlyPlayingSources = new List<AudioSource>();
    // 各AudioSourceの初期音量をキャッシュする辞書
    private readonly Dictionary<AudioSource, float> _initialVolumes = new Dictionary<AudioSource, float>();

    protected override void Awake()
    {
        base.Awake();
        // 起動時に全AudioSourceの初期音量をキャッシュする
        CacheAllInitialVolumes();
    }

    void Start()
    {
        // ▼▼▼ 修正箇所 ▼▼▼
        // 必須コンポーネントが設定されているか確認
        if (timeOfDayRenderer == null)
        {
            Debug.LogError("[EnvironmentBGM] Inspectorで 'Time Of Day Renderer' がアサインされていません！処理を中断します。", this);
            return; // 処理を中断
        }
        
        _playerLocation = PlayerLocation.instance;
        if (_playerLocation == null)
        {
            Debug.LogError("[EnvironmentBGM] 'PlayerLocation' のインスタンスが見つかりません！処理を中断します。", this);
            return; // 処理を中断
        }
        _playerLocation.SetLocationEvent.AddListener(PlayCurrentBGM);
        // ゲーム開始時に現在の状態に合わせたBGMを再生（フェードなしで即時再生）
        PlayInitialBGM();
    }
    
    /// <summary>
    /// Dictionaryに登録されている全てのAudioSourceの初期音量をキャッシュします。
    /// </summary>
    private void CacheAllInitialVolumes()
    {
        _initialVolumes.Clear();
        if (environmentBGMClips == null) return;

        foreach (var locationPair in environmentBGMClips.Values)
        {
            if (locationPair == null) continue;
            foreach (var timePair in locationPair.Values)
            {
                if (timePair == null) continue;
                foreach (var source in timePair)
                {
                    if (source != null && !_initialVolumes.ContainsKey(source))
                    {
                        _initialVolumes.Add(source, source.volume);
                    }
                }
            }
        }
    }

    /// <summary>
    /// ゲーム開始時専用のBGM再生処理。
    /// </summary>
    private void PlayInitialBGM()
    {
        List<AudioSource> sourcesToPlay = GetCurrentAudioSources();
        if (sourcesToPlay != null)
        {
            foreach(var source in sourcesToPlay)
            {
                if (source != null)
                {
                    // キャッシュから初期音量を取得して設定
                    source.volume = _initialVolumes.TryGetValue(source, out float vol) ? vol : 1f;
                    source.loop = true; // BGMはループ再生を基本とする
                    source.Play();
                }
            }
            // nullを除いたリストを現在再生中のリストとして設定
            _currentlyPlayingSources = new List<AudioSource>(sourcesToPlay.Where(s => s != null));
        }
    }

    /// <summary>
    /// 現在の場所と時間帯に応じたBGMをフェード付きで再生・更新します。
    /// </summary>
    [Button("現在のBGMを再生/更新", ButtonSizes.Large), GUIColor(0.2f, 0.8f, 0.2f)]
    public void PlayCurrentBGM()
    {
        if (timeOfDayRenderer == null)
        {
            Debug.LogError("[EnvironmentBGM] 必須コンポーネント 'TimeOfDayRenderer' が設定されていません。", this);
            return;
        }

        List<AudioSource> newSources = GetCurrentAudioSources();
        
        // GetCurrentAudioSources内でnullチェックしているので、ここで newSources がnullになることは基本的にないが念のため
        if (newSources == null)
        {
            // BGMがない場合は現在再生中のものを止める処理へ
            newSources = new List<AudioSource>();
        }

        // 再生するべきリストが現在再生中のリストと同一であれば何もしない
        bool areListsIdentical = _currentlyPlayingSources.Count == newSources.Count &&
                                 _currentlyPlayingSources.All(newSources.Contains);

        if (areListsIdentical)
        {
            return;
        }
        
        // 以前のフェード処理が実行中なら停止
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }
        // 新しいフェード処理を開始
        _fadeCoroutine = StartCoroutine(FadeBGMCoroutine(newSources));
    }
    
    /// <summary>
    /// BGMのクロスフェードを行うコルーチン。
    /// </summary>
    private IEnumerator FadeBGMCoroutine(List<AudioSource> newSources)
    {
        // nullの場合は空リストとして扱う
        newSources = newSources?.Where(s => s != null).ToList() ?? new List<AudioSource>();

        // これからフェードアウトさせるソース（現在再生中リストにはあるが、新リストにはない）
        List<AudioSource> sourcesToFadeOut = _currentlyPlayingSources.Except(newSources).ToList();
        // これからフェードインさせるソース（新リストにはあるが、現在再生中リストにはない）
        List<AudioSource> sourcesToFadeIn = newSources.Except(_currentlyPlayingSources).ToList();

        // 新しいソースを音量0で再生開始
        foreach (var source in sourcesToFadeIn)
        {
            source.volume = 0f;
            source.loop = true;
            source.Play();
        }

        // 現在の再生リストを新しいものに更新
        _currentlyPlayingSources = new List<AudioSource>(newSources);

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float progress = fadeDuration > 0 ? Mathf.Clamp01(timer / fadeDuration) : 1f;

            // フェードアウト処理
            foreach (var source in sourcesToFadeOut)
            {
                if (source != null && _initialVolumes.TryGetValue(source, out float maxVol))
                {
                    source.volume = Mathf.Lerp(maxVol, 0f, progress);
                }
            }

            // フェードイン処理
            foreach (var source in sourcesToFadeIn)
            {
                if (source != null && _initialVolumes.TryGetValue(source, out float maxVol))
                {
                    source.volume = Mathf.Lerp(0f, maxVol, progress);
                }
            }
            yield return null;
        }

        // フェード完了後、各ソースの状態を確定させる
        foreach (var source in sourcesToFadeOut)
        {
            if (source != null)
            {
                source.Stop();
            }
        }
        // 再生中のソース音量を最大値にきっちり設定
        foreach (var source in _currentlyPlayingSources)
        {
            if (source != null && _initialVolumes.TryGetValue(source, out float maxVol))
            {
                source.volume = maxVol;
            }
        }
    }


    
    /// <summary>
    /// 現在の状況に応じたAudioSourceのリストを取得します。
    /// </summary>
    /// <returns>再生すべきAudioSourceのリスト。見つからなければnull。</returns>
    private List<AudioSource> GetCurrentAudioSources()
    {
        // ▼▼▼ 修正箇所 ▼▼▼
        // Startでもチェックしているが、念のためここでもチェック
        if (timeOfDayRenderer == null || _playerLocation == null)
        {
             // Startでエラーが出ているはずなので、ここではエラーを重複させない
            return null;
        }
        
        TimeOfDayRenderer.TimeOfDay currentTime = timeOfDayRenderer.Current;
        if (environmentBGMClips.TryGetValue(_playerLocation.currentLocation, out var timeClips))
        {
            if (timeClips != null && timeClips.TryGetValue(currentTime, out var sources))
            {
                return sources;
            }
        }
        
        // クリップが見つからなかったことを警告
        Debug.LogWarning($"[EnvironmentBGM] Location: {_playerLocation.currentLocation}, Time: {currentTime} に対応するBGMリストが見つかりません。");
        return null;
    }
}