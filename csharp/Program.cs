// Client API OTPGmail — C# / .NET 6+ (không cần gói NuGet nào).
// Thuê một hộp thư Gmail/iCloud thật, chờ mã xác minh (OTP) gửi về,
// không có mã thì hủy để được hoàn tiền.  Tài liệu: https://otpgmail.net/app/docs
//
//   set OTPGMAIL_API_KEY=og_xxx        (Linux/macOS: export OTPGMAIL_API_KEY=og_xxx)
//   dotnet run -- git icloud.com
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

var service = args.Length > 0 ? args[0] : "git";
var domain = args.Length > 1 ? args[1] : "gmail.com";

var client = new OtpGmailClient(
    Environment.GetEnvironmentVariable("OTPGMAIL_API_KEY") ?? throw new InvalidOperationException("Chưa đặt biến môi trường OTPGMAIL_API_KEY"),
    Environment.GetEnvironmentVariable("OTPGMAIL_BASE") ?? "https://otpgmail.net");
var maxWait = TimeSpan.FromSeconds(int.TryParse(Environment.GetEnvironmentVariable("OTPGMAIL_MAX_WAIT"), out var s) ? s : 600);

Console.WriteLine($"Số dư (VND): {await client.BalanceAsync()}");
var order = await client.RentAsync(service, domain);
Console.WriteLine($"Dán địa chỉ này vào form đăng ký: {order.GetProperty("email").GetString()}");
var code = await client.WaitForCodeAsync(order.GetProperty("orderId").GetString()!, maxWait);
Console.WriteLine($"Mã OTP: {code ?? "không có - đơn đã hủy và hoàn tiền"}");

public sealed class OtpGmailException : Exception
{
    public string Code { get; }
    public OtpGmailException(string code, string message) : base($"{code}: {message}") => Code = code;
}

public sealed class OtpGmailClient
{
    // Các lỗi này KHÔNG bị trừ tiền: đổi định dạng (Gmail/iCloud), đổi dịch vụ hoặc thử lại sau.
    private static readonly HashSet<string> SoftErrors = new() { "NO_MAILS_AVAILABLE", "OUT_OF_STOCK", "DOMAIN_UNAVAILABLE" };
    private readonly HttpClient _http;

    public OtpGmailClient(string apiKey, string baseUrl = "https://otpgmail.net")
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("otpgmail-client/1.0");
    }

    private async Task<JsonElement> CallAsync(HttpMethod method, string path, object? body = null, string? idempotencyKey = null)
    {
        using var req = new HttpRequestMessage(method, path);
        if (body != null) req.Content = JsonContent.Create(body);
        if (idempotencyKey != null) req.Headers.Add("Idempotency-Key", idempotencyKey);
        using var res = await _http.SendAsync(req);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        if (!root.TryGetProperty("success", out var ok) || !ok.GetBoolean())
        {
            var code = "UNKNOWN";
            var message = "";
            if (root.TryGetProperty("error", out var err))
            {
                code = err.TryGetProperty("code", out var c) ? c.GetString() ?? code : code;
                message = err.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            }
            throw new OtpGmailException(code, message);
        }
        return root.GetProperty("data").Clone();
    }

    /// <summary>Số dư tài khoản (VND).</summary>
    public async Task<long> BalanceAsync() => (await CallAsync(HttpMethod.Get, "/v1/balance")).GetProperty("balance").GetInt64();

    /// <summary>[{code, name, price, stock, icloud: {price, stock} | null}] — giá tính bằng VND.</summary>
    public Task<JsonElement> ServicesAsync() => CallAsync(HttpMethod.Get, "/v1/services");

    /// <summary>Thuê 1 mail → {orderId, email, domain, status, price, otp, codeDeadlineAt, ...}</summary>
    public async Task<JsonElement> RentAsync(string service, string domain = "gmail.com")
    {
        // Idempotency-Key không bắt buộc nhưng nên có: request bị gửi lại sẽ nhận đúng
        // đơn cũ thay vì thuê (và trả tiền) thêm một mail nữa.
        var data = await CallAsync(HttpMethod.Post, "/v1/orders", new { service, domain }, Guid.NewGuid().ToString());
        return data[0]; // `data` luôn là mảng, kể cả khi quantity = 1
    }

    public Task<JsonElement> GetOrderAsync(string orderId) => CallAsync(HttpMethod.Get, $"/v1/orders/{Uri.EscapeDataString(orderId)}");

    /// <summary>Hủy đơn CHƯA có mã → hoàn 100% tiền.</summary>
    public Task<JsonElement> CancelAsync(string orderId) => CallAsync(HttpMethod.Post, $"/v1/orders/{Uri.EscapeDataString(orderId)}/cancel");

    /// <summary>Hỏi định kỳ tới khi có mã. Trả về mã, hoặc null (đơn đã hủy và hoàn tiền).</summary>
    public async Task<string?> WaitForCodeAsync(string orderId, TimeSpan maxWait, int intervalSeconds = 4)
    {
        var deadline = DateTime.UtcNow + maxWait;
        while (DateTime.UtcNow < deadline)
        {
            var order = await GetOrderAsync(orderId);
            var otp = order.GetProperty("otp");
            if (otp.GetArrayLength() > 0) return otp[otp.GetArrayLength() - 1].GetProperty("code").GetString();
            var status = order.GetProperty("status").GetString();
            if (status is "cancelled" or "failed") return null; // quá 30 phút: hệ thống đã tự hủy và hoàn tiền
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds)); // 3–5 giây/lần là đủ, còn xa giới hạn tốc độ
        }
        await CancelAsync(orderId); // hủy luôn để hoàn tiền ngay, khỏi đợi tự hoàn
        return null;
    }

    /// <summary>Ưu tiên iCloud (rẻ hơn); iCloud hết/không hỗ trợ thì tự chuyển sang Gmail.</summary>
    public async Task<JsonElement> RentWithFallbackAsync(string service, string prefer = "icloud.com")
    {
        try
        {
            return await RentAsync(service, prefer);
        }
        catch (OtpGmailException e) when (SoftErrors.Contains(e.Code))
        {
            return await RentAsync(service, prefer == "icloud.com" ? "gmail.com" : "icloud.com");
        }
    }
}
