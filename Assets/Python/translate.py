# translate.py
# (Assets/Python/translate.py に保存)
# ★ Base64 デコードを復活

import sys
import json
import base64 # (Base64を使うために必要)

def main():
    try:
        # --- ▼▼▼ Base64デコードを復活 ▼▼▼ ---
        # C#の StandardInput.Write() から「Base64文字列」を受け取る
        base64_data = sys.stdin.read()
        
        # Base64データをデコード
        json_data = base64.b64decode(base64_data).decode('utf-8')
        # --- ▲▲▲ ---
        
        # JSONをパース
        request = json.loads(json_data)
        
        response = {"results": []}

        # 各アイテムを処理
        for item in request["items"]:
            ja_text = item["japaneseText"]
            
            # (ダミー翻訳)
            
            if "\n" in ja_text:
                zh_text = ja_text + " [zh-Hans]\n(translated)"
                ko_text = ja_text + " [ko]\n(translated)"
            else:
                zh_text = ja_text + " [zh-Hans]"
                ko_text = ja_text + " [ko]"

            # 結果をレスポンスに追加
            response["results"].append({
                "collectionGUID": item["collectionGUID"],
                "key": item["key"],
                "chineseText": zh_text,
                "koreanText": ko_text
            })

        # C#側はこれを受け取る (ensure_ascii=False が重要)
        print(json.dumps(response, ensure_ascii=False))

    except Exception as e:
        # C#側でエラーが読み取れるように、エラーメッセージを標準エラー出力(stderr)に書き出す
        print(f"Python Error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()