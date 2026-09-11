# Yike 公共 DeepL 体验服务

Python 3.11+ 标准库服务，适合与现有网站共用的小型服务器；systemd 将内存限制为 192MB、CPU 限制为单核的 30%。仅监听 127.0.0.1:8093，通过现有 Nginx 提供 HTTPS。

## 额度与当前状态

每个账号注册时一次获得 50,000 字符。全站终身总预算 1,000,000 字符，用完停止，不自动按月重置；注册不预留总池预算。实际使用同时受 DeepL 官方返回的剩余额度限制。公共调用目前关闭，可先注册登录；个人客户端密钥不参与公共服务。

目前仅适配官方 DeepL API Free（`:fx` 密钥），不调用付费接口。所谓“100 万额度”的密钥仍需核对供应商和套餐；若是 Pro 或第三方中转密钥，不能直接填入本适配器，不能仅凭标称额度认定兼容。

## 部署与运维

当前实例位于 `/opt/yike-trial`，数据库位于 `/var/lib/yike-trial/accounts.sqlite3`，私密环境文件为 `/etc/yike-trial.env`（权限 600）。不要把这两个数据文件复制到仓库或安装包。备份 SQLite 时使用 SQLite backup API；数据库与 YIKE_SECRET 应作为私密备份一并保管。

首次部署：将本目录的 Python 文件、service 和部署脚本复制到服务器 `/opt/yike-trial`，以 root 运行 `bash /opt/yike-trial/deploy.sh`。脚本针对当前 n5v1b.cn 的 Nginx 配置；其他服务器需调整域名、配置文件路径和插入位置。已有部署更新时先备份私密数据，只替换程序，保留环境文件与数据库。

在管理员自己的交互式终端配置**专用**密钥（输入不回显）：

```bash
ssh -t dessert-duel-aliyun 'sudo python3 /opt/yike-trial/configure_key.py'
```

脚本仅请求 `/usage` 验证真实额度，输入 `ENABLE` 后才保存并开启。不会获取客户端个人密钥。不是官方 Free 类型时拒绝保存，不自动切换收费接口。

暂停公共翻译：将服务器 `/etc/yike-trial.env` 中 `YIKE_PUBLIC_ENABLED=0`，再执行 `systemctl restart yike-trial`。注册、登录和已有额度继续保留。检查：

```bash
systemctl status yike-trial --no-pager
curl --fail https://n5v1b.cn/yike-api/v1/config
```

不要删除数据库来更新程序，否则会丢失账号和已使用总额。不要同时启动多个服务进程：上游请求使用单进程全局锁串行处理。

## 客户端接口（包括 Windows）

基地址 `https://n5v1b.cn/yike-api/`，JSON 请求/响应；鉴权使用 `Authorization: Bearer <token>`，会话有效 30 天。Windows 可复用接口，并将 token 存入系统凭据管理器；Apple 翻译仅适用于 macOS。

| 方法与路径 | 请求/说明 |
| --- | --- |
| GET v1/config | 无需登录，返回赠送额度、公共池余额和 enabled |
| POST v1/register | username、password；返回 token、account、message |
| POST v1/login | username、password；不会重复赠送 |
| GET v1/me | 返回当前 account |
| POST v1/logout | 撤销当前 token |
| POST v1/password | old_password、new_password；撤销全部会话 |
| POST v1/translate | text 字符串数组、source、target、request_id UUID |

账号名为 4–24 位英文、数字或下划线，大小写不敏感；密码 10–128 字符。目前没有邮箱找回，不承诺一人只能注册一个账号。IP 注册限额为每天 3 次，登录按 IP/账号限流，翻译每账号每分钟 5 次。共享网络用户也可能触发 IP 限制。

source/target 为 `en`、`zh-CN`、`ja`、`ko`，且必须不同。自动检测由客户端完成。单次最多 40 段、5,000 Unicode 字符。网络结果不确定时必须用相同 request_id 和相同内容重试；服务端缓存成功译文 10 分钟，过期后同 ID 不再返回译文，也不重复扣额。错误返回 code 和 message，客户端应显示解决提示。

密码使用带独立盐的 scrypt 摘要，会话只存 HMAC 摘要。服务不保存原文；译文缓存有效期 10 分钟，过期后定期清理。上游失败退回账号额度；无法确定上游是否计费时保留公共池预算占用，防止超支。

## 验证

```bash
python3 -m unittest discover -s server -p 'test_*.py' -v
```

测试使用临时数据库与模拟 DeepL，不消耗实际 DeepL 字符。真实共享翻译需专用密钥启用后另行验收；部署与模拟测试通过不代表未知密钥已经兼容。
