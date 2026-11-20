using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DejavuBrowser
{
    internal sealed class OverlayPopup : Form
    {
        private readonly WebView2 _wv;
        private bool _ready;
        private string _pendingJson;
        private readonly bool _dismissOnDeactivate;

        public event Action<string> MessageReceived;

        public OverlayPopup(Form owner, bool dismissOnDeactivate)
        {
            _dismissOnDeactivate = dismissOnDeactivate;
            Owner = owner;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(12, 10, 22);
            TopMost = false;

            _wv = new WebView2();
            _wv.Dock = DockStyle.Fill;
            _wv.DefaultBackgroundColor = Color.FromArgb(12, 10, 22);
            Controls.Add(_wv);

            if (_dismissOnDeactivate)
            {
                Deactivate += (s, e) => HidePopup();
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return !_dismissOnDeactivate; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeWin32.ApplyWindows11Chrome(Handle, true, true);
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

        public void ShowAt(Rectangle screenBounds, string stateJson, bool activate)
        {
            Bounds = screenBounds;
            if (_ready && _wv.CoreWebView2 != null)
            {
                _wv.CoreWebView2.PostWebMessageAsJson(stateJson);
            }
            else
            {
                _pendingJson = stateJson;
            }

            if (!Visible)
            {
                if (activate)
                {
                    Show(Owner);
                    Activate();
                }
                else
                {
                    // Show without stealing focus (ideal for Download Toast)
                    NativeWin32.ShowWindow(Handle, NativeWin32.SW_SHOWNOACTIVATE);
                    Visible = true;
                }
            }
            else if (activate)
            {
                Activate();
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
            if (Visible)
            {
                Hide();
            }
        }
    }
}
