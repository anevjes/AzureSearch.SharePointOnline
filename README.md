# Introduction 
The motivation behind this project is to simplify the ingestion of SharePoint Online Document library content into Azure Cognitive Search.

Azure Cognitive Search brings in a number of benefits over standard SharePoint Online Search allowing you to have more control over how you further refine and enrich content in search through:

- Deep knowledge mining of your content with pre-built AI-based [Azure Cognitive Services](https://docs.microsoft.com/en-us/azure/search/cognitive-search-predefined-skills)
- [Synonyms](https://docs.microsoft.com/en-us/azure/search/search-synonyms
)) for query expansion over a search index
- More control over Type Aheads
- Modeling of relational Data
- Multi Language indexes

### Azure Cognitive Search
Cognitive search is an AI feature in Azure Cognitive Search, used to extract text from images, blobs, and other unstructured data sources - enriching the content to make it more searchable in an Azure Cognitive Search index. Extraction and enrichment are implemented through cognitive skills attached to an indexing pipeline. AI enrichments are supported in the following ways:
Natural language processing skills include entity recognition, language detection, key phrase extraction, text manipulation, and sentiment detection. With these skills, unstructured text can assume new forms, mapped as searchable and filterable fields in an index.

Image processing skills include Optical Character Recognition (OCR) and identification of visual features, such as facial detection, image interpretation, image recognition (famous people and landmarks) or attributes like colors or image orientation. You can create text-representations of image content, searchable using all the query capabilities of Azure Cognitive Search.

In addition to the out of the box pre built Azure Cognitive skills you can further enrich the index ingestion pipeline by building your own custom skills in form of custom Web APIs.

### Azure Cognitive Search indexer:

An [indexer](https://docs.microsoft.com/en-us/azure/search/search-indexer-overview) in Azure Cognitive Search is a crawler that extracts searchable data and metadata from an external Azure data source and populates an index based on field-to-field mappings between the index and your data source. This approach is sometimes referred to as a 'pull model' because the service pulls data in without you having to write any code that adds data to an index.

# SharePoint Online Document Library Connector
Azure Cognitive Search Indexers are based on data source types or platforms, with individual indexers for:
- **SQL Server on Azure**
- **Cosmos DB**
- **Azure Table Storage and Blob Storage**

At the moment there is no out of the box Azure Cognitive Search indexer for SharePoint Online content; hence the reason why we are publishing this project.

## High Level Architecture Overview

![AzureSearch.SharePointOnline High Level Architecture](https://raw.githubusercontent.com/anevjes/AzureSearch.SharePointOnline/master/Diagrams/PNG/HighLevelComponentArchitecture.png)

The solution comprises of the following Azure resources:

- **Azure Cognitive Search**
- **Azure General Purpose Storage Account** - We leverage (BLOBs and Tables)
- **Azure Web Apps**- for hosting custom SharePoint Online Metadata Merging Web API - used for merging SharePoint Field contents with the associated document inside Azure Cognitive Search index
- **Application Insights** for logging

## Software Components:

**AzureSearch.SharePointOnline.Connector:**

Stand-alone DotNetCore console app responsible for:

- Initializing Azure Cognitive Search index / indexer / skillset mapping / synonym mapping against the Azure Cognitive Search instance.
  
- Downloading SharePoint Online Document Library (Documents) and associated Fields - via Microsoft Graph SDK. Graph SDK handles the [Throttling](https://docs.microsoft.com/en-us/graph/throttling) retry attempts back to SharePoint Online for us as it respects the Retry-After HTTP response headers internally.
  
- Drops the discovered Documents and its associated metadata into Azure Blob storage. Container paths in blob storage follow the same naming convention as the urls. For each document that is discovered it is accomponied with an additional .json file which includes all the discovered metadata.<br/>
  
  _Example:_

    HelloWorld.pdf<br/>
    HelloWorld.pdf.json
  
  Where HelloWorld.pdf is the raw PDF file form SharePoint document library and HelloWorld.pdf.json contains SharePoint Field / SPWeb URL location metadata.

  Sample of contents from HelloWorld.pdf.json file:<br/>
```json
{
	"Classification": "HBI",
	"ContentType": "Document",
	"Created": "2019-09-19T05:08:38Z",
	"AuthorLookupId": "12",
	"Modified": "2019-09-19T05:12:40Z",
	"EditorLookupId": "12",
	"SPWebUrl": "https://somespourl.sharepoint.com/DemoDocs/HelloWorld.pdf",
	"LinkFilename": "HelloWorld.pdf",
	"FileSizeDisplay": "1473403",
	"_DisplayName": "",
	"createdAuthorDisplayName": "MOD Administrator"
}
```

- Incremental Crawling (partially working)- As part of this we leverage two Azure Storage Tables:
    - spoIncrementalCrawlerTokens - Used for keepign track of all the Microsoft Graph API Delta tokens per Document Library. This makes our crawling more efficent as we only recrawl the changes
    - Unfortunately as part of delta changes we do not get the URL of the deleted items. Microsoft Graph Delta query only returns ItemID for deleted items - so for us to keep track of the mapping between ItemIds and the URLs we write all the itemIds and their associated URLs inside spoItems Table. This way when it comes to removing deleted SharePoint Document Library items from azure index we can achieve this through this mapping.
- 
Usage example:


 ``` Usage example
AzureSearch.SharePointConnector.exe [-fullcrawl | -incrementalcrawl]
```

**AzureSearch.SharepointOnline.Connector.CustomSkills:**

Enriches the Azure Search Index with SharePoint metadata. This component is invoked as part of the Azure Cognitive Search indexer custom skill pipeline. You can read more details on the configuration around this compnent in the Getting Started Area.

## Current Functionality

At the moment the following functionality is available:

- Crawling of SharePoint online Document Libraries through Graph API.
- Association of SharePoint Document Library Fields to the documents - providing a single complete Azure search index with all the related data. 
- Crawl Type: **Full Crawl**
- Automatic creation of Azure Cognitive Search Index Schema based on the fields you need and BLOB indexer.
   
## Planned Functionality

Please note that some of the dev work on the below functionality has already commenced as you will see in the source code.

- Incremental crawling thorugh the use of [Graph API delta Tokens](https://docs.microsoft.com/en-us/graph/delta-query-overview) and leveraging [Azure Table storage](https://docs.microsoft.com/en-us/azure/storage/tables/table-storage-overview) for tracking of the last known change tokens during crawl time for more efficient crawling
- SharePoint Online ACL / Azure Search Security Trimming

Cognitive 

# Getting Started

## 1. Create Azure Resources

- Create brand new **Azure Resource Group**
- Inside the resource group create a new:
    - **General purpose v2 Storage Account** 
        - Create new BLOB container named **spocontent** and make sure it's Access Level is set to Private

        - Create Storage Table named:
            - **spoIncrementalCrawlerTokens**
        - Create Storage Table named:
            - **spoItems**

        Extract the **Connection String** for this account as you will need it to update the following files in later steps:
        - AzureSearch.SharePointOnline.Connector/appSettings.json 
        - AzureSearch.SharepointOnline.Connector.CustomSkills/appSettings.json

    -  **Azure Search Service** - Select a plan based on your scale needs. Make sure to extract the **Search Admin Key** as we will need this key to be updated inside:
        - AzureSearch.SharePointOnline.Connector/appSettings.json file.

    - **Azure Cognitive Services** - Make sure to extract the Cognitive services Keys once setup; as we will need this key to be updated inside:
        - AzureSearch.SharePointOnline.Connector/appSettings.json file.
    - **Azure App Service Plan / Web App** - Select a plan based on your estimated crawl freshness requirements. We will use this to host the **AzureSearch.SharepointOnline.Connector.CustomSkills WebAPI** required during search indexing stages.

    - **Azure Application Insights** - We will leverage Application Insights for logging purposes. Make sure to extract the App Insights Key once setup; as we will need this key to be updated inside:
        - AzureSearch.SharePointOnline.Connector/appSettings.json file.
    

## 2. Configure Azure Active Directory App Registration:
Calls to Graph API - for SharePoint online crawling will be perfomed by an AAD app registration. We require an AAD App registration to be created in your SharePoint Online AAD tenant. This will need to be carried out by your AAD *Global Admin* account as we will need them to grant consent. 

- Login into Azure Portal
- Switch Directory to your SharePoint Online Directory
- Locate your **Azure Active Directory** Resource and open the AAD blade
- Select **App registrations** from AAD blade
- Click on **+New registration**
    - Name:  _AzureSearch.SharePointOnline.Crawler_
    - Supported account types: _Accounts in this organizational directory only (only - Single tenant)_
    - Redirect URI: _set it to whatever you like - needs to be **https** if you set this value_
    - Click **Create**

- You will now be presented with a summary screen for your newly created AAD app registration. Make sure to capture the following information from this screen as you will shortly need to provide these details inside the AzureSearch.SharePointOnline.Connector/appSettings.json file.

- IMAGE OF AAD APP REG Summary screen here:

- In the App registration Summary screen select **Certificates & secrets**
- Under Client secrets - select **+New client secret**
- Add description / duration as per your standards.
- Make sure to copy the generated secret as we will be using the value inside the AzureSearch.SharePointOnline.Connector/appSettings.json file.

IMAGE-CLIENZT SECRET

**API Permissions for Microsoft Graph**

Now we need to grant the newly created AAD app registration with Graph API permissions.
 - Click on **API Permissions**
 - Click on **+Add a permission**
 - Select **Microsoft Graph**
 - Grant the follwoing permissions:
    - https://graph.microsoft.com/Group.Read.All
    - https://graph.microsoft.com/Sites.Read.All
 - Now Admin Consent the permissions for your PSO tenant by clicking the **Grant admin consent for 'Your tenant'**
 IMAGE API PERMISSIONS

## 2. Define the Azure Search Index, Indexer, SkillSet and SnynonymMap Defintions

### Index Definition
During this phase we will need to consider all the SharePoint Fields that we would like to be indexed and exposed via Azure Cognitive Search. We have provided a sample Azure Cognitive Search index definition for you which includes some basic SharePoint Fields as part of the index. 

Add Azure Cognitive Search [Fields](https://docs.microsoft.com/en-us/azure/search/search-what-is-an-index) as you require them but make sure to always keep **SPWebUrl** and **blobUri** as the code has dependendencies on these two fields. If you want to you can also add custom [Suggesters](https://docs.microsoft.com/en-us/azure/search/search-what-is-an-index#suggesters), [scoring profiles](https://docs.microsoft.com/en-us/azure/search/search-what-is-an-index#scoring-profiles) and [analyzers](https://docs.microsoft.com/en-us/azure/search/search-what-is-an-index#analyzers) as part of your index definition.

**Important Note:** Take note of the index Field names from this file as you will use them as config across the following files:

    AzureSearch.SharePointOnline.Connector/SearchDefinitions/blobIndexer.json

    AzureSearch.SharepointOnline.Connector.CustomSkills/Mapping/metadatatoindexmapping.json


- Open **AzureSearch.SharePointOnline.Connector/SearchDefinitions/blobIndex.json**

    Sample Index Definition below:

```json
 {
  "fields": [
    {
      "name": "id",
      "type": "Edm.String",
      "searchable": false,
      "filterable": false,
      "retrievable": true,
      "sortable": false,
      "facetable": false,
      "key": true
    },
    {
      "name": "blobUri",
      "type": "Edm.String",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "sortable": true,
      "facetable": false
    },
    {
      "name": "fullText",
      "type": "Edm.String",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "sortable": false,
      "facetable": false
    },
    {
      "name": "languageCode",
      "type": "Edm.String",
      "searchable": true,
      "filterable": true,
      "retrievable": true,
      "sortable": false,
      "facetable": true
    },
    {
      "name": "keyPhrases",
      "type": "Collection(Edm.String)",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "sortable": false,
      "facetable": false,
      "synonymMaps": [
        "[SynonymMapName]"
      ]
    },
    {
      "name": "organizations",
      "type": "Collection(Edm.String)",
      "searchable": true,
      "filterable": true,
      "retrievable": true,
      "sortable": false,
      "facetable": false
    },
    {
      "name": "locations",
      "type": "Collection(Edm.String)",
      "searchable": true,
      "filterable": true,
      "retrievable": true,
      "sortable": false,
      "facetable": true
    },
    {
      "name": "SPWebUrl",
      "type": "Edm.String",
      "searchable": true,
      "sortable": false,
      "filterable": false,
      "facetable": false,
      "retrievable": true
    },
    {
      "name": "ContentType",
      "type": "Edm.String",
      "searchable": true,
      "sortable": false,
      "filterable": true,
      "facetable": true,
      "retrievable": true
    },
    {
      "name": "CreatedAuthorDisplayName",
      "type": "Edm.String",
      "searchable": true,
      "sortable": true,
      "filterable": true,
      "facetable": true,
      "retrievable": true
    },
    {
      "name": "LinkFilename",
      "type": "Edm.String",
      "searchable": true,
      "sortable": true,
      "filterable": true,
      "facetable": false,
      "retrievable": true
    },
    {
      "name": "people",
      "type": "Collection(Edm.String)",
      "searchable": true,
      "filterable": true,
      "retrievable": true,
      "sortable": false,
      "facetable": false
    },
    {
      "name": "dateTimes",
      "type": "Collection(Edm.String)",
      "searchable": false,
      "filterable": true,
      "retrievable": true,
      "sortable": false,
      "facetable": false
    },
    {
      "name": "typelessEntities",
      "type": "Collection(Edm.String)",
      "searchable": false,
      "filterable": false,
      "retrievable": true,
      "sortable": false,
      "facetable": false
    },
    {
      "name": "imageDescriptions",
      "type": "Collection(Edm.String)",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "sortable": false,
      "facetable": false
    },
    {
      "name": "imageCategories",
      "type": "Collection(Edm.String)",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "sortable": false,
      "facetable": false
    },
    {
      "name": "imageTags",
      "type": "Collection(Edm.String)",
      "searchable": true,
      "filterable": false,
      "retrievable": true,
      "sortable": false,
      "facetable": false
    },
    {
      "name": "ocrPrintedText",
      "type": "Edm.String",
      "searchable": true,
      "sortable": true,
      "filterable": true,
      "facetable": false,
      "retrievable": true
    }
  ],
  "corsOptions": {
    "allowedOrigins": [ "*" ]
  },
  "suggesters": [
    {
      "name": "sg",
      "searchMode": "analyzingInfixMatching",
      "sourceFields": [ "keyPhrases", "organizations", "locations", "people" ]
    }
  ]
}
```
## Indexer Definition

During this stage you will need to map index fields from the index definition deifned in previous step to the Azure BLOB indexer. This is performed via fieldMappings.

**Important:**
Do not modify any of the below fields as we inject these values for you:
```json
{
  "name": "[IndexerName]",
  "dataSourceName": "[DataSourceName]",
  "targetIndexName": "[IndexName]",
  "skillsetName": "[SkillSetName]",
..
}
```

- Open up **AzureSearch.SharePointOnline.Connector/SearchDefinitions/blobIndexer.json** file


    Sample Indexer Definition:

```json
{
  "name": "[IndexerName]",
  "dataSourceName": "[DataSourceName]",
  "targetIndexName": "[IndexName]",
  "skillsetName": "[SkillSetName]",
  "fieldMappings": [
    {
      "sourceFieldName": "metadata_storage_path",
      "targetFieldName": "id",
      "mappingFunction": { "name": "base64Encode" }
    },
    {
      "sourceFieldName": "metadata_storage_path",
      "targetFieldName": "blobUri"
    },
    {
      "sourceFieldName": "metadata_storage_name",
      "targetFieldName": "metadata_storage_name"
    },
    {
      "sourceFieldName": "metadata_storage_sas_token",
      "targetFieldName": "metadata_storage_sas_token"
    },
    {
      "sourceFieldName": "metadataurl",
      "targetFieldName": "metadataurl"
    }

  ],
  "outputFieldMappings": [
    {
      "sourceFieldName": "/document/fullText",
      "targetFieldName": "fullText"
    },
    {
      "sourceFieldName": "/document/languageCode",
      "targetFieldName": "languageCode"
    },
    {
      "sourceFieldName": "/document/fullText/pages/*/keyPhrases/*",
      "targetFieldName": "keyPhrases"
    },
    {
      "sourceFieldName": "/document/fullText/pages/*/organizations/*",
      "targetFieldName": "organizations"
    },
    {
      "sourceFieldName": "/document/fullText/pages/*/locations/*",
      "targetFieldName": "locations"
    },
    {
      "sourceFieldName": "/document/fullText/pages/*/people/*",
      "targetFieldName": "people"
    },
    {
      "sourceFieldName": "/document/fullText/pages/*/dateTimes/*",
      "targetFieldName": "dateTimes"
    },
    {
      "sourceFieldName": "/document/fullText/pages/*/typelessEntities/*/name",
      "targetFieldName": "typelessEntities"
    },
    {
      "sourceFieldName": "/document/normalized_images/*/imageDescriptions/captions/*/text",
      "targetFieldName": "imageDescriptions"
    },
    {
      "sourceFieldName": "/document/normalized_images/*/imageCategories/*/name",
      "targetFieldName": "imageCategories"
    },
    {
      "sourceFieldName": "/document/normalized_images/*/imageTags/*/name",
      "targetFieldName": "imageTags"
    },
    {
      "sourceFieldName": "/document/CreatedAuthorDisplayName",
      "targetFieldName": "CreatedAuthorDisplayName"
    },
    {
      "sourceFieldName": "/document/SPWebUrl",
      "targetFieldName": "SPWebUrl"
    },
    {
      "sourceFieldName": "/document/LinkFilename",
      "targetFieldName": "LinkFilename"
    },
    {
      "sourceFieldName": "/document/ContentType",
      "targetFieldName": "ContentType"
    }
  ],
  "parameters": {
    "batchSize": 1,
    "maxFailedItems": -1,
    "maxFailedItemsPerBatch": -1,
    "configuration": {
      "dataToExtract": "contentAndMetadata",
      "imageAction": "generateNormalizedImages",
      "excludedFileNameExtensions": ".json,.js",
      "failOnUnsupportedContentType": false,
      "indexStorageMetadataOnlyForOversizedDocuments": true,
      "failOnUnprocessableDocument": false
    }
  }
}
```

## Skillset Definition

This is the area where we define the different skills that will run through as part of the indexer content enrichment pipeline. 

Here we have a number out of the box Azure Cognitive Search skills such as OCR detection and you will notice this is where we have out Custom WebAPI skillset defined. This is pointing back to our AzureSearch.SharepointOnline.Connector.CustomSkills WebAPI.

This custom skill provides us with the ability to extract information from two different datasets (raw sharePoint SPFile contents and the associated metadata which we store as an associated SPFile.json) and enrich the same Azure Cognitive Search index.

AzureSearch.SharepointOnline.Connector.CustomSkills skill needs to known which SharePoint Fields from the metadata.json files that are uploaded to the Azure Storage BLOB container as part of the 'AzureSearch.SharePointOnline.Connector' run need to be written back to which fields in Azure search Index. 


 - Locate the following section  within the:
**AzureSearch.SharePointOnline.Connector/SearchDefinitions/blobSkillset.json file.**

```json
{
      "@odata.type": "#Microsoft.Skills.Custom.WebApiSkill",
      "description": "Our SharePoint Metadata mapping custom skill",
      "uri": "[CustomSpoMetadataSkillUri]",
      "batchSize": 1,
      "context": "/document",
      "httpHeaders": {
        "SPOMetadataMapper-Api-Key": "[SPOMetadataMapper-Api-Key]"
      },
      "inputs": [
        {
          "name": "docpath",
          "source": "/document/blobUri"
        },
        {
          "name": "sastoken",
          "source": "/document/metadata_storage_sas_token"
        },
        {
          "name": "metadataurl",
          "source": "/document/metadataurl"
        }

      ],
      "outputs": [
        {
          "name": "tags",
          "targetName": "tags"
        },
        {
          "name": "acls",
          "targetName": "acls"
        },
        {
          "name": "createdAuthorDisplayName",
          "targetName": "CreatedAuthorDisplayName"
        },
        {
          "name": "SPWebUrl",
          "targetName": "SPWebUrl"
        },
        {
          "name": "LinkFilename",
          "targetName": "LinkFilename"
        },
        {
          "name": "ContentType",
          "targetName": "ContentType"
        }
      ]
    }
 ```  

**Important:**<br/>
    **inputs** are field names from Azure search BLOB indexer - never remove the following:
```json
inputs": [
       {
         "name": "docpath",
         "source": "/document/blobUri"
       },
       {
         "name": "sastoken",
         "source": "/document/metadata_storage_sas_token"
       },
       {
         "name": "metadataurl",
         "source": "/document/metadataurl"
       }
```

   **outputs** are back to the index Field names.
 
 <br/>

 

## Synonym Map Definition

Lastly we have a sample Synonym Map file which you can tweak as per the following [article](https://docs.microsoft.com/en-us/rest/api/searchservice/create-synonym-map).

We have provided a sample synonym map file for you inside:
AzureSearch.SharePointOnline.Connector/SearchDefinitions/blobSynonymMap.json.

**Important:**
Do not modify the name attribute of the below defintion.

```json
{
  "name": "[SynonymMapName]",
  "format": "solr",
  "synonyms": "MS => Microsoft\nAzure,cloud,nube\nvirtual machine,máquina virtual,vm\nDocker,containers,contenedores"
}
```


## 3. Deploy / Configure AzureSearch.SharepointOnline.Connector.CustomSkills: <br/>

During Azure Cognitive Search indexing - we call out into a custom Skill; which enriches the crawled SharePoint Document LIbrary Attachment data with raw SharePoint Field data. Before we can run the SPOConnector console app - we have to deploy **AzureSearch.SharepointOnline.Connector.CustomSkills** to a public endpoint.

- Open up AzureSearch.SharepointOnline.Connector.CustomSkills/appSettings.json

- Update the following properties within the appSettings.json file:

    - **MetadataStorageConnectionString** - set this to the full connection string to the General Purpose Storage account you setup as part of **step 1** above.

    - **EnvironmentConfig.ApiKey** - We use this key as another layer of authorization for the calls to the custom WebAPI from the indexer. You can set this to any value you like - we generate a GUID and set that valeu in here. Please make sure to save this value as you will need to supply this value inside **AzureSearch.SharePointOnline.Connector/appSettings.json** file later on.

    - **EnvironmentConfig.MappingFile** - This is a index to sharepoint field mapping file location. We have set this to be configurable incase you want to easily port the hosting of this WebAPI to a container. This generally points to physical path of where AzureSearch.SharepointOnline.Connector.CustomSkills project lives **/Mapping/metadatatoindexmapping.json**

    Now we are completed with the changes required inside *AzureSearch.SharepointOnline.Connector.CustomSkills/appSettings.json* time to update one more file inside AzureSearch.SharepointOnline.Connector.CustomSkills project.


- Open up **AzureSearch.SharepointOnline.Connector.CustomSkills/Mapping/metadatatoindexmapping.json**


    *Sample:* AzureSearch.SharepointOnline.Connector.CustomSkills/Mapping/metadatatoindexmapping.json



```json
{
  "description": "",
  "outputMapping": [
    {
      "metadataFieldName": "ContentType",
      "outputFieldName": "ContentType"
    },
    {
      "metadataFieldName": "Created",
      "outputFieldName": "Created"
    },
    {
      "metadataFieldName": "Modified",
      "outputFieldName": "Modified"
    },
    {
      "metadataFieldName": "SPWebUrl",
      "outputFieldName": "SPWebUrl"
    },
    {
      "metadataFieldName": "LinkFilename",
      "outputFieldName": "LinkFilename"
    },
    {
      "metadataFieldName": "createdAuthorDisplayName",
      "outputFieldName": "createdAuthorDisplayName"
    }
  ]
}
```
**Important:**  
You will see a file that looks as per the above structure. This is a mapping file which describes which SharePoint FieldName you want mapped to what Azure Cognitive Search Index Field name. 

**metadataFieldName** is the name of the SharePoint Field that is stored as part of the metadata json file during crawling.

**outputFieldName** is the name of the Azure Cognitive Search index that you want to map the MetadataFieldName to. Make sure that the outputFieldName is a valid field inside Azure Cognitive Search Index defintion located in:

    AzureSearch.SharePointOnline.Connector/SearchDefinitions/blobIndexer.json 
    
    and 

    AzureSearch.SharePointOnline.Connector/SearchDefinitions/blobIndex.json


- Now you're ready to deploy the AzureSearch.SharepointOnline.Connector.CustomSkills WebAPI to the Azure WebApp you built as part of Step 1 above.
  
- Grab the endpoint URL of the deployed WebAPi as you will need to place this URL inside AzureSearch.SharePointOnline.Connector/appSettings.json as per the below next set of steps. 



## AzureSearch.SharePointOnline.Connector Configuration Settings

Within the AzureSearch.SharePointOnline.Connector project you will need to modify the following configuration files:

*AzureSearch.SharePointOnline.Connector/appSettings.json*

```json
{
  "ConnectionStrings": {
    "AADDetails": {
      "applicationId": "",
      "applicationSecret": "",
      "tenantId": "",
      "redirectUri": "https://microsoft.com",
      "domain": "sometenant.onmicrosoft.com"
    },
    "SearchDetails": {
      "name": "YOUR_SEARCH_NAME",
      "adminKey": "",
      "indexName": "demo-index",
      "blobDataSourceName": "blob-datasource",
      "blobSynonymMapName": "blob-synonymmap",
      "blobSkillsetName": "demo-skillset",
      "blobIndexerName": "demo-indexer",
      "cognitiveAccount": "/subscriptions/REPLACE_SUBSCRIPTION_GUID/resourceGroups/REPLACE_RESOURCE_GROUP_NAME/providers/Microsoft.CognitiveServices/accounts/REPLACE_COGNITIVESERVICE_NAME/",
      "cognitiveKey": "",
      "customSpoMetadataSkillUri": "https://REPLACE_CUSTOM_SPOMETADATAHOST.azurewebsites.net/api/customskills/MergeSharePointMetadata",
      "SPOMetadataMapper-Api-Key": "THISNEEDSTOMATCH_GUID_FROM_SPOMETADATA_WEBAPI_APPSETTINGS"
    },
    "StorageDetails": {
      "storageAccountName": "YOUR_SEARCH_NAME",
      "storageAccountKey": "",
      "storageBlobContainerName": "spocontent",
      "storageTableName": "spoIncrementalCrawlerTokens",
      "spoItemStorageTableName": "spoItems"
    },
    "SPODetails": {
      "spoHostName": "somespohost.sharepoint.com",
      "siteUrl": "/",
      "metadataJSONStore": true,
      "metadataFieldsToIgnore": [
        "@odata.context",
        "@odata.id",
        "FileLeafRef",
        "@odata.etag",
        "LinkFilenameNoMenu",
        "DocIcon",
        "FolderChildCount",
        "_UIVersionString",
        "ParentVersionStringLookupId",
        "ParentLeafNameLookupId",
        "responseHeaders",
        "statusCode",
        "_ComplianceFlags",
        "_ComplianceTag",
        "_ComplianceTagWrittenTime",
        "_ComplianceTagUserId",
        "_CommentCount",
        "_LikeCount",
        "ItemChildCount",
        "Edit",
        "_CheckinComment"
      ],
      "docLibExclusions": [
      ]
    }
  },
  "Logging": {
    "key": "APPINSIGHTS_KEY",
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```


Most of the properties in the above JSON file are self explanatory.

For the **AADDetails** section fill in the details from your newly geenrated AAD App Registration you performed as part of step 2 above.


For **SearchDetails** fill in the details from your newly stood up Azure Cognitive Search instance and the cogntive Service.

Make sure to paste the same value you had generated and entered into **'AzureSearch.SharepointOnline.Connector.CustomSkills/appsettings.json' EnvironmentConfig.ApiKey** back into 

*  SearchDetails.SPOMetadataMapper-Api-Key 

and

Make sure that you place the public URI path to the AzureSearch.SharepointOnline.Connector.CustomSkills WebAPI inside:

 * SearchDetails.customSpoMetadataSkillUri property

inside *AzureSearch.SharePointOnline.Connector/appSettings.json*  



# Run
 
 You now have all the components and config setup.

 You can now build AzureSearch.SharePointOnline.Connector Project and run a crawl against your SPOSite/s.

  ``` Usage example
AzureSearch.SharePointConnector.exe [-fullcrawl | -incrementalcrawl]
```

TODO: Add in a GIF Animation of a working  AzureSearch.SharePointOnline.Connector Console App as a demo.

# Contribute
