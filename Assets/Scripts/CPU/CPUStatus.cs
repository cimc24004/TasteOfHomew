// CPUStatus.cs
using UnityEngine;
using UnityEngine.Animations.Rigging;

// StealthGameControllerから参照されるコンポーネント
public class CPUStatus : MonoBehaviour
{
    [Header("Animation")]
    public Animator CPUAnimator;
    public MultiAimConstraint multiAimConstraint;

    [Header("Dialogue UI (World Canvas)")]
    [Tooltip("このキャラクター専用のダイアログUI参照")]
    public DialogueUIRefs dialogueUIRefs;
}