import sys
import yt_dlp
import json
import re
import time

import yt_dlp.version
import producer as dlUpdates
from observability import telemetry
from observability import health
import control
from overall_progress import OverallProgress, part_fraction

# The parts (video, audio) of the current download; set by analyse().
_overall = OverallProgress()
from opentelemetry import trace
import pika
import format as f
import constants as c
import helper as h
import appsettings
import os

# INFO: ydl_opts
# https://github.com/ytdl-org/youtube-dl/blob/3e4cedf9e8cd3157df2457df7274d0c842421945/youtube_dl/YoutubeDL.py#L137-L312
# https://github.com/ytdl-org/youtube-dl/blob/3e4cedf9e8cd3157df2457df7274d0c842421945/youtube_dl/downloader/common.py

global _managerId
global _downloadId
global _url
global _downloadlName
global _downloadlExt
global _timeoutSeconds
global _downloadStalledTimeoutSeconds
global _progressHookLastPercentage
global _progressHookLastPercentageFirstDate

# True when the Manager resumes a paused download: yt-dlp continues the partial file instead of overwriting it.
_resume = False

class MyLogger:
    def debug(self, msg):
        if msg.startswith('[debug] '):
            pass
        else:
            self.info(msg)

    def info(self, msg):
        pass

    def warning(self, msg):
        pass

    def error(self, msg):
        sendError(msg)

def getValue(obj, key):
    #h.printLog(f"main.getValue(obj, key:{key})")

    value = None

    try:
        value = obj.get(key)
    except KeyError:
        value = None

    if value == "" or value == None or value == "None" or value == 'N/A':
        return ""

    return value

def getProgressValue(obj, key):
    #h.printLog(f"main.getProgressValue(obj, key:{key})")

    value = getValue(obj, key)

    if value == "" or value == None or value == 'N/A':
        return None

    regEx = re.compile(r'\x1b[^m]*m')
    cleanText = regEx.sub('', value)
    cleanText = cleanText.strip();

    return cleanText

def progressHooks(msg):
    #h.printLog(f"main.progressHooks(msg: {msg})")

    health.worker.touch()
    stopIfRequested()

    status = getProgressValue(msg, 'status')

    progress = {
        'managerId': _managerId,
        'activity': h.getDateTimeNowHuman(),
        'status': c.DownloadStatusTypeNone if status == None else status
    }

    if msg['status'] == "downloading":
        pc = getProgressValue(msg, '_percent_str')

        if(hasDownloadStalled(pc)):
            raise yt_dlp.DownloadCancelled(f"Downloaded cancelled after {_downloadStalledTimeoutSeconds} seconds at {pc}")

        # One figure across the parts (video + audio), never 100% before download() reports FINISHED.
        overall = min(99.9, _overall.percent(part_fraction(msg, pc)))
        progress['status'] = c.DownloadStatusTypeDownloading
        progress['eta'] = getProgressValue(msg, '_eta_str')
        progress['speed'] = getProgressValue(msg, '_speed_str')
        progress['percent'] = f"{overall:.1f}%"
        progress['total_bytes'] = getProgressValue(msg, '_total_bytes_str')
        progress['downloaded_bytes'] = getProgressValue(msg, '_downloaded_bytes_str')
        progress['elapsed'] = getProgressValue(msg, '_elapsed_str')
        progress['default_template'] = getProgressValue(msg, '_default_template')
        progress['filename'] = getProgressValue(msg, 'filename')
    elif msg['status'] == "finished":
        # A part is done (for a YouTube video, the video stream and later the audio stream). FINISHED is sent once,
        # by download(), after yt-dlp has merged and moved the file: the Distributor must not see a half-made file.
        last = _overall.part_finished()
        progress['status'] = c.DownloadStatusTypeDownloading
        progress['percent'] = "99.9%" if last else f"{_overall.percent(0):.1f}%"
        progress['filename'] = getProgressValue(msg, 'filename')
    else:
        h.printLog(f"Unknown progress status: {msg['status']}")
        progress['status'] = c.DownloadStatusTypeErrored
        progress['errorMsg'] = f"Unknown progress status: {msg['status']} - {msg}"

    dlUpdates.Send(appsettings.QueueDownloadUpdate, json.dumps(progress), appsettings.ChannelHost, appsettings.ChannelPortNumber)

def stopIfRequested():
    """Raises StopRequested when the Manager asked to pause or cancel this download."""
    action = control.requests.take(_managerId)
    if action:
        raise control.StopRequested(action, _managerId)

def stopped(action, when):
    """The job stopped for a user request: no ERRORED update (the Manager already set PAUSED / CANCELLED)."""
    h.printLog(f"{action} download {_managerId} {when}")
    trace.get_current_span().set_attribute("vd.control.action", action)
    health.worker.job_stopped(action)
    if action == control.CANCEL:
        clearCancelledFiles(appsettings.DownloadFolder, _downloadId)

def onControlRequest(managerId, action, downloadId):
    """Listener thread. A paused download has no running job to clean up, so cancelling it deletes its partial file
    here; a running job is stopped (and cleaned up) by its own progress hook."""
    if action == control.CANCEL and downloadId and health.worker.current_manager_id() != managerId:
        clearCancelledFiles(appsettings.DownloadFolder, downloadId)
        h.printLog(f"Deleted the partial files of cancelled download {managerId}")

def sendFinished():
    dlUpdates.Send(appsettings.QueueDownloadUpdate, json.dumps({
        'managerId': _managerId,
        'activity': h.getDateTimeNowHuman(),
        'status': c.DownloadStatusTypeFinished
    }), appsettings.ChannelHost, appsettings.ChannelPortNumber)

def postProcessorHooks(msg):
    h.printLog(f"main.postProcessorHooks: {msg['status']}, {msg['postprocessor']}")

def sendError(msg):
    #h.printLog(f"main.sendError(msg: {msg})")

    telemetry.mark_error(msg)
    health.worker.job_failed(msg)
    h.printLog(f"ERROR: download {_managerId}: {msg}")

    progress = {
        'managerId': _managerId,
        'activity': h.getDateTimeNowHuman(),
        'status': c.DownloadStatusTypeErrored,
        'errorMsg': msg
    }

    dlUpdates.Send(appsettings.QueueDownloadUpdate, json.dumps(progress), appsettings.ChannelHost, appsettings.ChannelPortNumber)

@telemetry.traced("yt-dlp download")
def download():
    #h.printLog("main.download(ydl_opts)")

    ydl_opts = getYtdlpOptions()

    with yt_dlp.YoutubeDL(ydl_opts) as ydl:

        try:
            if(analyse()):
                resetDownloadStalled()
                stopIfRequested()  # paused / cancelled while it was being analysed
                # The share folder may have been deleted since the last download: make it again.
                os.makedirs(appsettings.DownloadFolder, exist_ok=True)

                errorCode = ydl.download(_url)
                # errorMsg must be a string: the updater deserialises it into a C# string.
                if errorCode: sendError(f"yt-dlp download failed with error code {errorCode}")
                else: sendFinished()
        except control.StopRequested as stop:
            # Pause keeps the .part file for resume; cancel deletes it.
            stopped(stop.action, "while downloading")
        except Exception as e:
            errorMessage = repr(e)

            if("DownloadCancelled" in errorMessage):
                clearCancelledFiles(appsettings.DownloadFolder, _downloadId)

            sendError(errorMessage)

@telemetry.traced("yt-dlp analyse")
def analyse():
    #h.printLog("main.analyse()")

    result = True

    info = None

    # The download's own format choice, so requested_formats lists the parts the download will fetch.
    options = getYtdlpOptions()
    with yt_dlp.YoutubeDL({'format': options['format'], 'format_sort': options['format_sort']}) as ydl:

        try:
            info = ydl.extract_info(_url, download=False)
        except Exception as e:
            h.printLog(f"Caught inner exception on ydl.extract_info when web page doesnt contain a video & then works as expected: {repr(e)}")
        finally:
            _overall.reset(OverallProgress.part_sizes(info) if info else [])
            if info:
                liveStatus = str(getValue(info, "live_status"))

                if(liveStatus != "is_live"):
                    dlUpdates.Send(appsettings.QueueDownloadUpdate, json.dumps({
                        "managerId": _managerId,
                        "videoId": getValue(info, "id"),
                        "videoTitle": getValue(info, "title"),
                        "videoDescription": getValue(info, "description"),
                        "videoDuration": getValue(info, "duration_string"),
                        "videoThumbnail": getValue(info, "thumbnail"),
                        "videoUrl": getValue(info, "url"),
                        "videoResolution": getValue(info, "resolution"),
	                    "videoAspectRatio": str(getValue(info, "aspect_ratio")),
	                    "videoFilesizeApprox": str(getValue(info, "filesize_approx")),
                        "activity": h.getDateTimeNowHuman(),
                        "status": c.DownloadStatusTypeAnalysis
                    }), appsettings.ChannelHost, appsettings.ChannelPortNumber)
                else:
                    sendError("Live stream videos not supported.")
                    result = False
            else:
                sendError("Analysis failed, info object returned null.")
                result = False

    return result

@telemetry.traced("yt-dlp extract info")
def extract():
    #h.printLog("main.extract()")

    info = None

    ydl_opts = getYtdlpOptions()

    with yt_dlp.YoutubeDL(ydl_opts) as ydl:

        try:
            info = ydl.extract_info(_url, download=False)
        except Exception as e:
            h.printLog(f"Caught inner exception on ydl.extract_info when web page doesnt contain a video & then works as expected: {repr(e)}")
        finally:
            if info:
                dlUpdates.Send(appsettings.QueueDownloadUpdate, json.dumps({
                    "managerId": _managerId,
                    "extractInfo": { "formats": getValue(info, "formats") },
                    "status": c.RequestExtractInfoMessageType
                }), appsettings.ChannelHost, appsettings.ChannelPortNumber)
            else:
                sendError("Extraction failed, info object returned null.")

def on_message_received(channel, method_frame, header_frame, body):
    """Runs the message in a consumer span that continues the trace in its traceparent header."""
    headers = getattr(header_frame, "headers", None)
    queue = getattr(method_frame, "routing_key", None) or appsettings.QueueRequestDownload
    with telemetry.consume_span(queue, headers):
        try:
            handle_message(channel, method_frame, header_frame, body)
        finally:
            health.worker.job_finished()

def handle_message(channel, method_frame, header_frame, body):
    h.printLog(f"main.on_message_received(channel, method_frame, header_frame, body: {body})")

    channel.basic_ack(delivery_tag=method_frame.delivery_tag)

    request = {}

    if body:

        request = json.loads(body)
        h.printLog(request)

        #h.printLog(request['messageType'])

        span = trace.get_current_span()
        span.set_attribute(telemetry.MESSAGE_TYPE, str(request.get('messageType')))
        telemetry.messages.add(1, {telemetry.MESSAGE_TYPE: str(request.get('messageType'))})

        if request['messageType'] != c.SetVersionMessageType:

            setGlobals(request)
            health.worker.job_started(request['messageType'], _managerId, None if _downloadId == -1 else str(_downloadId))
            if _resume:
                control.requests.clear(_managerId)
            else:
                action = control.requests.take(_managerId)
                if action:
                    stopped(action, "before it started")
                    return
            span.set_attribute(telemetry.DOWNLOAD_ID, _managerId)
            if _downloadId != -1: span.set_attribute(telemetry.CLIENT_ID, str(_downloadId))

            if request['messageType'] == c.DownloadRequestMessageType:
                download()
            elif request['messageType'] == c.AnalyseDownloadRequestMessageType:
                analyse()
            elif request['messageType'] == c.RequestExtractInfoMessageType:
                extract()
            else:
                sendError(f"Message type not supported: {request['messageType']}")
        else:

            request['downloaderApp'] = h.getSoftwareVersion()
            request['downloaderFramework'] = f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}"
            request['downloaderYtdlp'] = yt_dlp.version.__version__
            request['status'] = c.SetVersionMessageType

            dlUpdates.Send(appsettings.QueueDownloadUpdate, json.dumps(request), appsettings.ChannelHost, appsettings.ChannelPortNumber)
    else:
        h.printLog(f"TODO: ERROR: empty body")

def setGlobals(request):
    #h.printLog("main.setGlobals(request)")

    global _managerId
    _managerId = request['managerId'] if not (request.get('managerId') is None) else -1
    global _downloadId
    _downloadId = request['downloadId'] if not (request.get('downloadId') is None) else -1
    global _resume
    _resume = bool(request.get('resume'))
    global _url
    _url = request['url'] if not (request.get('url') is None) else -1
    global _downloadlName
    _downloadlName = request['downloadName'] if not (request.get('downloadName') is None) else -1
    global _downloadlExt
    _downloadlExt = request['downloadExt'] if not (request.get('downloadExt') is None) else appsettings.DefaultDownloadExt
    global _retryCount
    _retryCount = getIntValue(request, 'retryCount', appsettings.DefaultRetryCount)
    global _timeoutSeconds
    _timeoutSeconds = getIntValue(request, 'timeoutSeconds', appsettings.DefaultTimeoutSeconds)
    global _downloadStalledTimeoutSeconds
    _downloadStalledTimeoutSeconds = getIntValue(request, 'downloadStalledTimeoutSeconds', appsettings.DefaultDownloadStalledTimeoutSeconds)

def getIntValue(request, key, default):
    # The Manager sends these numbers as strings, which may be null or empty; yt-dlp needs ints.
    value = request.get(key)
    return int(default) if value is None or str(value).strip() == "" else int(value)

def getYtdlpOptions():
    #h.printLog("main.getYtdlpOptions()")

    return {
        'format': f'bestvideo[ext=mp4][height<=1080]+bestaudio[ext=m4a]/best[ext=mp4][height<=1080]/best[ext=mp4]',
        'format_sort': ['ext', 'res:1080', '+vbr'],
        'logger': MyLogger(),
        'progress_hooks': [progressHooks],
        'postprocessor_hooks': [postProcessorHooks],
        'verbose': True,
        'outtmpl': f"{appsettings.DownloadFolder}/{_downloadId}.%(ext)s",
        'cachedir': False,
        # A resumed download continues its .part file; anything else starts clean (overwrites implies no-continue).
        'overwrites': not _resume,
        'continuedl': _resume,
        'retries': _retryCount,
        'socket_timeout': _timeoutSeconds,
    }

def getEnvBool(name, default):
    value = os.getenv(name)
    return default if value is None else value.strip().lower() in ("true", "1", "yes")

def getEnvInt(name, default):
    value = os.getenv(name)
    return default if value is None or value.strip() == "" else int(value)

def loadAppSettings():
    #h.printLog("main.loadAppSettings()")

    # Environment values are strings: "False" is truthy and yt-dlp's retry counter needs an int, so convert them.
    appsettings.IsDebugMode = getEnvBool('IS_DEBUG_MODE', appsettings.IsDebugMode)
    h.printLog("appsettings.IsDebugMode: " + str(appsettings.IsDebugMode))
    appsettings.QueueRequestDownload = os.getenv('QUEUE_REQUEST_DOWNLOAD', appsettings.QueueRequestDownload)
    h.printLog("appsettings.QueueRequestDownload: " + str(appsettings.QueueRequestDownload))
    appsettings.QueueDownloadUpdate = os.getenv('QUEUE_DOWNLOAD_UPDATE', appsettings.QueueDownloadUpdate)
    h.printLog("appsettings.QueueDownloadUpdate: " + str(appsettings.QueueDownloadUpdate))
    appsettings.ChannelHost = os.getenv('CHANNEL_HOST', appsettings.ChannelHost)
    h.printLog("appsettings.ChannelHost: " + str(appsettings.ChannelHost))
    appsettings.ChannelPortNumber = getEnvInt('CHANNEL_PORT_NUMBER', appsettings.ChannelPortNumber)
    h.printLog("appsettings.ChannelPortNumber: " + str(appsettings.ChannelPortNumber))
    appsettings.DefaultDownloadExt = os.getenv('DEFAULT_DOWNLOAD_EXT', appsettings.DefaultDownloadExt)
    h.printLog("appsettings.DefaultDownloadExt: " + str(appsettings.DefaultDownloadExt))
    appsettings.DefaultRetryCount = getEnvInt('DEFAULT_RETRY_COUNT', appsettings.DefaultRetryCount)
    h.printLog("appsettings.DefaultRetryCount: " + str(appsettings.DefaultRetryCount))
    appsettings.DefaultTimeoutSeconds = getEnvInt('DEFAULT_TIMEOUT_SECONDS', appsettings.DefaultTimeoutSeconds)
    h.printLog("appsettings.DefaultTimeoutSeconds: " + str(appsettings.DefaultTimeoutSeconds))
    appsettings.DefaultDownloadStalledTimeoutSeconds = getEnvInt('DEFAULT_DOWNLOAD_STALLED_TIMEOUT_SECONDS', appsettings.DefaultDownloadStalledTimeoutSeconds)
    appsettings.DownloadFolder = os.getenv('DOWNLOAD_FOLDER', appsettings.DownloadFolder) or appsettings.DownloadFolder
    h.printLog("appsettings.DownloadFolder: " + str(appsettings.DownloadFolder))
    h.printLog("appsettings.DefaultDownloadStalledTimeoutSeconds: " + str(appsettings.DefaultDownloadStalledTimeoutSeconds))

def resetDownloadStalled():
    #h.printLog(f"main.resetDownloadStalled()")

    global _progressHookLastPercentage
    _progressHookLastPercentage = "0.0%"
    global _progressHookLastPercentageFirstDate
    _progressHookLastPercentageFirstDate = h.getDateTimeNow()

def hasDownloadStalled(progressPercentage):
    #h.printLog(f"main.hasDownloadStalled(progressPercentage: {progressPercentage})")

    global _downloadStalledTimeoutSeconds
    global _progressHookLastPercentage
    global _progressHookLastPercentageFirstDate

    if progressPercentage != _progressHookLastPercentage:
        _progressHookLastPercentage = progressPercentage
        _progressHookLastPercentageFirstDate = h.getDateTimeNow()

        return False
    else:
        return h.getDateDiff(h.getDateTimeNow(), _progressHookLastPercentageFirstDate) > _downloadStalledTimeoutSeconds

def clearCancelledFiles(filepath, filename):
    #h.printLog(f"main.clearCancelledFiles(filename: {filename})")

    if not os.path.isdir(filepath):
        return

    for fname in os.listdir(filepath):
        if fname.startswith(str(filename)):
            os.remove(os.path.join(filepath, fname))


def main():
    #h.printLog("main.main()")

    loadAppSettings()
    telemetry.setup(h.getSoftwareVersion())
    # /health on HEALTH_PORT (8081) and the stuck-job watchdog. A stuck job is reported as ERRORED (so the Manager
    # retries it) before the process exits and Docker restarts it.
    health.worker = health.setup(h.getSoftwareVersion(), yt_dlp.version.__version__, on_stuck=sendError)
    # Pause / cancel requests from the Manager, on a background thread.
    control.start_listener(appsettings.ChannelHost, appsettings.ChannelPortNumber, h.printLog, on_request=onControlRequest)

    connection_parameters = pika.ConnectionParameters(appsettings.ChannelHost, appsettings.ChannelPortNumber, frame_max = 131072)
    connection = pika.BlockingConnection(connection_parameters)
    channel = connection.channel()

    channel.queue_declare(appsettings.QueueRequestDownload, durable=False, exclusive=False, auto_delete=False, arguments=None)
    channel.basic_consume(queue=appsettings.QueueRequestDownload, on_message_callback=on_message_received)
    try:
        health.worker.set_consuming(True)
        channel.start_consuming()
        h.printLog(f"Started consuming channel: {format(appsettings.QueueRequestDownload)}.")
    except KeyboardInterrupt:
        channel.stop_consuming()
        h.printLog(f"Stopped consuming channel: {format(appsettings.QueueRequestDownload)}.")
    finally:
        health.worker.set_consuming(False)

    connection.close()

if __name__ == '__main__':
    main()