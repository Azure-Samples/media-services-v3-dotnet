﻿namespace HighAvailability.Services
{
    using Microsoft.Azure.Management.Media;
    using System.Threading.Tasks;

    public interface IMediaServiceInstanceFactory
    {
        Task<IAzureMediaServicesClient> GetMediaServiceInstanceAsync(string accountName);
    }
}