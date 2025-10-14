using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "Readable_", menuName = "Game/Readable Asset")]
public class ReadableAsset : ScriptableObject
{
    [HorizontalGroup("h"), LabelText("ID"), Required]
    public string id;                   // セーブ/フラグ用の一意キー
    [HorizontalGroup("h"), LabelText("タイトル")]
    public string title;

    [Title("本文 / ページ")]
    [TextArea(3, 10)]
    [LabelText("ページ本文(複数)")]
    public List<string> pages = new();

    [LabelText("背景画像(任意)")]
    public List<Sprite> backgroundImages = new(); // pagesと同インデックス対応（足りない分はnullでOK）

    [LabelText("前面画像(任意)")]
    public List<Sprite> foregroundImages = new(); // pagesと同インデックス対応（足りない分はnullでOK）

    [Title("UI/挙動")]
    [LabelText("開いたらゲームを一時停止")]
    public bool pauseWhileOpen = true;

    [LabelText("最初に開くページ(1始まり)")]
    [MinValue(1)] public int defaultStartPage = 1;

    [LabelText("各ページで閉じるボタンを表示")]
    [Tooltip("本文ページのインデックスに対応します。チェックすると表示、しないと非表示。数が足りないページは表示されます。")]
    public List<bool> showCloseButtonOnPages = new();

    [Title("効果音(任意)")]
    public AudioClip openSE;
    public AudioClip closeSE;
    public AudioClip turnSE;
}