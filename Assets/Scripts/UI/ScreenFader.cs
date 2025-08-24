using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ScreenFader : MonoBehaviour
{
    [SerializeField] private Image fadeImage;

    public void FadeInOut(float fadeInTime = 1f, float waitTime = 0.5f, float fadeOutTime = 1f)
    {
        fadeImage.color = new Color(0, 0, 0, 0); // 透明から始める

        Sequence seq = DOTween.Sequence();
        seq.Append(fadeImage.DOFade(1f, fadeInTime));    // 黒くなる（フェードイン）
        seq.AppendInterval(waitTime);                   // 真っ暗で待機
        seq.Append(fadeImage.DOFade(0f, fadeOutTime));  // 黒が消える（フェードアウト）
    }
}
