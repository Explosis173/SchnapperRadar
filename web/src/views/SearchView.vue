<script setup lang="ts">
import { reactive, ref } from 'vue'
import { api } from '../api/client'
import type { ItemCondition, Listing, PipelineStats, SearchRequest, SortOrder, DeliveryPreference } from '../api/types'
import ListingCard from '../components/ListingCard.vue'
import { useListingActions } from '../composables/useListingActions'
import { CONDITION_LABELS, DELIVERY_LABELS, SELECTABLE_CONDITIONS, SORT_LABELS, formatEuro } from '../constants'

const form = reactive<SearchRequest>({
  query: '',
  platforms: 'All',
  minPrice: null,
  maxPrice: null,
  maxTotalCost: null,
  delivery: 'Any',
  maxDistanceKm: null,
  conditions: [],
  minTrustScore: null,
  onlyPrivateSellers: false,
  excludeDefective: true,
  includeSuspicious: false,
  useAi: true,
  sort: 'BestDeal',
})
const useKleinanzeigen = ref(true)
const useVinted = ref(true)
const showFilters = ref(false)

const items = ref<Listing[]>([])
const stats = ref<PipelineStats | null>(null)
const market = ref<number | null>(null)
const warnings = ref<string[]>([])
const error = ref<string | null>(null)
const busy = ref(false)
const { hide, toggleSave } = useListingActions(items, error)

const sortOptions = Object.entries(SORT_LABELS) as [SortOrder, string][]
const deliveryOptions = Object.entries(DELIVERY_LABELS) as [DeliveryPreference, string][]

function toggleCondition(c: ItemCondition): void {
  form.conditions = form.conditions.includes(c) ? form.conditions.filter((x) => x !== c) : [...form.conditions, c]
}

/** Leere Number-Inputs liefern '' → null, damit die API nicht stolpert. */
const num = (v: unknown): number | null => (v === '' || v == null || Number.isNaN(Number(v)) ? null : Number(v))

async function submit(): Promise<void> {
  error.value = null
  if (!useKleinanzeigen.value && !useVinted.value) {
    error.value = 'Mindestens eine Plattform wählen.'
    return
  }
  busy.value = true
  try {
    const res = await api.search({
      ...form,
      platforms: useKleinanzeigen.value && useVinted.value ? 'All' : useKleinanzeigen.value ? 'Kleinanzeigen' : 'Vinted',
      minPrice: num(form.minPrice),
      maxPrice: num(form.maxPrice),
      maxTotalCost: num(form.maxTotalCost),
      maxDistanceKm: num(form.maxDistanceKm),
      minTrustScore: num(form.minTrustScore),
    })
    items.value = res.items
    stats.value = res.stats
    market.value = res.marketReference
    warnings.value = res.warnings
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <section>
    <form class="search" @submit.prevent="submit">
      <div class="row">
        <input v-model="form.query" type="search" placeholder="Was suchst du? z. B. PS5, Nike Dunk 42" minlength="2" maxlength="100" required />
        <button class="primary" type="submit" :disabled="busy">{{ busy ? 'Suche…' : 'Suchen' }}</button>
      </div>
      <button type="button" class="link" @click="showFilters = !showFilters">
        {{ showFilters ? '▲ Filter ausblenden' : '▼ Filter' }}
      </button>

      <div v-if="showFilters" class="filters">
        <fieldset>
          <legend>Plattformen</legend>
          <label><input v-model="useKleinanzeigen" type="checkbox" /> Kleinanzeigen</label>
          <label><input v-model="useVinted" type="checkbox" /> Vinted</label>
        </fieldset>

        <fieldset>
          <legend>Preis</legend>
          <label>Min € <input v-model="form.minPrice" type="number" min="0" step="1" /></label>
          <label>Max € <input v-model="form.maxPrice" type="number" min="0" step="1" /></label>
          <label>Max. gesamt inkl. Versand € <input v-model="form.maxTotalCost" type="number" min="0" step="1" /></label>
        </fieldset>

        <fieldset>
          <legend>Lieferung</legend>
          <select v-model="form.delivery">
            <option v-for="[value, label] in deliveryOptions" :key="value" :value="value">{{ label }}</option>
          </select>
          <label>Max. Entfernung (km) <input v-model="form.maxDistanceKm" type="number" min="0" max="1000" /></label>
        </fieldset>

        <fieldset>
          <legend>Zustand</legend>
          <label v-for="c in SELECTABLE_CONDITIONS" :key="c">
            <input type="checkbox" :checked="form.conditions.includes(c)" @change="toggleCondition(c)" /> {{ CONDITION_LABELS[c] }}
          </label>
          <label><input v-model="form.excludeDefective" type="checkbox" /> Defekte ausblenden</label>
        </fieldset>

        <fieldset>
          <legend>Verkäufer & Sicherheit</legend>
          <label>Min. Vertrauen (0–100) <input v-model="form.minTrustScore" type="number" min="0" max="100" /></label>
          <label><input v-model="form.onlyPrivateSellers" type="checkbox" /> Nur Privatverkäufer</label>
          <label><input v-model="form.includeSuspicious" type="checkbox" /> Verdächtige trotzdem zeigen</label>
          <label><input v-model="form.useAi" type="checkbox" /> KI-Prüfung für Top-Kandidaten</label>
        </fieldset>

        <fieldset>
          <legend>Sortierung</legend>
          <select v-model="form.sort">
            <option v-for="[value, label] in sortOptions" :key="value" :value="value">{{ label }}</option>
          </select>
        </fieldset>
      </div>
    </form>

    <p v-if="busy" class="muted">Durchsuche Plattformen, prüfe Verkäufer und lasse die besten Kandidaten von der KI bewerten – dauert meist 30–90 Sekunden.</p>
    <p v-if="error" class="error">{{ error }}</p>
    <ul v-if="warnings.length" class="warnings">
      <li v-for="w in warnings" :key="w">{{ w }}</li>
    </ul>

    <p v-if="stats" class="stats">
      {{ stats.collected }} gefunden → {{ stats.passedPrefilter }} nach Vorfilter → {{ stats.enriched }} Verkäufer geprüft →
      {{ stats.aiAssessed + stats.aiFromCache }} KI-bewertet · {{ stats.excluded }} Fakes/Gesuche raus
      <span v-if="market"> · Marktwert ≈ {{ formatEuro(market) }}</span>
    </p>

    <div class="grid">
      <ListingCard v-for="l in items" :key="l.id" :listing="l" @hide="hide" @save="toggleSave" />
    </div>
    <p v-if="stats && !items.length" class="muted">Nichts Passendes gefunden – Filter lockern?</p>
  </section>
</template>

<style scoped>
.search { display: flex; flex-direction: column; gap: 8px; margin-bottom: 16px; }
.row { display: flex; gap: 8px; }
.row input { flex: 1; }
.filters { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 12px; }
fieldset { border: 1px solid var(--line); border-radius: 12px; padding: 10px 12px; display: flex; flex-direction: column; gap: 8px; }
legend { padding: 0 6px; font-weight: 600; font-size: 13px; }
fieldset label { display: flex; align-items: center; gap: 8px; font-size: 14px; justify-content: space-between; }
fieldset label input[type='number'] { width: 100px; }
fieldset label:has(input[type='checkbox']) { justify-content: flex-start; }
.stats { font-size: 13px; color: var(--muted); }
.warnings { font-size: 13px; color: var(--warn); padding-left: 18px; }
</style>
