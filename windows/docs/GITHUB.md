# 上传 GitHub 与发布版本

## 上传哪些文件

如果已有包含 Mac 版的 Yike 仓库，请按 [加入现有仓库](EXISTING_REPOSITORY.md) 操作，导出追加包并新增 windows/ 目录。下面的新建仓库流程适用于单独发布 Windows 版。

`tools/export-source.ps1` 生成的 `release/Yike-GitHub.zip` 是整理后的公开源码包，包含 README、源码、回归测试、图标、构建 / 安装器脚本、文档、第三方许可证和 .github 配置。

**先解压，再将包内文件上传到仓库根目录。** 请不要将 ZIP 本身作为唯一源码文件上传。网页上传时容易漏掉以点开头的 .github、.gitignore、.gitattributes、.editorconfig，建议使用 Git 上传。

```powershell
# 在干净的解压目录执行；YOUR_NAME 和 YOUR_REPOSITORY 请自行替换
git init -b main
git add .
git status --short
git diff --cached --stat
git commit -m "Prepare Yike Windows source"
git remote add origin https://github.com/YOUR_NAME/YOUR_REPOSITORY.git
git push -u origin main
```

这些命令是操作说明，不会自动创建远程仓库或上传文件。提交身份请使用自己的 Git 配置。

如直接从本机项目目录上传，应使用 Git 的忽略规则，不能把整个文件夹拖到网页。本机的 assets 语音运行库、release 和 dist 是可选依赖或构建结果，不属于源码上传内容。本机 .local-archive 为可恢复的旧资料归档，可能包含个人信息，已加入忽略规则且不进入公开源码包。

## 仓库设置建议

- 仓库名称：Yike 或 Yike-Windows。
- 简介：Windows 桌面翻译工具，支持 DeepL、划词、截图 OCR 与语音输入。
- Topics：windows、wpf、csharp、translation、deepl、ocr、whisper。
- 建仓库时不要勾选自动生成 README / .gitignore，避免覆盖已准备的文件。
- 本项目暂未声明统一开源许可证，上游参考版本也未附项目级 LICENSE；保留来源说明与第三方许可证，确定授权后再添加项目许可证。
- 若要接收安全报告，可在仓库安全设置中启用 Private vulnerability reporting；仓库中不提供个人邮箱。

## 构建检查

上传后，`.github/workflows/windows-ci.yml` 会在 push、pull_request 或手动触发时编译核心应用并运行回归测试。无需配置 DeepL 密钥或其他 GitHub Secrets。下载的 CI 构建不包含大型语音运行库。

界面、主题、缩放和预览检查请在有桌面会话的 Windows 机器上执行 `verify.ps1`，完整版本还应实际验证第三方语音功能。

## 发布安装包

1. 将 assets/update-feed.json 的 DownloadUrl 改为自己仓库的 `https://github.com/YOUR_NAME/YOUR_REPOSITORY/releases`，当前 example.com 为占位示例。
2. 按 README 构建完整版本、验证，再生成 Yike-Setup.exe。
3. 更新 CHANGELOG，并保持 AssemblyInfo.cs、更新配置与 Setup.cs 安装器登记版本一致；若发布 MSIX，同时调整其版本参数。
4. 在 GitHub Releases 发布版本标签和安装包，并附 SHA256。不要将安装包提交到源码目录。
5. 保留第三方组件的实际许可证、版本和源码来源；打包脚本会附带仓库第三方说明和许可证，运行库目录内的其他包许可证也必须保留。

需要远程更新配置时，可将 JSON 更新清单发布到自己的 HTTPS 地址，再在本机应用目录创建 update-source.txt 写入该地址。它属于本机配置，不提交到公开仓库。

## 重新导出源码

```powershell
.\tools\export-source.ps1
```

导出脚本使用明确的文件清单，不包含 .git 历史、用户数据、运行库、模型、安装包和本机构建产物。新增源文件目录后请同时更新该脚本的清单。
