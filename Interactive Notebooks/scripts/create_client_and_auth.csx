using Microsoft.Azure.Management.Media;
using Microsoft.Azure.Management.Media.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Microsoft.Rest;
using Common_Utils;
using dotenv.net;

public static async Task<IAzureMediaServicesClient> CreateMediaServicesClient()
{
    dotenv.net.DotEnv.Load();
    var envVars = dotenv.net.DotEnv.Read();
    var AADCLIENTID = envVars["AADCLIENTID"];
    var AADSECRET = envVars["AADSECRET"];
    var AADTENANTDOMAIN = envVars["AADTENANTDOMAIN"];
    var AADTENANTID = envVars["AADTENANTID"];
    var ACCOUNTNAME = envVars["ACCOUNTNAME"];
    var RESOURCEGROUP = envVars["RESOURCEGROUP"];
    var SUBSCRIPTIONID = envVars["SUBSCRIPTIONID"];
    var ARMAADAUDIENCE = envVars["ARMAADAUDIENCE"];
    var ARMENDPOINT = envVars["ARMENDPOINT"];
    var AADENDPOINT = envVars["AADENDPOINT"];
    var STORAGECONNECTIONSTRING = envVars["STORAGECONNECTIONSTRING"];

    string TokenType = "Bearer";
    var scopes = new[] { ARMAADAUDIENCE + ".default" };

    var app = ConfidentialClientApplicationBuilder.Create(AADCLIENTID)
        .WithClientSecret(AADSECRET)
        .WithAuthority(AzureCloudInstance.AzurePublic, AADTENANTID)
        .Build();

    var authResult = await app.AcquireTokenForClient(scopes)
                                            .ExecuteAsync()
                                            .ConfigureAwait(false);

    var credentials = new TokenCredentials(authResult.AccessToken, TokenType);

    IAzureMediaServicesClient client = null;

    try
    {
        client = new AzureMediaServicesClient(new Uri(ARMENDPOINT), credentials)
        {
            SubscriptionId = SUBSCRIPTIONID
        };
        // Set the polling interval for long running operations to 2 seconds.
        // The default value is 30 seconds for the .NET client SDK
        client.LongRunningOperationRetryTimeout = 2;

    }
    catch (Exception e)
    {
        Console.WriteLine("TIP: Make sure that you have filled out an .env file before running this sample.");
        Console.WriteLine($"{e.Message}");
    }


    return client;
}