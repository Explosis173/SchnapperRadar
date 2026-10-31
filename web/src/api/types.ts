export type Platform = 'Kleinanzeigen' | 'Vinted'
export type PlatformSet = 'None' | 'Kleinanzeigen' | 'Vinted' | 'All'
export type ItemCondition = 'Unknown' | 'NewWithTags' | 'New' | 'VeryGood' | 'Good' | 'Acceptable' | 'Defective'
export type DeliveryMode = 'Unknown' | 'Shipping' | 'Pickup'
export type DeliveryPreference = 'Any' | 'ShippingOnly' | 'PickupOnly'
export type AiVerdict = 'NotAssessed' | 'Buy' | 'Check' | 'Avoid'
export type PipelineStage = 'Collected' | 'Enriched' | 'AiAssessed'
export type SortOrder = 'BestDeal' | 'TotalCostAsc' | 'Newest' | 'MostTrusted'

export interface Seller {
  name: string | null
  isCommercial: boolean
  ratingPercent: number | null
  ratingCount: number | null
  badges: string[]
  memberSince: string | null
  soldItems: number | null
  isLoaded: boolean
}

export interface Cost {
  total: number
  mode: DeliveryMode
  shipping: number
  shippingIsEstimate: boolean
  buyerFee: number
  pickupTravel: number
  distanceKm: number | null
}

export interface Score {
  final: number
  deal: number
  trust: number
  aiLegitimacy: number | null
  aiDealQuality: number | null
  stage: PipelineStage
  aiVerdict: AiVerdict
  aiSummary: string | null
  aiModel: string | null
}

export interface Listing {
  id: string
  platform: Platform
  url: string
  title: string
  price: number | null
  isNegotiable: boolean
  imageUrl: string | null
  imageCount: number
  city: string | null
  postalCode: string | null
  condition: ItemCondition
  brand: string | null
  size: string | null
  marketReferencePrice: number | null
  seller: Seller
  cost: Cost
  score: Score
  redFlags: string[]
  postedAt: string | null
  isSaved: boolean
}

export interface PipelineStats {
  collected: number
  passedPrefilter: number
  enriched: number
  aiAssessed: number
  aiFromCache: number
  excluded: number
}

export interface SearchResponse {
  items: Listing[]
  stats: PipelineStats
  marketReference: number | null
  warnings: string[]
}

export interface FeedPage {
  items: Listing[]
  nextOffset: number
  hasMore: boolean
}

export interface SearchRequest {
  query: string
  platforms: PlatformSet
  minPrice: number | null
  maxPrice: number | null
  maxTotalCost: number | null
  delivery: DeliveryPreference
  maxDistanceKm: number | null
  conditions: ItemCondition[]
  minTrustScore: number | null
  onlyPrivateSellers: boolean
  excludeDefective: boolean
  includeSuspicious: boolean
  useAi: boolean
  sort: SortOrder
}

export interface Interest {
  id: string
  query: string
  minPrice: number | null
  maxPrice: number | null
  platforms: PlatformSet
  enabled: boolean
  lastRunAt: string | null
}

export interface AiUsage {
  calls: number
  inputTokens: number
  outputTokens: number
}

export interface SystemStatus {
  aiEnabled: boolean
  aiProvider: string
  aiModel: string
  homePostalCode: string
  usage24h: AiUsage
  usage30d: AiUsage
  backgroundRefresh: boolean
  refreshMinutes: number
  feedRefreshing: boolean
}
