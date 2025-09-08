using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Raycastを使用して地面の接地判定を専門に行うコンポーネント。
/// 接地状態と、接触している地面のコライダーを公開する。
/// </summary>
public class GroundCheck : SerializedMonoBehaviour
{
    [Header("レイキャスト設定")]
    [Tooltip("レイキャストの発射地点となるTransform")]
    [SerializeField] private Transform raycastOrigin;
    [Tooltip("レイキャストが届く最大距離")]
    [SerializeField] private float raycastDistance = 0.2f; // キャラクターコントローラーの底から少し下までを検知
    [Tooltip("地面として認識するオブジェクトのレイヤー")]
    [SerializeField] private LayerMask groundLayer;

    // --- 他のスクリプトから参照するプロパティ ---
    
    [Tooltip("現在、地面に接地しているか")]
    [ShowInInspector, ReadOnly]
    public bool IsGrounded { get; private set; }

    [Tooltip("接地している地面のコライダー")]
    [ShowInInspector, ReadOnly]
    public Collider GroundCollider { get; private set; }
    
    // 物理演算に関連するためFixedUpdateで処理
    private void FixedUpdate()
    {
        PerformGroundCheck();
    }

    /// <summary>
    /// 地面判定のレイキャストを実行し、結果をプロパティに格納する
    /// </summary>
    private void PerformGroundCheck()
    {
        if (Physics.Raycast(raycastOrigin.position, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
        {
            IsGrounded = true;
            GroundCollider = hit.collider;
        }
        else
        {
            IsGrounded = false;
            GroundCollider = null;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (raycastOrigin != null)
        {
            Gizmos.color = Color.cyan; // 色を変更して他のRaycastと区別
            Gizmos.DrawLine(raycastOrigin.position, raycastOrigin.position + Vector3.down * raycastDistance);
        }
    }
}