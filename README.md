# ExplorerTabs

タブが入りきらない時に **2段・3段と折り返す** フォルダ窓です。
中身は Windows 標準のフォルダ表示（ExplorerBrowser）をそのまま埋め込んでいるので、右クリックのメニュー・ドラッグ＆ドロップ・プレビューなどはエクスプローラーと同じように使えます。

A tabbed folder window for Windows whose tabs wrap onto multiple rows. It hosts the standard Windows Explorer view (ExplorerBrowser), so context menus and drag & drop work as usual.

## できること

- タブが多くなると折り返して2段・3段に並ぶ（横にスクロールしない）
- 左にフォルダのツリー、上にアドレス欄と ← → ↑ ＋ のボタン
- フォルダごとに **表示（詳細・アイコンとその大きさ）と並び替え** を覚えて、次に開いた時に戻す
- グループ化（「ダウンロード」の日付別など）はいつも「なし」で開く
- Ctrl+C / Ctrl+V / Ctrl+X / Ctrl+A / Ctrl+Z / F2 などはエクスプローラーと同じく効く
- **Del キーではファイルを消さない**（警告だけ出す）。消す時は右クリック →「削除」。Shift+Del・Ctrl+D も同じく止めてある（うっかり消し防止）

## 使い方

`ExplorerTabs.exe` を起動します。引数にフォルダを並べると、それぞれをタブで開きます。

```
ExplorerTabs.exe "C:\Users\Public" "D:\作業"
```

引数が無い時はユーザーフォルダを開きます。

| 操作 | 動き |
|---|---|
| Ctrl+T ／ ＋ボタン | 今のフォルダを新しいタブで開く |
| Ctrl+W ／ タブを中ボタン ／ タブをダブルクリック | タブを閉じる |
| Ctrl+Tab ／ Ctrl+Shift+Tab | 次 ／ 前のタブ |
| Ctrl+L | アドレス欄へ（パスを打って Enter で移動） |
| Alt+← ／ Alt+→ ／ Alt+↑ | 戻る ／ 進む ／ 上へ |
| マウスのサイドボタン | 戻る ／ 進む |

表示と並び替えの覚え書きは `%APPDATA%\ExplorerTabs\views.txt` に保存されます（エクスプローラー本体の設定には書きません）。要らなくなったらこのファイルを消せば元に戻ります。

## 動く環境

- Windows 10 / 11（64bit）
- .NET Framework 4.x（Windows 10 / 11 には最初から入っています）
- 確かめた環境：Windows 11 Pro（ビルド 26200）

## ビルドの仕方

Visual Studio は要りません。Windows に入っている C# コンパイラ（csc.exe）でビルドします。

1. このフォルダで `build.bat` を実行する
2. `bin\ExplorerTabs.exe` ができる

中でやっていること：

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:x64 /optimize+ /out:bin\ExplorerTabs.exe src\ExplorerTabs.cs
```

## ファイル

```
src/ExplorerTabs.cs   コード（1ファイル）
build.bat             ビルド
README.md             この説明
LICENSE               ライセンス（MIT）
.gitattributes        行末の決まり（.bat は CRLF のまま）
```

## ライセンス

MIT License（[LICENSE](LICENSE) を見てください）

作者：wellod789
