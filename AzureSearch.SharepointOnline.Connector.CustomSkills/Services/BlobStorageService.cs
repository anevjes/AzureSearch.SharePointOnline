using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BishopBlobCustomSkill.Services
{
    public class BlobStorageService
    {
        public void ReadMetadata(string sasToken, string blobUrl)
        {
            var connectionString = $"BlobEndpoint{blobUrl}?{sasToken}";

        }
    }
}
