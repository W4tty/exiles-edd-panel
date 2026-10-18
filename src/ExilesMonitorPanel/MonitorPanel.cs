using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using static EDDDLLInterfaces.EDDDLLIF;

namespace ExilesMonitorPanel
{
    // The panel EDDiscovery hosts: a small toolbar over a WebView2 showing the squad monitor.
    // "Follow my ship" opens the monitor page for the system (and station) of the history cursor,
    // which EDD moves to the newest journal entry as you play.
    public class MonitorPanel : UserControl, IEDDPanelExtension
    {
        public const string DefaultSite = "https://reports.theexiles.gg/";
        public const string RuntimeUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";
        public const string HelpUrl = "https://github.com/W4tty/exiles-edd-panel#readme";

        private const string KeyFollow = "Follow";
        private const string KeySite = "Site";

        private static readonly HashSet<string> TravelEvents =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FSDJump", "CarrierJump", "Location", "Docked", "Undocked" };

        private EDDPanelCallbacks cb;
        private bool hasCallbacks;
        private string site = DefaultSite;
        private int historyCount;

        private readonly FlowLayoutPanel bar = new FlowLayoutPanel();
        private readonly Button btnMonitor = new Button();
        private readonly Button btnBack = new Button();
        private readonly Button btnReload = new Button();
        private readonly CheckBox chkFollow = new CheckBox();
        private readonly Label lblWhere = new Label();
        private readonly Button btnOpen = new Button();
        private readonly Button btnSite = new Button();
        private readonly Panel body = new Panel();
        private readonly Label lblStatus = new Label();
        private readonly LinkLabel lnkRuntime = new LinkLabel();
        private readonly ToolTip tips = new ToolTip();
        private readonly Timer cursorTimer = new Timer();

        private WebView2 web;
        private bool webReady;
        private bool closing;
        private string pendingRoute;      // route decided before the browser was ready
        private string lastRoute;         // last route "Follow" navigated to
        private JournalEntry? cursor;     // newest cursor or travel entry

        public MonitorPanel()
        {
            AutoScaleMode = AutoScaleMode.Inherit;      // EDD scales the host; do not scale again
            BuildUi();
            cursorTimer.Interval = 150;                 // EDD can send several cursor changes in a burst
            cursorTimer.Tick += (s, e) => { cursorTimer.Stop(); ApplyCursor(); };
        }

        #region EDD panel interface

        public bool SupportTransparency { get { return false; } }     // a browser cannot be see-through
        public bool DefaultTransparent { get { return false; } }

        public void Initialise(EDDPanelCallbacks callbacks, int displayid, string themeasjson, string configuration)
        {
            cb = callbacks;
            hasCallbacks = true;
            site = NormaliseSite(GetString(KeySite, DefaultSite)) ?? DefaultSite;
            chkFollow.Checked = GetInt(KeyFollow, 1) != 0;
            ThemeChanged(themeasjson);
            SetTitle(null);
            ExilesMonitorEDDClass.Log("Exiles Monitor panel opened: " + site);
        }

        public void SetTransparency(bool ison, Color curcol) { }
        public void TransparencyModeChanged(bool on) { }
        public void ControlTextVisibleChange(bool on) { }
        public void LoadLayout() { }
        public bool AllowClose() { return true; }
        public string HelpKeyOrAddress() { return HelpUrl; }

        public void InitialDisplay()
        {
            StartBrowser();
        }

        public void Closing()
        {
            closing = true;
            cursorTimer.Stop();
            webReady = false;
            try
            {
                if (web != null)
                {
                    body.Controls.Remove(web);
                    web.Dispose();
                    web = null;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exiles Monitor panel: browser dispose failed " + ex.Message);
            }
        }

        public void HistoryChange(int count, string commander, bool beta, bool legacy)
        {
            historyCount = count;
            if (cursor == null && count > 0)
                TakeLastEntry();
        }

        // The history cursor: EDD moves it to the newest entry as you play, or to whatever you click on.
        void IEDDPanelExtension.CursorChanged(JournalEntry je)
        {
            TakeEntry(je);
        }

        // Live journal events: movement is applied even when the cursor is parked on an older entry.
        public void NewFilteredJournal(JournalEntry je)
        {
            if (je.eventid != null && TravelEvents.Contains(je.eventid))
                TakeEntry(je);
        }

        public void NewUnfilteredJournal(JournalEntry je) { }
        public void NewUIEvent(string jsonui) { }
        public void NewTarget(Tuple<string, double, double, double> target) { }
        public void ScreenShotCaptured(string file, Size s) { }

        public void ThemeChanged(string themeasjson)
        {
            try
            {
                Dictionary<string, object> t = null;
                if (!string.IsNullOrWhiteSpace(themeasjson))
                    t = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Dictionary<string, object>>(themeasjson);
                if (t == null)
                    t = new Dictionary<string, object>();

                Color orange = Color.FromArgb(0xF0, 0x7B, 0x05);
                Color form = ColorOf(t, "form", Color.Black);
                Color buttonBack = ColorOf(t, "button_back", Color.FromArgb(0x20, 0x20, 0x20));
                Color buttonBorder = ColorOf(t, "button_border", orange);
                Color buttonText = ColorOf(t, "button_text", orange);
                Color labelText = ColorOf(t, "label", buttonText);
                Color checkText = ColorOf(t, "checkbox", buttonText);
                string fontName = StringOf(t, "fontname") ?? StringOf(t, "font") ?? "Microsoft Sans Serif";
                float fontSize = FloatOf(t, "fontsize", 8.25f);
                Font font = SafeFont(fontName, fontSize);

                BackColor = form;
                bar.BackColor = form;
                body.BackColor = form;
                lblStatus.BackColor = form;
                lblStatus.ForeColor = labelText;
                lnkRuntime.BackColor = form;
                lnkRuntime.LinkColor = buttonText;
                lnkRuntime.ActiveLinkColor = buttonBorder;
                foreach (Button b in new[] { btnMonitor, btnBack, btnReload, btnOpen, btnSite })
                {
                    b.BackColor = buttonBack;
                    b.ForeColor = buttonText;
                    b.FlatAppearance.BorderColor = buttonBorder;
                    b.FlatAppearance.MouseOverBackColor = Mix(buttonBack, buttonBorder, 0.25f);
                    b.FlatAppearance.MouseDownBackColor = Mix(buttonBack, buttonBorder, 0.5f);
                    b.Font = font;
                }
                chkFollow.ForeColor = checkText;
                chkFollow.BackColor = form;
                chkFollow.Font = font;
                lblWhere.ForeColor = labelText;
                lblWhere.BackColor = form;
                lblWhere.Font = font;
                lblStatus.Font = font;
                lnkRuntime.Font = font;
                Font = font;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exiles Monitor panel: theme not applied " + ex.Message);
            }
        }

        #endregion

        #region Browser

        private async void StartBrowser()
        {
            if (web != null || closing)
                return;
            try
            {
                web = new WebView2();
                web.Dock = DockStyle.Fill;
                web.Visible = false;            // no white flash before the first page paints
                web.CoreWebView2InitializationCompleted += Web_InitCompleted;
                web.NavigationCompleted += (s, e) => { if (!closing && web != null) web.Visible = true; };
                body.Controls.Add(web);
                body.Controls.SetChildIndex(web, 0);   // fills what the status labels leave

                ShowStatus("Starting the browser", false);
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, ProfileFolder(), null);
                if (closing || web == null)
                    return;
                await web.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                ShowStatus("The browser could not start: " + ex.Message + Environment.NewLine +
                           "This panel and the Inara, EDSM and Spansh panels of EDDiscovery need the Microsoft Edge WebView2 Runtime.", true);
                ExilesMonitorEDDClass.Log("Exiles Monitor panel: WebView2 failed: " + ex.Message);
            }
        }

        private void Web_InitCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (closing || web == null)
                return;
            if (!e.IsSuccess)
            {
                string why = e.InitializationException != null ? e.InitializationException.Message : "unknown reason";
                ShowStatus("The browser could not start: " + why, true);
                ExilesMonitorEDDClass.Log("Exiles Monitor panel: WebView2 init failed: " + why);
                return;
            }

            webReady = true;
            try
            {
                CoreWebView2 core = web.CoreWebView2;
                core.Settings.IsStatusBarEnabled = false;
                core.NewWindowRequested += (s, a) => { a.Handled = true; OpenExternal(a.Uri); };   // links that want a new window go to the real browser
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exiles Monitor panel: settings " + ex.Message);
            }
            HideStatus();

            if (pendingRoute != null)
            {
                lastRoute = pendingRoute;
                Navigate(site + pendingRoute);
                pendingRoute = null;
            }
            else
            {
                Navigate(site);
            }

            // ask the history grid for its cursor so the first page is where the ship is
            try
            {
                if (hasCallbacks && cb.RequestTravelGridPosition != null)
                    cb.RequestTravelGridPosition();
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exiles Monitor panel: grid position " + ex.Message);
            }
            if (cursor == null && historyCount > 0)
                TakeLastEntry();
        }

        private void Navigate(string url)
        {
            if (!webReady || web == null || web.CoreWebView2 == null)
                return;
            try
            {
                web.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exiles Monitor panel: navigate " + ex.Message);
            }
        }

        // EDD keeps one WebView2 profile folder for its own web panels; share it so there is one browser
        // process and one set of cookies. Falls back to the usual EDD data folder.
        private static string ProfileFolder()
        {
            try
            {
                Type options = Type.GetType("EDDiscovery.EDDOptions, EDDiscovery", false);
                if (options != null)
                {
                    PropertyInfo inst = options.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    MethodInfo dir = options.GetMethod("WebView2ProfileDirectory", Type.EmptyTypes);
                    object o = inst != null ? inst.GetValue(null) : null;
                    string path = o != null && dir != null ? dir.Invoke(o, null) as string : null;
                    if (!string.IsNullOrEmpty(path))
                        return path;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exiles Monitor panel: host profile folder " + ex.Message);
            }
            string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EDDiscovery", "WebView2");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        #endregion

        #region Follow

        private void TakeEntry(JournalEntry je)
        {
            if (je.indexno < 0 || string.IsNullOrWhiteSpace(je.systemname))
                return;
            cursor = je;
            cursorTimer.Stop();
            cursorTimer.Start();
        }

        // The newest history entry, for a panel opened before the cursor ever moved.
        private void TakeLastEntry()
        {
            try
            {
                if (!ExilesMonitorEDDClass.HasCallbacks || ExilesMonitorEDDClass.Callbacks.RequestHistory == null)
                    return;
                foreach (long index in new long[] { historyCount, historyCount - 1 })
                {
                    JournalEntry je;
                    if (index > 0 && ExilesMonitorEDDClass.Callbacks.RequestHistory(index, false, out je) && je.indexno >= 0 && !string.IsNullOrWhiteSpace(je.systemname))
                    {
                        TakeEntry(je);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exiles Monitor panel: last entry " + ex.Message);
            }
        }

        private void ApplyCursor()
        {
            if (cursor == null || closing)
                return;
            JournalEntry je = cursor.Value;
            string system = (je.systemname ?? "").Trim();
            if (system.Length == 0)
                return;
            string station = je.isdocked ? StationOf(je) : null;

            lblWhere.Text = station != null ? system + " / " + station : system;
            SetTitle(lblWhere.Text);
            if (!chkFollow.Checked)
                return;

            string route = "#system/" + Uri.EscapeDataString(system);
            if (station != null)
                route += "?station=" + Uri.EscapeDataString(station);
            if (route == lastRoute)
                return;
            lastRoute = route;
            if (webReady)
                Navigate(site + route);
            else
                pendingRoute = route;
        }

        private static string StationOf(JournalEntry je)
        {
            string s = (je.stationname ?? "").Trim();
            if (s.Length == 0 || s.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                s = (je.whereami ?? "").Trim();
            if (s.Length == 0 || s.Equals(je.systemname, StringComparison.OrdinalIgnoreCase))
                return null;
            return s;
        }

        #endregion

        #region UI

        private void BuildUi()
        {
            SuspendLayout();
            BackColor = Color.Black;

            bar.Dock = DockStyle.Top;
            bar.AutoSize = true;
            bar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bar.WrapContents = true;
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.Padding = new Padding(3, 2, 3, 2);

            SetupButton(btnMonitor, "Monitor", "Open the front page of the monitor");
            SetupButton(btnBack, "Back", "Go back one page");
            SetupButton(btnReload, "Reload", "Reload the page");
            chkFollow.Text = "Follow my ship";
            chkFollow.AutoSize = true;
            chkFollow.Checked = true;
            chkFollow.Margin = new Padding(10, 4, 4, 2);
            tips.SetToolTip(chkFollow, "Open the monitor page for the system and station you are in");
            lblWhere.AutoSize = true;
            lblWhere.Margin = new Padding(6, 5, 6, 2);
            lblWhere.Text = "";
            SetupButton(btnOpen, "Open in browser", "Open this page in your web browser");
            SetupButton(btnSite, "Site", "Change the monitor address (for ops.theexiles.gg or a test server)");
            bar.Controls.AddRange(new Control[] { btnMonitor, btnBack, btnReload, chkFollow, lblWhere, btnOpen, btnSite });

            body.Dock = DockStyle.Fill;
            body.BackColor = Color.Black;

            lnkRuntime.Text = "Get the Microsoft Edge WebView2 Runtime";
            lnkRuntime.Dock = DockStyle.Top;
            lnkRuntime.AutoSize = false;
            lnkRuntime.Height = 28;
            lnkRuntime.Padding = new Padding(10, 0, 10, 4);
            lnkRuntime.Visible = false;
            lnkRuntime.LinkClicked += (s, e) => OpenExternal(RuntimeUrl);

            lblStatus.Dock = DockStyle.Top;
            lblStatus.AutoSize = false;
            lblStatus.Height = 70;
            lblStatus.Padding = new Padding(10, 8, 10, 4);
            lblStatus.ForeColor = Color.Gainsboro;
            lblStatus.Visible = false;

            body.Controls.Add(lnkRuntime);
            body.Controls.Add(lblStatus);      // added last: docks first, sits above the link

            Controls.Add(body);
            Controls.Add(bar);                 // added last: docks first, the bar stays above the body

            btnMonitor.Click += (s, e) => { lastRoute = null; Navigate(site); };
            btnBack.Click += (s, e) => { if (webReady && web != null && web.CanGoBack) web.GoBack(); };
            btnReload.Click += (s, e) => { if (webReady && web != null) web.Reload(); };
            btnOpen.Click += (s, e) => OpenExternal(webReady && web != null && web.Source != null ? web.Source.ToString() : site);
            btnSite.Click += (s, e) => ChangeSite();
            chkFollow.CheckedChanged += (s, e) =>
            {
                SaveInt(KeyFollow, chkFollow.Checked ? 1 : 0);
                if (chkFollow.Checked)
                {
                    lastRoute = null;
                    ApplyCursor();
                }
            };
            ResumeLayout();
        }

        private void SetupButton(Button b, string text, string tip)
        {
            b.Text = text;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.Margin = new Padding(2);
            b.Padding = new Padding(6, 1, 6, 1);
            b.UseVisualStyleBackColor = false;
            b.TabStop = false;
            tips.SetToolTip(b, tip);
        }

        private void ChangeSite()
        {
            using (var f = new Form())
            {
                f.Text = "Monitor address";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.ClientSize = new Size(480, 112);
                var lbl = new Label { Text = "Address of the monitor site (blank for the default):", Left = 12, Top = 12, AutoSize = true };
                var box = new TextBox { Left = 12, Top = 36, Width = 456, Text = site };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 312, Top = 72, Width = 75 };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 393, Top = 72, Width = 75 };
                f.Controls.AddRange(new Control[] { lbl, box, ok, cancel });
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                if (f.ShowDialog(FindForm()) != DialogResult.OK)
                    return;
                string s = NormaliseSite(box.Text);
                if (s == null)
                {
                    MessageBox.Show(FindForm(), "That is not a web address. Use something like https://reports.theexiles.gg/", "Monitor address", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                site = s;
                SaveString(KeySite, site);
                lastRoute = null;
                Navigate(site);
                ApplyCursor();
            }
        }

        // "reports.theexiles.gg" becomes "https://reports.theexiles.gg/"; null when it is not an http(s) address.
        public static string NormaliseSite(string text)
        {
            string s = (text ?? "").Trim();
            if (s.Length == 0)
                return DefaultSite;
            if (s.IndexOf("://", StringComparison.Ordinal) < 0)
                s = "https://" + s;
            int cut = s.IndexOfAny(new[] { '#', '?' });
            if (cut >= 0)
                s = s.Substring(0, cut);
            if (!s.EndsWith("/"))
                s += "/";
            Uri u;
            if (!Uri.TryCreate(s, UriKind.Absolute, out u) || (u.Scheme != "http" && u.Scheme != "https"))
                return null;
            return u.ToString();
        }

        private void ShowStatus(string text, bool offerRuntime)
        {
            lblStatus.Text = text;
            lblStatus.Visible = true;
            lnkRuntime.Visible = offerRuntime;
        }

        private void HideStatus()
        {
            lblStatus.Visible = false;
            lnkRuntime.Visible = false;
        }

        private void SetTitle(string where)
        {
            try
            {
                if (hasCallbacks && cb.SetControlText != null)
                    cb.SetControlText(where == null ? "Exiles Monitor" : "Exiles Monitor: " + where);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exiles Monitor panel: title " + ex.Message);
            }
        }

        private static void OpenExternal(string url)
        {
            try
            {
                if (url != null && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exiles Monitor panel: open external " + ex.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                cursorTimer.Dispose();
                tips.Dispose();
                try { if (web != null) web.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Settings and theme helpers

        private string GetString(string key, string def)
        {
            try { return hasCallbacks && cb.GetString != null ? (cb.GetString(key, def) ?? def) : def; } catch { return def; }
        }

        private int GetInt(string key, int def)
        {
            try { return hasCallbacks && cb.GetInt != null ? cb.GetInt(key, def) : def; } catch { return def; }
        }

        private void SaveString(string key, string value)
        {
            try { if (hasCallbacks && cb.SaveString != null) cb.SaveString(key, value); } catch { }
        }

        private void SaveInt(string key, int value)
        {
            try { if (hasCallbacks && cb.SaveInt != null) cb.SaveInt(key, value); } catch { }
        }

        private static Color ColorOf(Dictionary<string, object> t, string key, Color def)
        {
            try
            {
                object o;
                if (t.TryGetValue(key, out o) && o is string && ((string)o).Length > 0)
                {
                    Color c = ColorTranslator.FromHtml((string)o);
                    return c.A == 0 ? def : c;
                }
            }
            catch { }
            return def;
        }

        private static string StringOf(Dictionary<string, object> t, string key)
        {
            object o;
            return t.TryGetValue(key, out o) && o is string && ((string)o).Length > 0 ? (string)o : null;
        }

        private static float FloatOf(Dictionary<string, object> t, string key, float def)
        {
            object o;
            if (t.TryGetValue(key, out o) && o != null)
            {
                float v;
                if (float.TryParse(Convert.ToString(o, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) && v >= 5 && v <= 40)
                    return v;
            }
            return def;
        }

        private static Font SafeFont(string name, float size)
        {
            try { return new Font(name, size); } catch { return new Font("Microsoft Sans Serif", 8.25f); }
        }

        private static Color Mix(Color a, Color b, float amount)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * amount),
                (int)(a.G + (b.G - a.G) * amount),
                (int)(a.B + (b.B - a.B) * amount));
        }

        #endregion
    }
}
