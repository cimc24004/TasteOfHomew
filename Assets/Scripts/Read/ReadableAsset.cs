using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "Readable_", menuName = "Game/Readable Asset")]
public class ReadableAsset : ScriptableObject
{
    [HorizontalGroup("h"), LabelText("ID"), Required]
    public string id;                    // セーブ/フラグ用の一意キー
    [HorizontalGroup("h"), LabelText("タイトル")]
    public string title;

    [Title("本文 / ページ")]
    [TextArea(3, 10)]
    [LabelText("ページ本文(複数)")]
    public List<string> pages = new();

    [LabelText("ページ画像(任意)")]
    public List<Sprite> pageImages = new(); // pages と同インデックス対応（足りない分はnullでOK）

    [Title("UI/挙動")]
    [LabelText("開いたらゲームを一時停止")]
    public bool pauseWhileOpen = true;
    [LabelText("最初に開くページ(1始まり)")]
    [MinValue(1)] public int defaultStartPage = 1;

    [Title("効果音(任意)")]
    public AudioClip openSE;
    public AudioClip closeSE;
    public AudioClip turnSE;
}
