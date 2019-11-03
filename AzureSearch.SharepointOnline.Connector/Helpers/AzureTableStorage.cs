//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.


using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Threading.Tasks;
using WindowsAzure.ChronoTableStorage;

namespace AzureSearch.SharePointOnline.Connector.Helpers
{
    public class IndexCrawlEntity : TableEntity
    {
        public string DeltaToken { get; set; }
        public string UtcTime { get; set; }
        public IndexCrawlEntity() { }
        public IndexCrawlEntity(string documentLibraryUrl, string deltaToken)
        {
            DateTime timeNow = DateTime.UtcNow;
            this.PartitionKey = documentLibraryUrl;
            this.DeltaToken = deltaToken;
            this.RowKey = WindowsAzure.ChronoTableStorage.RowKey.CreateChronological(timeNow);
            this.UtcTime = timeNow.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }


    public class SpoItem : TableEntity
    {
        public string DocumentLibraryUrl { get; set; }
        public string UtcTime { get; set; }
        public SpoItem() { }
        public SpoItem(string itemId, string documentLibraryUrl)
        {
            DateTime timeNow = DateTime.UtcNow;
            this.PartitionKey = itemId;
            this.DocumentLibraryUrl = documentLibraryUrl;
            this.RowKey = WindowsAzure.ChronoTableStorage.RowKey.CreateChronological(timeNow);
            var base64encodedUrl = Base64Encode(documentLibraryUrl);
            this.RowKey = base64encodedUrl;
            this.UtcTime = timeNow.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }

    class AzureTableStorage
    {
        public static string TableStorageConnectionString { get; set; }
        private CloudTableClient TableClient { get; set; }
        private CloudStorageAccount StorageAccount { get; set; }
        private CloudTable AzureSearchTable { get; set; }
        public string[] DeltaTokens { get; set; }

        public AzureTableStorage(string tableStorageConnectionString, string tableName)
        {
            TableStorageConnectionString = tableStorageConnectionString;
            StorageAccount = CloudStorageAccount.Parse(TableStorageConnectionString);
            TableClient = StorageAccount.CreateCloudTableClient();
            AzureSearchTable = TableClient.GetTableReference(tableName);
        }

        async public Task<string> GetEntitiesInPartion(string documentLibraryUrl)
        {
            // Construct the query operation for all IndexCrawlEntities where PartitionKey="documentLibraryUrl"
            TableQuery<IndexCrawlEntity> query = new TableQuery<IndexCrawlEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, documentLibraryUrl)).Take(1);
            string deltaTokenValue = "";
            TableContinuationToken token = null;
            do
            {
                TableQuerySegment<IndexCrawlEntity> resultSegment = await AzureSearchTable.ExecuteQuerySegmentedAsync(query, token);

                //ExecuteQuery
                token = resultSegment.ContinuationToken;

                //foreach (IndexCrawlEntity entity in resultSegment.Results)
                //{
                //    deltaTokenValues.Add(entity.RowKey);
                //}

                var deltaTokenResults = resultSegment.Results;

                if (deltaTokenResults.Count > 0)
                {
                    deltaTokenValue = deltaTokenResults[0].DeltaToken;
                }

            } while (token != null);
            return deltaTokenValue;
        }

        async public Task<string> GetSpoItemEntitiesInPartion(string itemId)
        {
            // Construct the query operation for all IndexCrawlEntities where PartitionKey="documentLibraryUrl"
            TableQuery<SpoItem> query = new TableQuery<SpoItem>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, itemId)).Take(1);
            string spWebUrl = "";
            TableContinuationToken token = null;
            do
            {
                TableQuerySegment<SpoItem> resultSegment = await AzureSearchTable.ExecuteQuerySegmentedAsync(query, token);

                //ExecuteQuery
                token = resultSegment.ContinuationToken;

                //foreach (IndexCrawlEntity entity in resultSegment.Results)
                //{
                //    deltaTokenValues.Add(entity.RowKey);
                //}

                var spoItemResults = resultSegment.Results;

                if (spoItemResults.Count > 0)
                {
                    spWebUrl = spoItemResults[0].DocumentLibraryUrl;
                }

            } while (token != null);
            return spWebUrl;
        }


        async public void InsertEntity(IndexCrawlEntity searchInfoEntity)
        {
            TableOperation insertOperation = TableOperation.InsertOrReplace(searchInfoEntity);
            await AzureSearchTable.ExecuteAsync(insertOperation);
        }

        async public void InsertSpoItemEntity(SpoItem spoItemEntity)
        {
            TableOperation insertOperation = TableOperation.InsertOrReplace(spoItemEntity);
            await AzureSearchTable.ExecuteAsync(insertOperation);
        }

    }
}
