//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

// C# Tutorial: Combine data from multiple data sources in one Azure Search index
// https://docs.microsoft.com/en-us/azure/search/tutorial-multiple-data-sources

namespace AzureSearch.SharePointOnline.Connector.Helpers
{
    public class AzureSearchServiceHelper
    {
        private readonly SearchServiceClient client;

        public AzureSearchServiceHelper(string searchServiceName, string searchServiceAdminKey)
        {
            client = new SearchServiceClient(searchServiceName, new SearchCredentials(searchServiceAdminKey));
            client.HttpClient.DefaultRequestHeaders.Add("api-key", searchServiceAdminKey);
        }

        public async Task DeleteItemFromIndexAsync(string itemName, string indexName,string itemId)
        {
            Console.WriteLine($"Deleting '{itemName}' item from index source...");
            var index = await client.Indexes.GetAsync("demo-index");

            //POST / indexes /[index name] / docs / index ? api - version =[api - version]
            var uri = $"https://{client.SearchServiceName}.{client.SearchDnsSuffix}/indexes/{indexName}?api-version=2017-11-11-Preview";

            var json = @"
                {
              'value': [
                {  
                  '@search.action': 'delete',
                  'id': 'replaceme'
                },  
              ]  
            }";

            json = json.Replace("replaceme", itemId);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.HttpClient.PostAsync(uri, content);


            var b = index;
        }

    }
}
