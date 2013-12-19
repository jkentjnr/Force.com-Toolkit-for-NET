﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Text;
using ForceSDKforNET.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForceSDKforNET
{
    public class ForceClient
    {
        public ForceClient()
        {
            ApiVersion = "v29.0";
        }

        public ForceClient(string clientId, string clientSecret, string username, string password)
            : this()
        {
            Authenticate(clientId, clientSecret, username, password).Wait();
        }

        public string ApiVersion { get; set; }
        public string InstanceUrl { get; private set; }
        public string AccessToken { get; private set; }

        public async Task Authenticate(string clientId, string clientSecret, string username, string password)
        {
            var tokenRequestEndpointUrl = "https://login.salesforce.com/services/oauth2/token";
            var client = new HttpClient();

            var content = new FormUrlEncodedContent(new[] 
            {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password)
            });

            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(tokenRequestEndpointUrl),
                Content = content
            };

            var responseMessage = await client.SendAsync(request);
            var response = await responseMessage.Content.ReadAsStringAsync();

            if (responseMessage.IsSuccessStatusCode)
            {
                var authToken = JsonConvert.DeserializeObject<AuthToken>(response);

                AccessToken = authToken.access_token;
                InstanceUrl = authToken.instance_url;
            }
            else
            {
                var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(response);

                throw new ForceException(errorResponse.error, errorResponse.error_description);
            }
        }

        public async Task<IList<T>> Query<T>(string query)
        {

            var url = string.Format("{0}?q={1}", FormatUrl("query"), query);

            var client = new HttpClient();
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(url),
                Method = HttpMethod.Get
            };

            request.Headers.Add("Authorization", "Bearer " + AccessToken);

            var responseMessage = await client.SendAsync(request);
            var response = await responseMessage.Content.ReadAsStringAsync();

            if (responseMessage.IsSuccessStatusCode)
            {
                var jObject = JObject.Parse(response);
                var jToken = jObject.GetValue("records");

                var r = JsonConvert.DeserializeObject<IList<T>>(jToken.ToString());
                return r;
            }
            
            var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(response);
            throw new ForceException(errorResponse.error, errorResponse.error_description);
        }

        public async Task<string> Create(string objectName, object record)
        {
            var url = FormatUrl("sobjects") + "/" + objectName;

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            var json = JsonConvert.SerializeObject(record);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var responseMessage = await client.PostAsync(url, content);
            var response = await responseMessage.Content.ReadAsStringAsync();

            if (responseMessage.IsSuccessStatusCode)
            {
                var id = JsonConvert.DeserializeObject<SuccessResponse>(response).id;
                return id;
            }
            
            var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(response);
            throw new ForceException(errorResponse.message, errorResponse.errorCode);
        }

        protected string FormatUrl(string resourceName)
        {
            return string.Format("{0}/services/data/{1}/{2}", InstanceUrl, ApiVersion, resourceName);
        }
    }
}
