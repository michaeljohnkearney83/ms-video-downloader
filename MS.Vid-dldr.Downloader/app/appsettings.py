global IsDebugMode
IsDebugMode = True
global QueueRequestDownload
QueueRequestDownload = "ms_vd_q_downloadRequest"
global QueueDownloadUpdate
QueueDownloadUpdate = "ms_vd_q_downloadUpdate"
global ChannelHost
ChannelHost = "rabbitmq"
global ChannelPortNumber
ChannelPortNumber = 5672
global DefaultDownloadExt
DefaultDownloadExt = "mp4"
global DefaultRetryCount
DefaultRetryCount = 3
global DefaultTimeoutSeconds
DefaultTimeoutSeconds = 30
global DefaultDownloadStalledTimeoutSeconds
DefaultDownloadStalledTimeoutSeconds = 138

# Where downloads are written (the Distributor's share folder). Created when missing.
DownloadFolder = "h-drive"