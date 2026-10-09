using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Azure.Core;
using Azure.Identity;

namespace SoapTester;

public class TokenProvider
{
    readonly Dictionary<string, TokenCredential> credentials = new();

    TokenCredential For(EnvironmentConfig env)
    {
        var key = env.TenantId + "|" + env.BaseUrl;
        if (credentials.TryGetValue(key, out var c)) return c;

        var tenant = string.IsNullOrWhiteSpace(env.TenantId) ? null : env.TenantId.Trim();
        var cli = new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenant });
        var browser = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
        {
            TenantId = tenant,
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions { Name = "SoapTester" }
        });
        return credentials[key] = new ChainedTokenCredential(cli, browser);
    }

    public async Task<string> GetTokenAsync(EnvironmentConfig env, CancellationToken ct)
    {
        var scope = env.BaseUrl + "/.default";
        var token = await For(env).GetTokenAsync(new TokenRequestContext(new[] { scope }), ct);
        return token.Token;
    }
}

public record SoapResult(int Status, string StatusText, string Raw, string Display, TimeSpan Elapsed);

public class SoapClient
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    readonly TokenProvider tokens = new();

    public async Task<SoapResult> SendAsync(EnvironmentConfig env, ServiceSettings s, string requestXml, CancellationToken ct)
    {
        var token = await tokens.GetTokenAsync(env, ct);
        if (s.Transport.Equals("Rest", StringComparison.OrdinalIgnoreCase))
            return await SendRestAsync(env, s, token, requestXml, ct);
        var url = env.BaseUrl + s.ServicePath;
        var action = $"{s.Namespace}/{s.ServiceName}/{s.Operation}";

        var company = string.IsNullOrWhiteSpace(env.Company) ? "" :
            $"<Context xmlns=\"http://schemas.microsoft.com/dynamics/2010/01/datacontracts\"><Company>{SecurityElement.Escape(env.Company)}</Company></Context>";
        var envelope =
            "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
            $"<s:Header>{company}</s:Header>" +
            "<s:Body>" +
            $"<{s.Operation} xmlns=\"{s.Namespace}\"><{s.ParameterName}>{SecurityElement.Escape(requestXml)}</{s.ParameterName}></{s.Operation}>" +
            "</s:Body></s:Envelope>";

        using var msg = new HttpRequestMessage(HttpMethod.Post, url);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        msg.Headers.Add("SOAPAction", $"\"{action}\"");
        msg.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");

        var sw = Stopwatch.StartNew();
        using var resp = await Http.SendAsync(msg, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        sw.Stop();

        return new SoapResult((int)resp.StatusCode, resp.ReasonPhrase ?? "", Pretty(raw), Unwrap(raw), sw.Elapsed);
    }

    async Task<SoapResult> SendRestAsync(EnvironmentConfig env, ServiceSettings s, string token, string requestXml, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, string> { [s.ParameterName] = requestXml });
        using var msg = new HttpRequestMessage(HttpMethod.Post, env.BaseUrl + s.RestPath);
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        msg.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var sw = Stopwatch.StartNew();
        using var resp = await Http.SendAsync(msg, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        sw.Stop();

        var display = raw;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.String)
                display = Pretty(doc.RootElement.GetString() ?? "");
            else
                raw = display = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { }
        return new SoapResult((int)resp.StatusCode, resp.ReasonPhrase ?? "", raw, display, sw.Elapsed);
    }

    static string Pretty(string xml)
    {
        try { return XDocument.Parse(xml).ToString(); } catch { return xml; }
    }

    // Pulls the string result out of the SOAP body and pretty-prints it when it is itself XML;
    // faults and anything unexpected fall back to the pretty-printed raw response.
    static string Unwrap(string raw)
    {
        try
        {
            var doc = XDocument.Parse(raw);
            var body = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Body");
            var fault = body?.Elements().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (body == null || fault != null) return doc.ToString();
            var result = body.Elements().FirstOrDefault()?.Elements().FirstOrDefault();
            if (result == null) return doc.ToString();
            return Pretty(result.Value);
        }
        catch { return raw; }
    }
}
