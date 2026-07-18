using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure
{
    internal sealed class FileSystemOvertureDataFolderStore : IOvertureDataFolderStore
    {
        public Task<bool> ExistsAsync(string folderPath, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Directory.Exists(folderPath);
            }, cancellationToken);
        }

        public Task<bool> ContainsParquetFilesAsync(
            string folderPath,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Directory.Exists(folderPath) &&
                       Directory.EnumerateFiles(folderPath, "*.parquet").Any();
            }, cancellationToken);
        }

        public Task DeleteAsync(string folderPath, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, recursive: true);
                }
            }, cancellationToken);
        }
    }
}
