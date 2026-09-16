from artemis_preprocessing.gui.app import _crop_coverage_warning_message


def test_crop_coverage_warning_includes_shortfalls_and_recommendation():
    message = _crop_coverage_warning_message(
        {
            "warning_code": "insufficient_longitudinal_coverage",
            "roi_name": "PTV1_V01_1a+2cm_Ph",
            "caudal_missing_mm": 12.34,
            "cranial_missing_mm": 5.67,
        }
    )

    assert "PTV1_V01_1a+2cm_Ph" in message
    assert "Caudal: 12.3 mm additional coverage required" in message
    assert "Cranial: 5.7 mm additional coverage required" in message
    assert "larger longitudinal field of view" in message
    assert "structures were copied successfully" in message
    assert "processing will continue" in message


def test_crop_coverage_warning_shows_margin_on_covered_end():
    message = _crop_coverage_warning_message(
        {
            "warning_code": "insufficient_longitudinal_coverage",
            "roi_name": "PTV1_V01_1a+2cm_Ph",
            "caudal_missing_mm": 12.34,
            "cranial_available_mm": 8.44,
        }
    )

    assert "Longitudinal coverage of the +2 cm ring:" in message
    assert "Caudal: 12.3 mm additional coverage required" in message
    assert "Cranial: 8.4 mm available margin" in message
    assert "Cranial: 0.0 mm additional coverage required" not in message


def test_unrelated_crop_warning_does_not_create_coverage_popup():
    assert _crop_coverage_warning_message(
        {"warning_code": None, "warning": "no unique ROI"}
    ) is None


def test_reduced_in_plane_fov_warning_confirms_full_fov_is_retained():
    message = _crop_coverage_warning_message(
        {
            "warning_code": "insufficient_in_plane_crop_margin",
            "roi_name": "PTV1_V03-05_1a+2cm_Ph",
        }
    )

    assert "PTV1_V03-05_1a+2cm_Ph" in message
    assert "proposed reduced in-plane field of view" in message
    assert "full image field of view was retained" in message
    assert "structures were copied successfully" in message
    assert "processing will continue" in message


def test_in_plane_coverage_warning_recommends_larger_acquisition():
    message = _crop_coverage_warning_message(
        {
            "warning_code": "insufficient_in_plane_coverage",
            "roi_name": "PTV1_V03-05_1a+2cm_Ph",
        }
    )

    assert "acquired in-plane field of view" in message
    assert "larger in-plane field of view" in message
    assert "structures were copied successfully" in message
    assert "processing will continue" in message
