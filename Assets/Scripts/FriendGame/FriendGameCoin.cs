using TMPro;
using UnityEngine.UI;
using UnityEditor.Search;
using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

public class FriendGameCoin : MonoBehaviour
{
    private FriendGameController friendGameController;
    [SerializeField] private int GetCount = 1;
    [Title("演出")]
    [MinValue(0f)] public float riseDistance = 1.0f;
    [MinValue(0f)] public float tweenDuration = 0.45f;
    [MinValue(0f)] public float scalePunch = 0.2f;
    [MinValue(0f)] public float rotateZ = 25f;
    [LabelText("拾得後に当たり判定を切る")] public bool disableColliderOnPickup = true;

    [Title("見た目")]
    [Required, SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Collider2D triggerCollider;

    // 復活用に初期状態を保持
    private Vector3 initialPosition;
    private Color initialColor;

    void Awake()
    {
        if (!triggerCollider) triggerCollider = GetComponent<Collider2D>();
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        initialPosition = transform.position;
        initialColor = spriteRenderer ? spriteRenderer.color : Color.white;
    }

    void Start()
    {
        friendGameController = FriendGameController.instance;
    }

    void OnEnable()
    {
        // Retryで再表示されたときの復元
        transform.position = initialPosition;
        if (spriteRenderer)
        {
            var c = initialColor; c.a = 1f;
            spriteRenderer.color = c;
        }
        if (triggerCollider) triggerCollider.enabled = true;
        // 念のため前のTweenは殺しておく
        DOTween.Kill(transform);
        if (spriteRenderer) DOTween.Kill(spriteRenderer);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.name == "Player2D")
        {
            friendGameController.CoinGet(GetCount);

            if (disableColliderOnPickup && triggerCollider)
                triggerCollider.enabled = false;

            var seq = DOTween.Sequence();
            var startPos = transform.position;

            // ちょい拡大→上昇&フェード→非表示
            seq.Append(transform.DOPunchScale(Vector3.one * scalePunch, 0.18f, 6, 0.7f));
            seq.Join(transform.DORotate(new Vector3(0, 0, rotateZ), 0.18f, RotateMode.FastBeyond360));
            seq.Append(transform.DOMove(startPos + Vector3.up * riseDistance, tweenDuration).SetEase(Ease.OutQuad));
            if (spriteRenderer)
                seq.Join(spriteRenderer.DOFade(0f, tweenDuration));
            // ★ Destroyではなく SetActive(false)
            seq.OnComplete(() => gameObject.SetActive(false));
        }
    }

    void OnDisable()
    {
        // 念のためTween停止
        DOTween.Kill(transform);
        if (spriteRenderer) DOTween.Kill(spriteRenderer);
    }
}
