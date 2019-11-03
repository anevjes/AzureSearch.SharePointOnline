//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

namespace BishopBlobCustomSkill.Controllers
{
    class InputRecord
    {
        public class InputRecordData
        {
            public string DocPath { get; set; }
            public string SASToken { get; set; }
            public string Metadataurl { get; set; }
        }

        public string RecordId { get; set; }
        public InputRecordData Data { get; set; }
    }
}
