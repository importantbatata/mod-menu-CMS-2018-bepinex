using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CMS18ModMenu
{
    internal sealed class MenuPanel : IDisposable
    {
        private static readonly Color Surface = new Color32(33, 33, 33, 249);
        private static readonly Color Row = new Color32(51, 51, 51, 255);
        private static readonly Color Raised = new Color32(64, 64, 64, 255);
        private static readonly Color TextColor = new Color32(245, 245, 245, 255);
        private static readonly Color Muted = new Color32(193, 193, 193, 255);
        private static readonly Color Clear = new Color(0, 0, 0, 0);
        private const string WorkshopUrl = "https://steamcommunity.com/app/645630/workshop/";

        private readonly GameObject root;
        private readonly Sprite roundedSprite;
        private readonly RectTransform content;
        private readonly ScrollRect scroll;
        private readonly RectTransform workshopViewport;
        private readonly RectTransform pluginSection;
        private readonly RectTransform pluginList;
        private readonly RectTransform pluginDetail;
        private readonly ScrollRect pluginListScroll;
        private readonly ScrollRect pluginDetailScroll;
        private readonly List<Image> pluginSelections = new List<Image>();
        private readonly Button pluginsTab;
        private readonly Button workshopTab;
        private readonly Image pluginsIndicator;
        private readonly Image workshopIndicator;
        private readonly Button browse;
        private readonly Text status;
        private readonly Font font;
        private readonly Func<PluginInfo[]> readPlugins;
        private readonly WorkshopCatalog catalog;
        private bool showWorkshop;
        private string selectedPluginId;

        public bool IsOpen { get { return root != null && root.activeSelf; } }

        public MenuPanel(Scene scene, Font nativeFont, Image nativeUnderline, Action close, Func<PluginInfo[]> readPlugins, ManualLogSource logger)
        {
            this.readPlugins = readPlugins;
            catalog = new WorkshopCatalog(logger, WorkshopUpdated);
            font = nativeFont != null ? nativeFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            roundedSprite = CreateRoundedSprite();
            root = new GameObject("CMS18 Mods Panel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.SetActive(false);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var dimmer = Box(root.transform, "Backdrop", new Color(0, 0, 0, 0.65f));
            Fill(dimmer);
            var body = RoundedBox(dimmer, "Mods", Surface);
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(900, 588);
            body.anchoredPosition = Vector2.zero;

            Label(body, "Mods", 31, TextColor, 24, 12, 580, 48).fontStyle = FontStyle.Bold;
            ActionButton(body, "Close", 779, 19, 97, 36, close);
            pluginsTab = ActionButton(body, "BepInEx Mods", 24, 72, 178, 42, delegate { ChooseTab(false); });
            workshopTab = ActionButton(body, "Steam Workshop", 210, 72, 192, 42, delegate { ChooseTab(true); });
            pluginsIndicator = TabUnderline(pluginsTab, nativeUnderline);
            workshopIndicator = TabUnderline(workshopTab, nativeUnderline);
            ActionButton(body, "Refresh", 779, 75, 97, 36, Refresh);

            status = Label(body, "", 15, Muted, 24, 122, 852, 28);
            var viewport = RoundedBox(body, "List", new Color32(25, 25, 25, 255));
            workshopViewport = viewport;
            Place(viewport, 24, 156, 852, 365);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 29;

            content = Box(viewport, "Rows", Clear);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0, 1);
            content.sizeDelta = Vector2.zero;
            content.GetComponent<Image>().raycastTarget = false;
            scroll.content = content;

            pluginSection = Box(body, "BepInEx browser", Clear);
            Place(pluginSection, 24, 156, 852, 365);
            pluginSection.GetComponent<Image>().raycastTarget = false;
            RectTransform divider = Box(pluginSection, "Divider", new Color32(96, 96, 96, 255));
            Place(divider, 310, 0, 1, 365);
            divider.GetComponent<Image>().raycastTarget = false;
            pluginListScroll = PluginScroll(pluginSection, "Installed plugins", 0, 294, out pluginList);
            pluginDetailScroll = PluginScroll(pluginSection, "Plugin details", 330, 522, out pluginDetail);
            browse = ActionButton(body, "Browse Workshop", 692, 537, 184, 35, delegate { OpenSteamPage(WorkshopUrl); });
            Label(body, "Esc to close", 14, Muted, 24, 537, 300, 35);
        }

        public void Open()
        {
            root.SetActive(true);
            ChooseTab(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(pluginsTab.gameObject);
        }

        public void Close()
        {
            if (root != null)
                root.SetActive(false);
            catalog.Cancel();
        }

        private void ChooseTab(bool workshop)
        {
            showWorkshop = workshop;
            pluginsIndicator.enabled = !workshop;
            workshopIndicator.enabled = workshop;
            workshopViewport.gameObject.SetActive(workshop);
            pluginSection.gameObject.SetActive(!workshop);
            browse.gameObject.SetActive(workshop);
            Refresh();
        }

        private static Image TabUnderline(Button tab, Image nativeUnderline)
        {
            bool hasNativeSprite = nativeUnderline != null && nativeUnderline.sprite != null;
            var rect = Box(tab.transform, "Selection", hasNativeSprite ? nativeUnderline.color : (Color)new Color32(255, 164, 0, 255));
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.right;
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(-12, 4);
            rect.anchoredPosition = Vector2.zero;
            Image image = rect.GetComponent<Image>();
            if (hasNativeSprite)
                image.sprite = nativeUnderline.sprite;
            image.raycastTarget = false;
            return image;
        }

        private void Refresh()
        {
            if (showWorkshop)
            {
                catalog.Refresh();
                return;
            }

            catalog.Cancel();
            ClearRows(pluginList);
            pluginSelections.Clear();
            PluginInfo[] plugins = readPlugins();
            status.text = plugins.Length == 1 ? "1 loaded plugin" : plugins.Length + " loaded plugins";
            int selectedIndex = Array.FindIndex(plugins, plugin => plugin.Metadata.GUID == selectedPluginId);
            for (int i = 0; i < plugins.Length; i++)
                AddPluginLink(plugins[i], i);

            SetScrollLength(pluginListScroll, Mathf.Max(365, plugins.Length * 54));
            if (plugins.Length == 0)
            {
                ClearRows(pluginDetail);
                Label(pluginDetail, "No BepInEx plugins are loaded.", 17, Muted, 0, 12, 515, 34);
                SetScrollLength(pluginDetailScroll, 365);
            }
            else
                ShowPlugin(plugins[Mathf.Max(0, selectedIndex)], Mathf.Max(0, selectedIndex));
        }

        private ScrollRect PluginScroll(Transform parent, string name, int x, int width, out RectTransform rows)
        {
            RectTransform viewport = Box(parent, name, Clear);
            Place(viewport, x, 0, width, 365);
            viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect control = viewport.gameObject.AddComponent<ScrollRect>();
            control.viewport = viewport;
            control.horizontal = false;
            control.movementType = ScrollRect.MovementType.Clamped;
            control.scrollSensitivity = 28;
            rows = Box(viewport, "Content", Clear);
            Place(rows, 0, 0, width, 365);
            rows.GetComponent<Image>().raycastTarget = false;
            control.content = rows;
            return control;
        }

        private void AddPluginLink(PluginInfo plugin, int index)
        {
            var colors = new ColorBlock
            {
                normalColor = Clear,
                highlightedColor = new Color32(255, 255, 255, 22),
                pressedColor = new Color32(255, 255, 255, 37),
                disabledColor = Clear,
                colorMultiplier = 1
            };
            RectTransform row = RoundedBox(pluginList, plugin.Metadata.Name, Color.white);
            Place(row, 0, index * 54, 294, 51);
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = row.GetComponent<Image>();
            button.colors = colors;
            button.onClick.AddListener(delegate { PlayClick(); ShowPlugin(plugin, index); });
            var selection = Box(row, "Selection", new Color32(255, 164, 0, 255)).GetComponent<Image>();
            Place((RectTransform)selection.transform, 0, 8, 3, 34);
            selection.raycastTarget = false;
            pluginSelections.Add(selection);
            Text name = Label(row, plugin.Metadata.Name, 19, TextColor, 12, 4, 255, 27);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 14;
            name.resizeTextMaxSize = 19;
            Label(row, "v" + plugin.Metadata.Version, 14, Muted, 12, 29, 230, 19);
            Label(row, "›", 20, Muted, 274, 7, 16, 35);
        }

        private void ShowPlugin(PluginInfo plugin, int index)
        {
            selectedPluginId = plugin.Metadata.GUID;
            for (int i = 0; i < pluginSelections.Count; i++)
                pluginSelections[i].enabled = i == index;

            ClearRows(pluginDetail);
            var details = new PluginDetails(plugin);
            Text heading = Label(pluginDetail, plugin.Metadata.Name, 25, TextColor, 0, 0, 515, 40);
            heading.fontStyle = FontStyle.Bold;
            heading.resizeTextForBestFit = true;
            heading.resizeTextMinSize = 17;
            heading.resizeTextMaxSize = 25;
            Label(pluginDetail, "v" + plugin.Metadata.Version + "   ·   " + details.FileName, 14, Muted, 0, 44, 515, 25);
            int next = AddDetailText(details.Summary, 17, TextColor, 79);
            if (details.Source.Length > 0)
                next = AddDetailText(details.Source, 13, Muted, next + 3);
            if (details.Settings.Length > 0)
            {
                next = AddDetailText("Settings exposed by the plugin", 18, TextColor, next + 14);
                next = AddDetailText(details.Settings, 15, Muted, next);
            }
            SetScrollLength(pluginDetailScroll, Mathf.Max(365, next + 12));
        }

        private int AddDetailText(string value, int size, Color color, int top)
        {
            Text text = Label(pluginDetail, value, size, color, 0, top, 508, 30);
            text.alignment = TextAnchor.UpperLeft;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            int height = Mathf.CeilToInt(text.preferredHeight) + 3;
            ((RectTransform)text.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            return top + height + 5;
        }

        private void WorkshopUpdated(bool resetScroll)
        {
            if (!IsOpen || !showWorkshop)
                return;

            if (!resetScroll)
            {
                for (int i = 0; i < catalog.Entries.Count && i < content.childCount; i++)
                {
                    Transform name = content.GetChild(i).Find("Title");
                    if (name != null)
                        name.GetComponent<Text>().text = catalog.Entries[i].Title;
                }
                return;
            }

            ClearRows(content);
            int count = catalog.Entries.Count;
            status.text = catalog.Available
                ? (count == 1 ? "1 subscribed item" : count + " subscribed items")
                : "Steam Workshop unavailable";
            for (int i = 0; i < count; i++)
            {
                WorkshopEntry entry = catalog.Entries[i];
                AddRow(i, entry.Title, "Workshop ID " + entry.Id, entry.State,
                    delegate { OpenSteamPage("https://steamcommunity.com/sharedfiles/filedetails/?id=" + entry.Id); });
            }

            if (count == 0)
            {
                string message = catalog.Available ? "No Steam Workshop subscriptions found."
                    : "Launch CMS18 through Steam to see your Workshop subscriptions.";
                Label(content, message, 17, Muted, 20, 16, 800, 34);
            }
            SetLength(count);
        }

        private void ClearRows(RectTransform rows)
        {
            for (int i = rows.childCount - 1; i >= 0; i--)
            {
                Transform child = rows.GetChild(i);
                child.gameObject.SetActive(false);
                child.SetParent(root.transform, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private void SetLength(int rows)
        {
            SetScrollLength(scroll, Mathf.Max(365, 12 + rows * 70));
        }

        private static void SetScrollLength(ScrollRect viewport, int length)
        {
            viewport.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, length);
            Canvas.ForceUpdateCanvases();
            viewport.StopMovement();
            viewport.verticalNormalizedPosition = 1;
        }

        private void AddRow(int index, string title, string detail, string state, Action open)
        {
            var row = RoundedBox(content, "Mod", Row);
            Place(row, 8, 8 + index * 70, 836, 64);
            Text heading = Label(row, title, 18, TextColor, 13, 4, 595, 31);
            heading.name = "Title";
            heading.fontStyle = FontStyle.Bold;
            heading.resizeTextForBestFit = true;
            heading.resizeTextMinSize = 13;
            heading.resizeTextMaxSize = 18;
            Label(row, detail, 13, Muted, 13, 34, 595, 23);
            var badge = Label(row, state, 14, TextColor, 610, 11, open == null ? 211 : 132, 43);
            badge.alignment = TextAnchor.MiddleRight;
            if (open != null)
                ActionButton(row, "Open", 752, 14, 70, 36, open);
        }

        private static void OpenSteamPage(string url)
        {
            if (SteamManager.Initialized && Steamworks.SteamUtils.IsOverlayEnabled())
                Steamworks.SteamFriends.ActivateGameOverlayToWebPage(url);
            else
                Application.OpenURL(url);
        }

        private Button ActionButton(Transform parent, string name, float x, float y, float width, float height, Action action)
        {
            RectTransform rect = RoundedBox(parent, name, Color.white);
            Place(rect, x, y, width, height);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = Raised;
            colors.highlightedColor = new Color32(92, 92, 92, 255);
            colors.pressedColor = new Color32(42, 42, 42, 255);
            colors.fadeDuration = 0;
            button.colors = colors;
            Text label = TextElement(rect, name, font, 16, TextAnchor.MiddleCenter, TextColor);
            Fill((RectTransform)label.transform);
            button.onClick.AddListener(delegate { PlayClick(); action(); });
            return button;
        }

        internal static void PlayClick()
        {
            SoundManager sound = SoundManager.Get();
            if (sound != null)
                sound.PlaySFX("ButtonClick");
        }

        private Text Label(Transform parent, string text, int size, Color color, float x, float y, float width, float height)
        {
            Text label = TextElement(parent, text, font, size, TextAnchor.MiddleLeft, color);
            Place((RectTransform)label.transform, x, y, width, height);
            return label;
        }

        internal static Text TextElement(Transform parent, string value, Font font, int size, TextAnchor align, Color color)
        {
            var text = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.alignment = align;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private static RectTransform Box(Transform parent, string name, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            return (RectTransform)image.transform;
        }

        private RectTransform RoundedBox(Transform parent, string name, Color color)
        {
            RectTransform rect = Box(parent, name, color);
            Image image = rect.GetComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            return rect;
        }

        private static Sprite CreateRoundedSprite()
        {
            const int size = 32;
            const float radius = 7;
            const float border = radius + 1;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "CMS18 Mod Menu Corners";
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0);
                    float coverage = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * coverage));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void Dispose()
        {
            catalog.Cancel();
            if (root != null)
                UnityEngine.Object.Destroy(root);
            if (roundedSprite != null)
            {
                UnityEngine.Object.Destroy(roundedSprite.texture);
                UnityEngine.Object.Destroy(roundedSprite);
            }
        }
    }
}
