# 中控样板图标资源

管理员“分类图标”选择与裁切后的图片保存在此目录：kedit.png、vs.png、pet.png、update.png、other.png；头像为 avatar.png。

PNG 在每次构建时通过项目 EmbeddedResource 自动嵌入 Kedit.Console.exe，随后随 AHK 打包。运行时优先读取工作区图片，找不到则读取内嵌资源。选择图片不会重新编译已经生成的 EXE；确认图标后需要再构建一次。

请保留这些 PNG 作为后续构建素材。未配置的项目继续使用默认图标。图片裁切不修改原文件。
