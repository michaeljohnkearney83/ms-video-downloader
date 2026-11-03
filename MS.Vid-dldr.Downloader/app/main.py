import sys
import yt_dlp
import json
import re
import time

import yt_dlp.version
import producer as dlUpdates
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

    status = getProgressValue(msg, 'status')

    progress = {
        'managerId': _managerId, 
        'activity': h.getDateTimeNowHuman(),
        'status': c.DownloadStatusTypeNone if status == None else status
    }
    
    if msg['status'] == "downloading":
        pc = getProgressValue(msg, '_percent_str')
        eta = getProgressValue(msg, '_eta_str')
        if eta != "00:00" and pc != "100.0%": #BUG: - Progress hooks stop on odd occasions: i) eta - under 100% but 00:00, ii) pc - Finished status is never shown. TODO: look at part file?

            if(hasDownloadStalled(pc)):
                #global _downloadStalledTimeoutSeconds
                raise yt_dlp.DownloadCancelled(f"Downloaded cancelled after {_downloadStalledTimeoutSeconds} seconds at {pc}")

            progress['status'] = c.DownloadStatusTypeDownloading
            progress['eta'] = eta
            progress['speed'] = getProgressValue(msg, '_speed_str')
            progress['percent'] = pc
            progress['total_bytes'] = getProgressValue(msg, '_total_bytes_str')
            progress['downloaded_bytes'] = getProgressValue(msg, '_downloaded_bytes_str')
            progress['elapsed'] = getProgressValue(msg, '_elapsed_str')
            progress['default_template'] = getProgressValue(msg, '_default_template')
            progress['filename'] = getProgressValue(msg, 'filename')
        else:
            progress['status'] = c.DownloadStatusTypeFinished   
    elif msg['status'] == "finished":
        progress['status'] = c.DownloadStatusTypeFinished   
    else:
        h.printLog(f"Unknown progress status: {msg['status']}")
        progress['status'] = c.DownloadStatusTypeErrored
        progress['errorMsg'] = f"Unknown progress status: {msg['status']} - {msg}"
    
    dlUpdates.Send(appsettings.QueueDownloadUpdate, json.dumps(progress), appsettings.ChannelHost, appsettings.ChannelPortNumber)
    
def postProcessorHooks(msg):
    h.printLog(f"main.postProcessorHooks: {msg['status']}, {msg['postprocessor']}")

def sendError(msg):
    #h.printLog(f"main.sendError(msg: {msg})")

    progress = {
        'managerId': _managerId,    
        'activity': h.getDateTimeNowHuman(),
        'status': c.DownloadStatusTypeErrored,
        'errorMsg': msg
    }

    dlUpdates.Send(appsettings.QueueDownloadUpdate, json.dumps(progress), appsettings.ChannelHost, appsettings.ChannelPortNumber)

def download():
    #h.printLog("main.download(ydl_opts)")

    ydl_opts = getYtdlpOptions()

    with yt_dlp.YoutubeDL(ydl_opts) as ydl:
        
        try:
            if(analyse()):
                resetDownloadStalled()

                errorCode = ydl.download(_url)
                # errorMsg must be a string: the updater deserialises it into a C# string.
                if errorCode: sendError(f"yt-dlp download failed with error code {errorCode}")
        except Exception as e:
            errorMessage = repr(e)

            if("DownloadCancelled" in errorMessage):
                clearCancelledFiles("h-drive", _downloadId)

            sendError(errorMessage)

def analyse():
    #h.printLog("main.analyse()")

    result = True

    info = None
    
    with yt_dlp.YoutubeDL() as ydl:
        
        try:
            info = ydl.extract_info(_url, download=False)
        except Exception as e:
            h.printLog(f"Caught inner exception on ydl.extract_info when web page doesnt contain a video & then works as expected: {repr(e)}")
        finally:
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
    h.printLog(f"main.on_message_received(channel, method_frame, header_frame, body: {body})")

    channel.basic_ack(delivery_tag=method_frame.delivery_tag)

    request = {}

    if body: 

        request = json.loads(body)
        h.printLog(request)

        #h.printLog(request['messageType'])

        if request['messageType'] != c.SetVersionMessageType:

            setGlobals(request)

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
        'outtmpl': f"h-drive/{_downloadId}.%(ext)s",
        'cachedir': False,
        'overwrites': True,
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

    for fname in os.listdir(filepath):
        if fname.startswith(filename):
            os.remove(os.path.join(filepath, fname))


def main():
    #h.printLog("main.main()")

    loadAppSettings()

    connection_parameters = pika.ConnectionParameters(appsettings.ChannelHost, appsettings.ChannelPortNumber, frame_max = 131072)
    connection = pika.BlockingConnection(connection_parameters)
    channel = connection.channel()

    channel.queue_declare(appsettings.QueueRequestDownload, durable=False, exclusive=False, auto_delete=False, arguments=None)
    channel.basic_consume(queue=appsettings.QueueRequestDownload, on_message_callback=on_message_received)
    try:
        channel.start_consuming()
        h.printLog(f"Started consuming channel: {format(appsettings.QueueRequestDownload)}.")
    except KeyboardInterrupt:
        channel.stop_consuming()
        h.printLog(f"Stopped consuming channel: {format(appsettings.QueueRequestDownload)}.")

    connection.close()

if __name__ == '__main__':
    main()