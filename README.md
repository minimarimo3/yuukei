# 有景（yuukei）

## 概要

これはデスクトップマスコットです。
カゲロウデイズプロジェクトのエネやシュタインズ・ゲートゼロのAmadeusシステムのように、デバイスの中でキャラクターが生活できるようにします。

## 開発者用

### ビルド

Yuukeiがエクスプローラー内のアイテムの情報を取得するために使用するexeをビルドする必要があります。
`cd ExplorerItemScanner && dotnet publish -c Release`
これで`Yuukei/Assets/StreamingAssets/ExplorerItemScanner/ExplorerItemScanner.exe`にexeが配置されます。

### 開発の初期設定

これは開発中に必要な情報というわけではなく、プロジェクトを立ち上げたときにいろいろ困ったのでその対策をメモしているものです。
開発する際にこの情報は必要ありません。

- ウィンドウの透過とクリック判定には[UniWindowController](https://github.com/kirurobo/UniWindowController/tree/main)を使用しています。
プロジェクトはURPです。
UniWindowControllerの初期設定はPlayer Settings validationとURP Settings validationに表示されている内容にしたがって処理することで設定しました。
詳しくは[公式ドキュメントに記載されている利用方法](https://github.com/kirurobo/UniWindowController/blob/main/README-ja.md#unity-%E3%83%97%E3%83%AD%E3%82%B8%E3%82%A7%E3%82%AF%E3%83%88%E3%81%A7%E3%81%AE%E5%88%A9%E7%94%A8)を参照してください。

- キャラクターの表示には[UniVRM](https://github.com/vrm-c/UniVRM)を使用しています。
VRM 1.0の方をインストールしてください。26/03/02では[v0.131.0](https://github.com/vrm-c/UniVRM/releases/tag/v0.131.0)を使用しています。

- [DSL（台本）](https://github.com/minimarimo3/daihon)のパースには[ANTLR4](https://github.com/antlr/antlr4)を、非同期処理には[UniTask](https://github.com/Cysharp/UniTask)を使用しています。
UniTaskを利用するために[UniTaskのリリースページ](https://github.com/Cysharp/UniTask/releases)をインストールしてください。
また、[台本のリリースページ](https://github.com/minimarimo3/daihon/releases)にDaihon.dllがあります。
これと[Antlr4.Runtime.Standard](https://www.nuget.org/packages/Antlr4.Runtime.Standard/4.13.2#supportedframeworks-body-tab)からダウンロードしたnugetをzipで回答してnetstandard2.0にあるDLLを`Assets/Plugins/Daihon`に配置してください。

- WindowsでExplorerの要素を検出するために[ExplorerScanner](https://github.com/minimarimo3/ExplorerScanner)を使用しています。
ExplorerScannerの[リリースページ](https://github.com/minimarimo3/ExplorerScanner/releases)にExplorerScanner.dllがあります。
これを`Assets/Plugins`に配置してください。
