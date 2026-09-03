# ClashWpf — mihomo (Clash) 图形控制器

用 **WPF + Caliburn.Micro 4.x** 写的一个 Clash 系客户端骨架，架构与 FlClash/Clash Verge 相同：
**GUI 只做壳，真正干活的是独立 mihomo 内核进程**（通过 external-controller REST API 遥控）。

> 用途说明：仅供学习与合规使用（内网代理、调试、访问正常境内外服务）。请遵守所在地法律法规。

---

## 架构

```
┌──────────────────────────────────────────────┐
│  ClashWpf (WPF + Caliburn.Micro 4, net9)    │
│    Shell(Conductor) → Dashboard / Proxies / │
│    Connections / Logs / Settings            │
│        │  localhost REST (external-controller)│
├────────▼────────────────────────────────────┤
│  mihomo.exe（独立子进程，-d run 目录）       │
└──────────────────────────────────────────────┘
```

| 部件 | 文件 | 说明 |
|---|---|---|
| 启动装配 | `Bootstrapper.cs` | Caliburn.Micro 4 SimpleContainer，单例注册 |
| 设置存储 | `Services/SettingsStore.cs` | `%APPDATA%\ClashWpf\settings.json` + 目录约定 |
| 订阅/配置 | `Services/SubscriptionService.cs` | 拉取机场订阅 YAML → 文本级合并端口/控制器/secret → `run\config.yaml` |
| REST 客户端 | `Services/ClashApiClient.cs` | /version /proxies /delay /connections /traffic |
| 引擎宿主 | `Services/ClashEngine.cs` | 定位并启动 mihomo.exe、stdout 日志环、每秒流量采样 |
| 系统代理 | `Services/SystemProxyService.cs` | 注册表切换 WinINET 代理 → 本地 mixed 端口 |
| 事件总线 | `ViewModels/UiBridge.cs` | 后台线程 → Dispatcher → EventAggregator(async) |

## 使用步骤

1. **准备内核**：已放入 `core\mihomo-windows-386-go120.exe`（v1.19.30），并在设置中指定路径。
2. **填订阅**：已预填你的订阅地址；如更换，在设置页修改后点「保存设置」→「测试订阅并生成配置」。
   成功后 `%APPDATA%\ClashWpf\run\config.yaml` 已生成（含端口/控制器覆盖）。
3. **启动**：仪表盘点「启动引擎」。引擎就绪后顶部状态栏显示 `运行中 v1.19.30`。
4. **（可选）接管系统代理**：勾选「将 Windows 系统代理指向本地引擎」。
5. 在「代理节点」页刷新列表 → 选中组 → 点「切换」换节点、测延迟；「活动连接」页看实时连接。

> 无订阅也能启动：会自动生成"全部直连"的最简配置，用于先验证进程/API 链路。

## 端到端实测结论（2026-09-02，真实订阅 + 真实内核）

使用你的订阅地址与 mihomo v1.19.30 内核完成全链路验证（`tests/E2EProbe` 探针，10/10 通过）：

| 步骤 | 结果 |
|---|---|
| 订阅下载 + YAML 合并 | ✅ 72 节点 / 50 代理组 / 37 规则，端口与控制器键正确覆盖、无重复顶层键 |
| 引擎启动（真实 mihomo 进程） | ✅ 正常启动，external-controller 就绪 |
| GET /version | ✅ v1.19.30 |
| GET /proxies | ✅ 129 条目（51 组 + 78 节点条目） |
| 延迟测试（走真实节点） | ✅ GLOBAL 组 512~1200ms |
| GET /traffic（修复流式响应读取） | ✅ 正常返回，不再挂起 |
| 引擎日志落盘 | ✅ 引擎 stdout 实时写入 `%APPDATA%\ClashWpf\run\engine.log`（超 5MB 自动轮转） |

> ⚠️ 构建提示：**若工程位于非 ASCII（中文）路径下，会触发 dotnet SDK 的 WPF 已知 bug**（wpftmp 重复 AssemblyInfo，CS0579）。已在 csproj 中通过
> `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` + `<GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>` 规避，可正常编译。

## 日志都在哪？

| 类型 | 位置 |
|---|---|
| 引擎运行日志（实时 + 留档） | `%APPDATA%\ClashWpf\run\engine.log`（日志页「打开日志文件」直达） |
| 未处理异常 / 崩溃 | `%APPDATA%\ClashWpf\crash.log` |
| 订阅原始 YAML | `%APPDATA%\ClashWpf\run\subscription.yaml` |
| 引擎实际加载配置 | `%APPDATA%\ClashWpf\run\config.yaml` |
| 规则缓存 / 数据库 | `%APPDATA%\ClashWpf\run\rules\`、`cache.db` |
| 应用设置 | `%APPDATA%\ClashWpf\settings.json` |

## 内核找不到？

启动前仪表盘与顶部状态栏会给出提示。mihomo 内核**不在本仓库内**，请按上面第 1 步准备。
若你没有现成内核，也可以先跑一个**只读测试**：把任意 Clash 格式 YAML 放到
`%APPDATA%\ClashWpf\run\config.yaml`（含 `external-controller: 127.0.0.1:9090`）再启动。

## 已知限制（骨架版，非生产）

- 流量/连接用 1s REST 轮询，未实现 WS 推送（`/traffic`、`/connections` 均有 WS 通道，是后续优化点）。
- 订阅合并是"顶层键文本覆盖"策略（保留锚点/注释），复杂 `proxy-providers` 订阅理论上可用，但未充分验证。
- 只支持单引擎实例，端口冲突会启动失败（日志页可见原因）。
- 未实现 TUN 模式、规则编辑器、订阅定时更新等 Clash Verge 高级功能。

## 构建运行

```bash
cd ClashWpf
dotnet build
dotnet run --project ClashWpf.csproj
```

技术栈：.NET 9 (net9.0-windows) · WPF · Caliburn.Micro 4.0.230 · YamlDotNet 16。
