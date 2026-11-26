// 「正面にドアが見えたら開ける」母用コンポーネント（Doorタグ前提）
// - Rotate: 閉まってたら一度だけOpen()
// - Slide : OpenCloseAutoSelect() を短間隔で連打（アニメを継続させるイメージ）

using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("Game/AI/MotherDoorOpener")]
public class MotherDoorOpener : SerializedMonoBehaviour
{
    [TitleGroup("Raycast"), Tooltip("レイの原点（未設定ならこのオブジェクト）")]
    public Transform rayOrigin;

    [TitleGroup("Raycast"), Tooltip("原点からのローカルオフセット（身長に合わせて少し上げるなど）")]
    public Vector3 localOffset = new Vector3(0, 1.5f, 0);

    [TitleGroup("Raycast"), Tooltip("前方に飛ばす距離（室内なら 1.0〜1.8m 程度）")]
    [MinValue(0.1f)] public float rayLength = 1.4f;

    [TitleGroup("Raycast"), Tooltip("衝突させたいレイヤー（未指定でもタグで最終判定します）")]
    public LayerMask hitLayers = ~0;

    [TitleGroup("Behavior"), Tooltip("Slideドアに対して何秒おきにOpenCloseAutoSelectを叩くか")]
    [MinValue(0.01f)] public float slideCallInterval = 0.12f;

    [TitleGroup("Behavior"), Tooltip("ドアの判定コライダからDoorControllerを探索する際に親を辿る")]
    public bool searchInParents = true;

    [TitleGroup("Debug"), Tooltip("Gizmos を描画する")]
    public bool debugGizmos = true;

    // Slide 連打の打ち過ぎ防止
    private readonly Dictionary<DoorController, float> _nextAllowedSlideCall = new();

    private Transform Origin => rayOrigin != null ? rayOrigin : transform;

    void Update()
    {
        TryOpenDoorAhead();
    }

    [Button(ButtonSizes.Medium), GUIColor(0.2f, 0.7f, 1f), PropertyOrder(100)]
    public void TryOpenDoorAhead()
    {
        Vector3 o = Origin.position + Origin.TransformVector(localOffset);
        Vector3 d = Origin.forward;

        if (Physics.Raycast(o, d, out var hit, rayLength, hitLayers, QueryTriggerInteraction.Ignore))
        {
            // タグが Door でなければスルー
            if (!hit.collider.CompareTag("Door"))
                return;

            // DoorController を取る（親にも居る想定）
            DoorController door = searchInParents
                ? hit.collider.GetComponentInParent<DoorController>()
                : hit.collider.GetComponent<DoorController>();

            if (door == null) return;

            // Rotate: 閉まってたら一回だけ Open()
            if (door.open_type == DoorController.OpenType.Rotate)
            {
                if (door.nowDoorStatus == DoorController.DoorStatus.Closed)
                {
                    // interactor として this.gameObject を渡す
                    door.Open(this.gameObject);
                }
            }
            // Slide: 一定間隔で Interact を叩き続ける
            else if (door.open_type == DoorController.OpenType.SideOpen)
            {
                float now = Time.time;
                if (!_nextAllowedSlideCall.TryGetValue(door, out float next) || now >= next)
                {
                    // OpenCloseAutoSelect ではなく、
                    // interactor を渡せる Interact() を呼ぶ
                    door.Interact(this.gameObject);
                    _nextAllowedSlideCall[door] = now + slideCallInterval;
                }
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;
        DrawRayGizmo(new Color(0f, 0.8f, 1f, 0.9f));
    }

    void OnDrawGizmos()
    {
        if (!debugGizmos) return;
        // 選択していない時は薄く
        DrawRayGizmo(new Color(0f, 0.8f, 1f, 0.25f));
    }

    private void DrawRayGizmo(Color c)
    {
        var o = (rayOrigin ? rayOrigin : transform);
        Vector3 start = o.position + o.TransformVector(localOffset);
        Vector3 end = start + o.forward * rayLength;

        Handles.color = c;
        Handles.DrawAAPolyLine(4f, start, end);
        Gizmos.color = c;
        Gizmos.DrawWireSphere(end, 0.05f);
    }
#endif
}
