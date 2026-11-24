/**
 * Downloads the user cleared from a session's list, by client id, kept in localStorage. Clearing only hides rows in
 * this browser: the Manager keeps the download (its history, the dashboard and the Distributor rely on it).
 */
const storageKey = (sessionId: string) => `ms-vid-dldr.hidden.${sessionId}`

export function loadHidden(sessionId: string): Set<string> {
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey(sessionId)) ?? '[]') as unknown
    return new Set(Array.isArray(saved) ? saved.filter((id): id is string => typeof id === 'string') : [])
  } catch {
    return new Set()
  }
}

export function saveHidden(sessionId: string, hidden: Set<string>) {
  localStorage.setItem(storageKey(sessionId), JSON.stringify([...hidden]))
}
