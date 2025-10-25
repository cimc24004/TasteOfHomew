using UnityEngine;
using UnityEngine.Events; // UnityEventを使うために必要

/// <summary>
/// 指定した時間が経過すると onTimeElapsed イベントを実行するタイマーコンポーネント
/// </summary>
public class EventTimer : MonoBehaviour
{
    [Header("タイマー設定")]
    [Tooltip("目標時間（秒）")]
    public float targetTime = 5.0f;

    [Tooltip("trueにすると、シーン開始時に自動でタイマーを開始します")]
    public bool startOnAwake = false;

    [Header("実行するイベント")]
    [Tooltip("時間が経過したときに呼び出されます")]
    public UnityEvent2 onTimeElapsed;

    // --- プライベート変数 ---
    private float currentTime = 0.0f;
    private bool isTimerRunning = false;

    private void Awake()
    {
        if (startOnAwake)
        {
            StartTimer(this.currentTime);
        }
    }

    private void Update()
    {
        // タイマーが実行中でなければ、何もしない
        if (!isTimerRunning)
        {
            return;
        }

        // 経過時間を加算
        currentTime += Time.deltaTime;

        // 経過時間が目標時間に達したら
        if (currentTime >= targetTime)
        {
            // タイマーを停止
            isTimerRunning = false;

            // 設定されたイベントを実行
            if (onTimeElapsed != null)
            {
                onTimeElapsed.Invoke();
            }
        }
    }

    /// <summary>
    /// タイマーを（リセットして）スタートします。
    /// </summary>
    [ContextMenu("Start Timer")] // インスペクターから右クリックで実行（テスト用）
    public void StartTimer(float _targetTime)
    {
        this.targetTime = _targetTime;
        currentTime = 0.0f;
        isTimerRunning = true;
        Debug.Log($"EventTimer started: {targetTime} seconds.");
    }

    /// <summary>
    /// タイマーを一時停止します。
    /// </summary>
    [ContextMenu("Stop Timer")]
    public void StopTimer()
    {
        isTimerRunning = false;
        Debug.Log("EventTimer stopped.");
    }
}