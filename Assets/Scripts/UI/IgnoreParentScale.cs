using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class IgnoreParentScale : MonoBehaviour
{
    public enum Mode
    {
        NeutralizeParent,   // 親スケールを打ち消して等倍に見せる
        KeepWorldScale      // 指定したワールドスケールを保つ
    }

    [SerializeField] private Mode mode = Mode.NeutralizeParent;

    [Tooltip("KeepWorldScaleモードの時に目標とするワールドスケール")]
    [SerializeField] private Vector3 targetWorldScale = Vector3.one;

    [Tooltip("有効にすると毎フレーム追従（親のアニメでスケールが変わる場合にON）")]
    [SerializeField] private bool updateEveryFrame = true;

    [Tooltip("OnEnable時に一度だけ適用したい場合はOFFのままでもOK")]
    [SerializeField] private bool applyOnEnable = true;

    void OnEnable()
    {
        if (applyOnEnable) Apply();
    }

    void LateUpdate()
    {
        if (updateEveryFrame) Apply();
    }

    void OnTransformParentChanged()
    {
        // 親が付け替わった時も再適用
        Apply();
    }

    private void Apply()
    {
        var p = transform.parent;
        Vector3 parentLossy = Vector3.one;
        if (p != null)
        {
            parentLossy = p.lossyScale;
        }

        // 0除算ガード
        if (Mathf.Approximately(parentLossy.x, 0f)) parentLossy.x = 0.0001f;
        if (Mathf.Approximately(parentLossy.y, 0f)) parentLossy.y = 0.0001f;
        if (Mathf.Approximately(parentLossy.z, 0f)) parentLossy.z = 0.0001f;

        if (mode == Mode.NeutralizeParent)
        {
            // 親のスケールを完全に打ち消す（=見た目は常に等倍）
            transform.localScale = new Vector3(1f / parentLossy.x, 1f / parentLossy.y, 1f / parentLossy.z);
        }
        else // KeepWorldScale
        {
            // 目標ワールドスケールを維持する
            transform.localScale = new Vector3(
                targetWorldScale.x / parentLossy.x,
                targetWorldScale.y / parentLossy.y,
                targetWorldScale.z / parentLossy.z
            );
        }
    }

    // エディタ上で数値をいじったら即反映したい人向け（任意）
#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            Apply();
        }
    }
#endif
}
