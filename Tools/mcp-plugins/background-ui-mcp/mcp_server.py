"""Background UI Automation MCP Server for Windows.

Provides true background automation without stealing mouse, keyboard, or window focus:
- Inspect UI accessibility tree directly in JSON (no screenshots / no screen capture).
- Background click via UIA Patterns (InvokePattern, TogglePattern) or Win32 PostMessage.
- Background text typing via ValuePattern or WM_SETTEXT.
- Background key events via PostMessage(WM_KEYDOWN/CHAR).
- Non-intrusive window snapshot via PrintWindow (PW_RENDERFULLCONTENT) without bringing to front.
"""
from __future__ import annotations

from contextlib import contextmanager
import base64
import ctypes
from ctypes import wintypes
import io
import json
import os
import sys
import time
from typing import Any

if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
        sys.stdin.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32

# ---------------------------------------------------------------------------
# Attach to interactive desktop BEFORE any UI or COM library initializes
# ---------------------------------------------------------------------------
_DESKTOP_ATTACHED = False

def ensure_desktop() -> bool:
    global _DESKTOP_ATTACHED
    if _DESKTOP_ATTACHED or sys.platform != "win32":
        return True
    try:
        hdesk = user32.OpenInputDesktop(0, False, 0x01FF)
        if hdesk:
            if user32.SetThreadDesktop(hdesk):
                _DESKTOP_ATTACHED = True
                return True
    except Exception:
        pass
    return False

ensure_desktop()

import psutil
import uiautomation as uia
import win32con
import win32gui
import win32process
from PIL import Image

SERVER_NAME = "background-ui-mcp"
SERVER_VERSION = "1.1.0"
PROTOCOL_VERSION = "2024-11-05"

GWL_EXSTYLE = -20
WS_EX_LAYERED = 0x00080000
WS_EX_NOACTIVATE = 0x08000000
LWA_ALPHA = 0x00000002
SW_SHOWNOACTIVATE = 4
SW_SHOWMINNOACTIVE = 7


@contextmanager
def ghost_unminimize(hwnd: int, hold_seconds: float = 0.08):
    """Temporarily un-minimize a minimized window with alpha=1/255 and SW_SHOWNOACTIVATE."""
    ensure_desktop()
    was_minimized = bool(hwnd and user32.IsWindow(hwnd) and user32.IsIconic(hwnd))
    orig_ex = 0
    if was_minimized:
        try:
            orig_ex = user32.GetWindowLongW(hwnd, GWL_EXSTYLE)
            user32.SetWindowLongW(hwnd, GWL_EXSTYLE, orig_ex | WS_EX_LAYERED | WS_EX_NOACTIVATE)
            user32.SetLayeredWindowAttributes(hwnd, 0, 1, LWA_ALPHA)
            user32.ShowWindow(hwnd, SW_SHOWNOACTIVATE)
            if hold_seconds > 0:
                time.sleep(hold_seconds)
        except Exception:
            pass
    try:
        yield was_minimized
    finally:
        if was_minimized:
            try:
                user32.ShowWindow(hwnd, SW_SHOWMINNOACTIVE)
                user32.SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA)
                user32.SetWindowLongW(hwnd, GWL_EXSTYLE, orig_ex)
            except Exception:
                pass



def wake_up_accessibility(hwnd: int):
    """Wake up Chromium/Electron/DirectX accessibility tree if needed."""
    if not hwnd or not win32gui.IsWindow(hwnd):
        return
    try:
        user32.SendMessageW(hwnd, 0x003D, 0, -4)  # WM_GETOBJECT, OBJID_CLIENT
        def _cb(child, _):
            cname = win32gui.GetClassName(child)
            if "Chrome_RenderWidgetHost" in cname or "Intermediate D3D" in cname:
                user32.SendMessageW(child, 0x003D, 0, -4)
            return True
        win32gui.EnumChildWindows(hwnd, _cb, None)
    except Exception:
        pass


WNDENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)


def find_window_handle(window_title: str = "", hwnd: int = 0) -> int:
    """Resolve a window handle from hwnd or title filter."""
    ensure_desktop()
    if hwnd and user32.IsWindow(hwnd):
        return hwnd
    if not window_title:
        return 0

    matching = []

    def _enum(h_iter, _):
        try:
            if user32.IsWindow(h_iter):
                length = user32.GetWindowTextLengthW(h_iter)
                if length > 0:
                    buff = ctypes.create_unicode_buffer(length + 1)
                    user32.GetWindowTextW(h_iter, buff, length + 1)
                    t = buff.value
                    if t and window_title.lower() in t.lower():
                        matching.append((h_iter, t, bool(user32.IsWindowVisible(h_iter))))
        except Exception:
            pass
        return True

    proc = WNDENUMPROC(_enum)
    user32.EnumWindows(proc, 0)

    for h_cand, _, vis in matching:
        if vis:
            return h_cand
    if matching:
        return matching[0][0]
    return 0


# ---------------------------------------------------------------------------
# Core Tool Implementations
# ---------------------------------------------------------------------------

def bg_list_windows(filter_title: str = "", only_visible: bool = True) -> list[dict[str, Any]]:
    """List all top-level windows with handles, titles, processes, and bounding rects."""
    ensure_desktop()
    results = []

    def enum_cb(h, _):
        try:
            if only_visible and not user32.IsWindowVisible(h):
                return True
            length = user32.GetWindowTextLengthW(h)
            if length == 0:
                return True
            buff = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(h, buff, length + 1)
            t = buff.value
            if not t:
                return True
            if filter_title and filter_title.lower() not in t.lower():
                return True

            pid = wintypes.DWORD()
            user32.GetWindowThreadProcessId(h, ctypes.byref(pid))
            pname = "unknown"
            try:
                pname = psutil.Process(pid.value).name()
            except Exception:
                pass

            rect = wintypes.RECT()
            user32.GetWindowRect(h, ctypes.byref(rect))
            is_min = bool(user32.IsIconic(h))
            w = max(0, rect.right - rect.left)
            ht = max(0, rect.bottom - rect.top)
            if is_min:
                try:
                    placement = win32gui.GetWindowPlacement(h)
                    if placement and len(placement) >= 5:
                        rc = placement[4]
                        w = max(w, rc[2] - rc[0])
                        ht = max(ht, rc[3] - rc[1])
                except Exception:
                    pass
            results.append({
                "hwnd": h,
                "title": t,
                "process_name": pname,
                "pid": pid.value,
                "is_visible": bool(user32.IsWindowVisible(h)),
                "is_minimized": is_min,
                "rect": {
                    "left": rect.left,
                    "top": rect.top,
                    "right": rect.right,
                    "bottom": rect.bottom,
                    "width": w,
                    "height": ht,
                }
            })
        except Exception:
            pass
        return True

    proc = WNDENUMPROC(enum_cb)
    user32.EnumWindows(proc, 0)
    return results


def bg_get_ui_tree(window_title: str = "", hwnd: int = 0, max_depth: int = 3, max_elements: int = 120) -> dict[str, Any]:
    """Inspect UI hierarchy of a window directly without taking screenshots."""
    ensure_desktop()
    target_hwnd = find_window_handle(window_title, hwnd)
    if not target_hwnd:
        return {"ok": False, "error": f"Không tìm thấy cửa sổ với title='{window_title}' hoặc hwnd={hwnd}"}

    wake_up_accessibility(target_hwnd)
    time.sleep(0.05)

    try:
        root_ctrl = uia.ControlFromHandle(target_hwnd)
    except Exception as exc:
        return {"ok": False, "error": f"Không thể kết nối UIA Control cho HWND {target_hwnd}: {exc}"}

    counter = [0]

    def _dump_node(ctrl, depth):
        if counter[0] >= max_elements:
            return None
        counter[0] += 1

        patterns = []
        try:
            if ctrl.GetInvokePattern(): patterns.append("Invoke")
        except Exception: pass
        try:
            if ctrl.GetValuePattern(): patterns.append("Value")
        except Exception: pass
        try:
            if ctrl.GetTogglePattern(): patterns.append("Toggle")
        except Exception: pass
        try:
            if ctrl.GetSelectionItemPattern(): patterns.append("SelectionItem")
        except Exception: pass
        try:
            if ctrl.GetLegacyIAccessiblePattern(): patterns.append("LegacyIAccessible")
        except Exception: pass

        val = ""
        try:
            vp = ctrl.GetValuePattern()
            if vp:
                val = vp.Value or ""
            elif ctrl.GetLegacyIAccessiblePattern():
                val = ctrl.GetLegacyIAccessiblePattern().Value or ""
        except Exception:
            pass

        rect = None
        try:
            b = ctrl.BoundingRectangle
            if b:
                rect = {
                    "left": b.left, "top": b.top, "right": b.right, "bottom": b.bottom,
                    "width": b.width(), "height": b.height()
                }
        except Exception:
            pass

        node = {
            "name": ctrl.Name if ctrl.Name else "",
            "control_type": ctrl.ControlTypeName,
            "automation_id": ctrl.AutomationId if ctrl.AutomationId else "",
            "class_name": ctrl.ClassName if ctrl.ClassName else "",
            "patterns": patterns,
        }
        if val:
            node["value"] = val
        if rect:
            node["rect"] = rect

        if depth < max_depth and counter[0] < max_elements:
            children = []
            try:
                for child in ctrl.GetChildren():
                    c_node = _dump_node(child, depth + 1)
                    if c_node:
                        children.append(c_node)
                    if counter[0] >= max_elements:
                        break
            except Exception:
                pass
            if children:
                node["children"] = children

        return node

    tree = _dump_node(root_ctrl, 0)
    return {
        "ok": True,
        "window": {
            "hwnd": target_hwnd,
            "title": win32gui.GetWindowText(target_hwnd),
            "class_name": win32gui.GetClassName(target_hwnd),
        },
        "total_elements": counter[0],
        "tree": tree,
    }


def _find_matching_control(root_ctrl, name: str = "", automation_id: str = "", control_type: str = ""):
    """Find a control in the UIA tree matching criteria."""
    name_lower = name.lower().strip() if name else ""
    aid_lower = automation_id.lower().strip() if automation_id else ""
    ctype_lower = control_type.lower().strip() if control_type else ""

    candidates = []

    def _walk(ctrl, depth=0):
        if depth > 10:
            return
        c_name = (ctrl.Name or "").lower().strip()
        c_aid = (ctrl.AutomationId or "").lower().strip()
        c_type = (ctrl.ControlTypeName or "").lower().strip()

        match = True
        if name_lower and name_lower not in c_name:
            match = False
        if aid_lower and aid_lower != c_aid and aid_lower not in c_aid:
            match = False
        if ctype_lower and ctype_lower not in c_type:
            match = False

        if match and (name_lower or aid_lower or ctype_lower):
            candidates.append(ctrl)
            return

        try:
            for ch in ctrl.GetChildren():
                _walk(ch, depth + 1)
        except Exception:
            pass

    _walk(root_ctrl, 0)
    return candidates[0] if candidates else None


def bg_click(window_title: str = "", hwnd: int = 0, name: str = "", automation_id: str = "",
             control_type: str = "", x: int = 0, y: int = 0, method: str = "auto") -> dict[str, Any]:
    """Click a button or control without bringing window to front and without moving physical mouse."""
    ensure_desktop()
    target_hwnd = find_window_handle(window_title, hwnd)
    if not target_hwnd:
        return {"ok": False, "error": f"Không tìm thấy cửa sổ với title='{window_title}' hoặc hwnd={hwnd}"}

    wake_up_accessibility(target_hwnd)

    # Mode 1: Specified x, y directly relative to window client area
    if x > 0 or y > 0:
        lparam = (y << 16) | (x & 0xFFFF)
        win32gui.PostMessage(target_hwnd, win32con.WM_LBUTTONDOWN, win32con.MK_LBUTTON, lparam)
        time.sleep(0.04)
        win32gui.PostMessage(target_hwnd, win32con.WM_LBUTTONUP, 0, lparam)
        return {
            "ok": True,
            "method": "PostMessage WM_LBUTTONDOWN/UP",
            "coords": {"x": x, "y": y},
            "hwnd": target_hwnd,
        }

    # Mode 2: Search by element name, automation_id, or control_type
    try:
        root_ctrl = uia.ControlFromHandle(target_hwnd)
        target_ctrl = _find_matching_control(root_ctrl, name=name, automation_id=automation_id, control_type=control_type)
    except Exception as exc:
        target_ctrl = None

    if target_ctrl is None:
        return {
            "ok": False,
            "error": f"Không tìm thấy control với name='{name}', automation_id='{automation_id}', control_type='{control_type}'"
        }

    # Try UIA pattern first
    if method in ("uia", "auto"):
        try:
            inv = target_ctrl.GetInvokePattern()
            if inv:
                inv.Invoke()
                return {"ok": True, "method": "UIA InvokePattern", "element": target_ctrl.Name}
        except Exception:
            pass

        try:
            tog = target_ctrl.GetTogglePattern()
            if tog:
                tog.Toggle()
                return {"ok": True, "method": "UIA TogglePattern", "element": target_ctrl.Name}
        except Exception:
            pass

        try:
            sel = target_ctrl.GetSelectionItemPattern()
            if sel:
                sel.Select()
                return {"ok": True, "method": "UIA SelectionItemPattern", "element": target_ctrl.Name}
        except Exception:
            pass

        try:
            legacy = target_ctrl.GetLegacyIAccessiblePattern()
            if legacy:
                legacy.DoDefaultAction()
                return {"ok": True, "method": "UIA LegacyIAccessiblePattern", "element": target_ctrl.Name}
        except Exception:
            pass

    # Fallback to PostMessage using element center coordinates
    rect = target_ctrl.BoundingRectangle
    if rect and rect.width() > 0 and rect.height() > 0:
        cx = (rect.left + rect.right) // 2
        cy = (rect.top + rect.bottom) // 2
        pt = win32gui.ScreenToClient(target_hwnd, (cx, cy))
        lparam = (pt[1] << 16) | (pt[0] & 0xFFFF)
        win32gui.PostMessage(target_hwnd, win32con.WM_LBUTTONDOWN, win32con.MK_LBUTTON, lparam)
        time.sleep(0.04)
        win32gui.PostMessage(target_hwnd, win32con.WM_LBUTTONUP, 0, lparam)
        return {
            "ok": True,
            "method": "PostMessage (control center)",
            "element": target_ctrl.Name,
            "client_pos": {"x": pt[0], "y": pt[1]}
        }

    return {"ok": False, "error": "Control không hỗ trợ UIA Pattern và không xác định được tọa độ BoundingRectangle"}


def bg_set_text(window_title: str = "", hwnd: int = 0, name: str = "", automation_id: str = "",
                control_type: str = "EditControl", text: str = "") -> dict[str, Any]:
    """Set text into an edit field in the background without stealing keyboard focus."""
    ensure_desktop()
    target_hwnd = find_window_handle(window_title, hwnd)
    if not target_hwnd:
        return {"ok": False, "error": f"Không tìm thấy cửa sổ với title='{window_title}' hoặc hwnd={hwnd}"}

    wake_up_accessibility(target_hwnd)

    try:
        root_ctrl = uia.ControlFromHandle(target_hwnd)
        target_ctrl = _find_matching_control(root_ctrl, name=name, automation_id=automation_id, control_type=control_type)
    except Exception as exc:
        target_ctrl = None

    if target_ctrl:
        try:
            vp = target_ctrl.GetValuePattern()
            if vp:
                vp.SetValue(text)
                return {"ok": True, "method": "UIA ValuePattern.SetValue", "element": target_ctrl.Name}
        except Exception:
            pass

        try:
            legacy = target_ctrl.GetLegacyIAccessiblePattern()
            if legacy:
                legacy.SetValue(text)
                return {"ok": True, "method": "UIA LegacyIAccessible.SetValue", "element": target_ctrl.Name}
        except Exception:
            pass

    # Fallback to WM_SETTEXT
    try:
        win32gui.SendMessage(target_hwnd, win32con.WM_SETTEXT, 0, text)
        return {"ok": True, "method": "Win32 SendMessage WM_SETTEXT"}
    except Exception as exc:
        return {"ok": False, "error": f"Không thể set text: {exc}"}


def bg_send_keys(window_title: str = "", hwnd: int = 0, key: str = "ENTER",
                 text: str = "") -> dict[str, Any]:
    """Send keystrokes or text to a background window via Win32 message queue."""
    ensure_desktop()
    target_hwnd = find_window_handle(window_title, hwnd)
    if not target_hwnd:
        return {"ok": False, "error": f"Không tìm thấy cửa sổ với title='{window_title}' hoặc hwnd={hwnd}"}

    VK_MAP = {
        "ENTER": win32con.VK_RETURN,
        "RETURN": win32con.VK_RETURN,
        "TAB": win32con.VK_TAB,
        "ESC": win32con.VK_ESCAPE,
        "ESCAPE": win32con.VK_ESCAPE,
        "SPACE": win32con.VK_SPACE,
        "BACKSPACE": win32con.VK_BACK,
        "DELETE": win32con.VK_DELETE,
        "UP": win32con.VK_UP,
        "DOWN": win32con.VK_DOWN,
        "LEFT": win32con.VK_LEFT,
        "RIGHT": win32con.VK_RIGHT,
        "HOME": win32con.VK_HOME,
        "END": win32con.VK_END,
        "F1": win32con.VK_F1, "F2": win32con.VK_F2, "F3": win32con.VK_F3,
        "F4": win32con.VK_F4, "F5": win32con.VK_F5, "F6": win32con.VK_F6,
        "F7": win32con.VK_F7, "F8": win32con.VK_F8, "F9": win32con.VK_F9,
        "F10": win32con.VK_F10, "F11": win32con.VK_F11, "F12": win32con.VK_F12,
    }

    if text:
        # Send character by character via WM_CHAR
        for char in text:
            win32gui.PostMessage(target_hwnd, win32con.WM_CHAR, ord(char), 0)
            time.sleep(0.01)
        return {"ok": True, "method": "PostMessage WM_CHAR", "characters_sent": len(text)}

    key_upper = key.upper().strip()
    vk = VK_MAP.get(key_upper)
    if vk is None:
        return {"ok": False, "error": f"Phím '{key}' không được hỗ trợ trong VK_MAP"}

    win32gui.PostMessage(target_hwnd, win32con.WM_KEYDOWN, vk, 0)
    time.sleep(0.03)
    win32gui.PostMessage(target_hwnd, win32con.WM_KEYUP, vk, 0)
    return {"ok": True, "method": "PostMessage WM_KEYDOWN/UP", "key": key_upper}


def bg_read_text(window_title: str = "", hwnd: int = 0) -> dict[str, Any]:
    """Read all text content currently inside a window without screenshots or OCR."""
    ensure_desktop()
    target_hwnd = find_window_handle(window_title, hwnd)
    if not target_hwnd:
        return {"ok": False, "error": f"Không tìm thấy cửa sổ với title='{window_title}' hoặc hwnd={hwnd}"}

    wake_up_accessibility(target_hwnd)

    try:
        root_ctrl = uia.ControlFromHandle(target_hwnd)
    except Exception as exc:
        return {"ok": False, "error": f"Lỗi UIA: {exc}"}

    texts = []
    def _collect(ctrl, depth=0):
        if depth > 10:
            return
        name = ctrl.Name
        if name and name.strip():
            texts.append(name.strip())
        val = ""
        try:
            vp = ctrl.GetValuePattern()
            if vp and vp.Value:
                texts.append(vp.Value.strip())
        except Exception:
            pass
        try:
            for ch in ctrl.GetChildren():
                _collect(ch, depth + 1)
        except Exception:
            pass

    _collect(root_ctrl, 0)
    # Deduplicate sequential identical lines
    clean_lines = []
    for t in texts:
        if not clean_lines or clean_lines[-1] != t:
            clean_lines.append(t)

    return {
        "ok": True,
        "window": win32gui.GetWindowText(target_hwnd),
        "total_lines": len(clean_lines),
        "content": "\n".join(clean_lines)
    }


def bg_capture_window(window_title: str = "", hwnd: int = 0) -> dict[str, Any]:
    """Capture a snapshot of a background window using PrintWindow (even when minimized, without bringing to front)."""
    ensure_desktop()
    target_hwnd = find_window_handle(window_title, hwnd)
    if not target_hwnd:
        return {"ok": False, "error": f"Không tìm thấy cửa sổ với title='{window_title}' hoặc hwnd={hwnd}"}

    import win32ui
    user32.PrintWindow.argtypes = [wintypes.HWND, wintypes.HDC, wintypes.UINT]
    user32.PrintWindow.restype = wintypes.BOOL

    with ghost_unminimize(target_hwnd, hold_seconds=0.08) as was_minimized:
        rect = win32gui.GetWindowRect(target_hwnd)
        width = rect[2] - rect[0]
        height = rect[3] - rect[1]
        if width <= 0 or height <= 0:
            return {"ok": False, "error": "Cửa sổ có kích thước 0"}

        hwnd_dc = win32gui.GetWindowDC(target_hwnd)
        mfc_dc = win32ui.CreateDCFromHandle(hwnd_dc)
        save_dc = mfc_dc.CreateCompatibleDC()
        save_bitmap = win32ui.CreateBitmap()
        save_bitmap.CreateCompatibleBitmap(mfc_dc, width, height)
        save_dc.SelectObject(save_bitmap)

        # PW_RENDERFULLCONTENT = 2
        res = user32.PrintWindow(target_hwnd, save_dc.GetSafeHdc(), 2)
        if not res:
            user32.PrintWindow(target_hwnd, save_dc.GetSafeHdc(), 0)

        bmpinfo = save_bitmap.GetInfo()
        bmpstr = save_bitmap.GetBitmapBits(True)
        img = Image.frombuffer('RGB', (bmpinfo['bmWidth'], bmpinfo['bmHeight']), bmpstr, 'raw', 'BGRX', 0, 1)

        save_dc.DeleteDC()
        mfc_dc.DeleteDC()
        win32gui.ReleaseDC(target_hwnd, hwnd_dc)
        win32gui.DeleteObject(save_bitmap.GetHandle())

    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=85)
    b64 = base64.b64encode(buf.getvalue()).decode("ascii")

    return {
        "ok": True,
        "width": width,
        "height": height,
        "captured_while_minimized": was_minimized,
        "format": "jpeg",
        "data_base64": b64,
        "note": "Ảnh chụp ngầm bằng PrintWindow (hoạt động cả khi cửa sổ đang thu nhỏ, không chiếm chuột/phím)."
    }


# ---------------------------------------------------------------------------
# MCP Tool Schemas
# ---------------------------------------------------------------------------

TOOLS = [
    {
        "name": "bg_list_windows",
        "description": "Liệt kê tất cả các cửa sổ đang mở trên Windows (HWND, tiêu đề, tên tiến trình, kích thước, trạng thái hiển thị). Dùng để lấy danh sách cửa sổ trước khi thao tác ngầm.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "filter_title": {
                    "type": "string",
                    "description": "Lọc theo từ khóa trong tiêu đề cửa sổ (không phân biệt hoa thường)."
                },
                "only_visible": {
                    "type": "boolean",
                    "description": "Chỉ lấy các cửa sổ đang hiển thị (mặc định true).",
                    "default": True
                }
            }
        }
    },
    {
        "name": "bg_get_ui_tree",
        "description": "Đọc toàn bộ cây giao diện (Accessibility Tree) của cửa sổ mục tiêu dạng JSON mà KHÔNG CẦN CHỤP MÀN HÌNH. Trả về chi tiết các nút bấm, ô nhập liệu, văn bản, automation_id và các Pattern hỗ trợ (Invoke, Value).",
        "inputSchema": {
            "type": "object",
            "properties": {
                "window_title": {
                    "type": "string",
                    "description": "Tiêu đề hoặc từ khóa tiêu đề của cửa sổ mục tiêu."
                },
                "hwnd": {
                    "type": "integer",
                    "description": "Handle của cửa sổ mục tiêu (nếu đã có từ bg_list_windows)."
                },
                "max_depth": {
                    "type": "integer",
                    "description": "Độ sâu duyệt cây giao diện (mặc định 3, tối đa 8).",
                    "default": 3
                },
                "max_elements": {
                    "type": "integer",
                    "description": "Số lượng phần tử tối đa trả về để tránh quá tải token (mặc định 120).",
                    "default": 120
                }
            }
        }
    },
    {
        "name": "bg_click",
        "description": "Bấm nút hoặc click vào phần tử trong cửa sổ CHẠY NGẦM 100%. KHÔNG làm giật chuột, KHÔNG di chuyển con trỏ chuột vật lý của người dùng, KHÔNG đè cửa sổ lên trên.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "window_title": {
                    "type": "string",
                    "description": "Tiêu đề của cửa sổ."
                },
                "hwnd": {
                    "type": "integer",
                    "description": "Handle của cửa sổ."
                },
                "name": {
                    "type": "string",
                    "description": "Tên hoặc nhãn của nút/phần tử (ví dụ: 'Save', 'OK', 'Send')."
                },
                "automation_id": {
                    "type": "string",
                    "description": "AutomationId của phần tử."
                },
                "control_type": {
                    "type": "string",
                    "description": "Loại phần tử (ví dụ: 'ButtonControl', 'MenuItemControl', 'PaneControl'). Mặc định rỗng.",
                    "default": ""
                },
                "x": {
                    "type": "integer",
                    "description": "Tọa độ X tương đối trong cửa sổ (nếu muốn click theo tọa độ ngầm qua PostMessage).",
                    "default": 0
                },
                "y": {
                    "type": "integer",
                    "description": "Tọa độ Y tương đối trong cửa sổ.",
                    "default": 0
                },
                "method": {
                    "type": "string",
                    "enum": ["auto", "uia", "postmessage"],
                    "description": "Phương thức: 'auto' (thử UIA Invoke trước, fallback sang PostMessage), 'uia', hoặc 'postmessage'.",
                    "default": "auto"
                }
            }
        }
    },
    {
        "name": "bg_set_text",
        "description": "Gán văn bản vào ô nhập liệu/textbox CHẠY NGẦM. Sử dụng ValuePattern hoặc WM_SETTEXT. KHÔNG gõ phím vật lý, người dùng vẫn đang gõ phím ở cửa sổ khác bình thường không bị ảnh hưởng.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "window_title": {
                    "type": "string",
                    "description": "Tiêu đề của cửa sổ."
                },
                "hwnd": {
                    "type": "integer",
                    "description": "Handle của cửa sổ."
                },
                "name": {
                    "type": "string",
                    "description": "Tên hoặc nhãn của ô nhập liệu."
                },
                "automation_id": {
                    "type": "string",
                    "description": "AutomationId của ô nhập liệu."
                },
                "control_type": {
                    "type": "string",
                    "description": "Loại control (mặc định 'EditControl').",
                    "default": "EditControl"
                },
                "text": {
                    "type": "string",
                    "description": "Nội dung văn bản cần điền."
                }
            },
            "required": ["text"]
        }
    },
    {
        "name": "bg_send_keys",
        "description": "Gửi phím điều khiển (ENTER, TAB, ESC, BACKSPACE, mũi tên, F1-F12...) hoặc chuỗi ký tự vào cửa sổ ngầm qua PostMessage mà không cần active cửa sổ.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "window_title": {
                    "type": "string",
                    "description": "Tiêu đề của cửa sổ."
                },
                "hwnd": {
                    "type": "integer",
                    "description": "Handle của cửa sổ."
                },
                "key": {
                    "type": "string",
                    "description": "Phím cần gửi: ENTER, TAB, ESC, SPACE, BACKSPACE, DELETE, UP, DOWN, LEFT, RIGHT, F1-F12."
                },
                "text": {
                    "type": "string",
                    "description": "Nếu muốn gửi trực tiếp chuỗi ký tự liên tiếp qua WM_CHAR."
                }
            }
        }
    },
    {
        "name": "bg_read_text",
        "description": "Đọc toàn bộ văn bản đang hiển thị trong một cửa sổ ngầm (tổng hợp từ tất cả các thẻ Text, Label, Edit) mà KHÔNG cần chụp ảnh hay OCR.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "window_title": {
                    "type": "string",
                    "description": "Tiêu đề cửa sổ."
                },
                "hwnd": {
                    "type": "integer",
                    "description": "Handle cửa sổ."
                }
            }
        }
    },
    {
        "name": "bg_capture_window",
        "description": "Chụp snapshot cửa sổ mục tiêu bằng PrintWindow ngầm (không đưa cửa sổ lên trên, kể cả khi cửa sổ bị che khuất). Trả về ảnh base64.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "window_title": {
                    "type": "string",
                    "description": "Tiêu đề cửa sổ."
                },
                "hwnd": {
                    "type": "integer",
                    "description": "Handle cửa sổ."
                }
            }
        }
    },
    {
        "name": "bg_unity_focus_and_compile",
        "description": "Giả lập bấm/focus vào cửa sổ Unity Editor chạy ngầm 100% (WM_ACTIVATEAPP + WM_COMMAND Assets->Refresh Ctrl+R + Pipeline recompile) để Unity tự động nạp lại code, compile sạch (0 lỗi) và reload domain mà không chiếm chuột hay cần người dùng bấm vào cửa sổ Unity.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "project_path": {
                    "type": "string",
                    "default": "D:\\unity\\OpenWorldGame",
                    "description": "Đường dẫn project Unity."
                },
                "wait_seconds": {
                    "type": "number",
                    "default": 15.0,
                    "description": "Thời gian chờ tối đa (giây)."
                },
                "dismiss_modals": {
                    "type": "boolean",
                    "default": True,
                    "description": "Tự động đóng các hộp thoại popup chặn luồng trong Unity."
                }
            }
        }
    }
]


# ---------------------------------------------------------------------------
# MCP Dispatcher & Protocol Loop
# ---------------------------------------------------------------------------

def call_tool(name: str, args: dict[str, Any]) -> list[dict[str, Any]]:
    if name == "bg_list_windows":
        res = bg_list_windows(
            filter_title=str(args.get("filter_title", "")),
            only_visible=bool(args.get("only_visible", True))
        )
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "bg_get_ui_tree":
        res = bg_get_ui_tree(
            window_title=str(args.get("window_title", "")),
            hwnd=int(args.get("hwnd", 0)),
            max_depth=int(args.get("max_depth", 3)),
            max_elements=int(args.get("max_elements", 120))
        )
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "bg_click":
        res = bg_click(
            window_title=str(args.get("window_title", "")),
            hwnd=int(args.get("hwnd", 0)),
            name=str(args.get("name", "")),
            automation_id=str(args.get("automation_id", "")),
            control_type=str(args.get("control_type", "")),
            x=int(args.get("x", 0)),
            y=int(args.get("y", 0)),
            method=str(args.get("method", "auto"))
        )
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "bg_set_text":
        res = bg_set_text(
            window_title=str(args.get("window_title", "")),
            hwnd=int(args.get("hwnd", 0)),
            name=str(args.get("name", "")),
            automation_id=str(args.get("automation_id", "")),
            control_type=str(args.get("control_type", "EditControl")),
            text=str(args.get("text", ""))
        )
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "bg_send_keys":
        res = bg_send_keys(
            window_title=str(args.get("window_title", "")),
            hwnd=int(args.get("hwnd", 0)),
            key=str(args.get("key", "ENTER")),
            text=str(args.get("text", ""))
        )
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "bg_read_text":
        res = bg_read_text(
            window_title=str(args.get("window_title", "")),
            hwnd=int(args.get("hwnd", 0))
        )
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "bg_capture_window":
        res = bg_capture_window(
            window_title=str(args.get("window_title", "")),
            hwnd=int(args.get("hwnd", 0))
        )
        if res.get("ok") and "data_base64" in res:
            b64 = res.pop("data_base64")
            return [
                {"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)},
                {"type": "image", "data": b64, "mimeType": "image/jpeg"}
            ]
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "bg_unity_focus_and_compile":
        unity_plugin_dir = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "unity-mcp"))
        if unity_plugin_dir not in sys.path:
            sys.path.insert(0, unity_plugin_dir)
        import mcp_server as unity_mcp_mod
        res = unity_mcp_mod.unity_focus_and_compile(
            project_path=str(args.get("project_path", r"D:\unity\OpenWorldGame")),
            wait_seconds=float(args.get("wait_seconds", 15.0)),
            dismiss_modals=bool(args.get("dismiss_modals", True)),
        )
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    raise ValueError(f"Tool không tồn tại: {name}")


def response(request_id: Any, result: Any) -> dict[str, Any]:
    return {"jsonrpc": "2.0", "id": request_id, "result": result}


def error_response(request_id: Any, code: int, message: str) -> dict[str, Any]:
    return {"jsonrpc": "2.0", "id": request_id, "error": {"code": code, "message": message}}


def handle(message: dict[str, Any]) -> dict[str, Any] | None:
    method = message.get("method")
    request_id = message.get("id")
    if request_id is None and method and method.startswith("notifications/"):
        return None
    if method == "initialize":
        return response(request_id, {
            "protocolVersion": PROTOCOL_VERSION,
            "capabilities": {"tools": {"listChanged": False}},
            "serverInfo": {"name": SERVER_NAME, "version": SERVER_VERSION},
            "instructions": (
                "Background UI Automation MCP Server for Windows. "
                "Thao tác ngầm 100% không chiếm chuột, không chiếm bàn phím, không làm giật cửa sổ. "
                "Đọc cấu trúc giao diện trực tiếp không cần chụp màn hình."
            ),
        })
    if method == "ping":
        return response(request_id, {})
    if method == "tools/list":
        return response(request_id, {"tools": TOOLS})
    if method == "tools/call":
        params = message.get("params") or {}
        name = str(params.get("name", ""))
        args = params.get("arguments") or {}
        try:
            content = call_tool(name, args)
            return response(request_id, {"content": content, "isError": False})
        except Exception as exc:
            return response(request_id, {
                "content": [{"type": "text", "text": f"Background UI MCP Error: {exc}"}],
                "isError": True,
            })
    if request_id is not None:
        return error_response(request_id, -32601, f"Method not found: {method}")
    return None


def main() -> int:
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except AttributeError:
        pass
    for raw_line in sys.stdin.buffer:
        if not raw_line.strip():
            continue
        try:
            message = json.loads(raw_line.decode("utf-8"))
            if not isinstance(message, dict):
                continue
            result = handle(message)
            if result is not None:
                payload = json.dumps(result, ensure_ascii=False, separators=(",", ":")) + "\n"
                sys.stdout.buffer.write(payload.encode("utf-8"))
                sys.stdout.buffer.flush()
        except json.JSONDecodeError as exc:
            payload = json.dumps(error_response(None, -32700, f"Invalid JSON: {exc}"), ensure_ascii=False) + "\n"
            sys.stdout.buffer.write(payload.encode("utf-8"))
            sys.stdout.buffer.flush()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
