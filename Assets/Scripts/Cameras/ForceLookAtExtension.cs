// ライブVCamに付いている間だけ、指定ターゲット方向へAimを上書きする拡張
using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class ForceLookAtExtension : CinemachineExtension
{
    [Tooltip("注視対象。null なら何もしない")]
    public Transform Target;

    [Range(0f, 1f)]
    [Tooltip("向け具合(0=無効, 1=完全). ブレンドで滑らかに制御")]
    public float Weight = 1f;

    [Tooltip("ターゲットに加えるワールドオフセット")]
    public Vector3 WorldOffset;

    [Tooltip("ロール固定。通常はtrueでOK")]
    public bool KeepUpVector = true;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Aim) return;
        if (Target == null) return;
        if (Weight <= 0f) return;

        // CM3 では GetFinalPosition() が使える。もしコンパイルエラーなら RawPosition か FinalPosition に差し替えてOK
        Vector3 camPos = state.GetFinalPosition();
        Vector3 targetPos = Target.position + WorldOffset;
        Vector3 dir = targetPos - camPos;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion desired = Quaternion.LookRotation(dir.normalized, KeepUpVector ? Vector3.up : state.ReferenceUp);
        state.RawOrientation = Quaternion.Slerp(state.RawOrientation, desired, Mathf.Clamp01(Weight));
    }
}
