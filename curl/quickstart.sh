#!/usr/bin/env bash
# Gọi API OTPGmail bằng curl + jq.   Tài liệu: https://otpgmail.net/app/docs
#   OTPGMAIL_API_KEY=og_xxx ./quickstart.sh git icloud.com
set -euo pipefail
BASE="${OTPGMAIL_BASE:-https://otpgmail.net}"
AUTH="Authorization: Bearer ${OTPGMAIL_API_KEY:?Chua dat bien OTPGMAIL_API_KEY}"
SERVICE="${1:-git}"; DOMAIN="${2:-gmail.com}"; MAX_WAIT="${OTPGMAIL_MAX_WAIT:-600}"

echo "Số dư (VND): $(curl -fsS -H "$AUTH" "$BASE/v1/balance" | jq -r .data.balance)"

# 1) Thuê mail (Idempotency-Key: request gửi lại không bao giờ thuê trùng)
ORDER=$(curl -sS -X POST "$BASE/v1/orders" -H "$AUTH" -H 'Content-Type: application/json' \
  -H "Idempotency-Key: $(cat /proc/sys/kernel/random/uuid 2>/dev/null || uuidgen)" \
  -d "{\"service\":\"$SERVICE\",\"domain\":\"$DOMAIN\"}")
[ "$(echo "$ORDER" | jq -r .success)" = "true" ] || { echo "Lỗi: $(echo "$ORDER" | jq -c .error)"; exit 1; }
ID=$(echo "$ORDER" | jq -r '.data[0].orderId')
echo "Dán địa chỉ này vào form đăng ký: $(echo "$ORDER" | jq -r '.data[0].email')"

# 2) Hỏi mã mỗi 4 giây
END=$((SECONDS + MAX_WAIT))
while [ $SECONDS -lt $END ]; do
  O=$(curl -fsS -H "$AUTH" "$BASE/v1/orders/$ID")
  CODE=$(echo "$O" | jq -r '.data.otp[-1].code // empty')
  [ -n "$CODE" ] && { echo "Mã OTP: $CODE"; exit 0; }
  [ "$(echo "$O" | jq -r .data.status)" = "cancelled" ] && { echo "Đơn đã bị hủy và hoàn tiền"; exit 2; }
  sleep 4
done

# 3) Không có mã: hủy ngay → hoàn 100% tiền
curl -fsS -X POST -H "$AUTH" "$BASE/v1/orders/$ID/cancel" | jq -r '"Đã hủy: \(.data.status) - đã hoàn tiền"'
