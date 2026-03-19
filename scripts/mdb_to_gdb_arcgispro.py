import argparse
import datetime as dt
import json
import locale
import os
import re
import sys
import traceback
import xml.etree.ElementTree as ET

import arcpy
from osgeo import ogr


LAYER_IDENTITY_ELEMENT_NAMES = (
    "Name",
    "DatasetName",
    "CatalogPath",
    "enttypl",
    "itemName",
    "ftname",
    "resTitle",
)

FIELD_NODE_NAMES = ("attr", "Field", "GPFieldInfoEx", "FieldInfo")

RAW_LAYER_WINDOW_CHARS = 2000


def configure_stdio():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass


def info(message):
    print(f"INFO:{message}", flush=True)


def warn(message):
    print(f"WARN:{message}", flush=True)


def error(message):
    print(f"ERROR:{message}", flush=True)


def info_event(kind, index, message=""):
    print(f"INFO:EVENT|{kind}|{index}|{message}", flush=True)


def error_event(kind, index, message=""):
    print(f"ERROR:EVENT|{kind}|{index}|{message}", flush=True)


def preferred_encoding():
    encoding = locale.getpreferredencoding(False)
    return encoding or "gbk"


def score_text(value):
    if not value:
        return -(10**9)

    score = 0
    for ch in value:
        code = ord(ch)
        if ch == "\ufffd":
            score -= 100
        elif ch == "?":
            score -= 4
        elif not ch.isprintable():
            score -= 20
        elif 0xE000 <= code <= 0xF8FF:
            score -= 20
        elif 0x4E00 <= code <= 0x9FFF or 0x3400 <= code <= 0x4DBF:
            score += 3
        elif ch.isalnum() or ch in "_- \\/":
            score += 1
    return score


def looks_like_utf8(raw_bytes):
    try:
        raw_bytes.decode("utf-8")
        return True
    except UnicodeDecodeError:
        return False


def decode_best_text(raw_value):
    if raw_value is None:
        return ""
    if not isinstance(raw_value, str):
        return str(raw_value)
    if raw_value.isascii():
        return raw_value
    if not any(0xDC80 <= ord(ch) <= 0xDCFF for ch in raw_value):
        return raw_value

    try:
        raw_bytes = raw_value.encode("utf-8", "surrogateescape")
    except Exception:
        return raw_value

    candidates = []
    if looks_like_utf8(raw_bytes):
        candidates.append(raw_bytes.decode("utf-8", errors="replace"))

    for encoding in (preferred_encoding(), "gbk", "gb18030"):
        try:
            candidates.append(raw_bytes.decode(encoding))
        except Exception:
            continue

    candidates.append(raw_value)
    return max(candidates, key=score_text).strip()


def get_field_as_text(feature, field_index):
    return decode_best_text(feature.GetFieldAsString(field_index))


def get_layer_name(layer):
    return decode_best_text(layer.GetName())


def normalize_layer_path(value):
    if not value:
        return ""
    return value.strip().replace("/", "\\").strip("\\")


def get_last_segment(value):
    normalized = normalize_layer_path(value)
    if "\\" in normalized:
        return normalized.rsplit("\\", 1)[-1].strip()
    return normalized.strip()


def get_dataset_name_from_layer_name(layer_name):
    normalized = normalize_layer_path(layer_name)
    if "\\" not in normalized:
        return ""
    return normalized.rsplit("\\", 1)[0].strip()


def get_output_name_from_layer_name(layer_name):
    normalized = normalize_layer_path(layer_name)
    if "\\" in normalized:
        return normalized.rsplit("\\", 1)[-1].strip()
    return normalized.strip()


def split_catalog_path(path_value):
    normalized = normalize_layer_path(path_value)
    if not normalized:
        return []
    return [segment.strip() for segment in normalized.split("\\") if segment.strip()]


def build_relevant_layer_names(source_layer_name, output_layer_name):
    names = set()
    for value in (source_layer_name, output_layer_name):
        normalized = normalize_layer_path(value)
        if normalized:
            names.add(normalized.lower())
        last_segment = get_last_segment(normalized)
        if last_segment:
            names.add(last_segment.lower())
    return names


def matches_relevant_layer(value, relevant_layer_names):
    if not value:
        return False
    normalized = normalize_layer_path(decode_best_text(value)).lower()
    if normalized in relevant_layer_names:
        return True
    last_segment = get_last_segment(normalized).lower()
    return last_segment in relevant_layer_names


def get_local_name(tag):
    if not tag:
        return ""
    if "}" in tag:
        return tag.rsplit("}", 1)[-1]
    if ":" in tag:
        return tag.rsplit(":", 1)[-1]
    return tag


def is_field_node(element):
    return get_local_name(element.tag) in FIELD_NODE_NAMES


def read_child_text(element, *names):
    wanted = set(names)
    for child in element:
        if get_local_name(child.tag) in wanted and child.text:
            return child.text
    return ""


def node_matches_layer(element, relevant_layer_names):
    for value in element.attrib.values():
        if matches_relevant_layer(value, relevant_layer_names):
            return True

    for child_name in LAYER_IDENTITY_ELEMENT_NAMES:
        if matches_relevant_layer(
            read_child_text(element, child_name), relevant_layer_names
        ):
            return True

    return False


def context_matches_layer(element, parent_map, relevant_layer_names):
    current = element
    while current is not None:
        if node_matches_layer(current, relevant_layer_names):
            return True
        current = parent_map.get(current)
    return False


def extract_tag_values(xml_text, tag_name):
    pattern = re.compile(
        rf"<(?:[\w.-]+:)?{tag_name}(?:\s[^>]*)?>(.*?)</(?:[\w.-]+:)?{tag_name}>",
        re.IGNORECASE | re.DOTALL,
    )
    return [match.group(1) for match in pattern.finditer(xml_text)]


def extract_feature_dataset_name_from_fragment(xml_text):
    for tag in ("FeatureDatasetName", "CatalogPath"):
        for raw_value in extract_tag_values(xml_text, tag):
            value = decode_best_text(raw_value).strip()
            if not value:
                continue
            if tag == "CatalogPath":
                dataset_name = normalize_layer_path(
                    get_dataset_name_from_layer_name(value)
                )
                if dataset_name:
                    return dataset_name
                continue
            return normalize_layer_path(value)

    return ""


def extract_feature_dataset_name(xml_text, relevant_layer_names=None):
    if relevant_layer_names:
        lowered_xml = xml_text.lower()
        seen = set()
        names = sorted(
            (name for name in relevant_layer_names if name),
            key=len,
            reverse=True,
        )
        for name in names:
            lowered_name = name.lower()
            start = 0
            while True:
                index = lowered_xml.find(lowered_name, start)
                if index < 0:
                    break

                forward_fragment = xml_text[
                    index : min(
                        len(xml_text), index + len(name) + RAW_LAYER_WINDOW_CHARS
                    )
                ]
                if forward_fragment not in seen:
                    seen.add(forward_fragment)
                    dataset_name = extract_feature_dataset_name_from_fragment(
                        forward_fragment
                    )
                    if dataset_name:
                        return dataset_name

                window_start = max(0, index - RAW_LAYER_WINDOW_CHARS)
                window_end = min(
                    len(xml_text), index + len(name) + RAW_LAYER_WINDOW_CHARS
                )
                fragment = xml_text[window_start:window_end]
                if fragment not in seen:
                    seen.add(fragment)
                    dataset_name = extract_feature_dataset_name_from_fragment(fragment)
                    if dataset_name:
                        return dataset_name
                start = index + len(lowered_name)
        return ""

    return extract_feature_dataset_name_from_fragment(xml_text)


def read_feature_dataset_name(root, parent_map, relevant_names):
    for element in root.iter():
        if not context_matches_layer(element, parent_map, relevant_names):
            continue

        local_name = get_local_name(element.tag)
        if local_name == "FeatureDatasetName" and element.text:
            return normalize_layer_path(decode_best_text(element.text).strip())

        if local_name == "CatalogPath" and element.text:
            value = decode_best_text(element.text).strip()
            return normalize_layer_path(get_dataset_name_from_layer_name(value))

        feature_dataset_name = decode_best_text(
            read_child_text(element, "FeatureDatasetName")
        ).strip()
        if feature_dataset_name:
            return normalize_layer_path(feature_dataset_name)

        catalog_path = decode_best_text(read_child_text(element, "CatalogPath")).strip()
        if catalog_path:
            return normalize_layer_path(get_dataset_name_from_layer_name(catalog_path))

    return ""


def merge_field_aliases(
    root, parent_map, field_aliases, relevant_names, require_layer_context
):
    merged = False
    for element in root.iter():
        if not is_field_node(element):
            continue
        if require_layer_context and not context_matches_layer(
            element, parent_map, relevant_names
        ):
            continue

        field_name = decode_best_text(
            read_child_text(element, "attrlabl", "Name")
        ).strip()
        alias_name = decode_best_text(
            read_child_text(element, "attalias", "AliasName")
        ).strip()
        if field_name and alias_name and field_name not in field_aliases:
            field_aliases[field_name] = alias_name
            merged = True
    return merged


def merge_metadata_xml(xml_text, info, source_layer_name, output_layer_name):
    if not xml_text:
        return

    try:
        root = ET.fromstring(xml_text)
    except ET.ParseError:
        if not info["feature_dataset_name"]:
            relevant_names = build_relevant_layer_names(
                source_layer_name, output_layer_name
            )
            info["feature_dataset_name"] = extract_feature_dataset_name(
                xml_text, relevant_names
            )
        return

    parent_map = {child: parent for parent in root.iter() for child in parent}
    relevant_names = build_relevant_layer_names(source_layer_name, output_layer_name)

    if not info["feature_dataset_name"]:
        info["feature_dataset_name"] = read_feature_dataset_name(
            root, parent_map, relevant_names
        ) or extract_feature_dataset_name(xml_text, relevant_names)

    if not info["alias_name"]:
        best_alias = ""
        best_score = -(10**9)
        for element in root.iter():
            if is_field_node(element):
                continue
            alias_name = decode_best_text(read_child_text(element, "AliasName")).strip()
            if not alias_name:
                continue
            if not context_matches_layer(element, parent_map, relevant_names):
                continue

            score = 100
            if not matches_relevant_layer(alias_name, relevant_names):
                score += 20
            if score > best_score:
                best_alias = alias_name
                best_score = score

        if best_alias:
            info["alias_name"] = best_alias

    merged_layer_scoped = merge_field_aliases(
        root, parent_map, info["field_aliases"], relevant_names, True
    )
    if not merged_layer_scoped:
        merge_field_aliases(
            root, parent_map, info["field_aliases"], relevant_names, False
        )


def read_layer_metadata(source_ds, layer_name):
    info = {"feature_dataset_name": "", "alias_name": "", "field_aliases": {}}
    output_name = get_output_name_from_layer_name(layer_name)
    escaped_layer_name = layer_name.replace("'", "''")

    sql_candidates = (
        f"GetLayerDefinition {layer_name}",
        f"GetLayerMetadata {layer_name}",
        f"GetLayerMetadata '{escaped_layer_name}'",
        f"GetLayerDefinition '{escaped_layer_name}'",
    )

    for sql in sql_candidates:
        metadata_layer = None
        try:
            metadata_layer = source_ds.ExecuteSQL(sql, None, "PGeo")
            if metadata_layer is None:
                continue

            parts = []
            feature = metadata_layer.GetNextFeature()
            while feature is not None:
                for field_index in range(feature.GetFieldCount()):
                    parts.append(get_field_as_text(feature, field_index))
                feature = metadata_layer.GetNextFeature()

            xml_text = "".join(parts).strip()
            if not xml_text:
                continue

            merge_metadata_xml(xml_text, info, layer_name, output_name)
        except Exception:
            continue
        finally:
            if metadata_layer is not None:
                source_ds.ReleaseResultSet(metadata_layer)

    return info


def build_feature_dataset_lookup(item_rows):
    lookup = {}
    for item in item_rows:
        item_name = normalize_layer_path(item.get("name", ""))
        path_parts = split_catalog_path(item.get("path", ""))
        if not item_name or len(path_parts) < 2:
            continue

        dataset_name = path_parts[-2]
        feature_class_name = path_parts[-1]
        if feature_class_name and dataset_name:
            lookup.setdefault(feature_class_name, dataset_name)
            lookup.setdefault(item_name, dataset_name)

    return lookup


def read_feature_dataset_lookup(source_ds):
    layer = None
    try:
        layer = source_ds.ExecuteSQL("SELECT Name, Path FROM GDB_Items", None, "PGeo")
        if layer is None:
            return {}

        definition = layer.GetLayerDefn()
        field_indexes = {}
        for field_index in range(definition.GetFieldCount()):
            field_name = definition.GetFieldDefn(field_index).GetNameRef()
            field_indexes[field_name.upper()] = field_index

        name_index = field_indexes.get("NAME")
        path_index = field_indexes.get("PATH")
        if name_index is None or path_index is None:
            return {}

        rows = []
        feature = layer.GetNextFeature()
        while feature is not None:
            rows.append(
                {
                    "name": get_field_as_text(feature, name_index),
                    "path": get_field_as_text(feature, path_index),
                }
            )
            feature = layer.GetNextFeature()

        return build_feature_dataset_lookup(rows)
    except Exception:
        return {}
    finally:
        if layer is not None:
            source_ds.ReleaseResultSet(layer)


def collect_layer_infos(source_ds):
    feature_dataset_lookup = read_feature_dataset_lookup(source_ds)
    layer_infos = []
    for index in range(source_ds.GetLayerCount()):
        layer = source_ds.GetLayerByIndex(index)
        if layer is None:
            continue

        source_name = get_layer_name(layer)
        metadata = read_layer_metadata(source_ds, source_name)
        output_name = get_output_name_from_layer_name(source_name)
        feature_dataset_name = (
            feature_dataset_lookup.get(source_name)
            or feature_dataset_lookup.get(output_name)
            or feature_dataset_lookup.get(get_last_segment(source_name))
            or get_dataset_name_from_layer_name(source_name)
            or metadata["feature_dataset_name"]
        )
        is_table = (
            layer.GetLayerDefn() is None
            or layer.GetLayerDefn().GetGeomType() == ogr.wkbNone
        )

        layer_infos.append(
            {
                "index": index,
                "source_name": source_name,
                "output_name": output_name,
                "feature_dataset_name": normalize_layer_path(feature_dataset_name),
                "alias_name": metadata["alias_name"] or output_name,
                "field_aliases": metadata["field_aliases"],
                "is_table": is_table,
            }
        )

    return layer_infos


def ensure_file_gdb(output_gdb):
    if arcpy.Exists(output_gdb):
        return
    output_folder = os.path.dirname(output_gdb)
    gdb_name = os.path.splitext(os.path.basename(output_gdb))[0]
    arcpy.management.CreateFileGDB(output_folder, gdb_name)


def ensure_feature_dataset(output_gdb, dataset_name, spatial_reference):
    if not dataset_name:
        return
    dataset_path = os.path.join(output_gdb, dataset_name)
    if arcpy.Exists(dataset_path):
        return
    arcpy.management.CreateFeatureDataset(output_gdb, dataset_name, spatial_reference)


def map_geometry_type(geometry_type):
    flat_type = ogr.GT_Flatten(geometry_type)
    if flat_type == ogr.wkbPoint:
        return "POINT"
    if flat_type == ogr.wkbMultiPoint:
        return "MULTIPOINT"
    if flat_type in (ogr.wkbLineString, ogr.wkbMultiLineString):
        return "POLYLINE"
    if flat_type in (ogr.wkbPolygon, ogr.wkbMultiPolygon):
        return "POLYGON"
    raise NotImplementedError(f"不支持的几何类型: {geometry_type}")


def map_field_type(field_defn):
    field_type = field_defn.GetType()
    field_subtype = field_defn.GetSubType()
    serialize_as_text = False

    if field_type == ogr.OFTInteger and field_subtype == ogr.OFSTInt16:
        return "SHORT", serialize_as_text
    if field_type == ogr.OFTInteger:
        return "LONG", serialize_as_text
    if field_type == ogr.OFTInteger64:
        return "BIGINTEGER", serialize_as_text
    if field_type == ogr.OFTReal and field_subtype == ogr.OFSTFloat32:
        return "FLOAT", serialize_as_text
    if field_type == ogr.OFTReal:
        return "DOUBLE", serialize_as_text
    if (
        field_type in (ogr.OFTString, ogr.OFTWideString)
        and field_subtype == ogr.OFSTUUID
    ):
        return "GUID", serialize_as_text
    if field_type in (ogr.OFTString, ogr.OFTWideString):
        return "TEXT", serialize_as_text
    if field_type == ogr.OFTBinary:
        return "BLOB", serialize_as_text
    if field_type == ogr.OFTDate:
        return "DATEONLY", serialize_as_text
    if field_type == ogr.OFTTime:
        return "TIMEONLY", serialize_as_text
    if field_type == ogr.OFTDateTime:
        return "DATE", serialize_as_text

    return "TEXT", True


def should_skip_field(field_name, is_table):
    if not field_name:
        return True
    upper_name = field_name.upper()
    if upper_name == "OBJECTID":
        return True
    if not is_table and upper_name in ("SHAPE_LENGTH", "SHAPE_AREA"):
        return True
    return False


def build_fields(defn, layer_info):
    fields = []
    for source_index in range(defn.GetFieldCount()):
        field_defn = defn.GetFieldDefn(source_index)
        field_name = decode_best_text(field_defn.GetNameRef()).strip()
        if should_skip_field(field_name, layer_info["is_table"]):
            continue

        target_type, serialize_as_text = map_field_type(field_defn)
        alias_name = (
            layer_info["field_aliases"].get(field_name)
            or decode_best_text(field_defn.GetAlternativeNameRef())
            or field_name
        )

        fields.append(
            {
                "source_index": source_index,
                "name": field_name,
                "alias_name": alias_name.strip(),
                "target_type": target_type,
                "serialize_as_text": serialize_as_text,
                "length": field_defn.GetWidth()
                if target_type == "TEXT" and field_defn.GetWidth() > 0
                else (255 if target_type == "TEXT" else None),
                "precision": field_defn.GetPrecision()
                if field_defn.GetPrecision() > 0
                else None,
                "scale": None,
                "nullable": bool(field_defn.IsNullable()),
            }
        )

    return fields


def read_datetime_value(feature, field_index, mode):
    try:
        year, month, day, hour, minute, seconds, _timezone = feature.GetFieldAsDateTime(
            field_index
        )
    except Exception:
        return None

    if mode == "DATEONLY":
        if year <= 0 or month <= 0 or day <= 0:
            return None
        return dt.date(year, month, day)

    second = int(seconds)
    microsecond = int(round((float(seconds) - second) * 1_000_000))
    microsecond = max(0, min(999999, microsecond))

    if mode == "TIMEONLY":
        return dt.time(hour, minute, second, microsecond)

    if year <= 0 or month <= 0 or day <= 0:
        return None
    return dt.datetime(year, month, day, hour, minute, second, microsecond)


def read_field_value(feature, field):
    field_index = field["source_index"]
    if not feature.IsFieldSet(field_index) or feature.IsFieldNull(field_index):
        return None

    target_type = field["target_type"]
    if field["serialize_as_text"]:
        return get_field_as_text(feature, field_index)

    if target_type in ("SHORT", "LONG"):
        return int(feature.GetFieldAsInteger(field_index))
    if target_type == "BIGINTEGER":
        return int(feature.GetFieldAsInteger64(field_index))
    if target_type in ("FLOAT", "DOUBLE"):
        return float(feature.GetFieldAsDouble(field_index))
    if target_type in ("TEXT", "GUID"):
        return get_field_as_text(feature, field_index)
    if target_type == "BLOB":
        return feature.GetFieldAsBinary(field_index)
    if target_type in ("DATE", "DATEONLY", "TIMEONLY"):
        return read_datetime_value(feature, field_index, target_type)

    return get_field_as_text(feature, field_index)


def prepare_geometry_for_insert(geometry):
    if geometry is None or geometry.IsEmpty():
        return None, False

    has_curve = False
    try:
        has_curve = bool(geometry.HasCurveGeometry())
    except Exception:
        has_curve = False

    try:
        geometry_name = (geometry.GetGeometryName() or "").upper()
    except Exception:
        geometry_name = ""

    if has_curve or "CURVE" in geometry_name or "CIRCULAR" in geometry_name:
        try:
            linear = geometry.Clone().GetLinearGeometry()
            if linear is not None and not linear.IsEmpty():
                return linear, True
        except Exception:
            pass

    return geometry, False


def create_spatial_reference(spatial_ref):
    wkt_text = spatial_ref.ExportToWkt(["FORMAT=WKT1_ESRI"])
    arc_spatial_reference = arcpy.SpatialReference()
    arc_spatial_reference.loadFromString(wkt_text)
    return arc_spatial_reference


def ensure_schema(output_gdb, layer, layer_info):
    defn = layer.GetLayerDefn()
    fields = build_fields(defn, layer_info)
    output_name = layer_info["output_name"]
    dataset_name = layer_info["feature_dataset_name"]
    alias_name = layer_info["alias_name"] or output_name

    if layer_info["is_table"]:
        dataset_path = os.path.join(output_gdb, output_name)
        if not arcpy.Exists(dataset_path):
            arcpy.management.CreateTable(output_gdb, output_name, out_alias=alias_name)
        spatial_reference = None
    else:
        spatial_ref = layer.GetSpatialRef()
        if spatial_ref is None:
            raise RuntimeError(f"图层缺少空间参考: {layer_info['source_name']}")
        spatial_reference = create_spatial_reference(spatial_ref)
        ensure_feature_dataset(output_gdb, dataset_name, spatial_reference)
        target_workspace = (
            os.path.join(output_gdb, dataset_name) if dataset_name else output_gdb
        )
        dataset_path = os.path.join(target_workspace, output_name)
        if not arcpy.Exists(dataset_path):
            arcpy.management.CreateFeatureclass(
                target_workspace,
                output_name,
                map_geometry_type(defn.GetGeomType()),
                has_m="ENABLED" if ogr.GT_HasM(defn.GetGeomType()) else "DISABLED",
                has_z="ENABLED" if ogr.GT_HasZ(defn.GetGeomType()) else "DISABLED",
                spatial_reference=spatial_reference,
                out_alias=alias_name,
            )

    existing_field_names = {
        field.name.upper() for field in arcpy.ListFields(dataset_path)
    }
    for field in fields:
        if field["name"].upper() in existing_field_names:
            continue

        arcpy.management.AddField(
            dataset_path,
            field["name"],
            field["target_type"],
            field_precision=field["precision"],
            field_length=field["length"],
            field_alias=field["alias_name"],
            field_is_nullable="NULLABLE" if field["nullable"] else "NON_NULLABLE",
        )
    return dataset_path, spatial_reference, fields


def insert_rows(dataset_path, spatial_reference, layer, fields, is_table):
    cursor_fields = [field["name"] for field in fields]
    if not is_table:
        cursor_fields.append("SHAPE@WKB")

    inserted_count = 0
    fallback_count = 0
    linearized_count = 0
    with arcpy.da.InsertCursor(dataset_path, cursor_fields) as cursor:
        layer.ResetReading()
        feature = layer.GetNextFeature()
        while feature is not None:
            values = [read_field_value(feature, field) for field in fields]
            prepared_geometry = None
            linearized_for_feature = False
            if not is_table:
                geometry = feature.GetGeometryRef()
                if geometry is None or geometry.IsEmpty():
                    values.append(None)
                else:
                    prepared_geometry, linearized = prepare_geometry_for_insert(
                        geometry
                    )
                    if prepared_geometry is None:
                        values.append(None)
                    else:
                        linearized_for_feature = bool(linearized)
                        wkb = bytes(prepared_geometry.ExportToWkb())
                        values.append(wkb)
            try:
                cursor.insertRow(values)
            except RuntimeError as ex:
                if not is_table and "invalid WKB data" in str(ex):
                    fallback_geometry = prepared_geometry
                    if fallback_geometry is None:
                        geometry = feature.GetGeometryRef()
                        fallback_geometry, linearized = prepare_geometry_for_insert(
                            geometry
                        )
                        linearized_for_feature = bool(
                            linearized and fallback_geometry is not None
                        )

                    if fallback_geometry is None:
                        fallback_values = values[:-1] + [None]
                    else:
                        fallback_values = values[:-1] + [
                            arcpy.FromWKB(
                                bytes(fallback_geometry.ExportToWkb()),
                                spatial_reference,
                            )
                        ]
                    cursor.insertRow(fallback_values)
                    fallback_count += 1
                else:
                    raise
            if linearized_for_feature:
                linearized_count += 1
            inserted_count += 1
            feature = layer.GetNextFeature()

    if linearized_count:
        warn(f"{os.path.basename(dataset_path)} 线性化曲线几何 {linearized_count} 条")
    if fallback_count:
        warn(f"{os.path.basename(dataset_path)} 使用 FromWKB 回退 {fallback_count} 条")
    return inserted_count


def convert(input_mdb, output_gdb):
    ogr.UseExceptions()
    arcpy.SetLogHistory(False)

    source_ds = ogr.Open(input_mdb, 0)
    if source_ds is None:
        raise RuntimeError("打开 MDB 失败，请确认 ArcGIS Pro 内置 PGeo 驱动可用。")

    ensure_file_gdb(output_gdb)
    layer_infos = collect_layer_infos(source_ds)
    info(f"检测到图层/表数量: {len(layer_infos)}")

    for layer_info in layer_infos:
        layer = source_ds.GetLayerByIndex(layer_info["index"])
        if layer is None:
            continue

        display_name = layer_info["output_name"]
        if layer_info["feature_dataset_name"]:
            display_name = f"{layer_info['feature_dataset_name']}\\{display_name}"

        info(f"转换: {display_name}")
        dataset_path, spatial_reference, fields = ensure_schema(
            output_gdb, layer, layer_info
        )
        inserted_count = insert_rows(
            dataset_path, spatial_reference, layer, fields, layer_info["is_table"]
        )
        info(f"写入记录数: {inserted_count}")


def convert_jobs(jobs):
    has_failure = False
    for job in jobs:
        index = int(job["index"])
        input_mdb = job["input"]
        output_gdb = job["output"]
        display_name = os.path.splitext(os.path.basename(input_mdb))[0]
        info_event("START", index, display_name)
        try:
            convert(input_mdb, output_gdb)
            info_event("DONE", index, output_gdb)
        except Exception as ex:
            has_failure = True
            error(f"{display_name} 转换失败: {ex}")
            error_event("FAILED", index, str(ex))
    return 1 if has_failure else 0


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input")
    parser.add_argument("--output")
    parser.add_argument("--manifest")
    return parser.parse_args()


def main():
    configure_stdio()
    args = parse_args()

    try:
        if args.manifest:
            with open(args.manifest, "r", encoding="utf-8") as f:
                jobs = json.load(f)
            return convert_jobs(jobs)

        if not args.input or not args.output:
            raise RuntimeError("缺少输入或输出参数。")
        convert(args.input, args.output)
    except Exception as ex:
        error(str(ex))
        error(traceback.format_exc())
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
