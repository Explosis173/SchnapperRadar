<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { api } from '../api/client'
import type { Listing } from '../api/types'
import ListingCard from '../components/ListingCard.vue'
import { useInfiniteScroll } from '../composables/useInfiniteScroll'
import { useListingActions } from '../composables/useListingActions'

const PAGE_SIZE = 20
const POLL_MS = 5000

const items = ref<Listing[]>([])
const offset = ref(0)
const hasMore = ref(true)
const loading = ref(false)
const refreshing = ref(false)
const error = ref<string | null>(null)
const { hide, toggleSave } = useListingActions(items, error)
let pollTimer: number | undefined

async function loadMore(): Promise<void> {
  loading.value = true
  try {
    const page = await api.feed(offset.value, PAGE_SIZE)
    const known = new Set(items.value.map((i) => i.id))
    items.value.push(...page.items.filter((i) => !known.has(i.id)))
    offset.value = page.nextOffset
    hasMore.value = page.hasMore
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
    hasMore.value = false
  } finally {
    loading.value = false
  }
}

async function reload(): Promise<void> {
  items.value = []
  offset.value = 0
  hasMore.value = true
  await loadMore()
}

async function refreshNow(): Promise<void> {
  error.value = null
  try {
    await api.refreshFeed()
    refreshing.value = true
    poll()
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  }
}

function poll(): void {
  window.clearTimeout(pollTimer)
  pollTimer = window.setTimeout(async () => {
    try {
      const status = await api.status()
      if (status.feedRefreshing || refreshing.value) {
        // Erst wenn der Lauf wirklich gestartet + beendet ist, neu laden.
        if (!status.feedRefreshing && refreshing.value) {
          refreshing.value = false
          await reload()
          return
        }
        poll()
      }
    } catch {
      refreshing.value = false
    }
  }, POLL_MS)
}

useInfiniteScroll('sentinel', loadMore, () => hasMore.value && !loading.value)

onMounted(loadMore)
onBeforeUnmount(() => window.clearTimeout(pollTimer))
</script>

<template>
  <section>
    <div class="toolbar">
      <h1>Für dich</h1>
      <button type="button" class="ghost" :disabled="refreshing" @click="refreshNow">
        {{ refreshing ? 'Sucht neue Schnapper…' : '↻ Aktualisieren' }}
      </button>
    </div>
    <p v-if="error" class="error">{{ error }}</p>

    <div class="grid">
      <ListingCard v-for="l in items" :key="l.id" :listing="l" @hide="hide" @save="toggleSave" />
    </div>

    <div ref="sentinel" class="sentinel">
      <span v-if="loading">Lade…</span>
      <div v-else-if="!items.length && !hasMore" class="empty">
        <p>Noch keine geprüften Schnapper.</p>
        <p>Leg unter <RouterLink to="/settings">Einstellungen</RouterLink> Interessen an (z. B. „iPhone 15“, „Nike Dunk“) und tipp auf „Aktualisieren“.</p>
      </div>
      <span v-else-if="!hasMore" class="muted">Das war's – mehr kommt beim nächsten Durchlauf.</span>
    </div>
  </section>
</template>

<style scoped>
.sentinel { padding: 32px 0; text-align: center; color: var(--muted); }
.empty { max-width: 420px; margin: 0 auto; }
</style>
