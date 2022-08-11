using System;
using System.Collections.Generic;
using CluedIn.Core.Data.Relational;
using CluedIn.Core.Providers;

namespace CluedIn.ExternalSearch.Providers.Rest
{
    public static class Constants
    {
        public const string ComponentName = "Rest";
        public const string ProviderName = "Rest";
        public static readonly Guid ProviderId = new Guid("{BAD0FBC6-85FC-4D57-A176-D882D7D0259A}");

        public struct KeyName
        {
            public const string ApiToken = "apiToken";
            public const string Method = "method";
            public const string BaseUrl = "baseUrl";
            public const string Url = "url";
            public const string EntityType = "entityType";
            public const string TriggerProperty = "triggerProperty";
            public const string IngestionBaseEndpoint = "IngestionBaseEndpoint";
            public const string IngestionEndpoint = "ingestionEndpoint";
            public const string Transformer = "transformer";
            public const string RawBody = "rawBody";
        }

        public static string About { get; set; } = "Rest is a generic enrciher for making Rest calls and Parsing the result";
        public static string Icon { get; set; } = "Resources.cluedin.png";
        public static string Domain { get; set; } = "https://www.cluedin.com/";

        public static AuthMethods AuthMethods { get; set; } = new AuthMethods
        {
            token = new List<Control>()
            {
                new Control()
                {
                    displayName = "ApiToken",
                    type = "input",
                    isRequired = true,
                    name = KeyName.ApiToken
                }
            }
        };

        public static IEnumerable<Control> Properties { get; set; } = new List<Control>()
        {
            // NOTE: Leaving this commented as an example - BF
           new Control()
           {
                displayName = "Method",
                type = "input",
                isRequired = true,
                name = "method"
           },
           new Control()
           {
                displayName = "BaseUrl",
                type = "input",
                isRequired = true,
                name = "baseUrl"
           },
           new Control()
           {
                displayName = "Url",
                type = "input",
                isRequired = true,
                name = "url"
           },
           new Control()
           {
                displayName = "EntityType",
                type = "input",
                isRequired = true,
                name = "entityType"
           },
           new Control()
           {
                displayName = "TriggerPropeerty",
                type = "input",
                isRequired = true,
                name = "triggerPropeerty"
           },
           new Control()
           {
                displayName = "IngestionEndpoint",
                type = "input",
                isRequired = true,
                name = "ingestionEndpoint"
           },
            new Control()
           {
                displayName = "IngestionBaseEndpoint",
                type = "input",
                isRequired = true,
                name = "ingestionBaseEndpoint"
           },
           new Control()
           {
                displayName = "Transformer",
                type = "input",
                isRequired = false,
                name = "transformer"
           },
           new Control()
           {
                displayName = "RawBody",
                type = "input",
                isRequired = false,
                name = "rawBody"
           }
        };

        public static Guide Guide { get; set; } = null;
        public static IntegrationType IntegrationType { get; set; } = IntegrationType.Enrichment;
    }
}
