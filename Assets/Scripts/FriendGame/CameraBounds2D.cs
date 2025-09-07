using UnityEngine;
using Sirenix.OdinInspector;

public class CameraBounds2D : SerializedMonoBehaviour
{
    [Title("Refs")]
    [LabelText("追従ターゲット")]
    [ShowIf(nameof(followTarget))]
    [Required]
    public Transform target;

    [LabelText("ターゲットへのオフセット")]
    public Vector3 offset = Vector3.zero;

    [Title("Bounds (X, Y only)")]
    [LabelText("MinCameraPos (x,y)")]
    public Vector2 minCameraPos = new Vector2(-10f, -5f);
    [LabelText("MaxCameraPos (x,y)")]
    public Vector2 maxCameraPos = new Vector2(10f, 5f);

    [Title("挙動")]
    [LabelText("ターゲットを追う")]
    public bool followTarget = true;
    [LabelText("追従のスムース時間 (0 = 即時)")]
    public float smoothTime = 0f;

    [Title("自動化オプション")]
    [LabelText("Startでboundsをターゲット中心に合わせる")]
    public bool autoCenterOnStart = true;
    [LabelText("ターゲットがbounds外なら自動で拡張する")]
    public bool expandIfTargetOutside = true;

    [Title("座標系設定")]
    [LabelText("Boundsをローカル座標系として解釈する")]
    public bool boundsInLocalSpace = false;
    [LabelText("ローカル座標系の参照 (null=カメラの親)")]
    public Transform boundsReference;

    // 内部
    private Vector3 velocity;

    private void OnValidate()
    {
        // min/max の順序が逆なら直す
        var min = Vector2.Min(minCameraPos, maxCameraPos);
        var max = Vector2.Max(minCameraPos, maxCameraPos);
        minCameraPos = min;
        maxCameraPos = max;
    }

    private void Start()
    {
        if (followTarget)
        {
            Debug.Assert(target != null, "CameraBounds2D: followTarget=true だが target が割り当てられていない");

            if (autoCenterOnStart)
                CenterBoundsOnTargetKeepSize();

            if (expandIfTargetOutside)
                EnsureTargetInsideBounds();
        }
    }

    private void LateUpdate()
    {
        if (followTarget)
            Debug.Assert(target != null, "CameraBounds2D: followTarget=true だが target が割り当てられていない");

        // 目的ワールド座標（ターゲットにオフセットを足したワールド座標）
        Vector3 desiredWorld = followTarget && target != null ? target.position + offset : transform.position;
        float z = transform.position.z;

        // 必要ならターゲットが bounds 外なら拡張（軽いチェック）
        if (expandIfTargetOutside && followTarget && target != null)
        {
            Vector2 t = new Vector2(target.position.x + offset.x, target.position.y + offset.y);
            if (t.x < minCameraPos.x) minCameraPos.x = t.x;
            if (t.x > maxCameraPos.x) maxCameraPos.x = t.x;
            if (t.y < minCameraPos.y) minCameraPos.y = t.y;
            if (t.y > maxCameraPos.y) maxCameraPos.y = t.y;
        }

        Vector3 clampedWorld;

        if (boundsInLocalSpace)
        {
            Transform refT = boundsReference ? boundsReference : transform.parent;
            Debug.Assert(refT != null, "CameraBounds2D: boundsInLocalSpace=true だが boundsReference と親が null（参照を指定してくれ）");

            // 世界→参照ローカル → clamp → ローカル→世界
            Vector3 desiredLocal = refT.InverseTransformPoint(desiredWorld);
            float cx = Mathf.Clamp(desiredLocal.x, minCameraPos.x, maxCameraPos.x);
            float cy = Mathf.Clamp(desiredLocal.y, minCameraPos.y, maxCameraPos.y);
            Vector3 clampedLocal = new Vector3(cx, cy, desiredLocal.z);
            Vector3 outWorld = refT.TransformPoint(clampedLocal);
            clampedWorld = new Vector3(outWorld.x, outWorld.y, z);
        }
        else
        {
            float cx = Mathf.Clamp(desiredWorld.x, minCameraPos.x, maxCameraPos.x);
            float cy = Mathf.Clamp(desiredWorld.y, minCameraPos.y, maxCameraPos.y);
            clampedWorld = new Vector3(cx, cy, z);
        }

        // 適用
        if (smoothTime > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, clampedWorld, ref velocity, smoothTime);
        else
            transform.position = clampedWorld;
    }

    // Inspector 操作用ボタン
    [Button("Set Min to Current Camera Pos")]
    private void SetMinToCurrent() => minCameraPos = new Vector2(transform.position.x, transform.position.y);

    [Button("Set Max to Current Camera Pos")]
    private void SetMaxToCurrent() => maxCameraPos = new Vector2(transform.position.x, transform.position.y);

    [Button("Center bounds on target (keep size)")]
    [ShowIf(nameof(followTarget))]
    private void CenterBoundsOnTarget() => CenterBoundsOnTargetKeepSize();

    private void CenterBoundsOnTargetKeepSize()
    {
        Debug.Assert(target != null, "CenterBoundsOnTargetKeepSize: target が null");
        Vector2 half = (maxCameraPos - minCameraPos) * 0.5f;
        Vector2 center = new Vector2(target.position.x, target.position.y);
        minCameraPos = center - half;
        maxCameraPos = center + half;
    }

    // Start 時にターゲットが bounds 外なら拡張して追えるようにする
    private void EnsureTargetInsideBounds()
    {
        Debug.Assert(target != null, "EnsureTargetInsideBounds: target が null");
        Vector2 t = new Vector2(target.position.x + offset.x, target.position.y + offset.y);
        if (t.x < minCameraPos.x) minCameraPos.x = t.x;
        if (t.x > maxCameraPos.x) maxCameraPos.x = t.x;
        if (t.y < minCameraPos.y) minCameraPos.y = t.y;
        if (t.y > maxCameraPos.y) maxCameraPos.y = t.y;
    }
}
