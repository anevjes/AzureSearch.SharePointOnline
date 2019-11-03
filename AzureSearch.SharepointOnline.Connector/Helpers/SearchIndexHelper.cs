//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using System;
using System.Threading.Tasks;

namespace AzureSearch.SharePointOnline.Connector.Helpers
{
    public class SearchIndexHelper
    {
        private readonly SearchIndexClient client;

        public SearchIndexHelper(string searchServiceName, string searchServiceQueryKey, string indexName)
        {
            client = new SearchIndexClient(searchServiceName, indexName, new SearchCredentials(searchServiceQueryKey));
        }

        public async Task SearchIndexAsync(string[] select)
        {
            Console.WriteLine("Querying the index...");
            var results = await client.Documents.SearchAsync(
                searchText: "*",
                searchParameters: new SearchParameters() { Select = select }
            );

            Console.WriteLine("Results:");
            foreach (var result in results.Results)
            {
                Console.WriteLine("===========================================================================");
                foreach (string key in result.Document.Keys)
                {
                    if (result.Document[key] is string text)
                    {
                        text = text.Replace("\n", "");
                        text = text.Length > 200 ? $"{text.Substring(0, 200)}..." : text;
                        Console.WriteLine($"   {key}: '{text}'");
                    }
                    else if (result.Document[key] is string[] texts)
                    {
                        Console.Write($"   {key}: ");
                        foreach (var t in texts)
                        {
                            Console.Write($"'{t}' ");
                        }
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine($"   {key}: {result.Document[key]}");
                    }
                }

            }
            Console.WriteLine("===========================================================================\n");
        }
    }
}
