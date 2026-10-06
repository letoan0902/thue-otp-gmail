# Thuê OTP Gmail & iCloud – OTPGmail.net (API + code mẫu)

**Thuê Gmail nhận mã OTP** cho ChatGPT, GitHub, Facebook, Shopee, TikTok, Amazon và hơn 110 dịch vụ khác.
[OTPGmail.net](https://otpgmail.net/?utm_source=github&utm_medium=readme&utm_campaign=vi) cho thuê địa chỉ
**@gmail.com** và **@icloud.com thật**: bạn thuê một mail, dán vào form đăng ký, mã xác minh hiện ngay trên web
hoặc trả về qua API. **Không có mã trong 30 phút thì tự hoàn 100% tiền.**

[Thuê ngay](https://otpgmail.net/app?utm_source=github&utm_medium=readme&utm_campaign=vi) ·
[Bảng giá & tồn kho](https://otpgmail.net/app/services?utm_source=github&utm_medium=readme&utm_campaign=vi) ·
[Tài liệu API](https://otpgmail.net/app/docs?utm_source=github&utm_medium=readme&utm_campaign=vi) ·
[Hướng dẫn](https://otpgmail.net/huong-dan?utm_source=github&utm_medium=readme&utm_campaign=vi) ·
[English](https://github.com/letoan0902/otpgmail-email-otp-api)

Repo này dành cho anh em **developer / viết tool**: code mẫu gọi API thuê OTP Gmail bằng **Python, Node.js, PHP,
C# (.NET) và curl**. Toàn bộ code đã được chạy thử với API thật trước khi đăng.

> Nhiều bạn tìm dịch vụ này bằng cụm không dấu như *thue otp gmail*, *thue gmail nhan otp*, *thue gmail*,
> *thue icloud* hay gõ liền *otpgmail* — tất cả đều là **OTPGmail.net**.

## Thuê OTP Gmail là gì?

Là thuê **quyền nhận mã** ở một hộp thư Gmail (hoặc iCloud) thật trong thời gian ngắn. Bạn không nhận mật khẩu
hộp thư, không cần đăng nhập mail: hệ thống đọc mã giúp bạn và hiển thị ngay.

| | Mail 10 phút / temp mail | Tự tạo nhiều Gmail | **Thuê Gmail ở OTPGmail** |
|---|---|---|---|
| Dịch vụ lớn có chấp nhận? | Thường bị chặn, mã không về | Có | Có – mail @gmail.com / @icloud.com thật |
| Công sức | Thấp | Cao, Google đòi số điện thoại | Bấm thuê là có mail |
| Chi phí | Miễn phí | Thời gian + SIM | Từ 250đ/mail, không có mã hoàn tiền |
| Dùng cho tool tự động | Khó | Khó | Có REST API |

## Điểm chính

- ✅ Mail thật **@gmail.com** và **@icloud.com** (iCloud rẻ hơn Gmail khoảng 10%)
- ✅ **115 dịch vụ**; chưa có thì dùng mã `ot` (Dịch vụ bất kỳ) hoặc bấm **Thêm dịch vụ** để yêu cầu
- ✅ Giá **từ 250đ/mail**; chỉ trừ tiền khi được cấp mail
- ✅ **30 phút không có mã → tự hủy và hoàn 100%**, không cần nhắn admin
- ✅ Có mã rồi → **nhận thêm mã miễn phí trong 24 giờ**
- ✅ Nạp tiền bằng **QR ngân hàng** (tự cộng trong vài giây) hoặc **USDT** (BEP20 / TRC20 / TON)
- ✅ **Thuê theo lô** tới 200 mail/lần và **REST API** cho tool

## Thuê trên web trong 4 bước

1. [Đăng ký tài khoản](https://otpgmail.net/register?utm_source=github&utm_medium=readme&utm_campaign=vi) (miễn phí) và nạp tiền bằng QR ngân hàng hoặc USDT.
2. Vào **Thuê mail**, chọn **Định dạng** `@gmail.com` hoặc `@icloud.com`, chọn dịch vụ, bấm **Thuê ngay**.
3. Dán địa chỉ mail vừa nhận vào form đăng ký của dịch vụ.
4. Quay lại OTPGmail, mã hiện trên màn hình. Dịch vụ gửi mã lần nữa? Bấm **Nhận mã tiếp** (miễn phí trong 24 giờ).

## Dùng API thuê OTP Gmail (cho tool tự động)

```
POST /v1/orders {service, domain}     →  { orderId, email: "xxx@gmail.com", status: "waiting_code" }
GET  /v1/orders/{orderId}  mỗi 3–5 giây →  otp: [{ code: "123456", receivedAt }]   status: "completed"
POST /v1/orders/{orderId}/cancel      →  chưa có mã thì hủy = hoàn 100% (quá 30 phút hệ thống tự hoàn)
```

### 1. Lấy API key

Đăng nhập → **Tài khoản** → **API key**. Gửi kèm mọi request: `Authorization: Bearer <api_key>`.

### 2. Chạy code mẫu

```bash
export OTPGMAIL_API_KEY=og_xxx          # Windows: set OTPGMAIL_API_KEY=og_xxx

pip install requests && python python/otpgmail_client.py git icloud.com   # Python 3.8+
node node/otpgmail-client.mjs git icloud.com                               # Node.js 18+, không cần thư viện
php php/otpgmail_client.php git icloud.com                                 # PHP 7.4+ có ext-curl
cd csharp && dotnet run -- git icloud.com                                  # C# / .NET 6+
./curl/quickstart.sh git icloud.com                                        # bash + curl + jq
```

`git` là mã dịch vụ GitHub – xem đủ mã ở [BANG-MA-DICH-VU.md](BANG-MA-DICH-VU.md). Mỗi ví dụ sẽ in địa chỉ mail
vừa thuê, chờ mã (`OTPGMAIL_MAX_WAIT` giây, mặc định 600) và tự hủy đơn để hoàn tiền nếu không có mã.

### 3. Nhúng vào tool của bạn

```python
from otpgmail_client import rent_with_fallback, wait_for_code

order = rent_with_fallback("git", prefer="icloud.com")   # ưu tiên iCloud (rẻ hơn), hết thì sang Gmail
dien_form_dang_ky(email=order["email"])                  # code của bạn
ma = wait_for_code(order["orderId"])                     # None = đơn đã hủy và hoàn tiền
```

```csharp
var client = new OtpGmailClient(apiKey);
var order = await client.RentWithFallbackAsync("git", "icloud.com");
var email = order.GetProperty("email").GetString();
var ma = await client.WaitForCodeAsync(order.GetProperty("orderId").GetString()!, TimeSpan.FromMinutes(10));
```

### Tóm tắt API

Gốc `https://otpgmail.net` · JSON · Thành công: `{ "success": true, "data": ... }` · Lỗi: `{ "success": false, "error": { "code", "message" } }`

| Endpoint | Body | Trả về (`data`) |
|---|---|---|
| `GET /v1/balance` | – | `{ balance }` (VND) |
| `GET /v1/services` | – | `[{ code, name, price, stock, icloud: { price, stock } \| null }]` |
| `POST /v1/orders` | `{ service, domain?, quantity? }` | **mảng** đơn: `{ orderId, service, domain, email, status, price, otp, codeDeadlineAt, rentExpiresAt, createdAt }` |
| `GET /v1/orders/{orderId}` | – | một đơn; `otp` là `[{ code, receivedAt }]` |
| `POST /v1/orders/{orderId}/cancel` | – | đơn đã hủy (chỉ hủy được khi chưa có mã) |

- `domain`: `gmail.com` (mặc định) hoặc `icloud.com`. Tool cũ không gửi `domain` vẫn chạy bình thường và nhận Gmail.
- `quantity`: 1–200 mail/lần gọi (mặc định 1; trần là cài đặt chung, có thể khác theo tài khoản). Chỉ tính tiền số mail thực cấp.
- Tài liệu đầy đủ: <https://otpgmail.net/app/docs?utm_source=github&utm_medium=readme&utm_campaign=vi>

### Mã lỗi nên xử lý

| Mã | HTTP | Ý nghĩa | Có trừ tiền? |
|---|---|---|---|
| `NO_MAILS_AVAILABLE`, `OUT_OF_STOCK` | 503 / 409 | Tạm hết mail cho dịch vụ + định dạng đó | Không |
| `DOMAIN_UNAVAILABLE` | 409 | Dịch vụ chưa có iCloud hoặc iCloud tạm tắt → gọi lại với `gmail.com` | Không |
| `INSUFFICIENT_BALANCE` | 402 | Số dư không đủ | Không |
| `QUANTITY_EXCEEDED` | 400 | Quá trần số mail một lần gọi (mặc định 200) | Không |
| `WAITING_LIMIT_REACHED` | 429 | Quá nhiều đơn đang chờ mã → hủy bớt hoặc đợi xong | Không |
| `RATE_LIMITED` | 429 | Gọi quá nhanh (mặc định 20 request/giây, 900/phút mỗi key; số đang áp dụng hiện ở trang Tài liệu API) → đọc `Retry-After` | Không |

### Kinh nghiệm khi viết tool

- **Luôn gửi header `Idempotency-Key`** khi tạo đơn: mạng chập, tool gửi lại thì nhận đúng đơn cũ, không thuê trùng.
- **Hỏi mã 3–5 giây một lần** là đủ; hỏi dồn dập chỉ tổ dính `RATE_LIMITED`.
- **Không có mã thì gọi `cancel`** để tiền về ngay, khỏi chờ 30 phút tự hoàn.
- **iCloud hết thì rơi về Gmail** (hàm `rent_with_fallback` đã làm sẵn).
- **Python: dùng `requests`, đừng dùng `urllib` trần** – User-Agent mặc định `Python-urllib` bị CDN trả 403.
- **Thời gian mã về do dịch vụ đích quyết định**, không phải do hộp thư: đặt timeout theo từng dịch vụ.

## Số liệu thật (30 ngày gần nhất, tính đến 19/09/2026)

| Dịch vụ | Mã | Tỷ lệ có mã | Mã về sau |
|---|---|---|---|
| [GitHub](https://otpgmail.net/thue-mail/github?utm_source=github&utm_medium=readme&utm_campaign=vi) | `git` | 78% | ~30 giây |
| [AWS](https://otpgmail.net/thue-mail/aws?utm_source=github&utm_medium=readme&utm_campaign=vi) | `aws` | 96% | ~40 giây |
| [Shopee](https://otpgmail.net/thue-mail/shopee?utm_source=github&utm_medium=readme&utm_campaign=vi) | `ka` | 95% | ~45 giây |
| [Microsoft](https://otpgmail.net/thue-mail/microsoft?utm_source=github&utm_medium=readme&utm_campaign=vi) | `mm` | 89% | ~2 phút |
| [TikTok](https://otpgmail.net/thue-mail/tiktok?utm_source=github&utm_medium=readme&utm_campaign=vi) | `lf` | 88% | ~50 giây |
| [ChatGPT (OpenAI)](https://otpgmail.net/thue-mail/chatgpt?utm_source=github&utm_medium=readme&utm_campaign=vi) | `dr` | 70% | ~4 phút |
| [Facebook](https://otpgmail.net/thue-mail/facebook?utm_source=github&utm_medium=readme&utm_campaign=vi) | `fb` | 63% | ~1 phút |

Đơn không có mã chủ yếu do **dịch vụ đích** đòi thêm bước xác minh (captcha, số điện thoại) nên chưa gửi mail –
vì vậy mọi đơn không có mã đều được hoàn tiền tự động. Hệ thống đã cấp hơn **70.000 mail** từ tháng 7/2026.

## Thuê iCloud nhận OTP

Ngoài Gmail, bạn có thể **thuê mail iCloud** (`domain: "icloud.com"`) cho 114/115 dịch vụ, giá rẻ hơn khoảng 10%.
Cách dùng, thời gian giữ mail và chính sách hoàn tiền giống hệt Gmail. Riêng Apple ID chỉ có Gmail.
Xem thêm: [Thuê iCloud nhận OTP](https://otpgmail.net/thue-icloud?utm_source=github&utm_medium=readme&utm_campaign=vi).

## Bài viết hữu ích

- [Thuê OTP Gmail: nhận mã xác minh qua Gmail cho mọi dịch vụ](https://otpgmail.net/thue-otp-gmail?utm_source=github&utm_medium=readme&utm_campaign=vi)
- [Thuê Gmail giá rẻ: @gmail.com thật cho 110+ dịch vụ](https://otpgmail.net/thue-gmail?utm_source=github&utm_medium=readme&utm_campaign=vi)
- [Email ảo nhận code OTP: khác gì mail 10 phút](https://otpgmail.net/email-ao-nhan-code?utm_source=github&utm_medium=readme&utm_campaign=vi)
- [API nhận OTP qua email cho tool tự động](https://otpgmail.net/huong-dan/api-nhan-otp-qua-email-cho-tool-tu-dong?utm_source=github&utm_medium=readme&utm_campaign=vi)
- [Cách tạo tài khoản ChatGPT bằng email thuê](https://otpgmail.net/huong-dan/cach-tao-tai-khoan-chatgpt-khong-can-so-dien-thoai?utm_source=github&utm_medium=readme&utm_campaign=vi)
- [Không tạo được tài khoản GitHub: 5 lỗi thường gặp](https://otpgmail.net/huong-dan/khong-tao-duoc-tai-khoan-github?utm_source=github&utm_medium=readme&utm_campaign=vi)

## Câu hỏi thường gặp

**Thuê Gmail có phải mail 10 phút không?** Không. Đây là hộp thư Gmail/iCloud thật, nên các dịch vụ lớn không chặn.

**Tôi có nhận được mật khẩu mail không?** Không. Bạn chỉ nhận địa chỉ mail và mã gửi về; không đăng nhập hay gửi thư được.

**Thuê được bao lâu?** Tối đa 30 phút chờ mã đầu tiên; có mã rồi thì nhận thêm mã miễn phí trong 24 giờ.

**Không nhận được mã thì sao?** Đơn tự hủy sau 30 phút và hoàn 100% vào số dư. Muốn nhanh hơn thì bấm hủy hoặc gọi API `cancel`.

**Dịch vụ tôi cần chưa có trong danh sách?** Dùng mã `ot` (Dịch vụ bất kỳ) hoặc bấm **Thêm dịch vụ** ở trang thuê mail.

**Cần số lượng lớn?** API nhận `quantity` tới 200 mail/lần; trên web còn có **Thuê theo lô** tới 200 mail, giữ 24 giờ.

**Liên hệ hỗ trợ ở đâu?** Zalo / Telegram tại [trang Liên hệ](https://otpgmail.net/app/contact?utm_source=github&utm_medium=readme&utm_campaign=vi).

## Lưu ý sử dụng

Vui lòng dùng mail thuê đúng điều khoản của nền tảng bạn đăng ký: tài khoản phụ tách công việc, tài khoản test cho
developer, kiểm thử luồng đăng ký, đăng ký dịch vụ nước ngoài. OTPGmail không hỗ trợ mục đích vi phạm pháp luật.

## Giấy phép

MIT – xem [LICENSE](LICENSE). Hoan nghênh pull request bổ sung client cho ngôn ngữ khác.
