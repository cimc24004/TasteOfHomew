using UnityEngine;
using UnityEngine.Events;

public class PlayerLocation : Singleton<PlayerLocation>
{
    /// <summary>
    /// プレイヤーのいる場所を定義します。
    /// </summary>
    public enum Location
    {
        InHouse, // 家の中
        Outside  // 外
    }
    public Location currentLocation = Location.InHouse;
    public UnityEvent SetLocationEvent;
        /// <summary>
    /// プレイヤーの現在地を設定し、BGMを更新します。
    /// 家の内外に入るトリガーなどからこの関数を呼び出してください。
    /// </summary>
    /// <param name="newLocation">新しい場所</param>
    public void SetLocation(PlayerLocation.Location newLocation)
    {

        if (currentLocation != newLocation)
        {
            Debug.Log($"[EnvironmentBGM] 場所が変更されました: {currentLocation} -> {newLocation}");
            currentLocation = newLocation;
            SetLocationEvent.Invoke();
        }
    }
}
