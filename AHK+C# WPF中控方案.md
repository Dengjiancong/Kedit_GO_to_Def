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

开发运行时，AHK 查找 `Kedit.Console/bin/Release/Kedit.Console.exe`。正式分发时把 `Kedit.Console.exe` 放在主程序同目录。若尚未构建，中控菜单会提示构建路径。

验收：可从托盘打开、拖动和关闭中控；视频加载时窗口可拖动，圆角和阴影正常；视频缺失时仍能显示窗口；AHK 热键与托盘功能继续工作。

## 阶段二：设置与进程通信

引入本机命名管道（建议）实现 JSON 消息。AHK 仍是设置的唯一写入者；WPF 发送修改请求，AHK 验证后写 `Kedit_Settings.ini` 并返回结果。不要让两个进程同时编辑 INI。

当前已完成第一步：WPF 中控监听本机命名管道 `Kedit.Console`，AHK 打开中控后发送只读 `get_state` 请求，WPF 返回 JSON 确认并在界面显示连接状态。当前响应只表示通信链路已建立，尚未暴露快捷键设置内容。

建议消息：`get_state`、`set_hotkey`、`set_auto_update`、`check_update`、`open_about`。每条消息带请求 ID、命令名、参数和版本号。限制连接到本机用户会话，不开放网络端口。

迁移顺序：先做只读状态，再做自动更新开关，最后迁移快捷键编辑与其他设置。每项迁移都要验证原托盘操作仍然可用。

## 阶段三：媒体与更新界面迁移

将现有 AHK 设置窗口、更新窗口和关于窗口逐个迁入 WPF。中控负责布局和视频；AHK 负责版本检查、下载、校验和更新替换。只有新窗口的功能测试通过后，才移除对应 AHK GUI 代码。

如果 WPF `MediaElement` 对目标视频编码支持不足，优先把素材转为 Windows 常见的 H.264/AAC MP4；仍不能满足需求时再评估 LibVLCSharp 或 mpv 的 WPF 承载方式。不要重新使用独立 mpv 子窗口覆盖 WPF 的圆角区域。

## 阶段四：发布集成

构建脚本同时构建 AHK 主程序和 WPF 中控。发布包需要包含两者，并明确目录结构。将版本号、下载和回滚策略写入现有《方案+版本维护手册.md》。GitHub 先发布，Gitea 镜像同一版本与同一文件。

## 技术约束与后续 AI 接手

1. 先读本文件和《方案+版本维护手册.md》，检查 `git status` 与当前分支。
2. 不重写 AHK 快捷键核心；先以外部 WPF 进程逐步替换界面。
3. WPF UI 线程只负责界面；耗时下载与文件操作留在后台。
4. 无边框窗口通过 `WindowChrome` 实现拖动/窗口行为，按钮区域单独设置命中测试；视频层不得拦截拖动。
5. 圆角由 WPF 容器裁剪，阴影由外层窗口提供；每次改视觉布局都要实际启动检查。
6. 不把用户视频路径硬编码到 C#；由 AHK 传入已存在的素材路径。
7. 当前第一阶段没有进程通信协议，也没有设置编辑功能；不要把视觉占位按钮误认为已实现的功能。
