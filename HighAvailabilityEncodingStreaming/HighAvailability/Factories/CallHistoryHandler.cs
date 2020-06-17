// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace HighAvailability.Factories
{
    using HighAvailability.Helpers;
    using HighAvailability.Interfaces;
    using HighAvailability.Models;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Delegating handler to log data about each Media Services call
    /// </summary>
    public class CallHistoryHandler : DelegatingHandler
    {
        /// <summary>
        /// Storage service to persist status of all calls to Media Services APIs
        /// </summary>
        private readonly IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService;

        /// <summary>
        /// Logger to log data
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Factory to get Azure Media Service instance client
        /// </summary>
        private readonly IMediaServiceInstanceFactory mediaServiceInstanceFactory;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mediaServiceCallHistoryStorageService">Service to store Media Services call history</param>
        /// <param name="mediaServiceInstanceFactory">Factory to get Azure Media Service instance client</param>
        /// <param name="logger">Logger to log data</param>
        public CallHistoryHandler(IMediaServiceCallHistoryStorageService mediaServiceCallHistoryStorageService, IMediaServiceInstanceFactory mediaServiceInstanceFactory, ILogger logger)
        {
            this.mediaServiceCallHistoryStorageService = mediaServiceCallHistoryStorageService ?? throw new ArgumentNullException(nameof(mediaServiceCallHistoryStorageService));
            this.mediaServiceInstanceFactory = mediaServiceInstanceFactory ?? throw new ArgumentNullException(nameof(mediaServiceInstanceFactory));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Sends http request to server.
        /// </summary>
        /// <param name="request">request to send</param>
        /// <param name="cancellationToken">cancellation token</param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // any exception triggers reconnect to Media Services API
                this.mediaServiceInstanceFactory.ResetMediaServiceInstance();
                throw;
            }

            // any 5xx errors triggers reconnect to Media Services API
            if ((int)response.StatusCode > 499)
            {
                this.mediaServiceInstanceFactory.ResetMediaServiceInstance();
            }

            // Typical path that is used here, need to parse it
            // /subscriptions/<subscriptionId>/resourceGroups/<resourceGroypName>/providers/Microsoft.Media/mediaServices/<accountName>/assets/<assetName>
            // or
            // /subscriptions/<subscriptionId>/resourceGroups/<resourceGroypName>/providers/Microsoft.Media/mediaServices/<accountName>/transforms/<transformName>/jobs/<jobName>
            var items = request.RequestUri.AbsolutePath.Split('/');
            var amsOperations = items.SkipWhile(i => !i.Equals("mediaServices", StringComparison.InvariantCultureIgnoreCase)).ToList();

            var accountName = string.Empty;
            var callInfo = string.Empty;

            if (amsOperations.Count > 1)
            {
                // account name always follows mediaServices string
                accountName = amsOperations.ElementAt(1);
            }

            if (amsOperations.Count > 2)
            {
                // everything else after account name
                callInfo = string.Join("/", amsOperations.Skip(2));
            }

            // Create model and initialize common field values
            var mediaServiceCallHistoryModel = new MediaServiceCallHistoryModel
            {
                Id = Guid.NewGuid().ToString(),
                MediaServiceAccountName = accountName,
                CallInfo = $"{request.Method} {callInfo}",
                EventTime = DateTime.UtcNow,
                HttpStatus = response.StatusCode
            };

            // In order to keep this operation idempotent, there is no need to fail even if recording call data fails. Otherwise when request is resubmitted it can result in data duplication.
            var retryCount = 3;
            var retryTimeOut = 1000;

            do
            {
                try
                {
                    // try to store data
                    await this.mediaServiceCallHistoryStorageService.CreateOrUpdateAsync(mediaServiceCallHistoryModel, this.logger).ConfigureAwait(false);

                    // no exception, break
                    break;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    this.logger.LogError($"CallHistoryHandler::SendAsync got exception calling mediaServiceCallHistoryStorageService.CreateOrUpdateAsync: retryCount={retryCount} message={e.Message} mediaServiceCallHistoryModel={LogHelper.FormatObjectForLog(mediaServiceCallHistoryModel)}");
                    retryCount--;
                    await Task.Delay(retryTimeOut).ConfigureAwait(false);
                }
            }
            while (retryCount > 0);

            return response;
        }
    }
}
