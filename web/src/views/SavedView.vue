<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api } from '../api/client'
import type { Listing } from '../api/types'
import ListingCard from '../components/ListingCard.vue'
import { useListingActions } from '../composables/useListingActions'

const items = ref<Listing[]>([])
const error = ref<string | null>(null)
const loaded = ref(false)
const { hide, toggleSave } = useListingActions(items, error)

onMounted(async () => {
  try {
    items.value = await api.saved()
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loaded.value = true
  }
})
</script>

<template>
  <section>
    <h1>Gemerkt</h1>
    <p v-if="error" class="error">{{ error }}</p>
    <div class="grid">
      <ListingCard v-for="l in items" :key="l.id" :listing="l" @hide="hide" @save="toggleSave" />
    </div>
    <p v-if="loaded && !items.length" class="muted">Noch nichts gemerkt.</p>
  </section>
</template>
