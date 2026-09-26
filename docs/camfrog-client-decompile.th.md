# รายงานการวิเคราะห์ Camfrog Video Chat.exe

> วิธีวิเคราะห์: อ่าน strings (Unicode), import table, registry, version resource — ไม่แตะ auth/license

## 1. ไฟล์คืออะไร

- โปรแกรม C++ 64-bit ฝัง CEF (Chromium), ไม่ใช่ .NET, ไม่มี asar
- เวอร์ชัน 8.5.0.51219, Camfrog LLC, ติดตั้งแบบ per-user, รันอัตโนมัติตอนล็อกอิน (HKCU Run)
- ความสามารถจาก DLL: กล้อง (AVICAP32), เสียง/วิดีโอ (MF, AVRT, DirectX), เครือข่าย (WINHTTP, WS2_32)

## 2. ลิงก์ camfrog: ทั้งหมด (ยืนยันจาก binary)

| ลิงก์ | ใช้ทำอะไร |
|---|---|
| `camfrog://join_room/?name=[ROOM]` | เข้าห้อง (ตัวจัดการใช้ตัวนี้) |
| `camfrog:join:` / `ecamfrog:join:` | เข้าห้องแบบเก่า |
| `camfrog:add:` / `im:` / `userprofile:` | เพิ่มเพื่อน / แชท / โปรไฟล์ |
| `camfrog:register` | สมัครสมาชิก |
| `camfrog://gift/`, `open_gift_store`, `open_sticker_store` | ของขวัญ (จ่ายเงิน ดูได้อย่างเดียว) |
| `camfrog://open_url/?url=` | เปิดลิงก์ภายนอก |

## 3. สวิตช์ command line

มีเพียง `--url="%1"` (จาก registry handler) — ไม่มีสวิตช์ล็อกอินอัตโนมัติ, ใส่รหัสผ่าน, หรือ headless ดังนั้นต้องล็อกอินด้วยมือครั้งแรกทุกกล่อง

## 4. เซิร์ฟเวอร์ (ไว้ตรวจเน็ตเท่านั้น)

- `api-desktop.camfrog.com/json/rpc.php` — API หลัก (ต้อง access_token ของผู้ใช้ เราไม่แตะ)
- `videochat.camfrog.com`, `room-history.camfrogcdn.com`, `profiles.camfrog.com`
- ล็อกอิน: บัญชี Camfrog, Google OAuth, Facebook OAuth; มีช่อง Login Server Override ในตั้งค่าไคลเอนต์

## 5. ที่เก็บข้อมูลล็อกอิน (ทำไมต้องล็อกอินทีละกล่อง)

- ไคลเอนต์จำล็อกอินใน `HKCU\Software\Camfrog\Client\...` (ProfileInfo, Nickname List, Settings ต่อผู้ใช้)
- ใน Sandboxie รีจิสทรีถูกจำลองต่อกล่อง (เห็น RegHive ในโฟลเดอร์กล่อง) — กล่องใหม่ = ยังไม่เคยล็อกอิน
- วิธี: เปิดไคลเอนต์แต่ละกล่อง ล็อกอินด้วยมือครั้งเดียว ติ๊กจำรหัสผ่าน หลังจากนั้น Start จากตัวจัดการจะเข้าห้องเอง

## 6. คำสั่งในห้องแชท (พิมพ์เอง ไม่ใช่บอท)

`/addfriend /kick /banlist /blockmic /invisible /clearbl /clearol /featured` — ตัวจัดการไม่ส่งข้อความแทนผู้ใช้

## 7. ขอบเขตถาวร (ไม่ทำ)

- ไม่ใส่รหัสผ่าน/ล็อกอินอัตโนมัติ, ไม่ขโมย token, ไม่เลี่ยง auth/license/CAPTCHA
- ไม่สร้าง Gold/สีชื่อ (ของ server-side จ่ายเงินเท่านั้น)

## 8. สรุปสำหรับผู้ใช้

1. ติดตั้งไดรเวอร์ Sandboxie + สร้างกล่องต่อบัญชี (ทำแล้ว: Seaza, _oIo_)
2. ล็อกอินด้วยมือครั้งแรกในแต่ละกล่อง + ติ๊กจำรหัสผ่าน
3. ตั้ง Room URL `camfrog://join_room/?name=ชื่อห้อง` ต่อบัญชี
4. Start จากตัวจัดการ — เข้าห้องเองทั้งสองจอ
