<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ value: number; label?: string }>()

const tone = computed(() => (props.value >= 75 ? 'good' : props.value >= 55 ? 'ok' : 'bad'))
const dash = computed(() => `${Math.max(0, Math.min(100, props.value))} 100`)
</script>

<template>
  <div class="ring" :class="tone" :title="label ?? 'Gesamtscore'">
    <svg viewBox="0 0 36 36" aria-hidden="true">
      <path class="bg" d="M18 2.5a15.5 15.5 0 1 1 0 31a15.5 15.5 0 1 1 0-31" />
      <path class="fg" :stroke-dasharray="dash" d="M18 2.5a15.5 15.5 0 1 1 0 31a15.5 15.5 0 1 1 0-31" />
    </svg>
    <span>{{ Math.round(value) }}</span>
  </div>
</template>

<style scoped>
.ring { position: relative; width: 52px; height: 52px; flex-shrink: 0; }
svg { width: 100%; height: 100%; transform: rotate(0deg); }
path { fill: none; stroke-width: 3.2; stroke-linecap: round; }
.bg { stroke: var(--line); }
.good .fg { stroke: var(--good); }
.ok .fg { stroke: var(--warn); }
.bad .fg { stroke: var(--bad); }
span { position: absolute; inset: 0; display: grid; place-items: center; font-weight: 700; font-size: 15px; }
</style>
