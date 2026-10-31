<script setup lang="ts">
import { computed } from 'vue'
import type { Listing } from '../api/types'
import { CONDITION_LABELS, VERDICT_LABELS, formatEuro, safeListingUrl } from '../constants'
import ScoreRing from './ScoreRing.vue'

const props = defineProps<{ listing: Listing }>()
const emit = defineEmits<{ hide: [listing: Listing]; save: [listing: Listing] }>()

const l = computed(() => props.listing)
const link = computed(() => safeListingUrl(l.value.url))

const savingsPercent = computed(() => {
  const ref = l.value.marketReferencePrice
  if (!ref || ref <= 0) return null
  return Math.round((1 - l.value.cost.total / ref) * 100)
})

const deliveryText = computed(() => {
  const c = l.value.cost
  if (c.mode === 'Pickup') {
    return c.distanceKm != null
      ? `Abholung · ${c.distanceKm.toLocaleString('de-DE')} km · Fahrt ≈ ${formatEuro(c.pickupTravel)}`
      : `Abholung · Fahrt geschätzt ${formatEuro(c.pickupTravel)}`
  }
  if (c.mode === 'Shipping') {
    const parts = [`Versand ${c.shippingIsEstimate ? 'ca. ' : ''}${formatEuro(c.shipping)}`]
    if (c.buyerFee > 0) parts.push(`Käuferschutz ${formatEuro(c.buyerFee)}`)
    return parts.join(' + ')
  }
  return 'Lieferung unklar'
})

const sellerText = computed(() => {
  const s = l.value.seller
  if (!s.isLoaded) return 'Verkäufer noch nicht geprüft'
  const bits: string[] = []
  if (s.ratingPercent != null && s.ratingCount) bits.push(`${s.ratingPercent.toLocaleString('de-DE')} % (${s.ratingCount})`)
  bits.push(...s.badges.filter((b) => !b.includes('%')))
  if (s.memberSince) bits.push(`seit ${new Date(s.memberSince).getFullYear()}`)
  if (s.isCommercial) bits.push('gewerblich')
  return bits.join(' · ') || 'keine Angaben'
})

const verdictClass = computed(() => l.value.score.aiVerdict.toLowerCase())
</script>

<template>
  <article class="card">
    <a v-if="link" :href="link" target="_blank" rel="noopener noreferrer" class="media">
      <img v-if="l.imageUrl" :src="l.imageUrl" :alt="l.title" loading="lazy" referrerpolicy="no-referrer" />
      <div v-else class="noimg">Kein Bild</div>
      <span class="platform" :class="l.platform.toLowerCase()">{{ l.platform }}</span>
    </a>

    <div class="body">
      <header>
        <ScoreRing :value="l.score.final" />
        <div class="titles">
          <a v-if="link" :href="link" target="_blank" rel="noopener noreferrer" class="title">{{ l.title }}</a>
          <span v-else class="title">{{ l.title }}</span>
          <div class="meta">
            <span v-if="l.condition !== 'Unknown'">{{ CONDITION_LABELS[l.condition] }}</span>
            <span v-if="l.brand">{{ l.brand }}</span>
            <span v-if="l.size">Gr. {{ l.size }}</span>
            <span v-if="l.city">{{ l.postalCode }} {{ l.city }}</span>
          </div>
        </div>
      </header>

      <div class="price">
        <div>
          <strong>{{ formatEuro(l.cost.total) }}</strong>
          <small>bis zu dir</small>
        </div>
        <div class="sub">
          {{ formatEuro(l.price) }}<span v-if="l.isNegotiable"> VB</span> · {{ deliveryText }}
        </div>
        <div v-if="savingsPercent != null" class="savings" :class="{ neg: savingsPercent < 0 }">
          {{ savingsPercent >= 0 ? `${savingsPercent} % unter` : `${-savingsPercent} % über` }} Marktwert ({{ formatEuro(l.marketReferencePrice) }})
        </div>
      </div>

      <div class="verdict" :class="verdictClass">
        <b>{{ VERDICT_LABELS[l.score.aiVerdict] }}</b>
        <span v-if="l.score.aiSummary">{{ l.score.aiSummary }}</span>
      </div>

      <div class="seller">👤 {{ sellerText }}</div>

      <ul v-if="l.redFlags.length" class="flags">
        <li v-for="f in l.redFlags" :key="f">⚠ {{ f }}</li>
      </ul>

      <footer>
        <button type="button" class="ghost" @click="emit('hide', l)">Ausblenden</button>
        <button type="button" :class="l.isSaved ? 'primary' : 'ghost'" @click="emit('save', l)">
          {{ l.isSaved ? '★ Gemerkt' : '☆ Merken' }}
        </button>
      </footer>
    </div>
  </article>
</template>

<style scoped>
.card { background: var(--card); border: 1px solid var(--line); border-radius: 16px; overflow: hidden; display: flex; flex-direction: column; }
.media { position: relative; display: block; aspect-ratio: 4 / 3; background: var(--bg-2); }
.media img { width: 100%; height: 100%; object-fit: cover; display: block; }
.noimg { height: 100%; display: grid; place-items: center; color: var(--muted); }
.platform { position: absolute; top: 10px; left: 10px; padding: 3px 8px; border-radius: 999px; font-size: 12px; font-weight: 600; color: #fff; }
.platform.kleinanzeigen { background: #2c7a2c; }
.platform.vinted { background: #09838c; }
.body { padding: 14px; display: flex; flex-direction: column; gap: 10px; }
header { display: flex; gap: 12px; align-items: center; }
.titles { min-width: 0; }
.title { color: var(--text); font-weight: 600; text-decoration: none; display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.meta { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 4px; font-size: 12px; color: var(--muted); }
.meta span:not(:last-child)::after { content: '·'; margin-left: 6px; }
.price strong { font-size: 22px; }
.price small { margin-left: 6px; color: var(--muted); }
.sub { font-size: 13px; color: var(--muted); margin-top: 2px; }
.savings { font-size: 13px; color: var(--good); margin-top: 4px; font-weight: 600; }
.savings.neg { color: var(--bad); }
.verdict { font-size: 13px; padding: 8px 10px; border-radius: 10px; background: var(--bg-2); display: flex; flex-direction: column; gap: 2px; }
.verdict.buy { background: color-mix(in srgb, var(--good) 15%, transparent); }
.verdict.avoid { background: color-mix(in srgb, var(--bad) 15%, transparent); }
.verdict.check { background: color-mix(in srgb, var(--warn) 15%, transparent); }
.seller { font-size: 13px; color: var(--muted); }
.flags { margin: 0; padding: 0; list-style: none; font-size: 12px; color: var(--warn); display: flex; flex-direction: column; gap: 2px; }
footer { display: flex; gap: 8px; justify-content: flex-end; }
</style>
