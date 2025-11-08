// IInteractable.cs (変更なし)

using UnityEngine;

public interface IInteractable
{
    /// <summary>
    /// プレイヤーがこのオブジェクトとインタラクトした時に呼ばれる
    /// </summary>
    void Interact(); // ★既存のまま (引数なし)

    /// <summary>
    /// クロスヘアに表示する文字列
    /// </summary>
    string GetDisplayText();

    /// <summary>
    /// クロスヘアを強調表示するかどうか
    /// </summary>
    bool ShouldHighlight();
}