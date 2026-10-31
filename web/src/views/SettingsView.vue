<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api/client'
import type { Interest, PlatformSet, SystemStatus } from '../api/types'
import { formatEuro } from '../constants'

const router = useRouter()
const status = ref<SystemStatus | null>(null)
const interests = ref<Interest[]>([])
const error = ref<string | null>(null)

const query = ref('')
const minPrice = ref<number | ''>('')
const maxPrice = ref<number | ''>('')
const platforms = ref<PlatformSet>('All')

const PLATFORM_LABELS: Record<PlatformSet, string> = { All: 'Beide', Kleinanzeigen: 'Kleinanzeigen', Vinted: 'Vinted', None: '–' }

async function load(): Promise<void> {
  try {
    ;[status.value, interests.value] = await Promise.all([api.status(), api.interests()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  }
}

async function add(): Promise<void> {
  error.value = null
  try {
    const created = await api.addInterest(
      query.value.trim(),
      minPrice.value === '' ? null : Number(minPrice.value),
      maxPrice.value === '' ? null : Number(maxPrice.value),
      platforms.value,
    )
    interests.value.push(created)
    query.value = ''
    minPrice.value = ''
    maxPrice.value = ''
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  }
}

async function toggle(i: Interest): Promise<void> {
  const updated = await api.toggleInterest(i.id)
  interests.value = interests.value.map((x) => (x.id === i.id ? updated : x))
}

async function remove(i: Interest): Promise<void> {
  await api.deleteInterest(i.id)
  interests.value = interests.value.filter((x) => x.id !== i.id)
}

async function logout(): Promise<void> {
  await api.logout()
  await router.replace({ name: 'login' })
}

onMounted(load)
</script>

<template>
  <section class="settings">
    <h1>Einstellungen</h1>
    <p v-if="error" class="error">{{ error }}</p>

    <div class="panel">
      <h2>Interessen für deinen Feed</h2>
      <form class="add" @submit.prevent="add">
        <input v-model="query" placeholder="Suchbegriff, z. B. iPhone 15 Pro" minlength="2" maxlength="100" required />
        <input v-model="minPrice" type="number" min="0" placeholder="Min €" />
        <input v-model="maxPrice" type="number" min="0" placeholder="Max €" />
        <select v-model="platforms">
          <option value="All">Beide</option>
          <option value="Kleinanzeigen">Kleinanzeigen</option>
          <option value="Vinted">Vinted</option>
        </select>
        <button class="primary" type="submit">Hinzufügen</button>
      </form>
      <ul class="interests">
        <li v-for="i in interests" :key="i.id" :class="{ off: !i.enabled }">
          <div>
            <b>{{ i.query }}</b>
            <small>
              {{ PLATFORM_LABELS[i.platforms] }}
              <template v-if="i.minPrice != null || i.maxPrice != null"> · {{ formatEuro(i.minPrice ?? 0) }} – {{ i.maxPrice != null ? formatEuro(i.maxPrice) : '∞' }}</template>
              <template v-if="i.lastRunAt"> · zuletzt {{ new Date(i.lastRunAt).toLocaleString('de-DE') }}</template>
            </small>
          </div>
          <div class="actions">
            <button type="button" class="ghost" @click="toggle(i)">{{ i.enabled ? 'Pausieren' : 'Aktivieren' }}</button>
            <button type="button" class="ghost danger" @click="remove(i)">Löschen</button>
          </div>
        </li>
        <li v-if="!interests.length" class="muted">Noch keine Interessen.</li>
      </ul>
    </div>

    <div v-if="status" class="panel">
      <h2>System</h2>
      <dl>
        <dt>KI</dt>
        <dd>{{ status.aiEnabled ? `${status.aiProvider} / ${status.aiModel}` : `aus (${status.aiModel}) – nur Heuristik` }}</dd>
        <dt>KI-Nutzung 24 h</dt>
        <dd>{{ status.usage24h.calls }} Aufrufe · {{ status.usage24h.inputTokens.toLocaleString('de-DE') }} in / {{ status.usage24h.outputTokens.toLocaleString('de-DE') }} out Tokens</dd>
        <dt>KI-Nutzung 30 T</dt>
        <dd>{{ status.usage30d.calls }} Aufrufe · {{ status.usage30d.inputTokens.toLocaleString('de-DE') }} in / {{ status.usage30d.outputTokens.toLocaleString('de-DE') }} out Tokens</dd>
        <dt>Heimat-PLZ</dt>
        <dd>{{ status.homePostalCode }} <small class="muted">(per Env <code>Home__PostalCode</code>)</small></dd>
        <dt>Auto-Feed</dt>
        <dd>{{ status.backgroundRefresh ? `alle ${status.refreshMinutes} min` : 'aus (nur manuell)' }}</dd>
      </dl>
    </div>

    <button type="button" class="ghost" @click="logout">Abmelden</button>
  </section>
</template>

<style scoped>
.settings { display: flex; flex-direction: column; gap: 16px; max-width: 760px; }
.panel { background: var(--card); border: 1px solid var(--line); border-radius: 16px; padding: 16px; }
h2 { margin-top: 0; font-size: 17px; }
.add { display: grid; grid-template-columns: 2fr 1fr 1fr 1fr auto; gap: 8px; }
@media (max-width: 640px) { .add { grid-template-columns: 1fr 1fr; } .add input:first-child { grid-column: 1 / -1; } }
.interests { list-style: none; padding: 0; margin: 12px 0 0; display: flex; flex-direction: column; gap: 8px; }
.interests li { display: flex; justify-content: space-between; align-items: center; gap: 8px; padding: 10px; border-radius: 10px; background: var(--bg-2); }
.interests li.off { opacity: 0.55; }
.interests small { display: block; color: var(--muted); }
.actions { display: flex; gap: 6px; }
dl { display: grid; grid-template-columns: max-content 1fr; gap: 6px 16px; margin: 0; font-size: 14px; }
dt { color: var(--muted); }
dd { margin: 0; }
</style>
