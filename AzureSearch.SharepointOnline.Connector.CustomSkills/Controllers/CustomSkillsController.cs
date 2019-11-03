//THIS CODE IS PROVIDED AS IS WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AzureSearch.SharepointOnline.Connector.CustomSkills.Config;
using BishopBlobCustomSkill.Fields;
using BishopBlobCustomSkill.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace BishopBlobCustomSkill.Controllers
{
    /// <summary>
    /// Class structured to deserialize input format: https://docs.microsoft.com/en-us/azure/search/cognitive-search-custom-skill-interface
    /// </summary>
    class CustomSkillApiRequest
    {
        public List<InputRecord> Values { get; set; }
    }

    class WebApiResponse
    {
        public List<OutputRecord> Values { get; set; }
    }

    class CustomSkillApiResponse
    {
        public List<CustomSkillOutputRecord> Values { get; set; }
    }

    class CustomSkillOutputRecord
    {
        public class OutputRecordMessage
        {
            public string Message { get; set; }
        }

        public string RecordId { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public List<OutputRecordMessage> Errors { get; set; }
        public List<OutputRecordMessage> Warnings { get; set; }
    }

    

    [Route("api/[controller]")]
    //[Route("api/")]
    [ApiController]
    public class CustomSkillsController : ControllerBase
    {
        private readonly string ApiKey;

        private ISharePointMetadataService svc;
        private const string ApiKeyHeader = "SPOMetadataMapper-Api-Key";
        public CustomSkillsController(ISharePointMetadataService svc, IOptions<EnvironmentConfig> environmentOptions)
        {
            this.svc = svc;
            this.ApiKey = environmentOptions.Value.ApiKey;
        }



        [HttpGet]
        [HttpPost]
        [Route("MergeSharePointMetadatav2")]
        public async Task<ActionResult<string>> MergeSharePointMetadatav2()
        {

            System.Diagnostics.Trace.WriteLine("Starting call");

           var response = new WebApiResponse()
            {
                Values = new List<OutputRecord>()
            };

            string requestBody = new StreamReader(Request.Body).ReadToEnd();
            //dynamic data = JsonConvert.DeserializeObject(requestBody);
            var data = JsonConvert.DeserializeObject<CustomSkillApiRequest>(requestBody);

            // Do some schema validation
            if (data == null)
            {
                return new BadRequestObjectResult("The request schema does not match expected schema.");
            }
            if (data.Values == null)
            {
                return new BadRequestObjectResult("The request schema does not match expected schema. Could not find values array.");
            }

            // Calculate the response for each value.
            foreach (var record in data.Values)
            {
                if (record == null || record.RecordId == null) continue;



                OutputRecord responseRecord = new OutputRecord
                {
                    RecordId = record.RecordId,
                };


                try
                {
                    System.Diagnostics.Trace.WriteLine("Record Metadata URL Details: {0}", record.Data.Metadataurl);
                    responseRecord.Data = new OutputRecord.OutputRecordData();
                    var metadata = await svc.GetMetadata(new Uri(record.Data.Metadataurl));

                    responseRecord.Data.ACLS = "";
                    responseRecord.Data.SPWebUrl = metadata.SPWebUrl;
                    responseRecord.Data.CreatedAuthorDisplayName = metadata.CreatedAuthorDisplayName;
                    //responseRecord.Data.DocumentType = metadata.DocumentType;
                    //responseRecord.Data.Region = metadata.Region;
                    //responseRecord.Data.Country = metadata.Country;
                    //responseRecord.Data.AustraliaState = metadata.AustraliaState;
                    //responseRecord.Data.Asset = metadata.Asset;
                    responseRecord.Data.LinkFilename = metadata.LinkFilename;

                }
                catch (Exception e)
                {
                    // Something bad happened, log the issue.

                   System.Diagnostics.Trace.TraceInformation("Something [info] bad happened {0}", e.Message.ToString());
                   System.Diagnostics.Trace.TraceError("Something [error] bad happened {0}", e.Message.ToString());
                    System.Diagnostics.Trace.WriteLine("Something [error] bad happened {0}", e.Message.ToString());
                    var error = new OutputRecord.OutputRecordMessage
                    {

                        Message = e.InnerException.Message
                    };

                    responseRecord.Errors = new List<OutputRecord.OutputRecordMessage>
                    {
                        error
                    };
                }
                finally
                {
                    response.Values.Add(responseRecord);
                }
            }

            return (ActionResult)new OkObjectResult(response);

        }

        //private static OutputRecord.OutputRecordData GetEntityMetadata(InputRecord.InputRecordData input)
        //{
        //    var response = new OutputRecord.OutputRecordData()
        //    {
        //        ACLS = "no acls 20190821-1",
        //        Tags = "Metadata1: " + input.Metadata1,
        //        SourceUrl = input.SPUrl
        //    };

        //    return response;
        //}

        [HttpGet]
        [HttpPost]
        [Route("MergeSharePointMetadata")]
        public async Task<ActionResult<string>> MergeSharePointMetadata()
        {
            // Check API Key

            System.Diagnostics.Trace.WriteLine("Starting call");

            var requestApiKey = Request.Headers[ApiKeyHeader].FirstOrDefault<string>();

            if (requestApiKey != ApiKey) {
                return new BadRequestObjectResult("Invalid API key for custom skill");
            }

            var response = new CustomSkillApiResponse()
            {
                Values = new List<CustomSkillOutputRecord>()
            };

            string requestBody = new StreamReader(Request.Body).ReadToEnd();
            var data = JsonConvert.DeserializeObject<CustomSkillApiRequest>(requestBody);

            // Do some schema validation
            if (data == null)
            {
                return new BadRequestObjectResult("The request schema does not match expected schema.");
            }
            if (data.Values == null)
            {
                return new BadRequestObjectResult("The request schema does not match expected schema. Could not find values array.");
            }

            // Calculate the response for each value.
            foreach (var record in data.Values)
            {
                if (record == null || record.RecordId == null) continue;

                CustomSkillOutputRecord responseRecord = new CustomSkillOutputRecord
                {
                    RecordId = record.RecordId,
                };



                try
                {
                    System.Diagnostics.Trace.WriteLine("Record Metadata URL Details: {0}", record.Data.Metadataurl);
                    var metadata = await svc.GetMetadataAsDictionary(new Uri(record.Data.Metadataurl));                    
                    responseRecord.Data = svc.MapMetadataToOutput(metadata);
                }
                catch (Exception e)
                {
                    // Something bad happened, log the issue.

                    System.Diagnostics.Trace.TraceInformation("Something [info] bad happened {0}", e.Message.ToString());
                    System.Diagnostics.Trace.TraceError("Something [error] bad happened {0}", e.Message.ToString());
                    System.Diagnostics.Trace.WriteLine("Something [error] bad happened {0}", e.Message.ToString());
                    var error = new CustomSkillOutputRecord.OutputRecordMessage
                    {

                        Message = e.InnerException.Message
                    };

                    responseRecord.Errors = new List<CustomSkillOutputRecord.OutputRecordMessage>
                    {
                        error
                    };
                }
                finally
                {
                    response.Values.Add(responseRecord);
                }
            }

            return (ActionResult)new OkObjectResult(response);

        }
    }
}
