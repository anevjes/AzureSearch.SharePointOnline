//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using AzureSearch.SharePointOnline.Connector.Helpers;

namespace AzureSearch.SharePointConnector.Helpers
{
    class SharePointOnlineHelper
    {

        private static GraphServiceClient _graphServiceClient;
        private static String _clientSecret;
        private static String _clientId;
        private static String _tenantId;
        private static String _redirectUrl;
        private static String _authority;
        private static String _spoHostName;
        private static bool _getAcls;



        private static HttpClient _httpClient;
        public static string metadataJSONStore { get; set; }
        public static string[] metadataFieldsToIgnore { get; set; }
        public static bool acls { get; set; }
        public static AzureTableStorage azTableStorage { get; set; }

        private static IAuthenticationProvider CreateAuthorizationProvider(IConfigurationRoot config)
        {
            var clientId = config["ConnectionStrings:AADDetails:applicationId"];
            var clientSecret = config["ConnectionStrings:AADDetails:applicationSecret"];
            var tenantId = config["ConnectionStrings:AADDetails:tenantId"];
            var redirectUri = config["ConnectionStrings:AADDetails:redirectUri"];
            var authority = $"https://login.microsoftonline.com/{config["ConnectionStrings:AADDetails:tenantId"]}/v2.0";
            var spoHostName = config["ConnectionStrings:SPODetails:spoHostName"];

            _spoHostName = spoHostName;
            _clientSecret = clientSecret;
            _clientId = clientId;
            _tenantId = tenantId;
            _redirectUrl = redirectUri;
            _authority = authority;

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


        static Stream GenerateJsonMetadataFile(IDictionary<string, object> metadata)
        {
            string JSONresult = JsonConvert.SerializeObject(metadata);

            byte[] byteArray = Encoding.ASCII.GetBytes(JSONresult);
            MemoryStream jsonStream = new MemoryStream(byteArray);

            // convert stream to string
            StreamReader reader = new StreamReader(jsonStream);

            return jsonStream;
        }

        public static GraphServiceClient GetAuthenticatedGraphClient(IConfigurationRoot config)
        {
            var authenticationProvider = CreateAuthorizationProvider(config);

            _graphServiceClient = new GraphServiceClient(authenticationProvider);
            return _graphServiceClient;
        }

        public static async Task<List<DriveItem>> GetFolderContents(GraphServiceClient graphClient, string folderName, string driveId)

        {
            IDriveItemChildrenCollectionPage docFolderLibItems = null;
            var folderContents = new List<DriveItem>();

            try
            {
                docFolderLibItems = await graphClient
                .Drives[driveId]
                .Root
                .ItemWithPath(folderName)
                .Children
                .Request()
                .GetAsync();

                folderContents.AddRange(docFolderLibItems);

                while (docFolderLibItems.NextPageRequest != null)
                {
                    docFolderLibItems = await docFolderLibItems.NextPageRequest.GetAsync();
                    folderContents.AddRange(docFolderLibItems);
                }

            }
            catch (Exception e)
            {
                docFolderLibItems = null;
            }

            //return docFolderLibItems;
            return folderContents;
        }

        public static async Task GetSpoDocumentItems(GraphServiceClient graphClient, List<DriveItem> docLibItems, string driveId, CloudBlobContainer container, bool getAcls)
        {
            foreach (var item in docLibItems)
            {
                if (item.Folder != null)
                {
                    string ParentFolderPathString = null;
                    string fullFolderNamePath = null;
                    var folderName = item.Name;

                    if (item.ParentReference.Path != null)
                    {
                        var ParentFolderPathSplit = item.ParentReference.Path.Split(":");
                        if (ParentFolderPathSplit.Length >= 1)
                        {
                            ParentFolderPathString = ParentFolderPathSplit[1];
                            if (ParentFolderPathString.Length >= 1)
                            {
                                fullFolderNamePath = String.Format("{0}/{1}", ParentFolderPathString, folderName);
                            }
                            else
                            {
                                fullFolderNamePath = folderName;
                            }
                        }
                    }
                    else
                    {
                        fullFolderNamePath = folderName;
                    }


                    var folderItems = await GetFolderContents(graphClient, fullFolderNamePath, driveId);
                    if (folderItems.Count >0 )
                    {
                        await GetSpoDocumentItems(graphClient, folderItems, driveId, container, _getAcls);
                    }
                }
                // Let's download the first file we get in the response.
                if (item.File != null)
                {
                    // We'll use the file metadata to determine size and the name of the downloaded file
                    // and to get the download URL.
                    if (item.Deleted != null)
                    {
                        if (item.Deleted.State == "deleted")
                        {
                            Console.WriteLine("Deleted Item detected");
                           
                            var spoItemUrl = await azTableStorage.GetSpoItemEntitiesInPartion(item.Id);

                            //Clean up the Storage account path for the deleted item so we dont index it again
                            await AzureBLOBStorage.DeleteFileFromAzureBLOB(spoItemUrl, container);
                            //Clean up the json metadata file for the above file:
                            string spoItemUrlJson = ($"{spoItemUrl}.json");
                            await AzureBLOBStorage.DeleteFileFromAzureBLOB(spoItemUrlJson, container);

                            break;
                        }
                    }
 
                    var driveItemInfo = await graphClient.Drives[driveId].Items[item.Id].Request().GetAsync();

                    var SPWebUrl = driveItemInfo.WebUrl;
                    var createdAuthorDisplayName = driveItemInfo.CreatedBy.User.DisplayName;
                    var baseFileName = SPWebUrl;
                    var jsonMetadataFileName = String.Format("{0}.json", baseFileName);

                    //Below is for ACL Security trimming extraction which is still work in progress.
                    if (getAcls)
                    {
                        var driveItemPermissions = await graphClient.Drives[driveId].Items[item.Id].Permissions.Request().GetAsync();

                        foreach (var driveItemPermission in driveItemPermissions)
                        {

                            var grantedDispayName = driveItemPermission.GrantedTo.User.DisplayName;
                            var grantedObjectId = driveItemPermission.GrantedTo.User.Id;

                            //If no ID is present then its a sharepoint group
                            if (grantedObjectId == null)
                            {
                                var scopes = new[] { _spoHostName + "/.default" };
                                //var scopes = new[] { _spoHostName + "/Sites.FullControl.All" };

                                //var scopes = new[] { "https://graph.microsoft.com/contacts.read" };

                                var v1Authority = _authority.Replace("/v2.0", "");

                                var clientApplication = ConfidentialClientApplicationBuilder.Create(_clientId)
                                .WithAuthority(_authority)
                                .WithClientSecret(_clientSecret)
                                .WithClientId(_clientId)
                                .WithTenantId(_tenantId)
                                .Build();

                                var result = await clientApplication.AcquireTokenForClient(scopes).ExecuteAsync();

                                HttpClient client = new HttpClient();
                                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + result.AccessToken);
                                client.DefaultRequestHeaders.Add("Accept", "application/json");

                                ////setup the client get
                                HttpResponseMessage result2 = await client.GetAsync(String.Format("{0}/_api/Web/SiteGroups/GetByName('{1}')/users", SPWebUrl, grantedDispayName));

                                string filter = string.Format("startswith(displayName, {0}", grantedDispayName);
                                //string filter = string.Format("displayName startswith '{0}'", grantedDispayName);
                                var groupLookup = await graphClient.Groups
                                .Request()
                                .Filter($"startswith(displayName, '{grantedDispayName}')")
                                //.Filter(filter)
                                .Select("id, displayName").GetAsync();

                                var ac = groupLookup;
                            }
                        }
                    }
                    var fields = await graphClient.Drives[driveId].Items[item.Id].ListItem.Fields.Request().GetAsync();

                    //generate metadata content and upload to blob
                    var metadataFields = fields.AdditionalData;

                    foreach (var metadataFieldToIgnore in metadataFieldsToIgnore)
                    {
                        //Console.WriteLine("Removing key [{0}] from metadata fields to extract", metadataFieldToIgnore);
                        try
                        {
                            metadataFields.Remove(metadataFieldToIgnore);
                        }
                        catch
                        {
                            //swallow exceptions - where fields we want to remove may not exist / theres a better way to do this altogether.
                        }
                    }
                    metadataFields.Add("SPWebUrl", SPWebUrl);
                    metadataFields.Add("createdAuthorDisplayName", createdAuthorDisplayName);


                    // Get the download URL. This URL is preauthenticated and has a short TTL.
                    object downloadUrl;
                    driveItemInfo.AdditionalData.TryGetValue("@microsoft.graph.downloadUrl", out downloadUrl);
                    long size = (long)driveItemInfo.Size;

                    Console.WriteLine("located file {0}, full url [{1}]", baseFileName, downloadUrl.ToString());
                    //await DownloadFileLocal(graphClient, downloadUrl, fileName);
                    if (metadataJSONStore.Equals("True"))
                    {
                        //Metadata JSON logic
                        using (var metadataJson = GenerateJsonMetadataFile(metadataFields))
                        {
                            var uploadUri = await AzureBLOBStorage.UploadFileToAzureBLOB(metadataJson, jsonMetadataFileName, container);
                            //External JSON file approach
                            await AzureBLOBStorage.DownloadFileToAzureBLOB(graphClient, downloadUrl, baseFileName, container, uploadUri);
                        }

                    }
                    else
                    {
                        //BLOB metadata approach
                        await AzureBLOBStorage.DownloadFileToAzureBLOB(graphClient, downloadUrl, baseFileName, container, metadataFields);
                    }

                    //Persist the itemId and url to Storage Table
                    SpoItem spoItemEntity = new SpoItem(item.Id, SPWebUrl);
                    azTableStorage.InsertSpoItemEntity(spoItemEntity);
                }
            }
        }
    }
}
