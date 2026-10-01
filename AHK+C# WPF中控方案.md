# AHK + C# WPF 中控方案

## 目标与边界

现有 AutoHotkey v1 程序继续负责快捷键、托盘、Kedit/Visual Studio 操作和更新逻辑。新的 C# WPF 程序只负责图形中控。这样可以逐步迁移 UI，而不一次性重写已工作的快捷键功能。

第一阶段使用本机已有的 Visual Studio 2019 和 .NET Framework 4.7.2。项目目录为 `Kedit.Console/`，输出为 `Kedit.Console.exe`。视频由 WPF 的 `MediaElement` 播放，不把 mpv 子窗口嵌入 WPF 窗口。

## 阶段一：独立中控外壳（本次实施）

- WPF 无边框圆角窗口、阴影、拖动、最小化和关闭按钮。
- 视频背景；无视频或解码失败时显示静态渐变背景。
- AHK 托盘增加“打开中控”入口。
- AHK 将已有 `side.mp4` 解压路径作为命令行参数传给中控。
- 多次点击托盘入口时激活已有中控实例，不重复启动。
- 中控暂不修改设置，也不取代 AHK 原有窗口。

开发构建：

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe' `
  '.\Kedit.Console\Kedit.Console.csproj' /t:Build /p:Configuration=Release
```

开发运行未编译的 AHK 脚本时，中控入口查找 `Kedit.Console/bin/Release/Kedit.Console.exe`。运行 `Build-Kedit.cmd` 时先构建 WPF Release，再由 AHK 的 `FileInstall` 将中控 EXE 嵌入主程序；打包后的程序在点击托盘“打开 Kedit 中控”时释放到 `%TEMP%\Kedit_Media\Kedit.Console.exe` 并启动。只需分发打包后的主程序 EXE，不需要另附中控 EXE。AHK 的原设置、更新和关于窗口仍会打开旧界面；只有托盘的中控入口打开 WPF。

验收：可从托盘打开、拖动和关闭中控；视频加载时窗口可拖动，圆角和阴影正常；视频缺失时仍能显示窗口；AHK 热键与托盘功能继续工作。

## 阶段二：设置与进程通信

引入本机命名管道（建议）实现 JSON 消息。AHK 仍是设置的唯一写入者；WPF 发送修改请求，AHK 验证后写 `Kedit_Settings.ini` 并返回结果。不要让两个进程同时编辑 INI。

当前已完成通信和第一轮设置接入：WPF 中控监听本机命名管道 `Kedit.Console`，AHK 打开中控后发送包含自动更新、OSD 和快捷键的 `get_state` 请求，WPF 显示这些状态。WPF 的自动更新和 OSD 开关通过 `WM_COPYDATA` 写回 AHK；快捷键输入框通过同一通道发送 `set_hotkey`，由 AHK 校验、写入 INI 并调用 `UpdateHotkeys()` 立即生效。直接双击 WPF EXE 时没有 AHK 窗口句柄，设置控件会保持不可用；应从 AHK 托盘菜单“打开 Kedit 中控”进行联调。

建议消息：`get_state`、`set_hotkey`、`set_auto_update`、`check_update`、`open_about`。每条消息带请求 ID、命令名、参数和版本号。限制连接到本机用户会话，不开放网络端口。

迁移顺序：已完成只读状态、自动更新开关、OSD 开关和快捷键编辑的第一版。下一步应增加命令结果回传、输入格式校验提示，以及 OSD 其他参数的编辑。每项迁移都要验证原托盘操作仍然可用。

## 阶段三：媒体与更新界面迁移

将现有 AHK 设置窗口、更新窗口和关于窗口逐个迁入 WPF。中控负责布局和视频；AHK 负责版本检查、下载、校验和更新替换。只有新窗口的功能测试通过后，才移除对应 AHK GUI 代码。

如果 WPF `MediaElement` 对目标视频编码支持不足，优先把素材转为 Windows 常见的 H.264/AAC MP4；仍不能满足需求时再评估 LibVLCSharp 或 mpv 的 WPF 承载方式。不要重新使用独立 mpv 子窗口覆盖 WPF 的圆角区域。

## 阶段三补充：快捷键专属控制页面（当前样板）

中控左侧按托盘菜单分段显示分类：Kedit 快捷键、Visual Studio、其他快捷键、通用设置。右侧先显示该分类的功能列表；点击快捷键后，在同一个主窗口内切换为左侧演示视频、右侧快捷键录入和保存设置。返回列表和切换分类都不打开独立弹窗。

推荐结构：

```text
MainWindow
 ├─ 左侧分类导航
 └─ 右侧内容区
     ├─ 分类功能列表
     ├─ 单项快捷键详情：左演示，右设置
     └─ 通用设置：自动更新和 OSD 开关
```

视频按功能单独存放，例如：

```text
Kedit_Media\shortcuts\
 ├─ go_to_definition.mp4
 ├─ vs_bookmark_toggle.mp4
 ├─ vs_bookmark_next.mp4
 ├─ vs_bookmark_previous.mp4
 └─ vs_redo.mp4
```

当前样板已经在同一个 `MainWindow` 内支持定义跳转、Visual Studio 三项书签和重做的单项详情与录入。演示视频从 `%TEMP%\Kedit_Media\shortcuts\` 按文件名加载；不存在时显示占位提示。“其他快捷键”分类暂时只显示说明，待样板确认后再逐项接入 AHK 状态和设置命令。原 `BookmarkWindow` 独立弹窗已移除。

## 阶段四：发布集成

构建脚本同时构建 AHK 主程序和 WPF 中控。发布包需要包含两者，并明确目录结构。将版本号、下载和回滚策略写入现有《方案+版本维护手册.md》。GitHub 先发布，Gitea 镜像同一版本与同一文件。

## 技术约束与后续 AI 接手

1. 先读本文件和《方案+版本维护手册.md》，检查 `git status` 与当前分支。
2. 不重写 AHK 快捷键核心；先以外部 WPF 进程逐步替换界面。
3. WPF UI 线程只负责界面；耗时下载与文件操作留在后台。
4. 无边框窗口通过 `WindowChrome` 实现拖动/窗口行为，按钮区域单独设置命中测试；视频层不得拦截拖动。
5. 圆角由 WPF 容器裁剪，阴影由外层窗口提供；每次改视觉布局都要实际启动检查。
6. 不把用户视频路径硬编码到 C#；由 AHK 传入已存在的素材路径。
7. 当前通信已进入第二阶段，但仍属于本机测试协议；正式发布前需要补充命令结果回传、失败提示和版本兼容处理。
