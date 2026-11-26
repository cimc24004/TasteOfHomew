using UnityEngine;
using Sirenix.OdinInspector; // Odin Inspectorを使っているため
using System;               // PlayerStatus.instance のため

/// <summary>
/// プレイヤーとの距離と角度（前方180度）を監視し、
/// 条件に応じて NPCActions の "FadeRigWeight" を実行するトリガー。
/// (注：このスクリプトは体を回転させず、RigのWeightのみを変更します)
/// </summary>
[RequireComponent(typeof(NPCActions))] // NPCActionsが必須
public class NPCLookAtTrigger : MonoBehaviour
{
    [Title("Dependencies")]
    [Tooltip("このスクリプトが制御するNPCActions（自動取得されます）")]
    [ReadOnly]
    private NPCActions npcActions;

    [Tooltip("追跡対象のプレイヤーTransform（自動取得されます）")]
    [ReadOnly]
    private Transform playerTransform;

    [Title("Trigger Settings")]
    [SerializeField]
    [Tooltip("この距離以内、かつ前方180度にいるとRigがアクティブになります")]
    private float activationDistance = 10f;

    [SerializeField]
    [Tooltip("RigのWeightが変化するのにかかる時間（NPCActionsの関数に渡されます）")]
    private float rigFadeDuration = 0.3f;

    // --- 内部状態 ---
    [Header("Debug State")]
    [Tooltip("現在、Rigがアクティブ（アクティブ状態と判断されて）いるか")]
    [ReadOnly]
    private bool isRigActive = false;

    void Awake()
    {
        // 依存コンポーネントを自動取得
        npcActions = GetComponent<NPCActions>();
    }

    void Start()
    {
        // NPCActions スクリプトが保持している playerTransform を参照します
        if (npcActions.playerTransform != null)
        {
            playerTransform = npcActions.playerTransform;
        }
        else if (PlayerStatus.instance != null)
        {
            playerTransform = PlayerStatus.instance.transform;
        }
        else
        {
            Debug.LogError("プレイヤーが見つかりません。NPCLookAtTrigger は動作できません。", this);
            enabled = false; // プレイヤーがいないなら、このUpdateスクリプトを無効化
            return;
        }

        // 起動時の初期状態を設定
        isRigActive = ShouldActivateRig();

        // 初期状態をほぼ即時反映 (0.01秒でWeightを設定)
        npcActions.FadeRigWeight(isRigActive ? 1.0f : 0.0f, 0.01f);
    }

    void Update()
    {
        // プレイヤーが（シーン移動などで）いなくなったら処理を中断
        if (playerTransform == null)
        {
            if (PlayerStatus.instance != null) playerTransform = PlayerStatus.instance.transform;
            else return; // やはり見つからなければ何もしない
        }

        // --- 現在のフレームで、Rigをアクティブにすべきか判定 ---
        bool shouldActivate = ShouldActivateRig();

        // --- 状態が「変化」した瞬間に NPCActions の関数を呼ぶ ---

        // 1. 非アクティブ状態 -> アクティブ状態 になった瞬間
        if (shouldActivate && !isRigActive)
        {
            isRigActive = true;

            // ご要望の通り、Rig Weightを1にする関数を呼びます
            npcActions.FadeRigWeight(1.0f, rigFadeDuration);
        }
        // 2. アクティブ状態 -> 非アクティブ状態 になった瞬間
        else if (!shouldActivate && isRigActive)
        {
            isRigActive = false;

            // ご要望の通り、Rig Weightを0にする関数を呼びます
            npcActions.FadeRigWeight(0.0f, rigFadeDuration);
        }
    }

    /// <summary>
    /// 現在の状況でRigをアクティブにすべきか（距離と角度）を判定します。
    /// </summary>
    /// <returns>アクティブにすべきならtrue</returns>
    private bool ShouldActivateRig()
    {
        if (playerTransform == null) return false;

        // 1. 距離の判定
        float distance = Vector3.Distance(playerTransform.position, transform.position);
        if (distance > activationDistance)
        {
            return false; // 距離が遠すぎる
        }

        // 2. 角度の判定 (前方180度)

        // 自分からプレイヤーへの方向ベクトル（Y軸無視）
        Vector3 directionToPlayer = playerTransform.position - transform.position;
        directionToPlayer.y = 0;

        // 自分の正面方向ベクトル（Y軸無視）
        Vector3 myForward = transform.forward;
        myForward.y = 0;

        // ゼロベクトルチェック (プレイヤーが真上/真下など、水平方向の差がない場合)
        if (directionToPlayer.sqrMagnitude < 0.001f)
        {
            return false; // 真上や真下は「前方」とはみなさない
        }

        // 内積 (Dot Product) を計算
        // Dot(A, B) > 0 なら、2つのベクトルは同じ方向（前方180度）を向いている
        float dotProduct = Vector3.Dot(myForward.normalized, directionToPlayer.normalized);

        // ご要望の通り、前方180度（内積 > 0）の場合のみ true を返します
        return dotProduct > 0;
    }
}
