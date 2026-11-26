// https://www.hanachiru-blog.com/entry/2019/05/25/170042
//
// Created by hanachiru on 2019.05.26
// Modified by Gemini (for layout and scrolling)

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace PlayerPrefsEditor
{
    public class PlayerPrefsDeleter : EditorWindow
    {
        private List<PlayerPrefsData> _playerPrefData = null;

        // スクロール位置を保持する変数
        private Vector2 _scrollPosition;

        [MenuItem("Tools/PlayerPrefsDeleter")]
        private static void Open()
        {
            var window = GetWindow<PlayerPrefsDeleter>("PlayerPrefsDeleter");

            window.minSize = new Vector2(500, 180);

            window.LoadPlayerPrefs();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_playerPrefData == null || _playerPrefData.Count() == 0)
            {
                EditorGUILayout.LabelField("PlayerPrefs do not exist");
                return;
            }

            // リスト全体をスクロールビューで囲む
            // _scrollPosition 変数を使って現在のスクロール位置を渡し、
            // 更新されたスクロール位置を受け取る
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            {
                // リスト描画関数を呼ぶ
                DrawPlayerPrefsList();
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// PlayerPrefsを読み込む (変更なし)
        /// </summary>
        private void LoadPlayerPrefs()
        {
#if UNITY_EDITOR
            //リストの初期化
            if (_playerPrefData != null)
            {
                _playerPrefData.Clear();
            }
            else
            {
                _playerPrefData = new List<PlayerPrefsData>();
            }

            //PlayerPrefsのすべてのキーを検索する
            var keys = new List<string>();
            PlayerPrefsTools.GetAllPlayerPrefKeys(ref keys);

            //リストの作成
            foreach (var key in keys)
            {
                _playerPrefData.Add(new PlayerPrefsData(key));
            }
#else
            _playerPrefData = null;
#endif
        }

        /// <summary>
        /// PlayerPrefsを全て削除する (変更なし)
        /// </summary>
        private void ResetPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            _playerPrefData = null;
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                if (GUILayout.Button("DeleteAll...", EditorStyles.toolbarButton))
                {
                    Debug.Log("Delete All PlayerPrefs");
                    ResetPlayerPrefs();
                }
                if (GUILayout.Button("Reload...", EditorStyles.toolbarButton))
                {
                    Debug.Log("Reload PlayerPrefs");
                    LoadPlayerPrefs();
                }
            }
        }

        /// <summary>
        /// PlayerPrefsのリストを描画する
        /// </summary>
        private void DrawPlayerPrefsList()
        {
            bool isDeleted = false;

            // 値表示用のスタイルを定義（折り返しを有効にする）
            GUIStyle wrappedSelectableField = new GUIStyle(EditorStyles.textField);
            wrappedSelectableField.wordWrap = true;

            // BeginScrollView の直下には、要素全体の高さを計算させるため
            // 一つの Vertical グループが必要です。
            EditorGUILayout.BeginVertical();

            // リストの全項目をループ
            foreach (var item in _playerPrefData)
            {
                // 各項目をボックスで囲んで見やすくする
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // --- 1行目：キー（太字）と 削除ボタン ---
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField(item.Key, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace(); // ボタンを右端に寄せる

                    if (GUILayout.Button("Delete", GUILayout.Width(64), GUILayout.Height(16)))
                    {
                        item.Delete();
                        isDeleted = true;
                        Debug.Log(item.Key + " is deleted");
                    }
                }
                EditorGUILayout.EndHorizontal();

                // --- 2行目：値（折り返し表示） ---
                EditorGUILayout.LabelField("Value:");

                // SelectableLabel を使い、TextField風のスタイル（背景あり・折り返しあり）で表示
                EditorGUILayout.SelectableLabel(
                    item.Value,
                    wrappedSelectableField, // 上で定義した折り返しスタイル
                    GUILayout.ExpandWidth(true), // 幅はウィンドウに合わせる
                    GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2) // 最低でも2行分の高さを確保
                );

                EditorGUILayout.EndVertical();
                GUILayout.Space(4); // 各項目の間に少しスペースを入れる
            }

            // スクロールビュー内の Vertical を終了
            EditorGUILayout.EndVertical();

            if (isDeleted) LoadPlayerPrefs();
        }
    }

    /// <summary>
    /// PlayerPrefsのキーと値を保持するデータクラス (変更なし)
    /// </summary>
    public class PlayerPrefsData
    {
        public string Key { get; }
        public string Value { get; }

        public PlayerPrefsData(string key)
        {
            Key = key;
            Value = GetValue(key);
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
        }

        /// <summary>
        /// PlayerPrefsのstring,int,floatのどれかを返す (元のロジックのまま)
        /// </summary>
        private string GetValue(string key)
        {
            string value = PlayerPrefs.GetString(key, null);

            if (string.IsNullOrEmpty(value))
            {
                value = PlayerPrefs.GetInt(key, 0).ToString();

                if (value == "0")
                {
                    value = PlayerPrefs.GetFloat(key, 0).ToString();
                }
            }

            return value;
        }
    }
}
