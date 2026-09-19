"""Client API OTPGmail — Python 3.8+ (pip install requests).

Thuê một hộp thư Gmail/iCloud thật, chờ mã xác minh (OTP) gửi về,
không có mã thì hủy để được hoàn tiền.  Tài liệu: https://otpgmail.net/app/docs

    export OTPGMAIL_API_KEY=og_xxx
    python otpgmail_client.py git              # GitHub, mail @gmail.com
    python otpgmail_client.py git icloud.com   # GitHub, mail @icloud.com (rẻ hơn ~10%)

Lưu ý: dùng `requests` (hoặc tự đặt User-Agent). urllib có sẵn của Python gửi
"Python-urllib/3.x" và bị CDN của hệ thống từ chối (HTTP 403).
"""
import os
import sys
import time
import uuid

import requests

BASE = os.environ.get("OTPGMAIL_BASE", "https://otpgmail.net")
MAX_WAIT = int(os.environ.get("OTPGMAIL_MAX_WAIT", "600"))  # số giây chờ mã
HEADERS = {
    "Authorization": f"Bearer {os.environ['OTPGMAIL_API_KEY']}",
    "User-Agent": "otpgmail-client/1.0 (+https://otpgmail.net)",
}

# Các lỗi này KHÔNG bị trừ tiền: đổi định dạng (Gmail/iCloud), đổi dịch vụ hoặc thử lại sau.
SOFT_ERRORS = {"NO_MAILS_AVAILABLE", "OUT_OF_STOCK", "DOMAIN_UNAVAILABLE"}


class ApiError(RuntimeError):
    def __init__(self, code, message):
        super().__init__(f"{code}: {message}")
        self.code = code


def _data(resp):
    body = resp.json()
    if not body.get("success"):
        err = body.get("error") or {}
        raise ApiError(err.get("code", "UNKNOWN"), err.get("message", ""))
    return body["data"]


def balance():
    """Số dư tài khoản (VND)."""
    return _data(requests.get(f"{BASE}/v1/balance", headers=HEADERS, timeout=30))["balance"]


def services():
    """[{code, name, price, stock, icloud: {price, stock} | None}] — giá tính bằng VND."""
    return _data(requests.get(f"{BASE}/v1/services", headers=HEADERS, timeout=30))


def rent(service, domain="gmail.com"):
    """Thuê 1 mail. Trả về {orderId, email, domain, status, price, otp, codeDeadlineAt, ...}."""
    resp = requests.post(
        f"{BASE}/v1/orders",
        json={"service": service, "domain": domain},
        # Không bắt buộc nhưng nên có: request bị gửi lại sẽ nhận đúng đơn cũ
        # thay vì thuê (và trả tiền) thêm một mail nữa.
        headers={**HEADERS, "Idempotency-Key": str(uuid.uuid4())},
        timeout=30,
    )
    return _data(resp)[0]  # `data` luôn là mảng, kể cả khi quantity = 1


def get_order(order_id):
    return _data(requests.get(f"{BASE}/v1/orders/{order_id}", headers=HEADERS, timeout=30))


def cancel(order_id):
    """Hủy đơn CHƯA có mã -> hoàn 100% tiền."""
    return _data(requests.post(f"{BASE}/v1/orders/{order_id}/cancel", headers=HEADERS, timeout=30))


def wait_for_code(order_id, max_wait=MAX_WAIT, interval=4):
    """Hỏi định kỳ tới khi có mã. Trả về mã, hoặc None (đơn đã hủy và hoàn tiền)."""
    deadline = time.time() + max_wait
    while time.time() < deadline:
        order = get_order(order_id)
        if order["otp"]:
            return order["otp"][-1]["code"]
        if order["status"] in ("cancelled", "failed"):
            return None  # quá 30 phút: hệ thống đã tự hủy và hoàn tiền
        time.sleep(interval)  # 3-5 giây/lần là đủ, còn xa giới hạn tốc độ
    cancel(order_id)  # hết thời gian chờ: hủy luôn để hoàn tiền ngay, khỏi đợi tự hoàn
    return None


def rent_with_fallback(service, prefer="icloud.com"):
    """Ưu tiên iCloud (rẻ hơn); iCloud hết/không hỗ trợ thì tự chuyển sang Gmail."""
    other = "gmail.com" if prefer == "icloud.com" else "icloud.com"
    try:
        return rent(service, prefer)
    except ApiError as e:
        if e.code in SOFT_ERRORS:
            return rent(service, other)
        raise


if __name__ == "__main__":
    svc = sys.argv[1] if len(sys.argv) > 1 else "git"
    dom = sys.argv[2] if len(sys.argv) > 2 else "gmail.com"
    print("Số dư (VND):", balance())
    order = rent(svc, dom)
    print("Dán địa chỉ này vào form đăng ký:", order["email"])
    code = wait_for_code(order["orderId"])
    print("Mã OTP:", code if code else "không có - đơn đã hủy và hoàn tiền")
