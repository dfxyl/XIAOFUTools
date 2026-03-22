import importlib.util
import pathlib
import sys
import types
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
SCRIPT_PATH = ROOT / "scripts" / "mdb_to_gdb_arcgispro.py"


def load_module():
    fake_arcpy = types.ModuleType("arcpy")
    fake_osgeo = types.ModuleType("osgeo")
    fake_ogr = types.SimpleNamespace()
    setattr(fake_osgeo, "ogr", fake_ogr)

    previous_arcpy = sys.modules.get("arcpy")
    previous_osgeo = sys.modules.get("osgeo")

    sys.modules["arcpy"] = fake_arcpy
    sys.modules["osgeo"] = fake_osgeo

    try:
        spec = importlib.util.spec_from_file_location(
            "mdb_to_gdb_arcgispro", SCRIPT_PATH
        )
        assert spec is not None
        assert spec.loader is not None
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module
    finally:
        if previous_arcpy is None:
            sys.modules.pop("arcpy", None)
        else:
            sys.modules["arcpy"] = previous_arcpy

        if previous_osgeo is None:
            sys.modules.pop("osgeo", None)
        else:
            sys.modules["osgeo"] = previous_osgeo


class FakeGeometry:
    def __init__(self, name, linear_geometry=None, has_curve=False, empty=False):
        self._name = name
        self._linear_geometry = linear_geometry
        self._has_curve = has_curve
        self._empty = empty
        self.clone_calls = 0

    def IsEmpty(self):
        return self._empty

    def GetGeometryName(self):
        return self._name

    def HasCurveGeometry(self):
        return 1 if self._has_curve else 0

    def Clone(self):
        self.clone_calls += 1
        return self

    def GetLinearGeometry(self):
        return self._linear_geometry


class FakeCurveGeometry:
    def __init__(self, name, points=None, children=None, geometry_type=0):
        self._name = name
        self._points = points or []
        self._children = children or []
        self._geometry_type = geometry_type

    def GetGeometryName(self):
        return self._name

    def GetGeometryType(self):
        return self._geometry_type

    def GetGeometryCount(self):
        return len(self._children)

    def GetGeometryRef(self, index):
        return self._children[index]

    def GetPointCount(self):
        return len(self._points)

    def GetPoint(self, index):
        return self._points[index]

    def HasCurveGeometry(self):
        name = self._name.upper()
        return 1 if ("CURVE" in name or "CIRCULAR" in name) else 0


class FakeArcGeometry:
    def __init__(self, has_curves):
        self.hasCurves = has_curves


class CountingSourceDataset:
    def __init__(self):
        self.sql_count = 0

    def ExecuteSQL(self, _sql, _unused, _dialect):
        self.sql_count += 1
        return None

    def ReleaseResultSet(self, _layer):
        return None


class FakeFeatureForReaders:
    def __init__(self, values=None, null_indexes=None):
        self.values = values or {}
        self.null_indexes = set(null_indexes or [])

    def IsFieldSet(self, field_index):
        return field_index in self.values

    def IsFieldNull(self, field_index):
        return field_index in self.null_indexes

    def GetFieldAsInteger(self, field_index):
        return self.values[field_index]

    def GetFieldAsInteger64(self, field_index):
        return self.values[field_index]

    def GetFieldAsDouble(self, field_index):
        return self.values[field_index]

    def GetFieldAsString(self, field_index):
        return self.values[field_index]

    def GetFieldAsBinary(self, field_index):
        return self.values[field_index]

    def GetFieldAsDateTime(self, field_index):
        return self.values[field_index]


class PrepareGeometryForInsertTests(unittest.TestCase):
    def test_linearizes_hidden_curve_geometries(self):
        module = load_module()
        linear = FakeGeometry("MULTIPOLYGON")
        geometry = FakeGeometry("MULTISURFACE", linear_geometry=linear, has_curve=True)

        prepared, linearized = module.prepare_geometry_for_insert(geometry)

        self.assertIs(prepared, linear)
        self.assertTrue(linearized)

    def test_keeps_name_based_curve_detection_as_fallback(self):
        module = load_module()
        linear = FakeGeometry("LINESTRING")
        geometry = FakeGeometry(
            "CIRCULARSTRING", linear_geometry=linear, has_curve=False
        )

        prepared, linearized = module.prepare_geometry_for_insert(geometry)

        self.assertIs(prepared, linear)
        self.assertTrue(linearized)

    def test_keeps_linear_geometry_unchanged(self):
        module = load_module()
        geometry = FakeGeometry("POLYGON", has_curve=False)

        prepared, linearized = module.prepare_geometry_for_insert(geometry)

        self.assertIs(prepared, geometry)
        self.assertFalse(linearized)


class MetadataParsingTests(unittest.TestCase):
    def test_uses_layer_scoped_feature_dataset_name(self):
        module = load_module()
        info = {"feature_dataset_name": "", "alias_name": "", "field_aliases": {}}
        xml_text = """
<Metadata>
  <Layer>
    <Name>OtherLayer</Name>
    <FeatureDatasetName>DatasetA</FeatureDatasetName>
  </Layer>
  <Layer>
    <Name>Parcel</Name>
    <FeatureDatasetName>DatasetB</FeatureDatasetName>
  </Layer>
</Metadata>
""".strip()

        module.merge_metadata_xml(xml_text, info, "Parcel", "Parcel")

        self.assertEqual(info["feature_dataset_name"], "DatasetB")

    def test_uses_layer_scoped_feature_dataset_name_when_xml_is_malformed(self):
        module = load_module()
        info = {"feature_dataset_name": "", "alias_name": "", "field_aliases": {}}
        xml_text = """
<Metadata>
  <Layer>
    <Name>OtherLayer</Name>
    <FeatureDatasetName>DatasetA</FeatureDatasetName>
  </Layer>
  <Layer>
    <Name>Parcel</Name>
    <FeatureDatasetName>DatasetB</FeatureDatasetName>
  </Layer>
""".strip()

        module.merge_metadata_xml(xml_text, info, "Parcel", "Parcel")

        self.assertEqual(info["feature_dataset_name"], "DatasetB")


class GdbItemsLookupTests(unittest.TestCase):
    def test_builds_feature_dataset_lookup_from_catalog_paths(self):
        module = load_module()

        lookup = module.build_feature_dataset_lookup(
            [
                {"name": "ZJS", "path": r"\ZJS"},
                {"name": "测试", "path": r"\测试"},
                {"name": "DLX", "path": r"\ZJS\DLX"},
                {"name": "地类图斑", "path": r"\测试\地类图斑"},
                {"name": "地类线", "path": r"\测试\地类线"},
                {"name": "XZDW", "path": r"\XZDW"},
            ]
        )

        self.assertEqual(lookup["DLX"], "ZJS")
        self.assertEqual(lookup["地类图斑"], "测试")
        self.assertEqual(lookup["地类线"], "测试")
        self.assertNotIn("XZDW", lookup)


class CurveJsonTests(unittest.TestCase):
    def test_converts_circularstring_to_esri_curve_items(self):
        module = load_module()
        circular = FakeCurveGeometry(
            "CIRCULARSTRING",
            points=[
                (0.0, 0.0, 0.0),
                (1.0, 1.0, 0.0),
                (2.0, 0.0, 0.0),
                (3.0, -1.0, 0.0),
                (4.0, 0.0, 0.0),
            ],
        )

        items = module.circular_string_to_esri_items(circular, include_start=True)

        self.assertEqual(items[0], [0.0, 0.0])
        self.assertEqual(items[1], {"c": [[2.0, 0.0], [1.0, 1.0]]})
        self.assertEqual(items[2], {"c": [[4.0, 0.0], [3.0, -1.0]]})

    def test_converts_curvepolygon_to_esri_json(self):
        module = load_module()
        circular = FakeCurveGeometry(
            "CIRCULARSTRING",
            points=[
                (10.0, 10.0, 0.0),
                (12.0, 8.0, 0.0),
                (10.0, 6.0, 0.0),
                (8.0, 8.0, 0.0),
                (10.0, 10.0, 0.0),
            ],
        )
        compound = FakeCurveGeometry("COMPOUNDCURVE", children=[circular])
        curvepolygon = FakeCurveGeometry("CURVEPOLYGON", children=[compound])

        result = module.geometry_to_esri_json(curvepolygon)

        self.assertIn("curveRings", result)
        self.assertEqual(result["curveRings"][0][0], [10.0, 10.0])
        self.assertEqual(
            result["curveRings"][0][1],
            {"c": [[10.0, 6.0], [12.0, 8.0]]},
        )


class DecodeBestTextTests(unittest.TestCase):
    def test_recovers_gbk_text_misdecoded_as_latin1(self):
        module = load_module()
        garbled = "永久基本农田储备区编号".encode("gbk").decode("latin1")

        result = module.decode_best_text(garbled)

        self.assertEqual(result, "永久基本农田储备区编号")


class MetadataCacheTests(unittest.TestCase):
    def test_reuses_metadata_lookup_result(self):
        module = load_module()
        source_ds = CountingSourceDataset()
        cache = {}

        first = module.read_layer_metadata(source_ds, "Parcel", cache)
        second = module.read_layer_metadata(source_ds, "Parcel", cache)

        self.assertEqual(source_ds.sql_count, 4)
        self.assertIs(first, second)


class FieldReaderPlanTests(unittest.TestCase):
    def test_compiles_field_readers_for_common_types(self):
        module = load_module()
        fields = [
            {"source_index": 0, "target_type": "LONG", "serialize_as_text": False},
            {"source_index": 1, "target_type": "DOUBLE", "serialize_as_text": False},
            {"source_index": 2, "target_type": "TEXT", "serialize_as_text": False},
            {"source_index": 3, "target_type": "DATEONLY", "serialize_as_text": False},
        ]
        feature = FakeFeatureForReaders(
            values={
                0: 7,
                1: 2.5,
                2: "abc",
                3: (2026, 3, 20, 9, 30, 15.0, 0),
            }
        )

        readers = module.compile_field_readers(fields)
        values = module.read_feature_values(feature, readers)

        self.assertEqual(values[0], 7)
        self.assertEqual(values[1], 2.5)
        self.assertEqual(values[2], "abc")
        self.assertEqual(str(values[3]), "2026-03-20")

    def test_builds_add_fields_payload_for_batch_schema_creation(self):
        module = load_module()
        fields = [
            {
                "name": "CODE",
                "target_type": "TEXT",
                "alias_name": "编码",
                "length": 32,
                "precision": None,
                "scale": None,
                "nullable": True,
            },
            {
                "name": "AREA",
                "target_type": "DOUBLE",
                "alias_name": "面积",
                "length": None,
                "precision": None,
                "scale": None,
                "nullable": False,
            },
        ]

        payload = module.build_add_fields_payload(fields)

        self.assertEqual(payload[0][:4], ["CODE", "TEXT", "编码", 32])
        self.assertEqual(payload[1][:3], ["AREA", "DOUBLE", "面积"])
        self.assertEqual(len(payload[0]), 4)
        self.assertEqual(len(payload[1]), 4)


class GeometryWriterPlanTests(unittest.TestCase):
    def test_simple_geometry_type_uses_fast_writer(self):
        module = load_module()
        module.ogr = types.SimpleNamespace(
            wkbPoint=1,
            wkbMultiPoint=4,
            wkbLineString=2,
            wkbMultiLineString=5,
            wkbPolygon=3,
            wkbMultiPolygon=6,
            GT_Flatten=lambda value: value,
        )
        module.arcpy = types.SimpleNamespace(
            FromWKB=lambda data, spatial_reference: ("fromwkb", data, spatial_reference)
        )

        class FakeLayerDefn:
            def GetGeomType(self):
                return 3

        class FakeLayer:
            def GetLayerDefn(self):
                return FakeLayerDefn()

        geometry = types.SimpleNamespace(
            IsEmpty=lambda: False,
            ExportToWkb=lambda: b"abc",
        )

        writer = module.build_geometry_writer(FakeLayer(), False)
        arc_geometry, linearized, used_curve_json = writer(geometry, "SR")

        self.assertEqual(arc_geometry, ("fromwkb", b"abc", "SR"))
        self.assertFalse(linearized)
        self.assertFalse(used_curve_json)

    def test_curve_feature_forces_smart_writer_even_when_layer_type_is_polygon(self):
        module = load_module()
        module.ogr = types.SimpleNamespace(
            wkbPoint=1,
            wkbMultiPoint=4,
            wkbLineString=2,
            wkbMultiLineString=5,
            wkbPolygon=3,
            wkbMultiPolygon=6,
            GT_Flatten=lambda value: value,
        )

        class FakeLayerDefn:
            def GetGeomType(self):
                return 3

        curve_geometry = FakeGeometry("CURVEPOLYGON", has_curve=True)

        class FakeFeature:
            def GetGeometryRef(self):
                return curve_geometry

        class FakeLayer:
            def __init__(self):
                self.reset_calls = 0
                self.read_count = 0

            def GetLayerDefn(self):
                return FakeLayerDefn()

            def ResetReading(self):
                self.reset_calls += 1
                self.read_count = 0

            def GetNextFeature(self):
                if self.read_count > 0:
                    return None
                self.read_count += 1
                return FakeFeature()

        layer = FakeLayer()
        writer = module.build_geometry_writer(layer, False)

        self.assertIs(writer, module.build_arcpy_geometry)
        self.assertGreaterEqual(layer.reset_calls, 1)


class CurveDegradationLoggingTests(unittest.TestCase):
    def test_detects_curve_degradation_when_target_has_no_curves(self):
        module = load_module()
        source = FakeCurveGeometry(
            "CURVEPOLYGON",
            children=[FakeCurveGeometry("LINESTRING", points=[(0, 0), (1, 1), (2, 2)])],
        )

        result = module.is_curve_degraded(source, FakeArcGeometry(False))

        self.assertTrue(result)

    def test_builds_curve_degradation_summary(self):
        module = load_module()
        source = FakeCurveGeometry(
            "CURVEPOLYGON",
            children=[FakeCurveGeometry("LINESTRING", points=[(0, 0)] * 356)],
        )

        summary = module.build_curve_degradation_summary(4, source)

        self.assertEqual(summary, "#4 CURVEPOLYGON -> LINESTRING(356点)")


if __name__ == "__main__":
    unittest.main()
