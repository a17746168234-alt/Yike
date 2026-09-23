# 语音运行库准备

运行库和模型体积较大，仓库仅包含适配代码和许可证。核心应用及离线回归测试可直接使用 `build.ps1 -SkipRuntimes`，不需要以下依赖。

完整版本使用下面的目录布局：

```text
assets/
  speech-runtime/
    python.exe
    python312.dll
    python312.zip
    python312._pth
    LICENSE.txt
    Lib/site-packages/
      edge_tts/
      ...Python 包及其 dist-info / 许可证
  whisper-runtime/
    ggml-small-q8_0.bin
    LICENSE-whisper.cpp.txt
    LICENSE-openai-whisper.txt
    LICENSE-SDL2.txt
    THIRD-PARTY-NOTICES.txt
    Release/
      whisper-stream.exe
      whisper.dll
      SDL2.dll
      ...同一构建生成的 ggml DLL 及其他运行依赖
```

## 方式一：导入已有完整运行库

如果已有可信的 Yike 完整安装目录，且其中包含这两个运行库，可以从该目录导入：

```powershell
# assets 下不能已存在同名运行库目录，避免覆盖现有文件
.\tools\prepare-runtimes.ps1 -FromDirectory "$env:LOCALAPPDATA\Programs\Yike"
.\build.ps1 -OutputDirectory release\Yike
```

导入脚本只复制 speech-runtime 和 whisper-runtime，不读取用户数据或密钥。

## 方式二：从官方组件准备

### 在线朗读

目前测试的组合为 Python 3.12.10 x64 embedded 与 edge-tts 7.2.8。请从 [Python 官方下载目录](https://www.python.org/ftp/python/3.12.10/) 获取 `python-3.12.10-embed-amd64.zip`，解压到 `assets/speech-runtime/`。

另外安装一份带 pip 的 Python 3.12，用它将 Python 包安装到嵌入式运行库的目标目录；嵌入式 Python 本身不需要安装 pip。

```powershell
New-Item -ItemType Directory -Force assets\speech-runtime\Lib\site-packages
py -3.12 -m pip install --target assets\speech-runtime\Lib\site-packages "edge-tts==7.2.8"
if ($LASTEXITCODE -ne 0) { throw 'Python 依赖安装失败' }
```

将 `python312._pth` 内容调整为：

```text
python312.zip
.
Lib\site-packages
import site
```

检查导入：

```powershell
.\assets\speech-runtime\python.exe -c "import edge_tts; print(edge_tts.__version__)"
```

依赖源目录保留 Python 的 LICENSE.txt 以及每个包的 dist-info / 许可证。edge-tts 的依赖由 pip 解析，后续准备时其间接依赖版本可能变化。发布构建会在第三方许可证已经随包保存的前提下，从最终运行目录移除不会在运行时读取的 `__pycache__` 和 dist-info 安装元数据，以减小体积；源依赖目录不受影响。发布时保存 pip 安装清单及实际组件版本，勿声称跨时间完全一致。详见 [edge-tts 源码](https://github.com/rany2/edge-tts)。

### 离线语音识别

需要支持 SDL2 的 Windows x64 `whisper-stream.exe`，只下载 whisper-cli.exe 不足以启动麦克风识别。

可使用 [whisper.cpp](https://github.com/ggml-org/whisper.cpp) 的 Windows x64 构建；若从源码编译，需 Visual Studio C++ 构建工具、CMake 和 SDL2 x64 开发包。以 v1.8.3 源码为例，在其根目录运行：

```powershell
# 将 SDL2_DIR 指向自己解压的 SDL2 开发包 CMake 配置目录
cmake -B build -A x64 -DWHISPER_SDL2=ON -DBUILD_SHARED_LIBS=ON -DSDL2_DIR="C:/Dependencies/SDL2/cmake"
if ($LASTEXITCODE -ne 0) { throw 'CMake 配置失败' }
cmake --build build --config Release
if ($LASTEXITCODE -ne 0) { throw 'Whisper 编译失败' }
```

将构建产生的 whisper-stream.exe、whisper.dll、ggml DLL 及其依赖放入 `assets/whisper-runtime/Release/`，并复制 SDL2 开发包中 x64 的 SDL2.dll。Visual Studio 的 Release 输出通常在 `build/bin/Release/`，以实际构建结果为准；这些二进制必须来自兼容的同一构建。

模型使用多语言 `ggml-small-q8_0.bin`。可从 [whisper.cpp 模型目录](https://huggingface.co/ggerganov/whisper.cpp/tree/main) 获取对应文件；官方 SHA-256 为 `49c8fb02b65e6049d5fa6c04f81f53b867b5ec9540406812c643f177317f779f`。放到 `assets/whisper-runtime/`。不要用仅英语的 small.en 模型替代。实时识别需要 `whisper-stream.exe`，整段高精度校正还需要同一构建中的 `whisper-cli.exe`。

将仓库 `third_party/licenses/` 内三个 Whisper / SDL2 许可证复制到运行库根目录，并保留第三方说明。构建和麦克风使用说明见 [上游 stream 文档](https://github.com/ggml-org/whisper.cpp/tree/v1.8.3/examples/stream)。

## 验证完整版本

```powershell
.\build.ps1 -OutputDirectory release\Yike
.\verify.ps1 -OutputDirectory release\Yike -SkipBuild
.\installer\build-installer.ps1 -SourceExe release\Yike\Yike.exe
.\release\Yike-Setup.exe --verify
```

回归测试使用模拟翻译响应，不调用真实 API，也不验证实际麦克风和在线语音服务。发布完整版本前还需在应用中实际检查语音输入与朗读；安装器会检查关键运行库文件，但不能替代功能测试。
