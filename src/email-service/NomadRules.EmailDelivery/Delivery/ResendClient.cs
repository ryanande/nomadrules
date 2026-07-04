using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NomadRules.EmailDelivery.Delivery;

// Direct HTTP to Resend, matching the summarizer's direct-Claude decision (design Decision 3):
// one JSON POST, no SDK dependency. Send failures are returned as false (caller logs + retries next tick),
// never thrown — a dead Resend must not fault the worker.
//
// idempotencyKey is the provider-side dedup backstop (design Risks): the DB reserve rows guard the common
// case, but two concurrent workers or a crash-then-retry can still call SendAsync twice for the same logical
// email. Passing a stable Idempotency-Key means Resend collapses those into a single delivery.
public class ResendClient(HttpClient http, ResendOptions options, ILogger<ResendClient> log)
{
    public async Task<bool> SendAsync(string to, string subject, string body, string idempotencyKey, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new { from = options.FromAddress, to, subject, text = body });
        var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);

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
