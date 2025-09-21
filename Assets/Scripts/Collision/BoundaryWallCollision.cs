using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider))]
public class BoundaryWallCollision : MonoBehaviour
{
    [Header("衝突時に実行するイベント")]
    public UnityEvent onBoundaryCollision;

    [Header("ギズモの色 (半透明の赤)")]
    [SerializeField] 
    private Color gizmoColor = new Color(1f, 0f, 0f, 0.35f); // デフォルトを半透明の赤に設定

    private BoxCollider boxCollider;

    // Sceneビューで表示されるギズモ（ガイド）を描画する
    private void OnDrawGizmos()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        // Inspectorで設定した色をギズモに反映
        Gizmos.color = gizmoColor;

        // Transformの情報をギズモに反映させる
        Gizmos.matrix = transform.localToWorldMatrix;

        // ★変更点：ワイヤーフレームではなく、べた塗りの箱を描画
        Gizmos.DrawCube(boxCollider.center, boxCollider.size);
    }

    // 他のColliderと物理的に衝突した瞬間に呼び出される
    private void OnCollisionEnter(Collision collisionInfo)
    {
        Debug.Log(collisionInfo.gameObject.name + " が境界に衝突しました。");
        onBoundaryCollision?.Invoke();
    }
}