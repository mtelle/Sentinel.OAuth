﻿namespace Sentinel.Tests.Unit
{
    using System;
    using System.Linq;

    using NUnit.Framework;

    using Sentinel.OAuth.Core.Interfaces.Repositories;
    using Sentinel.OAuth.Core.Models.OAuth;
    using Sentinel.OAuth.Implementation;

    [TestFixture]
    [Category("Unit")]
    public class MemoryTokenRepositoryTests
    {
        private ITokenRepository tokenRepository;

        [SetUp]
        public void SetUp()
        {
            this.tokenRepository = new MemoryTokenRepository();
        }

        [Test]
        public async void InsertAndGet_WhenGivenValidAuthorizationCodes_ReturnsAuthorizationCodes()
        {
            await this.tokenRepository.InsertAuthorizationCode(new AuthorizationCode("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit", RedirectUri = "http://localhost", Subject = "Username"});
            await this.tokenRepository.InsertAuthorizationCode(new AuthorizationCode("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit2", RedirectUri = "http://localhost", Subject = "Username" });

            var authorizationCodes = await this.tokenRepository.GetAuthorizationCodes(x => x.ClientId == "NUnit" && x.RedirectUri == "http://localhost");

            Assert.AreEqual(1, authorizationCodes.Count());
        }

        [Test]
        public async void InsertAndDelete_WhenGivenValidAuthorizationCodes_ReturnsTrue()
        {
            var code1 = await this.tokenRepository.InsertAuthorizationCode(new AuthorizationCode("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit", RedirectUri = "http://localhost", Subject = "Username" });
            var code2 = await this.tokenRepository.InsertAuthorizationCode(new AuthorizationCode("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit2", RedirectUri = "http://localhost", Subject = "Username" });

            var deleteResult = await this.tokenRepository.DeleteAuthorizationCode(code1);
            var authorizationCodes = await this.tokenRepository.GetAuthorizationCodes(x => x.ClientId == "NUnit" && x.RedirectUri == "http://localhost");

            Assert.IsTrue(deleteResult);
            Assert.AreEqual(0, authorizationCodes.Count());
        }

        [Test]
        public async void InsertAndGet_WhenGivenValidAccessTokens_ReturnsAccessTokens()
        {
            await this.tokenRepository.InsertAccessToken(new AccessToken("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit", RedirectUri = "http://localhost"});
            await this.tokenRepository.InsertAccessToken(new AccessToken("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit2", RedirectUri = "http://localhost" });

            var accessTokens = await this.tokenRepository.GetAccessTokens(x => x.ClientId == "NUnit" && x.RedirectUri == "http://localhost");

            Assert.AreEqual(1, accessTokens.Count());
        }

        [Test]
        public async void InsertAndDelete_WhenGivenValidAccessTokens_ReturnsTrue()
        {
            var token1 = await this.tokenRepository.InsertAccessToken(new AccessToken("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit", RedirectUri = "http://localhost" });
            var token2 = await this.tokenRepository.InsertAccessToken(new AccessToken("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit2", RedirectUri = "http://localhost" });

            var deleteResult = await this.tokenRepository.DeleteAccessToken(token1);
            var accessTokens = await this.tokenRepository.GetAccessTokens(x => x.ClientId == "NUnit" && x.RedirectUri == "http://localhost");

            Assert.IsTrue(deleteResult);
            Assert.AreEqual(0, accessTokens.Count());
        }

        [Test]
        public async void InsertAndGet_WhenGivenValidRefreshTokens_ReturnsRefreshTokens()
        {
            await this.tokenRepository.InsertRefreshToken(new RefreshToken("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit", RedirectUri = "http://localhost" });
            await this.tokenRepository.InsertRefreshToken(new RefreshToken("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit2", RedirectUri = "http://localhost" });

            var accessTokens = await this.tokenRepository.GetRefreshTokens(x => x.ClientId == "NUnit" && x.RedirectUri == "http://localhost");

            Assert.AreEqual(1, accessTokens.Count());
        }

        [Test]
        public async void InsertAndDelete_WhenGivenValidRefreshTokens_ReturnsTrue()
        {
            var token1 = await this.tokenRepository.InsertRefreshToken(new RefreshToken("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit", RedirectUri = "http://localhost" });
            var token2 = await this.tokenRepository.InsertRefreshToken(new RefreshToken("123456789", DateTime.UtcNow.AddMinutes(1)) { ClientId = "NUnit2", RedirectUri = "http://localhost" });

            var deleteResult = await this.tokenRepository.DeleteRefreshToken(token1);
            var accessTokens = await this.tokenRepository.GetRefreshTokens(x => x.ClientId == "NUnit" && x.RedirectUri == "http://localhost");

            Assert.IsTrue(deleteResult);
            Assert.AreEqual(0, accessTokens.Count());
        }
    }
}