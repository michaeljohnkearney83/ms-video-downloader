"""One progress figure for a download that yt-dlp fetches in parts.

A YouTube download is usually a video stream and an audio stream, downloaded one after the other and then merged.
yt-dlp's progress hook reports each part from 0 to 100% and says "finished" after each one, so a client saw the bar
run twice, with a FINISHED in between. This turns the parts into one 0-100% figure, weighted by each part's size, and
says when the last part is done.
"""


class OverallProgress:
    def __init__(self):
        self.reset([])

    def reset(self, part_sizes):
        """part_sizes: one entry per part in download order, the size in bytes or None when unknown."""
        self._sizes = list(part_sizes) or [None]
        self._done = 0

    @staticmethod
    def part_sizes(info):
        """The parts yt-dlp will download for an extracted info dict: its requested_formats, or the video itself."""
        formats = (info or {}).get("requested_formats") or [info or {}]
        return [f.get("filesize") or f.get("filesize_approx") for f in formats]

    @property
    def parts(self):
        return len(self._sizes)

    def _weights(self):
        sizes = self._sizes
        if all(isinstance(s, (int, float)) and s > 0 for s in sizes):
            total = float(sum(sizes))
            return [s / total for s in sizes]
        return [1.0 / len(sizes)] * len(sizes)

    def percent(self, part_fraction):
        """Overall 0-100 for the current part at part_fraction (0-1). Parts download in order."""
        weights = self._weights()
        index = min(self._done, len(weights) - 1)
        fraction = min(1.0, max(0.0, part_fraction))
        return round(100.0 * (sum(weights[:index]) + weights[index] * fraction), 1)

    def part_finished(self):
        """Records that a part finished; True once every part has."""
        self._done += 1
        return self._done >= len(self._sizes)


def part_fraction(msg, percent_text):
    """0-1 for a yt-dlp 'downloading' message: from the byte counts when known, else from its percent text."""
    downloaded = msg.get("downloaded_bytes")
    total = msg.get("total_bytes") or msg.get("total_bytes_estimate")
    if isinstance(downloaded, (int, float)) and isinstance(total, (int, float)) and total > 0:
        return downloaded / total
    try:
        return float(str(percent_text).strip().rstrip("%")) / 100.0
    except (TypeError, ValueError):
        return 0.0
