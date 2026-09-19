// Client API OTPGmail — Node.js 18+ (không cần cài thư viện).
// Thuê một hộp thư Gmail/iCloud thật, chờ mã xác minh (OTP) gửi về,
// không có mã thì hủy để được hoàn tiền.  Tài liệu: https://otpgmail.net/app/docs
//
//   OTPGMAIL_API_KEY=og_xxx node otpgmail-client.mjs git icloud.com
import { randomUUID } from 'node:crypto';

const BASE = process.env.OTPGMAIL_BASE ?? 'https://otpgmail.net';
const KEY = process.env.OTPGMAIL_API_KEY;
const MAX_WAIT_MS = Number(process.env.OTPGMAIL_MAX_WAIT ?? 600) * 1000;
if (!KEY) throw new Error('Chưa đặt biến môi trường OTPGMAIL_API_KEY');

// Các lỗi này KHÔNG bị trừ tiền: đổi định dạng (Gmail/iCloud), đổi dịch vụ hoặc thử lại sau.
const SOFT_ERRORS = new Set(['NO_MAILS_AVAILABLE', 'OUT_OF_STOCK', 'DOMAIN_UNAVAILABLE']);

export class ApiError extends Error {
  constructor(code, message) {
    super(`${code}: ${message}`);
    this.code = code;
  }
}

async function call(path, init = {}) {
  const res = await fetch(BASE + path, {
    ...init,
    headers: {
      Authorization: `Bearer ${KEY}`,
      'Content-Type': 'application/json',
      'User-Agent': 'otpgmail-client/1.0 (+https://otpgmail.net)',
      ...(init.headers ?? {}),
    },
  });
  const body = await res.json();
  if (!body.success) throw new ApiError(body.error?.code ?? 'UNKNOWN', body.error?.message ?? '');
  return body.data;
}

/** Số dư tài khoản (VND). */
export const balance = async () => (await call('/v1/balance')).balance;

/** [{ code, name, price, stock, icloud: { price, stock } | null }] — giá tính bằng VND. */
export const services = () => call('/v1/services');

/** Thuê 1 mail → { orderId, email, domain, status, price, otp, codeDeadlineAt, ... } */
export async function rent(service, domain = 'gmail.com') {
  const data = await call('/v1/orders', {
    method: 'POST',
    body: JSON.stringify({ service, domain }),
    // Không bắt buộc nhưng nên có: request bị gửi lại sẽ nhận đúng đơn cũ, không thuê trùng.
    headers: { 'Idempotency-Key': randomUUID() },
  });
  return data[0]; // `data` luôn là mảng, kể cả khi quantity = 1
}

export const getOrder = (id) => call(`/v1/orders/${id}`);

/** Hủy đơn CHƯA có mã → hoàn 100% tiền. */
export const cancel = (id) => call(`/v1/orders/${id}/cancel`, { method: 'POST' });

/** Hỏi định kỳ tới khi có mã. Trả về mã, hoặc null (đơn đã hủy và hoàn tiền). */
export async function waitForCode(id, { maxWaitMs = MAX_WAIT_MS, intervalMs = 4000 } = {}) {
  const deadline = Date.now() + maxWaitMs;
  while (Date.now() < deadline) {
    const order = await getOrder(id);
    if (order.otp.length) return order.otp.at(-1).code;
    if (order.status === 'cancelled' || order.status === 'failed') return null;
    await new Promise((r) => setTimeout(r, intervalMs)); // 3–5 giây/lần là đủ, còn xa giới hạn tốc độ
  }
  await cancel(id); // hủy luôn để hoàn tiền ngay, khỏi đợi 30 phút tự hoàn
  return null;
}

/** Ưu tiên iCloud (rẻ hơn); iCloud hết/không hỗ trợ thì tự chuyển sang Gmail. */
export async function rentWithFallback(service, prefer = 'icloud.com') {
  try {
    return await rent(service, prefer);
  } catch (e) {
    if (e instanceof ApiError && SOFT_ERRORS.has(e.code)) {
      return rent(service, prefer === 'icloud.com' ? 'gmail.com' : 'icloud.com');
    }
    throw e;
  }
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const [service = 'git', domain = 'gmail.com'] = process.argv.slice(2);
  console.log('Số dư (VND):', await balance());
  const order = await rent(service, domain);
  console.log('Dán địa chỉ này vào form đăng ký:', order.email);
  const code = await waitForCode(order.orderId);
  console.log('Mã OTP:', code ?? 'không có - đơn đã hủy và hoàn tiền');
}
