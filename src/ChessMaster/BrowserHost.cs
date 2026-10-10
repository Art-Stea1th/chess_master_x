using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ChessMaster;

internal static class BrowserHost
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChessMaster");

    public static string ProfileDirectory { get; } = Path.Combine(Root, "WebView2");

    public static string WindowFile { get; } = Path.Combine(Root, "window.json");

    public static string BoardsDirectory { get; } = Path.Combine(Root, "boards");

    private sealed class HookedPage
    {
        public required CoreWebView2 Core { get; init; }
        public string FlatScriptId { get; set; } = "";
    }

    private static readonly List<HookedPage> Pages = [];

    public static CoreWebView2Environment? SharedEnvironment { get; private set; }

    public static async Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        if (SharedEnvironment is not null)
        {
            return SharedEnvironment;
        }

        Directory.CreateDirectory(ProfileDirectory);
        var options = new CoreWebView2EnvironmentOptions("--disable-features=OverlayScrollbar,FluentScrollbar");
        SharedEnvironment = await CoreWebView2Environment.CreateAsync(null, ProfileDirectory, options);
        return SharedEnvironment;
    }

    public static async Task AttachAsync(WebView2 webView)
    {
        webView.DefaultBackgroundColor = AppTheme.Background;
        var environment = await GetEnvironmentAsync();
        await webView.EnsureCoreWebView2Async(environment);
        var settings = webView.CoreWebView2.Settings;
        settings.IsStatusBarEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = true;
        settings.AreBrowserAcceleratorKeysEnabled = true;
        settings.IsZoomControlEnabled = true;
        var core = webView.CoreWebView2;
        core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Auto;
        HookBoardSwap(core);
        try
        {
            await core.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache);
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
        }

        await core.AddScriptToExecuteOnDocumentCreatedAsync(ScrollbarScript);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(BoardLookScript());
        await core.AddScriptToExecuteOnDocumentCreatedAsync(PieceLookScript);
        var page = new HookedPage { Core = core };
        page.FlatScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(FlatStyleScript());
        Pages.Add(page);

        core.NavigationCompleted += async (_, _) =>
        {
            await ApplyFlatStyleAsync(page);
        };
    }

    public static async Task ApplyFlatStyleAsync()
    {
        foreach (var page in Pages.ToArray())
        {
            try
            {
                page.Core.RemoveScriptToExecuteOnDocumentCreated(page.FlatScriptId);
                page.FlatScriptId = await page.Core.AddScriptToExecuteOnDocumentCreatedAsync(FlatStyleScript());
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or COMException)
            {
                Pages.Remove(page);
                continue;
            }

            await ApplyFlatStyleAsync(page);
        }
    }

    private static async Task ApplyFlatStyleAsync(HookedPage page)
    {
        var flag = AppPreferences.FlatStyle ? "true" : "false";
        try
        {
            await page.Core.ExecuteScriptAsync($"window.chessmasterApplyFlat && window.chessmasterApplyFlat({flag})");
            await page.Core.ExecuteScriptAsync("window.chessmasterPaintBoards && window.chessmasterPaintBoards()");
            _ = DumpBoardsLater(page);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or COMException)
        {
            Pages.Remove(page);
        }
    }

    private static string FlatStyleScript()
    {
        var initial = AppPreferences.FlatStyle ? "true" : "false";
        return $$"""
            (() => {
              const VERSION = 29;
              window.__cmFlatVersion = VERSION;
              const alive = () => window.__cmFlatVersion === VERSION;
              const id = "chessmaster-flat";
              const css = `
                html[data-cm-root] :is(button, [role="button"], [class*="cc-button"], [class*="Button"]):not(wc-chess-board, wc-chess-board *) {
                  appearance: none !important;
                  -webkit-appearance: none !important;
                  border-radius: 0 !important;
                  background-image: none !important;
                  box-shadow: none !important;
                  filter: none !important;
                  text-shadow: none !important;
                  border-image: none !important;
                  outline: none !important;
                }
                html[data-cm-root] [data-cm-flat] {
                  appearance: none !important;
                  -webkit-appearance: none !important;
                  background: var(--cm-rest) none !important;
                  background-image: none !important;
                  background-color: var(--cm-rest) !important;
                  box-shadow: none !important;
                  filter: none !important;
                  text-shadow: none !important;
                  border-image: none !important;
                  border-radius: 0 !important;
                  outline: none !important;
                  transform: none !important;
                }
                html[data-cm-root] [data-cm-flat][data-cm-ghost] {
                  background: transparent none !important;
                  background-color: transparent !important;
                }
                html[data-cm-root] [data-cm-flat]:hover,
                html[data-cm-root] [data-cm-flat][data-cm-state="hover"] {
                  background: var(--cm-hover) none !important;
                  background-image: none !important;
                  background-color: var(--cm-hover) !important;
                  border-color: var(--cm-hover-border) !important;
                }
                html[data-cm-root] [data-cm-flat]:active,
                html[data-cm-root] [data-cm-flat][data-cm-state="press"] {
                  background: var(--cm-press) none !important;
                  background-image: none !important;
                  background-color: var(--cm-press) !important;
                  border-color: var(--cm-press-border) !important;
                }
                html[data-cm-root] [data-cm-flat]::before,
                html[data-cm-root] [data-cm-flat]::after {
                  box-shadow: none !important;
                  filter: none !important;
                  border-radius: 0 !important;
                }
              `;
              const styled = new Set();
              let enabled = {{initial}};

              const mount = (root) => {
                const parent = root.head || root.documentElement || root;
                if (!parent || !parent.appendChild || !parent.querySelector) return;
                let style = parent.querySelector("#" + id);
                if (!enabled) {
                  if (style) style.remove();
                  return;
                }
                if (!style) {
                  style = document.createElement("style");
                  style.id = id;
                  parent.appendChild(style);
                  styled.add(style);
                }
                style.textContent = css;
              };

              const walk = (node) => {
                if (!node || !node.querySelectorAll) return;
                for (const el of node.querySelectorAll("*")) {
                  if (!el.shadowRoot) continue;
                  const tag = (el.tagName || "").toLowerCase();
                  if (tag === "wc-chess-board" || tag.includes("chess-board")) continue;
                  mount(el.shadowRoot);
                  walk(el.shadowRoot);
                }
              };

              const touched = new Set();
              let observer = null;
              let queued = false;

              const onBoard = (el) => {
                let node = el;
                while (node) {
                  const tag = (node.tagName || "").toLowerCase();
                  if (tag === "wc-chess-board" || tag.includes("chess-board")) return true;
                  if (node.parentNode) {
                    node = node.parentNode;
                    continue;
                  }
                  const root = node.getRootNode && node.getRootNode();
                  node = root && root.host ? root.host : null;
                }
                return false;
              };

              const controlHost = (el) => {
                let node = el;
                while (node && node.nodeType === 1) {
                  if (onBoard(node)) return null;
                  const tag = (node.tagName || "").toLowerCase();
                  const role = node.getAttribute ? (node.getAttribute("role") || "") : "";
                  const cls = typeof node.className === "string" ? node.className : "";
                  if (tag === "button" || tag === "a" || role === "button" || role === "tab" || role === "menuitem" || /button/i.test(cls)) {
                    return node;
                  }
                  const parent = node.parentElement || (node.getRootNode && node.getRootNode().host) || null;
                  if (parent === node) break;
                  node = parent;
                }
                return null;
              };

              const parseColor = (input) => {
                if (!input) return null;
                const s = String(input).trim();
                let m = s.match(/^rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)(?:\s*,\s*([\d.]+))?\s*\)$/i);
                if (m) return { r: +m[1], g: +m[2], b: +m[3], a: m[4] === undefined ? 1 : +m[4] };
                m = s.match(/^#([0-9a-f]{3,8})$/i);
                if (!m) return null;
                let h = m[1];
                if (h.length === 3 || h.length === 4) h = [...h].map((c) => c + c).join("");
                return {
                  r: parseInt(h.slice(0, 2), 16),
                  g: parseInt(h.slice(2, 4), 16),
                  b: parseInt(h.slice(4, 6), 16),
                  a: h.length >= 8 ? parseInt(h.slice(6, 8), 16) / 255 : 1
                };
              };

              const colorsIn = (text) => {
                if (!text || text === "none") return [];
                const out = [];
                const re = /rgba?\([^)]+\)|#[0-9a-fA-F]{3,8}/g;
                let m;
                while ((m = re.exec(text))) {
                  const c = parseColor(m[0]);
                  if (c) out.push(c);
                }
                return out;
              };

              const chromaOf = (c) => Math.max(c.r, c.g, c.b) - Math.min(c.r, c.g, c.b);
              const lumaOf = (c) => 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;
              const pickAccent = (list) => {
                let best = null;
                let score = -1;
                for (const c of list) {
                  if (!c || c.a < 0.35) continue;
                  const chroma = chromaOf(c);
                  if (chroma <= 24) continue;
                  const s = chroma * c.a;
                  if (s > score) {
                    score = s;
                    best = c;
                  }
                }
                return best;
              };

              const clamp = (n) => Math.max(0, Math.min(255, Math.round(n)));
              const shift = (c, d) => ({ r: clamp(c.r + d), g: clamp(c.g + d), b: clamp(c.b + d), a: 1 });
              const rgb = (c) => "rgb(" + clamp(c.r) + ", " + clamp(c.g) + ", " + clamp(c.b) + ")";
              const pageFill = () => {
                try {
                  const c = parseColor(getComputedStyle(document.body).backgroundColor);
                  if (c && c.a > 0.5) return c;
                } catch (e) {}
                return { r: 33, g: 32, b: 30, a: 1 };
              };

              const gatherFills = (el) => {
                const fills = [];
                const borders = [];
                const take = (cs) => {
                  if (!cs) return;
                  const bg = parseColor(cs.backgroundColor);
                  if (bg && bg.a > 0.45) fills.push(bg);
                  const image = cs.backgroundImage || "";
                  if (image.includes("gradient") && !image.includes("url(")) {
                    for (const c of colorsIn(image)) if (c.a > 0.4) fills.push(c);
                  }
                  const edge = parseColor(cs.borderTopColor);
                  const width = parseFloat(cs.borderTopWidth) || 0;
                  if (edge && width >= 1) borders.push(edge);
                  borders.push(...colorsIn(cs.boxShadow || "").filter((c) => c.a > 0.4));
                };
                try { take(getComputedStyle(el)); } catch (e) {}
                for (const pseudo of ["::before", "::after"]) {
                  try { take(getComputedStyle(el, pseudo)); } catch (e) {}
                }
                return { fills, borders };
              };

              const sampleLook = (el) => {
                const { fills, borders } = gatherFills(el);
                const accent = pickAccent(fills) || pickAccent(borders);
                if (accent) {
                  return { fill: accent, border: pickAccent(borders) || shift(accent, 8), accent: true };
                }
                let fill = null;
                try {
                  const own = parseColor(getComputedStyle(el).backgroundColor);
                  if (own && own.a > 0.5 && lumaOf(own) > 16) fill = own;
                } catch (e) {}
                if (!fill) {
                  const grays = fills.filter((c) => chromaOf(c) <= 24 && lumaOf(c) > 16 && lumaOf(c) < 96);
                  grays.sort((a, b) => lumaOf(a) - lumaOf(b));
                  fill = grays[Math.floor(grays.length / 2)] || null;
                }
                if (!fill) return null;
                const page = pageFill();
                if (lumaOf(fill) < lumaOf(page) + 12) {
                  return { fill: page, border: page, accent: false, ghost: true };
                }
                return { fill, border: shift(fill, 4), accent: false };
              };

              const signature = (el) => {
                const cls = typeof el.className === "string" ? el.className : "";
                const clean = cls.replace(/\bcm-no-volume\b/g, " ").replace(/\s+/g, " ").trim();
                return clean + "|" +
                  (el.getAttribute("aria-selected") || "") + "|" +
                  (el.getAttribute("aria-pressed") || "") + "|" +
                  (el.getAttribute("aria-current") || "");
              };

              const dropPaint = (node) => {
                if (!node || !node.style) return;
                for (const name of ["--cm-rest", "--cm-hover", "--cm-press", "--cm-border", "--cm-hover-border", "--cm-press-border", "--cm-text", "--cm-hover-text", "--cm-press-text", "--cm-bw"]) {
                  node.style.removeProperty(name);
                }
                for (const name of ["background", "background-image", "background-color", "box-shadow", "border", "border-color", "border-style", "border-width", "border-image", "filter", "text-shadow", "color", "outline"]) {
                  node.style.removeProperty(name);
                }
                if (node.dataset) {
                  delete node.dataset.cmFlat;
                  delete node.dataset.cmSig;
                  delete node.dataset.cmRest;
                  delete node.dataset.cmState;
                  delete node.dataset.cmGhost;
                }
                if (node.classList) node.classList.remove("cm-no-volume");
              };

              const isHostControl = (el) => {
                const tag = (el.tagName || "").toLowerCase();
                if (tag === "button") return true;
                if (el.closest && el.closest("button")) return false;
                if (el.querySelector && el.querySelector("button")) return false;
                const role = el.getAttribute ? (el.getAttribute("role") || "") : "";
                const cls = typeof el.className === "string" ? el.className : "";
                return role === "button" || /cc-button/i.test(cls);
              };

              const mix = (c, toward, t) => ({
                r: clamp(c.r + (toward.r - c.r) * t),
                g: clamp(c.g + (toward.g - c.g) * t),
                b: clamp(c.b + (toward.b - c.b) * t),
                a: 1
              });
              const WHITE = { r: 255, g: 255, b: 255, a: 1 };
              const BLACK = { r: 0, g: 0, b: 0, a: 1 };

              const killVolume = (el) => {
                el.style.setProperty("appearance", "none", "important");
                el.style.setProperty("-webkit-appearance", "none", "important");
                el.style.setProperty("background-image", "none", "important");
                el.style.setProperty("box-shadow", "none", "important");
                el.style.setProperty("filter", "none", "important");
                el.style.setProperty("border-image", "none", "important");
                el.style.setProperty("text-shadow", "none", "important");
                el.style.setProperty("outline", "none", "important");
                el.style.setProperty("border-radius", "0", "important");
              };

              const fillNode = (node, fill, border) => {
                if (!node || !node.style) return;
                const tag = (node.tagName || "").toLowerCase();
                if (tag === "img" || tag === "svg" || tag === "canvas" || tag === "video" || tag === "path") return;
                if (fill) {
                  node.style.setProperty("background", fill + " none", "important");
                  node.style.setProperty("background-image", "none", "important");
                  node.style.setProperty("background-color", fill, "important");
                }
                if (border) node.style.setProperty("border-color", border, "important");
                killVolume(node);
              };

              const setState = (el, state) => {
                if (!alive() || !el) return;
                el.dataset.cmState = state;
                const token = state === "press" ? "--cm-press" : state === "hover" ? "--cm-hover" : "--cm-rest";
                const edge = state === "press" ? "--cm-press-border" : state === "hover" ? "--cm-hover-border" : "--cm-border";
                const fill = (el.style.getPropertyValue(token) || "").trim();
                const border = (el.style.getPropertyValue(edge) || "").trim();
                fillNode(el, fill, border);
              };

              const hostFromNode = (start) => {
                let node = start;
                while (node && node.nodeType === 1) {
                  if (onBoard(node)) return null;
                  if (node.dataset && node.dataset.cmFlat === "1" && node.dataset.cmRest) return node;
                  const root = node.getRootNode && node.getRootNode();
                  node = node.parentElement || (root && root.host) || null;
                }
                return null;
              };

              const hostAtPoint = (x, y) => {
                let stack = [];
                try { stack = document.elementsFromPoint(x, y) || []; } catch (e) { return null; }
                for (const n of stack) {
                  const host = hostFromNode(n);
                  if (host) return host;
                }
                return null;
              };

              const bindPointer = () => {
                if (window.__cmHoverBound === VERSION) return;
                window.__cmHoverBound = VERSION;
                let last = { x: -1, y: -1, buttons: 0 };
                let prev = null;
                const applyAt = (x, y, buttons) => {
                  if (!alive()) return;
                  const host = hostAtPoint(x, y);
                  if (prev && prev !== host) setState(prev, "rest");
                  if (host) setState(host, buttons ? "press" : "hover");
                  prev = host || null;
                };
                const onMove = (e) => {
                  last = { x: e.clientX, y: e.clientY, buttons: e.buttons };
                  applyAt(last.x, last.y, last.buttons);
                };
                document.addEventListener("pointermove", onMove, true);
                document.addEventListener("pointerover", onMove, true);
                document.addEventListener("pointerdown", onMove, true);
                document.addEventListener("pointerup", onMove, true);
                document.addEventListener("pointerleave", () => {
                  if (prev) setState(prev, "rest");
                  prev = null;
                }, true);
                const tick = () => {
                  if (!alive()) return;
                  if (last.x >= 0) applyAt(last.x, last.y, last.buttons);
                  requestAnimationFrame(tick);
                };
                requestAnimationFrame(tick);
              };

              const stripVolume = (node) => {
                if (!node || !node.style) return;
                const tag = (node.tagName || "").toLowerCase();
                if (tag === "img" || tag === "svg" || tag === "canvas" || tag === "video" || tag === "path") return;
                node.style.setProperty("background-image", "none", "important");
                node.style.setProperty("box-shadow", "none", "important");
                node.style.setProperty("filter", "none", "important");
                node.style.setProperty("text-shadow", "none", "important");
                node.style.setProperty("border-image", "none", "important");
                node.style.setProperty("border-radius", "0", "important");
                node.classList.add("cm-no-volume");
                touched.add(node);
              };

              const paintFlat = (el) => {
                if (!el || onBoard(el)) return;
                const sig = signature(el);
                if (el.dataset.cmRest && el.dataset.cmFlat === "1" && el.dataset.cmSig === sig) {
                  return;
                }

                let look = sampleLook(el);
                if (!look) {
                  if (!isHostControl(el)) return;
                  look = { fill: pageFill(), border: pageFill(), accent: false, ghost: true };
                }
                if (!look.accent) look.ghost = true;

                const rest = look.fill;
                const hover = mix(rest, WHITE, look.accent ? 0.14 : 0.16);
                const press = look.ghost ? mix(rest, WHITE, 0.10) : mix(rest, BLACK, 0.14);
                const restCss = look.ghost ? "transparent" : rgb(rest);
                el.dataset.cmRest = restCss;
                if (look.ghost) el.dataset.cmGhost = "1";
                else delete el.dataset.cmGhost;
                el.style.setProperty("--cm-rest", restCss);
                el.style.setProperty("--cm-hover", rgb(hover));
                el.style.setProperty("--cm-press", rgb(press));
                el.style.setProperty("--cm-border", "transparent");
                el.style.setProperty("--cm-hover-border", "transparent");
                el.style.setProperty("--cm-press-border", "transparent");
                setState(el, el.matches(":active") ? "press" : el.matches(":hover") ? "hover" : "rest");
                bindPointer();
                if (el.querySelectorAll) {
                  for (const child of el.querySelectorAll("*")) stripVolume(child);
                }
                el.dataset.cmFlat = "1";
                el.dataset.cmSig = sig;
                el.classList.add("cm-no-volume");
                touched.add(el);
              };

              const flattenEl = (el) => {
                if (!el || el.nodeType !== 1 || onBoard(el)) return;
                if (!isHostControl(el)) return;
                const tag = (el.tagName || "").toLowerCase();
                if (el.closest && el.closest("[data-cm-flat]") && el.closest("[data-cm-flat]") !== el) return;
                if (el.querySelector && el.querySelector("button")) return;
                const scope = el.parentElement;
                if (tag !== "button" && scope && scope.querySelector && scope.querySelector(":scope > button")) return;
                const box = el.getBoundingClientRect();
                const buttonSized = box.height >= 16 && box.height <= 96 && box.width >= 24 && box.width <= 640;
                if (!buttonSized && tag !== "button") return;
                paintFlat(el);
              };

              const flattenRoot = (root) => {
                if (!root || !root.querySelectorAll) return;
                flattenEl(root.body || root.host || null);
                const marked = root.querySelectorAll("button, [role='button'], [class*='cc-button']");
                for (const el of marked) flattenEl(el);
                for (const el of root.querySelectorAll("*")) {
                  flattenEl(el);
                  if (!el.shadowRoot) continue;
                  const tag = (el.tagName || "").toLowerCase();
                  if (tag === "wc-chess-board" || tag.includes("chess-board")) continue;
                  flattenRoot(el.shadowRoot);
                }
              };

              const schedule = () => {
                if (!alive() || !enabled || queued) return;
                queued = true;
                requestAnimationFrame(() => {
                  queued = false;
                  if (!enabled) return;
                  flattenRoot(document);
                  walk(document);
                });
              };

              const clearFlatten = () => {
                for (const el of touched) {
                  try { dropPaint(el); } catch (e) {}
                }
                touched.clear();
              };

              const apply = (on) => {
                if (!alive()) return;
                bindPointer();
                enabled = !!on;
                if (!enabled) {
                  for (const style of styled) {
                    try { style.remove(); } catch (e) {}
                  }
                  styled.clear();
                  clearFlatten();
                  if (observer) observer.disconnect();
                  return;
                }
                document.documentElement.setAttribute("data-cm-root", "1");
                flattenRoot(document);
                mount(document);
                walk(document);
                flattenRoot(document);
                [400, 1000, 2000, 4000].forEach((ms) => {
                  setTimeout(() => { if (alive() && enabled) flattenRoot(document); }, ms);
                });
                if (!observer) {
                  observer = new MutationObserver(schedule);
                }
                observer.observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ["class", "aria-selected", "aria-pressed", "aria-current"] });
              };

              if (!Element.prototype.__chessmasterFlat) {
                const orig = Element.prototype.attachShadow;
                Element.prototype.attachShadow = function (init) {
                  const shadow = orig.call(this, init);
                  const tag = (this.tagName || "").toLowerCase();
                  if (enabled && tag !== "wc-chess-board" && !tag.includes("chess-board")) {
                    queueMicrotask(() => { try { mount(shadow); } catch (e) {} });
                  }
                  return shadow;
                };
                Element.prototype.__chessmasterFlat = true;
              }

              window.chessmasterApplyFlat = apply;
              window.chessmasterFlatDebug = () => {
                const els = [...document.querySelectorAll("[data-cm-flat]")];
                return {
                  version: VERSION,
                  count: els.length,
                  sample: els.slice(0, 10).map((el) => ({
                    tag: el.tagName,
                    rest: el.style.getPropertyValue("--cm-rest"),
                    hover: el.style.getPropertyValue("--cm-hover"),
                    state: el.dataset.cmState || "",
                    text: (el.innerText || "").replace(/\s+/g, " ").slice(0, 40)
                  }))
                };
              };
              document.addEventListener("DOMContentLoaded", () => { if (alive()) apply(enabled); });
              window.addEventListener("load", () => { if (alive()) apply(enabled); });
            })();
            """;
    }

    private const string ScrollbarScript = """
        (() => {
          const id = "chessmaster-scrollbar";
          const css = `
            ::-webkit-scrollbar { width: 10px !important; height: 10px !important; background: #0F0F10 !important; }
            ::-webkit-scrollbar-track { background: #0F0F10 !important; border: 0 !important; border-radius: 0 !important; box-shadow: none !important; }
            ::-webkit-scrollbar-thumb { background: #36363A !important; border: 0 !important; border-radius: 0 !important; box-shadow: none !important; }
            ::-webkit-scrollbar-thumb:hover { background: #4A4A50 !important; }
            ::-webkit-scrollbar-thumb:active { background: #1F1F21 !important; }
            ::-webkit-scrollbar-button,
            ::-webkit-scrollbar-button:single-button,
            ::-webkit-scrollbar-button:vertical:start:decrement,
            ::-webkit-scrollbar-button:vertical:end:increment,
            ::-webkit-scrollbar-button:horizontal:start:decrement,
            ::-webkit-scrollbar-button:horizontal:end:increment { width: 0 !important; height: 0 !important; display: none !important; background: none !important; }
            ::-webkit-scrollbar-corner { background: #070708 !important; border-radius: 0 !important; }
          `;
          const mount = () => {
            const parent = document.head || document.documentElement;
            if (!parent) return;
            let style = document.getElementById(id);
            if (!style) {
              style = document.createElement("style");
              style.id = id;
            }
            style.textContent = css;
            parent.appendChild(style);
          };
          mount();
          document.addEventListener("DOMContentLoaded", mount);
          window.addEventListener("load", mount);
        })();
        """;

    private static void HookBoardSwap(CoreWebView2 core)
    {
        Directory.CreateDirectory(BoardsDirectory);
        var localBoard = Path.Combine(BoardsDirectory, "board.png");
        if (!File.Exists(localBoard))
        {
            foreach (var bundled in new[]
            {
                Path.Combine(AppContext.BaseDirectory, "board.png"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "board.png"),
            })
            {
                if (File.Exists(bundled))
                {
                    File.Copy(bundled, localBoard);
                    break;
                }
            }
        }

        core.SetVirtualHostNameToFolderMapping(
            "chessmaster.boards",
            BoardsDirectory,
            CoreWebView2HostResourceAccessKind.Allow);
        core.AddWebResourceRequestedFilter(
            "https://assets-themes.chess.com/*",
            CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.All);
        core.AddWebResourceRequestedFilter(
            "https://images.chesscomfiles.com/*",
            CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.All);
        core.AddWebResourceRequestedFilter(
            "https://www.chess.com/*",
            CoreWebView2WebResourceContext.Image,
            CoreWebView2WebResourceRequestSourceKinds.All);
        core.AddWebResourceRequestedFilter(
            "https://chessmaster.boards/*",
            CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.All);
        core.WebResourceRequested += OnBoardRequested;
    }

    private static void OnBoardRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (sender is not CoreWebView2 core)
        {
            return;
        }

        var uri = e.Request.Uri;
        if (!IsLocalBoardUri(uri))
        {
            return;
        }

        var local = ResolveBoardFile(uri);
        if (local is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(Path.Combine(Root, "hook.log"), uri + Environment.NewLine);
        }
        catch (IOException)
        {
        }

        var stream = new MemoryStream(File.ReadAllBytes(local), writable: false);
        e.Response = core.Environment.CreateWebResourceResponse(
            stream,
            200,
            "OK",
            "Content-Type: image/png\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-store\r\n");
    }

    private static string? ResolveBoardFile(string uri)
    {
        var folder = BoardsDirectory;
        if (!Directory.Exists(folder))
        {
            return null;
        }

        var all = Path.Combine(folder, "board.png");
        if (File.Exists(all))
        {
            return all;
        }

        try
        {
            var parts = new Uri(uri).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var index = Array.FindIndex(parts, part => part.Equals("boards", StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && index + 1 < parts.Length)
            {
                var theme = parts[index + 1];
                var named = Path.Combine(folder, theme + ".png");
                if (File.Exists(named))
                {
                    return named;
                }
            }
        }
        catch (UriFormatException)
        {
        }

        return null;
    }

    private static bool IsLocalBoardUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return false;
        }

        if (uri.Contains("chessmaster.boards", StringComparison.OrdinalIgnoreCase)
            || uri.Contains("/chess-themes/boards/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!uri.Contains("assets-themes.chess.com/image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                new Uri(uri).AbsolutePath,
                @"^/image/[^/]+/\d+\.png$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static async Task DumpBoardsLater(HookedPage page)
    {
        try
        {
            await Task.Delay(4000);
            var dump = await page.Core.ExecuteScriptAsync("""
                (() => {
                  const sel = ".recommended-activity-board-preview, .daily-puzzle-preview, .recommended-activity-board, wc-chess-board, .board";
                  const nodes = [...document.querySelectorAll(sel)].map((el) => {
                    const s = getComputedStyle(el);
                    return {
                      cls: String(el.className || "").slice(0, 80),
                      color: s.backgroundColor,
                      size: s.backgroundSize,
                      pos: s.backgroundPosition,
                      repeat: s.backgroundRepeat,
                      bg: s.backgroundImage
                    };
                  });
                  return JSON.stringify({ count: nodes.length, nodes });
                })()
                """);
            File.WriteAllText(Path.Combine(Root, "board-debug.json"), dump ?? "");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or COMException or IOException)
        {
        }
    }

    private static string BoardLookScript()
    {
        const string tile = "https://chessmaster.boards/board.png";
        return $$"""
            (() => {
              const TILE = "{{tile}}";
              const SELECTORS = "wc-chess-board, chess-board, cc-chess-board, #board-board, #board, .board, .recommended-activity-board-preview, .recommended-activity-board, .daily-puzzle-preview";
              const BOARD_LAYER = /url\((['"]?)https?:\/\/assets-themes\.chess\.com\/image\/[^/]+\/\d+\.png\1\)/gi;
              const LOCAL = `url("${TILE}")`;
              const paint = (el) => {
                if (!el || !el.style || el.classList.contains("recommended-activity-board")) return;
                const s = getComputedStyle(el);
                let images = s.backgroundImage || "none";
                if (images === "none") {
                  el.style.setProperty("background-image", LOCAL, "important");
                  el.style.setProperty("background-size", "cover", "important");
                  el.style.setProperty("background-repeat", "no-repeat", "important");
                  el.style.setProperty("background-position", "0 0", "important");
                  return;
                }
                const hasOfficial = BOARD_LAYER.test(images);
                BOARD_LAYER.lastIndex = 0;
                const hasLocal = images.includes("chessmaster.boards/board.png");
                if (hasOfficial) {
                  el.style.setProperty("background-image", images.replace(BOARD_LAYER, LOCAL), "important");
                } else if (!hasLocal) {
                  el.style.setProperty("background-image", `${images}, ${LOCAL}`, "important");
                  el.style.setProperty("background-size", `${s.backgroundSize}, cover`, "important");
                  el.style.setProperty("background-position", `${s.backgroundPosition}, 0 0`, "important");
                  el.style.setProperty("background-repeat", `${s.backgroundRepeat}, no-repeat`, "important");
                }
                el.style.setProperty("background-color", "transparent", "important");
              };
              const walk = (root) => {
                root.querySelectorAll(SELECTORS).forEach(paint);
                root.querySelectorAll("*").forEach((el) => {
                  if (el.shadowRoot) walk(el.shadowRoot);
                });
              };
              const mount = () => walk(document);
              window.chessmasterPaintBoards = mount;
              mount();
              document.addEventListener("DOMContentLoaded", mount);
              window.addEventListener("load", mount);
              setInterval(mount, 1500);
            })();
            """;
    }

    private const string PieceLookScript = """
        (() => {
          const id = "chessmaster-pieces";
          const css = `
            wc-chess-board .piece {
              background-image: none !important;
              background-color: transparent !important;
              overflow: visible !important;
            }
            wc-chess-board .piece::before,
            wc-chess-board .piece::after {
              content: "";
              position: absolute;
              left: 50%;
              transform: translateX(-50%);
              pointer-events: none;
              box-sizing: border-box;
            }
            wc-chess-board .piece.wp::before,
            wc-chess-board .piece.wn::before,
            wc-chess-board .piece.wb::before,
            wc-chess-board .piece.wr::before,
            wc-chess-board .piece.wq::before,
            wc-chess-board .piece.wk::before {
              background: linear-gradient(180deg, #F7F2E8 0%, #E3D5BF 46%, #B79A74 100%);
              box-shadow: inset 0 2px 0 rgba(255,255,255,.8), inset 0 -8px 10px rgba(80,55,25,.35), 0 0 0 1px rgba(70,48,24,.55), 0 8px 5px rgba(0,0,0,.28);
            }
            wc-chess-board .piece.bp::before,
            wc-chess-board .piece.bn::before,
            wc-chess-board .piece.bb::before,
            wc-chess-board .piece.br::before,
            wc-chess-board .piece.bq::before,
            wc-chess-board .piece.bk::before {
              background: linear-gradient(180deg, #6A645C 0%, #34302C 52%, #161311 100%);
              box-shadow: inset 0 2px 0 rgba(255,255,255,.3), inset 0 -8px 10px rgba(0,0,0,.5), 0 0 0 1px rgba(0,0,0,.45), 0 8px 5px rgba(0,0,0,.4);
            }
            wc-chess-board .piece.wp::before,
            wc-chess-board .piece.bp::before { width: 38%; height: 24%; bottom: 12%; border-radius: 46% 46% 8px 8px; }
            wc-chess-board .piece.wp::after,
            wc-chess-board .piece.bp::after { width: 28%; height: 28%; bottom: 33%; border-radius: 50%; }
            wc-chess-board .piece.wp::after { background: linear-gradient(180deg, #F8F4EC, #D5C6AE); box-shadow: inset 0 2px 0 rgba(255,255,255,.85), 0 0 0 1px rgba(70,48,24,.4); }
            wc-chess-board .piece.bp::after { background: linear-gradient(180deg, #6A645C, #241F1C); box-shadow: inset 0 2px 0 rgba(255,255,255,.25), 0 0 0 1px rgba(0,0,0,.4); }
            wc-chess-board .piece.wr::before,
            wc-chess-board .piece.br::before { width: 44%; height: 32%; bottom: 12%; border-radius: 2px; }
            wc-chess-board .piece.wr::after,
            wc-chess-board .piece.br::after { width: 48%; height: 16%; bottom: 42%; }
            wc-chess-board .piece.wr::after { background: repeating-linear-gradient(90deg, #F7F2E8 0 20%, transparent 20% 33%, #F7F2E8 33% 53%, transparent 53% 66%, #F7F2E8 66% 100%); }
            wc-chess-board .piece.br::after { background: repeating-linear-gradient(90deg, #6A645C 0 20%, transparent 20% 33%, #6A645C 33% 53%, transparent 53% 66%, #6A645C 66% 100%); }
            wc-chess-board .piece.wb::before,
            wc-chess-board .piece.bb::before { width: 40%; height: 50%; bottom: 12%; border-radius: 0; clip-path: polygon(50% 2%, 88% 100%, 12% 100%); }
            wc-chess-board .piece.wb::after,
            wc-chess-board .piece.bb::after { width: 16%; height: 16%; bottom: 56%; border-radius: 50%; }
            wc-chess-board .piece.wb::after { background: radial-gradient(circle at 50% 42%, #F8F4EC 0 42%, #6E5434 46% 56%, #E7DCC8 57% 100%); }
            wc-chess-board .piece.bb::after { background: radial-gradient(circle at 50% 42%, #6A645C 0 42%, #E6DCC8 46% 56%, #2A2623 57% 100%); }
            wc-chess-board .piece.wn::before,
            wc-chess-board .piece.bn::before {
              width: 68%; height: 62%; bottom: 12%; border-radius: 0;
              clip-path: polygon(32% 100%, 30% 70%, 16% 64%, 24% 46%, 42% 40%, 36% 26%, 50% 12%, 66% 20%, 58% 34%, 74% 38%, 90% 26%, 80% 50%, 62% 54%, 68% 100%);
            }
            wc-chess-board .piece.wn::after,
            wc-chess-board .piece.bn::after { width: 8%; height: 8%; bottom: 56%; left: 61%; border-radius: 50%; transform: none; }
            wc-chess-board .piece.wn::after { background: #2A241C; }
            wc-chess-board .piece.bn::after { background: #E6DCC8; }
            wc-chess-board .piece.wq::before,
            wc-chess-board .piece.bq::before { width: 42%; height: 38%; bottom: 12%; border-radius: 48% 48% 8px 8px; }
            wc-chess-board .piece.wq::after,
            wc-chess-board .piece.bq::after {
              width: 54%; height: 22%; bottom: 44%;
              clip-path: polygon(0 100%, 0 42%, 16% 78%, 30% 18%, 42% 78%, 50% 0, 58% 78%, 70% 18%, 84% 78%, 100% 42%, 100% 100%);
            }
            wc-chess-board .piece.wq::after { background: linear-gradient(180deg, #F8F4EC, #CDB892); }
            wc-chess-board .piece.bq::after { background: linear-gradient(180deg, #6A645C, #2A2623); }
            wc-chess-board .piece.wk::before,
            wc-chess-board .piece.bk::before { width: 40%; height: 42%; bottom: 12%; border-radius: 10px 10px 6px 6px; }
            wc-chess-board .piece.wk::after,
            wc-chess-board .piece.bk::after { width: 28%; height: 30%; bottom: 48%; }
            wc-chess-board .piece.wk::after {
              background:
                linear-gradient(#F4EFE4, #F4EFE4) center / 34% 100% no-repeat,
                linear-gradient(#F4EFE4, #F4EFE4) center / 100% 32% no-repeat;
            }
            wc-chess-board .piece.bk::after {
              background:
                linear-gradient(#E6DCC8, #E6DCC8) center / 34% 100% no-repeat,
                linear-gradient(#E6DCC8, #E6DCC8) center / 100% 32% no-repeat;
            }
          `;
          const mount = () => {
            const parent = document.head || document.documentElement;
            if (!parent) return;
            let style = document.getElementById(id);
            if (!style) {
              style = document.createElement("style");
              style.id = id;
            }
            style.textContent = css;
            parent.appendChild(style);
          };
          mount();
          document.addEventListener("DOMContentLoaded", mount);
          window.addEventListener("load", mount);
        })();
        """;
}
