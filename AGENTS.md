# AGENTS.md

## 项目概览

IoTCommTool 是一个基于 .NET 10 / Avalonia 12 的跨平台物联网通信调试工具，支持 TCP、UDP、Modbus TCP/RTU 和 Siemens S7。UI 使用 CommunityToolkit.Mvvm，连接配置存储于 SQLite。

## 构建与运行

```bash
dotnet build IoTCommTool.slnx -c Debug
dotnet run --project src/IoTCommTool.App
```

- Avalonia 构建服务会写用户目录；在受限环境中构建时设置 `AVALONIA_TELEMETRY_OPTOUT=1`，必要时使用 `-m:1 -nodeReuse:false` 并关闭 MSBuild server。
- 不要新增 NuGet 依赖，除非需求明确且已确认跨平台可用。
- 仓库使用 `.gitignore` 忽略 `bin/`、`obj/`、IDE 配置、本地数据库/日志等；不要提交构建产物。若某个产物曾被跟踪，用 `git rm -r --cached <path>` 取消跟踪，保留工作区文件。
- 文本文件统一 LF；唯一例外是 Windows 批处理 `*.bat` 保持 CRLF。新增文件由 `.gitattributes` 和 `.editorconfig` 约束，不要再提交混合行尾。

## 架构

- `IoTCommTool.Core`：与 UI 无关的协议实现、`FrameComposer`、CRC、循环发送、丢包统计。
- `IoTCommTool.Infrastructure`：SQLite 连接配置仓储，不改现有表结构除非明确需要迁移。
- `IoTCommTool.App`：Avalonia 视图、ViewModel、样式和交互。
- 不使用 XAML 代码后台承载业务逻辑；除文件选择、剪贴板、窗口快捷键、弹窗打开等视图级操作外，逻辑放在 ViewModel/Core。

## UI 约定

- 所有颜色、圆角、卡片背景通过 `Styles/Tokens.axaml` 的 DynamicResource 获取；不要在视图里硬编码主题色。
- 控件统一：普通控件高 36，紧凑按钮/工具高 28，卡片圆角 12，控件圆角 8；间距使用 4/8/12/16/20/24。
- 表单使用 `<StackPanel Classes="field">` + `TextBlock Classes="fieldLabel"` + 输入控件，标签在上；不要恢复“水平标签 + 固定高度”的写法。
- 复用组件：`ConnectionActions`、`SendPanel`、`LogPanel`、`HistorySidebar`。新增原始协议页应复用 `SendPanel`，不要复制发送区。
- 所有 ComboBox 默认拉伸填满字段；不要恢复 Fluent 默认的 Left 对齐，否则会与 TextBox 宽度不一致。
- 标题层级：页面/顶层卡片标题用 `TextBlock.cardTitle`（16 SemiBold）；卡片内嵌套分区用 `TextBlock.section`（14 SemiBold）；说明用 `caption`（12）；正文 13（等宽文本 12）。同一层不要混用 14/16，新增卡片标题时按此选择。
- 主题由 `Services/ThemeManager` 管理：顶部使用太阳/月亮/显示器图标菜单（MenuFlyout）选择跟随系统 / 浅色 / 深色，持久化到 `ui-settings.json`。Windows 原生标题栏通过 DWM 同步，并在切换后用 WM_NCACTIVATE 强制重绘，不依赖后续切页。
- 所有 `DataTemplate` 尽量声明 `x:DataType`；需要跨 DataTemplate 访问 Window DataContext 时显式关闭编译绑定并写清 RelativeSource。
- 用户界面文案使用简体中文；图标使用 Path 几何图形，不使用 emoji 或第三方图标字体。
- 不引入新的窗口级覆盖式“配置库”；连接历史由左侧 `HistorySidebar` 承载。通信历史未来应作为同一历史宿主的第二个视图接入，避免回退成弹窗。
- 主窗口最小尺寸 1100×720；任何新页面必须在该尺寸和 1440×900 下检查无横向裁切。左侧工作区宽度由 GridSplitter 在 380–520 之间调整。

## 调试与历史

- 连接历史变更通过 `MainWindowViewModel` 刷新；删除采用“延迟写库 + 5 秒撤销”，不要改回同步立即删除。
- 发送历史仅在当前标签页内存中保留 20 条，不写 SQLite。
- 原始协议拼帧必须经过 `FrameComposer`：CRLF → 序号 → CRC16；手动和循环发送共用同一路径。
- 注册包仅用于 TCP 客户端 / UDP：连接成功后按自身 Hex/ASCII 解析原样发送，不经过 `FrameComposer` 附加项；默认填入本机 MAC；开关/内容/格式随连接配置保存（`RegisterEnabled` / `RegisterText` / `RegisterHex`），TCP 服务端不提供该功能。
- 只有确认发送序号时才解析接收序号；不要在未启用序号时把任意前 4 字节显示成序号。
- 连接状态的“连接中”按钮必须是“取消”，关闭/取消路径不得输出失败错误日志。
- 切换 Hex/ASCII 时必须自动转换当前输入；ASCII 解析必须支持 `\r \n \t \0 \\ \xNN` 转义，保证二进制帧往返不丢失。
- 网络绑定：TCP 服务端默认 `0.0.0.0`（所有 IPv4 网卡）；只有用户显式选择某个 IP 时才单网卡监听。地址目录由 `LocalNetworkAddressService` 动态刷新，网络变化不得自动重绑或关闭监听器。
- TCP 客户端的本地出口为空表示“自动（系统路由）”；指定 IP 只做源地址绑定，不保证覆盖 Windows 路由 metric。网络变化只刷新选项，不自动迁移已有 TCP 连接。
- Tab 的关闭操作必须支持“当前 / 左侧全部 / 右侧全部 / 全部”；右键与标签上的 `⋯` 入口应复用同一套命令。

## 验证清单

- `dotnet build IoTCommTool.slnx -c Debug` 无错误。
- 打开空状态、每个协议标签页，检查 1440×900 和 1100×720；展开/收起历史侧栏；检查日志区宽度。
- 检查连接卡、操作卡、发送卡的标签/输入框基线、按钮高度、ComboBox 与 TextBox 同宽和长文本省略。
- 检查同一工作区的顶层卡片标题（连接、手动发送、日志、读写/主站/寄存器）字号一致；说明文字明显小于标题。
- 点击主题图标切换跟随系统 / 浅色 / 深色，不切换标签页即可看到窗口标题栏同步；检查重启持久化。
- 检查 Hex/ASCII 自动转换、`\xNN` 转义往返和中文输入不受影响。
- 用本机 TCP 回环连接验证：连接成功、日志接收、连接耗时、历史自动保存。
- 验证注册包：TCP 客户端 / UDP 启用后连接成功即发一次，对端日志可见；默认填入本机 MAC；Hex 非法内容只警告不影响连接；从历史恢复后开关/内容/格式还原。
- 验证 ASCII 参考表：发送面板打开小窗、搜索（如 `0D`、`回车`）、双击行复制 `\xNN` 转义；切标签不重复开窗，关标签弹窗随之关闭。
- 验证 Tab 右键菜单和 `⋯` 菜单的四种关闭操作，以及关闭当前/全部后的选中标签行为。
- 双网卡：TCP 服务端默认 `0.0.0.0` 能同时接收有线和无线地址；指定 IP 只监听该地址；TCP 客户端本地出口列表含“自动”和当前网卡，连接日志显示实际本机 endpoint。
- 网络变化后监听地址列表刷新，但已运行的监听器不被自动重绑，已有连接不被应用主动关闭。
- 用临时控制台验证 `FrameComposer` 的已知 CRC 向量、序号、CRLF 和 ASCII 转义往返。
- 修改 README/AGENTS 后，确保快捷键、拼帧顺序和历史行为与实现一致。
