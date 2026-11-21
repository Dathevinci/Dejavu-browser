using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DejavuBrowser
{
    internal sealed class OverlayPopup : Form
    {
        private readonly Form _ownerForm;
        private readonly WebView2 _wv;
        private bool _ready;
        private string _pendingJson;
        private readonly bool _dismissOnDeactivate;
        private readonly bool _neverActivate;
        private bool _everShown;
        private bool _isOpen;
        private bool _hiding;
        private int _showGeneration;
        private bool _lastSurfaceLight;

        public bool IsPopupOpen
        {
            get { return _isOpen; }
        }

        public event Action<string> MessageReceived;
        public event Action PopupHidden;
        public event Func<Keys, bool> PreviewShortcut;

        public OverlayPopup(Form owner, bool dismissOnDeactivate)
            : this(owner, dismissOnDeactivate, false)
        {
        }

        public OverlayPopup(Form owner, bool dismissOnDeactivate, bool neverActivate)
        {
            _ownerForm = owner;
            _dismissOnDeactivate = dismissOnDeactivate;
            _neverActivate = neverActivate;
            Owner = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(12, 10, 22);
            TopMost = false;

            _wv = new WebView2();
            _wv.Dock = DockStyle.Fill;
            _wv.DefaultBackgroundColor = Color.FromArgb(12, 10, 22);
            _wv.KeyDown += (s, e) =>
            {
                if (e == null || e.Handled) return;
                Func<Keys, bool> handler = PreviewShortcut;
                if (handler != null && handler(e.KeyData))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            Controls.Add(_wv);
        }

        protected override bool ShowWithoutActivation
        {
            get { return _neverActivate || !_dismissOnDeactivate; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Keep floating popovers out of Alt-Tab / app-level Z-order competition
                cp.ExStyle |= NativeWin32.WS_EX_TOOLWINDOW;
                if (_neverActivate)
                {
                    cp.ExStyle |= NativeWin32.WS_EX_NOACTIVATE;
                }
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeWin32.ApplyWindows11Chrome(Handle, true, true);
        }

        protected override void WndProc(ref Message m)
        {
            if (_neverActivate && m.Msg == NativeWin32.WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)NativeWin32.MA_NOACTIVATE;
                return;
            }

            if (m.Msg == NativeWin32.WM_ACTIVATE)
            {
                int state = (int)(m.WParam.ToInt64() & 0xFFFF);
                IntPtr activatedHwnd = m.LParam;
                base.WndProc(ref m);

                // NEVER hide synchronously inside WM_ACTIVATE / Deactivate!
                // Doing so on an owned Form hosting an out-of-process WebView2 triggers
                // win32k ActivateOtherWindow() and pushes BrowserForm behind Chrome.
                if (state == NativeWin32.WA_INACTIVE && _dismissOnDeactivate && _isOpen && !_hiding && IsHandleCreated)
                {
                    int gen = _showGeneration;
                    try
                    {
                        BeginInvoke((Action)(() => HandleDeferredDeactivate(activatedHwnd, gen)));
                    }
                    catch { }
                }
                return;
            }

            base.WndProc(ref m);
        }

        private void HandleDeferredDeactivate(IntPtr activatedHwnd, int generation)
        {
            if (!_isOpen || IsDisposed || _hiding || generation != _showGeneration) return;

            IntPtr fg = NativeWin32.GetForegroundWindow();
            if (fg == Handle || (fg != IntPtr.Zero &&
                (NativeWin32.IsChild(Handle, fg) || NativeWin32.GetAncestor(fg, NativeWin32.GA_ROOT) == Handle)))
            {
                return; // Still active (including out-of-process WebView2 surface)
            }

            bool clickedOwner = false;
            if (_ownerForm != null && !_ownerForm.IsDisposed && _ownerForm.IsHandleCreated)
            {
                IntPtr ownerHwnd = _ownerForm.Handle;
                if (fg == ownerHwnd || activatedHwnd == ownerHwnd)
                {
                    clickedOwner = true;
                }
                else if (fg != IntPtr.Zero &&
                    (NativeWin32.IsChild(ownerHwnd, fg) || NativeWin32.GetAncestor(fg, NativeWin32.GA_ROOT) == ownerHwnd))
                {
                    clickedOwner = true;
                }
                else if (fg == IntPtr.Zero && _ownerForm.Visible &&
                    _ownerForm.WindowState != FormWindowState.Minimized &&
                    _ownerForm.Bounds.Contains(Cursor.Position))
                {
                    clickedOwner = true;
                }
            }

            HidePopup(clickedOwner);
        }

        public async Task InitializeAsync(CoreWebView2Environment env, Action<CoreWebView2WebResourceRequestedEventArgs> virtualResourceHandler)
        {
            await _wv.EnsureCoreWebView2Async(env);
            CoreWebView2 core = _wv.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            core.AddWebResourceRequestedFilter("https://dejavu.local/*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (s, e) => virtualResourceHandler(e);
            core.WebMessageReceived += (s, e) =>
            {
                Action<string> h = MessageReceived;
                if (h != null) h(e.WebMessageAsJson);
            };

            TaskCompletionSource<bool> navDone = new TaskCompletionSource<bool>();
            EventHandler<CoreWebView2NavigationCompletedEventArgs> onNav = null;
            onNav = (s, e) =>
            {
                core.NavigationCompleted -= onNav;
                navDone.TrySetResult(true);
            };
            core.NavigationCompleted += onNav;
            core.Navigate("https://dejavu.local/overlay.html");
            await navDone.Task;

            _ready = true;
            if (_pendingJson != null)
            {
                core.PostWebMessageAsJson(_pendingJson);
                _pendingJson = null;
            }
        }

        public async Task InitializeForExtensionPopupAsync(CoreWebView2Environment env, Action<string> onNewWindowUrl)
        {
            await _wv.EnsureCoreWebView2Async(env);
            CoreWebView2 core = _wv.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;

            core.NewWindowRequested += (s, e) =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(e.Uri))
                    {
                        e.Handled = true;
                        string target = e.Uri;
                        if (onNewWindowUrl != null) onNewWindowUrl(target);
                    }
                }
                catch { }
            };
            core.WindowCloseRequested += (s, e) =>
            {
                try { HidePopup(true); } catch { }
            };
            _ready = true;
        }

        public void NavigateExtensionUrl(string url)
        {
            if (!_ready || _wv == null || _wv.IsDisposed || _wv.CoreWebView2 == null || string.IsNullOrEmpty(url)) return;
            try { _wv.CoreWebView2.Navigate(url); } catch { }
        }

        public void UpdateSurfaceTheme(bool isLight)
        {
            if (IsDisposed) return;
            if (_everShown && _lastSurfaceLight == isLight) return;
            _lastSurfaceLight = isLight;
            Color bg = isLight ? Color.FromArgb(247, 242, 250) : Color.FromArgb(29, 27, 32);
            BackColor = bg;
            if (_wv != null && !_wv.IsDisposed)
            {
                try { _wv.DefaultBackgroundColor = bg; } catch { }
            }
            if (IsHandleCreated)
            {
                try { NativeWin32.ApplyWindows11Chrome(Handle, !isLight, true); } catch { }
            }
        }

        public void ShowAt(Rectangle screenBounds, string stateJson, bool activate)
        {
            _showGeneration++;
            if (!string.IsNullOrEmpty(stateJson))
            {
                if (_ready && _wv.CoreWebView2 != null)
                {
                    _wv.CoreWebView2.PostWebMessageAsJson(stateJson);
                }
                else
                {
                    _pendingJson = stateJson;
                }
            }

            if (Owner == null && _ownerForm != null && !_ownerForm.IsDisposed)
            {
                Owner = _ownerForm;
            }

            if (!_everShown)
            {
                Bounds = screenBounds;
                _everShown = true;
                _isOpen = true;
                if (activate)
                {
                    Show();
                    Activate();
                    FocusWebViewForInput();
                }
                else
                {
                    // ShowWithoutActivation is true for non-activating HUDs
                    Visible = true;
                }
                return;
            }

            bool wasOpen = _isOpen;
            bool boundsChanged = (Bounds != screenBounds);
            _isOpen = true;

            if (!wasOpen || boundsChanged)
            {
                uint flags = NativeWin32.SWP_NOZORDER | NativeWin32.SWP_NOOWNERZORDER;
                if (!wasOpen) flags |= NativeWin32.SWP_SHOWWINDOW;
                if (!boundsChanged) flags |= NativeWin32.SWP_NOMOVE | NativeWin32.SWP_NOSIZE;
                if (!activate) flags |= NativeWin32.SWP_NOACTIVATE;

                NativeWin32.SetWindowPos(
                    Handle,
                    IntPtr.Zero,
                    screenBounds.X,
                    screenBounds.Y,
                    screenBounds.Width,
                    screenBounds.Height,
                    flags);
            }

            if (activate)
            {
                if (!wasOpen || NativeWin32.GetForegroundWindow() != Handle)
                {
                    NativeWin32.SetForegroundWindow(Handle);
                    Activate();
                }
                FocusWebViewForInput();
            }
        }

        private void FocusWebViewForInput()
        {
            if (IsDisposed || _wv == null || _wv.IsDisposed) return;
            try
            {
                ActiveControl = _wv;
                _wv.Focus();
            }
            catch { }

            // Out-of-process WebView2 may finish processing SWP_SHOWWINDOW one UI tick
            // after WinForms Activate(); ensure wheel + keyboard focus lands inside
            // WebView2 cleanly without fighting window activation if already dismissed.
            if (IsHandleCreated)
            {
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        if (!_isOpen || IsDisposed || _hiding || _wv == null || _wv.IsDisposed) return;
                        IntPtr fg = NativeWin32.GetForegroundWindow();
                        if (fg != Handle) return;
                        try
                        {
                            if (!_wv.ContainsFocus)
                            {
                                ActiveControl = _wv;
                                _wv.Focus();
                            }
                        }
                        catch { }
                    }));
                }
                catch { }
            }
        }

        public void UpdateState(string stateJson)
        {
            if (_ready && _wv.CoreWebView2 != null)
            {
                _wv.CoreWebView2.PostWebMessageAsJson(stateJson);
            }
            else
            {
                _pendingJson = stateJson;
            }
        }

        public void HidePopup()
        {
            HidePopup(true);
        }

        public void HidePopup(bool keepOwnerForeground)
        {
            if (!_isOpen || IsDisposed || _hiding) return;
            _hiding = true;
            try
            {
                _isOpen = false;

                bool canKeepOwner = keepOwnerForeground &&
                    _ownerForm != null &&
                    !_ownerForm.IsDisposed &&
                    _ownerForm.IsHandleCreated &&
                    _ownerForm.Visible &&
                    _ownerForm.WindowState != FormWindowState.Minimized;

                IntPtr fgBefore = NativeWin32.GetForegroundWindow();

                // 1. If this popup (or its out-of-process WebView2) still holds foreground,
                //    transfer foreground + focus to BrowserForm BEFORE hiding so win32k
                //    never calls ActivateOtherWindow() to promote a background app.
                if (canKeepOwner && (fgBefore == Handle || fgBefore == IntPtr.Zero))
                {
                    NativeWin32.SetForegroundWindow(_ownerForm.Handle);
                    NativeWin32.SetFocus(_ownerForm.Handle);
                }

                // 2. CRITICAL: Hide ONLY via SetWindowPos(SWP_HIDEWINDOW | SWP_NOACTIVATE |
                //    SWP_NOZORDER | SWP_NOOWNERZORDER) instead of WinForms Form.Hide()!
                //    Calling Form.Hide() triggers WebView2.OnVisibleChanged -> async COM IPC
                //    CoreWebView2Controller.IsVisible = false in msedgewebview2.exe, which
                //    drops BrowserForm behind background apps (Chrome) ~66ms later (WebView2 #2929).
                if (IsHandleCreated)
                {
                    NativeWin32.SetWindowPos(
                        Handle,
                        IntPtr.Zero,
                        0, 0, 0, 0,
                        NativeWin32.SWP_HIDEWINDOW |
                        NativeWin32.SWP_NOACTIVATE |
                        NativeWin32.SWP_NOMOVE |
                        NativeWin32.SWP_NOSIZE |
                        NativeWin32.SWP_NOZORDER |
                        NativeWin32.SWP_NOOWNERZORDER);
                }

                // 3. Keep BrowserForm locked cleanly on top if focus stayed inside dejavu.
                if (canKeepOwner)
                {
                    NativeWin32.SetWindowPos(
                        _ownerForm.Handle,
                        NativeWin32.HWND_TOP,
                        0, 0, 0, 0,
                        NativeWin32.SWP_NOMOVE | NativeWin32.SWP_NOSIZE | NativeWin32.SWP_NOACTIVATE);

                    IntPtr fgAfter = NativeWin32.GetForegroundWindow();
                    if (fgAfter != _ownerForm.Handle)
                    {
                        NativeWin32.SetForegroundWindow(_ownerForm.Handle);
                    }
                }

                Action cb = PopupHidden;
                if (cb != null) cb();
            }
            finally
            {
                _hiding = false;
            }
        }
    }
}
