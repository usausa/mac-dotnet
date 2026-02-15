# WorkWifi - macOS Wi-Fi アクセスポイント スキャナー

macOS の CoreWLAN フレームワークを Objective-C ランタイム P/Invoke で直接操作し、周囲の Wi-Fi アクセスポイントの詳細情報を取得するライブラリです。

Windows 向けの [ManagedNativeWifi](https://github.com/emoacht/ManagedNativeWifi) と同等のアクセスポイント情報取得機能を macOS ネイティブで実現します。

## 取得できる情報

| 項目 | 説明 |
|------|------|
| SSID | ネットワーク名 (非公開ネットワークの場合は null) |
| BSSID | アクセスポイントの MAC アドレス |
| RSSI | 受信信号強度 (dBm) |
| ノイズフロア | 背景ノイズレベル (dBm) |
| SNR | 信号対雑音比 (dB) — RSSI - ノイズフロアで算出 |
| 信号品質 | 信号強度を 0-100% にマッピングした値 |
| チャンネル番号 | 使用中のチャンネル |
| 周波数帯域 | 2.4 GHz / 5 GHz / 6 GHz (Wi-Fi 6E) |
| チャンネル帯域幅 | 20 / 40 / 80 / 160 MHz |
| セキュリティ | サポートされるプロトコル一覧 (WPA2, WPA3, Enterprise 等) |
| ビーコン間隔 | ビーコンフレームの送信間隔 (ms) |
| 国コード | ISO 国コード (無線規制の準拠国) |
| IBSS | アドホックネットワークかどうか |
| IE データサイズ | 情報要素 (Information Element) データの長さ |

## 実装方式

- **Objective-C ランタイム P/Invoke**: `objc_msgSend` 経由で CoreWLAN の Objective-C クラスを直接操作
- **unsafe コード**: `NSError**` ポインタの受け渡しに使用
- **セレクタキャッシュ**: `sel_registerName` の結果を静的フィールドにキャッシュして効率化

## 権限について

### macOS 14 (Sonoma) 以降

Wi-Fi スキャン結果の取得には**位置情報サービス**の許可が必要です。

- **ターミナルから実行する場合**: システム環境設定 → プライバシーとセキュリティ → 位置情報サービス で、ターミナルアプリに位置情報のアクセスを許可してください。
- **管理者権限**: `sudo` で実行すると位置情報の制限を回避できる場合があります。
- **BSSID の取得**: BSSID の取得には位置情報の許可が特に重要です。許可されていない場合、BSSID が null になることがあります。

### App Sandbox 環境

App Sandbox 環境では CoreWLAN の機能が制限される場合があります。サンドボックス化されたアプリでは `com.apple.security.device.wifi` エンタイトルメントが必要になることがあります。

## 使い方

```bash
dotnet run --project Sandbox/WorkWifi
```

## 構成

| ファイル | 説明 |
|----------|------|
| `NativeMethods.cs` | Objective-C ランタイム P/Invoke 定義、CoreWLAN 列挙定数 |
| `WifiAccessPointInfo.cs` | データモデル (record 型) |
| `WifiInfoProvider.cs` | CoreWLAN を介したスキャン処理の実装 |
| `Program.cs` | コンソール出力による検証用エントリポイント |
