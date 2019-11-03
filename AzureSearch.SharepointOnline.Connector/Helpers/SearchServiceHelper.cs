//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AzureSearch.SharePointOnline.Connector.Helpers
{
    public class SearchServiceHelper
    {
        private readonly SearchServiceClient client;

        public SearchServiceHelper(string searchServiceName, string searchServiceAdminKey)
        {
            client = new SearchServiceClient(searchServiceName, new SearchCredentials(searchServiceAdminKey));
            client.HttpClient.DefaultRequestHeaders.Add("api-key", searchServiceAdminKey);
        }

        public async Task CreateOrUpdateBlobDataSourceAsync(
            string dataSourceName,
            string storageAccountName,
            string storageAccountKey,
            string storageContainerName)
        {
            Console.WriteLine($"Creating '{dataSourceName}' blob data source...");
            await client.DataSources.CreateOrUpdateAsync(new DataSource()
            {
                Name = dataSourceName,
                Type = "azureblob",
                Credentials = new DataSourceCredentials($"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};"),
                Container = new DataContainer(storageContainerName) // In query param you can specify an optional virtual directory name
            });
        }

        public async Task CreateOrUpdateCosmosDBDataSourceAsync(
            string dataSourceName,
            string cosmosDBConnectionString,
            string cosmosDbDatabaseName,
            string cosmosDBContainer)
        {
            Console.WriteLine($"Creating '{dataSourceName}' CosmosDB data source...");
            await client.DataSources.CreateOrUpdateAsync(DataSource.DocumentDb(
                name: dataSourceName,
                documentDbConnectionString: $"{cosmosDBConnectionString};Database={cosmosDbDatabaseName}",
                collectionName: cosmosDBContainer,
                useChangeDetection: true
            ));
        }

        public async Task DeleteDataSourceAsync(string dataSourceName)
        {
            Console.WriteLine($"Deleting '{dataSourceName}' data source...");
            await client.DataSources.DeleteAsync(dataSourceName);
        }

        public async Task CreateSynonymsMapFromJsonDefinitionAsync(string synonymMapName, string synonymMapDefinitionPath)
        {
            Console.WriteLine($"Creating '{synonymMapName}' synonym map with '{synonymMapDefinitionPath}'...");
            using (StreamReader reader = new StreamReader(synonymMapDefinitionPath))
            {
                var uri = $"https://{client.SearchServiceName}.{client.SearchDnsSuffix}/synonymmaps/{synonymMapName}?api-version=2017-11-11-Preview";
                var json = reader.ReadToEnd();
                json = json.Replace("[SynonymMapName]", synonymMapName);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.HttpClient.PutAsync(uri, content);
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task DeleteSynonymMapAsync(string synonymMapName)
        {
            Console.WriteLine($"Deleting '{synonymMapName}' synonym map...");
            await client.SynonymMaps.DeleteAsync(synonymMapName);
        }

        public async Task CreateIndexFromJsonDefinitionAsync(string indexName, string indexDefinitionPath, string synonymMapName)
        {
            Console.WriteLine($"Creating '{indexName}' index with '{indexDefinitionPath}'...");
            using (StreamReader reader = new StreamReader(indexDefinitionPath))
            {
                var uri = $"https://{client.SearchServiceName}.{client.SearchDnsSuffix}/indexes/{indexName}?api-version=2017-11-11-Preview";
                var json = reader.ReadToEnd();
                json = json.Replace("[SynonymMapName]", synonymMapName);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                try
                {
                    var response = await client.HttpClient.PutAsync(uri, content);

                }
                catch (Exception ex)
                //when (ex.Message.Contains("404 (Not Found)"))
                {
                    var _ = ex.Message;
                }

            }
        }

        public async Task DeleteIndexAsync(string indexName)
        {
            Console.WriteLine($"Deleting '{indexName}' index...");
            await client.Indexes.DeleteAsync(indexName);
        }

        public async Task CreateSkillsetFromJsonDefinitionAsync(string skillsetName, string skillsetDefinitionPath, string cognitiveKey, string cognitiveAccount, string customSpoMetadataSkillUri, string spoMetadataMapperApiKey)
        {
            Console.WriteLine($"Creating '{skillsetName}' skillset with '{skillsetDefinitionPath}'...");
            using (StreamReader reader = new StreamReader(skillsetDefinitionPath))
            {
                var uri = $"https://{client.SearchServiceName}.{client.SearchDnsSuffix}/skillsets/{skillsetName}?api-version=2017-11-11-Preview";
                var json = reader.ReadToEnd();
                json = json.Replace("[CognitiveServicesAccount]", cognitiveAccount);
                json = json.Replace("[CognitiveServicesKey]", cognitiveKey);
                json = json.Replace("[CustomSpoMetadataSkillUri]", customSpoMetadataSkillUri);
                json = json.Replace("[SPOMetadataMapper-Api-Key]", spoMetadataMapperApiKey);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.HttpClient.PutAsync(uri, content);
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task DeleteSkillsetAsync(string skillsetName)
        {
            try
            {
                Console.WriteLine($"Deleting '{skillsetName}' skillset...");
                var uri = $"https://{client.SearchServiceName}.{client.SearchDnsSuffix}/skillsets/{skillsetName}?api-version=2017-11-11-Preview";
                var response = await client.HttpClient.DeleteAsync(uri);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex) when (ex.Message.Contains("404 (Not Found)")) { }
        }

        public async Task CreateIndexerFromJsonDefinitionAsync(string indexerName, string indexerDefinitionPath, string dataSourceName, string indexName, string skillsetName)
        {
            Console.WriteLine($"Creating '{indexerName}' indexer with '{indexerDefinitionPath}'...");
            using (StreamReader reader = new StreamReader(indexerDefinitionPath))
            {
                var uri = $"https://{client.SearchServiceName}.{client.SearchDnsSuffix}/indexers/{indexerName}?api-version=2017-11-11-Preview";
                var json = reader.ReadToEnd();
                json = json.Replace("[IndexerName]", indexerName);
                json = json.Replace("[DataSourceName]", dataSourceName);
                json = json.Replace("[IndexName]", indexName);
                json = json.Replace("[SkillSetName]", skillsetName);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.HttpClient.PutAsync(uri, content);
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task CreateIndexerAsync(string indexerName, string dataSourceName, string indexName)
        {
            Console.WriteLine($"Creating '{indexerName}' indexer...");
            await client.Indexers.CreateAsync(new Indexer(
                name: indexerName,
                dataSourceName: dataSourceName,
                targetIndexName: indexName
            ));
        }

        public async Task DeleteIndexerAsync(string indexerName)
        {
            Console.WriteLine($"Deleting '{indexerName}' indexer...");
            await client.Indexers.DeleteAsync(indexerName);
        }

        public async Task WaitForIndexerToFinishAsync(string indexerName, int delaySecs = 60)
        {
            IndexerExecutionInfo info;

            do
            {
                Console.WriteLine($"   Waiting {delaySecs} seconds...");
                await Task.Delay(delaySecs * 1000);
                Console.WriteLine($"   Getting indexer status...");
                info = await client.Indexers.GetStatusAsync(indexerName);
                Console.WriteLine($"   ...Indexer status: {info.Status}, Indexer Execution Status: {info.LastResult?.Status}.");
            } while (
                info.Status == IndexerStatus.Running
                && (info.LastResult == null || info.LastResult.Status == IndexerExecutionStatus.InProgress));

            if (info.Status == IndexerStatus.Running && info.LastResult?.Status == IndexerExecutionStatus.Success)
            {
                Console.WriteLine($"...Indexer '{indexerName}' created successfully.");
            }
            else
            {
                Console.WriteLine($"...Failed to create '{indexerName}' indexer.");
                Console.WriteLine($"   Error: '{info.LastResult.ErrorMessage}'");
            }

            foreach (var warning in info.LastResult?.Warnings)
            {
                Console.WriteLine("===========================================================================");
                Console.WriteLine($"   Warning for '{warning.Key}': '{warning.Message}'");
            }

            foreach (var error in info.LastResult?.Errors)
            {
                Console.WriteLine("===========================================================================");
                Console.WriteLine($"   Error for '{error.Key}': '{error.ErrorMessage}'");
            }
            Console.WriteLine("===========================================================================");
        }
    }
}
