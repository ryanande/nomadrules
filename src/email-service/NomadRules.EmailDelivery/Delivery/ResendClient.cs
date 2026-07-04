using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NomadRules.EmailDelivery.Delivery;

// Direct HTTP to Resend, matching the summarizer's direct-Claude decision (design Decision 3):
// one JSON POST, no SDK dependency. Send failures are returned as false (caller logs + retries next tick),
// never thrown — a dead Resend must not fault the worker.
public class ResendClient(HttpClient http, ResendOptions options, ILogger<ResendClient> log)
{
    public async Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new { from = options.FromAddress, to, subject, text = body });
        var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        try
        {
            var resp = await http.SendAsync(request, ct);
            if (resp.IsSuccessStatusCode)
                return true;

            var detail = await resp.Content.ReadAsStringAsync(ct);
            log.LogError("Resend returned {Status} sending to {To}: {Detail}", (int)resp.StatusCode, to, detail);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Resend send to {To} failed", to);
            return false;
        }
    }
}
