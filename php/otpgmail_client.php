<?php
/**
 * Client API OTPGmail — PHP 7.4+ (ext-curl, ext-json).
 * Thuê một hộp thư Gmail/iCloud thật, chờ mã xác minh (OTP) gửi về,
 * không có mã thì hủy để được hoàn tiền.  Tài liệu: https://otpgmail.net/app/docs
 *
 *   OTPGMAIL_API_KEY=og_xxx php otpgmail_client.php git icloud.com
 */

// Các lỗi này KHÔNG bị trừ tiền: đổi định dạng (Gmail/iCloud), đổi dịch vụ hoặc thử lại sau.
const OTPGMAIL_SOFT_ERRORS = ['NO_MAILS_AVAILABLE', 'OUT_OF_STOCK', 'DOMAIN_UNAVAILABLE'];

class OtpGmailError extends RuntimeException
{
    /** @var string */
    public $errorCode;

    public function __construct(string $code, string $message)
    {
        parent::__construct("$code: $message");
        $this->errorCode = $code;
    }
}

function otpgmail_call(string $method, string $path, ?array $body = null, array $extraHeaders = [])
{
    $base = getenv('OTPGMAIL_BASE') ?: 'https://otpgmail.net';
    $key = getenv('OTPGMAIL_API_KEY');
    if (!$key) {
        throw new RuntimeException('Chưa đặt biến môi trường OTPGMAIL_API_KEY');
    }
    $ch = curl_init($base . $path);
    curl_setopt_array($ch, [
        CURLOPT_CUSTOMREQUEST => $method,
        CURLOPT_RETURNTRANSFER => true,
        CURLOPT_TIMEOUT => 30,
        CURLOPT_HTTPHEADER => array_merge([
            'Authorization: Bearer ' . $key,
            'Content-Type: application/json',
            'User-Agent: otpgmail-client/1.0 (+https://otpgmail.net)',
        ], $extraHeaders),
    ]);
    if ($body !== null) {
        curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($body));
    }
    $raw = curl_exec($ch);
    if ($raw === false) {
        throw new RuntimeException('HTTP error: ' . curl_error($ch));
    }
    $json = json_decode($raw, true);
    if (!is_array($json) || empty($json['success'])) {
        $err = is_array($json) && isset($json['error']) ? $json['error'] : [];
        throw new OtpGmailError($err['code'] ?? 'UNKNOWN', $err['message'] ?? substr((string) $raw, 0, 200));
    }
    return $json['data'];
}

/** Số dư tài khoản (VND). */
function otpgmail_balance(): int
{
    return (int) otpgmail_call('GET', '/v1/balance')['balance'];
}

/** [{code, name, price, stock, icloud: {price, stock} | null}] — giá tính bằng VND. */
function otpgmail_services(): array
{
    return otpgmail_call('GET', '/v1/services');
}

/** Thuê 1 mail → [orderId, email, domain, status, price, otp, codeDeadlineAt, ...] */
function otpgmail_rent(string $service, string $domain = 'gmail.com'): array
{
    // Idempotency-Key không bắt buộc nhưng nên có: request bị gửi lại sẽ nhận đúng
    // đơn cũ thay vì thuê (và trả tiền) thêm một mail nữa.
    $data = otpgmail_call('POST', '/v1/orders', ['service' => $service, 'domain' => $domain], [
        'Idempotency-Key: ' . bin2hex(random_bytes(16)),
    ]);
    return $data[0]; // `data` luôn là mảng, kể cả khi quantity = 1
}

function otpgmail_get_order(string $orderId): array
{
    return otpgmail_call('GET', '/v1/orders/' . rawurlencode($orderId));
}

/** Hủy đơn CHƯA có mã → hoàn 100% tiền. */
function otpgmail_cancel(string $orderId): array
{
    return otpgmail_call('POST', '/v1/orders/' . rawurlencode($orderId) . '/cancel');
}

/** Hỏi định kỳ tới khi có mã. Trả về mã, hoặc null (đơn đã hủy và hoàn tiền). */
function otpgmail_wait_for_code(string $orderId, ?int $maxWait = null, int $interval = 4): ?string
{
    $maxWait = $maxWait ?? (int) (getenv('OTPGMAIL_MAX_WAIT') ?: 600);
    $deadline = time() + $maxWait;
    while (time() < $deadline) {
        $order = otpgmail_get_order($orderId);
        if (!empty($order['otp'])) {
            return end($order['otp'])['code'];
        }
        if (in_array($order['status'], ['cancelled', 'failed'], true)) {
            return null; // quá 30 phút: hệ thống đã tự hủy và hoàn tiền
        }
        sleep($interval); // 3–5 giây/lần là đủ, còn xa giới hạn tốc độ
    }
    otpgmail_cancel($orderId); // hủy luôn để hoàn tiền ngay, khỏi đợi tự hoàn
    return null;
}

/** Ưu tiên iCloud (rẻ hơn); iCloud hết/không hỗ trợ thì tự chuyển sang Gmail. */
function otpgmail_rent_with_fallback(string $service, string $prefer = 'icloud.com'): array
{
    try {
        return otpgmail_rent($service, $prefer);
    } catch (OtpGmailError $e) {
        if (in_array($e->errorCode, OTPGMAIL_SOFT_ERRORS, true)) {
            return otpgmail_rent($service, $prefer === 'icloud.com' ? 'gmail.com' : 'icloud.com');
        }
        throw $e;
    }
}

if (PHP_SAPI === 'cli' && realpath($argv[0] ?? '') === __FILE__) {
    $service = $argv[1] ?? 'git';
    $domain = $argv[2] ?? 'gmail.com';
    echo 'Số dư (VND): ', otpgmail_balance(), PHP_EOL;
    $order = otpgmail_rent($service, $domain);
    echo 'Dán địa chỉ này vào form đăng ký: ', $order['email'], PHP_EOL;
    $code = otpgmail_wait_for_code($order['orderId']);
    echo 'Mã OTP: ', $code ?? 'không có - đơn đã hủy và hoàn tiền', PHP_EOL;
}
