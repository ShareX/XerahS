#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.
*/

#endregion License Information (GPL v3)

using XerahS.Uploaders.PluginSystem;

namespace XerahS.Core.Cloud;

public sealed record XerahSCloudSession(
    string AccessToken,
    string RefreshToken,
    string OwnerSubject,
    DateTimeOffset ExpiresAt);

public interface IXerahSCloudSessionStore
{
    XerahSCloudSession? Current { get; }
    void Accept(XerahSCloudSession session);
    (string OwnerSubject, string RefreshToken)? ReadRefreshCredential();
    void Clear();
}

/// <summary>
/// Keeps bearer access tokens in memory and persists only the refresh credential in the
/// operating-system-backed secret store. Cloud auth fails closed when that store is a fallback.
/// </summary>
public sealed class XerahSCloudSessionStore : IXerahSCloudSessionStore
{
    private const string ProviderId = "xerahs-cloud";
    private const string SecretKey = "session";
    private const string RefreshTokenName = "refresh-token";
    private const string OwnerSubjectName = "owner-subject";

    private readonly ISecretStore _secretStore;
    private readonly Lock _sync = new();
    private XerahSCloudSession? _current;

    public XerahSCloudSessionStore(ISecretStore secretStore)
    {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    public XerahSCloudSession? Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public void Accept(XerahSCloudSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.AccessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.RefreshToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(session.OwnerSubject);
        EnsureSecureBackend();

        lock (_sync)
        {
            _secretStore.SetSecret(ProviderId, SecretKey, RefreshTokenName, session.RefreshToken);
            _secretStore.SetSecret(ProviderId, SecretKey, OwnerSubjectName, session.OwnerSubject);
            _current = session;
        }
    }

    public (string OwnerSubject, string RefreshToken)? ReadRefreshCredential()
    {
        EnsureSecureBackend();
        lock (_sync)
        {
            string? owner = _secretStore.GetSecret(ProviderId, SecretKey, OwnerSubjectName);
            string? refreshToken = _secretStore.GetSecret(ProviderId, SecretKey, RefreshTokenName);
            return string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(refreshToken)
                ? null
                : (owner, refreshToken);
        }
    }

    public void Clear()
    {
        EnsureSecureBackend();
        lock (_sync)
        {
            _current = null;
            _secretStore.DeleteSecret(ProviderId, SecretKey, RefreshTokenName);
            _secretStore.DeleteSecret(ProviderId, SecretKey, OwnerSubjectName);
        }
    }

    private void EnsureSecureBackend()
    {
        if (_secretStore is not ISecretStoreInfo info || info.IsFallback)
        {
            throw new XerahSCloudSecurityException(
                "XerahS Cloud requires an operating-system protected secret store; the current fallback store is not accepted.");
        }
    }
}
