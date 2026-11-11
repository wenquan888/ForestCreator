using System;
using System.Collections.Generic;
using System.IO;
using Eremite.Buildings.UI;
using Eremite.Buildings.UI.Altars;
using Eremite.Controller;
using Eremite.Model;
using Eremite.Services;
using Eremite.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForestCreator
{
    /// <summary>
    /// The card menu shown when the player clicks the Town Hall.
    ///
    /// Built entirely at runtime with uGUI (no prefabs), so it cannot break on a game update that
    /// moves prefabs around. Layout:
    ///
    ///   overlay (dim, click to close)
    ///     window
    ///       title / hint / close button
    ///       ScrollRect (horizontal)
    ///         cards: round race portrait, price (2 + amber icon), "招募" button
    ///       status line
    /// </summary>
    internal static class TownHallUI
    {
        private static GameObject _root;
        private static TMP_FontAsset _font;
        private static Sprite _rounded;
        private static Sprite _circle;

        // Matches the native Popup_BG_Big4 rect (1400x980), shortened to 75% of that height.
        private const float WindowWidth = 1400f;
        private const float WindowHeight = 735f;

        // Content insets measured off the reference window: 110 sides, 143 above the card area,
        // 96 below it for the status line.
        private const float ContentInsetX = 110f;
        private const float ContentInsetTop = 239f;
        private const float ContentInsetBottom = 196f;

        internal static bool IsOpen => _root != null;

        internal static void Show()
        {
            try
            {
                if (_root != null) { Hide(); }

                var canvas = FindCanvas();
                if (canvas == null)
                {
                    Plugin.LogError("FC TOWNHALL ui: no overlay canvas found");
                    return;
                }

                _font = FindGameFont();
                if (_rounded == null) _rounded = MakeRoundedSprite(32, 10, new Color(0.13f, 0.12f, 0.11f, 1f));
                if (_circle == null) _circle = MakeCircleSprite(128);
                LoadStaticAssetsOnce();
                LoadPersistedTheme();

                // ---- overlay ----------------------------------------------------
                _root = NewGO("FC_TownHall_Root", canvas.transform);
                var rootRt = _root.GetComponent<RectTransform>();
                Stretch(rootRt);
                var dim = _root.AddComponent<Image>();
                dim.color = new Color(0f, 0f, 0f, 0.6f);
                dim.raycastTarget = true;

                var closeOnDim = _root.AddComponent<Button>();
                closeOnDim.transition = Selectable.Transition.None;
                closeOnDim.onClick.AddListener(Hide);

                // ---- window -----------------------------------------------------
                var window = NewGO("Window", _root.transform);
                var winRt = window.GetComponent<RectTransform>();
                Center(winRt, WindowWidth, WindowHeight);

                // The native frame sprite has a transparent middle — it is only the ornaments and
                // borders. So the window is built as a fill layer with the frame drawn on top.
                var backdrop = NewGO("Backdrop", window.transform);
                var bdRt = backdrop.GetComponent<RectTransform>();
                bdRt.anchorMin = Vector2.zero;
                bdRt.anchorMax = Vector2.one;
                bdRt.offsetMin = Vector2.zero;
                bdRt.offsetMax = Vector2.zero;
                var bdImg = backdrop.AddComponent<Image>();
                if (_nativePanelSprite != null)
                {
                    bdImg.color = new Color(0.07f, 0.06f, 0.05f, 0.97f);
                }
                else
                {
                    ApplySprite(bdImg, null, _rounded);
                    bdImg.color = new Color(0.23f, 0.20f, 0.16f, 1f);
                }
                // absorbs clicks so the dim's close handler does not fire
                backdrop.AddComponent<Button>().transition = Selectable.Transition.None;

                if (_nativePanelSprite != null)
                {
                    var frame = NewGO("Frame", window.transform);
                    var frRt = frame.GetComponent<RectTransform>();
                    frRt.anchorMin = Vector2.zero;
                    frRt.anchorMax = Vector2.one;
                    frRt.offsetMin = Vector2.zero;
                    frRt.offsetMax = Vector2.zero;
                    var frImg = frame.AddComponent<Image>();
                    frImg.sprite = _nativePanelSprite;
                    var b = _nativePanelSprite.border;
                    frImg.type = (b.x > 0f || b.y > 0f || b.z > 0f || b.w > 0f)
                        ? Image.Type.Sliced
                        : Image.Type.Simple;
                    frImg.color = _nativePanelColor;
                    frImg.raycastTarget = false;
                }

                // ---- title ------------------------------------------------------
                // Gold, centred, name only — no "· Town Hall" suffix and no description line.
                var title = NewText("Title", window.transform, "城镇大厅", 34, TextAlignmentOptions.Center);
                title.color = new Color(0.83f, 0.68f, 0.32f, 1f);
                Place(title.rectTransform, ContentInsetX, -52f, WindowWidth - ContentInsetX * 2f, 46f, new Vector2(0f, 1f));

                // ---- close ------------------------------------------------------
                // The native frame already reserves a corner slot for the X, so the hit area is
                // fully transparent: only the glyph shows, sitting on that reserved spot.
                var close = NewGO("Close", window.transform);
                Place(close.GetComponent<RectTransform>(), -4f, -2f, 40f, 40f, new Vector2(1f, 1f));
                var closeImg = close.AddComponent<Image>();
                closeImg.color = new Color(0f, 0f, 0f, 0f);
                closeImg.raycastTarget = true;
                var closeBtn = close.AddComponent<Button>();
                closeBtn.targetGraphic = closeImg;
                closeBtn.onClick.AddListener(Hide);
                var closeText = NewText("X", close.transform, "", 26, TextAlignmentOptions.Center);
                closeText.color = new Color(0.92f, 0.90f, 0.86f, 1f);
                Center(closeText.rectTransform, 40f, 40f);

                // ---- status -------------------------------------------------------
                // Recruit feedback goes to the log only; the window shows nothing below the cards.

                // ---- scroll view -------------------------------------------------
                BuildRaceCards(window.transform, null);

                Plugin.LogInfo("FC TOWNHALL ui opened");
            }
            catch (Exception ex)
            {
                Plugin.LogError("FC ERR TownHallUI.Show: " + ex);
                Hide();
            }
        }

        internal static void Hide()
        {
            try
            {
                if (_root != null)
                {
                    UnityEngine.Object.Destroy(_root);
                    _root = null;
                }
            }
            catch { }
        }

        // ---- cards --------------------------------------------------------------

        /// <summary>
        /// One row of cards inside a horizontally scrollable viewport. The viewport is exactly one
        /// card tall, so nothing inside is ever taller than the mask — that is what previously
        /// clipped the bottom of the recruit button. Cards keep their own sizeDelta and the layout
        /// group is told not to control either axis.
        /// </summary>
        private static void BuildRaceCards(Transform window, TMP_Text status)
        {
            var races = Plugin.FC_PlayableRaces();

            if (races.Count == 0)
            {
                if (status != null) status.text = "没有可选种族 / no races available";
                Plugin.LogInfo("FC TOWNHALL no races available");
                return;
            }

            var scroll = NewGO("Scroll", window);
            var srt = scroll.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.offsetMax = new Vector2(-ContentInsetX, -ContentInsetTop);
            srt.offsetMin = new Vector2(ContentInsetX, -ContentInsetTop - CardHeight);

            var scrollImg = scroll.AddComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0f);
            scrollImg.raycastTarget = true;

            var viewport = NewGO("Viewport", scroll.transform);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();

            var content = NewGO("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = CardGap;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scrollRect = scroll.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRt;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 30f;

            foreach (var race in races)
            {
                BuildCard(content.transform, race, status);
            }
        }

        private static GameObject BuildCard(Transform parent, RaceModel race, TMP_Text status)
        {
            var card = NewGO("Card_" + race.name, parent);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(CardWidth, CardHeight);

            // Only feeds ContentSizeFitter its preferred width; the layout group itself is told
            // not to control size, so sizeDelta above stays authoritative.
            var le = card.AddComponent<LayoutElement>();
            le.preferredWidth = CardWidth;
            le.preferredHeight = CardHeight;

            // Card skin: dark cloth texture with the copper hairline border, sliced so the border
            // stays crisp at any size (border comes from fc_theme_card.meta.txt).
            var cardImg = card.AddComponent<Image>();
            if (_nativeCardSprite != null)
            {
                cardImg.sprite = _nativeCardSprite;
                var cb = _nativeCardSprite.border;
                cardImg.type = (cb.x > 0f || cb.y > 0f || cb.z > 0f || cb.w > 0f)
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
                cardImg.color = _nativeCardColor;
            }
            else
            {
                cardImg.sprite = _rounded;
                cardImg.type = Image.Type.Sliced;
                cardImg.color = new Color(0.11f, 0.12f, 0.13f, 1f);
            }

            var cardLayout = card.AddComponent<VerticalLayoutGroup>();
            cardLayout.spacing = 10f;
            cardLayout.padding = new RectOffset(16, 16, 16, 12);
            cardLayout.childAlignment = TextAnchor.UpperCenter;
            cardLayout.childControlWidth = true;
            // Height stays off: with it on, the group redistributes the leftover card height and
            // pushes the recruit button out of view. Every child below gets an explicit height.
            cardLayout.childControlHeight = false;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            // ---- header plaque -------------------------------------------------
            // Matches the reference card: a dark plaque with copper hairlines across the top,
            // carrying the name as gold text, instead of a bare floating label.
            string display = null;
            try { display = race.displayName != null ? race.displayName.Text : race.name; } catch { }

            var header = NewGO("Header", card.transform);
            header.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 34f);
            var headerLe = header.AddComponent<LayoutElement>();
            headerLe.preferredHeight = 34f;
            headerLe.minHeight = 34f;

            var headerImg = header.AddComponent<Image>();
            if (_cardHeaderSprite != null)
            {
                headerImg.sprite = _cardHeaderSprite;
                var hb = _cardHeaderSprite.border;
                headerImg.type = (hb.x > 0f || hb.y > 0f || hb.z > 0f || hb.w > 0f)
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
                headerImg.color = Color.white;
            }
            else
            {
                headerImg.color = new Color(0.10f, 0.10f, 0.11f, 0.95f);
            }
            headerImg.raycastTarget = false;

            var headerText = NewText("HeaderLabel", header.transform, display, 22, TextAlignmentOptions.Center);
            headerText.color = new Color(0.86f, 0.70f, 0.34f, 1f);
            Center(headerText.rectTransform, CardWidth - 24f, 34f);

            // ---- round portrait ------------------------------------------------
            var avatarBox = NewGO("AvatarBox", card.transform);
            avatarBox.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 112f);
            var avatarLe = avatarBox.AddComponent<LayoutElement>();
            avatarLe.preferredWidth = 112f;
            avatarLe.preferredHeight = 112f;
            avatarLe.minHeight = 112f;

            var avatar = NewGO("Avatar", avatarBox.transform);
            Stretch(avatar.GetComponent<RectTransform>());
            var avatarImg = avatar.AddComponent<Image>();
            Sprite portrait = null;
            try { portrait = race.roundIcon != null ? race.roundIcon : race.icon; } catch { }
            if (portrait == null) portrait = _circle;
            avatarImg.sprite = portrait;
            avatarImg.preserveAspect = true;
            avatarImg.raycastTarget = false;

            // copper ring on top of the portrait, like the framed icons in the reference window
            if (_avatarRingSprite != null)
            {
                var ring = NewGO("Ring", avatarBox.transform);
                Stretch(ring.GetComponent<RectTransform>());
                var ringImg = ring.AddComponent<Image>();
                ringImg.sprite = _avatarRingSprite;
                ringImg.color = Color.white;
                ringImg.preserveAspect = true;
                ringImg.raycastTarget = false;
            }

            // ---- price ----------------------------------------------------------
            var priceRow = NewGO("Price", card.transform);
            priceRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 34f);
            var priceLe = priceRow.AddComponent<LayoutElement>();
            priceLe.preferredHeight = 34f;
            priceLe.minHeight = 34f;
            var priceLayout = priceRow.AddComponent<HorizontalLayoutGroup>();
            priceLayout.spacing = 6f;
            priceLayout.childAlignment = TextAnchor.MiddleCenter;
            priceLayout.childControlWidth = true;
            priceLayout.childControlHeight = true;
            priceLayout.childForceExpandWidth = false;

            var priceText = NewText("PriceText", priceRow.transform, Plugin.FC_TownHallPrice.ToString(), 26,
                TextAlignmentOptions.Center);
            var ptLe = priceText.gameObject.AddComponent<LayoutElement>();
            ptLe.preferredWidth = 40f;
            ptLe.minWidth = 34f;

            var amberIcon = NewGO("AmberIcon", priceRow.transform);
            var amberLe = amberIcon.AddComponent<LayoutElement>();
            amberLe.preferredWidth = 34f;
            amberLe.preferredHeight = 34f;
            var amberImg = amberIcon.AddComponent<Image>();
            amberImg.sprite = FindAmberIcon();
            amberImg.preserveAspect = true;
            amberImg.raycastTarget = false;

            // ---- recruit button -------------------------------------------------
            var btn = NewGO("Recruit", card.transform);
            btn.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, ButtonHeight);
            var btnLe = btn.AddComponent<LayoutElement>();
            // 65% of the previous 44 height.
            btnLe.preferredHeight = ButtonHeight;
            btnLe.minHeight = ButtonHeight;
            btnLe.flexibleHeight = 0f;

            var btnImg = btn.AddComponent<Image>();
            ApplySprite(btnImg, _nativeButtonSprite, _rounded);
            if (_nativeButtonSprite == null) btnImg.color = new Color(0.62f, 0.46f, 0.19f, 1f);

            var button = btn.AddComponent<Button>();
            button.targetGraphic = btnImg;

            var btnText = NewText("Label", btn.transform, "招募", 20, TextAlignmentOptions.Center);
            // Fixed size instead of a stretch anchor: the button's own rect is sized by the layout
            // group a frame later, and a 0-sized stretch made the label render as nothing.
            Center(btnText.rectTransform, CardWidth - 32f, ButtonHeight);

            var capturedRace = race;
            button.onClick.AddListener(() =>
            {
                try
                {
                    string msg = Plugin.FC_RecruitVillager(capturedRace);
                    Plugin.LogInfo("FC TOWNHALL ui " + msg + " 琥珀=" + Plugin.FC_AmberAmount());
                    if (status != null) status.text = msg;
                }
                catch (Exception ex)
                {
                    if (status != null) status.text = "错误 / error";
                    Plugin.LogError("FC ERR recruit click: " + ex.Message);
                }
            });

            return card;
        }

        // ---- ui helpers ---------------------------------------------------------

        private static GameObject NewGO(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            return go;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size,
            TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().localScale = Vector3.one;

            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text ?? "";
            t.fontSize = size;
            t.alignment = align;
            t.color = new Color(0.93f, 0.90f, 0.84f, 1f);
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rt, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h, Vector2 anchor)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        // ---- native theme -------------------------------------------------------

        private static Sprite _nativePanelSprite;
        private static Color _nativePanelColor = Color.white;
        private static Sprite _nativeButtonSprite;
        private static Color _nativeButtonColor = Color.white;
        private static Sprite _nativeCardSprite;
        private static Color _nativeCardColor = Color.white;
        private static Sprite _cardHeaderSprite;
        private static Sprite _avatarRingSprite;
        private static bool _staticAssetsLoaded;

        // The forge/relic window only exists on certain maps, so the skin cannot be collected on
        // demand every session. Whatever we read off a live popup gets written next to the dll as
        // plain PNG + a small border sidecar, and reloaded at startup from there.
        private const string PanelPng = "fc_theme_panel.png";
        private const string ButtonPng = "fc_theme_button.png";
        private const string CardPng = "fc_theme_card.png";
        private const string CardHeaderPng = "fc_theme_card_header.png";
        private const string AvatarRingPng = "fc_theme_avatar_ring.png";

        private const float CardWidth = 220f;
        private const float CardGap = 15f;
        private const float ButtonHeight = 52f;
        private const float CardHeight = 300f;

        private static bool _diskThemeChecked;
        private static bool _diskThemeLoaded;

        private static Type FindImageConversionType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType("UnityEngine.ImageConversion", false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }


        private static bool DecodePng(Texture2D tex, byte[] bytes)
        {
            try
            {
                var m = typeof(Texture2D).GetMethod("LoadImage", new[] { typeof(byte[]) });
                if (m != null) { m.Invoke(tex, new object[] { bytes }); return true; }
            }
            catch { }

            try
            {
                var ic = FindImageConversionType();
                if (ic != null)
                {
                    var m = ic.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
                    if (m != null) { m.Invoke(null, new object[] { tex, bytes }); return true; }
                }
            }
            catch { }

            return false;
        }

        private static string ThemeAssetsDir()
        {
            try
            {
                var loc = typeof(TownHallUI).Assembly.Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    var dir = Path.Combine(Path.GetDirectoryName(loc), "Assets");
                    return dir;
                }
            }
            catch { }

            try
            {
                if (!string.IsNullOrEmpty(BepInEx.Paths.PluginPath))
                    return Path.Combine(BepInEx.Paths.PluginPath, "TuHua_Technology-ForestCreator", "Assets");
            }
            catch { }

            return null;
        }


        private static Sprite LoadPersistedSprite(string fileName)
        {
            try
            {
                var dir = ThemeAssetsDir();
                if (string.IsNullOrEmpty(dir)) return null;

                string pngPath = Path.Combine(dir, fileName);
                if (!File.Exists(pngPath)) return null;

                var bytes = File.ReadAllBytes(pngPath);
                if (bytes == null || bytes.Length == 0) return null;

                var tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!DecodePng(tex, bytes)) return null;

                Vector4 border = Vector4.zero;
                float ppu = 100f;
                string metaPath = Path.ChangeExtension(pngPath, ".meta.txt");
                if (File.Exists(metaPath))
                {
                    var parts = File.ReadAllText(metaPath).Split(',');
                    if (parts.Length >= 5)
                    {
                        border = new Vector4(
                            float.Parse(parts[0]), float.Parse(parts[1]),
                            float.Parse(parts[2]), float.Parse(parts[3]));
                        ppu = float.Parse(parts[4]);
                    }
                }

                var sprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    ppu,
                    0,
                    SpriteMeshType.FullRect,
                    border);

                return sprite;
            }
            catch (Exception ex)
            {
                Plugin.FileLog("theme: load failed for " + fileName + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Card face, header plaque and avatar ring ship with the mod rather than being captured
        /// from the game, so they load independently of whether a native popup was ever seen.
        /// </summary>
        private static void LoadStaticAssetsOnce()
        {
            if (_staticAssetsLoaded) return;
            _staticAssetsLoaded = true;
            try
            {
                var card = LoadPersistedSprite(CardPng);
                if (card != null) { _nativeCardSprite = card; _nativeCardColor = Color.white; }

                var head = LoadPersistedSprite(CardHeaderPng);
                if (head != null) _cardHeaderSprite = head;

                var ring = LoadPersistedSprite(AvatarRingPng);
                if (ring != null) _avatarRingSprite = ring;

                Plugin.FileLog("assets: card=" + (card != null) + " header=" + (head != null)
                    + " ring=" + (ring != null));
            }
            catch (Exception ex)
            {
                Plugin.FileLog("assets: static load failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Startup path: if a previous session already saved the skin, use it and never touch the
        /// live-popup scavenging again.
        /// </summary>
        private static bool LoadPersistedTheme()
        {
            if (_diskThemeChecked) return _diskThemeLoaded;
            _diskThemeChecked = true;

            try
            {
                var panel = LoadPersistedSprite(PanelPng);
                if (panel == null) return false;

                _nativePanelSprite = panel;
                _nativePanelColor = Color.white;

                var button = LoadPersistedSprite(ButtonPng);
                if (button != null) { _nativeButtonSprite = button; _nativeButtonColor = Color.white; }


                // Card face is shipped as an asset (cropped from in-game cloth + copper hairline),
                // not captured at runtime, so it is available on every map.
                var card = LoadPersistedSprite(CardPng);
                if (card != null) { _nativeCardSprite = card; _nativeCardColor = Color.white; }

                _diskThemeLoaded = true;
                Plugin.FileLog("theme: loaded persisted skin from Assets");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.FileLog("theme: persisted skin load failed: " + ex.Message);
                return false;
            }
        }




        /// <summary>
        /// Spawns one instance of a native UI type to read its sprites from. PrefabAttribute.PrefabPath
        /// does GetCustomAttributes(...)[0], so a type without a [Prefab] annotation throws an index
        /// error instead of returning a path — hence the explicit path lookup plus manual instantiation
        /// rather than PrefabsService.GetInstance. The object is parented before its Awake runs, so an
        /// exception thrown while the panel builds itself still leaves a recoverable instance behind.
        /// </summary>
        private static GameObject SpawnThemeSource<T>(Transform parent) where T : Component
        {
            try
            {
                string path = PrefabAttribute.PrefabPath<T>();
                if (string.IsNullOrEmpty(path))
                {
                    Plugin.FileLog("theme: " + typeof(T).Name + " has no prefab path");
                    return null;
                }

                var prefab = Resources.Load<GameObject>(path);
                if (prefab == null)
                {
                    Plugin.FileLog("theme: no prefab at " + path);
                    return null;
                }

                int before = parent.childCount;
                try
                {
                    return UnityEngine.Object.Instantiate(prefab, parent, false);
                }
                catch (Exception ex)
                {
                    Plugin.FileLog("theme: " + typeof(T).Name + " threw on Instantiate: " + ex.Message);
                    for (int i = before; i < parent.childCount; i++)
                    {
                        var child = parent.GetChild(i);
                        if (child != null) return child.gameObject;
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Plugin.FileLog("theme: " + typeof(T).Name + " unavailable: " + ex.Message);
                return null;
            }
        }


        private static void ApplySprite(Image img, Sprite native, Sprite fallback)
        {
            img.sprite = native != null ? native : fallback;
            if (img.sprite == null) return;

            var b = img.sprite.border;
            bool hasBorder = b.x > 0f || b.y > 0f || b.z > 0f || b.w > 0f;
            img.type = hasBorder ? Image.Type.Sliced : Image.Type.Simple;
            img.color = Color.white;
        }

        /// <summary>
        /// The canvas the window hangs off. Two things matter here:
        /// 1) it must be the game's own overlay canvas, never a debug tool's — UnityExplorer (F7)
        ///    creates its own ScreenSpaceOverlay canvas with a much larger sortingOrder and a
        ///    different scaleFactor, so picking "the last one" made this window render half-size
        ///    whenever the inspector was open;
        /// 2) the first good result is cached, so the window keeps the same scale for the session
        ///    even if some other canvas appears later.
        /// </summary>
        private static Canvas _cachedCanvas;

        private static bool IsForeignCanvas(Canvas c)
        {
            try
            {
                var t = c.transform;
                for (int i = 0; i < 12 && t != null; i++)
                {
                    var n = t.name;
                    if (!string.IsNullOrEmpty(n))
                    {
                        var low = n.ToLowerInvariant();
                        if (low.Contains("explorer") || low.Contains("universelib")
                            || low.Contains("universe") || low.StartsWith("ue_"))
                            return true;
                    }
                    t = t.parent;
                }
            }
            catch { }
            return false;
        }

        private static Canvas FindCanvas()
        {
            if (_cachedCanvas != null && _cachedCanvas.isActiveAndEnabled) return _cachedCanvas;

            Canvas best = null;
            try
            {
                foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
                {
                    if (c == null || !c.isActiveAndEnabled) continue;
                    if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    if (IsForeignCanvas(c)) continue;
                    if (best == null || c.sortingOrder >= best.sortingOrder) best = c;
                }
            }
            catch { }

            _cachedCanvas = best;
            return best;
        }

        private static TMP_FontAsset FindGameFont()
        {
            try
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    if (t != null && t.font != null) return t.font;
                }
            }
            catch { }
            return TMP_Settings.defaultFontAsset;
        }

        private static Sprite FindAmberIcon()
        {
            // Walk Settings.Goods and match the amber model by its internal name. ToGoodModel() with a
            // plain "Amber" string silently failed, which is why the old build drew a white circle.
            try
            {
                var settings = MainController.Instance != null ? MainController.Instance.Settings : null;
                if (settings != null && settings.Goods != null)
                {
                    foreach (var g in settings.Goods)
                    {
                        if (g == null || g.icon == null) continue;
                        string n = null;
                        try { n = g.Name; } catch { }
                        if (string.IsNullOrEmpty(n)) n = g.name;
                        if (string.IsNullOrEmpty(n)) continue;
                        if (n.IndexOf("Amber", StringComparison.OrdinalIgnoreCase) >= 0) return g.icon;
                    }
                }
            }
            catch { }

            try
            {
                var model = ATS_API.Helpers.GoodsTypesExtensions.ToGoodModel(Plugin.FC_TownHallCurrency);
                if (model != null && model.icon != null) return model.icon;
            }
            catch { }
            return _circle;
        }

        private static Sprite MakeRoundedSprite(int size, int radius, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0f, dy = 0f;
                    if (x < radius) dx = radius - x - 0.5f;
                    else if (x >= size - radius) dx = x - (size - radius) + 0.5f;
                    if (y < radius) dy = radius - y - 0.5f;
                    else if (y >= size - radius) dy = y - (size - radius) + 0.5f;

                    bool inside = (dx * dx + dy * dy) <= radius * radius + 0.001f;
                    px[y * size + x] = inside ? color : new Color(0f, 0f, 0f, 0f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
        }

        private static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float r = size * 0.5f - 1f;
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c;
                    float dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
