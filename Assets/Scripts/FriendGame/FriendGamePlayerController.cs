using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class FriendGamePlayerController : SerializedMonoBehaviour
{
    [Title("Refs")]
    [Required] public Rigidbody2D rb;
    [Required] public Animator animator;
    [Required, LabelText("Rayの発射位置（地面判定の原点）")] public Transform rayOrigin;
    [LabelText("地面レイヤー（ここに含まれるレイヤーだけが地面として扱われる）")] public LayerMask groundLayer;

    [Title("機能フラグ")]
    [LabelText("Moveモードが true のときだけ移動できる／ジャンプできる")] public bool moveMode = true;

    [Title("地面判定")]
    [LabelText("地面として扱うタグ")] public string groundTag = "Ground";
    [LabelText("判定距離")] public float groundCheckDistance = 0.2f;

    [Title("移動")]
    [LabelText("移動速度")] public float speed = 5f;
    [ShowInInspector, ReadOnly] public bool isMoving = false;
    [LabelText("Inspectorで使う移動方向")] public Vector2 moveDirection = Vector2.right;

    [Title("ジャンプ（長押し対応）")]
    [LabelText("初動ジャンプ力（押した瞬間にセットする線形速度）")] public float initialJumpForce = 12f;
    [LabelText("長押しで追加する上向きの力（毎秒）")] public float holdJumpForcePerSec = 18f;
    [LabelText("長押しで追加力が効く最大時間（秒）")] public float maxJumpHoldTime = 0.25f;

    [Title("ジャンプ入力補助")]
    [LabelText("コヨーテタイム（秒）")] public float coyoteTime = 0.12f;
    [LabelText("ジャンプバッファ時間（秒）")] public float jumpBufferTime = 0.12f;

    [Title("Animator パラメータ名")]
    [LabelText("移動フラグ名")] public string animIsMoving = "isMoving";
    [LabelText("速度（float）名")] public string animSpeed = "speed";
    [LabelText("接地フラグ名")] public string animIsGrounded = "isGrounded";
    [LabelText("ジャンプ Trigger 名")] public string animJumpTrigger = "Jump";
    [LabelText("垂直速度（任意、float）名（空文字なら使わない）")] public string animVertical = "verticalVelocity";

    [Title("サウンド")]
    [Required] public AudioSource audioSource;
    [LabelText("ジャンプ音")] public AudioClip jumpClip;

    // Inspector表示用
    [ShowInInspector, ReadOnly] public bool inspectorIsGrounded = false;
    [ShowInInspector, ReadOnly] public float lastGroundedTime = -999f;
    [ShowInInspector, ReadOnly] public float lastJumpPressedTime = -999f;
    [ShowInInspector, ReadOnly] public string groundHitName = "null";
    [ShowInInspector, ReadOnly] public float holdTimer = 0f;

    // 内部
    private InputAction jumpAction; // Start() で取得
    private bool prevJumpPressed = false;
    private bool isButtonHeld = false;
    private bool isHoldingJumpAdd = false;

    // Start で Input 初期化（AwakeにSingleton参照を書かない方針）
    private void Start()
    {
        jumpAction = PlayerDataHub.instance.CurrentplayerInput.actions["FriendGameJump"];
    }

    private void Update()
    {
        // --- 入力ポーリング（Update内） ---
        float actionVal = jumpAction.ReadValue<float>();
        bool currentPressed = actionVal > 0.5f
                              || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);

        // 上昇エッジ（押し始め）
        if (currentPressed && !prevJumpPressed)
        {
            // 今回は moveMode が true のときのみバッファに記録する仕様
            if (moveMode) lastJumpPressedTime = Time.time;
            isButtonHeld = true;
        }

        // 下降エッジ（離し）
        if (!currentPressed && prevJumpPressed)
        {
            isButtonHeld = false;
            isHoldingJumpAdd = false;
            holdTimer = 0f;
        }

        prevJumpPressed = currentPressed;

        // 入力バッファ・コヨーテ判定を Update で評価してジャンプを発行
        TryConsumeBufferedJump();

        // Animator 更新（Update で行う）
        float vx = rb.linearVelocity.x;
        animator.SetBool(animIsMoving, moveMode && isMoving); // moveMode false なら animator にも反映しない
        animator.SetFloat(animSpeed, Mathf.Abs(vx));
        if (!string.IsNullOrEmpty(animVertical)) animator.SetFloat(animVertical, rb.linearVelocity.y);
        animator.SetBool(animIsGrounded, inspectorIsGrounded);
    }

    private void FixedUpdate()
    {
        // 横移動
        if (moveMode && isMoving)
        {
            Vector2 lv = rb.linearVelocity;
            lv.x = moveDirection.normalized.x * speed;
            rb.linearVelocity = lv;
        }
        else
        {
            // moveMode が false のときは横速度をゼロにして移動不可にする
            Vector2 lv = rb.linearVelocity;
            lv.x = 0f;
            rb.linearVelocity = lv;
        }

        // 地面判定（Inspector表示用を更新）
        UpdateGroundedState();

        // 長押しジャンプの追加上向き加速（FixedUpdateで処理）
        if (moveMode && isHoldingJumpAdd && holdTimer < maxJumpHoldTime)
        {
            float delta = Time.fixedDeltaTime;
            float add = holdJumpForcePerSec * delta;
            Vector2 lv = rb.linearVelocity;
            lv.y += add;
            rb.linearVelocity = lv;
            holdTimer += delta;
        }
    }

    [Button("Start Move (Inspector)")]
    private void MovestartInspector() => Movestart();

    // Movestart しても moveMode が false なら移動させない（フラグ優先）
    public void Movestart()
    {
        isMoving = true;
        if (moveMode)
        {
            Vector2 lv = rb.linearVelocity;
            lv.x = moveDirection.x * speed;
            rb.linearVelocity = lv;
        }
        else
        {
            // もし moveMode が false なら確実に停止させる
            Vector2 lv = rb.linearVelocity;
            lv.x = 0f;
            rb.linearVelocity = lv;
        }
    }

    [Button("Stop (Inspector)")]
    public void Stop()
    {
        isMoving = false;
        rb.linearVelocity = Vector2.zero;
    }

    // バッファ→ジャンプ実行判定（Updateから呼ぶ）
    private void TryConsumeBufferedJump()
    {
        // moveMode が true のときだけジャンプ処理へ進める
        if (!moveMode) return;

        if (Time.time - lastJumpPressedTime <= jumpBufferTime)
        {
            if (inspectorIsGrounded || (Time.time - lastGroundedTime) <= coyoteTime)
            {
                DoJump();
                lastJumpPressedTime = -999f;
            }
        }
    }

    private void DoJump()
    {
        // moveMode が true のときだけ実行（念のための二重チェック）
        if (!moveMode) return;

        // 初速セット
        Vector2 lv = rb.linearVelocity;
        lv.y = initialJumpForce;
        rb.linearVelocity = lv;

        // 長押し追加処理を許可（ボタンがまだ押されている場合のみ）
        isHoldingJumpAdd = isButtonHeld;
        holdTimer = 0f;

        // サウンド（初動のみ）
        audioSource.PlayOneShot(jumpClip);

        // Animator のジャンプトリガー
        animator.SetTrigger(animJumpTrigger);
    }

    // 地面判定を実行して Inspector 表示用の値を更新
    private void UpdateGroundedState()
    {
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin.position, Vector2.down, groundCheckDistance, groundLayer);
        if (hit.collider)
        {
            bool tagMatch = hit.collider.CompareTag(groundTag);
            inspectorIsGrounded = tagMatch;
            groundHitName = hit.collider.name;
            if (tagMatch)
            {
                lastGroundedTime = Time.time;
                if (Time.time - lastJumpPressedTime <= jumpBufferTime)
                {
                    DoJump();
                    lastJumpPressedTime = -999f;
                }
            }
        }
        else
        {
            inspectorIsGrounded = false;
            groundHitName = "null";
        }
    }

    [Button("Jump (Inspector)")]
    private void JumpInspector()
    {
        if (moveMode)
        {
            lastJumpPressedTime = Time.time;
            TryConsumeBufferedJump();
        }
    }

    // Sceneビューで見やすくする Gizmos（Ray とヒット表示）
    private void OnDrawGizmosSelected()
    {
        if (rayOrigin == null) return;

        Vector3 origin = rayOrigin.position;
        Vector3 endPoint = origin + Vector3.down * groundCheckDistance;

        // レイの基本ライン
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, endPoint);
        Gizmos.DrawWireSphere(endPoint, 0.02f);

        // Raycast 実行してヒット情報を描画
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
        if (hit.collider)
        {
            Gizmos.color = hit.collider.CompareTag(groundTag) ? Color.green : Color.yellow;
            Gizmos.DrawSphere((Vector3)hit.point, 0.04f);
            Gizmos.DrawLine(hit.point, hit.point + (Vector2)hit.normal * 0.2f);

#if UNITY_EDITOR
            string info = $"Hit: {hit.collider.name}\nTag: {hit.collider.tag}\nLayer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}";
            Handles.color = Color.white;
            Handles.Label(hit.point + Vector2.up * 0.08f, info);
#endif
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(endPoint, 0.03f);
#if UNITY_EDITOR
            Handles.color = Color.white;
            Handles.Label(endPoint + Vector3.up * 0.06f, "No hit");
#endif
        }

        // origin 表示
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(origin, 0.02f);
#if UNITY_EDITOR
        Handles.Label(origin + Vector3.up * 0.06f, $"Origin");
#endif
    }
}
