using System.Collections.Generic;
using CluedIn.Core.Crawling;

namespace CluedIn.ExternalSearch.Providers.Rest
{
    public class RestExternalSearchJobData : CrawlJobData
    {
        public RestExternalSearchJobData(IDictionary<string, object> configuration)
        {
            ApiToken = GetValue<string>(configuration, Constants.KeyName.ApiToken);
            Method = GetValue<string>(configuration, Constants.KeyName.Method);
            BaseUrl = GetValue<string>(configuration, Constants.KeyName.BaseUrl);
            Url = GetValue<string>(configuration, Constants.KeyName.Url);
            EntityType = GetValue<string>(configuration, Constants.KeyName.EntityType);
            TriggerPropeerty = GetValue<string>(configuration, Constants.KeyName.TriggerProperty);
            IngestionEndpoint = GetValue<string>(configuration, Constants.KeyName.IngestionEndpoint);
            Transformer = GetValue<string>(configuration, Constants.KeyName.Transformer);
            RawBody = GetValue<string>(configuration, Constants.KeyName.RawBody);
        }

        public IDictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object> {
                {
                    Constants.KeyName.ApiToken, ApiToken
                },
                {
                    Constants.KeyName.Method, Method
                },
                {
                    Constants.KeyName.BaseUrl, BaseUrl
                },
                {
                    Constants.KeyName.Url, Url
                },
                {
                    Constants.KeyName.EntityType, EntityType
                },
                {
                    Constants.KeyName.TriggerProperty, TriggerPropeerty
                },
                {
                    Constants.KeyName.IngestionEndpoint, IngestionEndpoint
                },
                {
                    Constants.KeyName.Transformer, Transformer
                }
                ,
                {
                    Constants.KeyName.RawBody, RawBody
                }
            };
        }

        public string Transformer { get; set; }
        public string Method { get; set; }
        public string BaseUrl { get; set; }
        public string Url { get; set; }

        public string EntityType { get; set; }

        //Might be a property of a Vocabulary
        public string TriggerPropeerty { get; set; }

        public string IngestionEndpoint { get; set; }

        public string ApiToken { get; set; }

        public string RawBody { get; set; }
    }
}
