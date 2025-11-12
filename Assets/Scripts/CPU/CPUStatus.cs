using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

public class CPUStatus : MonoBehaviour
{
    [Header("必須コンポーネント")]
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] public Animator CPUAnimator;
    [SerializeField] public FootStepController footStepController;
    [SerializeField] public MultiAimConstraint multiAimConstraint;
    // 状態の閾値。この速度より大きければ「歩いている」と判断する
    [Header("設定")]
    [Tooltip("移動中と判断する最低速度")]
    [SerializeField] private float moveThreshold = 0.1f;
    [Header("Dialogue UI (World Canvas)")]
    [Tooltip("このキャラクター用のダイアログUI参照")]
    public DialogueUIRefs dialogueUIRefs;

    // 前のフレームの状態を保存しておく変数
    // (元のコード通り "MovementState" を使用)
    [SerializeField]private MovementState previousState = MovementState.Idle;

    private void Start()
    {
        // 起動時にコンポーネントが設定されているかチェック
        if (navMeshAgent == null)
        {
            Debug.LogError("NavMeshAgentが設定されていません。", this.gameObject);
        }
        if (footStepController == null)
        {
            // footStepControllerがなくてもAnimatorは制御できるよう警告(Warning)に変更
            Debug.LogWarning("FootStepControllerが設定されていません。", this.gameObject);
        }
        if (CPUAnimator == null)
        {
            Debug.LogError("CPUAnimatorが設定されていません。", this.gameObject);
        }

        // 起動時の状態を反映
        UpdateMovementState(true); 
    }

    void Update()
    {
        UpdateMovementState(false);
    }

    /// <summary>
    /// 現在の速度に基づき、状態（AnimatorとFootStep）を更新する
    /// </summary>
    /// <param name="forceUpdate">trueの場合、状態が変化していなくても強制的に値を設定する</param>
    private void UpdateMovementState(bool forceUpdate = false)
    {
        // 必要なコンポーネントがなければ処理を中断
        if (navMeshAgent == null)
        {
            return;
        }

        // 1. 現在の速度を取得
        float currentSpeed = navMeshAgent.velocity.magnitude;

        // 2. 現在の速度から新しい状態を判断
        // (元のコード通り "MovementState" を使用)
        MovementState newState;
        if (currentSpeed > moveThreshold)
        {
            newState = MovementState.Walking;
        }
        else
        {
            newState = MovementState.Idle;
        }

        // 3. 状態が変化した時（または強制更新時）だけ通知
        if (newState != previousState || forceUpdate)
        {
            // A. FootStepControllerに通知
            if (footStepController != null)
            {
                footStepController.CurrentState = newState;
            }

            // B. Animatorに通知 (★要求されたロジック)
            if (CPUAnimator != null)
            {
                switch (newState)
                {
                    case MovementState.Idle:
                        CPUAnimator.SetBool("Walk", false);
                        break;
                    case MovementState.Walking:
                        CPUAnimator.SetBool("Walk", true);
                        break;
                    
                    // ※もし MovementState に Running がある場合の分岐例
                    // case MovementState.Running:
                    //     CPUAnimator.SetBool("Walk", true); 
                    //     break;
                }
            }

            previousState = newState; // 現在の状態を保存
        }
    }
}