# Retainer Price Adjuster

FFXIVのリテイナー出品価格を自動調整するDalamudプラグイン

## 機能

- 呼び鈴に近づくと自動的にインタラクト
- リテイナーを順番に選択
- 出品リストを自動取得
- MarketBuddyと連携して最安値を取得
- 最安値-1ギルに価格を自動設定
- 全リテイナーを自動処理

## ビルド方法

### 前提条件

- .NET 10.0 SDK
- Dalamud開発環境

### セットアップ

1. リポジトリをクローン:
```bash
git clone <repository-url>
cd RetainerPriceAdjuster
```

2. サブモジュールを初期化:
```bash
git submodule update --init --recursive
```

3. ビルド:
```bash
dotnet build
```

### 別のPCでのセットアップ

1. リポジトリをクローン後、必ずサブモジュールを初期化してください:
```bash
git clone <repository-url>
cd RetainerPriceAdjuster
git submodule update --init --recursive
```

これでECommonsライブラリが自動的にダウンロードされます。

## 依存関係

- [ECommons](https://github.com/NightmareXIV/ECommons) - Gitサブモジュールとして含まれています
- Dalamud
- FFXIVClientStructs

## 使い方

1. プラグインを有効化
2. チェックボックスをONにすると自動処理が開始されます
3. 処理中はログウィンドウで進行状況を確認できます
