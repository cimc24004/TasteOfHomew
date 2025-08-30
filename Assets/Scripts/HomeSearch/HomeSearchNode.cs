using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))] // isTrigger = true 推奨
public class HomeSearchNode : MonoBehaviour
{
    // ===== Arrival (Trigger) =====
    [Header("Arrival (Trigger)")]
    [SerializeField] private bool filterByTag = true;
    [SerializeField] private string playerTag = "Player";

    // ===== Ping Sound =====
    [Header("Ping Sound")]
    [Tooltip("未指定なら AudioSource.clip を鳴らす")]
    [SerializeField] private AudioClip pingClip;
    [SerializeField, Min(0f)] private float volume = 1f;
    [SerializeField, Min(0.1f)] private float pingIntervalMin = 1.2f;
    [SerializeField, Min(0.1f)] private float pingIntervalMax = 2.2f;

    // ===== Events =====
    [Header("Events")]
    public UnityEvent onActivate;   // ノード有効化時（ヒント点灯など）
    public UnityEvent onArrive;     // Trigger侵入時（演出開始など）

    // ===== Gizmos =====
    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawOnlyWhenSelected = false;

    [Tooltip("塗り（内外両面）")]
    [SerializeField] private Color gizmoFillColor = new Color(0f, 1f, 1f, 0.25f);
    [Tooltip("アウトライン")]
    [SerializeField] private Color gizmoWireColor = new Color(0f, 1f, 1f, 0.9f);

    [Tooltip("Editor Handles を使って塗りを描く（推奨）。ビルドには含まれない")]
    [SerializeField] private bool useEditorHandlesFill = true;

    [Tooltip("ZTest=Always で常に見えるように描く（壁の裏でも見たい時ON）")]
    [SerializeField] private bool handlesZTestAlways = false;

    // ===== Runtime =====
    private Transform player;       // 互換用（必須ではない）
    private bool isActive;
    private bool hasArrived;
    private AudioSource audioSource; // ノードに付いてるやつ（自動追加しない）
    private Coroutine pingRoutine;
    private BoxCollider box;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>(); // 付いてるやつだけ使う
        box = GetComponent<BoxCollider>();
    }

    private void OnValidate()
    {
        if (pingIntervalMax < pingIntervalMin) pingIntervalMax = pingIntervalMin;

        var c = GetComponent<BoxCollider>();
        if (c && !c.isTrigger)
            Debug.LogWarning($"[HomeSearchNode] {name} の BoxCollider は isTrigger=true にしてね。");

        // Trigger検知にはどちらか片方（プレイヤーorノード）に Rigidbody が必要
        // 自動追加はしないので必要に応じてノード側に kinematic を付けてね
    }

    /// <summary>コントローラから有効化。player は未使用でもOK。</summary>
    public void Activate(Transform playerTransform)
    {
        player = playerTransform;
        isActive = true;
        hasArrived = false;

        onActivate?.Invoke();

        if (audioSource == null)
            Debug.LogWarning($"[HomeSearchNode] AudioSource なし: {name}（音は鳴らさず進行）");

        if (audioSource != null && (pingClip != null || audioSource.clip != null) && pingRoutine == null)
            pingRoutine = StartCoroutine(PingLoop());
    }

    public void Deactivate()
    {
        isActive = false;
        if (pingRoutine != null)
        {
            StopCoroutine(pingRoutine);
            pingRoutine = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive || hasArrived) return;
        if (filterByTag && !other.CompareTag(playerTag)) return;

        // ★ 到達（ここでは“次へ”に進めない）
        hasArrived = true;
        onArrive?.Invoke();
        // 進行は UnityEvent から HomeSearchGameController.AdvanceToNextNode() を呼んでね
        // 必要なら onArrive に HomeSearchNode.Deactivate() でピン音停止
    }

    private IEnumerator PingLoop()
    {
        while (isActive)
        {
            var clip = pingClip != null ? pingClip : audioSource.clip;
            if (clip != null) audioSource.PlayOneShot(clip, volume);
            yield return new WaitForSeconds(Random.Range(pingIntervalMin, pingIntervalMax));
        }
    }

    // ===== Gizmos / Handles =====
    private void OnDrawGizmos()
    {
        if (!drawGizmos || drawOnlyWhenSelected) return;
        DrawVolumeGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        DrawVolumeGizmo();
    }

    private void DrawVolumeGizmo()
    {
        if (box == null) box = GetComponent<BoxCollider>();
        if (box == null) return;

        // まずアウトラインは従来通り（見やすい）
        var prevMatrix = Gizmos.matrix;
        var prevColor  = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.TRS(box.center, Quaternion.identity, box.size);
        Gizmos.color  = gizmoWireColor;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.color  = prevColor;
        Gizmos.matrix = prevMatrix;

        // 塗りは Editor Handles で両面＆ZTest制御（Editorのみ）
        #if UNITY_EDITOR
        if (useEditorHandlesFill)
        {
            UnityEditor.Handles.color = gizmoFillColor;
            var prevZ = UnityEditor.Handles.zTest;
            UnityEditor.Handles.zTest = handlesZTestAlways
                ? UnityEngine.Rendering.CompareFunction.Always
                : UnityEngine.Rendering.CompareFunction.LessEqual;

            DrawSolidBoxWithHandles(transform, box.center, box.size);

            UnityEditor.Handles.zTest = prevZ;
        }
        else
        {
            // 予備：Gizmos での塗り（片面/深度ありで見えづらい場合がある）
            var prevM = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.TRS(box.center, Quaternion.identity, box.size);
            var prevC = Gizmos.color;
            Gizmos.color = gizmoFillColor;
            // 単純に6面を DrawCube で代用（薄いけど環境によっては見えにくい）
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            Gizmos.color = prevC;
            Gizmos.matrix = prevM;
        }
        #endif
    }

    #if UNITY_EDITOR
    // 6面を両面扱いでソリッド描画（Editor Handlesを使用）
    private static void DrawSolidBoxWithHandles(Transform t, Vector3 centerLocal, Vector3 sizeLocal)
    {
        var e = sizeLocal * 0.5f;

        Vector3 W(float sx, float sy, float sz)
        {
            var p = new Vector3(sx * e.x + centerLocal.x, sy * e.y + centerLocal.y, sz * e.z + centerLocal.z);
            return t.TransformPoint(p);
        }

        // 各面の4点（時計回り）を描く。Handles.DrawAAConvexPolygon は面の片側だけでOK（ZTestで内外が見える）
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            UnityEditor.Handles.DrawAAConvexPolygon(a, b, c, d);
        }

        // +X
        Face(W(+1,-1,-1), W(+1,-1,+1), W(+1,+1,+1), W(+1,+1,-1));
        // -X
        Face(W(-1,-1,+1), W(-1,-1,-1), W(-1,+1,-1), W(-1,+1,+1));
        // +Y
        Face(W(-1,+1,-1), W(+1,+1,-1), W(+1,+1,+1), W(-1,+1,+1));
        // -Y
        Face(W(-1,-1,+1), W(+1,-1,+1), W(+1,-1,-1), W(-1,-1,-1));
        // +Z
        Face(W(-1,-1,+1), W(-1,+1,+1), W(+1,+1,+1), W(+1,-1,+1));
        // -Z
        Face(W(+1,-1,-1), W(+1,+1,-1), W(-1,+1,-1), W(-1,-1,-1));
    }
    #endif
}
