﻿using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using IO.Ably.Realtime;
using IO.Ably.Realtime.Workflow;
using IO.Ably.Transport;
using Xunit;
using Xunit.Abstractions;

namespace IO.Ably.Tests.Realtime
{
    public class ConnectionAttemptsInfoSpecs : MockHttpRealtimeSpecs
    {
        private ConnectionAttemptsInfo Info => _state.AttemptsInfo;

        private RealtimeState _state = new RealtimeState();

        [Fact]
        public void Reset_SHouldResetFirstAttemptAndNumberOfAttempts()
        {
            Info.Reset();
            Info.FirstAttempt.Should().NotHaveValue();
            Info.NumberOfAttempts.Should().Be(0);
        }

        [Fact]
        public void ShouldSuspend_WhenConnectionHasNotBeenAttempted_ShouldReturnFalse()
        {
            _state.ShouldSuspend().Should().BeFalse();
        }

        [Fact]
        public void ShouldSuspend_WhenFirstAttemptIsWithinConnectionStateTtl_ShouldReturnFalse()
        {
            SetNowFunc(() => DateTimeOffset.UtcNow);
            Info.Attempts.Add(new ConnectionAttempt(Now));

            // Move now to default ConnetionStatettl - 1 second
            NowAdd(Defaults.ConnectionStateTtl.Add(TimeSpan.FromSeconds(-1)));
            _state.ShouldSuspend().Should().BeFalse();
            SetNowFunc(() => DateTimeOffset.UtcNow);
        }

        [Fact]
        public void ShouldSuspend_WhenFirstAttemptEqualOrGreaterThanConnectionStateTtl_ShouldReturnTrue()
        {
            var currentDate = DateTimeOffset.UtcNow;
            Func<DateTimeOffset> now = () => currentDate;

            // We want access to the modified closure so we can manipulate time within ConnectionAttemptsInfo
            // ReSharper disable once AccessToModifiedClosure
            DateTimeOffset NowWrapperFn() => now();
            var state = new RealtimeState(null, now);

            state.AttemptsInfo.Attempts.Add(new ConnectionAttempt(NowWrapperFn()));

            // Move now to default ConnetionStatettl - 1 second
            currentDate = DateTimeOffset.UtcNow.Add(Defaults.ConnectionStateTtl);
            state.ShouldSuspend(now).Should().BeTrue("When time is equal"); // =
            currentDate = DateTimeOffset.UtcNow.Add(Defaults.ConnectionStateTtl).AddSeconds(60);
            state.ShouldSuspend(now).Should().BeTrue("When time is greater than"); // >
        }

        [Fact]
        public async Task CanAttemptFallback_ShouldBeFalseWithNonDefaultHost()
        {
            var client = GetRealtime(opts => opts.RealtimeHost = "test.test.com");

            var result = await client.RestClient.CanFallback(null);
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(500)]
        [InlineData(501)]
        [InlineData(502)]
        [InlineData(503)]
        [InlineData(504)]
        public async Task CanAttemptFallback_WithDefaultHostAndAppropriateError_ShouldBeTrue(int httpcode)
        {
            var client = GetRealtime();

            var result = await client.RestClient.CanFallback(new ErrorInfo("test", 111, (HttpStatusCode)httpcode));
            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanAttemptFallback_WhenInternetCheckFails_ShouldBeFalse()
        {
            var client = GetRealtime(internetCheckOk: false);
            var result = await client.RestClient.CanFallback(null);
            result.Should().BeFalse();
        }

        public ConnectionAttemptsInfoSpecs(ITestOutputHelper output)
            : base(output)
        {
        }

        private AblyRealtime GetRealtime(Action<ClientOptions> optionsAction = null, bool internetCheckOk = true)
        {
            return GetRealtimeClient(
                request =>
            {
                if (request.Url == Defaults.InternetCheckUrl)
                {
                    return (internetCheckOk ? Defaults.InternetCheckOkMessage : "Blah").ToAblyResponse();
                }

                return DefaultResponse.ToTask();
            }, optionsAction);
        }
    }
}
