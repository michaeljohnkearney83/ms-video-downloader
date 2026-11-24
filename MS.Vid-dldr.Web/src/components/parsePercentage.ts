/** Turns yt-dlp's percentage text (" 42.5%", "100.0%", "null", "") into 0-100, or null when unknown. */
export function parsePercentage(value: string | undefined): number | null {
  if (!value) return null
  const match = /(-?\d+(?:\.\d+)?)/.exec(value)
  if (!match) return null
  return Math.min(100, Math.max(0, Number(match[1])))
}
