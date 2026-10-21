# IoTCommTool

跨平台（Windows / Linux / macOS）物联网通信调试工具，C# / .NET 10 / Avalonia。

## 功能

- 协议：TCP 客户端、TCP 服务端（多会话）、UDP、Modbus TCP/RTU（主站 + 从站）、S7（S7-300/400/1200/1500）
- 多连接：所有连接可多开，每个连接一个标签页；TCP 服务端默认监听所有 IPv4 网卡，也可指定单个地址
- 双网卡：TCP 客户端可选择本地出口 IP，便于有线接设备、WiFi 接外网时强制走指定网卡
- 连接历史：左侧常驻历史栏，按最近使用排序，支持搜索、协议分组过滤、打开、删除及 5 秒内撤销
- 发送调试：最近 20 条发送历史；Hex / ASCII 切换并自动转换当前报文；可选 4 字节序号；ASCII 追加 CRLF；Hex 追加 Modbus CRC16；发送前显示帧长度
- 注册包：TCP 客户端 / UDP 连接成功后可自动发送一次注册包；默认可填入本机 MAC，也可手动输入；独立 Hex / ASCII 格式，随连接配置保存
- ASCII 转义：支持 `\r`、`\n`、`\t`、`\0`、`\\`、`\xNN`，Hex ↔ ASCII 切换可无损往返二进制报文
- ASCII 参考表：发送面板一键打开 0–127 码值对照小窗，支持按十进制 / Hex / 字符 / 说明搜索，双击行复制 `\xNN` 转义
- 日志：复制、导出为文本、暂停/恢复自动滚动、清空、Hex 显示切换
- 主题：顶部太阳/月亮图标菜单，跟随系统 / 浅色 / 深色，可跨启动保存；Windows 原生标题栏即时同步
- 标签操作：右键连接标签或点击标签上的 `⋯`，可关闭当前、关闭左侧全部、关闭右侧全部、全部关闭
- 循环发送/丢包测试：固定间隔×N、总时长内均匀/随机发 N 次、每秒内随机时刻发 M 次、[min,max] 随机间隔；可选报文附加 4 字节序号，统计已发/已收/丢失/乱序/重复/丢包率
- Modbus 主站支持 01/02/03/04/05/06/0F/10 功能码与周期轮询；从站寄存器区可查看/编辑
- S7 支持 DB/M/I/Q 区，byte[]、short、int、float、bool、string 读写
- 连接配置存 SQLite（`%AppData%/IoTCommTool/connections.db`，Linux/macOS 为 `~/.config/IoTCommTool/`），连接成功自动保存，可打开/删除；主题偏好保存在同目录 `ui-settings.json`
- 连接中可取消；连接成功/失败日志附带耗时
- 连接历史侧栏为后续“通信历史”预留了独立的扩展宿主

## 界面与快捷键

主界面由“品牌栏 + 连接历史侧栏 + 连接标签 + 工作区”组成。连接工作区左侧为操作/发送区，右侧为日志区，中间可拖动调整宽度；窗口最小支持 1100×720。

| 快捷键 | 功能 |
|---|---|
| `Ctrl+N` | 新建连接 |
| `Ctrl+W` | 关闭当前标签 |
| `Ctrl+F` | 展开连接历史侧栏并聚焦搜索 |
| `Ctrl+Enter` | 在原始协议标签页发送当前报文 |
| `Ctrl+L` | 清空当前日志 |
| `Esc` | 清空历史搜索（弹层由控件自身处理，不全局收起侧栏） |

标签上的 `⋯` 按钮与右键菜单等价；主题切换使用顶部工具栏的太阳/月亮图标，当前模式在菜单中带单选标记，偏好保存在 `%AppData%/IoTCommTool/ui-settings.json`（Linux/macOS 为 `~/.config/IoTCommTool/`）。

连接历史侧栏可通过顶部“历史”按钮或侧栏右上角按钮折叠。删除历史后，在侧栏底部的撤销条消失前点击“撤销”即可恢复。

### 拼帧顺序

固定为：

```text
输入 → Hex/ASCII 解析 → CRLF（仅 ASCII 模式）→ 4 字节大端序号（可选）→ CRC16（仅 Hex 模式，低字节在前）→ 发送
```

手动发送和循环发送共用同一套拼帧逻辑。

Hex ↔ ASCII 切换会自动转换当前输入：可打印内容保持原样，二进制字节使用 `\xNN` 转义，保证往返不丢数据。

发送历史只保留当前标签页内的最近 20 条成功报文，不写入数据库；连接历史（保存的连接配置）和主题偏好才会持久化。

## 从源码运行

```bash
dotnet run --project src/IoTCommTool.App
```

要求 .NET 10 SDK。首次还原需要访问 NuGet。构建产物、IDE 配置、本地数据库和日志已由 `.gitignore` 排除，首次构建会重新生成 `bin/obj`。源码文本统一 LF，`build.bat` 保留 CRLF，规则见 `.gitattributes` / `.editorconfig`。

## 发布（自包含单文件，无需目标机装 .NET）

```bash
./build.sh                 # 三平台全部
./build.sh win-x64         # 只打某一个
build.bat                  # Windows 下等价
```

输出在 `publish/<rid>/`。

## 联通排查建议

1. **TCP / UDP**：先确认远端 IP、端口和防火墙；TCP 服务端监听 `0.0.0.0` 时从本机可用 `127.0.0.1` 验证，局域网设备则使用网卡实际 IP。
2. **Modbus TCP**：确认站号、功能码、起始地址（本工具按 0 基地址发送）和寄存器数量；写操作注意值类型。
3. **Modbus RTU**：确认串口号、波特率、数据位、校验、停止位与从站一致；本机联调可用虚拟串口对（Windows: com0com，Linux/macOS: socat）。
4. **S7**：S7-1200/1500 需在 TIA Portal 的 PLC 属性 → 防护与安全 中开启“允许来自远程对象的 PUT/GET 通信访问”；S7-300/400 的 Rack/Slot 默认 0/2，1200/1500 默认 0/1。
5. 如果连接长时间无响应，可在“连接中”直接点击“取消”；连接失败和成功都会记录耗时，便于定位网络或串口问题。

### 双网卡：有线接设备 + WiFi 接外网

- TCP 服务端默认监听 `0.0.0.0`，表示所有 IPv4 网卡；“监听地址”可改为指定有线或 WiFi IP，只接收该网卡的连接。
- 网络地址变化时，监听地址列表会自动刷新，但不会重绑或关闭正在运行的监听器；已有 TCP 连接不会跨网卡迁移。
- TCP 客户端的“本地出口 IP（可选）”默认为“自动（系统路由）”。双网卡时指定有线/无线 IP，可避免中间件连接被系统选错网卡；连接日志会显示实际本机 endpoint。
- 有线设备与 WiFi 建议处于不同网段；若两个网卡都有默认网关，检查 `route print` 和 `Get-NetIPInterface -AddressFamily IPv4` 的接口跃点，必要时调整网卡 metric 或静态路由。
- 添加 WiFi 后如果已有连接断开，优先排查 Windows 防火墙网络配置文件切换、网卡重置和设备侧重连策略；应用只会记录网络变化并允许手动重连，不会主动迁移已有 TCP 会话。

## 使用注意

- **S7-1200/1500**：需在博途（TIA Portal）PLC 属性 → 防护与安全 中开启“允许来自远程对象的 PUT/GET 通信访问”；S7-300/400 的 Rack/Slot 默认 0/2，1200/1500 默认 0/1
- **Linux 串口**：当前用户需在 `dialout` 组（`sudo usermod -aG dialout $USER` 后重新登录）；设备名为 `/dev/ttyUSB0` 等形式，macOS 为 `/dev/tty.*`
- **RTU 调试**：本机联调可用虚拟串口对（Windows: com0com，Linux/macOS: socat）
- **Windows UDP**：若绑定端口报 10013 错误，说明端口落在系统排除端口段（Hyper-V/WSL 保留），换个端口即可

## 后续规划

- 通信历史：在现有 `HistorySidebar` 宿主上增加按连接、方向、时间检索的历史视图，不改变主工作区布局。
- 日志方向着色/关键字过滤。
- Modbus 站号扫描等批量联通检查。

## 项目结构

```text
src/IoTCommTool.Core/           通信核心（协议实现、FrameComposer、循环发送引擎、丢包统计、CRC16）
src/IoTCommTool.Infrastructure/ SQLite 连接配置仓储（Dapper）
src/IoTCommTool.App/            Avalonia MVVM 桌面应用
AGENTS.md                       构建、界面约定与验证清单
```

第三方库：FluentModbus（Modbus）、S7netplus（S7）、System.IO.Ports（串口）。
