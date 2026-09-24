# Yike 邮箱账号与 DeepL 公共额度服务

Python 3.11+，标准库。生产实例 `/opt/yike-trial`，仅监听 `127.0.0.1:8093`，Nginx 对外入口为 `https://n5v1b.cn/yike-api/`。内存上限 192MB，CPUQuota 30%；共用服务器上的网站和游戏保持独立。

## 当前状态与领取规则

Build 67 使用邮箱、密码和六位邮箱验证码，不需要人机验证或打开浏览器。只有完成邮箱验证后才创建账号并一次发放 **200,000 字符**。所有账号共用 **4,000,000 字符终身总预算**，按实际使用扣减，不按月重置，注册不预留公共预算。

旧用户名账号可使用现有登录会话绑定邮箱；验证成功后总赠额补齐到 200,000，已有消费保留。旧客户端的用户名注册/登录接口已关闭，避免绕过邮箱验证。未绑定邮箱的旧会话不能使用公共翻译。已退出的旧用户名账号可使用邮箱重新注册；旧账号数据保留但不会获得公共翻译权限。

**SMTP 发信服务和公共 DeepL 密钥由管理员另行配置，尚未完成真实收信与公共翻译验收。** 不读取客户端个人密钥，不复用四级网站的发信服务。

一个验证过的邮箱只能领取一次。验证码10分钟有效、最多尝试5次，登录按IP和邮箱限流、发信同邮箱60秒一次，每IP每小时5次。Gmail 点号和加号别名合并处理；邮箱验证不等于真实身份认证，不能保证一个自然人只拥有一个邮箱。

## 配置（在自己的终端操作，勿将密钥发到聊天或提交仓库）

### 独立 SMTP 发信服务

准备支持 SSL 的 SMTP 地址、端口、用户名、授权码和已验证的发件邮箱。确认供应商免费额度和付费超额开关后运行：

```bash
ssh -t dessert-duel-aliyun 'sudo python3 /opt/yike-trial/configure_email.py'
```

脚本以隐藏输入接收密码，检查 SMTP 登录并设置 Yike 发信限制；默认每天50封、滚动31天1000封，达到上限停止发信。脚本不购买服务、不发送测试邮件；保存后请自行在应用中验证真实收信。该限制仅统计 Yike，供应商若还有其他用途，应预留相应额度。

### 四个公共 DeepL 密钥

```bash
ssh -t dessert-duel-aliyun 'sudo python3 /opt/yike-trial/configure_key.py'
```

输入数量1–4，再逐个隐藏输入专用密钥。脚本仅通过官方 `/usage` 查询每个密钥实际余额，输入 `ENABLE` 后才保存并启动服务。

目前适配 **官方 API Free（`:fx`）**，不调用 Pro 收费接口；官网密钥也须核对具体套餐。标称400万不会覆盖实际供应商限制，若官网合计只有200万，服务按真实剩余量停止。密钥不足以覆盖单次请求时选其他余额充足的密钥；翻译超时或结果不确定时不换密钥重发，避免重复计费。

服务按请求选择余额充足且空闲的密钥。官方余额缓存30秒，缓存期间预扣每次请求字符；上游异常使缓存失效，过期且存在进行中请求时等待其结束再查询。每个密钥复用HTTPS连接，失败时丢弃连接且不重发翻译。最多同时处理2个翻译请求，每个密钥同时仅处理1个；SQLite事务保护个人额度、总池预算和重复请求。总池4百万独立持久化，不因轮换密钥或供应商月度重置而重置。

### 暂停、备份和升级

将 `/etc/yike-trial.env` 的 `YIKE_PUBLIC_ENABLED=0` 后运行 `systemctl restart yike-trial`，账号与余额保留。

```bash
systemctl status yike-trial --no-pager
curl --fail https://n5v1b.cn/yike-api/v1/config
```

环境文件权限600；数据库 `/var/lib/yike-trial/accounts.sqlite3` 位于权限700的目录。使用SQLite backup API备份数据库，并私密备份环境文件。不要复制它们到源码或安装包，不要删库升级，不要同时启动多个服务进程。

部署需要复制本目录的 `*.py`、service 和部署脚本到 `/opt/yike-trial`。`deploy.sh` 为当前 n5v1b.cn 配置设计，其他服务器需调整Nginx文件和插入点。升级先测试再重启，保留环境和数据库；Build65自动添加邮箱列与验证码表，不删除旧账号。

Build 67 移除旧验证页面、回调和服务端校验；启动时仅清理废弃的人机验证票据表，账号、会话、邮箱验证码和额度记录保留。旧客户端访问已停用的验证入口会收到更新提示，应安装最新版。

## Windows / 其他客户端接口

基地址如上，JSON请求；会话使用 `Authorization: Bearer <token>`，有效30天，放入系统凭据管理器。所有错误返回 `code` 和 `message`。Windows可复用服务接口，无需浏览器验证组件；Apple引擎不支持Windows。

| 方法与路径 | 请求与结果 |
| --- | --- |
| GET v1/config | enabled、gift、pool_limit、pool_remaining |
| GET v1/update/macos | 最新版本、Build、说明、下载地址与 SHA-256；无需登录 |
| GET v1/update/windows | Windows 最新版本、说明、GitHub 安装包地址、SHA-256 与文件大小；无需登录 |
| POST v2/login | email、password；返回token/account/message |
| POST v2/register/send | email、password；返回challenge_id/message，无账号或赠额 |
| POST v2/register/verify | email、challenge_id、code；验证后返回token/account/message |
| POST v2/reset/send | email；返回challenge_id/message |
| POST v2/reset/verify | email、challenge_id、code、new_password；撤销会话，重新登录 |
| POST v2/bind/send | 旧会话 + email |
| POST v2/bind/verify | 旧会话 + email、challenge_id、code；补齐总赠額，替换会话 |
| GET v1/me | account包含email、username、granted、used、remaining |
| POST v1/profile | 登录后保存 profile_name 与 avatar_data；昵称和压缩后的 JPEG/PNG 头像按账号写入数据库，GET v1/me 可读取 |
| POST v1/logout | 撤销当前会话 |
| POST v1/password | old_password、new_password；撤销全部会话 |
| POST v1/translate | text字符串数组、source、target、request_id UUID |

macOS v2.6.2 Build 80 安装包通过 `https://n5v1b.cn/yike-download/Yike-macOS-arm64-v2.6.2-build80.dmg` 提供同域下载。Nginx 将版本固定路径映射到 `/opt/yike-trial/public/Yike-macOS-arm64-v2.6.2-build80.dmg`。每次发布 macOS 版本都必须先上传并核对服务器 DMG 的 SHA-256，再更新服务器 `update-macos.json` 和对应 Nginx 路径；GitHub Release 同时保留安装包下载入口。

翻译仅支持 `en`、`zh-CN`、`ja`、`ko`、`de`、`fr` 且源目标不同，自动语言检测由客户端完成。单次最多40段、5000 Unicode字符，每账号每分钟5次。网络结果不确定时同内容使用同request_id重试。成功结果缓存10分钟，过期后同ID不重复扣费；上游失败退回个人体验额度，不确定是否已被上游计费时保守保留总池预算占用。

不保存原文；译文短时缓存，过期后定期清理；密码存scrypt摘要，会话/验证码存HMAC摘要。验证码、密码、会话和邮件授权码不进入日志。

## 测试

```bash
python3 -m unittest discover -s server -p 'test_*.py' -v
```

测试使用临时数据库和模拟邮件/上游，覆盖邮箱发放条件、重复/并发验证、过期/尝试上限、无外部验证码调用、限流、升级保留账号、密码找回撤销会话、旧账号补齐、四密钥选择和不确定失败禁止换key重发。生产真实收信、真人交互和DeepL成功翻译须在管理员完成配置后另行验收。
