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


if __name__ == "__main__":
    unittest.main()
