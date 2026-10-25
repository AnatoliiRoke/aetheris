#!/usr/bin/env zsh
#
# Starts the AETHERIS server and the Vite client with one command (task 00005).
#
#   ./dev.sh               start both, open http://localhost:5173 when the server is ready
#   ./dev.sh --no-browser  start both without opening the browser
#
# The ports can be moved with AETHERIS_SERVER_PORT and AETHERIS_CLIENT_PORT, for example when
# another copy is already running.
#
# The script only changes its own environment: PATH and DOTNET_ROOT are set for this process
# and its children, nothing is written to the shell profile. It never prints the connection
# string. Ctrl+C stops both processes and frees the ports.

emulate -L zsh
setopt no_unset pipe_fail

readonly ROOT_DIR=${0:A:h}
readonly SERVER_DIR="$ROOT_DIR/server"
readonly CLIENT_DIR="$ROOT_DIR/client"
readonly HOST_PROJECT="$SERVER_DIR/src/Aetheris.Host"
readonly LOCAL_SETTINGS="$HOST_PROJECT/appsettings.Local.json"
readonly SERVER_PORT=${AETHERIS_SERVER_PORT:-5080}
readonly CLIENT_PORT=${AETHERIS_CLIENT_PORT:-5173}
readonly SERVER_URL="http://localhost:$SERVER_PORT"
readonly CLIENT_URL="http://localhost:$CLIENT_PORT"
readonly MIN_NODE_MAJOR=23
readonly READY_TIMEOUT_SECONDS=120
readonly STOP_TIMEOUT_SECONDS=10

open_browser=1
server_pid=""
client_pid=""
stopping=0

for argument in "$@"; do
  case $argument in
    --no-browser) open_browser=0 ;;
    -h|--help)
      print "Використання: ./dev.sh [--no-browser]"
      print "Запускає сервер ($SERVER_URL) і клієнт ($CLIENT_URL); Ctrl+C зупиняє обидва."
      exit 0
      ;;
    *)
      print -u2 "Невідомий параметр: $argument. Використання: ./dev.sh [--no-browser]"
      exit 2
      ;;
  esac
done

fail() {
  print -u2 ""
  print -u2 "✖ $1"
  [[ -n ${2:-} ]] && print -u2 "  Як виправити: $2"
  exit 1
}

info() {
  print "▸ $1"
}

# --- .NET SDK -------------------------------------------------------------------------------

if ! command -v dotnet >/dev/null 2>&1; then
  if [[ -x "$HOME/.dotnet/dotnet" ]]; then
    # Only for this process and its children; the shell profile stays as it is.
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$HOME/.dotnet:$PATH"
  else
    fail "Не знайдено .NET SDK (ні в PATH, ні в ~/.dotnet)." \
      "curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir \"\$HOME/.dotnet\""
  fi
fi

# global.json pins the SDK; a missing version makes this call fail.
if ! dotnet_version=$(cd "$SERVER_DIR" && dotnet --version 2>/dev/null); then
  fail "Встановлений .NET SDK не відповідає версії з server/global.json." \
    "встановіть SDK 10.0 тією ж командою: curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir \"\$HOME/.dotnet\""
fi
info ".NET SDK $dotnet_version"

# --- Node.js --------------------------------------------------------------------------------

if ! command -v node >/dev/null 2>&1 || ! command -v npm >/dev/null 2>&1; then
  fail "Не знайдено Node.js або npm." "brew install node"
fi

node_version=$(node -v)
node_major=${${node_version#v}%%.*}
if (( node_major < MIN_NODE_MAJOR )); then
  fail "Потрібен Node.js $MIN_NODE_MAJOR або новіший, встановлено $node_version." "brew upgrade node"
fi
info "Node.js $node_version"

# --- Connection string ----------------------------------------------------------------------

# The connection string is read only to find the address of the database; it is never printed.
connection_string=${AETHERIS_DATABASE__CONNECTIONSTRING:-}
if [[ -z $connection_string && -f $LOCAL_SETTINGS ]]; then
  connection_string=$(node -e '
    try {
      const settings = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
      process.stdout.write((settings.ConnectionStrings && settings.ConnectionStrings.Default) || "");
    } catch { process.stdout.write(""); }
  ' "$LOCAL_SETTINGS")
fi

if [[ -z $connection_string ]]; then
  fail "Не задано рядок підключення до бази." \
    "задайте змінну AETHERIS_DATABASE__CONNECTIONSTRING або створіть server/src/Aetheris.Host/appsettings.Local.json (README, «Підготовка бази»)."
fi

connection_value() {
  local key=$1 part name
  for part in ${(s:;:)connection_string}; do
    name=${${part%%=*}:l}
    name=${name// /}
    if [[ $name == $key ]]; then
      print -r -- ${part#*=}
      return 0
    fi
  done
  return 0
}

db_host=$(connection_value host)
[[ -z $db_host ]] && db_host=$(connection_value server)
[[ -z $db_host ]] && db_host=localhost
db_host=${db_host%%,*}
db_port=$(connection_value port)
[[ -z $db_port ]] && db_port=5432
unset connection_string
info "Рядок підключення знайдено (база на $db_host:$db_port)"

# --- PostgreSQL -----------------------------------------------------------------------------

if command -v pg_isready >/dev/null 2>&1; then
  if ! pg_isready -q -h "$db_host" -p "$db_port" -t 3; then
    fail "PostgreSQL недоступний на $db_host:$db_port." "brew services start postgresql@17"
  fi
elif ! nc -z "$db_host" "$db_port" >/dev/null 2>&1; then
  fail "PostgreSQL недоступний на $db_host:$db_port." "brew services start postgresql@17"
fi
info "PostgreSQL відповідає"

# --- Ports ----------------------------------------------------------------------------------

check_port() {
  local port=$1 owner
  owner=$(lsof -nP -iTCP:"$port" -sTCP:LISTEN -Fc 2>/dev/null | sed -n 's/^c//p' | head -n 1)
  if [[ -n $owner ]]; then
    fail "Порт $port уже зайнятий процесом «$owner»." "зупиніть його (lsof -nP -iTCP:$port -sTCP:LISTEN покаже PID) або виберіть інші порти: AETHERIS_SERVER_PORT=5081 AETHERIS_CLIENT_PORT=5174 ./dev.sh"
  fi
}

check_port $SERVER_PORT
check_port $CLIENT_PORT

# --- Client packages ------------------------------------------------------------------------

if [[ ! -d "$CLIENT_DIR/node_modules" ]]; then
  info "Встановлюю пакети клієнта (npm ci)..."
  (cd "$CLIENT_DIR" && npm ci --no-audit --no-fund) || fail "npm ci не вдався." "перевірте мережу й запустіть npm ci у client/ вручну."
fi

# --- Start and stop -------------------------------------------------------------------------

# Every line of a process gets a prefix, so the two logs stay readable side by side.
prefix_lines() {
  local label=$1 line
  while IFS= read -r line || [[ -n $line ]]; do
    print -r -- "[$label] $line"
  done
}

# Stops a process and every process it started: `dotnet run` and `npm` run the real
# servers as their children.
kill_tree() {
  local pid=$1 signal=$2 child
  for child in $(pgrep -P "$pid" 2>/dev/null); do
    kill_tree "$child" "$signal"
  done
  kill -s "$signal" "$pid" 2>/dev/null || true
}

is_running() {
  [[ -n $1 ]] && kill -0 "$1" 2>/dev/null
}

stop_all() {
  (( stopping )) && return 0
  stopping=1
  trap '' INT TERM

  print ""
  info "Зупиняю сервер і клієнт..."

  local pid
  for pid in $server_pid $client_pid; do
    is_running "$pid" && kill_tree "$pid" TERM
  done

  local waited=0
  while (( waited < STOP_TIMEOUT_SECONDS )) && { is_running "$server_pid" || is_running "$client_pid"; }; do
    sleep 1
    (( waited++ ))
  done

  for pid in $server_pid $client_pid; do
    is_running "$pid" && kill_tree "$pid" KILL
  done

  wait 2>/dev/null || true

  local port busy=0
  for port in $SERVER_PORT $CLIENT_PORT; do
    if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
      print -u2 "✖ Порт $port досі зайнятий: lsof -nP -iTCP:$port -sTCP:LISTEN"
      busy=1
    fi
  done

  (( busy )) || info "Зупинено, порти $SERVER_PORT і $CLIENT_PORT вільні."
}

trap 'stop_all; exit 130' INT
trap 'stop_all; exit 143' TERM

info "Запускаю сервер на $SERVER_URL..."
(cd "$SERVER_DIR" && exec dotnet run --project src/Aetheris.Host --launch-profile http -- --urls "$SERVER_URL") > >(prefix_lines server) 2>&1 &
server_pid=$!

info "Запускаю клієнт на $CLIENT_URL..."
(cd "$CLIENT_DIR" && AETHERIS_SERVER_URL="$SERVER_URL" exec npm run dev -- --port "$CLIENT_PORT" --strictPort) > >(prefix_lines client) 2>&1 &
client_pid=$!

# The first start applies the migrations and creates the world, so the server may need a while.
ready=0
for (( second = 0; second < READY_TIMEOUT_SECONDS; second++ )); do
  if ! is_running "$server_pid"; then
    stop_all
    fail "Сервер зупинився під час запуску." "подивіться рядки [server] вище (найчастіше база недоступна або немає бази aetheris_dev: createdb aetheris_dev)."
  fi
  if curl -fsS -o /dev/null "$SERVER_URL/health" 2>/dev/null; then
    ready=1
    break
  fi
  sleep 1
done

if (( ! ready )); then
  stop_all
  fail "Сервер не відповів на $SERVER_URL/health за $READY_TIMEOUT_SECONDS с." "подивіться рядки [server] вище."
fi

info "Готово: $CLIENT_URL (API $SERVER_URL). Ctrl+C зупиняє обидва процеси."
(( open_browser )) && command -v open >/dev/null 2>&1 && open "$CLIENT_URL"

# When one of the processes ends by itself, the other one is stopped too.
while is_running "$server_pid" && is_running "$client_pid"; do
  sleep 1
done

if is_running "$server_pid"; then
  print -u2 "✖ Клієнт зупинився; зупиняю сервер."
else
  print -u2 "✖ Сервер зупинився; зупиняю клієнт."
fi

stop_all
exit 1
