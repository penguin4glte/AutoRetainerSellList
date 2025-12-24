# Implementation Guide: Completing the ItemSearchResult Parser

このガイドでは、RetainerPriceAdjusterプラグインを完成させるために必要な、ItemSearchResultアドオンの解析方法を説明します。

## 📋 目次

1. [必要なツール](#必要なツール)
2. [ItemSearchResultアドオンの調査手順](#itemsearchresultアドオンの調査手順)
3. [ノードインデックスの特定](#ノードインデックスの特定)
4. [コードの更新](#コードの更新)
5. [テストとデバッグ](#テストとデバッグ)

## 🛠️ 必要なツール

### 1. Dalamud Inspector
Dalamudの組み込みアドオン調査ツールです。

**有効化方法：**
```
/xldev
```
開発者メニューから "Addon Inspector" を選択

### 2. SimpleTweaks (オプション)
より高度なアドオン調査機能を提供します。

**インストール：**
- Dalamudプラグインインストーラーから "SimpleTweaks" を検索してインストール
- 設定から "Addon Inspector" を有効化

## 🔍 ItemSearchResultアドオンの調査手順

### ステップ1: マーケットボードを開く

1. ゲーム内でマーケットボードに移動
2. 任意のアイテムを検索（例：ポーション、素材など）
3. 検索結果が表示されることを確認

### ステップ2: Dalamud Inspectorを起動

```
/xldev
```

"Addon Inspector" を選択し、"ItemSearchResult" アドオンを探します。

### ステップ3: アドオン構造の確認

ItemSearchResultアドオンを選択すると、以下のような構造が表示されます：

```
ItemSearchResult
├── Node[0] - Background
├── Node[1] - Title bar
├── Node[2] - Close button
├── Node[?] - List component (これを見つける必要があります)
│   ├── ListItem[0]
│   │   ├── Price text node
│   │   ├── Quantity text node
│   │   ├── HQ icon node
│   │   └── Seller name node
│   ├── ListItem[1]
│   └── ...
└── ...
```

### ステップ4: デバッグログを使用して構造を出力

プラグインにはデバッグ機能が組み込まれています：

1. プラグイン設定で "Enable debug logging" をONにする
2. 価格調整を実行すると、ログに以下が出力されます：
   ```
   === Addon Structure: ItemSearchResult ===
   Node count: XX
   [0] Type: ..., Visible: ..., Pos: (...), Size: (...)
   [1] Type: ..., Visible: ..., Pos: (...), Size: (...)
   ...
   ```

3. `/xllog` コマンドでログファイルを開き、構造を確認

## 🎯 ノードインデックスの特定

### 探すべき情報

各マーケットボードリストエントリについて、以下の情報を見つける必要があります：

| 情報 | 型 | 説明 |
|------|------|------|
| **List Component Index** | Component | 出品リスト全体を含むコンポーネント |
| **Price Node** | Text | 価格を表示するテキストノード |
| **Quantity Node** | Text | 数量を表示するテキストノード |
| **HQ Icon Node** | Image | HQアイコン（表示=HQ、非表示=NQ） |
| **Seller Name Node** | Text | 出品者名を表示するテキストノード |

### 手順

1. **リストコンポーネントを探す**
   - Type が `Component` または `ComponentNode`
   - 子ノードが複数ある（出品数分）
   - 通常、UldManager.NodeListの中盤～後半に位置

2. **各エントリーのノードを探す**
   - リストコンポーネントの子ノード（ItemRenderer）を調査
   - 各ItemRendererの中にある個別のノードを確認
   - Textノードで価格らしき数字を表示しているものを探す

3. **HQアイコンを探す**
   - Imageノードで、HQ品の場合のみ表示されているものを探す
   - アイコンのテクスチャIDも確認（通常、HQアイコンは特定のID）

### 実際の調査例

```
ItemSearchResult -> UldManager.NodeList
  [15] ComponentNode (リストコンポーネントの可能性)
    -> Component -> UldManager.NodeList
      [0] ItemRenderer (1つ目の出品)
        [3] TextNode: "50,000" (価格の可能性)
        [5] TextNode: "99" (数量の可能性)
        [7] ImageNode: Visible=true (HQアイコンの可能性)
        [9] TextNode: "Player Name" (出品者名の可能性)
      [1] ItemRenderer (2つ目の出品)
        ...
```

この場合、以下のように更新します：
- `LIST_COMPONENT_INDEX = 15`
- `PRICE_NODE_INDEX = 3`
- `QUANTITY_NODE_INDEX = 5`
- `HQ_ICON_NODE_INDEX = 7`
- `SELLER_NODE_INDEX = 9`

## 📝 コードの更新

### ファイル: `Services/ItemSearchResultParser.cs`

特定したインデックスで、以下の定数を更新します：

```csharp
// 行 99 付近
const int LIST_COMPONENT_INDEX = 999; // ← 実際の値に変更

// 行 143 付近
const int PRICE_NODE_INDEX = 999;      // ← 実際の値に変更
const int QUANTITY_NODE_INDEX = 999;   // ← 実際の値に変更
const int HQ_ICON_NODE_INDEX = 999;    // ← 実際の値に変更
const int SELLER_NODE_INDEX = 999;     // ← 実際の値に変更
```

### 確認ポイント

1. **価格のパース**
   - カンマが含まれている場合は除去: `Replace(",", "")`
   - 正しくuintにパースできるか確認

2. **HQフラグ**
   - HQ品の場合、アイコンが **表示** されている
   - NQ品の場合、アイコンが **非表示** または存在しない

3. **エラーハンドリング**
   - ノードが存在しない場合のnullチェック
   - パースに失敗した場合のフォールバック

## 🧪 テストとデバッグ

### テスト手順

1. **デバッグログを有効化**
   ```
   Settings -> Enable debug logging: ON
   ```

2. **テストアイテムを準備**
   - HQ品とNQ品の両方が出品されているアイテムを選ぶ
   - 価格帯が異なる複数の出品があると良い

3. **プラグインを実行**
   - リテイナー呼び鈴の近くに移動
   - チェックボックスをONにして価格調整を開始

4. **ログを確認**
   ```
   /xllog
   ```
   以下の情報を確認：
   - アドオン構造が正しく出力されているか
   - パースした出品数は正しいか
   - 価格、数量、HQフラグが正しく読み取れているか

### デバッグのヒント

#### ログに何も出力されない
- `plugin.Configuration.EnableDebugLogging` がtrueか確認
- ItemSearchResultアドオンが実際に開いているか確認

#### パースした出品数が0
- `LIST_COMPONENT_INDEX` が間違っている可能性
- アドオン構造のログ出力を再確認

#### 価格が正しく読み取れない
- `PRICE_NODE_INDEX` が間違っている
- 価格テキストのフォーマットを確認（カンマ、小数点など）

#### HQ/NQの判定が逆
- `HQ_ICON_NODE_INDEX` が間違っている
- `IsVisible()` の判定ロジックを確認

### トラブルシューティング

| 問題 | 原因 | 解決策 |
|------|------|--------|
| アドオンが開かない | AgentId が間違っている | AutoRetainerのコードを参考に正しいAgentIdを確認 |
| リストが空 | ノードインデックスが間違っている | Dalamud Inspectorで再度確認 |
| 価格が0 | テキストのパースに失敗 | ログでテキスト内容を確認、パースロジックを調整 |
| クラッシュ | nullチェック不足 | try-catchとnullチェックを追加 |

## 🎓 参考リソース

### 類似実装の参考

1. **AutoRetainer**
   - リテイナーUI操作の実装
   - 場所: `AutoRetainer/Scheduler/Handlers/RetainerHandlers.cs`

2. **MarketBuddy** (他のマーケットボードプラグイン)
   - マーケットボード関連の実装例

3. **Dalamud Sample Plugin**
   - 基本的なアドオン操作の例
   - https://github.com/goatcorp/SamplePlugin

### FFXIVClientStructs ドキュメント

```csharp
// よく使う型
AtkUnitBase*          // アドオンのベース
AtkComponentList*     // リストコンポーネント
AtkComponentNode*     // コンポーネントノード
AtkTextNode*          // テキストノード
AtkImageNode*         // 画像ノード
AtkResNode*           // 基本ノード

// よく使うメソッド
node->IsVisible()           // ノードが表示されているか
node->GetAsAtkTextNode()    // テキストノードにキャスト
textNode->NodeText.ToString() // テキスト内容を取得
```

## ✅ 完成の確認

以下がすべてtrueになれば完成です：

- [ ] ItemSearchResultアドオンが自動的に開く
- [ ] 検索結果から価格情報を正しく読み取れる
- [ ] HQ/NQの区別が正しくできる
- [ ] 最安値が正しく取得できる
- [ ] アドオンが正しく閉じられる
- [ ] エラーが発生しない

## 📞 サポート

質問や問題がある場合：
1. プラグインのGitHubリポジトリでIssueを作成
2. Dalamud Discordで質問
3. FFXIVModding Discordで相談

---

**注意**: アドオン構造はゲームのパッチで変更される可能性があります。大型パッチ後は再度検証が必要になる場合があります。
