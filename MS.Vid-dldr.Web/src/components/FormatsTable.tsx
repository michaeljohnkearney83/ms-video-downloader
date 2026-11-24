import type { Format } from '../api/types'

function formatSize(bytes: number | undefined) {
  if (!bytes) return '–'
  const units = ['B', 'KB', 'MB', 'GB']
  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  return `${value.toFixed(unit === 0 ? 0 : 1)} ${units[unit]}`
}

const codec = (value: string | undefined) => (!value || value === 'none' ? '–' : value)

/** Formats yt-dlp found for a URL. Choosing one copies its extension into the download form. */
export function FormatsTable({ formats, onUseExtension }: { formats: Format[]; onUseExtension: (ext: string) => void }) {
  if (formats.length === 0) return <p className="hint">No formats were returned for this URL.</p>

  return (
    <div className="table-wrap">
      <table aria-label="Available formats">
        <thead>
          <tr>
            <th>ID</th>
            <th>Ext</th>
            <th>Resolution</th>
            <th>Video</th>
            <th>Audio</th>
            <th>Size</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {formats.map((format, index) => (
            <tr key={`${format.formatId ?? index}-${index}`}>
              <td>{format.formatId ?? '–'}</td>
              <td>{format.ext ?? '–'}</td>
              <td>{format.resolution ?? (format.height ? `${format.width ?? '?'}x${format.height}` : '–')}</td>
              <td>{codec(format.vcodec)}</td>
              <td>{codec(format.acodec)}</td>
              <td>{formatSize(format.filesize || format.filesizeApprox)}</td>
              <td>
                {format.ext && (
                  <button type="button" className="link" onClick={() => onUseExtension(format.ext!)}>
                    Use .{format.ext}
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
