# 可选 Microsoft Store / MSIX 打包

普通 GitHub Releases 使用 EXE 安装器，不需要 Store 发布身份。此目录保留 MSIX 打包能力，模板中的发布者字段均为占位符。

准备完整的 release/Yike 目录，并安装含 MakeAppx.exe 的 Windows SDK。在 Partner Center 中确认自己的包名、Publisher 和 PublisherDisplayName 后运行：

```powershell
.\packaging\store\build-store-msix.ps1 `
    -PackageName "YOUR_PACKAGE_NAME" `
    -Publisher "CN=YOUR_PUBLISHER_ID" `
    -PublisherDisplayName "YOUR_PUBLISHER_NAME" `
    -Version "1.2.0.0" `
    -SourceDirectory "$PWD/release/Yike"
```

以上身份值必须替换，模板不包含任何原发布者身份。可通过 -MakeAppxPath 指定 SDK 中的 MakeAppx.exe，输出默认放在 release/store。

脚本生成未签名 MSIX。测试侧载还需使用自己的证书签名，Store 上传则遵循 Partner Center 流程。证书、私钥和发布账号资料不得提交到源码仓库。