# 第三方来源与许可证

本文件记录来源，不构成对整个 Yike 项目的统一许可证声明。第三方软件的许可范围以对应项目和所用版本的许可证为准。

## 产品参考

Windows 版参考 [mac-translator](https://github.com/a17746168234-alt/mac-translator) 的功能结构与交互。参考提交为 `1b414c90ed1d671b69878acec13fbca03b081a75`（v1.6-build59）。该本地快照未附带项目级 LICENSE，仅包含 SwiftEdgeTTS 的第三方 MIT 声明，已保留在 `third_party/licenses/SwiftEdgeTTS-NOTICE.txt`。SwiftEdgeTTS 的声明不等于上游整个项目的 MIT 授权。

当前 Windows 代码亦包含由既有发行版本恢复并按功能整理的成员，保留源代码兼容结构。本仓库尚未声明统一开源许可证。

## 可选运行组件

| 组件 | 用途 | 来源 / 许可 |
| --- | --- | --- |
| Python 3.12.10 embedded x64 | 在线朗读脚本运行环境 | [Python](https://www.python.org/)，PSF 及随发行包附带的许可 |
| edge-tts 7.2.8 | Microsoft 在线语音适配 | [rany2/edge-tts](https://github.com/rany2/edge-tts)，LGPLv3；srt_composer.py 单独使用 MIT |
| whisper.cpp / ggml | 本机语音识别 | [ggml-org/whisper.cpp](https://github.com/ggml-org/whisper.cpp)，MIT |
| OpenAI Whisper 模型 | 多语言语音识别模型 | [openai/whisper](https://github.com/openai/whisper)，MIT |
| SDL2 | 麦克风音频采集 | [libsdl-org/SDL](https://github.com/libsdl-org/SDL)，zlib |

已保留本机发行运行库附带的许可证于 `third_party/licenses/`。大型二进制、Python 包和模型不包含在公开源码包中；它们各自的版权与许可不会因未提交到 Git 而消失。

edge-tts 会引入 aiohttp 等间接依赖。准备或发布运行库时，应保留其 LICENSE、dist-info 和组件版本；完整依赖树以实际运行库安装记录为准。本文件不将这些依赖笼统授权为 MIT。

DeepL 和 Microsoft 在线语音为外部服务；本仓库不包含它们的服务端代码、账号或共享密钥。Windows / .NET Framework 由用户系统提供。