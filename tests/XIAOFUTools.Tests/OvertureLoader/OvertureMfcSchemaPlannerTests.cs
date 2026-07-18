using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class OvertureMfcSchemaPlannerTests
{
    [Fact]
    public void SqlBuilders_NormalizeAndEscapeLocalPaths()
    {
        var path = Path.Combine(Path.GetTempPath(), "O'Brien", "land.parquet");

        var describeSql = OvertureMfcSchemaPlanner.BuildDescribeSql(path);
        var geometrySql = OvertureMfcSchemaPlanner.BuildGeometryTypeSql(path);

        Assert.Contains("O''Brien", describeSql, StringComparison.Ordinal);
        Assert.DoesNotContain('\\', describeSql);
        Assert.Contains("DESCRIBE SELECT * FROM read_parquet", describeSql, StringComparison.Ordinal);
        Assert.Contains("ST_GeometryType(geometry)", geometrySql, StringComparison.Ordinal);
        Assert.Contains("geometry IS NOT NULL", geometrySql, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanDataset_PreservesDivisionFieldRulesAndGeometryContract()
    {
        var dataset = OvertureMfcSchemaPlanner.PlanDataset(
            "division",
            new[]
            {
                new OvertureDuckDbColumn("id", "VARCHAR"),
                new OvertureDuckDbColumn("sources", "STRUCT(provider VARCHAR)"),
                new OvertureDuckDbColumn("perspectives", "VARCHAR"),
                new OvertureDuckDbColumn("norms", "VARCHAR"),
                new OvertureDuckDbColumn("is_land", "BOOLEAN"),
                new OvertureDuckDbColumn("geometry", "BLOB")
            },
            "MULTIPOLYGON");

        Assert.DoesNotContain(dataset.Fields, field => field.Name == "sources");
        Assert.Contains(dataset.Fields, field => field.Name == "perspectives_mode");
        Assert.Contains(dataset.Fields, field => field.Name == "norms_driving_side");
        Assert.Contains(dataset.Fields, field =>
            field.Name == "is_land" &&
            field.Type == "String" &&
            field.SourceType == "Boolean");
        Assert.Equal(
            new[] { "bbox_xmin", "bbox_xmax", "bbox_ymin", "bbox_ymax" },
            dataset.Fields
                .Where(field => field.Name.StartsWith("bbox_", StringComparison.Ordinal))
                .Select(field => field.Name));
        Assert.True(
            dataset.Fields.ToList().FindIndex(field => field.Name == "geometry") <
            dataset.Fields.ToList().FindIndex(field => field.Name == "is_land"));
        Assert.Equal("esriGeometryPolygon", dataset.Geometry?.GeometryType);
        Assert.Equal(4326, dataset.Geometry?.SpatialReference.Wkid);
        Assert.Equal("WKB", dataset.Geometry?.Fields.Single().Formats.Single());
    }

    [Fact]
    public void PlanDataset_ExpandsKnownStructsAndPlaceCompatibilityFields()
    {
        var dataset = OvertureMfcSchemaPlanner.PlanDataset(
            "place",
            new[]
            {
                new OvertureDuckDbColumn("id", "VARCHAR"),
                new OvertureDuckDbColumn("names", "STRUCT(primary VARCHAR)"),
                new OvertureDuckDbColumn("categories", "STRUCT(primary VARCHAR)"),
                new OvertureDuckDbColumn("brand", "STRUCT(wikidata VARCHAR)")
            },
            detectedGeometryType: null);

        Assert.Contains(dataset.Fields, field => field.Name == "names_primary");
        Assert.Contains(dataset.Fields, field => field.Name == "categories_primary");
        Assert.Contains(dataset.Fields, field => field.Name == "brand_wikidata");
        Assert.Contains(dataset.Fields, field => field.Name == "brand_names_primary");
        Assert.Null(dataset.Geometry);
    }

    [Theory]
    [InlineData("POINT", "esriGeometryPoint")]
    [InlineData("MULTILINESTRING", "esriGeometryPolyline")]
    [InlineData("POLYGON", "esriGeometryPolygon")]
    [InlineData("GEOMETRYCOLLECTION", "esriGeometryPoint")]
    public void GeometryTypeMapper_PreservesExistingMapping(
        string duckDbType,
        string expectedEsriType)
    {
        Assert.Equal(
            expectedEsriType,
            OvertureGeometryTypeMapper.ToEsriGeometryType(duckDbType));
    }
}
