using UnityEngine;
using UnityEngine.Animations.Rigging;

public class AimConstraintTrigger : MonoBehaviour
{
    // Inspectorから制御したいMultiAimConstraintコンポーネントをアタッチします
    [SerializeField]
    private MultiAimConstraint aimConstraint;

    // オブジェクトがトリガーに侵入した時に呼び出されるメソッド
    private void OnTriggerEnter(Collider other)
    {
        // 侵入してきたオブジェクトのタグが "Player" かどうかを確認
        if (other.CompareTag("Player"))
        {
            // aimConstraintが設定されていれば、weightを1に設定
            if (aimConstraint != null)
            {
                aimConstraint.weight = 1f;
                Debug.Log("Player entered: AimConstraint weight set to 1");
            }
            else
            {
                Debug.LogWarning("AimConstraintが設定されていません。");
            }
        }
    }

    // オブジェクトがトリガーから出た時に呼び出されるメソッド
    private void OnTriggerExit(Collider other)
    {
        // 出ていったオブジェクトのタグが "Player" かどうかを確認
        if (other.CompareTag("Player"))
        {
            // aimConstraintが設定されていれば、weightを0に設定
            if (aimConstraint != null)
            {
                aimConstraint.weight = 0f;
                Debug.Log("Player exited: AimConstraint weight set to 0");
            }
        }
    }
}
