using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace HighAvailability.Services
{
    public interface IJobOutputStatusSyncService
    {
        Task SyncJobOutputStatusAsync(DateTime currentTime, ILogger logger);
    }
}