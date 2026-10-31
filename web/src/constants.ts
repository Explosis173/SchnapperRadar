import type { AiVerdict, DeliveryPreference, ItemCondition, SortOrder } from './api/types'

export const CONDITION_LABELS: Record<ItemCondition, string> = {
  Unknown: 'Unbekannt',
  NewWithTags: 'Neu mit Etikett',
  New: 'Neu',
  VeryGood: 'Sehr gut',
  Good: 'Gut',
  Acceptable: 'In Ordnung',
  Defective: 'Defekt',
}

export const SELECTABLE_CONDITIONS: ItemCondition[] = ['NewWithTags', 'New', 'VeryGood', 'Good', 'Acceptable']

export const VERDICT_LABELS: Record<AiVerdict, string> = {
  NotAssessed: 'Nicht KI-geprüft',
  Buy: 'Zuschlagen',
  Check: 'Prüfen',
  Avoid: 'Finger weg',
}

export const DELIVERY_LABELS: Record<DeliveryPreference, string> = {
  Any: 'Egal (günstigste Option)',
  ShippingOnly: 'Nur Versand',
  PickupOnly: 'Nur Abholung',
}

export const SORT_LABELS: Record<SortOrder, string> = {
  BestDeal: 'Bester Deal',
  TotalCostAsc: 'Günstigste Gesamtkosten',
  Newest: 'Neueste',
  MostTrusted: 'Vertrauenswürdigste Verkäufer',
}

/** Nur Links auf die echten Plattformen öffnen (Defense in Depth gegen manipulierte URLs). */
const ALLOWED_LINK_HOSTS = [/^www\.kleinanzeigen\.de$/, /^www\.vinted\.[a-z.]{2,6}$/]

export function safeListingUrl(url: string): string | null {
  try {
    const u = new URL(url)
    return u.protocol === 'https:' && ALLOWED_LINK_HOSTS.some((r) => r.test(u.hostname)) ? u.toString() : null
  } catch {
    return null
  }
}

const euro = new Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' })
export const formatEuro = (v: number | null | undefined): string => (v == null ? '–' : euro.format(v))
