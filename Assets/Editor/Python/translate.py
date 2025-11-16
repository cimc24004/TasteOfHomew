# translate.py
# (Assets/Python/translate.py に保存)
# ★ ワークフロー変更
# C#から来たデータに、翻訳用の空キー (englishText, etc.) を追加し、
# 整形して C# に返す

import sys
import json
import base64 

def main():
    try:
        # 1. C#から Base64 データを受け取り、デコードする
        base64_data = sys.stdin.read()
        json_data = base64.b64decode(base64_data).decode('utf-8')
        request = json.loads(json_data)
        
        # 2. C#から来た「日本語の表」 (request["items"]) をループ処理
        
        for item in request["items"]:
            # 3. ★ご要望の、翻訳用の「空のキー」を追加する
            item["englishText"] = ""
            item["chineseText"] = ""
            item["koreanText"] = ""
        
        # 4. C# に「キーが追加された表」を、読みやすく整形(indent)して返す
        #    C#側はこれをファイルに保存する
        
        # indent=2 をつけることで、見やすいJSONファイルになる
        print(json.dumps(request, ensure_ascii=False, indent=2))

    except Exception as e:
        # C#側でエラーが読み取れるように、エラーメッセージを標準エラー出力(stderr)に書き出す
        print(f"Python Error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()