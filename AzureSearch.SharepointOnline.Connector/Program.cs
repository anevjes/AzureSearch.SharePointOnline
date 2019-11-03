//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AzureSearch.SharePointConnector.Helpers;
using AzureSearch.SharePointOnline.Connector.Helpers;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.Extensions.Logging;

namespace AzureSearch.SharePointConnector
{
    class Program
    {
        private static GraphServiceClient _graphServiceClient;
        private static HttpClient _httpClient;
        public static SearchServiceHelper searchServiceHelper;
        public CloudBlobContainer container;

        private static bool IncrementalCrawl { get; set; }
        public static string BlobContainerName { get; set; }
        public static string StorageAccountName { get; set; }
        public static string StorageAccountKey { get; set; }
        public static string StorageConnectionString { get; set; }
        public static string StorageTableName { get; set; }
        public static string SpoItemStorageTableName { get; set; }
        public static string SPOHostName { get; set; }
        public static string SiteUrl { get; set; }
        public static string MetadataJSONStore { get; set; }
        public static bool IncludeAcls { get; set; }
        public static string[] MetadataFieldsToIgnore { get; set; }
        public static string[] DocLibsToIgnore { get; set; }
        public static string SearchServiceName { get; set; }
        public static string SearchServiceAdminKey { get; set; }
        public static string SearchServiceIndexName { get; set; }
        public static string SearchServiceBlobDataSourceName { get; set; }
        public static string SearchServiceBlobSynonymMapName { get; set; }
        public static string SearchServiceBlobSkillsetName { get; set; }
        public static string SearchServiceBlobIndexerName { get; set; }
        public static string CognitiveAccount { get; set; }
        public static string CognitiveKey { get; set; }
        public static string CustomSpoMetadataSkillUri { get; set; }
        public static string SPOMetadataMapperApiKey { get; set; }
        public static string AppInsightsApiKey { get; set; }

        public static string DefinitionsPath = "SearchDefinitions";

        //CloudStorageAccount storageAccount;



        static async Task Main(string[] args)
        {

            // Load appsettings.json
            var config = LoadAppSettings();
            if (null == config)
            {
                Console.WriteLine("Missing or invalid appsettings.json file. Please see README.md for configuration instructions.");
                return;
            }
            SetGlobalConfig(config);

            ////Logging
            //IServiceCollection services = new ServiceCollection();

            //// Channel is explicitly configured to do flush on it later.
            //var channel = new InMemoryChannel();
            //services.Configure<TelemetryConfiguration>(
            //    (config) =>
            //    {
            //        config.TelemetryChannel = channel;
            //    }
            //);

            //services.AddLogging(builder =>
            //{
            //    builder.AddConsole();
            //    builder.AddApplicationInsights(AppInsightsApiKey);
            //});

            //var provider = services.BuildServiceProvider();
            //var logger = provider.GetService<ILogger<Program>>();

            //logger.LogInformation("This will show up in Application Insights");

            // Explicitly call Flush() followed by sleep is required in Console Apps.
            // This is to ensure that even if application terminates, telemetry is sent to the back-end.
            //channel.Flush();



            searchServiceHelper = new SearchServiceHelper(SearchServiceName, SearchServiceAdminKey);

            System.Diagnostics.Trace.TraceWarning("Slow response - database01");

            TimeSpan elapsedTime;

            //Start stopwatch for timing telemtry
            Stopwatch sw = new Stopwatch();
            var timeStart = DateTime.Now;
            sw.Start();

            //Storage
            var storageAccount = CloudStorageAccount.Parse(StorageConnectionString);
            var storageClient = storageAccount.CreateCloudBlobClient();

            AzureTableStorage azTableStorage = new AzureTableStorage(StorageConnectionString, StorageTableName);
            AzureTableStorage azTableStorageSpoItems = new AzureTableStorage(StorageConnectionString, SpoItemStorageTableName);

            CloudBlobContainer container = await AzureBLOBStorage.CreateAzureBLOBContainer(storageClient, BlobContainerName);


            //Search
            AzureSearchServiceHelper searchClient = new AzureSearchServiceHelper(SearchServiceName, SearchServiceAdminKey);

            //Lookup itemId for the item that needs to be removed from the search index

            //Now delete the itemId from the index
            await searchClient.DeleteItemFromIndexAsync("aaaa", SearchServiceIndexName, "11");

            IDriveItemChildrenCollectionPage docLibItems;
            IDriveItemDeltaCollectionPage docLibDeltaItems;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "-incrementalcrawl")
                {
                    IncrementalCrawl = true;
                    Console.WriteLine("Search Crawl mode set to Incremental");
                    container = await AzureBLOBStorage.CreateAzureBLOBContainer(storageClient, BlobContainerName);

                }

                if (args[i].ToLower() == "-fullcrawl")
                {
                    IncrementalCrawl = false;
                    Console.WriteLine("Search Crawl mode set to Full");
                    await AzureBLOBStorage.DeleteContainerFromAzureBLOB(container);
                    container = await AzureBLOBStorage.CreateAzureBLOBContainer(storageClient, BlobContainerName);

                }

                if (args[i].ToLower() == "-includeAcls")
                {
                    IncludeAcls = true;
                    Console.WriteLine("Search Crawl mode set to Full");
                }
            }


            SharePointOnlineHelper.metadataFieldsToIgnore = MetadataFieldsToIgnore;
            SharePointOnlineHelper.metadataJSONStore = MetadataJSONStore;
            SharePointOnlineHelper.acls = IncludeAcls;
            SharePointOnlineHelper.azTableStorage = azTableStorageSpoItems;


            foreach (var metadataFieldToIgnore in MetadataFieldsToIgnore)
            {
                Console.WriteLine("Removing key [{0}] from metadata fields to extract", metadataFieldToIgnore);
            }

            //Query using Graph SDK (preferred when possible)
            GraphServiceClient graphClient = SharePointOnlineHelper.GetAuthenticatedGraphClient(config);
            Site targetSite = await graphClient.Sites.GetByPath(SiteUrl, SPOHostName).Request().GetAsync();

            ISiteDrivesCollectionPage drives = graphClient.Sites[targetSite.Id].Drives.Request().GetAsync().Result;


            //Graph BETA supports site pages
            //var sitePages = graphClient.Sites[targetSite.Id].Pages.Request().GetAsync().GetAwaiter().GetResult();
            //var sitePages = graphClient.Sites[targetSite.Id].Pages.Request().GetAsync().Result;
            //var a = 1;

            foreach (var drive in drives)
            {
                var driveName = drive.Name;
                var driveUrl = drive.WebUrl;
                bool excludedDocLIb = Array.Exists(DocLibsToIgnore, element => element == driveName);

                if (excludedDocLIb)
                {
                    Console.WriteLine("Skipping [{0}] as its an excluded docLib", DocLibsToIgnore);
                    continue;
                }
                Console.WriteLine("Fetching items from drive [{0}]", driveName);

                var driveId = drive.Id;
                var driveContents = new List<DriveItem>();

                //Full Crawl Logic
                if (!IncrementalCrawl)
                {
                    docLibItems = await graphClient
                   .Drives[driveId]
                   .Root
                   .Children
                   .Request()
                   .GetAsync();

                    driveContents.AddRange(docLibItems.CurrentPage);

                    if (docLibItems.NextPageRequest != null)
                    {
                        while (docLibItems.NextPageRequest != null)
                        {
                            docLibItems = await docLibItems.NextPageRequest.GetAsync();
                            driveContents.AddRange(docLibItems.CurrentPage);
                            await SharePointOnlineHelper.GetSpoDocumentItems(graphClient, driveContents, driveId, container, IncludeAcls);
                        }
                    }
                    else
                    {
                        await SharePointOnlineHelper.GetSpoDocumentItems(graphClient, driveContents, driveId, container, IncludeAcls);
                    }

                }

                //Incremental Crawl Logic
                if (IncrementalCrawl)
                {


                    //Retrieve the last known deltaToken from Table storage, if the value is null it will fetch all items for that drive
                    //Base64 encode the string to remove special characters
                    byte[] byt = System.Text.Encoding.UTF8.GetBytes(driveUrl);
                    var driveUrlEscpaed = Convert.ToBase64String(byt);

                    var lastDeltaToken = await azTableStorage.GetEntitiesInPartion(driveUrlEscpaed);
                    docLibDeltaItems = await graphClient
                    .Drives[driveId]
                    .Root
                    .Delta(lastDeltaToken)
                    .Request()
                    .GetAsync();

                    var deltaLink = docLibDeltaItems.AdditionalData["@odata.deltaLink"].ToString();
                    if (deltaLink != null)
                    {
                        var tokenindex = deltaLink.IndexOf("token=");

                        var token = deltaLink.Substring(tokenindex + 7, deltaLink.ToString().Length - tokenindex - 9);
                        driveContents.AddRange(docLibDeltaItems.CurrentPage);

                        if (docLibDeltaItems.NextPageRequest != null)
                        {
                            while (docLibDeltaItems.NextPageRequest != null)
                            {
                                var docLibItems2 = await docLibDeltaItems.NextPageRequest.GetAsync();
                                driveContents.AddRange(docLibItems2.CurrentPage);
                                await SharePointOnlineHelper.GetSpoDocumentItems(graphClient, driveContents, driveId, container, IncludeAcls);
                            }
                        }
                        else
                        {
                            await SharePointOnlineHelper.GetSpoDocumentItems(graphClient, driveContents, driveId, container, IncludeAcls);

                            //Lets persist the changeToken to storage so we can continue the next incrmental crawl from this point.
                            IndexCrawlEntity indexCrawlEntity = new IndexCrawlEntity(driveUrlEscpaed, token);
                            azTableStorage.InsertEntity(indexCrawlEntity);
                        }
                        //Console.WriteLine("Fetched total of {0} documents from [{1}] data source", DownloadFileCount, driveName);
                    }
                }

            }

            if (!IncrementalCrawl)
            {
                //Now lets do a  full crawl of all the fetched SPO documents from the BLOB store as the fetching of all documents into storage would have completed by now
                //Warning this will perform an entire search index rebuild - so while this phase is running search resultset will be impacted

                await IndexDocumentsAsync();
            }

            sw.Stop();
            elapsedTime = sw.Elapsed;
            var timeEnd = DateTime.Now;

            Console.WriteLine("Fetched total of {0} documents during crawl", AzureBLOBStorage.DownloadFileCount);
            Console.WriteLine("Crawl Start time: {0}", timeStart);
            Console.WriteLine("Crawl Completed time: {0}", timeEnd);
            Console.WriteLine("Total crawl duration time: {0}", elapsedTime);
        }


        private static void SetGlobalConfig(IConfigurationRoot config)
        {
            StorageAccountName = config["ConnectionStrings:StorageDetails:storageAccountName"];
            StorageAccountKey = config["ConnectionStrings:StorageDetails:storageAccountKey"];
            BlobContainerName = config["ConnectionStrings:StorageDetails:storageBlobContainerName"];
            StorageConnectionString = ($"DefaultEndpointsProtocol=https;AccountName={StorageAccountName};AccountKey={StorageAccountKey};");

            SPOHostName = config["ConnectionStrings:SPODetails:SPOHostName"];
            SiteUrl = config["ConnectionStrings:SPODetails:siteUrl"];
            MetadataFieldsToIgnore = config.GetSection("ConnectionStrings:SPODetails:metadataFieldsToIgnore").GetChildren().ToArray().Select(c => c.Value).ToArray();
            DocLibsToIgnore = config.GetSection("ConnectionStrings:SPODetails:docLibExclusions").GetChildren().ToArray().Select(c => c.Value).ToArray();
            MetadataJSONStore = config["ConnectionStrings:SPODetails:metadataJSONStore"];

            StorageTableName = config["ConnectionStrings:StorageDetails:storageTableName"];
            SpoItemStorageTableName = config["ConnectionStrings:StorageDetails:spoItemStorageTableName"];

            SearchServiceName = config["ConnectionStrings:SearchDetails:name"];
            SearchServiceAdminKey = config["ConnectionStrings:SearchDetails:adminKey"];
            SearchServiceIndexName = config["ConnectionStrings:SearchDetails:indexName"];
            SearchServiceBlobDataSourceName = config["ConnectionStrings:SearchDetails:blobDataSourceName"];
            SearchServiceBlobSynonymMapName = config["ConnectionStrings:SearchDetails:blobSynonymMapName"];
            SearchServiceBlobSkillsetName = config["ConnectionStrings:SearchDetails:blobSkillsetName"];
            SearchServiceBlobIndexerName = config["ConnectionStrings:SearchDetails:blobIndexerName"];
            CognitiveAccount = config["ConnectionStrings:SearchDetails:cognitiveAccount"];
            CognitiveKey = config["ConnectionStrings:SearchDetails:cognitiveKey"];
            CustomSpoMetadataSkillUri = config["ConnectionStrings:SearchDetails:customSpoMetadataSkillUri"];
            SPOMetadataMapperApiKey = config["ConnectionStrings:SearchDetails:SPOMetadataMapper-Api-Key"];
            AppInsightsApiKey = config["Logging:key"];

        }
        private static IAuthenticationProvider CreateAuthorizationProvider(IConfigurationRoot config)
        {
            var clientId = config["ConnectionStrings: AADDetails:applicationId"];
            var clientSecret = config["ConnectionStrings:AADDetails:applicationSecret"];
            var redirectUri = config["ConnectionStrings:AADDetails:redirectUri"];
            var authority = $"https://login.microsoftonline.com/{config["ConnectionStrings:AADDetails:tenantId"]}/v2.0";

            //this specific scope means that application will default to what is defined in the application registration rather than using dynamic scopes
            List<string> scopes = new List<string>();
            scopes.Add("https://graph.microsoft.com/.default");

            var cca = ConfidentialClientApplicationBuilder.Create(clientId)
                                                    .WithAuthority(authority)
                                                    .WithRedirectUri(redirectUri)
                                                    .WithClientSecret(clientSecret)
                                                    .Build();
            return new MsalAuthenticationProvider(cca, scopes.ToArray());
        }

        private static IConfigurationRoot LoadAppSettings()
        {
            try
            {
                var settingsFileName = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "appSettings.json");

                var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
               .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
               .Build();

                //config.AddEnvironmentVariables("");

                // Validate required settings
                if (string.IsNullOrEmpty(config["ConnectionStrings:AADDetails:applicationId"]) ||
                    string.IsNullOrEmpty(config["ConnectionStrings:AADDetails:applicationSecret"]) ||
                    string.IsNullOrEmpty(config["ConnectionStrings:AADDetails:redirectUri"]) ||
                    string.IsNullOrEmpty(config["ConnectionStrings:AADDetails:tenantId"]) ||
                    string.IsNullOrEmpty(config["ConnectionStrings:AADDetails:domain"]) ||
                    string.IsNullOrEmpty(config["ConnectionStrings:StorageDetails:storageBlobContainerName"]) ||
                    string.IsNullOrEmpty(config["ConnectionStrings:SPODetails:SPOHostName"]) ||
                    string.IsNullOrEmpty(config["ConnectionStrings:SPODetails:siteUrl"]) ||
                    string.IsNullOrEmpty(config["ConnectionStrings:SPODetails:metadataJSONStore"]))
                {
                    return null;
                }

                return config;
            }
            catch (System.IO.FileNotFoundException)
            {
                return null;
            }
        }

        private static async Task IndexDocumentsAsync()
        {
            //var definitionsPath = "definitions";
            var synonymMapDefinitionPath = Path.Combine(DefinitionsPath, "blobSynonymMap.json");
            var indexDefinitionPath = Path.Combine(DefinitionsPath, "blobIndex.json");
            var skillsetDefinitionPath = Path.Combine(DefinitionsPath, "blobSkillset.json");
            var indexerDefinitionPath = Path.Combine(DefinitionsPath, "blobIndexer.json");

            await searchServiceHelper.CreateOrUpdateBlobDataSourceAsync(SearchServiceBlobDataSourceName, StorageAccountName, StorageAccountKey, BlobContainerName);

            await searchServiceHelper.DeleteSynonymMapAsync(SearchServiceBlobSynonymMapName);
            await searchServiceHelper.CreateSynonymsMapFromJsonDefinitionAsync(SearchServiceBlobSynonymMapName, synonymMapDefinitionPath);

            await searchServiceHelper.DeleteIndexAsync(SearchServiceIndexName);
            await searchServiceHelper.CreateIndexFromJsonDefinitionAsync(SearchServiceIndexName, indexDefinitionPath, SearchServiceBlobSynonymMapName);

            await searchServiceHelper.DeleteSkillsetAsync(SearchServiceBlobSkillsetName);
            await searchServiceHelper.CreateSkillsetFromJsonDefinitionAsync(SearchServiceBlobSkillsetName, skillsetDefinitionPath, CognitiveKey, CognitiveAccount, CustomSpoMetadataSkillUri, SPOMetadataMapperApiKey);

            await searchServiceHelper.DeleteIndexerAsync(SearchServiceBlobIndexerName);
            await searchServiceHelper.CreateIndexerFromJsonDefinitionAsync(SearchServiceBlobIndexerName, indexerDefinitionPath, SearchServiceBlobDataSourceName, SearchServiceIndexName, SearchServiceBlobSkillsetName);

            await searchServiceHelper.WaitForIndexerToFinishAsync(SearchServiceBlobIndexerName);

        }
    }
}