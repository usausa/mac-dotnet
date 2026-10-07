# 🛠️ MacDotNet.SystemInfo ハンドル保持化 作業指示書

- 対象リポジトリ: `lib-MacDotNet`（`MacDotNet.SystemInfo`）
- 実行環境: **Apple Silicon Mac**（Intel Mac は対象外）。バッテリー関係の確認には MacBook が必要
- 作成日: 2026-10-06
- 関連文書: `lib-LinuxDotNet/Document/SystemInfo-HandleReuse.md`（Linux 版）

> 📝 **進め方**
> この文書はチェックリスト形式です。作業が終わった項目は `- [ ]` を `- [x]` に更新してください。
> 結果は §📝「結果記録」に記入してください。
> 判断に迷う点や、本書と実際の挙動が食い違う点が見つかった場合は、作業を止めてユーザーに確認してください。

---

## 📌 1. 目的と背景

今の `Update()` は呼ばれるたびに次のような処理を行っています。

- `IOServiceGetMatchingService` で IOKit のサービスを検索する
- `IOServiceOpen` / `IOServiceClose` で接続を開閉する（`SmcMonitor`）
- IOReport の channels と subscription を作り直す（`CpuFrequency`、`PowerStat`）
- 辞書のキーにする `CFString` を毎回生成して解放する

利用側（`Service-PrometheusExporter`）は、オブジェクトを一度だけ作ってスクレイプのたびに `Update()` を呼ぶ使い方をしています。

そこで次のように変えて **`Update()` の実行コストを下げる** のが目的です。

- これらを **作成時に一度だけ用意して保持** する
- 各クラスを **`IDisposable`** にする

---

## 🧭 2. 決定済みの方針

ユーザーとの合意事項です。変更しないでください。

- [x] 内容を理解した

| # | 方針 |
|---|---|
| D1 | **破壊的変更は許容** する |
| D2 | **非スレッドセーフ前提** でよい |
| D3 | 目的は **実行コストの低減**。開き直さずに使い回せるもの（接続、サービス、subscription、キー文字列など）は保持して再利用する方式を採用し、実装を統一する |
| D4 | **`Update()` を持つクラスはすべて `IDisposable`** にする。保持するハンドルが実際にはないクラスも、作りを揃えるために `IDisposable`（Dispose は実質何もしない）にする |
| D5 | `PlatformProvider` をファサードとする。コンストラクタは公開せず、**各クラスに `internal static Create(...)` ファクトリを用意** する。オープン失敗時の扱いはファクトリとクラス内部で完結させ、Provider から先（利用側）では意識しなくてよい作りにする |
| D6 | ホットプラグやスリープ復帰で無効になり得る処理では、**失敗したら一度だけ開き直す** |
| D7 | 作業はユーザーが別途指示する Mac 実機で行う（ログインして検証する。またはユーザーが本書のコマンドを実行する） |
| D8 | **Intel Mac は対象外**（検証しない。ただし既存の `Supported` 判定などの分岐は壊さない） |

### コーディング規約（`AGENTS.md`）

- メンバ変数に `_` プレフィックスを付けない
- **ビルド警告ゼロ**（net8.0 と net10.0 の両方）
- 警告を抑制する必要が出た場合は、**適用する前にユーザーに確認** する
- 既存ファイルの改行コードは変えない。新規テキストファイルは **CRLF** にする

### Git の運用

- [x] 作業用ブランチ `feature/systeminfo-handle-reuse` を作成した
- コミットは Phase ごとに行う。**push とバージョン番号の変更はユーザーの指示があるまで行わない**

---

## 🎯 3. 対象クラスと方式

方式の凡例:

- **Hold**: ネイティブのリソースを保持する
- **Cache**: リソースではないが、解決済みの値（CFString キー、sysctl の MIB）を保持する
- **None**: 保持するものがない（Dispose は実質何もしない）

| 優先 | クラス | 今の Update の動き | 方式 | 再オープン(D6) | 備考 |
|---|---|---|---|---|---|
| ◎ | `SmcMonitor` | 毎回 `IOServiceGetMatchingService` → `IOServiceOpen` → `IOServiceClose` | Hold（`io_connect_t`） | 対象（スリープ復帰） | 今は `public` コンストラクタ。`Create()` に変える |
| ◎ | `CpuFrequency` | 毎回 channels の取得とコピー、`IOReportCreateSubscription` | Hold（IOReport） | 対象 | §4.3 の共通ヘルパーを使う |
| ◎ | `PowerStat` | 同上（Energy Model） | Hold（IOReport） | 対象 | 同上 |
| ○ | `BatteryDevice` | 毎回サービスを検索し、キー文字列を約14個生成 | Hold（`io_service_t`）＋ Cache（キー） | 対象 | |
| ○ | `GpuDevice` | 毎回 IOAccelerator を全件たどって ID で探す | Hold（`io_registry_entry_t`）＋ Cache（キー） | 対象 | 失敗したら ID で探し直す（§4.4） |
| △ | `CpuStat` / `MemoryStat` | 毎回 `mach_host_self()` を取得して解放 | Hold（host port） | ― | **M0-4 で効果があった場合だけ** 採用する。なければ None |
| △ | `FileHandleStat` / `SwapUsage` / `Uptime` | `sysctlbyname` | Cache（MIB） | ― | **M0-4 で効果があった場合だけ** 採用する |
| △ | `MainsDevice` / `PowerManagementStat` / `DiskStat` | IOPS / IOPM の API、IOMedia の列挙 | Cache（キー）／ None | ― | `DiskStat` はディスクの追加・削除を検知するために列挙を続ける |
| ― | `LoadAverage` / `FileSystemStat` / `NetworkStat` / `ProcessSummary` | getloadavg / getfsstat / sysctl / proc_* | None | ― | |
| ― | `HardwareInfo` / `KernelInfo` / `ProcessInfo` | ― | 対象外 | ― | `Update()` がないスナップショット型。**ファクトリ化（D5）だけ** 行う |

> ℹ️ `lib-RaspberryDotNet` は既に `IDisposable` でハンドルを保持する実装なので、今回は変更しません。

---

## 🏗️ 4. 設計仕様

### 4.1 クラスの基本形（D4/D5）

```csharp
public sealed class SmcMonitor : IDisposable
{
    private readonly SmcConnection connection;

    private bool disposed;

    // ... プロパティは既存どおり

    private SmcMonitor(SmcConnection connection, ...)
    {
        this.connection = connection;
    }

    internal static SmcMonitor Create()
    {
        var connection = new SmcConnection();
        connection.Open();               // 失敗しても例外にしない
        // キーの列挙（今のコンストラクタの処理）を connection を使って行う
        return new SmcMonitor(connection, ...);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        connection.Dispose();
    }

    public bool Update()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // ...
    }
}
```

規則:

- [ ] コンストラクタは `private` にする。`PlatformProvider.GetXxx()` からは `Xxx.Create()` を呼ぶ
- [ ] **ファクトリはオープン失敗で例外を投げない**。常に null でないインスタンスを返し、失敗は `Update()` の戻り値 `false`（または既存の `Supported`）で表す
- [ ] `Dispose()` は何度呼んでもよい（冪等）。Dispose 後に `Update()` を呼んだら `ObjectDisposedException` を投げる
- [ ] 保持するネイティブリソースは **SafeHandle の派生クラス** で持つ（§4.2）。これで Dispose 漏れはファイナライザが回収する。クラス自身にはファイナライザを実装しない
- [ ] 保持するリソースがないクラスも同じ形にする。`Dispose()` では `disposed = true` だけを行う
- [ ] スナップショット型は `IDisposable` にしない。ファクトリ化だけ行う

### 4.2 SafeHandle 型（`Handles.cs` に追加）

今の `IOObj`、`CFRef`、`MachPortRef` などは `ref struct` なので、フィールドとして持てません。一時的な用途にはこれまでどおり使い、**保持するもの** には次の SafeHandle 派生クラスを使います。

```csharp
internal sealed class SafeIOObjectHandle : SafeHandle
{
    public SafeIOObjectHandle(uint value)
        : base(IntPtr.Zero, true)
    {
        SetHandle((IntPtr)value);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    public uint Value => (uint)handle;

    protected override bool ReleaseHandle() => IOObjectRelease((uint)handle) == KERN_SUCCESS;
}
```

| 型 | 解放処理 | 用途 |
|---|---|---|
| `SafeIOObjectHandle` | `IOObjectRelease` | `io_service_t` / `io_registry_entry_t`（Battery、Gpu） |
| `SafeIOConnectHandle` | `IOServiceClose` | `io_connect_t`（SMC） |
| `SafeCFTypeHandle` | `CFRelease` | IOReport の channels / subscription |
| `SafeMachPortHandle` | `mach_port_deallocate(mach_task_self(), port)` | host port（△。採用した場合だけ） |

> ⚠️ 保持中のハンドルを既存の `ref struct` ラッパー（`IOObj` など）に渡してプロパティを読むときは、**`using` を付けずに** `new IOObj(safe.Value)` として使ってください。`using` を付けると Dispose で解放されてしまい、二重解放になります。

### 4.3 IOReport 共通ヘルパー `IOReportSampler`（新規、`internal sealed`、`IDisposable`）

`CpuFrequency` と `PowerStat` から共通で使います。

```csharp
internal sealed class IOReportSampler : IDisposable
{
    // group / subGroup を保持する
    // channels (SafeCFTypeHandle, mutable copy) と subscription (SafeCFTypeHandle) を保持する
    public static IOReportSampler Create(string group, string? subGroup);  // 失敗しても例外にしない (IsOpen = false)
    public bool IsOpen { get; }
    public IntPtr Channels { get; }   // CreateSamples に渡すもの (今の実装と同じものを渡す)

    // 呼び出し側が CFRelease する (using var sample = new CFRef(...))
    // 失敗 (0 が返る) したら channels と subscription を作り直して一度だけ再試行する (D6)
    public IntPtr CreateSample();

    // 作り直しが起きたかどうか (呼び出し側が差分計算の基準をリセットするため)
    public bool Reopened { get; }
}
```

- `IOReportCopyChannelsInGroup` などが `EntryPointNotFoundException` を投げる環境では、`IsOpen = false` として扱います（今の `PowerStat` の挙動を引き継ぎます）。
- **`CpuFrequency` は差分計算をしている** ので、`Reopened` が立った回は前回値を基準にしないでください。今の「初回」と同じ扱いにリセットします。
- **M0-2 で必ず確認すること**: subscription を保持したまま取ったサンプルの値（累積カウンタ）が、毎回 subscription を作り直したときの値と **同じ基準（起動からの累積）** であること。基準が違う場合は、`PowerStat` の値の意味が変わってしまうので、ユーザーに確認してください。

### 4.4 個別の仕様

- **SmcMonitor**
  - 新規に `SmcConnection`（internal、`SafeIOConnectHandle` を保持）を作ります。
  - `SmcCall` は kern_return を返すので、それを呼び出し側まで伝えます。
  - 再オープンする条件は **接続レベルのエラー** のときだけです。`MACH_SEND_INVALID_DEST`（0x10000003）、`kIOReturnNotOpen`（0xE00002CD）、`kIOReturnNoDevice`（0xE00002C0）を候補とし、**M0-3 で実際に出たコードで確定** します。
  - 接続レベルのエラーが出たら、閉じて開き直し、センサーのループ全体を **一度だけ** やり直します。
  - キー単位のエラー（センサーが存在しないなど）は、今と同じく値を 0 にするだけで、再オープンはしません。
- **BatteryDevice**: `io_service_t` を保持します。`IORegistryEntryCreateCFProperties` が失敗したら、サービスを検索し直して一度だけ再試行します。
- **GpuDevice**
  - `GetDevices()` で列挙したときのエントリを、解放せずに `SafeIOObjectHandle` に移して保持します。
  - 失敗したら、`IORegistryEntryIDMatching(RegistryEntryId)` と `IOServiceGetMatchingService` で探し直して一度だけ再試行します。`IORegistryEntryIDMatching` の P/Invoke は新たに追加します。
  - `PlatformProvider.GetGpuDevices()` の戻り値は `IReadOnlyList<GpuDevice>` のままです。**利用側が要素をそれぞれ Dispose** します。
- **CFString キーのキャッシュ（Cache）**
  - `Update()` の経路で使う定数キーは、**static readonly で一度だけ生成し、解放しない**（ネイティブの `CFSTR` と同じ扱い）ようにします。
  - 対象: `"IOReportChannels"`、BatteryDevice の各キー、GpuDevice の `PerformanceStatistics` 関連、MainsDevice、PowerManagementStat、DiskStat の Statistics 関連
  - `CFRef` と `IOObj` に、キーを `IntPtr` で受け取るオーバーロードを追加します。
- **sysctl の MIB キャッシュ（△）**
  - `sysctlnametomib` で解決した MIB（`int[]`）をインスタンスで持ち、`sysctl(mib, …)` で読みます。
  - `sysctlnametomib` の P/Invoke は新たに追加します。

---

## 🖥️ 5. 検証環境の準備

- [x] E-1 環境情報を記録した（§📝 結果記録の「環境」表）

```bash
sw_vers && uname -a
```

```bash
sysctl -n machdep.cpu.brand_string hw.perflevel0.logicalcpu hw.perflevel1.logicalcpu hw.memsize
```

```bash
dotnet --info
```

- [x] E-2 .NET SDK 10 と net8.0 ランタイムが入っている
- [x] E-3 リポジトリを clone して、`dotnet build -c Release` が **警告ゼロ** で通ることを確認した（変更前の状態で）
- [x] E-4 作業ディレクトリ `~/handle-reuse/` を作成した
- [x] E-5 MacBook（バッテリーあり）かデスクトップかを記録した（デスクトップの場合、バッテリー関連の項目は N/A とする）

---

## 🔬 Phase 0: 前提確認（PoC）

ライブラリには手を入れません。Phase 1 で作る `__Sandbox/WorkSystemInfoMonitor` に、先に `poc` サブコマンドを作って確認します。

- [x] M0-1 **SMC 接続の使い回し**: `poc smc`
  - 接続を1回だけ開き、温度センサーのキーを数個、1000回読む
  - 「毎回 open/close する場合」と所要時間を比べて表示する
  - 失敗したときは kern_return の値を16進で表示する
- [x] M0-2 **IOReport subscription の使い回し**: `poc ioreport`
  - 「CPU Stats / CPU Core Performance States」と「Energy Model」の subscription を1回だけ作り、1秒間隔で10回サンプリングする
  - 同じ時点で、subscription を毎回作り直した場合の値も取り、**累積値の基準が一致するか** を確認する（§4.3）
  - CPU 周波数の計算結果を、下記の `powermetrics` の出力と比べて、だいたい合っているか確認する

```bash
sudo powermetrics --samplers cpu_power,gpu_power -i 1000 -n 5
```

- [ ] M0-3 **スリープ復帰**
  - `poc smc --loop` と `poc ioreport --loop`（1秒間隔で、エラーコードもログに出す）をバックグラウンドで動かす
  - スリープさせて、手動で復帰させる
  - 復帰後に保持中の接続や subscription がそのまま使えるか、使えない場合は **どのエラーコードが出るか** を記録する

```bash
pmset sleepnow
```

- [x] M0-4 **マイクロ計測**: `poc micro`（Stopwatch で 100,000 回まわした平均）
  - `sysctlbyname("kern.num_files")` と、MIB を使った `sysctl`
  - `mach_host_self()` を取って解放する処理と、保持した port を使う処理（それぞれ `host_statistics64` の呼び出しを含めて比べる）
  - `CFStringCreateWithCString` と `CFRelease` を毎回行う処理と、キャッシュしたキーを使う処理（`CFDictionaryGetValue` を含めて比べる）
- [ ] **Phase 0 の結論** を結果記録に書いた
  - 再オープンの条件にするエラーコード
  - △ 項目（host port、MIB、キーのキャッシュ）を採用するかどうか。目安として、**1回あたり 0.2µs 以上、または 10% 以上** 速くなるなら採用する

---

## 📏 Phase 1: ベースライン計測（変更前）

**ライブラリを変更する前に** 実施します。ここで作るツールは、変更前と変更後のどちらでもコンパイルできるように書きます。
具体的には、オブジェクトは `PlatformProvider.GetXxx()` で取得し、解放は `(x as IDisposable)?.Dispose()` で行います。

### 1-1. ツールの作成

- [x] T-1 `__Sandbox/SandboxMac.slnx` を作成した（リポジトリ直下の `__Sandbox` フォルダ）
- [x] T-2 `__Sandbox/WorkSystemInfoBenchmark`（BenchmarkDotNet、`[MemoryDiagnoser]`、net10.0）を作成した
  - ベンチマークはクラスごとに1つ作り、`Update()` を呼ぶ。対象は次のとおり
    - `CpuStat`、`MemoryStat`、`SwapUsage`、`LoadAverage`、`Uptime`、`FileHandleStat`
    - `DiskStat`、`FileSystemStat`、`NetworkStat`、`ProcessSummary`
    - `CpuFrequency`、`GpuDevices`（全要素）、`PowerStat`、`PowerManagementStat`
    - `BatteryDevice`、`MainsDevice`、`SmcMonitor`
    - `All`（上記すべて）
- [x] T-3 `__Sandbox/WorkSystemInfoMonitor`（コンソール、net10.0 と net8.0）を作成した。コマンドは次の4つ
  - `poc ...`: Phase 0 の処理
  - `dump`: 全オブジェクトを作成して1回 Update し、全プロパティを `名前=値` の形で出力する
  - `loop --iterations N --interval MS [--log file.csv] [--verbose]`
    - 1回ごとに次の値を CSV に出す: Update の合計所要時間、割り当てバイト数の差分、fd 数（`/dev/fd` のエントリ数）、`Update()` が false を返した数
    - 可能なら Mach port 数（`mach_port_names` の件数）も出す
  - 終了時に Dispose して、fd 数と port 数が開始時に戻ったかを表示する

### 1-2. 変更前バイナリの保存とベンチマーク

```bash
dotnet publish __Sandbox/WorkSystemInfoMonitor -c Release -f net10.0 -o ~/handle-reuse/before/monitor
```

```bash
dotnet ~/handle-reuse/before/monitor/WorkSystemInfoMonitor.dll dump > ~/handle-reuse/dump-before.txt
```

```bash
dotnet run -c Release --project __Sandbox/WorkSystemInfoBenchmark -- --filter '*' --exporters github
```

- [x] B-1 変更前のモニターを publish し、`dump-before.txt` を保存した
- [x] B-2 ベンチマークの結果を `Document/HandleReuse/results/benchmark-before.md` にコピーした
- [x] B-3 リソース数の基準値を記録した（`loop --iterations 100 --interval 0` を実行中に次のコマンドで確認する）

```bash
lsof -p <pid> | wc -l
```

```bash
sudo lsmp -p <pid> | wc -l
```

```bash
ioreg -r -c AppleSMCClient | grep -c AppleSMCClient
```

- [x] B-4 ここまでをコミットした（ツールだけで、ライブラリは未変更）

---

## 🔧 Phase 2: 実装

上から順に進めます。各ステップで `dotnet build -c Release` の **警告ゼロ** を確認してください。

### 2-1. 基盤

- [x] I-1 `Handles.cs` に SafeHandle 型を追加した（§4.2）
- [x] I-2 `IOReportSampler` を追加した（§4.3）
- [x] I-3 CFString キーのキャッシュ方式を追加した（`IntPtr` キーのオーバーロード）
- [x] I-4 `NativeMethods.cs` に `IORegistryEntryIDMatching` を追加した（`sysctlnametomib` は M0-4 で採用した場合だけ）

### 2-2. ◎

- [ ] I-5 `SmcMonitor` と `SmcConnection`（M0-3 で確定したエラーコードで再オープンする）
- [x] I-6 `CpuFrequency`（`IOReportSampler` を使う。再オープンしたら差分の基準をリセットする）
- [x] I-7 `PowerStat`（`IOReportSampler` を使う。`Supported` の判定は維持する）

### 2-3. ○

- [x] I-8 `BatteryDevice`
- [x] I-9 `GpuDevice`（`internal static GetDevices()` は維持し、各要素を IDisposable にする）

### 2-4. △ / None

- [x] I-10 `CpuStat`、`MemoryStat`（M0-4 の結論に従い、Hold か None にする）
- [x] I-11 `FileHandleStat`、`SwapUsage`、`Uptime`（M0-4 の結論に従い、Cache か None にする）
- [x] I-12 `MainsDevice`、`PowerManagementStat`、`DiskStat`（キーのキャッシュと、None の IDisposable 化）
- [x] I-13 `LoadAverage`、`FileSystemStat`、`NetworkStat`、`ProcessSummary`（None の IDisposable 化）
- [x] I-14 スナップショット型のファクトリ化: `HardwareInfo`、`KernelInfo`（`internal static Create()`）。`ProcessInfo` は既存の static メソッドのままでよい
- [x] I-15 `PlatformProvider` を全部ファクトリ呼び出しに変えた
- [x] I-16 `Update()` の経路に、毎回のサービス検索、`IOServiceOpen`、subscription の生成、キー用の `CFRef.CreateString` が残っていないことを grep で確認した

```bash
grep -n -E "IOServiceGetMatchingService|IOServiceOpen|IOReportCreateSubscription|CFRef.CreateString|CFStringCreateWithCString" MacDotNet.SystemInfo/*.cs
```

### 2-5. サンプルとドキュメント

- [x] I-17 `Example.SystemInfo.ConsoleApp`（`Commands.cs`、`SystemMonitor.cs`）を Dispose する形に直した
- [x] I-18 `README.md` に使用例があれば、Dispose する形に直した
- [x] I-19 ソリューション全体のビルド（net8.0 と net10.0）が警告ゼロで通った
- [x] I-20 コミットした

---

## ✅ Phase 3: 正しさの検証

```bash
dotnet publish __Sandbox/WorkSystemInfoMonitor -c Release -f net10.0 -o ~/handle-reuse/after/monitor
```

```bash
dotnet ~/handle-reuse/before/monitor/WorkSystemInfoMonitor.dll dump > ~/handle-reuse/dump-before2.txt && dotnet ~/handle-reuse/after/monitor/WorkSystemInfoMonitor.dll dump > ~/handle-reuse/dump-after.txt
```

```bash
diff ~/handle-reuse/dump-before2.txt ~/handle-reuse/dump-after.txt
```

- [x] C-1 `dump` の diff を確認した
  - 静的な値（センサーの一覧と件数、GPU 名、バッテリーの設計容量など）は **完全に一致** すること
  - 変動する値は妥当な範囲であること
- [x] C-2 OS のツールの値と突き合わせた
  - `vm_stat`、`sysctl vm.swapusage`、`pmset -g batt`
  - `ioreg -rn AppleSmartBattery`
  - `sudo powermetrics --samplers cpu_power,gpu_power -i 1000 -n 3`
- [x] C-3 `loop --iterations 10 --interval 1000 --verbose` で、`CpuFrequency`、`PowerStat`、`SmcMonitor`、`GpuDevice` の値が更新され続けることを確認した
- [x] C-4 Dispose の挙動を確認した（2回呼んでも例外にならない。Dispose 後の Update で `ObjectDisposedException` になる。終了時に fd 数と port 数が開始時に戻る）
- [x] C-5 net8.0 でも C-1 と C-3 を実施した

---

## 📊 Phase 4: 性能比較（変更後）

- [x] P-1 Phase 1 と同じ条件でベンチマークを実行し、`Document/HandleReuse/results/benchmark-after.md` に保存した
- [x] P-2 結果記録の「性能比較」表に記入した
- [x] P-3 判定基準（下記）を確認し、満たさない項目があれば原因を調べて記録した

| 判定基準 | 内容 |
|---|---|
| 悪化なし | 全クラスで、after の Mean が before の **+5% 以内** |
| ◎クラス | `SmcMonitor`、`CpuFrequency`、`PowerStat` で、Mean **30% 以上改善** を目標とする（未達でも悪化がなければ採用してよい。数値を記録する） |
| 割り当て | Hold / Cache のクラスは、Allocated が before より減っていること |
| リソース | 定常状態で、`AppleSMCClient` の数と Mach port 数が増えていないこと |

---

## 🔌 Phase 5: スリープ・抜き差し・長時間稼働

after のモニターを `loop --interval 1000 --verbose --log` で動かしながら実施します。

- [ ] H-1 **スリープ復帰**（`pmset sleepnow` の後、手動で復帰させる）
  - 復帰後に `SmcMonitor`、`CpuFrequency`、`PowerStat`、`BatteryDevice` の値の取得が再開すること（必要なら再オープンが働くこと）
  - 例外が出ないこと
  - `AppleSMCClient` の数が増え続けないこと
- [ ] H-2 （MacBook のみ）**AC アダプタの抜き差し**で、`MainsDevice` と `BatteryDevice` の値が切り替わる
- [ ] H-3 （任意）**USB ストレージの抜き差し**で、`DiskStat` と `FileSystemStat` にデバイスが追加・削除される（回帰確認）
- [ ] H-4 （任意）**外部ディスプレイの抜き差し** の前後で、`GpuDevice` が例外を出さずに値を取れる
- [ ] H-5 **24時間連続稼働**（アイドルスリープを防ぐため `caffeinate -i` を付ける）

```bash
nohup caffeinate -i dotnet ~/handle-reuse/after/monitor/WorkSystemInfoMonitor.dll loop --iterations 86400 --interval 1000 --log ~/handle-reuse/longrun.csv > ~/handle-reuse/longrun.out 2>&1 &
```

  - fd 数と Mach port 数が一定であること
  - RSS が増え続けないこと（`ps -o rss= -p <pid>` を数回記録する）
  - `Update()` の失敗数が 0 であること

---

## 📦 Phase 6: 利用側の対応（ライブラリのパッケージ公開後、ユーザーの指示があったら実施）

`Service-PrometheusExporter` は NuGet パッケージを参照しています（今は `MacDotNet.SystemInfo` 1.7.0）。新しいバージョンを公開した後に実施します。

- [ ] U-1 `PrometheusExporter.Instrumentation.Mac/MacInstrumentation.cs` を `IDisposable` にした。取得したオブジェクトをすべて保持し、Dispose で解放する（`GetGpuDevices()` の要素と `SmcMonitor` も含める）
- [ ] U-2 Instrumentation の Dispose がホスト終了時に呼ばれることを確認した（`RaspberryInstrumentation` の呼ばれ方に合わせる）
- [ ] U-3 Exporter を実機で動かし、`/metrics` の出力が変更前と同じ系列名・ラベルであることを確認した
- [ ] U-4 ビルド警告ゼロ

---

## 🏁 完了条件

- [ ] Phase 0〜5 のチェックがすべて完了している（任意の項目や N/A の項目は、理由を記録してあればよい）
- [ ] 判定基準を満たさない項目について、原因と対応方針が書かれている
- [ ] ユーザーに結果を報告した（要約: 改善率、リソース数、スリープ復帰の結果、問題点）

---

## ⚠️ 既知の制約・注意事項

- **DiskStat**: ディスクの追加・削除を検知するために、IOMedia の列挙は毎回行います。IOKit の通知を使う方式は今後の課題です。
- **再オープン後の差分**: `CpuFrequency` は、IOReport を作り直した直後の1回だけ、差分が計算できません（初回と同じ扱いになります）。
- **CFString キーのキャッシュ**: プロセスが終わるまで解放しません（`CFSTR` と同じ扱い）。数十個程度の定数に限ります。
- **スレッド安全性**: 非スレッドセーフです（D2）。同じインスタンスに対して `Update()` を並行して呼ばないでください。
- **Intel Mac**: 検証の対象外です（D8）。

---

## 📝 結果記録

### 環境

| 項目 | 値 |
|---|---|
| 機種 / チップ | Mac mini（Mac14,12、ホスト名 macmini.local）/ Apple M2 Pro、メモリ 16 GB |
| P/E コア数 | P コア 6 / E コア 4 |
| macOS | macOS 27.0.1（26A434）、Darwin 27.0.0 arm64 |
| .NET SDK / Runtime | SDK 10.0.401 / Microsoft.NETCore.App 8.0.31 と 10.0.12（`~/handle-reuse/dotnet` に dotnet-install.sh で追加。§判定・メモ） |
| バッテリーの有無 | なし（デスクトップ。AC 電源のみ）。バッテリー関連の項目は N/A |

### Phase 0 の結論

| 項目 | 結果 |
|---|---|
| M0-1 SMC 接続の使い回し（open あり / なしの時間） | 温度キー5個 × 1000 回: 保持 923 µs、毎回開き直し 1,577 µs（41.5% 速い、失敗 0）。ただし今の `SmcMonitor` は 423 キー（T 218、V 52、P 74、I 75、ファン 1）を読み、`Update()` 1 回で 81.2 ms かかるので、減るのは約 0.65 ms（約 0.8%）の見込み |
| M0-2 IOReport の累積値の基準は一致したか | 一致した（判定 SAME）。residency と GPU Energy は、保持と作り直しで同じ基準（差は2つのサンプルの間の約 130 ms 分だけ）。subscription の作り直しには毎回約 120 ms かかる。周波数は powermetrics とだいたい合っている（E クラスタはどちらも約 1 GHz）。CPU Energy は値が止まることがある（§判定・メモ） |
| M0-3 スリープ復帰後の挙動とエラーコード | 保留（ユーザーの指示で後で実施） |
| M0-4 sysctl の MIB / host port / キーのキャッシュの効果 | 3つとも採用の目安を満たす。MIB 1,259→343 ns（73%）、host port 1,384→509 ns（63%）、CFString キー 440→47 ns（89%）。net8.0 でも同じ傾向 |
| 採用方式の決定（△ 項目を含む） | SMC 接続の保持と `IOReportSampler` を採用する。△ の3項目（MIB、host port、キーのキャッシュ）もすべて採用する（2026-10-07 ユーザー確認済み）。再オープンの条件にするエラーコードは、候補の3つ（`0x10000003`、`0xE00002CD`、`0xE00002C0`）で実装を進め、M0-3 の結果で確定する（2026-10-07 ユーザー指示） |

#### Phase 0 の出力

M0-1（`poc smc`、net10.0）:

```text
#KEY=1355, scanned 608 index(es), selected 5 temperature key(s) (limit 5)
benchmark: 1000 iteration(s) x 5 key(s), warm-up 10 iteration(s) per variant
held  : avg 922.763 us/iteration, reads 5000, read failures 0
reopen: avg 1577.247 us/iteration, reads 5000, read failures 0, opens 1000, open failures 0
difference: 654.484 us/iteration saved by the held connection (41.5% faster than reopen)
```

M0-1 の補足（今のライブラリの `SmcMonitor` をそのまま計測）:

```text
create: 300.0 ms, sensors T=218 V=52 P=74 I=75 Fans=1
Update: avg 81.23 ms (20 runs)
```

M0-2（`poc ioreport --count 10`、net10.0。まとめの部分）:

```text
energy  CPU Energy                       samples=10 maxRel=0.000E+00 (worst CPU Energy) diff min=0 max=0 -> same
energy  ANE0                             samples=10 maxRel=0.000E+00 (worst ANE0) diff min=0 max=0 -> same
energy  DRAM0                            samples=10 maxRel=0.000E+00 (worst DRAM0) diff min=0 max=0 -> same
energy  GPU Energy                       samples=10 maxRel=3.777E-05 (worst GPU Energy) diff min=0 max=+847985 -> same
cpu     CPU total residency (all cores)  samples=100 maxRel=1.892E-05 (worst ECPU000) diff min=+1838238 max=+3140879 -> same
verdict: basis looks SAME (SAME = every diff >= 0 and maxRel <= 1%)
```

- 各回の held と fresh の間隔は約 125〜131 ms で、そのうち fresh の subscription の作成が約 115〜122 ms。
- 保持した subscription から計算した周波数（E 約 1,000〜1,250 MHz、P 約 700〜2,850 MHz）は、作り直した側から計算した値とほぼ一致した。

M0-2 の powermetrics との比較（`poc ioreport --loop` を動かしながら `sudo powermetrics --samplers cpu_power,gpu_power -i 1000 -n 5` を実行）:

```text
# poc ioreport --loop（保持した subscription）
2026-10-07T09:08:22.652+09:00 #1 held cpu=OK energy=OK E=n/a P=n/a CPU Energy=3484798 mJ
2026-10-07T09:08:26.923+09:00 #5 held cpu=OK energy=OK E=1190 MHz P=2101 MHz CPU Energy=5654749 mJ (delta +2169951)
2026-10-07T09:08:27.944+09:00 #6 held cpu=OK energy=OK E=998 MHz P=1068 MHz CPU Energy=5654783 mJ (delta +34)
2026-10-07T09:08:28.960+09:00 #7 held cpu=OK energy=OK E=977 MHz P=702 MHz CPU Energy=5655085 mJ (delta +302)
2026-10-07T09:08:29.980+09:00 #8 held cpu=OK energy=OK E=1331 MHz P=1066 MHz CPU Energy=5655140 mJ (delta +55)
2026-10-07T09:08:31.002+09:00 #9 held cpu=OK energy=OK E=1124 MHz P=1071 MHz CPU Energy=5655197 mJ (delta +57)
2026-10-07T09:08:32.022+09:00 #10 held cpu=OK energy=OK E=1189 MHz P=1186 MHz CPU Energy=5655228 mJ (delta +31)
2026-10-07T09:08:43.237+09:00 #21 held cpu=OK energy=OK E=948 MHz P=702 MHz CPU Energy=5656120 mJ (delta +892)
# powermetrics（同じ時間帯）
09:08:27  E-Cluster 971 MHz  P0-Cluster 3480 MHz  P1-Cluster 0 MHz    CPU Power: 41 mW
09:08:28  E-Cluster 974 MHz  P0-Cluster 1521 MHz  P1-Cluster 0 MHz    CPU Power: 291 mW
09:08:29  E-Cluster 965 MHz  P0-Cluster 702 MHz   P1-Cluster 702 MHz  CPU Power: 58 mW
09:08:30  E-Cluster 1056 MHz P0-Cluster 1443 MHz  P1-Cluster 948 MHz  CPU Power: 56 mW
09:08:31  E-Cluster 1052 MHz P0-Cluster 1342 MHz  P1-Cluster 1187 MHz CPU Power: 30 mW
```

- 周波数: E クラスタはどちらも約 1 GHz。P は、こちらの値が P コア 6 個の平均（動いていないコアは最低周波数として数える）で、powermetrics の値がクラスタごとに動いている時間だけの平均なので、定義が少し違う。ただし値の範囲は同じくらい。
- CPU Energy: 表示した行（#1、#5〜#10、#21）以外では、値は変わっていない。

M0-4（`poc micro`、net10.0）:

```text
case                                      per-call ns/op  cached ns/op  diff us/op   faster  threshold
sysctl kern.num_files                             1259.4         343.4       0.916    72.7%  MET
host_statistics64(HOST_VM_INFO64)                 1384.2         509.0       0.875    63.2%  MET
CFDictionaryGetValue(serial number key)            440.4          47.4       0.393    89.2%  MET
```

### 性能比較（Mean / Allocated）

| クラス | before | after | 改善率 |
|---|---|---|---|
| CpuStat | 5.02 µs / 0 B | 4.61 µs / 0 B | −8.2% |
| MemoryStat | 1.98 µs / 0 B | 1.02 µs / 0 B | −48.3% |
| SwapUsage | 618 ns / 0 B | 308 ns / 0 B | −50.1% |
| LoadAverage | 313 ns / 0 B | 313 ns / 0 B | +0.1% |
| Uptime | 674 ns / 0 B | 334 ns / 0 B | −50.5% |
| FileHandleStat | 1.65 µs / 0 B | 757 ns / 0 B | −54.2% |
| DiskStat | 333 µs / 0 B | 326 µs / 0 B | −2.4% |
| FileSystemStat | 8.51 µs / 80 B | 8.49 µs / 80 B | −0.2% |
| NetworkStat | 4.27 ms / 1,168 B | 4.32 ms / 1,168 B | +1.1% |
| ProcessSummary | 568 µs / 0 B | 571 µs / 0 B | +0.4% |
| CpuFrequency | 37.3 ms / 400 B | 1.03 ms / 0 B | −97.2% |
| GpuDevices | 29.1 µs / 0 B | 21.9 µs / 0 B | −24.7% |
| PowerStat | 39.7 ms / 22,304 B | 1.60 ms / 0 B | −96.0% |
| PowerManagementStat | 191 µs / 0 B | 185 µs / 0 B | −3.2% |
| BatteryDevice | 0.68 ns / 0 B（バッテリーなしで即 false。N/A） | 0.39 ns / 0 B（N/A） | N/A |
| MainsDevice | 163 µs / 40 B | 163 µs / 0 B | −0.0%（割り当ては 40 B → 0 B） |
| SmcMonitor | 108.7 ms / 0 B | 107.2 ms / 0 B | −1.3% |
| **All** | **170.2 ms / 23,992 B** | **123.0 ms / 1,248 B** | **−27.7%（割り当ては −94.8%）** |

### リソース数（定常状態）

| 項目 | before | after |
|---|---|---|
| fd 数（lsof） | 76（5 回とも同じ） | 76（4 回とも同じ） |
| Mach port 数（lsmp） | 107〜109 | 111（4 回とも同じ。SMC の接続、GPU のエントリ、host port などを保持する分が増えるが、実行中に増え続けることはない） |
| AppleSMCClient の数 | 7〜8（システム全体の数。変更前は、自プロセスの接続が `Update()` の間だけある） | 8（4 回とも同じ。接続を保持し続けるため） |

B-3 の loop の集計（`loop --iterations 100 --interval 0`、変更前）:

```text
update_us: avg=172467.9 min=160009.5 max=209461.3
alloc_bytes: avg=25352.7 per iteration
false count per target: total=100 （BatteryDevice=100。バッテリーがないため。ほかは 0）
fd: start=49 end=49 returned=yes
ports: start=50 end=48 returned=no
```

- ports が 50→48 と減ったのは、スレッドの増減によるもの（スレッドごとに port がある）。増えたわけではないので、リークではない。

同じ条件での loop の集計（変更後、`after-sudo-checks.sh` の中で実行）:

```text
update_us: avg=108494.2 min=83679.5 max=155428.0
alloc_bytes: avg=2486.4 per iteration
false count per target: total=100 （BatteryDevice=100。ほかは 0）
fd: start=49 end=49 returned=yes
ports: start=50 end=48 returned=no
```

### スリープ・抜き差し・長時間稼働

| 項目 | 結果 | 備考 |
|---|---|---|
| H-1 スリープ復帰 | | |
| H-2 AC アダプタ | | |
| H-3 USB ストレージ | | |
| H-4 外部ディスプレイ | | |
| H-5 24時間（fd / port / RSS / 失敗数） | | |

### 判定・メモ

（基準を満たさなかった項目、気づいた点、今後の課題）

#### 作業前の決定事項（2026-10-06）

- **`GpuDevice.GetDevices()` は I-9 で internal にする**: 今は `public static` になっている。作業ディレクトリ（`D:\GitHubDevice`）のソースを、gitignore されたフォルダも含めてすべて調べた。呼び出し元は `PlatformProvider.GetGpuDevices()`（ライブラリ本体と、そのコピー）だけだった。`PlatformProvider` から呼ぶので private にはできないため、internal にする。
- **`SmcMonitor` のコンストラクタを直接呼ぶコードはない**: 利用側（MacStatDisplay、PrometheusExporter）は、どちらも `PlatformProvider.GetSmcMonitor()` を使っている。
- **ツールはコミットしない**: `__Sandbox` は `.gitignore` の `__*` で除外されている。ツールはコミットせず、B-4 のコミットにはドキュメントの更新だけを入れる。
- **実機には SSH で入る**: 接続先は、必要になったときにユーザーが指示する。
- Linux 版で見つかった既存バグ（string と `ReadOnlySpan<char>` の `==`）と同じパターンは、MacDotNet にはなかった（ビルドした DLL の IL で確認）。

#### Phase 0 で見つかった点（2026-10-07）

- **E-2 と E-3 は、SDK とランタイムを追加して満たした（2026-10-07 ユーザー了承）**
  - 実機にある SDK 10.0.103（`~/.dotnet` と Homebrew）では、main（e836dde）のビルドが失敗した（CS0214 が 136 件）。このリポジトリは SDK 10.0.4xx のコンパイラを前提にしている。
  - Microsoft 公式の `dotnet-install.sh` で、専用ディレクトリ `~/handle-reuse/dotnet` に SDK 10.0.401 とランタイム 8.0.31 を入れた。既存の dotnet（`/usr/local/share/dotnet`、`~/.dotnet`、Homebrew）には触れていない。
  - 実機でのコマンドは `DOTNET_ROOT=$HOME/handle-reuse/dotnet` と `PATH=$HOME/handle-reuse/dotnet:$PATH` を付けて実行する。
  - この SDK で、main がソリューション全体で警告 0、エラー 0 でビルドできた。
- **sudo にはパスワードが必要**: powermetrics と lsmp は、ユーザーに実行してもらう。
- **SmcMonitor の改善は小さい見込み**
  - 接続を保持すると、開き直しの約 0.65 ms が減る。
  - ただし `Update()` は 423 キーを読み（キー1つ約 190 µs）、1回 81.2 ms かかる。改善は約 0.8% にとどまり、◎ の目標「30% 以上」は接続の保持だけでは届かない見込み。
  - 指示書では「未達でも悪化がなければ採用してよい」としているので、そのとおり扱う。大きく減らすには、読むキーを絞る必要がある（今回の範囲外）。
- **CpuFrequency と PowerStat は大きく改善する見込み**: 今の `Update()` は、毎回 subscription を作り直しており、その作成に約 120 ms かかっている。
- **CPU Energy は、powermetrics などが計測している間しか更新されない（今のライブラリでも同じ。後で検討）**
  - Energy Model の `CPU Energy`、`ANE0`、`DRAM0` の値は、ふだんは止まっている（約 3 分の間、3484798 mJ のまま）。`GPU Energy` と CPU の residency は常に変わっている。
  - powermetrics を始めた瞬間に、`CPU Energy` が +2,170 J 増えた（それまでの分がまとめて反映された）。powermetrics が動いている間は毎秒増え、その増え方は powermetrics の CPU Power とほぼ一致した（例: +302 mJ/s と 291 mW）。
  - つまり値そのものは正しいが、更新は何かがきっかけで反映されたときだけになる。subscription を毎回作り直しても同じなので、保持したせいではない。
  - 今の `PowerStat.Cpu`、`Ane`、`Ram` も、ふだんは古い値のままになっている可能性が高い。今回の範囲外として、後で検討する。
- **SMC のキーの読み込みを改善できるかは、後で検討する**（2026-10-07 ユーザー指示）。

#### Phase 2 の実装メモ（2026-10-07）

- **I-5 は候補のエラーコードで実装した**: 再オープンの条件は `0x10000003`、`0xE00002CD`、`0xE00002C0` の3つ（`NativeMethods` の定数に、M0-3 で確定する候補だとコメントしてある）。M0-3 の結果で確定したら、I-5 にチェックを付ける。
- **I-16 の grep の結果**: ヒットは次の場所だけで、ふだんの `Update()` の経路にはない。
  - 作成と再オープンの経路: `SmcConnection.Open`、`IOReportSampler.Open`、`BatteryDevice.FindService`（作成時と、失敗したときの探し直し）、`GpuDevice` の ID での探し直し
  - 作成: `GpuDevice.GetDevices` と、そのコンストラクタ（IOClass の取得）
  - static の `Lazy` の初期化: `CpuFrequency`（周波数テーブル）、`CpuStat`（コアの種類）
  - 新しいエントリを見つけたとき: `FileSystemStat`、`NetworkStat`
  - スナップショット型: `HardwareInfo`
  - 意図的に毎回行うもの: `DiskStat` の IOMedia の列挙（ディスクの追加と削除を検知するため）
  - そのほか: 宣言、コメント、`CFSTR` の定義、`IOObj` の string キー版のオーバーロード
- **指示書にない追加（Phase 4 の「Allocated が before より減る」のため）**
  - `CpuFrequency` と `PowerStat` は、チャネルの位置ごとの対応（コア、エネルギーの種類と単位）をキャッシュする。次のサンプルでは、チャネルの数と、名前とグループの CFString（`CFRetain` で保持）が同じかを `CFEqual` で確かめ、違えば作り直す。ふだんの `Update()` では文字列を作らない。
  - `MainsDevice` は、電源の種類を文字列にせず、`CFEqual` で比べる。
  - `task_self_trap()` は、呼ぶたびに task port のユーザー参照が増えて戻らない。そこで task port の名前は、プロセスで1回だけ取得して `MachTask.Self` に保持し、`CpuStat` の `vm_deallocate`、`SmcConnection` の `IOServiceOpen`、`SafeMachPortHandle` の解放で使うようにした。
    - 最初は `mach_task_self()` に置き換えたが、Phase 3 で、エクスポートされている関数 `mach_task_self()` も内部で `task_self_trap()` を呼んでいて、1回ごとに参照が1つ増えることがわかった（1000 回で 9 → 1009）。
    - `MachTask.Self` にしてからは、`CpuStat.Update()` を 1000 回呼んでも、参照数は 5 のまま変わらない。
- **保持したハンドルは `IOObj` を通さない**: `new IOObj(held.Value)` を `using` なしで作ると CA2000 が出るため、`GpuDevice` は保持しているエントリに対して `IORegistryEntryCreateCFProperty` を直接呼び、戻り値の辞書だけを `using CFRef` で包んでいる。
- **後で検討する課題**
  - `NetworkStat` は `Update()` のたびに SCPreferences を作り直している（`RefreshEnabledState`）。4.27 ms の大半はこれと思われる。指示書では保持の対象外（None）なので、今回は変えていない。
  - README の Process の例にある `summary.OpenFileCount` は、`ProcessSummary` に存在しないプロパティ（以前からの誤り）。

#### Phase 3 の結果（2026-10-07）

- **C-1 / C-5（dump の diff）**: net10.0 と net8.0 のどちらも、変更前と変更後の dump は同じ 2,217 行で、片方にしかない項目はない。
  - 違いは、値が変わって当然の項目だけ（センサーの値、CPU の tick、メモリ・ネットワーク・ディスクのカウンタ、空き容量、時刻、周波数）。
  - 名前、件数、キー、説明、データ型、GPU 名などは完全に一致した。
  - 累積カウンタ 77 個は、すべて「後 ≥ 前」だった（net10.0）。
- **C-2（OS のツールとの突き合わせ）**
  - `vm_stat` とほぼ一致した（差は取った時刻のずれの分）。`vm_stat` の「Pages free」は free から speculative を引いた値なので、`FreeCount` から `SpeculativeCount` を引いて比べると一致する（以前からの定義の違い）。
  - `sysctl vm.swapusage`（swap は 0）、`pmset -g batt`（AC Power）とも一致した。
  - AppleSmartBattery のサービスはあるが、`BatteryInstalled = No` なので `BatteryDevice.Supported=False` で正しい。
  - powermetrics（`after-sudo-checks.sh` で、変更後の loop を動かしながら 3 秒取得）とも、だいたい合っている。
    - E クラスタの周波数は、こちらが 1,078〜1,322 MHz（E コア4つの平均）、powermetrics が 1,013〜1,084 MHz。P もほぼ同じ範囲。
    - `PowerStat.Cpu` は M0-2 と同じ動きだった。止まっていた値が、powermetrics を始めたときにまとめて増え（+383 J）、そのあと1秒ごとの増え方は powermetrics の CPU Power とほぼ一致した（264 mJ と 270 mW、49 mJ と 48 mW）。powermetrics が終わると、また止まった。
    - GPU のエネルギーは毎回増えていて、増え方（約 0.4〜1.8 mW）は powermetrics の GPU Power（0〜1 mW）と合っている。
- **C-3 / C-5（loop --verbose、10 回）**: net10.0 と net8.0 のどちらも、`CpuFrequency`（E コアの周波数）、`SmcMonitor`（温度）、`PowerStat`（GPU のエネルギー）、`GpuDevice` の値が毎回更新された。
  - subscription を作った直後に subscribed channels を解放しても、値は更新される。
  - 1回の割り当ては 3,312 B（変更前の loop は約 25,353 B）。失敗は `BatteryDevice`（バッテリーなし）だけ。
- **C-4（Dispose）**: 17 個のオブジェクトすべてで、`Dispose()` を2回呼んでも例外にならず、Dispose 後の `Update()` は `ObjectDisposedException` になった。
  - loop の終了時、fd 数は開始時と同じ（49 → 49）。
  - port 数は 50 → 49 と1つ減った（スレッドの増減によるもので、増えてはいない）。

#### Phase 4 の判定（2026-10-07）

| 判定基準 | 結果 |
|---|---|
| 悪化なし（+5% 以内） | 満たす。悪化した最大は NetworkStat の +1.1%（ProcessSummary +0.4%、LoadAverage +0.1%）。どれも、保持の対象外（None）のクラスで、誤差の範囲 |
| ◎クラス（30% 以上改善） | CpuFrequency −97.2%、PowerStat −96.0% は満たす。**SmcMonitor は −1.3% で未達** |
| 割り当て（Hold / Cache は減る） | 満たす。CpuFrequency 400 B → 0 B、PowerStat 22,304 B → 0 B、MainsDevice 40 B → 0 B。ほかの Hold / Cache のクラスは変更前から 0 B で、増えていない。All は 23,992 B → 1,248 B（残りは None の FileSystemStat 80 B と NetworkStat 1,168 B） |
| リソース（増えていない） | 満たす。計測中の AppleSMCClient は 8、Mach port は 111 で一定（変更前は 7〜8、107〜109）。保持する分だけ一定量増えるが、増え続けることはない |

- **SmcMonitor が未達の理由**: `Update()` は 423 個のキーを読み、1つの読み込みに約 190 µs かかる（1回で約 107 ms）。接続を保持して減るのは開き直しの約 0.65 ms だけなので、改善は約 1% にとどまる（M0-1 の見込みどおり）。悪化はしていないので、指示書どおり採用する。大きく減らすには読むキーを絞る必要があり、ユーザーの指示で後で検討する。
- **全体**: 1回のスクレイプに相当する All は、170.2 ms → 123.0 ms（−27.7%）、割り当ては −94.8%。loop（`--iterations 100 --interval 0`）でも、172.5 ms → 108.5 ms、25,353 B → 2,486 B。
- ベンチマークは、Phase 1 と同じ条件（Mac mini、AC 電源、`caffeinate -i`、BenchmarkDotNet の既定のジョブ）で実行した。
