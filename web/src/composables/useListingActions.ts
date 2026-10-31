import type { Ref } from 'vue'
import { api } from '../api/client'
import type { Listing } from '../api/types'

/** Merken/Ausblenden – gemeinsam für Feed, Suche und Gemerkt. */
export function useListingActions(items: Ref<Listing[]>, error: Ref<string | null>) {
  async function hide(listing: Listing): Promise<void> {
    try {
      await api.hide(listing.id)
      items.value = items.value.filter((l) => l.id !== listing.id)
    } catch (e) {
      error.value = e instanceof Error ? e.message : String(e)
    }
  }

  async function toggleSave(listing: Listing): Promise<void> {
    try {
      const { saved } = await api.toggleSave(listing.id)
      items.value = items.value.map((l) => (l.id === listing.id ? { ...l, isSaved: saved } : l))
    } catch (e) {
      error.value = e instanceof Error ? e.message : String(e)
    }
  }

  return { hide, toggleSave }
}
