#!/usr/bin/env bash
# 在已有 PostgreSQL 的云主机安装账号宿主；程序只读，数据和配置独立保留。
set -euo pipefail

if [[ $# != 2 ]]; then
  printf 'Usage: %s PUBLISH_DIRECTORY VERSION\n' "$0" >&2
  exit 2
fi
payload=$(realpath "$1")
version=$2
[[ "$version" =~ ^p[124]-[a-zA-Z0-9._-]+$ ]] || { printf 'Invalid version\n' >&2; exit 2; }
[[ -f "$payload/ProjectStar.Server" && -f "$payload/ProjectStar.Server.runtimeconfig.json" ]] || exit 2
[[ -f "$payload/rules/project-star-rules.x86_64" && -f "$payload/rules/rules-version.txt" ]] || exit 2
[[ -z "$(find "$payload" -type l -print -quit)" ]] || { printf 'Symlinks in payload rejected\n' >&2; exit 2; }
release="/opt/project-star/releases/$version"
[[ ! -e "$release" ]] || { printf 'Release already exists; use a new version\n' >&2; exit 2; }

if ! id project-star >/dev/null 2>&1; then
  sudo -n useradd --system --home-dir /var/lib/project-star --shell /usr/sbin/nologin project-star
fi
sudo -n install -d -o project-star -g project-star -m 0750 /var/lib/project-star /var/log/project-star/api
sudo -n install -d -o root -g project-star -m 0750 /etc/project-star
sudo -n install -d -o root -g root -m 0755 "$release/host"
sudo -n cp -a "$payload/." "$release/host/"
sudo -n chown -R root:root "$release"
sudo -n chmod -R a-w "$release"
sudo -n chmod a+rx "$release/host/ProjectStar.Server"

# 本机 Unix socket 使用 peer 认证：服务系统账号与数据库角色同名，无数据库密码。
if [[ $(sudo -n -u postgres psql -Atqc "SELECT count(*) FROM pg_roles WHERE rolname='project-star'") == 0 ]]; then
  sudo -n -u postgres psql -v ON_ERROR_STOP=1 -c 'CREATE ROLE "project-star" LOGIN'
fi
if [[ $(sudo -n -u postgres psql -Atqc "SELECT count(*) FROM pg_database WHERE datname='project_star'") == 0 ]]; then
  sudo -n -u postgres createdb --owner=project-star project_star
fi
if [[ ! -e /etc/project-star/api.env ]]; then
  printf '%s\n' \
    'PROJECT_STAR_DATA_DIR=/var/lib/project-star' \
    'PROJECT_STAR_DATABASE="Host=/var/run/postgresql;Database=project_star;Username=project-star"' \
    'DOTNET_ENVIRONMENT=Production' | sudo -n tee /etc/project-star/api.env >/dev/null
  sudo -n chown root:project-star /etc/project-star/api.env
  sudo -n chmod 0640 /etc/project-star/api.env
fi

# 先验证最终程序包，不覆盖现有配置；迁移失败时不切换当前程序或服务。
sudo -n -u project-star bash -c 'cd /var/lib/project-star; exec timeout 60 "$@"' _ "$release/host/rules/project-star-rules.x86_64" --headless --quiet -- --verify-save
schema_before=$(sudo -n -u postgres psql -d project_star -Atqc "SELECT to_regclass('public.schema_versions') IS NOT NULL")
if [[ "$schema_before" == t ]]; then
  schema_before=$(sudo -n -u postgres psql -d project_star -Atqc 'SELECT COALESCE(MAX(version),0) FROM schema_versions')
else
  schema_before=0
fi
sudo -n -u project-star bash -c 'set -a; source /etc/project-star/api.env; set +a; exec "$1" migrate' _ "$release/host/ProjectStar.Server"
schema_after=$(sudo -n -u postgres psql -d project_star -Atqc 'SELECT COALESCE(MAX(version),0) FROM schema_versions')

if [[ ! -e /etc/systemd/system/project-star-api.service ]]; then
  sudo -n tee /etc/systemd/system/project-star-api.service >/dev/null <<'UNIT'
[Unit]
Description=Project Star account API (loopback)
After=network.target postgresql.service
Requires=postgresql.service

[Service]
Type=simple
User=project-star
Group=project-star
WorkingDirectory=/var/lib/project-star
EnvironmentFile=/etc/project-star/api.env
ExecStart=/opt/project-star/api-current/host/ProjectStar.Server
Restart=on-failure
RestartSec=3
TimeoutStopSec=20
UMask=0077
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/var/lib/project-star /var/log/project-star/api
RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6
LimitNOFILE=4096
MemoryMax=512M
CPUQuota=200%

[Install]
WantedBy=multi-user.target
UNIT
fi

previous=$(readlink /opt/project-star/api-current || true)
sudo -n ln -s "$release" /opt/project-star/api-next
sudo -n mv -Tf /opt/project-star/api-next /opt/project-star/api-current
sudo -n systemctl daemon-reload
sudo -n systemctl enable project-star-api >/dev/null
sudo -n systemctl restart project-star-api
for attempt in {1..20}; do
  if curl --silent --fail http://127.0.0.1:5080/health/ready >/dev/null; then
    printf 'Account API ready: %s (previous: %s)\n' "$release" "${previous:-none}"
    exit 0
  fi
  sleep 1
done
if [[ "$schema_before" != "$schema_after" ]]; then
  printf 'Readiness failed after schema migration; retain current program and repair forward. Do not run an incompatible old program or downgrade player data. Inspect journalctl -u project-star-api.\n' >&2
  exit 1
elif [[ -n "$previous" ]]; then
  sudo -n ln -s "$previous" /opt/project-star/api-rollback
  sudo -n mv -Tf /opt/project-star/api-rollback /opt/project-star/api-current
  sudo -n systemctl restart project-star-api
else
  sudo -n systemctl stop project-star-api
fi
printf 'Readiness failed; restored previous program when available. Inspect journalctl -u project-star-api.\n' >&2
exit 1
