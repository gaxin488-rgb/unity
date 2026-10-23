---
name: unity-mcp
description: Điều khiển Unity Editor chạy ngầm 100% từ Antigravity (kể cả khi cửa sổ Unity đang bị thu nhỏ / Minimized). Bao gồm chức năng unity_focus_and_compile (tự nạp code mới, compile sạch 0 lỗi và reload domain ngay cả khi cửa sổ Unity bị thu nhỏ mà không nháy màn hình hay chiếm chuột), unity_capture_view (chụp Game View / Scene View / Editor Window khi đang thu nhỏ), unity_eval, unity_command, unity_invoke_menu và unity_get_console_logs.
---

# Unity Editor & Pipeline MCP (`unity-mcp`)

Plugin MCP kết nối trực tiếp Antigravity với **Unity Editor (Unity 6 / `com.unity.pipeline`)** và **Win32 Background Window Message Queue**, hoạt động **100% ngay cả khi cửa sổ Unity đang bị thu nhỏ (Minimized / `IsIconic == 1`)** hoặc bị che khuất.

## Các công cụ (Tools)

### 1. `unity_focus_and_compile` (Bấm/Focus ngầm + Compile sạch 0 lỗi cả khi thu nhỏ)
- **Mục đích**: Giải quyết triệt để tình trạng sửa file `.cs` hoặc thu nhỏ cửa sổ Unity (`IsIconic == 1`) làm Unity ngừng cập nhật:
  1. Bật `Pipeline AutoTick` (`EditorApplication.SignalTick` ở tần số 60Hz) và `Application.runInBackground = true` để vòng lặp chính của Unity Editor luôn chạy mượt ngay cả khi cửa sổ bị thu nhỏ xuống Taskbar.
  2. Gọi `AssetDatabase.Refresh()` trực tiếp qua Pipeline `eval` + `unity command recompile`.
  3. Nếu cửa sổ Unity đang bị thu nhỏ (`IsIconic == 1`) và cần gửi lệnh Win32 (`WM_ACTIVATEAPP` + `WM_COMMAND 40323`), sử dụng cơ chế **Invisible Ghost Wake** (`WS_EX_LAYERED | WS_EX_NOACTIVATE` với độ trong suốt `alpha = 1/255` + `SW_SHOWNOACTIVATE` -> gửi lệnh -> `SW_SHOWMINNOACTIVE`), giúp C++ `ContainerWndProc` nhận lệnh mà **không nháy màn hình** và **không làm mất focus** cửa sổ hiện tại của người dùng.
  4. Theo dõi các dòng mới trong `Editor.log` cho đến khi `bee_backend.exe` biên dịch xong và Pipeline Server (`port 7800`) sẵn sàng trở lại.

### 2. `unity_capture_view`
- Chụp ảnh `game` (Game View), `scene` (Scene View) thông qua off-screen `RenderTexture` của `Camera.Render()`, hoặc chụp `editor` (toàn bộ cửa sổ Unity Editor qua `ghost_unminimize` + `PrintWindow`) — hoạt động 100% kể cả khi cửa sổ Unity đang bị thu nhỏ xuống Taskbar.

### 3. `unity_status`
- Kiểm tra cửa sổ Unity Editor (`HWND`, kích thước thực tế khi khôi phục, trạng thái thu nhỏ `is_minimized`), trạng thái Pipeline Server (`http://127.0.0.1:7800`), và trạng thái compile.

### 4. `unity_eval`
- Chạy trực tiếp đoạn code C# bên trong Unity Editor đang mở (hoạt động cả khi cửa sổ đang thu nhỏ).

### 5. `unity_command`
- Thực thi bất kỳ lệnh nào trong 48 nhóm lệnh của Unity Pipeline (`scene`, `gameobject`, `component`, `editor_play`, `capture`, `recompile`, v.v.).

### 6. `unity_invoke_menu`
- Gửi lệnh `WM_COMMAND` trực tiếp tới bất kỳ mục menu nào trên thanh Menu Bar của Unity Editor (kể cả khi cửa sổ đang bị thu nhỏ nhờ `ghost_unminimize`).

### 7. `unity_get_console_logs`
- Đọc nhanh danh sách lỗi biên dịch (`error CSxxxx`), ngoại lệ (`PostProcessing failed`) và cảnh báo từ `Editor.log`.
