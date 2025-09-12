using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

public class CPUStatus : MonoBehaviour
{
    [Header("必須コンポーネント")]
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] public Animator CPUAnimator;
    [SerializeField] private FootStepController footStepController;
    [SerializeField] public MultiAimConstraint multiAimConstraint;

    // 状態の閾値。この速度より大きければ「歩いている」と判断する
    [Header("設定")]
    [Tooltip("移動中と判断する最低速度")]
    [SerializeField] private float moveThreshold = 0.1f;
    [Header("Dialogue UI (World Canvas)")]
    [Tooltip("このキャラクター用のダイアログUI参照")]
    public DialogueUIRefs dialogueUIRefs;

    // 前のフレームの状態を保存しておく変数
    private MovementState previousState = MovementState.Idle;

    private void Start()
    {
        // 起動時にコンポーネントが設定されているかチェック
        if (navMeshAgent == null)
        {
            Debug.LogError("NavMeshAgentが設定されていません。", this.gameObject);
        }
        if (footStepController == null)
        {
            Debug.LogError("FootStepControllerが設定されていません。", this.gameObject);
        }
    }

    void Update()
    {
        // 必要なコンポーネントがなければ処理を中断
        if (navMeshAgent == null || footStepController == null)
        {
            return;
        }

        // 1. 現在の速度を取得
        // navMeshAgent.velocity.magnitude はエージェントの現在の速度（スカラー値）
        float currentSpeed = navMeshAgent.velocity.magnitude;

        // 2. 現在の速度から新しい状態を判断
        MovementState newState;
        if (currentSpeed > moveThreshold)
        {
            newState = MovementState.Walking;
        }
        else
        {
            newState = MovementState.Idle;
        }

        // 3. 状態が変化した時だけ、FootStepControllerに通知
        // 毎フレーム値を設定するのを防ぎ、効率化する
        if (newState != previousState)
        {
            footStepController.CurrentState = newState;
            previousState = newState; // 現在の状態を保存
        }
    }
}