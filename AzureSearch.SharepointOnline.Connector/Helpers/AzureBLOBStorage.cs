//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

using Microsoft.Graph;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace AzureSearch.SharePointOnline.Connector.Helpers
{
    class AzureBLOBStorage
    {

        public static int DownloadFileCount;
        private static int _currentRetry = 0;
        private static int _retryCount = 5;
        private static TimeSpan _delay = TimeSpan.FromSeconds(15);
        private static int spoDownloadErrorCount = 0;
        private static int spoDownloadErrorRetryCount = 5;


        static async Task DownloadFileLocal(GraphServiceClient graphClient, object downloadUrl, string fileName)
        {
            // Create a file stream to contain the downloaded file.
            using (FileStream fileStream = System.IO.File.Create((@"C:\Temp\" + fileName)))
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, (string)downloadUrl);
                HttpResponseMessage response = await graphClient.HttpProvider.SendAsync(req);
                var responseStream = await response.Content.ReadAsStreamAsync();
                responseStream.CopyTo(fileStream);
                Console.WriteLine("file {0} written to BLOB", fileName);
                DownloadFileCount++;
            }

        }

        public static async Task DownloadFileToAzureBLOB(GraphServiceClient graphClient, object downloadUrl, string fileName, CloudBlobContainer container, string storageUploadUri)
        {
            var blockBlob = container.GetBlockBlobReference(fileName);
            await DownloadSPOFile(graphClient, downloadUrl, fileName, blockBlob, storageUploadUri);
        }

        static async Task DownloadSPOFile(GraphServiceClient graphClient, object downloadUrl, string fileName, CloudBlockBlob blockBlob, string storageUploadUri)
        {
            try
            {
                HttpRequestMessage req2 = new HttpRequestMessage(HttpMethod.Get, (string)downloadUrl);
                HttpResponseMessage response = await graphClient.HttpProvider.SendAsync(req2);
                if (response.IsSuccessStatusCode)
                {
                    using (var responseStream = await response.Content.ReadAsStreamAsync())
                    {
                        blockBlob.Metadata.Add("Metadataurl", storageUploadUri);

                        await blockBlob.UploadFromStreamAsync(responseStream);
                        //await blockBlob.SetMetadataAsync();
                        Console.WriteLine("file {0} written to Azure BLOB", fileName);
                        DownloadFileCount++;
                    }
                }
                else
                {
                    
                }
            }
            catch (Exception e)
            {
                spoDownloadErrorCount++;
                if (spoDownloadErrorCount <= spoDownloadErrorRetryCount)
                {
                    Console.WriteLine("Retry count [{0}] downloading file {1}", spoDownloadErrorCount, downloadUrl);
                    await DownloadSPOFile(graphClient, downloadUrl, fileName, blockBlob, storageUploadUri);
                }
            }
            spoDownloadErrorCount = 0;
        }

        static async Task DownloadFileToAzureBLOB(GraphServiceClient graphClient, object downloadUrl, string fileName, CloudBlobContainer container)
        {
            var blockBlob = container.GetBlockBlobReference(fileName);
            // Create a file stream to contain the downloaded file.

            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, (string)downloadUrl);
            HttpResponseMessage response = await graphClient.HttpProvider.SendAsync(req);
            var responseStream = await response.Content.ReadAsStreamAsync();

            await blockBlob.UploadFromStreamAsync(responseStream);
            //await blockBlob.SetMetadataAsync();
            Console.WriteLine("file {0} written to Azure BLOB", fileName);
            DownloadFileCount++;
        }

        public static async Task DownloadFileToAzureBLOB(GraphServiceClient graphClient, object downloadUrl, string fileName, CloudBlobContainer container, IDictionary<string, object> metadata)
        {
            var blockBlob = container.GetBlockBlobReference(fileName);
            // Create a file stream to contain the downloaded file.
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, (string)downloadUrl);
                HttpResponseMessage response = await graphClient.HttpProvider.SendAsync(req);
                var responseStream = await response.Content.ReadAsStreamAsync();

                foreach (var meta in metadata)
                {
                    var metaKey = meta.Key;
                    var metaValue = meta.Value.ToString();
                    blockBlob.Metadata.Add(metaKey, metaValue);
                }
                //Write Metadata tags:

                await blockBlob.UploadFromStreamAsync(responseStream);
                //await blockBlob.SetMetadataAsync();
                Console.WriteLine("file {0} written to Azure BLOB", fileName);
                DownloadFileCount++;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Downloading File: " + e.Message.ToString());
            }


        }

        public static async Task<String> UploadFileToAzureBLOB(Stream contents, string fileName, CloudBlobContainer container)
        {
            var blockBlob = container.GetBlockBlobReference(fileName);
            await blockBlob.UploadFromStreamAsync(contents);
            Console.WriteLine("file {0} written to Azure BLOB", fileName);
            return blockBlob.StorageUri.PrimaryUri.ToString();

        }

        public static async Task<String> DeleteFileFromAzureBLOB(string fileName, CloudBlobContainer container)
        {
            var blockBlob = container.GetBlockBlobReference(fileName);
            Console.WriteLine("Removing fileName [" + fileName + "]");
            await blockBlob.DeleteAsync();
            Console.WriteLine("file {0} deleted from Azure BLOB", fileName);
            return blockBlob.StorageUri.PrimaryUri.ToString();

        }

        public static async Task<CloudBlobContainer> CreateAzureBLOBContainer(CloudBlobClient storageClient, string containerName)
        {
            Console.WriteLine("Creating container [" + containerName + "]");
            var container = storageClient.GetContainerReference(containerName);
            try
            {
                await container.CreateIfNotExistsAsync();
            }
            catch (Exception err)
            {
                //Wait for the The specified container is being deleted. Try operation later. to clear
                if (err.HResult.Equals(-2146233088))
                {
                    Console.WriteLine("The specified container is being deleted. Will try again");
                    _currentRetry++;
                    await Task.Delay(_delay);
                    await CreateAzureBLOBContainer(storageClient, containerName);
                    if (_currentRetry > _retryCount)
                    {
                        // If this isn't a transient error or we shouldn't retry,
                        // rethrow the exception.
                        throw;
                    }

                }
            }

            CloudBlobContainer newContainer = storageClient.GetContainerReference(containerName);
            return newContainer;
        }

        public static async Task DeleteContainerFromAzureBLOB(CloudBlobContainer container)
        {
            await container.DeleteAsync();

            Console.WriteLine("Removing container [" + container + "]");
        }


    }
}
