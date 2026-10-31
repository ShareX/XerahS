#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team
    This program is free software; you can redistribute it and/or modify it under the GPL v3.
*/

#endregion License Information (GPL v3)

using NUnit.Framework;
using XerahS.CLI.Commands;
using XerahS.Core.Cloud;

namespace XerahS.Tests.Tools;

[TestFixture]
public sealed class CloudCommandTests
{
    [TestCase(new[] { "xerahs://oauth/callback?code=abc&state=xyz" }, true)]
    [TestCase(new[] { "cloud", "complete", "xerahs://oauth/callback?code=abc&state=xyz" }, true)]
    [TestCase(new[] { "cloud", "status" }, false)]
    [TestCase(new[] { "https://cloud.xerahs.com/auth/desktop/callback?code=abc&state=xyz" }, false)]
    public void TryGetCallbackArgument_AcceptsProtocolAndCompleteInvocation(string[] args, bool expected)
    {
        bool parsed = CloudOAuthCallbackPipe.TryGetCallbackArgument(args, out string? callback);

        Assert.That(parsed, Is.EqualTo(expected));
        Assert.That(callback != null, Is.EqualTo(expected));
    }

    [Test]
    public void CreateCommandLine_InvokesCloudComplete()
    {
        string command = CloudProtocolBinding.CreateCommandLine(@"C:\Apps\xerahscli.exe");

        Assert.That(command, Is.EqualTo(@"""C:\Apps\xerahscli.exe"" cloud complete ""%1"""));
    }

    [Test]
    public async Task CallbackPipe_RoundTripsAuthorizationUri()
    {
        string pipeName = "XerahS.CloudOAuth.Test." + Guid.NewGuid().ToString("N");
        const string callback = "xerahs://oauth/callback?code=one-time-code&state=abc";
        Task<Uri?> wait = CloudOAuthCallbackPipe.WaitAsync(
            TimeSpan.FromSeconds(5),
            CancellationToken.None,
            pipeName);

        bool sent = false;
        for (int attempt = 0; attempt < 20 && !sent; attempt++)
        {
            await Task.Delay(50);
            sent = await CloudOAuthCallbackPipe.TrySendAsync(callback, CancellationToken.None, pipeName);
        }

        Uri? received = await wait;

        Assert.Multiple(() =>
        {
            Assert.That(sent, Is.True);
            Assert.That(received?.AbsoluteUri, Is.EqualTo(callback));
        });
    }

    [Test]
    public async Task SignIn_OpensBrowserAndAcceptsForwardedCallback()
    {
        var coordinator = new FakeCoordinator();
        var client = new FakeClient();
        string? openedUrl = null;
        var callback = new Uri("xerahs://oauth/callback?code=one-time-code&state=abc");

        int exitCode = await CloudCommand.SignInAsync(
            coordinator,
            client,
            url =>
            {
                openedUrl = url;
                return true;
            },
            (_, _) => Task.FromResult<Uri?>(callback),
            createProtocolBinding: null,
            json: true,
            TimeSpan.FromSeconds(5));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(openedUrl, Does.Contain("/api/auth/oauth2/authorize"));
            Assert.That(coordinator.LastCallback, Is.EqualTo(callback));
            Assert.That(client.AccountCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SignIn_ReportsTokenRejected()
    {
        var coordinator = new FakeCoordinator { CompleteResult = XerahSCloudOAuthCompletion.TokenRejected };
        var client = new FakeClient();

        int exitCode = await CloudCommand.SignInAsync(
            coordinator,
            client,
            _ => true,
            (_, _) => Task.FromResult<Uri?>(new Uri("xerahs://oauth/callback?code=one-time-code&state=abc")),
            createProtocolBinding: null,
            json: true,
            TimeSpan.FromSeconds(5));

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(client.AccountCalls, Is.EqualTo(0));
    }

    [Test]
    public void DescribeCompletion_ExplainsTokenRejected()
    {
        Assert.That(
            CloudCommand.DescribeCompletion(XerahSCloudOAuthCompletion.TokenRejected),
            Does.Contain("security checks"));
    }

    private sealed class FakeCoordinator : IXerahSCloudOAuthCoordinator
    {
        public XerahSCloudOAuthCompletion CompleteResult { get; set; } = XerahSCloudOAuthCompletion.Accepted;
        public Uri? LastCallback { get; private set; }

        public XerahSCloudOAuthAttempt Begin() => new(
            new Uri("https://cloud.xerahs.com/api/auth/oauth2/authorize?client_id=test"),
            "state",
            "nonce",
            "verifier",
            DateTimeOffset.UtcNow.AddMinutes(10));

        public Task<XerahSCloudOAuthCompletion> WaitForCompletionAsync(
            string state,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CompleteResult);

        public Task<XerahSCloudOAuthCompletion> CompleteAsync(
            Uri callbackUri,
            CancellationToken cancellationToken = default)
        {
            LastCallback = callbackUri;
            return Task.FromResult(CompleteResult);
        }
    }

    private sealed class FakeClient : IXerahSCloudClient
    {
        public int AccountCalls { get; private set; }
        public bool IsConfigured { get; set; } = true;
        public bool HasSessionCredential { get; set; }
        public string? CurrentOwnerSubject { get; set; }

        public Task<bool> RestoreSessionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HasSessionCredential);

        public Task<XerahSCloudAccountSummary> GetAccountAsync(CancellationToken cancellationToken = default)
        {
            AccountCalls++;
            return Task.FromResult(new XerahSCloudAccountSummary(
                "owner-name",
                new Uri("https://cloud.xerahs.com/owner-name/"),
                new Uri("https://cloud.xerahs.com/settings"),
                "Australia/Perth",
                true,
                "active",
                DateTimeOffset.UtcNow.AddDays(7),
                null,
                null,
                true,
                false));
        }

        public void SignOut() => HasSessionCredential = false;

        public Task<XerahSCloudPublishResponse> PublishAsync(
            XerahSCloudPublishRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<XerahSCloudDeleteResponse> UnpublishAsync(
            string clientItemId,
            string expectedOwnerSubject,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
