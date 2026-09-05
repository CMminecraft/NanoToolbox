# 纳米工具箱 (Nano Toolbox)

一个基于 WPF / .NET Framework 4.8 的轻量桌面工具合集，由 [陈梦方块匠](https://github.com/) 利用业余时间独立开发。

## 功能

- 批量重命名
- 文件哈希校验
- Base64 编解码
- 颜色拾取 / 取色器
- 下载器
- 网络工具（IP 信息、Ping）
- 文本编辑器 / 记事本
- 进程管理器
- 性能监视器
- 计算器
- 更多小工具持续加入中……

## 技术栈

- .NET Framework 4.8
- WPF (XAML) + C#
- 多主题切换（明 / 暗及多种暗色主题，见 `Themes/`）

## 运行 / 编译

1. 安装 [.NET Framework 4.8 开发者包 / Visual Studio](https://dotnet.microsoft.com/)（需勾选“.NET 桌面开发”）。
2. 用 Visual Studio 打开 `Toolbox.csproj`（或对应的解决方案）并生成。
3. 运行生成的 `Toolbox.exe`。

> 注：仓库目前提供 `.csproj` 工程文件；如需 `.sln` 解决方案文件，可在 Visual Studio 中“新建解决方案”并添加现有项目。

## 目录结构

```
IconPack/   内置图标资源（按类别分目录）
Icons/      界面图标
Themes/     多套 XAML 主题
*.xaml      各功能窗口界面
*.xaml.cs   对应后台逻辑
Toolbox.csproj  工程文件
```

## 许可

个人项目，仅供学习与自用。如需转载或二次开发，请保留作者信息并联系作者。
