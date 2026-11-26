using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components; // LocalizedAssetEvent を継承するために必要
using TMPro; // TMP_FontAsset のために必要

/// <summary>
/// TMP_FontAsset をローカライズするための専用コンポーネント
/// </summary>
[AddComponentMenu("Localization/Localize TMP_FontAsset Event")]
public class LocalizedFontAssetEvent : LocalizedAssetEvent<TMP_FontAsset, LocalizedAsset<TMP_FontAsset>, UnityEvent<TMP_FontAsset>>
{
    // このスクリプトは、中身が空のままでOKです。
    // 親クラス (LocalizedAssetEvent) がすべてのロジックを処理してくれます。
}
