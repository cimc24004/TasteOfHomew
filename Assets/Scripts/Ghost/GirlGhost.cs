using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

public class GirlGhost : MonoBehaviour
{
    private GameObject player;
    private GameObject CameraRoot;
    [SerializeField] private AudioSource JumpScareSound;
    
    // ★ 追加: カメラのどれくらい手前で止まるか
    [SerializeField] private float stopDistance = 0.5f; // 0.5m (50cm) 手前
    private float to_player_distance;
    [SerializeField]private Animator anim;
    void Start()
    {
        player = PlayerStatus.instance.gameObject;
        CameraRoot = PlayerStatus.instance.CameraRoot;
    }

    [Button]
    public void JumpScareToPlayer()
    {
        // --- 移動先の計算 ---
        Vector3 vectorToCamera = player.transform.position - this.transform.position;
        Vector3 directionToCamera = vectorToCamera.normalized;
        Vector3 targetPosition = player.transform.position - (directionToCamera * stopDistance);
        // --------------------

        // ★ 修正: 移動開始と同時に音を鳴らす
        anim.SetBool("GhostIdle", true);
        DOVirtual.DelayedCall(1, () =>
        {
            // ★ 修正: OnUpdate での距離計算と再生処理を削除
            this.transform.DOMove(targetPosition, 0.35f)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                // オブジェクトを非表示
                this.gameObject.SetActive(false);
            })
            .SetLink(this.gameObject);
            JumpScareSound.Play();
        });
        


    }
}