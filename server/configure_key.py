"""Run on the server interactively. Never prints or stores the key in the project."""
import getpass, json, os, pathlib, subprocess, tempfile, urllib.request
if os.geteuid()!=0: raise SystemExit('请使用 sudo python3 /opt/yike-trial/configure_key.py')
key=getpass.getpass('输入公共体验专用 DeepL API Free 密钥（输入不显示）：').strip()
if not key.endswith(':fx'):
    raise SystemExit('当前适配官方 API Free。此密钥类型不符，未保存也未启用；请先确认它对应的服务地址和套餐，以免误用付费接口。')
request=urllib.request.Request('https://api-free.deepl.com/v2/usage',headers={'Authorization':'DeepL-Auth-Key '+key})
try:
    with urllib.request.urlopen(request,timeout=15) as response: usage=json.load(response)
    remaining=max(0,int(usage['character_limit'])-int(usage['character_count']))
except Exception: raise SystemExit('余额验证失败。未保存密钥，公共翻译仍保持关闭。')
print('官方接口返回剩余字符：',remaining)
print('Yike 每人一次赠送50,000字符，全站总池1,000,000字符；同时受上述实际余额限制。')
if input('确认这是公共专用密钥，且已确认允许对外共享服务；输入 ENABLE 开启：')!='ENABLE': raise SystemExit('未启用。')
p=pathlib.Path('/etc/yike-trial.env')
lines=[line for line in p.read_text().splitlines() if not line.startswith(('DEEPL_API_KEY=','YIKE_PUBLIC_ENABLED='))]
lines += ['DEEPL_API_KEY='+key,'YIKE_PUBLIC_ENABLED=1']
fd,temp=tempfile.mkstemp(prefix='.yike-trial-',dir='/etc')
os.fchmod(fd,0o600)
with os.fdopen(fd,'w') as f: f.write('\n'.join(lines)+'\n')
os.replace(temp,p)
subprocess.run(['systemctl','restart','yike-trial'],check=True)
print('已保存到服务器并开启公共翻译。未修改任何个人客户端密钥。')
