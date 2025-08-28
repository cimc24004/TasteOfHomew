using UnityEngine;

/// <summary>
/// 汎用的な Audio 再生用コンポーネント
/// - Inspector で設定した clip を Play()
/// - 引数で指定した clip を PlayOneShot()
/// - Stop / Pause / Resume も可能
/// </summary>
using Sirenix.OdinInspector;
[RequireComponent(typeof(AudioSource))]
public class AudioPlayer : SerializedMonoBehaviour
{
    [Header("Default Clip")]
    [SerializeField] private AudioClip defaultClip;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// Inspector で設定した defaultClip を再生
    /// </summary>
    [Button]
    public void Play()
    {
        if (defaultClip == null)
        {
            Debug.LogWarning("[AudioPlayer] Default Clip が設定されていません。");
            return;
        }

        audioSource.clip = defaultClip;
        audioSource.Play();
    }

    /// <summary>
    /// 引数で指定した clip を再生（置き換え）
    /// </summary>
    public void Play(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioPlayer] 渡された Clip が null です。");
            return;
        }

        audioSource.clip = clip;
        audioSource.Play();
    }

    /// <summary>
    /// 引数で指定した clip を OneShot で再生（clip は差し替えず同時再生も可能）
    /// </summary>
    public void PlayOneShot(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioPlayer] 渡された Clip が null です。");
            return;
        }

        audioSource.PlayOneShot(clip);
    }

    /// <summary>
    /// 一時停止
    /// </summary>
    public void Pause()
    {
        if (audioSource.isPlaying)
            audioSource.Pause();
    }

    /// <summary>
    /// 一時停止した音を再開
    /// </summary>
    public void Resume()
    {
        if (!audioSource.isPlaying && audioSource.clip != null)
            audioSource.UnPause();
    }

    /// <summary>
    /// 停止（再生位置を0に戻す）
    /// </summary>
    public void Stop()
    {
        audioSource.Stop();
    }

    /// <summary>
    /// 音量を変更
    /// </summary>
    public void SetVolume(float volume)
    {
        audioSource.volume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Spatial Blend を変更（0=2D, 1=3D）
    /// </summary>
    public void SetSpatialBlend(float blend)
    {
        audioSource.spatialBlend = Mathf.Clamp01(blend);
    }
}
