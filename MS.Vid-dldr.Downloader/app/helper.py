import logging
import constants as c
import datetime as dt
import appsettings
import re

_logger = logging.getLogger("ms-vid-dldr-downloader")

def printLog(msg, logThis = True):
    if appsettings.IsDebugMode or logThis: print(f"{c.MsNoticeLogPrefix}{msg}")
    # Also to Python logging: once telemetry.setup() has run this goes to Loki with the current trace id.
    text = str(msg)
    if text.startswith(("ERROR", "TODO: ERROR")):
        level = logging.ERROR
    elif "exception" in text.lower() and not text.startswith("SUCCESS"):
        level = logging.WARNING
    else:
        level = logging.INFO
    _logger.log(level, text)

def getDateTimeNow():
    return dt.datetime.now()

def getDateTimeNowHuman():
    return dt.datetime.now().strftime("%d/%m/%Y %H:%M:%S")

def getDateDiff(date1, date2):
    # total_seconds(), not .seconds: .seconds drops whole days.
    return int(abs(date2-date1).total_seconds())

def getSoftwareVersion():
    verstrline = open(c.VersionFilePath, "rt").read()
    VSRE = r"^__version__ = ['\"]([^'\"]*)['\"]"
    mo = re.search(VSRE, verstrline, re.M)
    if mo:
        return mo.group(1)
    else:
       return "Unable to find version string in %s." % (c.VersionFilePath,)
    