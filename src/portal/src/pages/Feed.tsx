import { useEffect, useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { api, type LawChangeFeedItem } from '@/lib/api'

const PAGE_SIZE = 20

export function Feed({ subscriberId }: { subscriberId: string }) {
  const [items, setItems] = useState<LawChangeFeedItem[]>([])
  const [total, setTotal] = useState(0)
  const [loading, setLoading] = useState(true)

  async function loadPage(offset: number) {
    setLoading(true)
    const res = await api.getFeed(subscriberId, PAGE_SIZE, offset)
    setItems((prev) => (offset === 0 ? res.items : [...prev, ...res.items]))
    setTotal(res.total_count)
    setLoading(false)
  }

  useEffect(() => {
    loadPage(0)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [subscriberId])

  return (
    <div className="mx-auto mt-16 w-full max-w-xl space-y-4">
      {items.length === 0 && !loading && (
        <p className="text-center text-sm text-muted-foreground">
          No law changes for your state yet.
        </p>
      )}
      {items.map((item) => (
        <Card key={item.changeId}>
          <CardHeader>
            <CardTitle className="text-base">{item.headline}</CardTitle>
            <p className="text-xs text-muted-foreground">
              {item.severity} · {new Date(item.detectedAt).toLocaleDateString()}
            </p>
          </CardHeader>
          <CardContent>
            <p className="text-sm">{item.plainEnglishSummary}</p>
          </CardContent>
        </Card>
      ))}
      {items.length < total && (
        <Button variant="secondary" className="w-full" onClick={() => loadPage(items.length)}>
          Load more
        </Button>
      )}
    </div>
  )
}
