# REA WPF GUI — 架构规划与任务分工

> 目标：为 REA（本地逆向分析 CLI + MCP 工具）构建一个 Windows WPF 桌面 GUI，
> 让不熟悉命令行的用户通过界面使用本项目的全部分析能力。
>
> 状态：规划稿（v1）。实现按本文档 WBS 推进。

---

## 1. 定位与设计原则

**GUI 是 CLI 的包装器，不是第二个实现。**

| 原则           | 说明                                                                                                              |
| -------------- | ----------------------------------------------------------------------------------------------------------------- |
| CLI 是唯一权威 | 所有分析逻辑复用 `rea` CLI（`scripts/rea.mjs` → `dist/` 运行时）。GUI 不重新实现任何解析、Provider、Evidence 语义 |
| 无界面也可用   | GUI 的每个能力都必须能对应到一条 CLI 命令；`--selftest` 提供无界面自检                                            |
| 本地优先       | 与 REA 本身一致：分析在本地进程完成，配置/历史只落 `%LOCALAPPDATA%\ReaGui`，不上传任何数据                        |
| 失败可见       | 退出码、stderr、原始输出、耗时全部保留在"运行日志"页，不吞错                                                      |
| 大输出安全     | Evidence 可达数百 MB；JSON 树懒加载 + 单层子节点上限 + 原始文本截断提示                                           |

**非目标（v1 不做）**：跨平台（Avalonia/MAUI）、MCP 直连（走 MCP 协议而非 CLI）、
Provider 的图形化安装引导（仍走 `rea setup`）、结果的可视化图表。

---

## 2. 总体架构

```
┌───────────────────────────────────────────────────────────────┐
│ 表现层 Views (WPF/XAML)                                        │
│  MainWindow ── RunPage / HistoryPage / SettingsPage / AboutPage │
│       │ 绑定                                                  │
│  ViewModels (MVVM-lite: BaseViewModel + RelayCommand)          │
│  MainViewModel / RunViewModel / HistoryViewModel / SettingsVM  │
├───────────────────────────────────────────────────────────────┤
│ 应用服务层 Services                                            │
│  ReaCliService      定位 CLI、组装 argv、运行、取消、超时        │
│  CommandCatalog     约 150 条命令的分组/spec 元数据（内嵌 JSON） │
│  ProviderDiscovery  `providers --json` → 提供方下拉框            │
│  OutputSummarizer   从结果 JSON 提取关键事实（状态/提供方/错误） │
│  SettingsStore      设置读写（%LOCALAPPDATA%/ReaGui）            │
│  HistoryStore       运行历史持久化 + 重新运行                    │
├───────────────────────────────────────────────────────────────┤
│ 基础设施 Models + JsonTree                                      │
│  CommandSpec / CliSettings / RunRecord / RunResult              │
│  JsonTree: JsonDocument → 懒加载树节点（展开时才物化子节点）      │
├───────────────────────────────────────────────────────────────┤
│ 外部：REA CLI                                                   │
│  node <repo>/scripts/rea.mjs <command> ... --json               │
└───────────────────────────────────────────────────────────────┘
```

**核心数据流（一次运行）：**

```
用户选择命令 → CommandSpec 渲染动态表单 → 收集参数
  → ReaCliService.BuildArguments() 组装 argv（自动补 --json）
  → Process 异步运行（stdout/stderr 分流，支持取消/超时）
  → RunResult { ExitCode, Stdout, Stderr, Duration, CommandLine }
  → JsonTree（树视图） + OutputSummarizer（事实页） + 原始输出 + 运行日志
  → HistoryStore 记录（可重放）
```

---

## 3. 模块划分与职责

目录：`gui/ReaGui/`

| 模块     | 文件                                                                          | 职责                                                               |
| -------- | ----------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| 应用入口 | `App.xaml(.cs)`                                                               | 启动、主题资源字典装载、`--selftest` 分支                          |
| 主窗口   | `Views/MainWindow.xaml(.cs)`                                                  | 左侧导航 + 内容区 + 底部状态栏（CLI 探测结果）                     |
| 运行页   | `Views/Pages/RunPage.xaml(.cs)`                                               | 命令选择器、动态参数表单、目标拖拽、运行/取消、输出四个页签        |
| 历史页   | `Views/Pages/HistoryPage.xaml(.cs)`                                           | 历史列表、重放、打开输出文件、删除                                 |
| 设置页   | `Views/Pages/SettingsPage.xaml(.cs)`                                          | CLI 模式/仓库根/Node 路径/默认提供方/输出目录/主题                 |
| 关于页   | `Views/Pages/AboutPage.xaml(.cs)`                                             | 版本、CLI 探测明细、免责声明入口                                   |
| 主题     | `Themes/Dark.xaml`、`Themes/Light.xaml`                                       | 全部控件样式（暗色为默认）                                         |
| 命令模型 | `Models/CommandSpec.cs`                                                       | positionals/flags/kind（path/text/hex/number/bool/provider）       |
| 目录数据 | `Assets/command-catalog.json`                                                 | 由 `Assets/generate-catalog.mjs` 从 `dist/cliCommandNames.js` 生成 |
| CLI 服务 | `Services/ReaCliService.cs`                                                   | 三种定位模式、argv 组装、异步运行、进程树取消                      |
| 其余服务 | `Services/{SettingsStore,HistoryStore,ProviderDiscovery,OutputSummarizer}.cs` | 见上表                                                             |
| JSON 树  | `Models/JsonTree.cs`                                                          | 懒加载节点，防大 JSON 卡死                                         |
| 自检     | `App` 的 `--selftest`                                                         | 定位 CLI → 跑 `providers --json` → 打印摘要 → 非零退出码报错       |

**CLI 定位优先级**（设置页可覆盖）：

1. 设置中显式配置的模式
2. `local`：从 GUI 可执行文件向上查找含 `scripts/rea.mjs` 的目录（开发态默认命中 `D:\REA\Thisrea`）
3. `global`：`PATH` 中的 `rea`
4. `npx`：`npx -y rea-agents@latest`

**命令目录策略**：约 150 条命令分 14 组（环境/原生分析/构建元数据/.NET/
JavaScript·Electron·Web/Apple/Android/ADB/JEB/Frida/固件/工件与证据…）。

- 核心 ~30 条命令：手写 spec（位置参数标签、path 选择器、provider/快照等旗标）
- 其余命令：通用 spec（目标路径 + provider + `--json` + 自由附加参数）
- 每条命令的 `--help` 文本按需拉取并缓存，显示在运行页帮助面板

---

## 4. 关键技术决策

| 决策                               | 理由                                                               |
| ---------------------------------- | ------------------------------------------------------------------ |
| .NET 10 + WPF（`net10.0-windows`） | 本机已是 .NET 10 SDK；REA 主要使用场景是 Windows                   |
| 零外部 NuGet                       | `System.Text.Json` + `System.Diagnostics.Process` 足够；离线可构建 |
| MVVM-lite 手写基类                 | 避免引 CommunityToolkit；命令/通知逻辑量小                         |
| 暗色主题默认                       | 与用户 IDE 主题一致；`Themes/` 双字典运行时切换                    |
| 树懒加载                           | 334MB Evidence 文件不能一次 `JObject.Parse` 后全量物化             |
| 进程树取消                         | node CLI 会拉起子进程；`Kill(entireProcessTree: true)`             |
| `--selftest` 模式                  | GUI 无法在无头环境交互验证；自检复用同一 ReaCliService 代码路径    |

---

## 5. 任务分工（WBS）

| #   | 任务       | 产出                                           | 验收标准                                                    | 依赖     |
| --- | ---------- | ---------------------------------------------- | ----------------------------------------------------------- | -------- |
| T1  | 工程骨架   | `gui/ReaGui.sln` + csproj + App/主题/MVVM 基类 | `dotnet build` 零警告通过，窗口可起                         | 无       |
| T2  | CLI 服务层 | `ReaCliService` + selftest                     | `ReaGui --selftest` 能定位 CLI 并跑通 `providers --json`    | T1       |
| T3  | 命令目录   | 生成脚本 + `command-catalog.json` + 分组       | 150 条命令全部有分组，核心 30 条有 spec；无未分组命令       | 无       |
| T4  | 运行页 UI  | RunPage + 动态表单 + 拖拽 + 提供方发现         | 选命令→填参→运行→出结果，全流程可用                         | T2,T3    |
| T5  | 输出展示   | JSON 树 + 事实摘要 + 原始 + 日志 + 保存/复制   | 大 JSON 不卡死；保存的文件可被 `inspect-analysis-view` 消费 | T4       |
| T6  | 历史页     | HistoryPage + HistoryStore                     | 重放/打开/删除正常，重启后保留                              | T4       |
| T7  | 设置页     | SettingsPage + SettingsStore                   | CLI 模式切换立即生效并持久化                                | T2       |
| T8  | 收尾验证   | README + 端到端自检 + 文档                     | 按 README 从零构建并自检通过                                | T5,T6,T7 |

**并行建议**：T3 可与 T1/T2 并行；T6/T7 可在 T4 完成后并行。

---

## 6. 里程碑

- **M1 骨架**：T1 完成，空窗口构建运行通过
- **M2 打通 CLI**：T2+T3 完成，selftest 通过
- **M3 核心闭环**：T4+T5 完成，`analyze`/`decompile`/`search` 等核心命令端到端可用
- **M4 交付**：T6+T7+T8 完成，README + 自检报告

---

## 7. 风险与未决问题

| 风险                                | 现状 / 缓解                                                                                                              |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| CLI dist 过期 + node_modules 不完整 | 已发现（`normalizeCliOutputArguments` 缺失、saxes/cborg/openpgp 未装）；正在修复，修复前 selftest 会明确报告而非静默失败 |
| 150 条命令 spec 覆盖                | 核心手写 + 通用兜底；帮助文本按需拉取弥补描述缺失                                                                        |
| 大 Evidence 内存                    | 懒加载树 + 上限；原始页只加载前 N MB                                                                                     |
| 仅 Windows                          | WPF 限定；文档注明，后续如需跨平台再评估 Avalonia                                                                        |
| 仓库检查管线                        | `gui/` 为 C#，不参与 TS 的 knip/jscpd/oxfmt 检查面；需在 `gui/README.md` 注明独立构建方式                                |

---

_维护约定：新增 CLI 命令后重跑 `Assets/generate-catalog.mjs`；核心命令 spec 变更同步更新本文档第 3 节。_
