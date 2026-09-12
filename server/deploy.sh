#!/usr/bin/env bash
set -euo pipefail
# Run as root on the existing server after copying this directory to /opt/yike-trial.
cd /opt/yike-trial
python3 -m unittest discover -s . -p 'test_*.py' -v
id yike-trial >/dev/null 2>&1 || useradd --system --home-dir /var/lib/yike-trial --shell /usr/sbin/nologin yike-trial
if [[ ! -f /etc/yike-trial.env ]]; then
  python3 - <<'PY'
import os, secrets
fd=os.open('/etc/yike-trial.env',os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
with os.fdopen(fd,'w') as f:
    f.write('YIKE_SECRET='+secrets.token_hex(32)+'\nYIKE_PUBLIC_ENABLED=0\nYIKE_TRIAL_GIFT=200000\nDEEPL_API_KEY=\n')
PY
fi
install -m 644 yike-trial.service /etc/systemd/system/yike-trial.service
systemctl daemon-reload
systemctl enable yike-trial
systemctl restart yike-trial
python3 - <<'PY'
from pathlib import Path
p=Path('/etc/nginx/conf.d/n5v1b.cn.conf')
s=p.read_text()
Path('/opt/yike-trial/nginx-predeploy.conf').write_text(s)
if '# BEGIN YIKE TRIAL API' not in s:
    backup=Path('/opt/yike-trial/nginx-before-yike.conf')
    if not backup.exists(): backup.write_text(s)
    marker='    root /var/www/dessert-duel-site;'
    assert s.count(marker)==1
    location='''    # BEGIN YIKE TRIAL API
    location ^~ /yike-api/ {
        proxy_pass http://127.0.0.1:8093/;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header Host $host;
        proxy_set_header Connection "";
        proxy_http_version 1.1;
        proxy_read_timeout 55s;
        proxy_connect_timeout 5s;
        client_max_body_size 32k;
        client_body_timeout 8s;
        limit_req zone=yike_api burst=12 nodelay;
        access_log off;
    }
    # END YIKE TRIAL API

'''
    p.write_text(s.replace(marker,location+marker))
Path('/etc/nginx/conf.d/yike-rate.conf').write_text('limit_req_zone $binary_remote_addr zone=yike_api:1m rate=3r/s;\n')
PY
if nginx -t; then
  systemctl reload nginx
else
  cp /opt/yike-trial/nginx-predeploy.conf /etc/nginx/conf.d/n5v1b.cn.conf
  exit 1
fi
curl --fail --silent --retry 3 --retry-connrefused --retry-delay 1 http://127.0.0.1:8093/v1/config
