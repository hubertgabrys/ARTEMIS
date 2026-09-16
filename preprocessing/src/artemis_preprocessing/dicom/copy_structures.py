import copy
import os
import re
import tempfile
import time
from pathlib import Path

import numpy as np
import pydicom
from tqdm import tqdm

from artemis_preprocessing.utils import float_to_ds_string


LIMBUS_STRUCTURE_MAP = {
    "Bladder": "Bladder",
    "Bowel": "Bowel",
    "Sigma": "Sigma",
    "Rectum": "Rectum",
    "SeminalVesicle": "SeminalVesicle",
    "PubicSymphys": "PubicSymphys",
    "Prostate": "Prostate",
}

ROI_DISPLAY_COLOR_MAP = {
    "bladder": [255, 255, 0],
    "bowel": [0, 178, 47],
    "sigma": [255, 175, 0],
    "rectum": [191, 127, 0],
}

SLICE_PLANE_TOLERANCE_MM = 0.1


def _contour_points(contour):
    values = getattr(contour, "ContourData", None)
    if values is None or not values or len(values) % 3:
        raise ValueError("ContourData is missing or is not a sequence of XYZ points")
    try:
        points = np.asarray([float(value) for value in values], dtype=float).reshape(-1, 3)
    except (TypeError, ValueError) as exc:
        raise ValueError("ContourData contains invalid coordinates") from exc
    if not np.all(np.isfinite(points)):
        raise ValueError("ContourData contains non-finite coordinates")
    return points


def _ring_slice_range(rtstruct):
    """Return the source-frame slice normal and inclusive +2cm helper extent."""
    helpers = [
        roi for roi in getattr(rtstruct, "StructureSetROISequence", [])
        if str(getattr(roi, "ROIName", "")).casefold().startswith("ptv")
        and str(getattr(roi, "ROIName", "")).casefold().endswith("+2cm_ph")
    ]
    if len(helpers) != 1:
        raise ValueError(f"Expected one PTV +2cm_Ph helper ROI; found {len(helpers)}")
    number = helpers[0].ROINumber
    contours = [
        contour
        for roi_contour in getattr(rtstruct, "ROIContourSequence", [])
        if getattr(roi_contour, "ReferencedROINumber", None) == number
        for contour in getattr(roi_contour, "ContourSequence", [])
    ]
    if not contours:
        raise ValueError("PTV +2cm_Ph helper ROI has no contours")

    first_points = _contour_points(contours[0])
    if len(first_points) < 3:
        raise ValueError("PTV +2cm_Ph helper contour cannot define a slice plane")
    _, singular_values, vh = np.linalg.svd(first_points - first_points.mean(axis=0))
    if len(singular_values) < 2 or singular_values[1] < 1e-6:
        raise ValueError("PTV +2cm_Ph helper contour cannot define a slice plane")
    normal = vh[-1]

    positions = []
    for contour in contours:
        positions.append(_contour_slice_position(contour, normal))
    return normal, min(positions), max(positions)


def _contour_slice_position(contour, normal):
    projections = _contour_points(contour) @ normal
    if float(np.ptp(projections)) > SLICE_PLANE_TOLERANCE_MM:
        raise ValueError("Contour does not lie on a single +2cm helper slice plane")
    return float(np.mean(projections))


def transform_contour_points(transform, contour_data, precision: int = 8):
    """
    Given:
      - transform: a SimpleITK transform (with .TransformPoint)
      - contour_data: flat sequence of floats or numeric strings [x1,y1,z1, x2,y2,z2, ...]
    Returns:
      - a list of pydicom.valuerep.DS strings suitable for placing back into ContourData,
        with each coordinate transformed and formatted per DICOM DS VR.
    """
    if len(contour_data) % 3 != 0:
        raise ValueError("contour_data length must be a multiple of 3")

    out_ds = []
    for i in range(0, len(contour_data), 3):
        # build the input point as floats
        x, y, z = (float(contour_data[i]),
                   float(contour_data[i + 1]),
                   float(contour_data[i + 2]))
        # transform
        x_t, y_t, z_t = transform.TransformPoint((x, y, z))
        # convert each to DS
        out_ds.append(float_to_ds_string(x_t, precision))
        out_ds.append(float_to_ds_string(y_t, precision))
        out_ds.append(float_to_ds_string(z_t, precision))

    return out_ds


def _apply_roi_display_color(roi_contour, roi_name):
    roi_name_lower = (roi_name or "").lower()
    color = ROI_DISPLAY_COLOR_MAP.get(roi_name_lower)
    if color is None and roi_name_lower.startswith("ctv"):
        color = [255, 255, 128]
    if color is not None:
        roi_contour.ROIDisplayColor = color


def _rtstruct_references_series(ds, series_uid):
    """Return True if *ds* references *series_uid*."""
    try:
        for fr in ds.ReferencedFrameOfReferenceSequence:
            for st in fr.RTReferencedStudySequence:
                for se in st.RTReferencedSeriesSequence:
                    if getattr(se, "SeriesInstanceUID", None) == series_uid:
                        return True
    except Exception:
        pass
    return False


def find_rtstruct(directory, description_prefix=None, series_uid=None):
    """Locate an RTSTRUCT in *directory* optionally filtered by description prefix
    or referenced SeriesInstanceUID."""

    for file_name in os.listdir(directory):
        rtstruct_path = os.path.join(directory, file_name)
        try:
            ds = pydicom.dcmread(rtstruct_path, stop_before_pixels=True)
        except Exception:
            continue
        if ds.Modality != "RTSTRUCT":
            continue

        if series_uid and not _rtstruct_references_series(ds, series_uid):
            continue

        if description_prefix is None:
            return ds, file_name
        elif hasattr(ds, "SeriesDescription"):
            if ds.SeriesDescription.lower().startswith(description_prefix.lower()):
                return ds, file_name
    return None, None


def _find_limbus_rtstruct(directory, series_uid=None):
    """Return the first RTSTRUCT whose Structure Set Label is 'Limbus RTStruct'."""

    for file_name in os.listdir(directory):
        rtstruct_path = os.path.join(directory, file_name)
        try:
            ds = pydicom.dcmread(rtstruct_path, stop_before_pixels=True)
        except Exception:
            continue

        if getattr(ds, "Modality", None) != "RTSTRUCT":
            continue

        # Check Structure Set Label (3006,0002)
        label = getattr(ds, "StructureSetLabel", "") or ""
        if label.strip() != "Limbus RTStruct":
            continue

        if series_uid and not _rtstruct_references_series(ds, series_uid):
            continue

        return ds, file_name

    return None, None


def read_base_rtstruct(patient_id, rtplan_label, series_uid=None):
    base_dir = Path(os.environ.get("BASEPLAN_DIR")) / patient_id / rtplan_label
    rtstruct, _ = find_rtstruct(str(base_dir), series_uid=series_uid)
    return rtstruct


def read_new_rtstruct(current_directory, series_uid=None):
    """Return RTSTRUCT in *current_directory* matching *series_uid* if provided."""

    if series_uid:
        filename = os.path.join(current_directory, f"RS_{series_uid}.dcm")
        if os.path.exists(filename):
            ds = pydicom.dcmread(filename)
            return ds, os.path.basename(filename)
        rtstruct, rtstruct_filename = find_rtstruct(current_directory, series_uid=series_uid)
        if rtstruct is not None:
            return rtstruct, rtstruct_filename

    # Fallback to keyword-based search
    rtstruct, rtstruct_filename = find_rtstruct(current_directory, "syntheticcthu")
    if rtstruct is not None:
        return rtstruct, rtstruct_filename
    rtstruct, rtstruct_filename = find_rtstruct(current_directory, "synthetic ct")
    if rtstruct is not None:
        return rtstruct, rtstruct_filename
    rtstruct, rtstruct_filename = find_rtstruct(current_directory, "t2_tse_tra")
    return rtstruct, rtstruct_filename


def copy_structures(current_directory, patient_id, rtplan_label, rigid_transform,
                    series_uid=None, base_series_uid=None, progress_callback=None,
                    propagate_ptvs=False):
    """Copy structures from the base plan RTSTRUCT to the daily RTSTRUCT."""

    # Read the base and new RTSTRUCT files referencing the chosen series.
    rtstruct_base = read_base_rtstruct(patient_id, rtplan_label, series_uid=base_series_uid)
    rtstruct_new, rtstruct_new_filename = read_new_rtstruct(current_directory, series_uid)
    if rtstruct_base is None or rtstruct_new is None or not rtstruct_new_filename:
        raise ValueError("Base or daily RTSTRUCT could not be found")
    slice_normal, lowest_ring_slice, highest_ring_slice = _ring_slice_range(rtstruct_base)
    rtstruct_new = copy.deepcopy(rtstruct_new)

    # Extract plan suffix (e.g. "_1a") from the plan label if present
    match = re.search(r"_(\d[a-z])$", rtplan_label.lower())
    plan_suffix = match.group(0) if match else None

    # Get the inverse of the transform
    rigid_transform = rigid_transform.GetInverse()

    target_for_uid = None
    try:
        fr_seq = getattr(rtstruct_new, "ReferencedFrameOfReferenceSequence", None)
        if fr_seq and len(fr_seq):
            target_for_uid = getattr(fr_seq[0], "FrameOfReferenceUID", None)
    except Exception:
        target_for_uid = None

    # Determine which ROI numbers contain contour data
    roi_number_has_contour = set()
    if hasattr(rtstruct_base, "ROIContourSequence"):
        for roi_contour in rtstruct_base.ROIContourSequence:
            num = getattr(roi_contour, "ReferencedROINumber", None)
            if num is None:
                continue
            has_data = False
            if hasattr(roi_contour, "ContourSequence"):
                for contour in roi_contour.ContourSequence:
                    data = getattr(contour, "ContourData", [])
                    if data:
                        has_data = True
                        break
            if has_data:
                roi_number_has_contour.add(num)

    # Helper function: determine if an ROI name should be skipped.
    def skip_roi(name, number):
        name_lower = name.lower()

        # Skip if there are no contours associated with this ROI
        if number not in roi_number_has_contour:
            print(f"Skipping ROI {number} ({name}) because it has no contour data")
            return True

        # Check for ROI names ending with pattern like "_1a" and ensure it matches the plan label
        if plan_suffix:
            match_suffix = re.search(r"_(\d[a-z])$", name_lower)
            if match_suffix and match_suffix.group(0) != plan_suffix:
                print(f"Skipping ROI {number} ({name}) because it does not match the plan suffix")
                return True

        # The +2cm_ph helper is needed by the image crop; other _ph ROIs stay excluded.
        if name_lower.startswith("ptv") and name_lower.endswith("+2cm_ph"):
            pass  # allowed
        else:
            if (
                (name_lower.startswith("ptv") and not propagate_ptvs)
                or name_lower.startswith("zzz")
                or name_lower.endswith("_ph")
            ):
                print(f"Skipping ROI {number} ({name})")
                return True

        if name_lower in {"couchsurface", "couchinterior"}:
            print(f"Skipping ROI {number} ({name})")
            return True
        if "ring" in name_lower or "body" in name_lower or "skin" in name_lower:
            print(f"Skipping ROI {number} ({name})")
            return True

        return False

    def _collect_roi_numbers_by_name(rtstruct):
        lookup = {}
        if hasattr(rtstruct, "StructureSetROISequence"):
            for roi in rtstruct.StructureSetROISequence:
                name = getattr(roi, "ROIName", "")
                number = getattr(roi, "ROINumber", None)
                if number is not None:
                    lookup[name.lower()] = number
        return lookup

    def _remove_roi_by_number(rtstruct, roi_number):
        def _filter_sequence(attr, number_field):
            if hasattr(rtstruct, attr):
                seq = getattr(rtstruct, attr)
                filtered = [item for item in seq if getattr(item, number_field, None) != roi_number]
                setattr(rtstruct, attr, pydicom.sequence.Sequence(filtered))

        _filter_sequence("StructureSetROISequence", "ROINumber")
        _filter_sequence("ROIContourSequence", "ReferencedROINumber")
        _filter_sequence("RTROIObservationsSequence", "ReferencedROINumber")

    def _next_roi_number(rtstruct):
        max_number = 0
        if hasattr(rtstruct, "StructureSetROISequence"):
            for roi in rtstruct.StructureSetROISequence:
                number = getattr(roi, "ROINumber", 0)
                try:
                    max_number = max(max_number, int(number))
                except Exception:
                    continue
        return max_number + 1

    def _next_observation_number(rtstruct):
        """Return the next free ObservationNumber for RTROIObservationsSequence."""
        max_number = 0
        if hasattr(rtstruct, "RTROIObservationsSequence"):
            for obs in rtstruct.RTROIObservationsSequence:
                num = getattr(obs, "ObservationNumber", None)
                if num is None:
                    continue
                try:
                    max_number = max(max_number, int(num))
                except Exception:
                    continue
        return max_number + 1

    def _copy_limbus_structures(target_rtstruct, directory, series_uid=None):
        # Wait up to 3 s, retrying every 3 s for the Limbus RTSTRUCT
        max_wait_s = 3
        retry_interval_s = 3
        deadline = time.time() + max_wait_s

        limbus_rtstruct = None
        limbus_filename = None
        attempt = 0

        while time.time() < deadline:
            limbus_rtstruct, limbus_filename = _find_limbus_rtstruct(
                directory, series_uid=series_uid
            )
            if limbus_rtstruct is not None:
                break
            attempt += 1
            print(
                f"Limbus RTSTRUCT not found (attempt {attempt}); "
                f"retrying in {retry_interval_s} s..."
            )
            time.sleep(retry_interval_s)

        if limbus_rtstruct is None:
            print(
                f"Limbus RTSTRUCT not found after waiting {max_wait_s} s. "
                "Proceeding without Limbus contours."
            )
            return
        else:
            print(f"Limbus RTSTRUCT found after waiting {attempt*retry_interval_s} s. ")

        limbus_path = os.path.join(directory, limbus_filename)

        target_lookup = _collect_roi_numbers_by_name(target_rtstruct)
        next_number = _next_roi_number(target_rtstruct)

        # Ensure ObservationNumber uniqueness across existing and imported observations
        next_obs_number = _next_observation_number(target_rtstruct)

        source_lookup = _collect_roi_numbers_by_name(limbus_rtstruct)
        if not source_lookup:
            return

        copied_any = False  # track whether we actually imported anything

        for source_name, target_name in LIMBUS_STRUCTURE_MAP.items():
            source_number = source_lookup.get(source_name.lower())
            if source_number is None:
                continue

            if target_name.lower() in target_lookup:
                _remove_roi_by_number(target_rtstruct, target_lookup[target_name.lower()])

            new_number = next_number
            next_number += 1

            source_roi = next(
                (roi for roi in limbus_rtstruct.StructureSetROISequence
                 if getattr(roi, "ROIName", "").lower() == source_name.lower()),
                None,
            )
            if source_roi is None:
                continue

            new_roi = copy.deepcopy(source_roi)
            new_roi.ROIName = target_name
            new_roi.ROINumber = new_number

            if target_for_uid:
                new_roi.ReferencedFrameOfReferenceUID = target_for_uid

            target_rtstruct.StructureSetROISequence.append(new_roi)

            if hasattr(limbus_rtstruct, "ROIContourSequence"):
                print(f"Copying ROIContourSequence for {target_name} from Limbus")
                for contour in limbus_rtstruct.ROIContourSequence:
                    if getattr(contour, "ReferencedROINumber", None) != source_number:
                        continue
                    new_contour = copy.deepcopy(contour)
                    new_contour.ReferencedROINumber = new_number
                    _apply_roi_display_color(new_contour, target_name)
                    target_rtstruct.ROIContourSequence.append(new_contour)

            if hasattr(limbus_rtstruct, "RTROIObservationsSequence"):
                print(f"Copying RTROIObservationsSequence for {target_name} from Limbus")
                for obs in limbus_rtstruct.RTROIObservationsSequence:
                    if getattr(obs, "ReferencedROINumber", None) != source_number:
                        continue
                    new_obs = copy.deepcopy(obs)
                    new_obs.ReferencedROINumber = new_number

                    # Assign a new, unique ObservationNumber to avoid collisions
                    try:
                        new_obs.ObservationNumber = int(next_obs_number)
                    except Exception:
                        new_obs.ObservationNumber = next_obs_number
                    next_obs_number += 1

                    target_rtstruct.RTROIObservationsSequence.append(new_obs)

        # Remove the Limbus RTSTRUCT file.
        if os.path.exists(limbus_path):
            try:
                os.remove(limbus_path)
                print(f"Deleted Limbus RTSTRUCT: {limbus_path}")
            except Exception as exc:
                print(f"Warning: failed to delete Limbus RTSTRUCT {limbus_path}: {exc}")

    candidate_numbers = {
        roi.ROINumber
        for roi in rtstruct_base.StructureSetROISequence
        if not skip_roi(getattr(roi, "ROIName", ""), getattr(roi, "ROINumber", None))
    }

    # Validate and select copied contours before changing the daily RTSTRUCT.
    kept_numbers = set()
    selected_by_item = {}
    for roi_contour in getattr(rtstruct_base, "ROIContourSequence", []):
        number = getattr(roi_contour, "ReferencedROINumber", None)
        if number not in candidate_numbers:
            continue
        selected = []
        for contour in getattr(roi_contour, "ContourSequence", []):
            position = _contour_slice_position(contour, slice_normal)
            if (lowest_ring_slice - SLICE_PLANE_TOLERANCE_MM <= position
                    <= highest_ring_slice + SLICE_PLANE_TOLERANCE_MM):
                selected.append(contour)
        if selected:
            kept_numbers.add(number)
            selected_by_item[id(roi_contour)] = selected

    rtstruct_new.StructureSetROISequence = pydicom.sequence.Sequence()
    rtstruct_new.ROIContourSequence = pydicom.sequence.Sequence()
    rtstruct_new.RTROIObservationsSequence = pydicom.sequence.Sequence()

    # --- Step 1: Filter Structure Set ROI Sequence ---
    # Process each ROI item based on its ROI Name (tag 3006,0026).
    # Record its associated ROI Number (tag 3006,0022) and copy the ROI item into the new StructureSetROISequence.
    approved_roi_numbers = set()
    # Build a lookup from ROI Number to ROI Name (for later use in Step 2)
    roi_lookup = {}
    for roi in rtstruct_base.StructureSetROISequence:
        roi_name = getattr(roi, "ROIName", "")
        roi_number = getattr(roi, "ROINumber", None)
        if roi_number not in candidate_numbers or roi_number not in kept_numbers:
            continue
        if roi_number is not None:
            approved_roi_numbers.add(roi_number)
            roi_lookup[roi_number] = roi_name  # store the name
            new_roi = copy.deepcopy(roi)

            if target_for_uid:
                new_roi.ReferencedFrameOfReferenceUID = target_for_uid

            rtstruct_new.StructureSetROISequence.append(new_roi)

    # --- Step 2: Copy and Transform ROI Contour Sequence ---
    # Process ROI contour items only if their Referenced ROI Number (tag 3006,0084)
    # is part of the approved ROI numbers.
    sequence = rtstruct_base.ROIContourSequence
    iterator = tqdm(sequence, desc="ROIContourSequence") if progress_callback is None else sequence
    total = len(sequence)
    for idx, roi_contour in enumerate(iterator, 1):
        # Retrieve the Referenced ROI Number (tag 3006,0084)
        ref_roi_num = getattr(roi_contour, "ReferencedROINumber", None)
        if ref_roi_num not in approved_roi_numbers or id(roi_contour) not in selected_by_item:
            continue

        # Look up the ROI name that corresponds to this contour using the ROI number.
        roi_name = roi_lookup.get(ref_roi_num, "")
        new_roi_contour = copy.deepcopy(roi_contour)
        new_roi_contour.ContourSequence = pydicom.sequence.Sequence(
            copy.deepcopy(selected_by_item[id(roi_contour)])
        )
        _apply_roi_display_color(new_roi_contour, roi_name)
        print(f"Copying ROI {ref_roi_num} ({roi_name})")
        for contour in new_roi_contour.ContourSequence:
            new_data = transform_contour_points(rigid_transform, contour.ContourData)
            contour.ContourData = [str(value) for value in new_data]

        rtstruct_new.ROIContourSequence.append(new_roi_contour)
        if progress_callback:
            progress_callback(idx, total)

    # --- Step 3: Copy RT ROI Observations Sequence ---
    # Each observation item is included only if its Referenced ROI Number (tag 3006,0084)
    # matches one of the approved ROI numbers.
    if hasattr(rtstruct_base, "RTROIObservationsSequence"):
        for obs in rtstruct_base.RTROIObservationsSequence:
            ref_roi_num = getattr(obs, "ReferencedROINumber", None)
            if ref_roi_num not in approved_roi_numbers:
                continue
            new_obs = copy.deepcopy(obs)
            rtstruct_new.RTROIObservationsSequence.append(new_obs)

    # --- Step 4: Copy supplemental limbus structures when available ---
    # _copy_limbus_structures(rtstruct_new, current_directory, series_uid=series_uid)

    # --- Save the Updated RTSTRUCT ---
    output_filename = os.path.join(current_directory, rtstruct_new_filename)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{rtstruct_new_filename}.", suffix=".tmp", dir=current_directory
    )
    os.close(descriptor)
    try:
        pydicom.dcmwrite(temporary_name, rtstruct_new, write_like_original=False)
        written = pydicom.dcmread(temporary_name)
        if (len(written.StructureSetROISequence) != len(rtstruct_new.StructureSetROISequence)
                or len(written.ROIContourSequence) != len(rtstruct_new.ROIContourSequence)
                or len(written.RTROIObservationsSequence)
                != len(rtstruct_new.RTROIObservationsSequence)
                or [len(item.ContourSequence) for item in written.ROIContourSequence]
                != [len(item.ContourSequence) for item in rtstruct_new.ROIContourSequence]):
            raise ValueError("Staged RTSTRUCT did not retain the copied contours")
        os.replace(temporary_name, output_filename)
    finally:
        Path(temporary_name).unlink(missing_ok=True)
    # print(f"Updated RTSTRUCT saved to {output_filename}")
    return output_filename
