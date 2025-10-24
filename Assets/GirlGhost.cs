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

    void Start()
    {
        player = PlayerStatus.instance.gameObject;
        CameraRoot = PlayerStatus.instance.CameraRoot;
    }

    [Button]
    public void JumpScareToPlayer()
    {
        // --- ★ 修正点: 移動先を「カメラの手前」に計算し直す ---
        
        // 1. ゴーストからカメラへの「方向」と「距離」を計算
        Vector3 vectorToCamera = CameraRoot.transform.position - this.transform.position;
        Vector3 directionToCamera = vectorToCamera.normalized; // 方向 (長さ1のベクトル)
        // float currentDistance = vectorToCamera.magnitude; // 現在の距離 (今回は不要)

        // 2. 最終的な移動先（ゴール地点）を計算
        //    カメラの位置から、「ゴーストが来た方向」に stopDistance 分だけ戻った場所
        Vector3 targetPosition = CameraRoot.transform.position - (directionToCamera * stopDistance);

        // ----------------------------------------------------


        // ★ 修正点: DOMoveの移動先を targetPosition に変更
        this.transform.DOMove(targetPosition, 0.2f)
            .SetEase(Ease.Linear)
            .OnUpdate(() =>
            {
                // 移動中、常に「カメラ本体」を見続ける (これで顔が映る)
                this.transform.LookAt(CameraRoot.transform.position);
            })
            .OnComplete(() =>
            {
                // 音はカメラの位置（耳元）で鳴らすのが効果的
                if (JumpScareSound != null && JumpScareSound.clip != null)
                {
                    AudioSource.PlayClipAtPoint(JumpScareSound.clip, CameraRoot.transform.position);
                }
                else
                {
                    Debug.LogWarning("JumpScareSound または AudioClip が設定されていません。");
                }

                // オブジェクトを非表示
                this.gameObject.SetActive(false);
            })
            .SetLink(this.gameObject);
    }
}