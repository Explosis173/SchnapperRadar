import { onBeforeUnmount, onMounted, useTemplateRef } from 'vue'

/** Ruft `loadMore` auf, sobald das Element mit `ref="<refName>"` in Sichtweite kommt. */
export function useInfiniteScroll(refName: string, loadMore: () => Promise<void>, canLoad: () => boolean): void {
  const sentinel = useTemplateRef<HTMLElement>(refName)
  let observer: IntersectionObserver | null = null
  let busy = false

  onMounted(() => {
    observer = new IntersectionObserver(
      async (entries) => {
        if (!entries.some((e) => e.isIntersecting) || busy || !canLoad()) return
        busy = true
        try {
          await loadMore()
        } finally {
          busy = false
        }
      },
      { rootMargin: '600px' },
    )
    if (sentinel.value) observer.observe(sentinel.value)
  })

  onBeforeUnmount(() => observer?.disconnect())
}
