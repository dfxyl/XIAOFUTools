using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Application
{
    internal interface IOvertureMfcDataFolderInspector
    {
        Task<bool> EnsureOutputFolderAsync(string folderPath, CancellationToken cancellationToken);

        Task<OvertureMfcDataFolderInspection> InspectAsync(
            string folderPath,
            CancellationToken cancellationToken);
    }
}
