# Kedit GO to Def

**让操作，随心而动。**

为 Kedit 与 Visual Studio 提供快捷键增强，用统一中控管理常用操作，并加入桌宠、OSD 和本地使用统计。

这个项目最初是为了简化 PK2 开发环境中 Kedit 的跳转定义操作：用一次鼠标中键点击，替代反复打开右键菜单。如今，它逐渐成为一个围绕日常代码编辑习惯的小工具。

[使用指南 Wiki](https://github.com/Dengjiancong/Kedit_GO_to_Def/wiki) · [Gitea 下载](https://gitea.evadd.xyz:88/EVADD/Kedit_GO_to_Def/releases) · [GitHub 下载](https://github.com/Dengjiancong/Kedit_GO_to_Def/releases) · [问题反馈](https://github.com/Dengjiancong/Kedit_GO_to_Def/issues) · [交流讨论](https://github.com/Dengjiancong/Kedit_GO_to_Def/discussions)

## 下载与开始使用

1. 从 **Gitea 主更新源**或 GitHub 发行页选择版本，下载 `Kedit_GO_to_Def.exe`。
2. 放在方便保存配置的目录运行；升级时保留自己的 `Kedit_Settings.ini`。
3. 从 Windows 托盘打开中控，查看当前绑定和功能说明。
4. 在对应编辑器中测试快捷键，再按自己的习惯调整。

> **版本说明（2026-10-11）**：以下功能按开发代码 `v19.00-Amiya.v008` 整理，不代表这些功能均已随公开 EXE 发布。当前 Gitea 最新发行标签为 `v18.30-Meme.v200`，GitHub 为 `v18.30-Meme.v009`；下载时以发行页的实时版本及说明为准。

当前中控基于 Windows WPF，目标为 **.NET Framework 4.7.2**；桌宠使用 WebView2。历史 AHK 版本的兼容性不代表新版中控支持 Windows XP。普通用户优先使用编译后的 EXE；源码运行与构建使用 **AutoHotkey v1**，不是 v2。

## 主要功能

| 功能 | 可以做什么 |
| --- | --- |
| Kedit 快捷键 | 跳转定义、回退、查找剪贴板、另存为、注释、缩进整理等 |
| FLOW 编辑 | 按 FLOW 重新编号行末 Bin；从剪贴板插入测试节点 |
| Visual Studio 快捷键 | 跳转／预览定义、回退、生成、注释与书签导航 |
| 其他快捷键 | 快速打开指定文件或文件夹，在资源管理器中运行 Python 文件 |
| 统一中控 | 修改绑定、检查冲突、保存配置，以及未保存草稿提示 |
| 桌宠与 OSD | 可选 Live2D 桌宠、互动与陪伴、按键文字及表情提示 |
| 本地使用统计 | 功能次数、使用趋势、常用时段、年度活跃墙及导出 |
| 页面与素材 | 公告轮播、图片／GIF／MP4 背景、构图调整和双状态分类图标 |
| 更新 | Gitea 优先检查、GitHub 备用检查、下载进度、文件检查及网络诊断 |

## 常用默认快捷键

已有用户的自定义设置会保留，**以中控显示的当前绑定为准**。Kedit 与 Visual Studio 的功能分别在对应应用中生效。

### Kedit

| 按键 | 功能 |
| --- | --- |
| 鼠标中键 | 智能点击／跳转定义 |
| 鼠标侧后键 / 侧前键 | Ctrl+B 回退 / Shift+F2 书签导航 |
| F1 | 查找剪贴板内容 |
| Alt+F | 文件中查找 |
| Ctrl+W | 关闭当前窗口 |
| Alt+A | 另存为 |
| Alt+I | 列填入数据 |
| Ctrl+/ | 注释／取消注释 |
| Ctrl+\ | 行首空格转 Tab，按 4 列制表位处理 |
| Alt+Shift+B | FLOW 行末 Bin 从 Bin4 递增，每个 FLOW 重新开始 |
| Alt+Shift+V | 插入剪贴板中的测试节点，新增行默认 Bin31 |

FLOW 插入需要把光标放在第二列节点名称前；原行 Bin 保留。具体示例见 [FLOW 编辑教程](https://github.com/Dengjiancong/Kedit_GO_to_Def/wiki/FLOW编辑教程)。

### Visual Studio 与其他

| 按键 | 场景 | 功能 |
| --- | --- | --- |
| 鼠标中键 | Visual Studio | 跳转／预览定义 |
| Ctrl+B / F7 | Visual Studio | 回退 / 生成 |
| Ctrl+/ | Visual Studio | 注释／取消注释 |
| Ctrl+F2 | Visual Studio | 建立／取消书签 |
| F2 / Shift+F2 | Visual Studio | 下一个 / 上一个书签 |
| Ctrl+Shift+F2 | Visual Studio | 清除书签 |
| Ctrl+Y | Visual Studio | 重做 |
| Ctrl+Q | 快速打开 | 打开配置的文件或文件夹 |
| F8 | 资源管理器 | 运行选中的 .py 文件，依赖本机 Python 环境 |
| Pause | 主程序 | 暂停／恢复快捷键 |

Visual Studio 命令依赖其键盘映射，与 VS Code 不通用。完整命令对应关系和绑定方法见 [快捷键参考](https://github.com/Dengjiancong/Kedit_GO_to_Def/wiki/快捷键参考)。

## 中控、桌宠与统计

- **中控**：托盘设置入口直接定位功能页。配置面板可收起，非交互背景可以拖动窗口。关闭中控窗口不会退出快捷键程序；从主程序退出时，中控与桌宠一起结束。
- **桌宠与 OSD**：可以分别启用或关闭。没有模型时可选择本地模型，或下载默认兔兔模型；模型资源和动作以实际模型支持为准。
- **统计**：只记录本程序接受的功能操作，不采集普通键盘输入、剪贴板、代码内容、文件路径或窗口标题。数据本地保存，支持 JSON／CSV 导出和清空。
- **活跃墙**：颜色深浅表示使用次数，不代表工作产出或效率排名。

**账号、个人云端同步、多设备合并及公开对比尚未上线**，计划留待后续大版本。当前本地统计无需注册，也不会自动上传。

## 更新与内容来源

程序更新以 [Gitea Release](https://gitea.evadd.xyz:88/EVADD/Kedit_GO_to_Def/releases) 为主，GitHub 作为备用检查来源。自动检查受用户开关控制；检查不等于自动下载。下载后显示实际执行的文件检查结果，已有 SHA-256 时核验哈希。

公告、轮播和模型来源由 [Kedit_Content](https://gitea.evadd.xyz:88/EVADD/Kedit_Content) 维护，与软件 EXE 发布分开。云端内容不可用时，首页保留有效缓存或内置内容。

详见 [更新与下载](https://github.com/Dengjiancong/Kedit_GO_to_Def/wiki/更新与下载) 和 [管理员素材配置](https://github.com/Dengjiancong/Kedit_GO_to_Def/wiki/管理员素材配置)。

## 开发与构建

项目由 **AutoHotkey v1 + C# WPF** 组成，桌宠使用 WebView2／Live2D 渲染。AHK 负责快捷键及配置应用，中控负责编辑、展示和相关服务。

使用项目的 `Build-Kedit.cmd`，或运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Kedit.ps1
```

构建需要相应编译工具和媒体／桌宠依赖；仅克隆源码不保证具备所有大型素材。脚本读取 AHK 中的 `CurrentVersion`，当前源文件名 `Kedit_GO_to_Def.ahk` 不等于实际运行版本号。

构建不会自动发布 Release。测试入口、架构和发布注意事项见 [开发与维护](https://github.com/Dengjiancong/Kedit_GO_to_Def/wiki/开发与维护)。

## 问题反馈

欢迎在 [Issues](https://github.com/Dengjiancong/Kedit_GO_to_Def/issues) 提交问题，或在 [Discussions](https://github.com/Dengjiancong/Kedit_GO_to_Def/discussions) 交流建议。

请尽量提供软件版本、Windows 和目标编辑器版本、复现步骤、预期与实际结果，以及必要截图或日志。提交前遮住私人代码、账号和路径，不必上传整份工作项目。

遇到问题可先查看 [常见问题与反馈](https://github.com/Dengjiancong/Kedit_GO_to_Def/wiki/常见问题与反馈)。桌宠偶发消失、特定环境下的组合键问题仍需按实际复现情况排查，不把暂时无法复现视为彻底修复。

## 项目缘起与致谢

这是一个从实际编辑需求出发、利用业余时间逐步打磨的项目。它起于对 PK2 和 Kedit 的使用，也记录了作者在测试开发中的学习、工作与成长。感谢使用、反馈和帮助改进它的同事与朋友。

旧首页中的 PK2 回忆、个人后记、图片和致谢均完整保留在 [2026-10-11 README 历史备份](docs/history/README-2026-10-11.md)。该备份用于保存历史，其中的功能状态与计划不作为当前版本说明。

默认兔兔模型作者为 [泡芙妙妙屋](https://space.bilibili.com/283810584)，[作者原下载入口](https://www.aplaybox.com/details/model/XieIXWh6Noz1)。模型及第三方组件遵守各自作者说明和许可，本项目不额外授予其权利。
