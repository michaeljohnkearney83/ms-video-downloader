import pytest

from overall_progress import OverallProgress, part_fraction


def test_single_part_is_its_own_percent():
    progress = OverallProgress()
    progress.reset([None])
    assert progress.percent(0.425) == 42.5
    assert progress.part_finished() is True


def test_parts_are_weighted_by_size_and_run_in_order():
    progress = OverallProgress()
    progress.reset([750, 250])
    assert progress.percent(1.0) == 75.0
    assert progress.part_finished() is False
    assert progress.percent(0.0) == 75.0
    assert progress.percent(0.5) == 87.5
    assert progress.part_finished() is True


def test_unknown_sizes_weigh_parts_equally():
    progress = OverallProgress()
    progress.reset([1000, None])
    assert progress.percent(0.5) == 25.0


def test_part_sizes_from_info():
    assert OverallProgress.part_sizes({"requested_formats": [{"filesize": 5}, {"filesize_approx": 3}, {}]}) == [5, 3, None]
    assert OverallProgress.part_sizes({"filesize": 9}) == [9]
    assert OverallProgress.part_sizes(None) == [None]


@pytest.mark.parametrize("msg, text, expected", [
    ({"downloaded_bytes": 25, "total_bytes": 100}, "10.0%", 0.25),
    ({"downloaded_bytes": 50, "total_bytes_estimate": 200}, None, 0.25),
    ({}, " 42.0%", 0.42),
    ({}, None, 0.0),
])
def test_part_fraction(msg, text, expected):
    assert part_fraction(msg, text) == pytest.approx(expected)
