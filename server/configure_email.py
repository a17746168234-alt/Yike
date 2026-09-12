"""Run interactively on the server; credentials never enter source control."""
import getpass, os, pathlib, re, smtplib, ssl, subprocess, tempfile
if os.geteuid()!=0: raise SystemExit('请使用 sudo python3 /opt/yike-trial/configure_email.py')
values={}
values['SMTP_HOST']=input('SMTP 服务器（SSL）：').strip()
values['SMTP_PORT']=input('SSL 端口（默认465）：').strip() or '465'
values['SMTP_USER']=input('SMTP 用户名：').strip()
values['SMTP_PASSWORD']=getpass.getpass('SMTP 密码/授权码（不显示）：')
values['SMTP_FROM']=input('已验证的发件邮箱：').strip()
values['YIKE_MAIL_DAILY_LIMIT']=input('Yike 每日发信上限（默认50，请按免费额度设置）：').strip() or '50'
values['YIKE_MAIL_MONTHLY_LIMIT']=input('Yike 滚动31天发信上限（默认1000）：').strip() or '1000'
if any(not value or any(c in value for c in '\r\n\x00') for value in values.values()): raise SystemExit('配置格式无效，未保存。')
if not re.fullmatch(r'[^\s<>@]+@[^\s<>@]+\.[^\s<>@]+',values['SMTP_FROM']): raise SystemExit('发件邮箱无效。')
try:
    assert 1<=int(values['SMTP_PORT'])<=65535
    assert 1<=int(values['YIKE_MAIL_DAILY_LIMIT'])<=100
    assert 1<=int(values['YIKE_MAIL_MONTHLY_LIMIT'])<=3000
    with smtplib.SMTP_SSL(values['SMTP_HOST'],int(values['SMTP_PORT']),context=ssl.create_default_context(),timeout=15) as smtp:
        smtp.login(values['SMTP_USER'],values['SMTP_PASSWORD'])
except Exception: raise SystemExit('SMTP 登录或免费发信上限检查失败，未保存。请核对配置。')
if input('已核对供应商免费额度并关闭付费超额，输入 SAVE 保存（不会发送测试邮件）：')!='SAVE': raise SystemExit('未保存。')
p=pathlib.Path('/etc/yike-trial.env'); lines=[line for line in p.read_text().splitlines() if line.split('=',1)[0] not in values]
# Double-quoted systemd EnvironmentFile values preserve spaces and escaped quotes/backslashes.
lines += [name+'="'+value.replace('\\','\\\\').replace('"','\\"')+'"' for name,value in values.items()]
fd,temp=tempfile.mkstemp(prefix='.yike-mail-',dir='/etc'); os.fchmod(fd,0o600)
with os.fdopen(fd,'w') as f: f.write('\n'.join(lines)+'\n')
os.replace(temp,p); subprocess.run(['systemctl','restart','yike-trial'],check=True)
print('邮件配置已保存。请在 Yike 使用自己的邮箱验证收信。')
