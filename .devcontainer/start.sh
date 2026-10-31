#!/usr/bin/env bash
# Bei jedem Start des Codespaces: App im Hintergrund starten.
set -euo pipefail
cd "$(dirname "$0")/.."

STATE="$HOME/.schnapperradar"
mkdir -p "$STATE/keys"
chmod 700 "$STATE"

# App-Passwort einmalig zufällig erzeugen und merken.
if [[ ! -s "$STATE/password" ]]; then
  head -c 32 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 20 > "$STATE/password"
  chmod 600 "$STATE/password"
fi

export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://0.0.0.0:8080
export Auth__Password="$(cat "$STATE/password")"
export Auth__CookieSecure=Always            # Codespaces liefert die Seite über HTTPS aus
export BehindProxy=true
export DataProtection__KeysPath="$STATE/keys"
export ConnectionStrings__Postgres="${ConnectionStrings__Postgres:-Host=localhost;Database=schnapperradar;Username=schnapper;Password=codespace-only}"
export Home__PostalCode="${HOME_POSTAL_CODE:-10115}"

# KI: Gemini (falls Codespaces-Secret gesetzt) → Groq (falls gesetzt) → GitHub Models (Token bringt Codespaces mit).
providers=()
add_provider() { providers+=("$1|$2"); }
[[ -n "${GEMINI_API_KEY:-}" ]] && add_provider gemini "$GEMINI_API_KEY"
[[ -n "${GROQ_API_KEY:-}" ]] && add_provider groq "$GROQ_API_KEY"
[[ -n "${GITHUB_TOKEN:-}" ]] && add_provider github "$GITHUB_TOKEN"

if (( ${#providers[@]} == 0 )); then
  export Ai__Provider=none
else
  IFS='|' read -r p k <<< "${providers[0]}"
  export Ai__Provider="$p" Ai__ApiKey="$k" Ai__Model=auto
  [[ "$p" == github ]] && export Ai__Model=openai/gpt-4.1-mini
  for i in "${!providers[@]}"; do
    (( i == 0 )) && continue
    IFS='|' read -r p k <<< "${providers[$i]}"
    n=$((i - 1))
    export "Ai__Fallbacks__${n}__Provider=$p" "Ai__Fallbacks__${n}__ApiKey=$k"
    [[ "$p" == github ]] && export "Ai__Fallbacks__${n}__Model=openai/gpt-4.1-mini"
  done
fi

# Laufende Instanz beenden (z. B. nach Codespace-Neustart), dann neu starten.
if [[ -f "$STATE/app.pid" ]] && kill -0 "$(cat "$STATE/app.pid")" 2>/dev/null; then
  kill "$(cat "$STATE/app.pid")" || true
  sleep 2
fi

echo "▶ Warte auf Datenbank …"
for _ in $(seq 1 30); do
  (exec 3<>/dev/tcp/127.0.0.1/5432) 2>/dev/null && break
  sleep 1
done

nohup dotnet run --project src/SchnapperRadar.Api -c Release --no-launch-profile > "$STATE/app.log" 2>&1 &
echo $! > "$STATE/app.pid"

ai_desc="aus (nur Heuristik)"
(( ${#providers[@]} > 0 )) && ai_desc="$(printf '%s ' "${providers[@]%%|*}" | sed 's/ $//; s/ / → /g')"

cat > "$STATE/info.txt" <<INFO

============================================================
  🎯 SchnapperRadar läuft
  Öffnen:   Reiter "PORTS" → Port 8080 → 🌐 (Globus-Symbol)
  Passwort: $(cat "$STATE/password")
  KI:       $ai_desc
  PLZ:      $Home__PostalCode
  Log:      tail -f ~/.schnapperradar/app.log
============================================================
INFO
cat "$STATE/info.txt"
