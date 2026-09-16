from pathlib import Path

import pydicom
import pytest
from pydicom.dataset import Dataset, FileDataset, FileMetaDataset
from pydicom.sequence import Sequence
from pydicom.uid import ExplicitVRLittleEndian, RTStructureSetStorage, generate_uid

from artemis_preprocessing.dicom import copy_structures as copy_module


class IdentityTransform:
    def GetInverse(self):
        return self

    def TransformPoint(self, point):
        return point


class ShiftedTransform(IdentityTransform):
    def TransformPoint(self, point):
        return point[0], point[1], point[2] + 5


def _contour(position, *, axis=2):
    other = [index for index in range(3) if index != axis]
    values = []
    for first, second in ((0, 0), (1, 0), (1, 1), (0, 1)):
        point = [0.0, 0.0, 0.0]
        point[axis] = float(position)
        point[other[0]] = float(first)
        point[other[1]] = float(second)
        values.extend(point)
    contour = Dataset()
    contour.ContourGeometricType = "CLOSED_PLANAR"
    contour.NumberOfContourPoints = 4
    contour.ContourData = values
    return contour


def _make_rtstruct(path: Path, rois):
    meta = FileMetaDataset()
    meta.TransferSyntaxUID = ExplicitVRLittleEndian
    meta.MediaStorageSOPClassUID = RTStructureSetStorage
    meta.MediaStorageSOPInstanceUID = generate_uid()
    rtstruct = FileDataset(str(path), {}, file_meta=meta, preamble=b"\0" * 128)
    rtstruct.Modality = "RTSTRUCT"
    rtstruct.SOPClassUID = RTStructureSetStorage
    rtstruct.SOPInstanceUID = meta.MediaStorageSOPInstanceUID
    rtstruct.SeriesInstanceUID = generate_uid()
    rtstruct.StructureSetROISequence = Sequence()
    rtstruct.ROIContourSequence = Sequence()
    rtstruct.RTROIObservationsSequence = Sequence()
    for number, (name, contours) in enumerate(rois, 1):
        roi = Dataset()
        roi.ROINumber = number
        roi.ROIName = name
        rtstruct.StructureSetROISequence.append(roi)
        roi_contour = Dataset()
        roi_contour.ReferencedROINumber = number
        roi_contour.ContourSequence = Sequence(contours)
        rtstruct.ROIContourSequence.append(roi_contour)
        observation = Dataset()
        observation.ReferencedROINumber = number
        rtstruct.RTROIObservationsSequence.append(observation)
    pydicom.dcmwrite(path, rtstruct, enforce_file_format=True)
    return rtstruct


def _copy(monkeypatch, tmp_path, rois, *, propagate_ptvs=False):
    base_path = tmp_path / "base.dcm"
    target_path = tmp_path / "daily.dcm"
    base = _make_rtstruct(base_path, rois)
    target = _make_rtstruct(target_path, [("Dummy_PH", [_contour(0)])])
    base_bytes = base_path.read_bytes()
    monkeypatch.setattr(copy_module, "read_base_rtstruct", lambda *args, **kwargs: base)
    monkeypatch.setattr(
        copy_module, "read_new_rtstruct", lambda *args, **kwargs: (target, target_path.name)
    )
    copy_module.copy_structures(
        str(tmp_path), "patient", "plan_1a", IdentityTransform(),
        propagate_ptvs=propagate_ptvs,
    )
    assert base_path.read_bytes() == base_bytes
    return pydicom.dcmread(target_path)


def _roi_contour(rtstruct, name):
    number = next(
        roi.ROINumber for roi in rtstruct.StructureSetROISequence if roi.ROIName == name
    )
    return next(
        roi for roi in rtstruct.ROIContourSequence if roi.ReferencedROINumber == number
    )


def test_copy_structures_skips_ptvs_except_two_centimeter_helper(monkeypatch, tmp_path):
    copied = _copy(
        monkeypatch, tmp_path,
        [
            ("PTV_1a", [_contour(0)]),
            ("PTVboost", [_contour(0)]),
            ("PTV+2cm_Ph", [_contour(0), _contour(2)]),
            ("CTV_1a", [_contour(1)]),
        ],
    )
    assert [roi.ROIName for roi in copied.StructureSetROISequence] == [
        "PTV+2cm_Ph", "CTV_1a"
    ]
    assert len(_roi_contour(copied, "PTV+2cm_Ph").ContourSequence) == 2
    assert len(_roi_contour(copied, "CTV_1a").ContourSequence) == 1


def test_copy_structures_propagates_ptvs_excluding_ph_except_crop_helper(
    monkeypatch, tmp_path
):
    copied = _copy(
        monkeypatch, tmp_path,
        [
            ("PTV_1a", [_contour(0)]),
            ("ptvBoost", [_contour(1)]),
            ("PTV_A_pH", [_contour(1)]),
            ("PTV+2cm_Ph", [_contour(0), _contour(2)]),
            ("CTV_1a", [_contour(1)]),
        ],
        propagate_ptvs=True,
    )
    assert [roi.ROIName for roi in copied.StructureSetROISequence] == [
        "PTV_1a", "ptvBoost", "PTV+2cm_Ph", "CTV_1a"
    ]
    assert len(_roi_contour(copied, "PTV_1a").ContourSequence) == 1
    assert len(_roi_contour(copied, "ptvBoost").ContourSequence) == 1


@pytest.mark.parametrize("axis", [0, 2])
@pytest.mark.parametrize("ring_positions", [(0, 2), (2, 0)])
def test_copy_keeps_inclusive_ring_slices_and_drops_empty_rois(
    monkeypatch, tmp_path, axis, ring_positions
):
    copied = _copy(
        monkeypatch, tmp_path,
        [
            ("PTV+2cm_Ph", [_contour(p, axis=axis) for p in ring_positions]),
            ("CTV_1a", [_contour(p, axis=axis) for p in (-1, 0, 1, 2, 3)]),
            ("Bladder", [_contour(-1, axis=axis)]),
        ],
    )
    assert [roi.ROIName for roi in copied.StructureSetROISequence] == [
        "PTV+2cm_Ph", "CTV_1a"
    ]
    positions = [
        float(contour.ContourData[axis])
        for contour in _roi_contour(copied, "CTV_1a").ContourSequence
    ]
    assert positions == [0, 1, 2]
    assert len(copied.RTROIObservationsSequence) == 2


@pytest.mark.parametrize(
    "helpers",
    [[], [("PTV+2cm_Ph", [])],
     [("PTV_A+2cm_Ph", [_contour(0)]), ("PTV_B+2cm_Ph", [_contour(1)])],
     [("PTV+2cm_Ph", [_contour(0)])]],
)
def test_invalid_ring_leaves_daily_rtstruct_unchanged(monkeypatch, tmp_path, helpers):
    base_path = tmp_path / "base.dcm"
    target_path = tmp_path / "daily.dcm"
    base = _make_rtstruct(base_path, helpers)
    if len(helpers) == 1 and helpers[0][1]:
        base.ROIContourSequence[0].ContourSequence[0].ContourData = [0, 0, 0, 1]
    target = _make_rtstruct(target_path, [("Dummy_PH", [_contour(0)])])
    original = target_path.read_bytes()
    monkeypatch.setattr(copy_module, "read_base_rtstruct", lambda *args, **kwargs: base)
    monkeypatch.setattr(
        copy_module, "read_new_rtstruct", lambda *args, **kwargs: (target, target_path.name)
    )
    with pytest.raises(ValueError):
        copy_module.copy_structures(str(tmp_path), "patient", "plan_1a", IdentityTransform())
    assert target_path.read_bytes() == original


def test_filter_uses_base_frame_before_transform(monkeypatch, tmp_path):
    base = _make_rtstruct(
        tmp_path / "base.dcm",
        [
            ("PTV+2cm_Ph", [_contour(0), _contour(2)]),
            ("CTV_1a", [_contour(-1), _contour(1), _contour(3)]),
        ],
    )
    target_path = tmp_path / "daily.dcm"
    target = _make_rtstruct(target_path, [("Dummy_PH", [_contour(0)])])
    monkeypatch.setattr(copy_module, "read_base_rtstruct", lambda *args, **kwargs: base)
    monkeypatch.setattr(
        copy_module, "read_new_rtstruct", lambda *args, **kwargs: (target, target_path.name)
    )
    copy_module.copy_structures(str(tmp_path), "patient", "plan_1a", ShiftedTransform())
    copied = pydicom.dcmread(target_path)
    contours = _roi_contour(copied, "CTV_1a").ContourSequence
    assert len(contours) == 1
    assert float(contours[0].ContourData[2]) == 6


def test_staged_write_failure_preserves_daily_rtstruct(monkeypatch, tmp_path):
    base = _make_rtstruct(
        tmp_path / "base.dcm", [("PTV+2cm_Ph", [_contour(0), _contour(2)])]
    )
    target_path = tmp_path / "daily.dcm"
    target = _make_rtstruct(target_path, [("Dummy_PH", [_contour(0)])])
    original = target_path.read_bytes()
    monkeypatch.setattr(copy_module, "read_base_rtstruct", lambda *args, **kwargs: base)
    monkeypatch.setattr(
        copy_module, "read_new_rtstruct", lambda *args, **kwargs: (target, target_path.name)
    )
    def fail_write(*args, **kwargs):
        raise OSError("staged write failed")
    monkeypatch.setattr(copy_module.pydicom, "dcmwrite", fail_write)
    with pytest.raises(OSError, match="staged write failed"):
        copy_module.copy_structures(str(tmp_path), "patient", "plan_1a", IdentityTransform())
    assert target_path.read_bytes() == original
    assert sorted(path.name for path in tmp_path.iterdir()) == ["base.dcm", "daily.dcm"]


def test_nonplanar_copied_contour_leaves_daily_rtstruct_unchanged(
    monkeypatch, tmp_path
):
    base = _make_rtstruct(
        tmp_path / "base.dcm",
        [
            ("PTV+2cm_Ph", [_contour(0), _contour(2)]),
            ("CTV_1a", [_contour(1)]),
        ],
    )
    base.ROIContourSequence[1].ContourSequence[0].ContourData[2] = 1.5
    target_path = tmp_path / "daily.dcm"
    target = _make_rtstruct(target_path, [("Dummy_PH", [_contour(0)])])
    original = target_path.read_bytes()
    monkeypatch.setattr(copy_module, "read_base_rtstruct", lambda *args, **kwargs: base)
    monkeypatch.setattr(
        copy_module, "read_new_rtstruct", lambda *args, **kwargs: (target, target_path.name)
    )
    with pytest.raises(ValueError, match="single.*slice plane"):
        copy_module.copy_structures(str(tmp_path), "patient", "plan_1a", IdentityTransform())
    assert target_path.read_bytes() == original
