# Connect Puzzle

Game giải đố nối ô cùng màu, chạy hoàn toàn **offline**, không server, không tài khoản.

Điểm khác biệt so với match-3 thông thường: bàn không được gieo ngẫu nhiên rồi mong nó chơi được, mà được **sinh từ seed kèm bảo đảm giải được**. Mỗi màn mang theo một lời giải tham chiếu, và toàn bộ hệ thống cân bằng (số lượt, mốc sao, nút gợi ý, chế độ leo tháp) đều dựng trên con số đó.

---

## Luật cốt lõi

Kéo ngón qua các ô **cùng màu nằm kề nhau** — tính cả bốn hướng chéo — rồi thả ra để ăn.

| | |
|---|---|
| Độ dài chuỗi | 3–5 ô ở 86/100 màn; một số màn cuối cho tới 6 |
| Điểm một chuỗi | `n × (n − 1)` — chuỗi 5 ô ăn 20 điểm, chuỗi 3 ô chỉ 6 |
| Thắng | Dọn sạch bàn, hoặc dọn hết ô đích ở màn mục tiêu |
| Thua | Hết lượt, hoặc bàn không còn chuỗi hợp lệ nào |

**Trần chuỗi là núm độ khó mạnh nhất.** Không có trần thì người chơi quét nguyên một cục lớn trong một nước và ngân sách lượt thành thừa thãi — đo được là bot tham lam thắng 14/24 màn, có màn dư tới 14 lượt. Có trần thì phải chọn **chẻ cục lớn ở đâu**, và chẻ sai thì phần dư không ăn được.

---

## Sáu chế độ chơi

### 1. Chiến dịch — 100 màn
10 thế giới × 10 màn. Mỗi thế giới đưa vào đúng một cơ chế mới. Mở khoá tuần tự, hoặc bật **Chơi tự do** để vào thẳng màn nào cũng được (tiến độ vẫn ghi bình thường).

### 2. Vô tận
Bàn 7×8, ô rơi xuống mãi, không giới hạn lượt. Áp lực đến từ **số màu tăng theo điểm**: 4 màu → 5 màu (từ 800đ) → 6 màu (từ 2500đ). Càng nhiều màu, ô càng khó gặp bạn cùng màu.

- **Combo**: chuỗi từ 4 ô trở lên nối tiếp nhau nhân điểm, tối đa ×3
- 3 lượt xáo bàn; xáo là **mất mạch combo** — đó là cái giá
- 3,5% ô rơi xuống là ô đa sắc
- Bảo đảm duy nhất: sau mỗi lần đổ đầy, bàn **phải** còn ít nhất một nước đi. Người chơi thua vì chơi dở, không vì xúc xắc
- Lưu ván đang chơi, mở lại là tiếp tục đúng chỗ

### 3. Thử thách hằng ngày
Mỗi ngày một bàn, **giống nhau trên mọi máy**. Seed lấy từ ngày UTC (không dùng giờ máy — hai người ở hai múi giờ phải nhận cùng một bàn trong cùng một "hôm nay"). Kiểu bàn xoay theo **thứ trong tuần**, nên đoán trước được: thứ Hai luôn là ngày có đá.

Có **chuỗi ngày** (streak), và kết quả **chia sẻ được** dưới dạng đoạn chữ dán vào chat:

```
Connect Puzzle · Thử thách 26/08
★★★ 11 lượt · 268 điểm
🟩🟩🟦🟩⬜🟩🟩🟦🟩🟩
🟦
🟩 chuỗi dài nhất · ⬜ ngắn nhất · mỗi ô một lượt
chuỗi 7 ngày
```

Ô vuông tô theo **chất lượng chuỗi**, không theo màu ô — vì cả ngày mọi người chơi cùng một bàn, nên lưới tô theo màu hay vị trí sẽ là bản đồ đáp án.

### 4. Đấu seed bạn bè
Hai người nhập cùng một mã thì gặp cùng một bàn, rồi so ai ít lượt / điểm cao hơn. Không cần server.

- Không gian seed 24 bit = **16.777.216 bàn**, 4 preset (Cơ bản / Đá / Băng / Dây trói)
- Mã có checksum; mã kết quả mang **dấu nhận dạng bàn** 16 bit, nên hai người không thể vô tình so kết quả của hai bàn khác nhau

### 5. Đấu cùng Wi-Fi (LAN)
Tự tìm nhau trong mạng nội bộ qua UDP broadcast, không dùng Netcode, không server.

- Tìm **hai chiều** (chủ phát lời mời + khách phát gói tìm), vì broadcast có thể chỉ đi được một chiều: có router chặn chiều này mà không chặn chiều kia, và có Android bỏ gói broadcast đến trong khi vẫn gửi đi bình thường
- Mỗi gói có magic + phiên bản + CRC8 — cổng UDP là cổng chung, máy in và TV cũng phát lên đó
- **Tiến độ trực tiếp**: mỗi nước đi phát một gói, HUD hiện `⚔ Nam · 12 lượt · 8 ô`. Bạn biết mình đang dẫn hay bị bỏ xa, và cái biết đó đổi cách chọn nước tiếp theo

### 6. Leo tháp
3–5 màn cuối của một thế giới, **một ngân sách lượt dùng chung**. Không lưu tiếp tục, không chơi lại một màn giữa chặng.

Nó khai thác một chỗ trống trong thiết kế: bình thường **về đích còn dư lượt không để làm gì cả**. Ở đây lượt dư là vốn — tiết kiệm ở màn 1 là thứ cứu bạn ở màn cuối.

Ngân sách = tổng par + **45% lượt dư** mà bạn sẽ có nếu chơi rời từng màn. Lấy theo phần trăm chứ không cộng số cố định, để độ khít bằng nhau ở mọi thế giới. Vào từ menu: bấm nhãn thế giới.

---

## Mười thế giới

| # | Thế giới | Cơ chế mới | Nó phá giả định nào |
|---|---|---|---|
| 1 | Nhập môn | Nối và dọn sạch | — |
| 2 | Hình dạng | Bàn khuyết, cục màu dính lớn | "Ăn ở đâu cũng như nhau" → phải chẻ |
| 3 | Gravity | Ô rơi, hàng chờ tụt vào | Ô chỉ rơi **trong cột**, hai ô cùng màu ở hai cột xa có thể không bao giờ gặp nhau |
| 4 | Đá tảng | Không nối được, vỡ khi có chuỗi ăn **kề** nó | Cơ chế đầu tiên làm **vị trí** của chuỗi có ý nghĩa |
| 5 | Đa sắc | Ghép được mọi màu, tối đa 1 ô/chuỗi | Cho phép đẩy lên 6 màu mà bàn vẫn chơi được |
| 6 | Ngòi nổ | Đếm ngược theo lượt | Thêm ràng buộc **thứ tự**: nước đúng nhưng đi sai lúc vẫn thua |
| 7 | Mục tiêu | Thắng khi dọn hết ô đích | Không cần sạch bàn — par ở đây nhỏ hơn hẳn |
| 8 | Băng giá | Ô có màu nhưng khoá, tan khi chuỗi kề bị ăn | Đá là **gỡ vật cản**, băng là **mở khoá đường đi** |
| 9 | Dây trói | Ăn một đầu, đầu kia vỡ theo dù ở xa | Phá luật "muốn ăn thì phải kề" |
| 10 | Chính xác | Ngân sách lượt **bằng đúng par** | Bỏ hết lượt dư — một nước hớ là hết |

**Thế giới 10** là thế giới duy nhất mà độ khó không đến từ cơ chế mới. Đi kèm ba lựa chọn cân bằng: bàn nhỏ (5×5 → 6×7, vì phải nhìn ra cả kế hoạch trước nước đầu), nhiều hoàn tác (6–8, để một nước hớ sửa được tại chỗ), và **cấm vật phẩm** — "+1 lượt" giá ★2 thì xoá đúng điều làm nên chế độ này. Par thực tế 6–11 lượt.

---

## Tiến độ và kinh tế

| Thứ | Cách kiếm |
|---|---|
| **Sao** 1–3 | Theo số lượt dùng: ≤ par = 3★, ≤ mốc hai sao = 2★, còn lại 1★ |
| **Huy hiệu kỹ thuật** ◆ | Ăn đủ số chuỗi **kịch trần** trong một ván thắng. Thưởng ★2 |
| **Ví sao** | Sao kiếm được − sao đã tiêu. Bảng thành tích **không bị trừ** — tiêu sao xong màn 3★ vẫn là 3★ |

**Vật phẩm** mua bằng sao, dùng ngay, không có kho đồ. Bỏ kho đi vì kho đòi thêm màn hình quản lý và chỗ lưu mà không thêm quyết định nào — quyết định thật chỉ có một: *dùng bây giờ, hay để dành sao?*

| Vật phẩm | Giá | Vì sao giá đó |
|---|---|---|
| Sơn (biến ô thành đa sắc) | ★5 | Thứ duy nhất **tạo ra khả năng mới** thay vì chỉ dọn bớt; giá trị kéo dài tới cuối ván |
| Búa (đập một ô) | ★3 | Dọn vật cản tại chỗ |
| +1 lượt | ★2 | Chỉ **hoãn** thất bại, không gỡ được thế bí |

Vật phẩm **không tốn lượt** — tốn lượt thì đúng lúc cần nó nhất (sắp hết lượt) nó lại thành vô dụng. Tắt ở chế độ Vô tận, Thử thách, Đấu, Leo tháp và thế giới Chính xác.

---

## Chống bí

Ba lớp, theo thứ tự người chơi gặp:

1. **Hoàn tác** — có hạn mức mỗi màn. Hoàn tác bước đã dùng vật phẩm thì **hoàn lại sao**, không thì hoàn tác thành hình phạt và người chơi học cách không bao giờ bấm nó.
2. **Xáo lại** — không chỉ tô lại màu mà **dồn ô về một khối liền**, nên luôn có lời giải. Và nó **nói thẳng khi vô vọng**: nếu không cách xáo nào dọn sạch được trong số lượt còn lại thì báo luôn, không tiêu quota, thay vì xáo ra một bàn đẹp mà vẫn thua.
3. **Chẩn đoán thua** — khi không thể thắng nữa, game chỉ thẳng vào **các ô cụ thể** gây ra chuyện đó, thắp sáng lần lượt và đánh số 1-2-3-4, biến "4 nhóm rời nhau" thành thứ đếm được bằng mắt.

Bộ chẩn đoán chỉ báo thua khi **chắc chắn đúng** — không phỏng đoán. Chiều ngược lại không được bảo đảm: không báo gì chỉ nghĩa là chưa chứng minh được là thua.

---

## Hướng dẫn và phản hồi

**Bài hướng dẫn cơ chế** — mỗi thế giới một bài, tự hiện lần đầu, xem lại bằng cách bấm nhãn thế giới ở menu. Bàn minh hoạ là một **phiên chơi thật chạy trên engine thật**, không phải hình vẽ mô tả luật: nước đi đi qua đúng bộ luật của game. Nghĩa là bài học không nói sai được — hôm nào luật đá đổi thì hình minh hoạ đổi theo, thay vì âm thầm dạy luật cũ.

**Âm thanh** sinh tại runtime, không dùng file: mỗi ô nối thêm là một bậc **thang ngũ cung** đi lên, nên chuỗi càng dài càng nghe rõ là đang đi lên. Ăn chuỗi thì rải 2–4 nốt tuỳ độ dài — tai biết mình vừa ăn lớn hay nhỏ trước khi mắt kịp đọc điểm.

**Rung** (Android) — cú gõ 9–40ms có cường độ, theo độ dài chuỗi. Nút phản hồi ở chân menu đi qua ba trạng thái: tắt → âm thanh → âm thanh + rung.

---

## Kiến trúc

**Tách `Core` / `View`.** `Core` không phụ thuộc `UnityEngine` — toàn bộ luật chơi, sinh màn, solver, chẩn đoán thua và giao thức mạng đều chạy được trong một chương trình console. Đây không phải sở thích kiến trúc mà là thứ trả tiền hàng ngày: kiểm được luật mà không cần mở Editor.

**Prefab là nguồn duy nhất của UI.** Code dựng UI đã bị xoá; sửa UI thì sửa prefab bằng Editor. Có một **ảnh chụp bố cục** nằm trong git làm lưới an toàn: ai kéo nhầm một node thì hiện ra thành diff.

**Bảo đảm sinh màn.** Mỗi màn sinh từ seed và chỉ được nhận nếu phân hoạch được thành các nhóm hợp luật. Ô đặc biệt (đá, băng, ngòi, đích, dây trói) gắn **sau** khi phân hoạch, theo những ràng buộc giữ cho lời giải tham chiếu vẫn còn hợp lệ.

**Tính giống nhau giữa các máy đã được đo, không phải giả định.** Bộ mẫu 150 bàn cho ra cùng vân tay trên PC (.NET JIT, x64), giả lập (IL2CPP, x86_64) và điện thoại thật (IL2CPP, ARM64). Đó là điều kiện để chế độ đấu và thử thách hằng ngày có nghĩa.

---

## Kiểm thử

| Bộ | Kiểm gì |
|---|---|
| Sinh màn | Cả 100 màn dựng được, par > 0, bàn đầu còn nước đi |
| Chế độ Chính xác | 10 màn mới có `MaxMoves == par`, vật phẩm bị cấm, nút xáo tắt. Kèm **chặn hồi quy**: 90 màn cũ không đổi |
| Bài hướng dẫn | Cả 10 bài chạy qua engine thật, và **điểm của từng bài thật sự xảy ra** (đá xa còn nguyên, băng tan thành ô dùng được, đầu dây đối diện vỡ mà không bị chạm) |
| Ngân sách leo tháp | Giữ 40–50% lượt dư ở mọi thế giới, luôn khít hơn chơi rời, luôn xong được nếu đi đúng par |
| Giao thức LAN | Vòng tròn mã hoá–phân tích ở các biên, CRC bắt được gói sửa, **tương thích ngược** với bản cũ, và một ván đấu thật 12 nước đi qua dây |
| Bố cục thẻ nổi | 188 phép kiểm: 4 loại thẻ × 4 tỉ lệ màn hình, canh chữ không tụt xuống dưới nút và số nút khớp với số đã khai |

Build: **IL2CPP / arm64-v8a**, APK 21,9 MB.

---

## Trạng thái

**Chạy được và đã kiểm:** toàn bộ phần trên biên dịch sạch, đóng gói vào APK, và các bộ kiểm ở trên đều đạt.

**Chưa ai chơi thử:** 10 màn Chính xác và ngân sách leo tháp — par là số bot tính được; "khít tới mức thú vị" hay "khít tới mức khó chịu" thì phải cầm máy mới biết.

**Chưa đo trên hai máy thật:** gói tiến độ LAN. Định dạng gói đã kiểm kỹ, nhưng "hai điện thoại có thấy nhau không" thì cần hai điện thoại.

**Việc còn lại đáng làm nhất:**

- Bàn đối thủ thu nhỏ trong ván đấu LAN — kênh dữ liệu đã có, chỉ cần chỗ trên màn hình
- Đòn tấn công: chuỗi dài đổ đá/băng sang bàn đối phương
- Tách bảng chuỗi tiếng Việt, nếu tính phát hành ngoài Việt Nam
- Trình soạn màn + chia sẻ bằng mã (bàn tự tạo), dùng lại hạ tầng mã đấu đã có
