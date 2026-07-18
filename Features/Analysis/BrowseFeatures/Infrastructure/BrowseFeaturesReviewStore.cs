using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using XIAOFUTools.Features.Analysis.BrowseFeatures.Core;

namespace XIAOFUTools.Features.Analysis.BrowseFeatures.Infrastructure
{
    internal sealed class BrowseFeaturesReviewStore
    {
        private const string FieldProjectKey = "ProjectKey";
        private const string FieldBatchId = "BatchId";
        private const string FieldBatchName = "BatchName";
        private const string FieldReviewer = "Reviewer";
        private const string FieldReviewStatus = "ReviewStatus";
        private const string FieldNotes = "Notes";
        private const string FieldLayerName = "LayerName";
        private const string FieldLayerUri = "LayerUri";
        private const string FieldSourceObjectId = "SourceObjectId";
        private const string FieldGeometryType = "GeometryType";
        private const string FieldGeometryWkt = "GeometryWkt";
        private const string FieldVisitedAt = "VisitedAt";
        private const string FieldUpdatedAt = "UpdatedAt";

        public bool HasRequiredSchema(string gdbPath, string tableName)
        {
            using var geodatabase = OpenGeodatabase(gdbPath);
            using var table = geodatabase.OpenDataset<Table>(tableName);
            var definition = table.GetDefinition();
            return definition.FindField(FieldProjectKey) >= 0 &&
                   definition.FindField(FieldBatchId) >= 0 &&
                   definition.FindField(FieldLayerUri) >= 0 &&
                   definition.FindField(FieldSourceObjectId) >= 0 &&
                   definition.FindField(FieldReviewStatus) >= 0 &&
                   definition.FindField(FieldNotes) >= 0 &&
                   definition.FindField(FieldUpdatedAt) >= 0;
        }

        public bool TableExists(string gdbPath, string tableName)
        {
            using var geodatabase = OpenGeodatabase(gdbPath);
            return TableExists(geodatabase, tableName);
        }

        public void CreateTable(string gdbPath, string tableName)
        {
            using var geodatabase = OpenGeodatabase(gdbPath);
            if (TableExists(geodatabase, tableName))
            {
                throw new InvalidOperationException($"审阅清单表已存在: {tableName}");
            }

            var fieldDescriptions = new List<FieldDescription>
            {
                new FieldDescription(FieldProjectKey, FieldType.String) { Length = 500 },
                new FieldDescription(FieldBatchId, FieldType.String) { Length = 80 },
                new FieldDescription(FieldBatchName, FieldType.String) { Length = 120 },
                new FieldDescription(FieldReviewer, FieldType.String) { Length = 120 },
                new FieldDescription(FieldReviewStatus, FieldType.String) { Length = 20 },
                new FieldDescription(FieldNotes, FieldType.String) { Length = 4000 },
                new FieldDescription(FieldLayerName, FieldType.String) { Length = 260 },
                new FieldDescription(FieldLayerUri, FieldType.String) { Length = 1200 },
                new FieldDescription(FieldSourceObjectId, FieldType.Integer),
                new FieldDescription(FieldGeometryType, FieldType.String) { Length = 40 },
                new FieldDescription(FieldGeometryWkt, FieldType.String) { Length = 4000 },
                new FieldDescription(FieldVisitedAt, FieldType.Date),
                new FieldDescription(FieldUpdatedAt, FieldType.Date)
            };

            var tableDescription = new TableDescription(tableName, fieldDescriptions);
            var schemaBuilder = new SchemaBuilder(geodatabase);
            schemaBuilder.Create(tableDescription);

            if (!schemaBuilder.Build())
            {
                throw new InvalidOperationException($"创建审阅清单表失败: {tableName}");
            }
        }

        public ReviewRecord TryGet(string gdbPath, string tableName, string projectKey, string batchId, string layerUri, long objectId)
        {
            using var geodatabase = OpenGeodatabase(gdbPath);
            using var table = geodatabase.OpenDataset<Table>(tableName);
            var whereClause = BuildUniqueWhereClause(projectKey, batchId, layerUri, objectId);

            using var cursor = table.Search(new QueryFilter { WhereClause = whereClause }, false);
            if (!cursor.MoveNext())
            {
                return null;
            }

            using var row = cursor.Current;
            return new ReviewRecord
            {
                ProjectKey = Convert.ToString(row[FieldProjectKey]) ?? string.Empty,
                BatchId = Convert.ToString(row[FieldBatchId]) ?? string.Empty,
                BatchName = Convert.ToString(row[FieldBatchName]) ?? string.Empty,
                Reviewer = Convert.ToString(row[FieldReviewer]) ?? string.Empty,
                ReviewStatus = BrowseFeaturesCore.NormalizeReviewStatus(Convert.ToString(row[FieldReviewStatus])),
                Notes = Convert.ToString(row[FieldNotes]) ?? string.Empty,
                LayerName = Convert.ToString(row[FieldLayerName]) ?? string.Empty,
                LayerUri = Convert.ToString(row[FieldLayerUri]) ?? string.Empty,
                SourceObjectId = SafeReadLong(row[FieldSourceObjectId]),
                GeometryType = Convert.ToString(row[FieldGeometryType]) ?? string.Empty,
                GeometryWkt = Convert.ToString(row[FieldGeometryWkt]) ?? string.Empty,
                VisitedAt = SafeReadDateTime(row[FieldVisitedAt]),
                UpdatedAt = SafeReadDateTime(row[FieldUpdatedAt])
            };
        }

        public void Upsert(string gdbPath, string tableName, ReviewRecord record)
        {
            using var geodatabase = OpenGeodatabase(gdbPath);
            using var table = geodatabase.OpenDataset<Table>(tableName);
            var whereClause = BuildUniqueWhereClause(record.ProjectKey, record.BatchId, record.LayerUri, record.SourceObjectId);

            using var cursor = table.Search(new QueryFilter { WhereClause = whereClause }, false);
            if (cursor.MoveNext())
            {
                using var row = cursor.Current;
                row[FieldProjectKey] = record.ProjectKey ?? string.Empty;
                row[FieldBatchName] = record.BatchName ?? string.Empty;
                row[FieldReviewer] = record.Reviewer ?? string.Empty;
                row[FieldReviewStatus] = BrowseFeaturesCore.ToDisplayText(record.ReviewStatus);
                row[FieldNotes] = record.Notes ?? string.Empty;
                row[FieldLayerName] = record.LayerName ?? string.Empty;
                row[FieldGeometryType] = record.GeometryType ?? string.Empty;
                row[FieldGeometryWkt] = Truncate(record.GeometryWkt, 4000);
                row[FieldUpdatedAt] = record.UpdatedAt;
                row.Store();
                return;
            }

            using var rowBuffer = table.CreateRowBuffer();
            rowBuffer[FieldProjectKey] = record.ProjectKey ?? string.Empty;
            rowBuffer[FieldBatchId] = record.BatchId ?? string.Empty;
            rowBuffer[FieldBatchName] = record.BatchName ?? string.Empty;
            rowBuffer[FieldReviewer] = record.Reviewer ?? string.Empty;
            rowBuffer[FieldReviewStatus] = BrowseFeaturesCore.ToDisplayText(record.ReviewStatus);
            rowBuffer[FieldNotes] = record.Notes ?? string.Empty;
            rowBuffer[FieldLayerName] = record.LayerName ?? string.Empty;
            rowBuffer[FieldLayerUri] = record.LayerUri ?? string.Empty;
            rowBuffer[FieldSourceObjectId] = record.SourceObjectId;
            rowBuffer[FieldGeometryType] = record.GeometryType ?? string.Empty;
            rowBuffer[FieldGeometryWkt] = Truncate(record.GeometryWkt, 4000);
            rowBuffer[FieldVisitedAt] = record.VisitedAt;
            rowBuffer[FieldUpdatedAt] = record.UpdatedAt;
            using var _ = table.CreateRow(rowBuffer);
        }

        public long? TryGetLatestObjectId(string gdbPath, string tableName, string projectKey, string batchId, string layerUri)
        {
            using var geodatabase = OpenGeodatabase(gdbPath);
            using var table = geodatabase.OpenDataset<Table>(tableName);
            var whereClause = BuildProjectLayerWhereClause(projectKey, batchId, layerUri);

            using var cursor = table.Search(new QueryFilter { WhereClause = whereClause }, false);
            DateTime latestUpdatedAt = DateTime.MinValue;
            long? latestObjectId = null;
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                var updatedAt = SafeReadDateTime(row[FieldUpdatedAt]);
                if (updatedAt >= latestUpdatedAt)
                {
                    latestUpdatedAt = updatedAt;
                    latestObjectId = SafeReadLong(row[FieldSourceObjectId]);
                }
            }

            return latestObjectId;
        }

        public IReadOnlyList<string> GetCompatibleTableNames(string gdbPath, string namePrefix)
        {
            using var geodatabase = OpenGeodatabase(gdbPath);
            var tableNames = new List<string>();
            foreach (var definition in geodatabase.GetDefinitions<TableDefinition>())
            {
                var tableName = definition.GetName();
                if (!string.IsNullOrWhiteSpace(namePrefix) &&
                    !tableName.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (definition.FindField(FieldProjectKey) >= 0 &&
                    definition.FindField(FieldBatchId) >= 0 &&
                    definition.FindField(FieldLayerUri) >= 0 &&
                    definition.FindField(FieldSourceObjectId) >= 0 &&
                    definition.FindField(FieldReviewStatus) >= 0 &&
                    definition.FindField(FieldNotes) >= 0 &&
                    definition.FindField(FieldUpdatedAt) >= 0)
                {
                    tableNames.Add(tableName);
                }
            }

            return tableNames
                .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string? TryGetLatestBatchId(string gdbPath, string tableName, string projectKey, string layerUri)
        {
            using var geodatabase = OpenGeodatabase(gdbPath);
            using var table = geodatabase.OpenDataset<Table>(tableName);
            var whereClause = BuildProjectLayerBaseWhereClause(projectKey, layerUri);

            using var cursor = table.Search(new QueryFilter { WhereClause = whereClause }, false);
            DateTime latestUpdatedAt = DateTime.MinValue;
            string? latestBatchId = null;
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                var updatedAt = SafeReadDateTime(row[FieldUpdatedAt]);
                if (updatedAt >= latestUpdatedAt)
                {
                    latestUpdatedAt = updatedAt;
                    latestBatchId = Convert.ToString(row[FieldBatchId]);
                }
            }

            return string.IsNullOrWhiteSpace(latestBatchId) ? null : latestBatchId;
        }

        private static Geodatabase OpenGeodatabase(string gdbPath)
        {
            if (string.IsNullOrWhiteSpace(gdbPath))
            {
                throw new ArgumentException("输出地理数据库路径为空。", nameof(gdbPath));
            }

            return new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));
        }

        private static bool TableExists(Geodatabase geodatabase, string tableName)
        {
            try
            {
                using var _ = geodatabase.OpenDataset<Table>(tableName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildUniqueWhereClause(string projectKey, string batchId, string layerUri, long objectId)
        {
            var safeProjectKey = EscapeSqlLiteral(projectKey);
            var safeBatchId = EscapeSqlLiteral(batchId);
            var safeLayerUri = EscapeSqlLiteral(layerUri);
            return $"{FieldProjectKey} = '{safeProjectKey}' AND {FieldBatchId} = '{safeBatchId}' AND {FieldLayerUri} = '{safeLayerUri}' AND {FieldSourceObjectId} = {objectId}";
        }

        private static string BuildProjectLayerWhereClause(string projectKey, string batchId, string layerUri)
        {
            var safeProjectKey = EscapeSqlLiteral(projectKey);
            var safeBatchId = EscapeSqlLiteral(batchId);
            var safeLayerUri = EscapeSqlLiteral(layerUri);
            return $"{FieldProjectKey} = '{safeProjectKey}' AND {FieldBatchId} = '{safeBatchId}' AND {FieldLayerUri} = '{safeLayerUri}'";
        }

        private static string BuildProjectLayerBaseWhereClause(string projectKey, string layerUri)
        {
            var safeProjectKey = EscapeSqlLiteral(projectKey);
            var safeLayerUri = EscapeSqlLiteral(layerUri);
            return $"{FieldProjectKey} = '{safeProjectKey}' AND {FieldLayerUri} = '{safeLayerUri}'";
        }

        private static string EscapeSqlLiteral(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        private static long SafeReadLong(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return 0L;
            }

            if (value is long l)
            {
                return l;
            }

            if (value is int i)
            {
                return i;
            }

            return long.TryParse(Convert.ToString(value), out var parsed) ? parsed : 0L;
        }

        private static DateTime SafeReadDateTime(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return DateTime.MinValue;
            }

            if (value is DateTime dt)
            {
                return dt;
            }

            return DateTime.TryParse(Convert.ToString(value), out var parsed) ? parsed : DateTime.MinValue;
        }
    }
}
