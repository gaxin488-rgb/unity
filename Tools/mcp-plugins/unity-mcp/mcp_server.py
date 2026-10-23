"""Unity Editor & Pipeline MCP Server for Antigravity.

Provides 100% background control of the Unity Editor on Windows — including when the Unity Editor
window is MINIMIZED (IsIconic == 1) or covered by other windows:
- unity_focus_and_compile: Simulates clicking/focusing the Unity Editor window in the background
  (via Pipeline AssetDatabase.Refresh + AutoTick + Invisible Ghost Wake WM_ACTIVATEAPP/WM_COMMAND 40323)
  so Unity refreshes AssetDatabase, compiles all C# scripts cleanly (0 errors), and reloads the domain
  even when minimized, WITHOUT moving the user's mouse, flashing the screen, or stealing window focus.
- unity_status: Inspects running Unity Editor window (including minimized state), Pipeline server status, and compile state.
- unity_eval: Executes arbitrary C# code directly inside the live Unity Editor via Unity Pipeline.
- unity_command: Executes any Unity Pipeline command (scene, gameobject, component, playmode, capture, etc.).
- unity_invoke_menu: Invokes any top-level Unity Editor menu item in the background via Win32 HMENU (even when minimized).
- unity_capture_view: Captures Game View, Scene View, or Editor Window in the background (works 100% even when minimized).
- unity_get_console_logs: Reads latest compile errors, warnings, and logs from Unity Editor.log.
"""
from __future__ import annotations

import base64
from contextlib import contextmanager
import ctypes
from ctypes import wintypes
import io
import json
import os
import re
import subprocess
import sys
import tempfile
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

# ---------------------------------------------------------------------------
# Attach to interactive desktop so sandboxed processes can reach Unity HWND
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
from PIL import Image

SERVER_NAME = "unity"
SERVER_VERSION = "1.1.0"
PROTOCOL_VERSION = "2024-11-05"

UNITY_CLI_PATH = r"C:\Users\Admin\AppData\Local\Unity\bin\unity.exe"
DEFAULT_PROJECT_PATH = r"D:\unity\OpenWorldGame"
EDITOR_LOG_PATH = os.path.expandvars(r"%LOCALAPPDATA%\Unity\Editor\Editor.log")

WM_ACTIVATE = 0x0006
WM_SETFOCUS = 0x0007
WM_ACTIVATEAPP = 0x001C
WM_COMMAND = 0x0111
MF_BYPOSITION = 0x0400
DEFAULT_REFRESH_MENU_ID = 40323

GWL_EXSTYLE = -20
WS_EX_LAYERED = 0x00080000
WS_EX_NOACTIVATE = 0x08000000
LWA_ALPHA = 0x00000002
SW_SHOWNOACTIVATE = 4
SW_SHOWMINNOACTIVE = 7

WNDENUMPROC = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)


# ---------------------------------------------------------------------------
# Invisible Ghost Wake for Minimized Windows (IsIconic == 1)
# ---------------------------------------------------------------------------

@contextmanager
def ghost_unminimize(hwnd: int, hold_seconds: float = 0.08):
    """Temporarily un-minimize a minimized window with alpha=1/255 and SW_SHOWNOACTIVATE.

    This clears WS_MINIMIZE so Unity's C++ ContainerWndProc, PrintWindow, and UIA
    process native commands and full-resolution geometry without flashing the screen
    or changing the user's foreground window / focus.
    """
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


# ---------------------------------------------------------------------------
# Win32 Unity Window & Menu Helpers
# ---------------------------------------------------------------------------

def get_normal_window_size(hwnd: int) -> tuple[int, int]:
    """Return the window's current or restored (if minimized) width and height."""
    try:
        if user32.IsIconic(hwnd):
            placement = win32gui.GetWindowPlacement(hwnd)
            if placement and len(placement) >= 5:
                rc = placement[4]
                w = max(0, rc[2] - rc[0])
                h = max(0, rc[3] - rc[1])
                if w > 0 and h > 0:
                    return w, h
    except Exception:
        pass
    rect = wintypes.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(rect))
    return max(0, rect.right - rect.left), max(0, rect.bottom - rect.top)


def find_unity_windows() -> dict[str, Any]:
    """Find the main Unity Editor window (UnityContainerWndClass) and any modal dialogs."""
    ensure_desktop()
    main_windows: list[dict[str, Any]] = []
    modal_dialogs: list[dict[str, Any]] = []

    def _enum_cb(h, _):
        try:
            if not user32.IsWindowVisible(h):
                return True
            pid = wintypes.DWORD()
            user32.GetWindowThreadProcessId(h, ctypes.byref(pid))
            try:
                proc = psutil.Process(pid.value)
                pname = proc.name()
            except Exception:
                return True

            if pname.lower() != "unity.exe":
                return True

            length = user32.GetWindowTextLengthW(h)
            buff = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(h, buff, length + 1)
            title = buff.value or ""
            cname = win32gui.GetClassName(h) or ""

            is_minimized = bool(user32.IsIconic(h))
            w, ht = get_normal_window_size(h)

            has_menu = bool(user32.GetMenu(h))
            info = {
                "hwnd": int(h),
                "title": title,
                "class_name": cname,
                "pid": int(pid.value),
                "width": w,
                "height": ht,
                "is_minimized": is_minimized,
                "has_menu": has_menu,
            }
            if cname == "UnityContainerWndClass":
                if has_menu or w >= 600:
                    main_windows.append(info)
            elif w > 50 and ht > 50 and title:
                modal_dialogs.append(info)
        except Exception:
            pass
        return True

    proc_cb = WNDENUMPROC(_enum_cb)
    user32.EnumWindows(proc_cb, 0)
    main_windows.sort(key=lambda x: (x.get("has_menu", False), x["width"] * x["height"]), reverse=True)
    return {
        "main": main_windows[0] if main_windows else None,
        "all_main": main_windows,
        "modals": modal_dialogs,
    }


def dismiss_unity_modals(modals: list[dict[str, Any]]) -> list[str]:
    """Dismiss blocking Unity modal popup dialogs in the background."""
    dismissed = []
    for m in modals:
        hwnd = m["hwnd"]
        title = m["title"]
        try:
            root = uia.ControlFromHandle(hwnd)
            clicked = False
            for ch in root.GetChildren():
                cname = (ch.Name or "").strip().lower()
                ctype = (ch.ControlTypeName or "").strip()
                if ctype == "ButtonControl" and cname in ("ok", "ignore", "continue", "yes", "close"):
                    try:
                        inv = ch.GetInvokePattern()
                        if inv:
                            inv.Invoke()
                            clicked = True
                            break
                    except Exception:
                        pass
            if not clicked:
                user32.PostMessageW(hwnd, win32con.WM_KEYDOWN, win32con.VK_RETURN, 0)
                user32.PostMessageW(hwnd, win32con.WM_KEYUP, win32con.VK_RETURN, 0)
            dismissed.append(f"{title} (HWND {hwnd})")
        except Exception:
            pass
    return dismissed


def find_menu_item_id(main_hwnd: int, top_menu_name: str, item_Display_substring: str) -> int:
    """Resolve a native Win32 HMENU command ID from Unity Editor's menu bar."""
    ensure_desktop()
    hmenu = user32.GetMenu(main_hwnd)
    if not hmenu:
        return 0
    top_count = user32.GetMenuItemCount(hmenu)
    buf = ctypes.create_unicode_buffer(256)

    for i in range(top_count):
        user32.GetMenuStringW(hmenu, i, buf, 256, MF_BYPOSITION)
        top_label = (buf.value or "").replace("&", "").strip()
        if top_menu_name.lower() in top_label.lower():
            sub = user32.GetSubMenu(hmenu, i)
            if not sub:
                continue
            sub_count = user32.GetMenuItemCount(sub)
            for j in range(sub_count):
                user32.GetMenuStringW(sub, j, buf, 256, MF_BYPOSITION)
                sub_label = (buf.value or "").replace("&", "").strip()
                if item_Display_substring.lower() in sub_label.lower():
                    mid = user32.GetMenuItemID(sub, j)
                    if mid > 0:
                        return int(mid)
                nested = user32.GetSubMenu(sub, j)
                if nested:
                    ncount = user32.GetMenuItemCount(nested)
                    for k in range(ncount):
                        user32.GetMenuStringW(nested, k, buf, 256, MF_BYPOSITION)
                        nlabel = (buf.value or "").replace("&", "").strip()
                        if item_Display_substring.lower() in nlabel.lower():
                            mid = user32.GetMenuItemID(nested, k)
                            if mid > 0:
                                return int(mid)
    return 0


def run_unity_cli(args: list[str], project_path: str = DEFAULT_PROJECT_PATH, timeout: float = 25.0) -> dict[str, Any]:
    """Run unity.exe CLI command and parse JSON output."""
    if not os.path.exists(UNITY_CLI_PATH):
        return {"ok": False, "error": f"Không tìm thấy Unity CLI tại {UNITY_CLI_PATH}"}
    cwd = project_path if os.path.isdir(project_path) else os.getcwd()
    cmd = [UNITY_CLI_PATH] + args
    if "--format" not in args and "--json" not in args:
        cmd.extend(["--format", "json"])
    try:
        proc = subprocess.run(
            cmd,
            cwd=cwd,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout,
        )
        stdout = (proc.stdout or "").strip()
        try:
            data = json.loads(stdout)
            return {"ok": proc.returncode == 0, "exit_code": proc.returncode, "data": data}
        except Exception:
            return {
                "ok": proc.returncode == 0,
                "exit_code": proc.returncode,
                "stdout": stdout,
                "stderr": (proc.stderr or "").strip(),
            }
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": f"Lệnh unity {' '.join(args)} quá thời gian chờ ({timeout}s)"}
    except Exception as exc:
        return {"ok": False, "error": str(exc)}


def parse_editor_log_tail(max_lines: int = 250, start_line: int = 0) -> dict[str, Any]:
    """Parse Editor.log for compiler errors, warnings, and ILPostProcessor failures."""
    if not os.path.exists(EDITOR_LOG_PATH):
        return {"exists": False, "errors": [], "warnings": [], "tail": []}
    try:
        with open(EDITOR_LOG_PATH, "r", encoding="utf-8", errors="replace") as f:
            lines = f.readlines()

        if start_line > 0 and start_line < len(lines):
            tail = [ln.rstrip("\r\n") for ln in lines[start_line:]]
        else:
            tail = [ln.rstrip("\r\n") for ln in lines[-max_lines:]]

        last_compile_idx = 0
        for idx, ln in enumerate(tail):
            if ("Starting:" in ln and "bee_backend.exe" in ln) or "Asset Pipeline Refresh" in ln:
                last_compile_idx = idx

        recent_slice = tail[last_compile_idx:]
        errors = []
        warnings = []
        for ln in recent_slice:
            if re.search(r":\s*error\s+CS\d+:", ln) or "Unhandled Exception:" in ln or "PostProcessing failed:" in ln:
                if ln not in errors:
                    errors.append(ln)
            elif re.search(r":\s*warning\s+CS\d+:", ln):
                if ln not in warnings:
                    warnings.append(ln)

        return {
            "exists": True,
            "total_lines": len(lines),
            "errors": errors,
            "warnings": warnings[:20],
            "warnings_count": len(warnings),
            "tail": tail[-40:],
        }
    except Exception as exc:
        return {"exists": True, "error": str(exc), "errors": [], "warnings": [], "tail": []}


# ---------------------------------------------------------------------------
# Tool Implementations
# ---------------------------------------------------------------------------

def unity_focus_and_compile(project_path: str = DEFAULT_PROJECT_PATH,
                            wait_seconds: float = 20.0,
                            dismiss_modals: bool = True) -> dict[str, Any]:
    """Simulate clicking/focusing the Unity Editor window in the background and trigger a clean compile.

    Works 100% even when the Unity Editor window is minimized (IsIconic == 1).
    """
    ensure_desktop()
    win_info = find_unity_windows()
    dismissed = []
    if dismiss_modals and win_info["modals"]:
        dismissed = dismiss_unity_modals(win_info["modals"])
        time.sleep(0.3)
        win_info = find_unity_windows()

    main_win = win_info["main"]
    if not main_win:
        return {
            "ok": False,
            "compiled_cleanly": False,
            "error": "Không tìm thấy cửa sổ Unity Editor (UnityContainerWndClass) đang mở.",
        }

    hwnd = main_win["hwnd"]
    was_minimized = bool(main_win.get("is_minimized", False))
    before_log = parse_editor_log_tail(50)
    before_lines = before_log.get("total_lines", 0)

    triggered_actions = []

    # 1. Enable Pipeline AutoTick & trigger AssetDatabase.Refresh() via Pipeline eval (works while minimized!)
    auto_tick_res = run_unity_cli(
        ["command", "editor", "set_auto_tick", "--enable", "true", "--interval-ms", "16", "--project-path", project_path],
        project_path=project_path,
        timeout=5.0,
    )
    if auto_tick_res.get("ok"):
        triggered_actions.append("Pipeline editor set_auto_tick (16ms / 60Hz)")

    eval_refresh = run_unity_cli(
        [
            "command", "eval",
            "UnityEngine.Application.runInBackground = true; UnityEditor.AssetDatabase.Refresh(); return \"REFRESHED\";",
            "--project-path", project_path,
        ],
        project_path=project_path,
        timeout=8.0,
    )
    if eval_refresh.get("ok"):
        triggered_actions.append("Pipeline eval: AssetDatabase.Refresh()")

    # 2. Send Win32 WM_ACTIVATEAPP + WM_COMMAND(Assets->Refresh)
    #    If Unity is minimized (IsIconic == 1) and Pipeline eval didn't succeed (e.g. recovering from a compile error),
    #    use ghost_unminimize so Unity's C++ ContainerWndProc processes WM_COMMAND 40323 without flashing on screen!
    refresh_id = find_menu_item_id(hwnd, "Assets", "Refresh") or DEFAULT_REFRESH_MENU_ID
    if was_minimized and not eval_refresh.get("ok"):
        with ghost_unminimize(hwnd, hold_seconds=0.08):
            user32.PostMessageW(hwnd, WM_ACTIVATEAPP, 1, 0)
            user32.PostMessageW(hwnd, WM_ACTIVATE, 1, 0)
            user32.PostMessageW(hwnd, WM_SETFOCUS, 0, 0)
            user32.PostMessageW(hwnd, WM_COMMAND, refresh_id, 0)
            time.sleep(0.18)
        triggered_actions.append(f"GhostWake(alpha=1, SW_SHOWNOACTIVATE) + WM_COMMAND(id={refresh_id}) -> HWND {hwnd}")
    else:
        user32.PostMessageW(hwnd, WM_ACTIVATEAPP, 1, 0)
        user32.PostMessageW(hwnd, WM_ACTIVATE, 1, 0)
        user32.PostMessageW(hwnd, WM_SETFOCUS, 0, 0)
        user32.PostMessageW(hwnd, WM_COMMAND, refresh_id, 0)
        triggered_actions.append(f"WM_ACTIVATEAPP(1) + WM_COMMAND(Assets->Refresh, id={refresh_id}) -> HWND {hwnd}")

    # 3. Also request script recompile via Pipeline if up
    cli_recompile = run_unity_cli(
        ["command", "recompile", "--project-path", project_path],
        project_path=project_path,
        timeout=6.0,
    )
    if cli_recompile.get("ok"):
        triggered_actions.append("unity command recompile (Pipeline API)")

    # 4. Wait for Editor.log lines AFTER before_lines to show refresh/compile completion
    deadline = time.time() + max(3.0, min(wait_seconds, 45.0))
    saw_activity = False
    compile_started = False
    start_wait = time.time()

    while time.time() < deadline:
        time.sleep(0.5)
        cur_wins = find_unity_windows()
        if dismiss_modals and cur_wins["modals"]:
            newly = dismiss_unity_modals(cur_wins["modals"])
            dismissed.extend(newly)

        if os.path.exists(EDITOR_LOG_PATH):
            try:
                with open(EDITOR_LOG_PATH, "r", encoding="utf-8", errors="replace") as f:
                    all_lines = f.readlines()
            except Exception:
                all_lines = []

            if len(all_lines) > before_lines:
                saw_activity = True
                delta_text = "".join(all_lines[before_lines:])
                if "bee_backend.exe" in delta_text or "Starting:" in delta_text:
                    compile_started = True

                # If compile started, wait until domain reload finishes or compiler error appears
                if compile_started:
                    if ("StopAssetImportingV2" in delta_text or
                            "initialDomainReloadingComplete" in delta_text or
                            "Server started on http://" in delta_text or
                            re.search(r":\s*error\s+CS\d+:", delta_text)):
                        time.sleep(0.5)
                        break
                else:
                    # Only Asset Pipeline Refresh with no script changes needed
                    if "Asset Pipeline Refresh" in delta_text and (time.time() - start_wait) >= 1.5:
                        break
            elif (time.time() - start_wait) >= 2.2 and eval_refresh.get("ok"):
                # No new log lines after 2.2s when AssetDatabase.Refresh() already returned -> nothing dirty on disk
                break

    # 5. Wait for Pipeline server to be ready after domain reload
    status_res = run_unity_cli(
        ["status", "--until-ready", "--timeout", "8", "--project-path", project_path],
        project_path=project_path,
        timeout=10.0,
    )
    if not status_res.get("ok"):
        status_res = run_unity_cli(["status", "--project-path", project_path], project_path=project_path, timeout=5.0)

    after_log = parse_editor_log_tail(250, start_line=before_lines if saw_activity else 0)
    errors = after_log.get("errors", [])
    compiled_cleanly = len(errors) == 0

    return {
        "ok": compiled_cleanly,
        "compiled_cleanly": compiled_cleanly,
        "unity_window": find_unity_windows().get("main") or main_win,
        "worked_while_minimized": was_minimized,
        "dismissed_modals": dismissed,
        "triggered_actions": triggered_actions,
        "saw_log_activity": saw_activity,
        "compile_started": compile_started,
        "log_lines_delta": after_log.get("total_lines", 0) - before_lines,
        "compile_errors": errors,
        "compile_warnings_count": after_log.get("warnings_count", 0),
        "compile_warnings_sample": after_log.get("warnings", [])[:10],
        "pipeline_status": status_res.get("data"),
    }


def unity_status(project_path: str = DEFAULT_PROJECT_PATH) -> dict[str, Any]:
    """Get comprehensive Unity Editor window, Pipeline server, and compile status."""
    win_info = find_unity_windows()
    pipe_status = run_unity_cli(["status", "--project-path", project_path], project_path=project_path, timeout=6.0)
    log_info = parse_editor_log_tail(150)
    return {
        "ok": True,
        "unity_window": win_info["main"],
        "modal_dialogs": win_info["modals"],
        "pipeline_status": pipe_status.get("data"),
        "compile_clean": len(log_info.get("errors", [])) == 0,
        "compile_errors": log_info.get("errors", []),
        "compile_warnings_count": log_info.get("warnings_count", 0),
    }


def unity_eval(code: str, project_path: str = DEFAULT_PROJECT_PATH, timeout: float = 20.0) -> dict[str, Any]:
    """Execute C# code inside the running Unity Editor via Unity Pipeline eval (works even when minimized)."""
    res = run_unity_cli(
        ["command", "eval", code, "--project-path", project_path],
        project_path=project_path,
        timeout=timeout,
    )
    win_info = find_unity_windows()
    if win_info["modals"]:
        dismissed = dismiss_unity_modals(win_info["modals"])
        res["dismissed_modals"] = dismissed
    return res


def unity_command(command_name: str, args: list[str] | None = None,
                  project_path: str = DEFAULT_PROJECT_PATH, timeout: float = 20.0) -> dict[str, Any]:
    """Run any Unity Pipeline command against the connected Editor (works even when minimized)."""
    cmd_parts = command_name.strip().split()
    cli_args = ["command"] + cmd_parts + (args or []) + ["--project-path", project_path]
    return run_unity_cli(cli_args, project_path=project_path, timeout=timeout)


def unity_invoke_menu(top_menu: str, menu_item: str, project_path: str = DEFAULT_PROJECT_PATH) -> dict[str, Any]:
    """Invoke any Unity Editor top-bar menu item in the background (works even when minimized)."""
    ensure_desktop()
    win_info = find_unity_windows()
    main_win = win_info["main"]
    if not main_win:
        return {"ok": False, "error": "Không tìm thấy cửa sổ Unity Editor đang mở."}
    hwnd = main_win["hwnd"]
    was_minimized = bool(main_win.get("is_minimized", False))

    # First try executing via EditorApplication.ExecuteMenuItem if full menu path is workable via Pipeline
    mid = find_menu_item_id(hwnd, top_menu, menu_item)
    if not mid:
        return {
            "ok": False,
            "error": f"Không tìm thấy mục menu '{top_menu} -> {menu_item}' trên thanh menu Unity Editor.",
        }

    with ghost_unminimize(hwnd, hold_seconds=0.06):
        user32.PostMessageW(hwnd, WM_ACTIVATEAPP, 1, 0)
        user32.PostMessageW(hwnd, WM_COMMAND, mid, 0)
        if was_minimized:
            time.sleep(0.15)

    return {
        "ok": True,
        "hwnd": hwnd,
        "was_minimized": was_minimized,
        "menu": f"{top_menu} -> {menu_item}",
        "menu_item_id": mid,
    }


def unity_capture_view(view: str = "game", width: int = 1280, height: int = 720,
                       project_path: str = DEFAULT_PROJECT_PATH) -> dict[str, Any]:
    """Capture Unity Game View, Scene View, or Editor Window even when the Unity window is minimized."""
    ensure_desktop()
    view_norm = (view or "game").strip().lower()
    win_info = find_unity_windows()
    main_win = win_info["main"]
    was_minimized = bool(main_win and main_win.get("is_minimized", False))

    if view_norm in ("game", "game_view", "scene", "scene_view"):
        subcmd = "game_view" if "game" in view_norm else "scene_view"
        cli_args = [
            "command", "capture", subcmd,
            "--width", str(width),
            "--height", str(height),
            "--project-path", project_path,
        ]
        if subcmd == "game_view":
            cli_args.extend(["--source", "camera"])
        res = run_unity_cli(cli_args, project_path=project_path, timeout=15.0)
        inner = (res.get("data") or {}).get("data") or {}
        payload = inner.get("result") if isinstance(inner.get("result"), dict) else inner
        b64_png = payload.get("base64") or payload.get("Base64")
        if b64_png:
            raw_bytes = base64.b64decode(b64_png)
            img = Image.open(io.BytesIO(raw_bytes)).convert("RGB")
            buf = io.BytesIO()
            img.save(buf, format="JPEG", quality=85)
            b64_jpg = base64.b64encode(buf.getvalue()).decode("ascii")
            return {
                "ok": True,
                "view": subcmd,
                "source": payload.get("source") or payload.get("Source") or subcmd,
                "width": img.width,
                "height": img.height,
                "captured_while_minimized": was_minimized,
                "data_base64": b64_jpg,
            }
        return {"ok": False, "error": "Không lấy được ảnh từ Pipeline capture.", "details": res}

    # Fallback / "editor" view: capture full Unity Editor window via ghost_unminimize + PrintWindow
    if not main_win:
        return {"ok": False, "error": "Không tìm thấy cửa sổ Unity Editor đang mở."}
    hwnd = main_win["hwnd"]
    import win32ui
    user32.PrintWindow.argtypes = [wintypes.HWND, wintypes.HDC, wintypes.UINT]
    user32.PrintWindow.restype = wintypes.BOOL

    with ghost_unminimize(hwnd, hold_seconds=0.08):
        rect = win32gui.GetWindowRect(hwnd)
        w = max(1, rect[2] - rect[0])
        h = max(1, rect[3] - rect[1])
        hwnd_dc = win32gui.GetWindowDC(hwnd)
        mfc_dc = win32ui.CreateDCFromHandle(hwnd_dc)
        save_dc = mfc_dc.CreateCompatibleDC()
        bmp = win32ui.CreateBitmap()
        bmp.CreateCompatibleBitmap(mfc_dc, w, h)
        save_dc.SelectObject(bmp)
        ok = user32.PrintWindow(hwnd, save_dc.GetSafeHdc(), 2)
        if not ok:
            user32.PrintWindow(hwnd, save_dc.GetSafeHdc(), 0)
        bmpinfo = bmp.GetInfo()
        bmpstr = bmp.GetBitmapBits(True)
        img = Image.frombuffer("RGB", (bmpinfo["bmWidth"], bmpinfo["bmHeight"]), bmpstr, "raw", "BGRX", 0, 1)
        save_dc.DeleteDC()
        mfc_dc.DeleteDC()
        win32gui.ReleaseDC(hwnd, hwnd_dc)
        win32gui.DeleteObject(bmp.GetHandle())

    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=85)
    b64 = base64.b64encode(buf.getvalue()).decode("ascii")
    return {
        "ok": True,
        "view": "editor_window",
        "width": img.width,
        "height": img.height,
        "captured_while_minimized": was_minimized,
        "data_base64": b64,
    }


# ---------------------------------------------------------------------------
# MCP Tool Definitions
# ---------------------------------------------------------------------------

TOOLS = [
    {
        "name": "unity_focus_and_compile",
        "description": (
            "Giả lập bấm vào cửa sổ Unity Editor chạy ngầm 100% (kể cả khi cửa sổ Unity đang bị THU NHỎ / Minimized) "
            "để Unity tự động nạp lại code mới, compile sạch (0 lỗi) và reload domain "
            "mà KHÔNG chiếm chuột/bàn phím, KHÔNG nháy màn hình và KHÔNG cần người dùng mở lại cửa sổ Unity."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "project_path": {
                    "type": "string",
                    "default": DEFAULT_PROJECT_PATH,
                    "description": "Đường dẫn project Unity (mặc định D:\\unity\\OpenWorldGame).",
                },
                "wait_seconds": {
                    "type": "number",
                    "default": 20.0,
                    "description": "Thời gian tối đa chờ Unity biên dịch xong (giây).",
                },
                "dismiss_modals": {
                    "type": "boolean",
                    "default": True,
                    "description": "Tự động bấm đóng các hộp thoại popup chặn luồng trong Unity (mặc định true).",
                },
            },
        },
    },
    {
        "name": "unity_status",
        "description": "Kiểm tra trạng thái cửa sổ Unity Editor (bao gồm trạng thái thu nhỏ is_minimized), kết nối Pipeline Server (port 7800) và trạng thái compile hiện tại.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "project_path": {
                    "type": "string",
                    "default": DEFAULT_PROJECT_PATH,
                    "description": "Đường dẫn project Unity.",
                }
            },
        },
    },
    {
        "name": "unity_eval",
        "description": "Thực thi trực tiếp code C# trong Unity Editor đang mở thông qua Unity Pipeline eval (hoạt động cả khi cửa sổ Unity bị thu nhỏ).",
        "inputSchema": {
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "Đoạn code C# cần chạy trong Unity Editor (ví dụ: 'return Application.unityVersion;').",
                },
                "project_path": {
                    "type": "string",
                    "default": DEFAULT_PROJECT_PATH,
                    "description": "Đường dẫn project Unity.",
                },
                "timeout": {
                    "type": "number",
                    "default": 20.0,
                    "description": "Thời gian chờ tối đa (giây).",
                },
            },
            "required": ["code"],
        },
    },
    {
        "name": "unity_command",
        "description": (
            "Thực thi bất kỳ lệnh nào trong 48 nhóm lệnh của Unity Pipeline (ví dụ: 'scene get_hierarchy', "
            "'scene save', 'editor_play', 'recompile', 'capture game_view') ngay cả khi cửa sổ Unity đang thu nhỏ."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "command_name": {
                    "type": "string",
                    "description": "Tên lệnh Pipeline (ví dụ: 'scene get_hierarchy', 'scene save', 'editor_play').",
                },
                "args": {
                    "type": "array",
                    "items": {"type": "string"},
                    "description": "Danh sách tham số bổ sung cho lệnh (tùy chọn).",
                },
                "project_path": {
                    "type": "string",
                    "default": DEFAULT_PROJECT_PATH,
                    "description": "Đường dẫn project Unity.",
                },
            },
            "required": ["command_name"],
        },
    },
    {
        "name": "unity_invoke_menu",
        "description": (
            "Kích hoạt bất kỳ mục menu nào trên thanh Menu Bar của Unity Editor (Assets -> Refresh, "
            "EotL -> Build Sandbox Scene, v.v.) chạy ngầm 100% qua Win32 HMENU (kể cả khi cửa sổ đang thu nhỏ)."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "top_menu": {
                    "type": "string",
                    "description": "Tên menu cấp 1 (ví dụ: 'Assets', 'File', 'Edit', 'EotL', 'Window').",
                },
                "menu_item": {
                    "type": "string",
                    "description": "Tên lệnh menu cần bấm (ví dụ: 'Refresh', 'Reimport', 'Build Sandbox Scene').",
                },
            },
            "required": ["top_menu", "menu_item"],
        },
    },
    {
        "name": "unity_capture_view",
        "description": (
            "Chụp ảnh Game View, Scene View (qua off-screen RenderTexture) hoặc toàn bộ cửa sổ Unity Editor "
            "chạy ngầm 100%, hoạt động ngay cả khi cửa sổ Unity đang bị thu nhỏ (Minimized)."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "view": {
                    "type": "string",
                    "enum": ["game", "scene", "editor"],
                    "default": "game",
                    "description": "Góc nhìn cần chụp: 'game' (Game View), 'scene' (Scene View), hoặc 'editor' (cửa sổ Unity Editor).",
                },
                "width": {
                    "type": "integer",
                    "default": 1280,
                    "description": "Chiều rộng ảnh (mặc định 1280).",
                },
                "height": {
                    "type": "integer",
                    "default": 720,
                    "description": "Chiều cao ảnh (mặc định 720).",
                },
                "project_path": {
                    "type": "string",
                    "default": DEFAULT_PROJECT_PATH,
                    "description": "Đường dẫn project Unity.",
                },
            },
        },
    },
    {
        "name": "unity_get_console_logs",
        "description": "Đọc danh sách lỗi biên dịch (error CSxxxx), cảnh báo và các dòng log mới nhất từ Unity Editor.log.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "max_lines": {
                    "type": "integer",
                    "default": 200,
                    "description": "Số dòng cuối của Editor.log cần phân tích.",
                }
            },
        },
    },
]


def call_tool(name: str, args: dict[str, Any]) -> list[dict[str, Any]]:
    if name == "unity_focus_and_compile":
        res = unity_focus_and_compile(
            project_path=str(args.get("project_path", DEFAULT_PROJECT_PATH)),
            wait_seconds=float(args.get("wait_seconds", 20.0)),
            dismiss_modals=bool(args.get("dismiss_modals", True)),
        )
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "unity_status":
        res = unity_status(project_path=str(args.get("project_path", DEFAULT_PROJECT_PATH)))
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "unity_eval":
        res = unity_eval(
            code=str(args.get("code", "")),
            project_path=str(args.get("project_path", DEFAULT_PROJECT_PATH)),
            timeout=float(args.get("timeout", 20.0)),
        )
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "unity_command":
        raw_args = args.get("args") or []
        str_args = [str(x) for x in raw_args] if isinstance(raw_args, list) else []
        res = unity_command(
            command_name=str(args.get("command_name", "")),
            args=str_args,
            project_path=str(args.get("project_path", DEFAULT_PROJECT_PATH)),
        )
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "unity_invoke_menu":
        res = unity_invoke_menu(
            top_menu=str(args.get("top_menu", "Assets")),
            menu_item=str(args.get("menu_item", "Refresh")),
        )
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "unity_capture_view":
        res = unity_capture_view(
            view=str(args.get("view", "game")),
            width=int(args.get("width", 1280)),
            height=int(args.get("height", 720)),
            project_path=str(args.get("project_path", DEFAULT_PROJECT_PATH)),
        )
        if res.get("ok") and "data_base64" in res:
            b64 = res.pop("data_base64")
            return [
                {"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)},
                {"type": "image", "data": b64, "mimeType": "image/jpeg"},
            ]
        return [{"type": "text", "text": json.dumps(res, ensure_ascii=False, indent=2)}]

    if name == "unity_get_console_logs":
        res = parse_editor_log_tail(max_lines=int(args.get("max_lines", 200)))
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
                "Unity Editor & Pipeline MCP Server for Antigravity. "
                "Hoạt động ngầm 100% kể cả khi cửa sổ Unity Editor đang bị thu nhỏ (minimized)."
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
                "content": [{"type": "text", "text": f"Unity MCP Error: {exc}"}],
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
