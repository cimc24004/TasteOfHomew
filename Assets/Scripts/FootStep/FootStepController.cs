using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using System;

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
    public float walkStepInterval = 0.5f;
    [SerializeField]
    private float runStepInterval = 0.3f;
    public Dictionary<string, List<AudioClip>> footstepSounds = new Dictionary<string, List<AudioClip>>();

    [ShowInInspector]
    public MovementState CurrentState = MovementState.Idle;

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
    /// GroundCheckの結果を参照して、地面に応じたサウンドを再生する。
    /// 接地しているオブジェクトのタグが "Ground" の場合、Terrainとみなし、TerrainLayer名で再生音を判断する。
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
        Debug.Log($"[Footstep] Ground detected. Tag: '{groundTag}'");

        // タグが "Ground" の場合、Terrainとして特別処理を試みる
        if (groundTag == "Ground")
        {
            Terrain terrain = groundCheck.GroundCollider.GetComponent<Terrain>();
            if (terrain != null)
            {
                // Terrainからレイヤー名を取得し、名前に基づいてサウンドを再生
                PlayTerrainSound(terrain);
                return; // Terrainの処理が完了したのでここで終了
            }
            else
            {
                // "Ground"タグが付いているがTerrainコンポーネントがない場合
                Debug.LogWarning($"[Footstep] Object with 'Ground' tag does not have a Terrain component. Falling back to tag-based sound.");
            }
        }

        // 通常のタグベースでのサウンド再生（"Ground"タグでTerrainコンポーネントがなかった場合もこちらが実行される）
        PlayTagBasedSound(groundTag);
    }

    /// <summary>
    /// Terrainのテクスチャレイヤーに基づいて足音を再生する。
    /// </summary>
    /// <param name="terrain">接地しているTerrainオブジェクト</param>
    private void PlayTerrainSound(Terrain terrain)
    {
        // 現在位置のTerrainLayer名を取得
        string layerName = GetDominantTerrainLayerName(terrain, transform.position);

        if (string.IsNullOrEmpty(layerName))
        {
            Debug.LogWarning("[Footstep] Could not determine TerrainLayer name at the current position.");
            return;
        }

        Debug.Log($"[Footstep] On Terrain. Detected TerrainLayer: '{layerName}'");

        // footstepSoundsのキーを走査し、レイヤー名にキーが含まれているかチェック
        foreach (var pair in footstepSounds)
        {
            // キーがnullまたは空でないことを確認
            if (string.IsNullOrEmpty(pair.Key)) continue;

            // TerrainLayerの名前に、辞書のキーが含まれていたらそのサウンドを再生
            if (layerName.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                List<AudioClip> clips = pair.Value;
                if (clips != null && clips.Count > 0)
                {
                    Debug.Log($"[Footstep] Found matching key '{pair.Key}' for TerrainLayer '{layerName}'. Playing sound.");
                    PlayRandomClip(clips);
                    return; // 最初に見つかったもので再生して終了
                }
            }
        }

        Debug.LogWarning($"[Footstep] No matching footstep sound key found for TerrainLayer '{layerName}'.");
    }

    /// <summary>
    /// オブジェクトのタグに基づいて足音を再生する。
    /// </summary>
    /// <param name="tag">接地しているオブジェクトのタグ</param>
    private void PlayTagBasedSound(string tag)
    {
        if (footstepSounds.TryGetValue(tag, out List<AudioClip> clips) && clips != null && clips.Count > 0)
        {
            Debug.Log($"[Footstep] Found tag '{tag}' in dictionary. Playing sound.");
            PlayRandomClip(clips);
        }
        else
        {
            Debug.LogWarning($"[Footstep] Tag '{tag}' not found in footstepSounds dictionary, or the clip list is empty.");
        }
    }

    /// <summary>
    /// 指定されたAudioClipのリストからランダムに1つ選んで再生する。
    /// </summary>
    /// <param name="clips">再生候補のAudioClipリスト</param>
    private void PlayRandomClip(List<AudioClip> clips)
    {
        AudioClip clipToPlay = clips[UnityEngine.Random.Range(0, clips.Count)];
        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay);
        }
    }

    /// <summary>
    /// 指定したワールド座標で最もウェイトの高いTerrainLayerの名前を取得する。
    /// </summary>
    /// <param name="terrain">対象のTerrain</param>
    /// <param name="worldPos">ワールド座標</param>
    /// <returns>最もウェイトの高いTerrainLayerの名前</returns>
    private string GetDominantTerrainLayerName(Terrain terrain, Vector3 worldPos)
    {
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainLocalPos = worldPos - terrain.transform.position;

        // ワールド座標をアルファマップの座標に変換
        int mapX = (int)((terrainLocalPos.x / terrainData.size.x) * terrainData.alphamapWidth);
        int mapZ = (int)((terrainLocalPos.z / terrainData.size.z) * terrainData.alphamapHeight);

        // 座標が範囲外なら処理を中断
        if (mapX < 0 || mapX >= terrainData.alphamapWidth || mapZ < 0 || mapZ >= terrainData.alphamapHeight)
        {
            return null;
        }

        // 1x1の範囲でアルファマップデータを取得
        float[,,] splatmapData = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);
        if (splatmapData == null) return null;

        float maxWeight = 0;
        int dominantLayerIndex = -1;

        // レイヤーのウェイトを比較して最も大きいものを探す
        int numLayers = splatmapData.GetLength(2);
        for (int i = 0; i < numLayers; i++)
        {
            if (splatmapData[0, 0, i] > maxWeight)
            {
                maxWeight = splatmapData[0, 0, i];
                dominantLayerIndex = i;
            }
        }

        // インデックスに対応するTerrainLayerの名前を返す
        if (dominantLayerIndex != -1 && terrainData.terrainLayers.Length > dominantLayerIndex)
        {
            return terrainData.terrainLayers[dominantLayerIndex].name;
        }

        return null;
    }
}
