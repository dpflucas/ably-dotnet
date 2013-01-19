﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Ably.Tests
{
    public abstract class RestApiTests
    {
        protected const string ValidKey = "AHSz6w:uQXPNQ:FGBZbsKSwqbCpkob";
        protected AblyRequest _currentRequest;
        protected MimeTypes mimeTypes = new MimeTypes();
        
        protected Rest GetRestClient()
        {
            var rest = new Rest(ValidKey);
        
            rest.ExecuteRequest = x => { _currentRequest = x; return (AblyResponse)null; };
            return rest;
        }

    }
    public class TimeTests : RestApiTests
    {
        [Fact]
        public void Time_ShouldSendGetRequestToCorrectPathWithCorrectHeaders()
        {
            var rest = GetRestClient();
            rest.Time();
            
            Assert.Equal("/time", _currentRequest.Path);
            Assert.Equal(HttpMethod.Get, _currentRequest.Method);
            _currentRequest.AssertContainsHeader(Headers.Accept, mimeTypes["json"]);
        }
    }

    public static class Headers
    {
        public const string Accept = "Accept";
        public const string ContentType = "Content-Type";
    }

    public static class TestHelpers
    {
        public static void AssertContainsHeader(this AblyRequest request, string key, string value)
        {
            Assert.True(request.Headers.ContainsKey(key), 
                String.Format("Header '{0}' doesn't exist in request", key));
            Assert.Equal(value, request.Headers[key]);
        }
    }
}