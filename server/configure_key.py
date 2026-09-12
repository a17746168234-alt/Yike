"""Interactive server-only setup for 1–4 official public-service keys."""
import getpass, json, re, os, pathlib, subprocess, tempfile, urllib.request
if os.geteuid()!=0: raise SystemExit('请使用 sudo python3 /opt/yike-trial/configure_key.py')
try: count=int(input('本次配置几个公共专用密钥（1–4）：'))
except ValueError: raise SystemExit('请输入 1–4。')
if not 1<=count<=4: raise SystemExit('最多配置四个密钥。')
keys=[]; total=0
for index in range(count):
    key=getpass.getpass(f'第 {index+1} 个官方 DeepL API Free 密钥（输入不显示）：').strip()
    if not re.fullmatch(r'[A-Za-z0-9_-]+:fx',key):
        raise SystemExit('当前适配官方 API Free；非 Free 类型未保存，请先确认套餐和接口，避免收费。')
    if key in keys: raise SystemExit('密钥重复，未保存。')
    request=urllib.request.Request('https://api-free.deepl.com/v2/usage',headers={'Authorization':'DeepL-Auth-Key '+key})
    try:
        with urllib.request.urlopen(request,timeout=15) as response: usage=json.load(response)
        remaining=max(0,int(usage['character_limit'])-int(usage['character_count']))
    except Exception: raise SystemExit('官方余额验证失败，本次配置未保存。')
    total+=remaining; keys.append(key); print(f'第 {index+1} 份实际剩余：{remaining:,} 字符')
print(f'官方合计剩余：{total:,} 字符。每邮箱一次赠送 200,000；全站总预算 4,000,000，用完即止。')
if input('确认这些是公共专用密钥且允许共享服务，输入 ENABLE 开启：')!='ENABLE': raise SystemExit('未保存。')
p=pathlib.Path('/etc/yike-trial.env')
lines=[line for line in p.read_text().splitlines() if not line.startswith(('DEEPL_API_KEY=','DEEPL_API_KEYS=','YIKE_PUBLIC_ENABLED=','YIKE_TRIAL_GIFT='))]
# systemd EnvironmentFile retains interior JSON quotes when the value is enclosed in single quotes.
lines += ["DEEPL_API_KEYS='"+json.dumps(keys,separators=(',',':'))+"'",'DEEPL_API_KEY=','YIKE_PUBLIC_ENABLED=1']
fd,temp=tempfile.mkstemp(prefix='.yike-trial-',dir='/etc'); os.fchmod(fd,0o600)
with os.fdopen(fd,'w') as f: f.write('\n'.join(lines)+'\n')
os.replace(temp,p); subprocess.run(['systemctl','restart','yike-trial'],check=True)
print('公共密钥已保存到服务器并开启。未读取客户端个人密钥。')
