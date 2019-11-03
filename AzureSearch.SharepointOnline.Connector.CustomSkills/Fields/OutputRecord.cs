//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BishopBlobCustomSkill.Fields
{
    class OutputRecord
    {
        public class OutputRecordData
        {
            [JsonProperty(PropertyName = "tags")]
            public string Tags { get; set; } = "";
            public string ACLS { get; set; } = "";
            public string SourceUrl { get; set; } = "";
            public string CreatedAuthorDisplayName { get; set; } = "";
            public string SPWebUrl { get; set; } = "";
            public string DocumentType { get; set; } = "";
            public IList<String> Region { get; set; } = new List<String>();
            public IList<String> Country { get; set; } = new List<String>();
            public IList<string> AustraliaState { get; set; } = new List<String>();
            public IList<String> Asset { get; set; } = new List<String>();
            public string LinkFilename { get; set; } = "";

        }

        public class OutputRecordMessage
        {
            public string Message { get; set; }
        }

        public string RecordId { get; set; }
        public OutputRecordData Data { get; set; }
        public List<OutputRecordMessage> Errors { get; set; }
        public List<OutputRecordMessage> Warnings { get; set; }
    }
}