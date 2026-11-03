import datetime as dt
import json

import pytest

import format as f
import helper as h
from conftest import APP_DIR


def test_getDateDiff_is_absolute_seconds():
    a = dt.datetime(2026, 11, 3, 21, 0, 0)
    assert h.getDateDiff(a, a + dt.timedelta(seconds=90)) == 90
    assert h.getDateDiff(a + dt.timedelta(seconds=90), a) == 90


def test_getDateDiff_counts_whole_days():
    a = dt.datetime(2026, 11, 3, 21, 0, 0)
    # timedelta.seconds drops the days part, so a 1-day-and-5-second gap used to come out as 5.
    assert h.getDateDiff(a, a + dt.timedelta(days=1, seconds=5)) == 86405


def test_getDateTimeNowHuman_matches_format_the_updater_parses():
    # DownloadUpdateDto.LastActivityUtc parses this with the en-GB culture (day first).
    parsed = dt.datetime.strptime(h.getDateTimeNowHuman(), "%d/%m/%Y %H:%M:%S")
    assert abs((dt.datetime.now() - parsed).total_seconds()) < 5


def test_getSoftwareVersion_reads_version_file(monkeypatch):
    monkeypatch.chdir(APP_DIR)
    assert h.getSoftwareVersion() == "2.1.0"


def test_printLog_prefixes_message(capsys):
    h.printLog("hello")
    assert capsys.readouterr().out == " >>--> hello\n"


def test_serializeDatetime_gives_iso_format():
    assert json.dumps({"at": dt.datetime(2026, 11, 3, 21, 5, 9)}, default=f.serializeDatetime) == '{"at": "2026-11-03T21:05:09"}'


def test_serializeDatetime_rejects_other_types():
    with pytest.raises(TypeError):
        f.serializeDatetime(object())
