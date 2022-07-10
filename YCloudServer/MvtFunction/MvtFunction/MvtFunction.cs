using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;

namespace Function
{

    public class Tile
    {
        [JsonPropertyName("x")]
        public string X { get; set; }

        [JsonPropertyName("y")]
        public string Y { get; set; }

        [JsonPropertyName("z")]
        public string Z { get; set; }
    }

    public class Request
    {
        [JsonPropertyName("httpMethod")]
        public string HttpMethod { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("queryStringParameters")]
        public Tile QueryStringParameters { get; set; }
    }

    public class Response
    {
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("body")]
        public byte[] Body { get; set; }

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; }

        [JsonPropertyName("isBase64Encoded")]
        public bool IsBase64Encoded { get; set; }

    }

    public class Handler
    {
        public Response FunctionHandler(Request request)
        {
            var x = Convert.ToInt32(request.QueryStringParameters.X);
            var y = Convert.ToInt32(request.QueryStringParameters.Y);
            var z = Convert.ToInt32(request.QueryStringParameters.Z);
            y = (1 << z) - y - 1;

            string url = $"https://storage.yandexcloud.net/bucket-for-tiles/tiles/{z}/{x}/{y}.pbf";

            WebClient client = new WebClient();
            byte[] data = null;
            try
            {
                data = client.DownloadData(url);
            }
            catch
            {
                return new Response { StatusCode = 204 };
            }

            var tmp = new Dictionary<string, string>();
            tmp.Add("Content-Type", "application/vnd.mapbox-vector-tile");
            tmp.Add("Content-Encoding", "base64");

            return new Response { StatusCode = 200, Body = data, Headers = tmp, IsBase64Encoded = true };
        }
    }
}
