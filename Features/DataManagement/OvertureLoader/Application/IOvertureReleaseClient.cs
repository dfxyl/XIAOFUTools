using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Application
{
    internal interface IOvertureReleaseClient
    {
        Task<string> GetLatestReleaseAsync(CancellationToken cancellationToken);
    }
}
