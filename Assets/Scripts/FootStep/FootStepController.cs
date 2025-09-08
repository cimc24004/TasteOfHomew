using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// プレイヤーの移動状態に応じて足音の再生を制御するコントローラー。
/// </summary>
public enum MovementState
{
    Idle,      // 待機中（音は鳴らない）
    Walking,   // 歩行中
    Running,   // 走行中
    Crouching  // しゃがみ中（音は鳴らない）
}

[RequireComponent(typeof(AudioSource))]
public class FootStepController : SerializedMonoBehaviour
{
    [Header("必須コンポーネント")]
    [SerializeField]
    private AudioSource audioSource;
    [Tooltip("地面判定を行うGroundCheckコンポーネント")]
    [SerializeField]
    private GroundCheck groundCheck;

    [Header("足音設定")]
    [SerializeField]
    private float walkStepInterval = 0.5f;
    [SerializeField]
    private float runStepInterval = 0.3f;
    public Dictionary<string, List<AudioClip>> footstepSounds = new Dictionary<string, List<AudioClip>>();

    [ShowInInspector, ReadOnly]
    public MovementState CurrentState { get; set; } = MovementState.Idle;

    private float stepTimer = 0f;

    private void Reset()
    {
        audioSource = GetComponent<AudioSource>();
        // 同じオブジェクトにGroundCheckがあれば自動で設定
        groundCheck = GetComponent<GroundCheck>();
    }

    private void Update()
    {
        if (stepTimer > 0)
        {
            stepTimer -= Time.deltaTime;
        }

        if (ShouldPlaySound() && stepTimer <= 0)
        {
            HandleFootstep();
        }
    }

    private bool ShouldPlaySound()
    {
        return CurrentState == MovementState.Walking || CurrentState == MovementState.Running;
    }

    private void HandleFootstep()
    {
        PlaySoundBasedOnGround();
        
        switch (CurrentState)
        {
            case MovementState.Walking:
                stepTimer = walkStepInterval;
                break;
            case MovementState.Running:
                stepTimer = runStepInterval;
                break;
        }
    }

    /// <summary>
    /// GroundCheckの結果を参照して、地面に応じたサウンドを再生する
    /// </summary>
    private void PlaySoundBasedOnGround()
    {
        // GroundCheckが設定されていないか、接地していなければ処理しない
        if (groundCheck == null || !groundCheck.IsGrounded || groundCheck.GroundCollider == null)
        {
            return;
        }
        
        // 接地している地面のタグを取得
        string groundTag = groundCheck.GroundCollider.tag;
        Debug.Log($"[Footstep] Ground detected by GroundCheck. Tag: '{groundTag}'");

        // 辞書にタグが存在するかどうかをチェック
        if (footstepSounds.TryGetValue(groundTag, out List<AudioClip> clips) && clips != null && clips.Count > 0)
        {
            Debug.Log($"[Footstep] Found tag '{groundTag}' in dictionary. Playing sound.");
            // リストからランダムにクリップを選択
            AudioClip clipToPlay = clips[Random.Range(0, clips.Count)];
            if (clipToPlay != null)
            {
                audioSource.PlayOneShot(clipToPlay);
            }
        }
        else
        {
            // 辞書に見つからなかった場合の警告
            Debug.LogWarning($"[Footstep] Tag '{groundTag}' not found in footstepSounds dictionary, or the clip list is empty.");
        }
    }
}