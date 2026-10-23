---
name: background-ui-mcp
description: "Tự động hóa và điều khiển giao diện phần mềm Windows 100% chạy ngầm (Background Automation): đọc cây giao diện trực tiếp (không chụp màn hình), click nút ngầm (không di chuyển chuột), nhập text ngầm (không chiếm bàn phím), và gửi phím tắt mà không làm mất focus cửa sổ hiện tại của người dùng."
---

# Background UI Automation (Windows)

Skill điều khiển và thao tác giao diện ứng dụng Windows chạy ngầm hoàn toàn, giải quyết triệt để 2 nhược điểm của Computer Use thông thường:
1. **Không chiếm quyền chuột/bàn phím/màn hình:** Người dùng vẫn làm việc bình thường, cửa sổ mục tiêu không bị kéo lên đè màn hình, chuột không bị giật.
2. **Không cần chụp màn hình liên tục:** Đọc trực tiếp cấu trúc cây giao diện (Accessibility Tree) dạng JSON nhẹ, chính xác 100%, không tốn tài nguyên GPU/VRAM.

## Danh sách công cụ (Tools)

### 1. `bg_list_windows`
Liệt kê các cửa sổ đang mở trên Windows.
* **Tham số:**
  * `filter_title` (string, tùy chọn): Lọc theo tiêu đề cửa sổ.
  * `only_visible` (bool, mặc định `true`): Chỉ lấy các cửa sổ đang hiển thị.
* **Trả về:** Danh sách HWND, ProcessName, Title, Bounding Rect.

### 2. `bg_get_ui_tree`
Đọc toàn bộ các thành phần giao diện (nút bấm, ô nhập text, danh sách, checkbox...) dạng JSON.
* **Tham số:**
  * `window_title` hoặc `hwnd`: Cửa sổ cần đọc.
  * `max_depth` (int, mặc định 3): Độ sâu duyệt cây.
  * `max_elements` (int, mặc định 120): Số phần tử tối đa để tránh quá tải token.
* **Ưu điểm:** Tốc độ mili-giây, không tốn token ảnh, không lo nhận diện sai pixel.

### 3. `bg_click`
Bấm nút hoặc click vào phần tử chạy ngầm.
* **Tham số:**
  * `window_title` hoặc `hwnd`: Cửa sổ mục tiêu.
  * `name` (string): Tên nút hiển thị (ví dụ: "Lưu", "OK", "Submit").
  * `automation_id` (string): ID phần tử (nếu có).
  * `x`, `y` (int, tùy chọn): Tọa độ tương đối nếu muốn click theo tọa độ qua PostMessage.
  * `method`: `"auto"` (thử UIA Invoke trước, fallback PostMessage), `"uia"`, hoặc `"postmessage"`.
* **Cơ chế:** Dùng `InvokePattern` hoặc `WM_LBUTTONDOWN/UP` gửi vào hàng đợi message ngầm. **Con trỏ chuột vật lý của người dùng đứng yên 100%**.

### 4. `bg_set_text`
Điền nội dung vào ô nhập liệu ngầm.
* **Tham số:**
  * `window_title` hoặc `hwnd`.
  * `name` hoặc `automation_id`: Tên hoặc ID ô input.
  * `text`: Nội dung cần gán.
* **Cơ chế:** Dùng `ValuePattern.SetValue` hoặc `WM_SETTEXT`. **Bàn phím vật lý của người dùng không bị gõ gián đoạn**.

### 5. `bg_send_keys`
Gửi phím điều khiển (ENTER, TAB, ESC, BACKSPACE, F1-F12...) hoặc chuỗi ký tự qua `PostMessage`.
* **Tham số:**
  * `window_title` hoặc `hwnd`.
  * `key`: Phím cần gửi.

### 6. `bg_read_text`
Đọc toàn bộ nội dung chữ đang có trong cửa sổ mà không cần chụp ảnh OCR.

### 7. `bg_capture_window`
Chụp ảnh nhanh của cửa sổ bằng `PrintWindow` ngầm (dành cho trường hợp người dùng hoặc AI muốn xem ảnh trực quan mà không cần đưa cửa sổ lên trên).

### 8. `bg_unity_focus_and_compile`
Giả lập thao tác bấm/focus vào cửa sổ Unity Editor chạy ngầm 100% (`WM_ACTIVATEAPP` + `WM_COMMAND Assets->Refresh` ID `40323` + `unity command recompile`) để Unity tự động nạp lại code mới, compile sạch (0 lỗi) và reload domain mà **không chiếm chuột/bàn phím** và **tuyệt đối không cần yêu cầu người dùng bấm vào cửa sổ Unity**.
