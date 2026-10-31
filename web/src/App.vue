<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'

const route = useRoute()
const showNav = computed(() => route.name !== 'login')

const tabs = [
  { to: '/', label: 'Feed', icon: '🔥' },
  { to: '/search', label: 'Suche', icon: '🔍' },
  { to: '/saved', label: 'Gemerkt', icon: '★' },
  { to: '/settings', label: 'Einstellungen', icon: '⚙' },
] as const
</script>

<template>
  <div class="shell" :class="{ 'with-nav': showNav }">
    <nav v-if="showNav" class="nav">
      <span class="brand">🎯 SchnapperRadar</span>
      <RouterLink v-for="t in tabs" :key="t.to" :to="t.to" class="tab">
        <span class="icon">{{ t.icon }}</span><span class="label">{{ t.label }}</span>
      </RouterLink>
    </nav>
    <main class="content">
      <RouterView />
    </main>
  </div>
</template>
