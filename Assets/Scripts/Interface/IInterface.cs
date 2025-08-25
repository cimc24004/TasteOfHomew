using UnityEngine;

public interface IInteractable
{
    /// <summary> プレイヤーがこのオブジェクトとインタラクトした時に呼ばれる </summary>
    void Interact();

    /// <summary> クロスヘアに表示する文字列 </summary>
    string GetDisplayText();

    /// <summary> クロスヘアを強調表示するかどうか </summary>
    bool ShouldHighlight();
}
