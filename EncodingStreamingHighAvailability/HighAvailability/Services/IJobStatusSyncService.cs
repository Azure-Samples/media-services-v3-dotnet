using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace HighAvailability.Services
{
    public interface IJobStatusSyncService
    {
        Task SyncJobStatusAsync(DateTime currentTime, ILogger logger);
    }
}