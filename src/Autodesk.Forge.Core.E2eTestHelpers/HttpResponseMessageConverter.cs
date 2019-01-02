﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Autodesk.Forge.Core.E2eTestHelpers
{
    internal class HttpResponeMessageConverter : JsonConverter
    {
        static readonly HashSet<string> UselessHeaders = new HashSet<string>()
        {
            "Cache-Control", "Content-Security-Policy", "Date", "Pragma", "Set-Cookie",
            "X-Frame-Options", "Connection", "Expires", "Via", "x-amz-apigw-id",
            "X-Amz-Cf-Id", "x-amzn-RequestId", "X-Amzn-Trace-Id", "X-Cache"
        };

        public override bool CanConvert(Type objectType)
        {
            return typeof(HttpResponseMessage).IsAssignableFrom(objectType);
        }
        private static HttpContent DeserializeContent(JObject jsonContent)
        {
            if (jsonContent == null)
            {
                return new StringContent(string.Empty);
            }
            var content = new StringContent(jsonContent["Body"].ToString());
            content.Headers.Clear();
            foreach (var header in ((JObject)jsonContent["Headers"]).Properties())
            {
                content.Headers.TryAddWithoutValidation(header.Name, header.Value.Value<string>());
            }
            return content;
        }

        private static void DeserializeHeaders(HttpHeaders headers, JObject container)
        {
            headers.Clear();
            var headersToken = (JObject)container["Headers"];
            if (headersToken != null)
            {
                foreach (var header in headersToken.Properties())
                {
                    headers.Add(header.Name, header.Value.Value<string>());
                }
            }
        }
        private static HttpRequestMessage DeserializeRequest(JObject json)
        {
            var msg = new HttpRequestMessage();
            msg.Method = new HttpMethod(json["Method"].Value<string>());
            msg.RequestUri = new Uri(json["RequestUri"].Value<string>());
            DeserializeHeaders(msg.Headers, json);
            msg.Content = DeserializeContent((JObject)json["Content"]);
            return msg;
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var json = JObject.ReadFrom(reader);
            var msg = new HttpResponseMessage();
            msg.StatusCode = (HttpStatusCode)json["StatusCode"].Value<int>();
            msg.RequestMessage = DeserializeRequest((JObject)json["Request"]);
            msg.Content = DeserializeContent((JObject)json["Content"]);
            DeserializeHeaders(msg.Headers, (JObject)json);
            return msg;
        }
        
        private static void SerializeHeaders(JObject container, HttpHeaders headers)
        {
            var jsonHeaders = new JObject();
            foreach (var h in headers)
            {
                if (!UselessHeaders.Contains(h.Key))
                {
                    if (h.Key == "Authorization")
                    {
                        jsonHeaders.Add(h.Key, "***");
                    }
                    else
                    {
                        jsonHeaders.Add(h.Key, string.Join(";", h.Value));
                    }
                }
            }
            if (jsonHeaders.Count > 0)
            {
                container.Add("Headers", jsonHeaders);
            }
        }
        private static void SerializeContent(JObject container, HttpContent content)
        {
            var jsonContent = new JObject();
            if (content != null)
            {
                var mediaType = content.Headers.ContentType?.MediaType;
                if (mediaType == "application/json")
                {
                    var str = content.ReadAsStringAsync().Result;
                    var body = JToken.Parse(str);
                    if (body.Type == JTokenType.String)
                    {
                        jsonContent.Add("Body", str);
                    }
                    else
                    {
                        jsonContent.Add("Body", body);
                    }
                }
                else if (mediaType == "application/x-www-form-urlencoded")
                {
                    var str = content.ReadAsStringAsync().Result;
                    jsonContent.Add("Body", str);
                }
                else if (mediaType == null)
                {

                }
                else
                {
                    throw new JsonSerializationException("Unknown media type.");
                }
                if (jsonContent.Count > 0)
                {
                    SerializeHeaders(jsonContent, content.Headers);
                }
            }
            if (jsonContent.Count > 0)
            {
                container.Add("Content", jsonContent);
            }
        }
        private static JObject SerializeRequest(HttpRequestMessage msg)
        {
            var json = new JObject();
            json.Add("Method", msg.Method.Method);
            json.Add("RequestUri", msg.RequestUri.ToString());
            SerializeHeaders(json, msg.Headers);
            SerializeContent(json, msg.Content);
            return json;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var json = new JObject();
            var msg = (HttpResponseMessage)value;
            json.Add("StatusCode", (int)msg.StatusCode);
            SerializeHeaders(json, msg.Headers);
            SerializeContent(json, msg.Content);
            json.Add("Request", SerializeRequest(msg.RequestMessage));
            serializer.Serialize(writer, json);
        }
    }
}