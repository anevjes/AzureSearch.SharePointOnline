using AzureSearch.SharepointOnline.Connector.CustomSkills.Config;
using BishopBlobCustomSkill.Config;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace BishopBlobCustomSkill.Services
{
    public class SharePointMetadataService : ISharePointMetadataService
    {
        private CloudStorageAccount storageAccount; 
        private CloudBlobClient blobClient;
        private string endpoint;
        private readonly JObject metadataMapping;
        private IDictionary<string, string> metadataToFieldMapping = new Dictionary<string, string>();
        public SharePointMetadataService(IOptions<ConnectionStringsConfig> configOption, 
            IOptions<AppSettingsEnvironmentConfig> appSettingsEnvironmentOptions,
            IOptions<EnvironmentConfig> environmentOptions)
        {

            storageAccount = CloudStorageAccount.Parse(configOption.Value.MetadataStorageConnectionString);
            blobClient = storageAccount.CreateCloudBlobClient();

            // Read mapping file and extract field mappings from metadata to output fields
            var mappingFilePath = environmentOptions.Value.MappingFile;

            if (mappingFilePath == null || mappingFilePath.Trim().Length == 0)
            {
                mappingFilePath = appSettingsEnvironmentOptions.Value.MappingFile;
            }

            metadataMapping = JObject.Parse(File.ReadAllText(mappingFilePath));

            metadataToFieldMapping = MappingToDictionary(metadataMapping);

            
        }


        private IDictionary<string, string> MappingToDictionary(JObject mappingJson)
        {
            var mapping = mappingJson["outputMapping"];

            var d = new Dictionary<string, string>();

            foreach (JObject m in mapping)
            {
                d.Add(m["metadataFieldName"].ToString(), m["outputFieldName"].ToString());
            }

            return d;
        }


        //public string GetMetadata(string metadataUrl)
        //{
        //    CloudStorageAccount storageAccount = CreateStorageAccountFromConnectionString(CloudConfigurationManager.GetSetting("StorageConnectionString"));
        //}
        public async Task<SharePointFileMetadata> GetMetadata(Uri metadataUri)
        {
            CloudBlockBlob cbb = new CloudBlockBlob(metadataUri, blobClient);
            using (var ms = new MemoryStream())
            {
                await cbb.DownloadToStreamAsync(ms);
                var x = DeserializeFromStream(ms);
                var output = new SharePointFileMetadata();
                output.CreatedAuthorDisplayName = x.createdAuthorDisplayName;
                output.SPWebUrl = x.SPWebUrl;
                output.DocumentType = x.Documenttype;
                object o = x.Region;
                output.Region = JArrayToStringCollection(x.Region);
                output.Country = JArrayToStringCollection(x.Country);               
                output.AustraliaState = JArrayToStringCollection(x.AustraliaState_x0028_ifapplicable_x0029_);
                output.Asset = JArrayToStringCollection(x.Asset); 
                output.LinkFilename = x.LinkFilename;
                return output;
            }
        }

        public async Task<IDictionary<string, object>> GetMetadataAsDictionary(Uri metadataUri)
        {
            CloudBlockBlob cbb = new CloudBlockBlob(metadataUri, blobClient);
            try
            {
                using (var ms = new MemoryStream())
                {
                    await cbb.DownloadToStreamAsync(ms);
                    var metadataDictionary = DeserializeDictionaryFromStream(ms);
                    return metadataDictionary;
                }
            } catch (Exception ex)
            {
                var msg = ex.ToString();
                throw;
            }
        }

        public Dictionary<string, object> MapMetadataToOutput(IDictionary<string, object> metadata)
        {
            var outputDictionary = new Dictionary<string, object>();

            // Key, value pair is: (meta data field name, output field name)
            foreach (var outputMapping in metadataToFieldMapping)
            {
                if (metadata.ContainsKey(outputMapping.Key))
                {
                    outputDictionary.Add(outputMapping.Value, metadata[outputMapping.Key]);
                }
            }
            return outputDictionary;
        }

        private static List<String> JArrayToStringCollection(JArray arry)
        {
            try
            {
                if (arry == null)
                {
                    return new List<String>();
                }
                else
                {
                    return arry.ToObject<List<String>>();
                }
            }
            catch (Exception ex)
            {
                var x = ex.Message;
                return new List<String>();
            }

        }

        public static dynamic DeserializeFromStream(Stream stream)
        {
            stream.Position = 0;
            var serializer = new JsonSerializer();

            using (var sr = new StreamReader(stream))
            using (var jsonTextReader = new JsonTextReader(sr))
            {
                return serializer.Deserialize(jsonTextReader);
            }
        }

        public static IDictionary<string, object> DeserializeDictionaryFromStream(Stream stream)
        {
            stream.Position = 0;
            var serializer = new JsonSerializer();

            using (var sr = new StreamReader(stream))
            {
                var jsonString = sr.ReadToEnd();
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
            }
        }
    }
}
