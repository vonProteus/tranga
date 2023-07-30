﻿using System.Net;
using Logging;

namespace Tranga;

internal class DownloadClient
    {
        private static readonly HttpClient Client = new()
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private readonly Dictionary<byte, DateTime> _lastExecutedRateLimit;
        private readonly Dictionary<byte, TimeSpan> _rateLimit;
        // ReSharper disable once InconsistentNaming
        private readonly Logger? logger;

        /// <summary>
        /// Creates a httpClient
        /// </summary>
        /// <param name="rateLimitRequestsPerMinute">Rate limits for requests. byte is RequestType, int maximum requests per minute for RequestType</param>
        /// <param name="logger"></param>
        public DownloadClient(Dictionary<byte, int> rateLimitRequestsPerMinute, Logger? logger)
        {
            this.logger = logger;
            _lastExecutedRateLimit = new();
            _rateLimit = new();
            foreach(KeyValuePair<byte, int> limit in rateLimitRequestsPerMinute)
                _rateLimit.Add(limit.Key, TimeSpan.FromMinutes(1).Divide(limit.Value));
        }

        /// <summary>
        /// Request Webpage
        /// </summary>
        /// <param name="url"></param>
        /// <param name="requestType">For RateLimits: Same Endpoints use same type</param>
        /// <param name="referrer">Used in http request header</param>
        /// <returns>RequestResult with StatusCode and Stream of received data</returns>
        public RequestResult MakeRequest(string url, byte requestType, string? referrer = null)
        {
            if (_rateLimit.TryGetValue(requestType, out TimeSpan value))
                _lastExecutedRateLimit.TryAdd(requestType, DateTime.Now.Subtract(value));
            else
            {
                logger?.WriteLine(this.GetType().ToString(), "RequestType not configured for rate-limit.");
                return new RequestResult(HttpStatusCode.NotAcceptable, Stream.Null);
            }

            TimeSpan rateLimitTimeout = _rateLimit[requestType]
                .Subtract(DateTime.Now.Subtract(_lastExecutedRateLimit[requestType]));
            
            if(rateLimitTimeout > TimeSpan.Zero)
                Thread.Sleep(rateLimitTimeout);

            HttpResponseMessage? response = null;
            while (response is null)
            {
                try
                {
                    HttpRequestMessage requestMessage = new(HttpMethod.Get, url);
                    if(referrer is not null)
                        requestMessage.Headers.Referrer = new Uri(referrer);
                    _lastExecutedRateLimit[requestType] = DateTime.Now;
                    response = Client.Send(requestMessage);
                }
                catch (HttpRequestException e)
                {
                    logger?.WriteLine(this.GetType().ToString(), e.Message);
                    logger?.WriteLine(this.GetType().ToString(), $"Waiting {_rateLimit[requestType] * 2}... Retrying.");
                    Thread.Sleep(_rateLimit[requestType] * 2);
                }
            }
            if (!response.IsSuccessStatusCode)
            {
                logger?.WriteLine(this.GetType().ToString(), $"Request-Error {response.StatusCode}: {response.ReasonPhrase}");
                return new RequestResult(response.StatusCode, Stream.Null);
            }

            // Request has been redirected to another page. For example, it redirects directly to the results when there is only 1 result
            if(response.RequestMessage is not null && response.RequestMessage.RequestUri is not null)
            {
                return new RequestResult(response.StatusCode, response.Content.ReadAsStream(), true, response.RequestMessage.RequestUri.AbsoluteUri);
            }

            return new RequestResult(response.StatusCode, response.Content.ReadAsStream());
        }

        public struct RequestResult
        {
            public HttpStatusCode statusCode { get; }
            public Stream result { get; }
            public bool HasBeenRedirected { get; }
            public string? RedirectedToUrl { get; }

            public RequestResult(HttpStatusCode statusCode, Stream result)
            {
                this.statusCode = statusCode;
                this.result = result;
            }

            public RequestResult(HttpStatusCode statusCode, Stream result, bool hasBeenRedirected, string redirectedTo)
                : this(statusCode, result)
            {
                this.HasBeenRedirected = hasBeenRedirected;
                RedirectedToUrl = redirectedTo;
            }
        }
    }