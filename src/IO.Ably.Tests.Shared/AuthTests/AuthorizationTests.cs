using IO.Ably.Realtime;
using IO.Ably.Tests.Realtime;
using IO.Ably.Types;

namespace IO.Ably.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;
    using Xunit.Abstractions;

    [Collection("UnitTests")]
    public class AuthorizationTests : MockHttpRestSpecs
    {
        internal AblyResponse DummyTokenResponse = new AblyResponse()
        {
            Type = ResponseType.Json, TextResponse = "{ \"access_token\": {}}"
        };

        internal override AblyResponse DefaultResponse => DummyTokenResponse;

        protected static string KeyId => ValidKey.Split(':')[0];

        private async Task<TokenRequest> CreateTokenRequest(
            AblyRest client,
            TokenParams @params = null,
            AuthOptions options = null)
        {
            return JsonHelper.Deserialize<TokenRequest>(await client.Auth.CreateTokenRequestAsync(@params, options));
        }

        public class General : AuthorizationTests
        {
            [Fact]
            [Trait("spec", "RSA1")]
            public async Task WithTlsFalseAndBasicAuth_Throws()
            {
                var client = GetRestClient(setOptionsAction: options => { options.Tls = false; });
                await Assert.ThrowsAsync<InsecureRequestException>(() => client.Auth.AuthorizeAsync());
            }

            [Fact]
            [Trait("spec", "RSA1")]
            public async Task WithTlsTrueAndBasicAuth_ShouldWork()
            {
                var client = GetRestClient(setOptionsAction: options => { options.Tls = true; });
                await client.Auth.AuthorizeAsync();

                // Success
            }

            private static TokenRequest CreateDefaultTokenRequest(AblyRest client)
            {
                return JsonHelper.Deserialize<TokenRequest>(client.Auth.CreateTokenRequestAsync(null, null).Result);
            }

            [Fact]
            public void UsesKeyIdFromTheClient()
            {
                var client = GetRestClient();
                var data = CreateDefaultTokenRequest(client);
                data.KeyName.Should().Be(client.Options.ParseKey().KeyName);
            }

            [Fact]
            [Trait("spec", "RSA5")]
            public void UsesDefaultTtlWhenNoneIsSpecified()
            {
                var client = GetRestClient();
                var data = CreateDefaultTokenRequest(client);
                data.Ttl.Should().Be(Defaults.DefaultTokenTtl);
            }

            [Fact]
            [Trait("spec", "RSA6")]
            public void UsesTheDefaultCapability()
            {
                var client = GetRestClient();
                var data = CreateDefaultTokenRequest(client);
                data.Capability.Should().Be(Defaults.DefaultTokenCapability);
            }

            [Fact]
            public void UsesUniqueNonseWhichIsMoreThan16Characters()
            {
                var client = GetRestClient();
                var data = CreateDefaultTokenRequest(client);
                var secondTime = CreateDefaultTokenRequest(client);
                data.Nonce.Should().NotBe(secondTime.Nonce);
                data.Nonce.Length.Should().BeGreaterOrEqualTo(16);
            }

            [Fact]
            [Trait("spec", "RSA14")]
            public async Task WithNoTokenOrWayToGenerateOneAndUseTokenAuthIsTrue_AuthorizeShouldThrow()
            {
                var client = GetRestClient(setOptionsAction: options =>
                {
                    options.Key = string.Empty;
                    options.UseTokenAuth = true;
                });

                var ex = await Assert.ThrowsAsync<AblyException>(() => client.Auth.AuthorizeAsync());
                ex.ErrorInfo.Message.Should().Be("TokenAuth is on but there is no way to generate one");
            }

            public General(ITestOutputHelper output)
                : base(output)
            {
            }
        }

        [Trait("spec", "RSA9a")]
        [Trait("spec", "RSA9b")]
        [Trait("spec", "RSA9i")]
        public class CreateTokenRequestAuthOptionSpecs : AuthorizationTests
        {
            public CreateTokenRequestAuthOptionSpecs(ITestOutputHelper output)
                : base(output)
            {
            }

            private AblyRest GetClientWithTokenParams()
            {
                return GetRestClient(null, options =>
                {
                    options.DefaultTokenParams = new TokenParams()
                    {
                        Ttl = TimeSpan.FromHours(2),
                        Capability = Capability.AllowAll,
                        ClientId = "123",
                        Timestamp = Now.AddMinutes(1),
                        Nonce = "defaultnonce"
                    };
                });
            }

            [Fact]
            public async Task WithDefaultTokenParams_ShouldSetTokenRequestValuesCorrectly()
            {
                var client = GetClientWithTokenParams();

                var request = await CreateTokenRequest(client);
                request.Capability.Should().Be(Capability.AllowAll);
                request.ClientId.Should().Be("123");
                request.KeyName.Should().Be(ApiKey.Parse(client.Options.Key).KeyName);
                request.Ttl.Should().Be(TimeSpan.FromHours(2));
                request.Timestamp.Value.Should().BeCloseTo(Now.AddMinutes(1));
                request.Nonce.Should().Be("defaultnonce");
            }

            [Fact]
            public async Task WithOverrideTokenParams_ShouldSetTokenRequestValuesCorrectly()
            {
                var client = GetClientWithTokenParams();

                var overridingTokenParams = new TokenParams()
                {
                    Ttl = TimeSpan.FromHours(1),
                    ClientId = "999",
                    Capability = new Capability(),
                    Nonce = "overrideNonce",
                    Timestamp = Now.AddMinutes(10)
                };

                var request = await CreateTokenRequest(client, overridingTokenParams);

                request.Capability.Should().Be(Capability.Empty);
                request.ClientId.Should().Be("999");
                request.Ttl.Should().Be(TimeSpan.FromHours(1));
                request.Timestamp.Value.Should().BeCloseTo(Now.AddMinutes(10), 500);
                request.Nonce.Should().Be("overrideNonce");
            }

            [Fact]
            public async Task WithOverrideAuthOptions_ShouldSetTokenRequestValuesCorrectly()
            {
                var client = GetClientWithTokenParams();

                var overrideAuthOptions = new AuthOptions()
                {
                    Key = "keyid.name:secret",
                };

                var request = await CreateTokenRequest(client, null, overrideAuthOptions);

                request.KeyName.Should().Be("keyid.name");
            }
        }

        public sealed class CreateTokenRequestSpecs : AuthorizationTests
        {
            [Fact]
            [Trait("spec", "RSA9h")]
            public async Task ReturnsASignedTokenRequest()
            {
                var request = await CreateTokenRequest(Client);
                request.Should().NotBeNull();
                request.Mac.Should().NotBeEmpty();
            }

            [Fact]
            [Trait("spec", "RSA9c")]
            public async Task WithEmptyNonce_ShouldGenerateANonceWithMoreThan16Characters()
            {
                var request = await CreateTokenRequest(Client);
                request.Nonce.Length.Should().BeGreaterThan(16, "Nonce should be more than 16 characters");
            }

            [Fact]
            [Trait("spec", "RSA9d")]
            public async Task WithNoTimeStapmInRequest_ShouldUseSystemType()
            {
                var request = await CreateTokenRequest(Client);
                request.Timestamp.Value.Should().BeCloseTo(Now, 500);
            }

            [Fact]
            [Trait("spec", "RSA9d")]
            public async Task WithTimeStampOverridesDefault()
            {
                var date = new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero);
                var data = await CreateTokenRequest(Client, new TokenParams() { Timestamp = date }, null);
                data.Timestamp.Should().Be(date);
            }

            [Fact]
            [Trait("spec", "RSA9d")]
            public async Task WithQueryTimeQueriesForTimestamp()
            {
                var currentTime = TestHelpers.Now();
                var client = GetRestClient(x =>
                    ("[" + currentTime.ToUnixTimeInMilliseconds() + "]").ToAblyJsonResponse());
                var authOptions = client.AblyAuth.CurrentAuthOptions;
                authOptions.QueryTime = true;
                var data = await CreateTokenRequest(client, null, authOptions);
                data.Timestamp.Should().BeCloseTo(currentTime);
            }

            [Fact]
            [Trait("spec", "RSA9e")]
            public async Task WithTtlOverridesDefault()
            {
                var data = await CreateTokenRequest(Client, new TokenParams { Ttl = TimeSpan.FromHours(2) }, null);

                data.Ttl.Should().Be(TimeSpan.FromHours(2));
            }

            [Fact]
            [Trait("spec", "RSA9f")]
            public async Task WithCapabilitySpecifiedInTokenParams_ShouldPassTheJsonStringToRequest()
            {
                var capability = new Capability();
                capability.AddResource("a").AllowAll();
                var customParams = new TokenParams() { Capability = capability };
                var request = await CreateTokenRequest(Client, customParams);
                request.Capability.Should().Be(capability);
            }

            [Fact]
            [Trait("spec", "RSA9g")]
            public async Task GeneratesHMac()
            {
                var data = await CreateTokenRequest(Client);
                data.Mac.Should().NotBeEmpty();
            }

            [Fact]
            public async Task WithNonceOverridesDefault()
            {
                var data = await CreateTokenRequest(Client, new TokenParams() { Nonce = "Blah" }, null);
                data.Nonce.Should().Be("Blah");
            }

            [Fact]
            public async Task WithClientIdOverridesDefault()
            {
                var data = await CreateTokenRequest(Client, new TokenParams() { ClientId = "123" }, null);
                data.ClientId.Should().Be("123");
            }

            [Fact]
            public async Task WithOutKeyIdThrowsException()
            {
                var client = new AblyRest(new ClientOptions() { UseTokenAuth = true });
                await Assert.ThrowsAsync<AblyException>(() => client.Auth.CreateTokenRequestAsync(null, null));
            }

            [Fact]
            public async Task WithOutKeyValueThrowsException()
            {
                var client = new AblyRest(new ClientOptions() { Key = "111.222" });
                await Assert.ThrowsAsync<AblyException>(() => client.Auth.CreateTokenRequestAsync(null, null));
            }

            public CreateTokenRequestSpecs(ITestOutputHelper output)
                : base(output)
            {
                Client = GetRestClient();
            }

            public AblyRest Client { get; }
        }

        public class ClientIdSpecs : AuthorizationTests
        {
            private string _clientId = "123";

            public ClientIdSpecs(ITestOutputHelper output)
                : base(output)
            {
            }

            private AblyRest GetRestClientWithClientId()
            {
                return GetRestClient(null, options => options.ClientId = _clientId);
            }

            [Fact]
            [Trait("spec", "RSA7a1")]
            [Trait("spec", "RSL1g1a")]
            public void ShouldNotPassClientIdWithPublishedMessage()
            {
                var client = GetRestClientWithClientId();

                client.Channels["test"].PublishAsync("boo", "baa");

                var message = (LastRequest.PostData as IEnumerable<Message>).First();

                message.ClientId.Should().BeNullOrEmpty();
            }

            [Fact]
            [Trait("spec", "RSA7a4")]
            public void WithClientIdAndDefaultOptionsParamsClientId_UsesOptionsClientIdWhenMakingTokenRequests()
            {
                var client = GetRestClient(null, options =>
                {
                    options.ClientId = "123";
                    options.DefaultTokenParams = new TokenParams() { ClientId = "999" };
                });

                client.Auth.AuthorizeAsync();
                var tokenRequest = LastRequest.PostData as TokenRequest;
                tokenRequest.ClientId.Should().Be("123");
            }

            [Fact]
            [Trait("spec", "RSA12b")]
            public void WhenNoClientIdIsSpecified_AuthClientIdShouldBeNull()
            {
                var client = GetRestClient();
                client.AblyAuth.ClientId.Should().BeNullOrEmpty();
            }

            [Fact]
            [Trait("spec", "RSC17")]
            [Trait("spec", "RSA7b1")]
            public void WhenClientIdInOptions_ShouldPassClientIdtoAblyAuth()
            {
                var options = new ClientOptions(ValidKey) { ClientId = "123" };
                var client = new AblyRest(options);
                client.AblyAuth.ClientId.Should().Be(options.ClientId);
            }

            [Fact]
            [Trait("spec", "RSA7b4")]
            public void WhenClientIsInitialisedWithTokenDetails_AuthClientIdShouldBeTheSame()
            {
                var options = new ClientOptions() { TokenDetails = new TokenDetails() { ClientId = "*" } };
                var client = new AblyRest(options);
                client.AblyAuth.ClientId.Should().Be("*");
            }

            [Fact]
            [Trait("spec", "RSA7b3")]
            public async Task WhenConnectedMessageContainsClientId_AuthClientIdShouldBeTheSame()
            {
                // Arrange
                var options = new ClientOptions(ValidKey) { TransportFactory = new FakeTransportFactory(), SkipInternetCheck = true };
                var realtime = new AblyRealtime(options);
                var clientId = "testId";

                // Act
                realtime.FakeProtocolMessageReceived(new ProtocolMessage(ProtocolMessage.MessageAction.Connected)
                {
                    ConnectionDetails = new ConnectionDetails { ClientId = clientId },
                });

                await realtime.WaitForState(ConnectionState.Connected);

                // Assert
                realtime.Auth.ClientId.Should().Be(clientId);
            }

            [Fact]
            [Trait("spec", "RSA7c")]
            public void ClientIdInClientOptionsCannotBeWildCard()
            {
                Assert.Throws<InvalidOperationException>(() => new ClientOptions() { ClientId = "*" });
            }
        }

        public AuthorizationTests(ITestOutputHelper output)
            : base(output)
        {
        }
    }
}
