namespace AIQuotaBar.Providers.GitHubCopilot.Adapter;

using System.Reflection;
using GitHub.Copilot;
using GitHub.Copilot.Rpc;

public sealed class StandardCopilotClientAdapter : ICopilotClientAdapter
{
    public async Task<CopilotFetchResult> FetchQuotasAsync(
        string executablePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executablePath);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var workingDir = Path.Combine(Path.GetTempPath(), "AIQuotaBar", "provider-runtime");
        try
        {
            Directory.CreateDirectory(workingDir);
        }
        catch
        {
            workingDir = Path.GetTempPath();
        }

        var options = new CopilotClientOptions
        {
            Connection = RuntimeConnection.ForStdio(path: executablePath),
            WorkingDirectory = workingDir,
            UseLoggedInUser = true
        };

        using var client = new CopilotClient(options);

        await client.StartAsync(cts.Token).ConfigureAwait(false);

        try
        {
            var authStatus = await client.GetAuthStatusAsync(cts.Token).ConfigureAwait(false);
            var currentAuth = await client.Rpc.Account.GetCurrentAuthAsync(cts.Token).ConfigureAwait(false);

            string? plan = null;
            string? accessTypeSku = null;
            string? login = authStatus?.Login;

            if (currentAuth?.AuthInfo != null)
            {
                var authInfoObj = currentAuth.AuthInfo;
                if (authInfoObj is AuthInfoUser u)
                {
                    plan = u.CopilotUser?.CopilotPlan;
                    accessTypeSku = u.CopilotUser?.AccessTypeSku;
                    login ??= u.Login;
                }
                else if (authInfoObj is AuthInfoGhCli gh)
                {
                    plan = gh.CopilotUser?.CopilotPlan;
                    accessTypeSku = gh.CopilotUser?.AccessTypeSku;
                    login ??= gh.Login;
                }
                else if (authInfoObj is AuthInfoToken t)
                {
                    plan = t.CopilotUser?.CopilotPlan;
                    accessTypeSku = t.CopilotUser?.AccessTypeSku;
                }
                else if (authInfoObj is AuthInfoApiKey k)
                {
                    plan = k.CopilotUser?.CopilotPlan;
                    accessTypeSku = k.CopilotUser?.AccessTypeSku;
                }
                else
                {
                    // Dynamic fallback
                    var cuProp = authInfoObj.GetType().GetProperty("CopilotUser", BindingFlags.Public | BindingFlags.Instance);
                    var cu = cuProp?.GetValue(authInfoObj);
                    if (cu != null)
                    {
                        plan = cu.GetType().GetProperty("CopilotPlan")?.GetValue(cu)?.ToString();
                        accessTypeSku = cu.GetType().GetProperty("AccessTypeSku")?.GetValue(cu)?.ToString();
                    }
                }
            }

            var authInfo = new CopilotAuthInfoDto
            {
                IsAuthenticated = authStatus?.IsAuthenticated ?? false,
                Login = login,
                StatusMessage = authStatus?.StatusMessage,
                Plan = plan,
                AccessTypeSku = accessTypeSku
            };

            var quotaSnapshots = await client.Rpc.Account.GetQuotaAsync(cancellationToken: cts.Token).ConfigureAwait(false);

            var quotas = new List<CopilotQuotaDto>();
            if (quotaSnapshots?.QuotaSnapshots != null)
            {
                foreach (var kvp in quotaSnapshots.QuotaSnapshots)
                {
                    quotas.Add(new CopilotQuotaDto
                    {
                        Key = kvp.Key,
                        EntitlementRequests = kvp.Value.EntitlementRequests,
                        IsUnlimitedEntitlement = kvp.Value.IsUnlimitedEntitlement,
                        UsedRequests = kvp.Value.UsedRequests,
                        RemainingPercentage = kvp.Value.RemainingPercentage,
                        ResetDate = kvp.Value.ResetDate,
                        Overage = kvp.Value.Overage
                    });
                }
            }

            return new CopilotFetchResult
            {
                AuthInfo = authInfo,
                Quotas = quotas
            };
        }
        finally
        {
            try
            {
                await client.StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best effort stop
            }
        }
    }
}
