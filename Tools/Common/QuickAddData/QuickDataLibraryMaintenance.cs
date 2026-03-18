namespace XIAOFUTools.Tools.QuickAddData
{
    public static class QuickDataLibraryMaintenance
    {
        public static bool HydrateExistingLibraryOnDisk()
        {
            var store = new QuickDataLibraryStore();
            var importService = new QuickDataImportService(new ArcGisQuickDataGeodatabaseInspector());
            var document = store.Load();
            var changed = QuickDataLibraryHydrator.HydrateGeodatabases(document, importService);
            if (changed)
            {
                store.Save(document);
            }

            return changed;
        }
    }
}
