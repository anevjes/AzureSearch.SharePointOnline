using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BishopBlobCustomSkill.Services
{
    public class SharePointFileMetadata
    {
        public string SPWebUrl { get; set; } = "";
        public string CreatedAuthorDisplayName { get; set; } = "";
        public string DocumentType { get; set; } = "";
        public IList<string> Region { get; set; } = new List<string>();
        public IList<string> Country { get; set; } = new List<string>();
        public IList<string> AustraliaState { get; set; } = new List<string>();
        public IList<string> Asset { get; set; } = new List<string>();
        public string LinkFilename { get; set; } = "";
        

    }
}
