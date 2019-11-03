using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BishopBlobCustomSkill.Services
{
    public interface ISharePointMetadataService
    {
        Task<SharePointFileMetadata> GetMetadata(Uri metadataUri);
        Dictionary<string, object> MapMetadataToOutput(IDictionary<string, object> metadata);

        Task<IDictionary<string, object>> GetMetadataAsDictionary(Uri metadataUri);
    }
}
