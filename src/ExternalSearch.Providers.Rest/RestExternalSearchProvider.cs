using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using CluedIn.Core;
using CluedIn.Core.Data;
using CluedIn.Core.Data.Parts;
using CluedIn.Core.Data.Relational;
using CluedIn.Core.ExternalSearch;
using CluedIn.Core.Providers;
using JUST;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using RestSharp;
//using RestSharp.Extensions.MonoHttp;
using EntityType = CluedIn.Core.Data.EntityType;

namespace CluedIn.ExternalSearch.Providers.Rest
{

    /// <summary>The VatLayer graph external search provider.</summary>
    /// <seealso cref="ExternalSearchProviderBase" />
    public class RestExternalSearchProvider : ExternalSearchProviderBase, IExtendedEnricherMetadata, IConfigurableExternalSearchProvider
    {
        public static EntityType[] AcceptedEntityTypes = { };
        /**********************************************************************************************************
        * CONSTRUCTORS
        **********************************************************************************************************/

        public RestExternalSearchProvider()
           : base(Constants.ProviderId, AcceptedEntityTypes)
        {
            var nameBasedTokenProvider = new NameBasedTokenProvider("Rest");

            if (nameBasedTokenProvider.ApiToken != null)
            {
                TokenProvider = new RoundRobinTokenProvider(
                    nameBasedTokenProvider.ApiToken.Split(',', ';'));
            }
        }

        public override bool Accepts(EntityType entityType)
        {        
            return false;
        }

        public RestExternalSearchProvider(IEnumerable<string> tokens)
            : this(true)
        {
            TokenProvider = new RoundRobinTokenProvider(tokens);
        }

        public RestExternalSearchProvider(IExternalSearchTokenProvider tokenProvider)
            : this(true)
        {
            TokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        private RestExternalSearchProvider(bool tokenProviderIsRequired)
            : this()
        {
            TokenProviderIsRequired = tokenProviderIsRequired;
        }

        /**********************************************************************************************************
         * METHODS
         **********************************************************************************************************/

        /// <summary>Builds the queries.</summary>
        /// <param name="context">The context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The search queries.</returns>
        public override IEnumerable<IExternalSearchQuery> BuildQueries(ExecutionContext context, IExternalSearchRequest request)
        {
            var apiToken = TokenProvider?.ApiToken;

            foreach (var externalSearchQuery in InternalBuildQueries(context, request, apiToken, null))
            {
                yield return externalSearchQuery;
            }
        }

        private IEnumerable<IExternalSearchQuery> InternalBuildQueries(ExecutionContext context, IExternalSearchRequest request, string apiToken, IDictionary<string, object> config)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (context.Log.BeginScope($"{GetType().Name} BuildQueries: request {request}"))
            {
                if (string.IsNullOrEmpty(apiToken))
                {
                    context.Log.LogError("ApiToken for VatLayer must be provided.");
                    yield break;
                }

                if (!Accepts(request.EntityMetaData.EntityType))
                {
                    context.Log.LogTrace("Unacceptable entity type from '{EntityName}', entity code '{EntityCode}'", request.EntityMetaData.DisplayName, request.EntityMetaData.EntityType.Code);

                    yield break;
                }

                context.Log.LogTrace("Starting to build queries for {EntityName}", request.EntityMetaData.DisplayName);

                var existingResults = request.GetQueryResults<RestResponse>(this).ToList();

                bool vatFilter(string value) => existingResults.Any(r => string.Equals(request.EntityMetaData.Properties[config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.TriggerProperty].ToString()], value, StringComparison.InvariantCultureIgnoreCase));

                var entityType = request.EntityMetaData.EntityType;
                var vatNumber = new HashSet<string>() { request.EntityMetaData.Properties[config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.TriggerProperty].ToString()] };
                if (!vatNumber.Any())
                {
                    context.Log.LogTrace("No query parameter for '{VatNumber}' in request, skipping build queries", Core.Data.Vocabularies.Vocabularies.CluedInOrganization.VatNumber);
                }
                else
                {
                    var filteredValues = vatNumber.Where(v => !vatFilter(v)).ToArray();

                    if (!filteredValues.Any())
                    {
                        context.Log.LogWarning("Filter removed all VAT numbers, skipping processing. Original '{Original}'", string.Join(",", vatNumber));
                    }
                    else
                    {
                        foreach (var value in filteredValues)
                        {
                            request.CustomQueryInput = vatNumber.ElementAt(0);
                           
                            context.Log.LogInformation("External search query produced, ExternalSearchQueryParameter: '{Identifier}' EntityType: '{EntityCode}' Value: '{SanitizedValue}'", ExternalSearchQueryParameter.Identifier, entityType.Code, "Some Value");

                            yield return new ExternalSearchQuery(this, entityType, ExternalSearchQueryParameter.Identifier, value);
                        }
                    }

                    context.Log.LogTrace("Finished building queries for '{Name}'", request.EntityMetaData.Name);
                }
            }
        }

        /// <summary>Executes the search.</summary>
        /// <param name="context">The context.</param>
        /// <param name="query">The query.</param>
        /// <returns>The results.</returns>
        public override IEnumerable<IExternalSearchQueryResult> ExecuteSearch(ExecutionContext context, IExternalSearchQuery query)
        {
            var apiToken = TokenProvider?.ApiToken;

            foreach (var externalSearchQueryResult in InternalExecuteSearch(context, query, apiToken, null))
            {
                yield return externalSearchQueryResult;
            }
        }

        private IEnumerable<IExternalSearchQueryResult> InternalExecuteSearch(ExecutionContext context, IExternalSearchQuery query, string apiToken, IDictionary<string, object> config)
        {
            if (string.IsNullOrEmpty(apiToken))
            {
                throw new InvalidOperationException("ApiToken for Ingestion Endpoint must be provided.");
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            using (context.Log.BeginScope("{0} {1}: query {2}", GetType().Name, "ExecuteSearch", query))
            {
                context.Log.LogTrace("Starting external search for Id: '{Id}' QueryKey: '{QueryKey}'", query.Id, query.QueryKey);

                var vat = query.QueryParameters[ExternalSearchQueryParameter.Identifier].FirstOrDefault();

                if (string.IsNullOrEmpty(vat))
                {
                    context.Log.LogTrace("No parameter for '{Identifier}' in query, skipping execute search", ExternalSearchQueryParameter.Identifier);
                }
                else
                {

                    var client = new RestClient(config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.BaseUrl].ToString());
                    var request = new RestRequest(config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.Url].ToString());
                    var method = config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.Method].ToString();
                    if (method == "GET")
                    {
                        request.Method = Method.GET;
                    }
                    else
                    {
                        request.Method = Method.POST;
                        request.AddJsonBody(config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.Method].ToString());
                    }

                    var response = client.Execute(request);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        if (response.Content != null)
                        {
                            //var transformJson = JsonUtility.Serialize(response.Content);
                            var transformedString = response.Content;
                            if (!string.IsNullOrEmpty(config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.Method].ToString()))
                            {
                                transformedString = JsonTransformer.Transform(config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.Transformer].ToString(), response.Content);
                            }                       

                            yield return new ExternalSearchQueryResult<string>(query, transformedString);
                        }
                    }
                    else if (response.StatusCode == HttpStatusCode.NoContent ||
                             response.StatusCode == HttpStatusCode.NotFound)
                    {
                        var diagnostic =
                            $"External search for Id: '{query.Id}' QueryKey: '{query.QueryKey}' produced no results - StatusCode: '{response.StatusCode}' Content: '{response.Content}'";

                        context.Log.LogWarning(diagnostic);

                        yield break;
                    }
                    else if (response.ErrorException != null)
                    {
                        var diagnostic =
                            $"External search for Id: '{query.Id}' QueryKey: '{query.QueryKey}' produced no results - StatusCode: '{response.StatusCode}' Content: '{response.Content}'";

                        context.Log.LogError(diagnostic, response.ErrorException);

                        throw new AggregateException(response.ErrorException.Message, response.ErrorException);
                    }
                    else
                    {
                        var diagnostic =
                            $"Failed external search for Id: '{query.Id}' QueryKey: '{query.QueryKey}' - StatusCode: '{response.StatusCode}' Content: '{response.Content}'";

                        context.Log.LogError(diagnostic);

                        throw new ApplicationException(diagnostic);
                    }

                    context.Log.LogTrace("Finished external search for Id: '{Id}' QueryKey: '{QueryKey}'", query.Id, query.QueryKey);
                }
            }
        }

        /// <summary>Builds the clues.</summary>
        /// <param name="context">The context.</param>
        /// <param name="query">The query.</param>
        /// <param name="result">The result.</param>
        /// <param name="request">The request.</param>
        /// <returns>The clues.</returns>
        public override IEnumerable<Clue> BuildClues(ExecutionContext context,
            IExternalSearchQuery query,
            IExternalSearchQueryResult result,
            IExternalSearchRequest request)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (context.Log.BeginScope("{0} {1}: query {2}, request {3}, result {4}", GetType().Name, "BuildClues", query, request, result))
            {
                var resultItem = result.As<string>();
                var dirtyClue = request.CustomQueryInput.ToString();
                var code = GetOriginEntityCode(resultItem);
                var clue = new Clue(code, context.Organization);
                if (!string.IsNullOrEmpty(dirtyClue))
                    clue.Data.EntityData.Codes.Add(new EntityCode(EntityType.Organization, CodeOrigin.CluedIn.CreateSpecific("vatlayer"), dirtyClue));
                //PopulateMetadata(clue.Data.EntityData, resultItem);

                context.Log.LogInformation("Clue produced, Id: '{Id}' OriginEntityCode: '{OriginEntityCode}' RawText: '{RawText}'", clue.Id, clue.OriginEntityCode, clue.RawText);

                return new[] { clue };
            }
        }


        public IEnumerable<Clue> InternalBuildClues(ExecutionContext context,
           IExternalSearchQuery query,
           IExternalSearchQueryResult result,
           IExternalSearchRequest request,
            IDictionary<string, object> config, IProvider provider)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (context.Log.BeginScope("{0} {1}: query {2}, request {3}, result {4}", GetType().Name, "BuildClues", query, request, result))
            {
                var resultItem = result.As<string>();
                var dirtyClue = request.CustomQueryInput.ToString();
                var code = GetOriginEntityCode(resultItem);
                var clue = new Clue(code, context.Organization);
                if (!string.IsNullOrEmpty(dirtyClue))
                    clue.Data.EntityData.Codes.Add(new EntityCode(EntityType.Organization, CodeOrigin.CluedIn.CreateSpecific("vatlayer"), dirtyClue));
                //PopulateMetadata(clue.Data.EntityData, resultItem);

                context.Log.LogInformation("Clue produced, Id: '{Id}' OriginEntityCode: '{OriginEntityCode}' RawText: '{RawText}'", clue.Id, clue.OriginEntityCode, clue.RawText);

                return new[] { clue };
            }
        }


        /// <summary>Gets the primary entity metadata.</summary>
        /// <param name="context">The context.</param>
        /// <param name="result">The result.</param>
        /// <param name="request">The request.</param>
        /// <returns>The primary entity metadata.</returns>
        public override IEntityMetadata GetPrimaryEntityMetadata(ExecutionContext context,
            IExternalSearchQueryResult result,
            IExternalSearchRequest request)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (context.Log.BeginScope("{0} {1}: request {2}, result {3}", GetType().Name, "GetPrimaryEntityMetadata", request, result))
            {
                var metadata = CreateMetadata(result.As<string>(), null);

                context.Log.LogInformation("Primary entity meta data created, Name: '{Name}' OriginEntityCode: '{OriginEntityCode}'", metadata.Name, metadata.OriginEntityCode.Origin.Code);

                return metadata;
            }
        }

        public IEntityMetadata InternalGetPrimaryEntityMetadata(ExecutionContext context,
          IExternalSearchQueryResult result,
          IExternalSearchRequest request,
          IDictionary<string, object> config, IProvider provider)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (context.Log.BeginScope("{0} {1}: request {2}, result {3}", GetType().Name, "GetPrimaryEntityMetadata", request, result))
            {
                var metadata = CreateMetadata(result.As<string>(), config);

                context.Log.LogInformation("Primary entity meta data created, Name: '{Name}' OriginEntityCode: '{OriginEntityCode}'", metadata.Name, metadata.OriginEntityCode.Origin.Code);

                return metadata;
            }
        }

        /// <summary>Gets the preview image.</summary>
        /// <param name="context">The context.</param>
        /// <param name="result">The result.</param>
        /// <param name="request">The request.</param>
        /// <returns>The preview image.</returns>
        public override IPreviewImage GetPrimaryEntityPreviewImage(ExecutionContext context, IExternalSearchQueryResult result, IExternalSearchRequest request)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            using (context.Log.BeginScope("{0} {1}: request {2}, result {3}", GetType().Name, "GetPrimaryEntityPreviewImage", request, result))
            {
                context.Log.LogInformation("Primary entity preview image not produced, returning null");

                return null;
            }
        }

        public IPreviewImage InternalGetPrimaryEntityPreviewImage(ExecutionContext context, IExternalSearchQueryResult result, IExternalSearchRequest request, IDictionary<string, object> config, IProvider provider)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            using (context.Log.BeginScope("{0} {1}: request {2}, result {3}", GetType().Name, "GetPrimaryEntityPreviewImage", request, result))
            {
                context.Log.LogInformation("Primary entity preview image not produced, returning null");

                return null;
            }
        }

        private static IEntityMetadata CreateMetadata(IExternalSearchQueryResult<string> resultItem, IDictionary<string, object> config)
        {
            var metadata = new EntityMetadataPart();

            PopulateMetadata(config, metadata, resultItem);

            return metadata;
        }

        private static EntityCode GetOriginEntityCode(IExternalSearchQueryResult<string> resultItem)
        {
            return new EntityCode(EntityType.Organization, GetCodeOrigin(), resultItem.Data.ToString());
        }

        private static CodeOrigin GetCodeOrigin()
        {
            return CodeOrigin.CluedIn.CreateSpecific("vatlayer");
        }

        private static void PopulateMetadata(IDictionary<string, object> config, IEntityMetadata metadata, IExternalSearchQueryResult<string> resultItem)
        {
            //Post to the Endpoints Instead
            PostToCluedIn(config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.IngestionBaseEndpoint].ToString(), config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.IngestionEndpoint].ToString(), config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.ApiToken].ToString(), resultItem.Data);
        }

        public static void PostToCluedIn(string ingestionBaseEndpoint, string ingestionEndpoint, string apiToken, string data)
        {
            var client = new RestClient(ingestionBaseEndpoint);
            var restRequest = new RestRequest(ingestionEndpoint, Method.POST);
            restRequest.AddHeader("Content-Type", "application/json");
            restRequest.AddHeader("Authorization", "Bearer " + apiToken);
            //Make sure it is posting an array
            restRequest.AddJsonBody(data);
            var response =  client.Execute(restRequest);

        }

        public IEnumerable<EntityType> Accepts(IDictionary<string, object> config, IProvider provider)
        {

            AcceptedEntityTypes = new EntityType[] { config[CluedIn.ExternalSearch.Providers.Rest.Constants.KeyName.EntityType].ToString() };

            return AcceptedEntityTypes;
        }

        public IEnumerable<IExternalSearchQuery> BuildQueries(ExecutionContext context, IExternalSearchRequest request, IDictionary<string, object> config, IProvider provider)
        {
            var jobData = new RestExternalSearchJobData(config);

            foreach (var externalSearchQuery in InternalBuildQueries(context, request, jobData.Transformer, config))
            {
                yield return externalSearchQuery;
            }
        }

        public IEnumerable<IExternalSearchQueryResult> ExecuteSearch(ExecutionContext context, IExternalSearchQuery query, IDictionary<string, object> config, IProvider provider)
        {
            var jobData = new RestExternalSearchJobData(config);

            foreach (var externalSearchQueryResult in InternalExecuteSearch(context, query, jobData.Transformer, config))
            {
                yield return externalSearchQueryResult;
            }
        }

        public IEnumerable<Clue> BuildClues(ExecutionContext context, IExternalSearchQuery query, IExternalSearchQueryResult result, IExternalSearchRequest request, IDictionary<string, object> config, IProvider provider)
        {
            return InternalBuildClues(context, query, result, request, config, provider);
        }

        public IEntityMetadata GetPrimaryEntityMetadata(ExecutionContext context, IExternalSearchQueryResult result, IExternalSearchRequest request, IDictionary<string, object> config, IProvider provider)
        {
            return InternalGetPrimaryEntityMetadata(context, result, request, config, provider);
        }

        public IPreviewImage GetPrimaryEntityPreviewImage(ExecutionContext context, IExternalSearchQueryResult result, IExternalSearchRequest request, IDictionary<string, object> config, IProvider provider)
        {
            return InternalGetPrimaryEntityPreviewImage(context, result, request, config, provider);
        }

        public string Icon { get; } = Constants.Icon;
        public string Domain { get; } = Constants.Domain;
        public string About { get; } = Constants.About;

        public AuthMethods AuthMethods { get; } = Constants.AuthMethods;
        public IEnumerable<Control> Properties { get; } = Constants.Properties;
        public Guide Guide { get; } = Constants.Guide;
        public IntegrationType Type { get; } = Constants.IntegrationType;

        
    }
}
