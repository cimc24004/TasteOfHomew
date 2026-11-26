// (Assets/Editor フォルダに配置してください)
// StringTableCollection単位で、全言語を1枚のCSV（ワイド形式）で入出力するバージョン
// CS0029エラー (GetLocales()の型) を修正

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;
using System.IO;
using System.Text;
using UnityEngine.Localization;
using System.Linq;

/// <summary>
/// エクスポート/インポートするStringTableCollectionとCSVファイルの紐付け
/// </summary>
[System.Serializable]
public class CollectionCsvMapping
{
    // StringTableCollection (例: UI_StringTableCollection.asset)
    public StringTableCollection collection;
    // CSVファイルのアセットパス (例: Assets/Localization_CSVs/UI_StringTableCollection.csv)
    public string csvAssetPath;
}

/// <summary>
/// LocalizationのStringTableCollectionをCSVで一括エクスポート・インポートするエディタウィンドウ
/// </summary>
public class LocalizationTableProcessor : EditorWindow
{
    // --- UI用変数 ---
    private string searchPath = "Assets/";
    private string exportPath = "Assets/Localization_CSVs";
    private Vector2 scrollPos;

    // シリアライズ（保存）対象のマッピングリスト
    [SerializeField]
    private List<CollectionCsvMapping> mappings = new List<CollectionCsvMapping>();

    // --- メニューに項目を追加 ---
    [MenuItem("Window/Localization/Table Collection CSV Processor")]
    public static void ShowWindow()
    {
        GetWindow<LocalizationTableProcessor>("Localization CSV Processor");
    }

    // --- UIの描画 ---
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Localization Table Collection CSV Processor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("StringTableCollection単位で、全言語を1枚のCSV（ワイド形式）で入出力します。", MessageType.Info);
        EditorGUILayout.Space(10);

        // --- 1. 設定エリア ---
        EditorGUILayout.LabelField("1. Settings", EditorStyles.miniBoldLabel);
        searchPath = EditorGUILayout.TextField("Collection Search Path", searchPath);
        exportPath = EditorGUILayout.TextField("CSV Export/Import Path", exportPath);

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("Search Path: StringTableCollectionアセット(*.asset)を検索するフォルダ。\n" +
                              "Export Path: CSVを保存/読み込みするフォルダ。", MessageType.Info);

        // --- 2. 実行エリア ---
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("2. Actions", EditorStyles.miniBoldLabel);

        if (GUILayout.Button("Find Collections and Update List"))
        {
            FindCollections();
        }

        EditorGUILayout.Space(5);

        // Exportボタン
        EditorGUI.BeginDisabledGroup(mappings.Count == 0);
        if (GUILayout.Button($"Export {mappings.Count} Collections to CSV"))
        {
            ExportCollections();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(5);

        // Importボタン
        EditorGUI.BeginDisabledGroup(mappings.Count == 0);
        if (GUILayout.Button($"Import {mappings.Count} CSVs to Collections"))
        {
            if (EditorUtility.DisplayDialog("Confirm Import",
                $"本当に {mappings.Count} 件のCSVをインポートしますか？\n" +
                "テーブルのデータが上書きされます。元に戻すことはできません。", "Import (実行)", "Cancel"))
            {
                ImportCollections();
            }
        }
        EditorGUI.EndDisabledGroup();

        // --- 3. 紐付けリスト表示エリア ---
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"3. Mapped Collections - {mappings.Count} items", EditorStyles.miniBoldLabel);

        // スクロールビュー
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (mappings.Count == 0)
        {
            EditorGUILayout.LabelField("No StringTableCollections found. Check Path.");
        }
        else
        {
            SerializedObject serializedObject = new SerializedObject(this);
            SerializedProperty mappingsProp = serializedObject.FindProperty("mappings");

            for (int i = 0; i < mappingsProp.arraySize; i++)
            {
                SerializedProperty mapping = mappingsProp.GetArrayElementAtIndex(i);
                SerializedProperty collectionProp = mapping.FindPropertyRelative("collection");
                SerializedProperty csvPathProp = mapping.FindPropertyRelative("csvAssetPath");

                EditorGUILayout.BeginHorizontal(EditorStyles.textField);

                EditorGUILayout.PropertyField(collectionProp, GUIContent.none, GUILayout.MinWidth(150), GUILayout.MaxWidth(300));

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(csvPathProp.stringValue);
                EditorGUI.EndDisabledGroup();

                TextAsset csvAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPathProp.stringValue);
                EditorGUI.BeginDisabledGroup(csvAsset == null);
                if (GUILayout.Button("Ping", GUILayout.Width(40)))
                {
                    EditorGUIUtility.PingObject(csvAsset);
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 1. テーブルコレクション検索
    /// </summary>
    private void FindCollections()
    {
        if (!AssetDatabase.IsValidFolder(searchPath))
        {
            Debug.LogError($"[Table Processor] Search path '{searchPath}' is not valid.");
            return;
        }

        // "t:StringTableCollection" で検索
        string[] guids = AssetDatabase.FindAssets("t:StringTableCollection", new[] { searchPath });

        mappings.Clear();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StringTableCollection collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);

            if (collection != null)
            {
                // CSVの保存パスを決定 (例: UI_StringTableCollection.csv)
                string csvName = $"{collection.name}.csv";
                string csvAssetPath = Path.Combine(exportPath, csvName);
                csvAssetPath = csvAssetPath.Replace("\\", "/");

                mappings.Add(new CollectionCsvMapping
                {
                    collection = collection,
                    csvAssetPath = csvAssetPath
                });
            }
        }

        EditorUtility.SetDirty(this);
        Debug.Log($"[Table Processor] Found and mapped {mappings.Count} StringTableCollections.");
    }

    /// <summary>
    /// 2. エクスポート (ワイド形式)
    /// </summary>
    private void ExportCollections()
    {
        if (!Directory.Exists(exportPath))
        {
            Directory.CreateDirectory(Path.GetFullPath(exportPath));
            AssetDatabase.Refresh();
        }

        int exportCount = 0;
        foreach (var mapping in mappings)
        {
            if (mapping.collection == null || mapping.collection.SharedData == null) continue;

            StringTableCollection collection = mapping.collection;
            SharedTableData sharedData = collection.SharedData;

            // --- 修正箇所 (CS0029 エラー対応) ---

            // 1. このコレクションに含まれるロケールのIDをすべて取得 (HashSetによる高速検索用)
            var collectionLocaleIds = collection.Tables
                .Select(lazyRef => lazyRef.asset as StringTable)
                .Where(table => table != null)
                .Select(table => table.LocaleIdentifier)
                .ToHashSet(); // HashSetに変換

            // 2. プロジェクト設定に定義されている全ロケールを「定義順」に取得
            // GetLocales() は ReadOnlyCollection<Locale> を返すため、new List<Locale>() で変換する
            List<Locale> projectLocales = new List<Locale>(LocalizationEditorSettings.GetLocales());

            // 3. プロジェクトの定義順を維持しつつ、このコレクションに含まれるロケールだけを抽出
            List<Locale> localesForExport = projectLocales
                .Where(locale => collectionLocaleIds.Contains(locale.Identifier))
                .ToList();

            StringBuilder sb = new StringBuilder();

            // --- ヘッダー行の作成 ---
            List<string> headers = new List<string> { "Key", "Id" };
            // 3. で抽出した「正しい順序」のロケールリストからヘッダーを作成
            foreach (var locale in localesForExport)
            {
                // (例: "Japanese (ja)")
                headers.Add($"{locale.LocaleName} ({locale.Identifier.Code})");
            }
            sb.AppendLine(string.Join(",", headers));

            // --- データ行の作成 ---
            foreach (var sharedEntry in sharedData.Entries)
            {
                string key = sharedEntry.Key;

                // Idを指数表記にしないための対策
                string keyId = $"=\"{sharedEntry.Id}\"";

                List<string> row = new List<string> { key, keyId };

                // 各ロケールテーブルから「正しい順序」で値を取得
                foreach (var locale in localesForExport)
                {
                    StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                    string value = "";
                    if (table != null)
                    {
                        var tableEntry = table.GetEntry(key);
                        if (tableEntry != null)
                        {
                            value = tableEntry.Value;
                        }
                    }
                    row.Add(EscapeCsvField(value)); // 値をエスケープ
                }
                sb.AppendLine(string.Join(",", row));
            }

            // --- 文字化け対策: UTF-8 with BOM で書き出す ---
            string fullPath = Path.GetFullPath(mapping.csvAssetPath);
            try
            {
                File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(true));
                exportCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Table Processor] Failed to write CSV '{fullPath}': {e.Message}");
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[Table Processor] Exported {exportCount} CSV files to '{exportPath}'.");
    }

    /// <summary>
    /// 3. インポート (ワイド形式)
    /// </summary>
    private void ImportCollections()
    {
        int importCount = 0;
        HashSet<Object> modifiedAssets = new HashSet<Object>();

        foreach (var mapping in mappings)
        {
            if (mapping.collection == null) continue;

            string fullPath = Path.GetFullPath(mapping.csvAssetPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[Table Processor] Skipping import for '{mapping.collection.name}': CSV file not found at '{mapping.csvAssetPath}'");
                continue;
            }

            string[] lines;
            try
            {
                // --- 文字化け対策: UTF-8 with BOM で読み取る ---
                lines = File.ReadAllLines(fullPath, new UTF8Encoding(true));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Table Processor] Failed to read CSV '{fullPath}': {e.Message}");
                continue;
            }

            if (lines.Length <= 1) continue; // ヘッダーのみ

            StringTableCollection collection = mapping.collection;
            SharedTableData sharedData = collection.SharedData;

            // --- ヘッダーの解析 ---
            string[] headerParts = lines[0].Split(',');

            Dictionary<int, LocaleIdentifier> columnIndexToLocale = new Dictionary<int, LocaleIdentifier>();
            for (int i = 2; i < headerParts.Length; i++) // 0=Key, 1=Id
            {
                string header = headerParts[i];
                int startIndex = header.LastIndexOf('(');
                int endIndex = header.LastIndexOf(')');
                if (startIndex != -1 && endIndex != -1 && endIndex > startIndex + 1)
                {
                    string code = header.Substring(startIndex + 1, endIndex - startIndex - 1);
                    columnIndexToLocale[i] = new LocaleIdentifier(code);
                }
            }

            // --- データ行の解析 ---
            for (int i = 1; i < lines.Length; i++) // 1行目からデータ
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                string[] parts = SplitCsvLine(line); // 簡易パーサー
                if (parts.Length < 2) continue;

                string key = parts[0].Trim();
                if (string.IsNullOrEmpty(key)) continue;

                // 1. SharedDataにキーがなければ追加
                if (sharedData.GetEntry(key) == null)
                {
                    sharedData.AddKey(key);
                    modifiedAssets.Add(sharedData);
                }

                // 2. 各ロケールテーブルに値を設定
                foreach (var pair in columnIndexToLocale)
                {
                    int colIndex = pair.Key;
                    LocaleIdentifier localeId = pair.Value;

                    if (colIndex < parts.Length) // CSVデータにその列が存在するか
                    {
                        string value = UnescapeCsvField(parts[colIndex]);

                        // 対応するStringTableを取得
                        StringTable table = collection.GetTable(localeId) as StringTable;

                        // テーブルがまだ存在しない場合 (例: jaはあるがenはCSVにしか存在しない)
                        if (table == null)
                        {
                            // 1. StringTableインスタンスを新規作成
                            table = ScriptableObject.CreateInstance<StringTable>();

                            // 2. ロケールIDとSharedDataを設定
                            table.LocaleIdentifier = localeId;
                            table.SharedData = sharedData;

                            // 3. アセットファイルとして保存
                            string tablePath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(collection));
                            string tableName = $"{collection.TableCollectionName}_{localeId.Code}.asset";
                            string fullAssetPath = Path.Combine(tablePath, tableName).Replace("\\", "/");

                            AssetDatabase.CreateAsset(table, fullAssetPath);

                            // 4. 作成したアセット(table)をCollectionに追加
                            collection.AddTable(table);

                            modifiedAssets.Add(table); // 新規テーブルを保存対象に
                            modifiedAssets.Add(collection); // Collectionも変更された
                            Debug.Log($"[Table Processor] Created new table: {fullAssetPath}");
                        }

                        // エントリーを更新または追加
                        var tableEntry = table.GetEntry(key);
                        if (tableEntry != null)
                        {
                            tableEntry.Value = value;
                        }
                        else
                        {
                            tableEntry = table.AddEntry(key, value);
                        }

                        if (tableEntry.IsSmart) tableEntry.IsSmart = true; // Smart Stringの再パース

                        modifiedAssets.Add(table);
                    }
                }
            }
            importCount++;
        }

        if (importCount > 0)
        {
            foreach (var asset in modifiedAssets)
            {
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets(); // アセットの変更をディスクに保存
            Debug.Log($"[Table Processor] Import complete. Updated {importCount} collections. Assets saved.");
        }
        else
        {
            Debug.Log("[Table Processor] Import finished, but no files were imported or no changes detected.");
        }
    }

    // --- CSVヘルパー関数 ---

    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.Contains("\"") || field.Contains(",") || field.Contains("\n") || field.Contains("\r"))
        {
            string escaped = field.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }
        return field;
    }

    private string UnescapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (field.StartsWith("\"") && field.EndsWith("\"") && field.Length >= 2)
        {
            string unescaped = field.Substring(1, field.Length - 2);
            return unescaped.Replace("\"\"", "\"");
        }
        // 指数表記対策で ="[ID]" としたものを元に戻す処理
        if (field.StartsWith("=\"") && field.EndsWith("\"") && field.Length >= 3)
        {
             return field.Substring(2, field.Length - 3);
        }
        return field;
    }

    /// <summary>
    /// CSVの行を分割する簡易パーサー (ダブルクォート内のカンマを考慮)
    /// </summary>
    private string[] SplitCsvLine(string line)
    {
        List<string> fields = new List<string>();
        StringBuilder currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '\"')
            {
                // " が連続している (escaped quote)
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    currentField.Append('\"');
                    i++; // 次の " をスキップ
                }
                else
                {
                    inQuotes = !inQuotes;
                    currentField.Append(c); // クォート自体もフィールドに含める (UnescapeCsvFieldで処理するため)
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // フィールドの区切り
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }

        fields.Add(currentField.ToString()); // 最後のフィールド
        return fields.ToArray();
    }
}
