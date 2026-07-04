const SUBSCRIBER_ID_KEY = 'nr_subscriber_id'

export function setSubscriberId(id: string) {
  localStorage.setItem(SUBSCRIBER_ID_KEY, id)
}

export function clearSubscriberId() {
  localStorage.removeItem(SUBSCRIBER_ID_KEY)
}

export function getSubscriberId(): string | null {
  return localStorage.getItem(SUBSCRIBER_ID_KEY)
}
