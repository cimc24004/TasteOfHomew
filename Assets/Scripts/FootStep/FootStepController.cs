using UnityEngine;
using System.Collections.Generic;

// Odin Inspectorを使用している場合、Dictionaryがインスペクタに表示されます。
using Sirenix.OdinInspector; // 必要に応じてコメントを外してください

/// <summary>
/// Raycastを使用して地面のタグを検出し、対応する足音を再生するコントローラー。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FootstepController : SerializedMonoBehaviour
{
    // --- インスペクタ設定項目 ---

    [Header("必須コンポーネント")]
    [Tooltip("足音を再生するためのAudioSource")]
    [SerializeField] private AudioSource audioSource;

    [Header("レイキャスト設定")]
    [Tooltip("レイキャストの発射地点となるTransform")]
    [SerializeField] private Transform raycastOrigin;

    [Tooltip("レイキャストが届く最大距離")]
    [SerializeField] private float raycastDistance = 1.2f;

    [Tooltip("地面として認識するオブジェクトのレイヤー")]
    [SerializeField] private LayerMask groundLayer;

    [Header("足音設定")]
    [Tooltip("歩いている間の足音の再生間隔（秒）")]
    [SerializeField] private float stepInterval = 0.5f;

    // Odin InspectorのおかげでDictionaryがインスペクタに表示されます。
    [Tooltip("地面のタグと、それに対応するAudioClipのリスト")]
    public Dictionary<string, List<AudioClip>> footstepSounds = new Dictionary<string, List<AudioClip>>();


    // --- 内部変数 ---
    private float stepTimer = 0f;

    // --- 初期化処理 ---

    private void Reset()
    {
        // コンポーネントがアタッチされた時に自動でAudioSourceを取得
        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        // タイマーを更新
        if (stepTimer > 0)
        {
            stepTimer -= Time.deltaTime;
        }
    }

    // --- publicメソッド ---

    /// <summary>
    /// プレイヤーが歩いている時に外部のスクリプトから呼び出すメソッド。
    /// 設定されたインターバルごとに足音を再生しようと試みます。
    /// </summary>
    public void TryPlayFootstep()
    {
        // タイマーが0以下の場合のみ処理を実行
        if (stepTimer <= 0)
        {
            PlayFootstepSound();
            // タイマーをリセット
            stepTimer = stepInterval;
        }
    }


    // --- privateメソッド ---

    /// <summary>
    /// 実際にRaycastを飛ばして足音を再生する処理。
    /// </summary>
    private void PlayFootstepSound()
    {
        // raycastOriginが設定されていなければ処理を中断
        if (raycastOrigin == null)
        {
            Debug.LogWarning("Raycast Originが設定されていません。", this);
            return;
        }

        // 真下にRaycastを飛ばす
        if (Physics.Raycast(raycastOrigin.position, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
        {
            // ヒットしたオブジェクトのタグを取得
            string groundTag = hit.collider.tag;

            // Dictionaryにそのタグのキーが存在するかチェック
            if (footstepSounds.TryGetValue(groundTag, out List<AudioClip> clips))
            {
                // AudioClipのリストが空でなく、要素が存在する場合
                if (clips != null && clips.Count > 0)
                {
                    // リストからランダムにAudioClipを1つ選択
                    AudioClip clipToPlay = clips[Random.Range(0, clips.Count)];

                    // サウンドを再生
                    if (clipToPlay != null)
                    {
                        audioSource.PlayOneShot(clipToPlay);
                    }
                }
            }
        }
    }

    /// <summary>
    /// SceneビューにRaycastのデバッグ情報を表示するためのギズモ。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (raycastOrigin != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(raycastOrigin.position, raycastOrigin.position + Vector3.down * raycastDistance);
        }
    }
}