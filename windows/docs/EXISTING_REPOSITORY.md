# 将 Windows 版加入现有 Yike 仓库

适用于现有仓库保留 Mac 版 native/ 和 server/ 的情况。Windows 版独立放在 windows/ 下，不覆盖根目录原有 README、.gitignore 或 Mac 代码。

## 目录结构

```text
Yike/
  native/                     现有 Mac 版
  server/                     现有服务端
  windows/                    新增 Windows 版与其 README
  .github/workflows/
    yike-windows-ci.yml        新增 Windows 构建检查
  README.md                   保留原文并添加平台入口
```

## 导出追加包

在 Windows 项目根目录执行：

```powershell
.\tools\export-source.ps1 -ForExistingRepository
```

生成 release/Yike-Windows-追加包.zip。解压后，顶层为 windows/ 和 .github/，将这两项合入现有仓库的本机克隆目录。

追加包只包含公开 Windows 源码和配置，不包含本机旧资料、语音运行库、模型或安装包。它不修改现有 native/、server/、根 README 或根 .gitignore。如果目标已有 windows/ 或同名 .github 文件，应先比较并合并，不要直接覆盖。

## 上传步骤

1. 用 Git 克隆现有仓库到新的本机目录；不要在已有仓库中重复 git init。
2. 将追加包解压到克隆目录根部，确认是 Yike/windows/src，而不是 Yike/windows/windows/src。
3. 在根 README 的标题之后加入下方平台入口，保留原有 Mac 说明。
4. 执行 git status --short 和 git diff --stat，新增源码应位于 windows/，其他新增配置应位于根 .github/；native/ 和 server/ 不应出现修改。
5. 将改动提交到新分支并推送，发起 Pull Request 审核后合并。

根 README 可添加：

```markdown
## 平台版本

| 平台 | 源码与说明 |
| --- | --- |
| macOS | [Mac 源码](native/)；使用说明见本文下方 |
| Windows | [Windows 使用与构建说明](windows/README.md) |

Windows 版采用 C# / WPF 与 DeepL API，两个平台各有自己的实现和构建流程。
```

## 构建和 CI

本机运行 Windows 命令前先进入 windows/：

```powershell
cd windows
.\verify.ps1 -SkipRuntimes
```

有效工作流为仓库根 .github/workflows/yike-windows-ci.yml，默认工作目录已设为 windows，构建产物路径为 windows/dist/ci。GitHub 不会加载 windows/.github 下的工作流；该目录仅保留 Windows 项目单独导出时使用的配置与模板。

追加包另附根 .github/ISSUE_TEMPLATE/windows_bug_report.md 与 windows_feature_request.md。若仓库原本已有问题模板，可保留原模板并按需添加这两个 Windows 模板。不要用 Windows 项目的通用 PR 模板覆盖已有全仓库模板。

## 安装包发布

完整安装包放在同一仓库 Releases 中。建议使用独立的 Windows 标签和名称，例如 windows-v1.2.0、Yike Windows 1.2.0，避免与已有 Mac 版本标签混淆。

发布前将 windows/assets/update-feed.json 的下载地址替换为该仓库的 Releases 地址，再从 windows/ 按 README 构建和打包。安装包作为 Release 附件上传，不提交到源码目录。项目和第三方许可证范围仍以各自的来源说明为准。
