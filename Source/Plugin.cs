using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CMS18ModMenu
{
    [BepInPlugin(Id, "CMS18 Mod Menu", "1.2.1")]
    [BepInProcess("cms2018.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "local.cms18.modmenu";
        private const BindingFlags MenuFields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo QuitField = typeof(MainMenuManager).GetField("ButtonQuit", MenuFields);
        private static readonly FieldInfo HorizontalField = typeof(MainMenuManager).GetField("horizontalItemsMove", MenuFields);
        private static readonly FieldInfo VerticalField = typeof(MainMenuManager).GetField("verticalItemsMove", MenuFields);

        private MainMenuManager menu;
        private Button exitButton;
        private Button modsButton;
        private MenuPanel panel;
        private float nextSearch;
        private Behaviour horizontal;
        private Behaviour vertical;
        private bool horizontalWasEnabled;
        private bool verticalWasEnabled;
        private GameObject previousSelection;

        private void Awake()
        {
            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            nextSearch = 0;
        }

        private void Update()
        {
            if (panel != null && panel.IsOpen)
            {
                if (exitButton == null || !exitButton.gameObject.activeInHierarchy || Input.GetKeyDown(KeyCode.Escape))
                    ClosePanel();
                return;
            }

            if (modsButton != null || Time.unscaledTime < nextSearch)
                return;

            nextSearch = Time.unscaledTime + 1;
            menu = FindObjectOfType<MainMenuManager>();
            if (menu == null || QuitField == null)
                return;

            exitButton = QuitField.GetValue(menu) as Button;
            if (exitButton == null || !exitButton.gameObject.activeInHierarchy)
                return;

            AddButton();
        }

        private void AddButton()
        {
            var exit = (RectTransform)exitButton.transform;
            Text exitLabel = exitButton.GetComponentInChildren<Text>(true);
            Canvas canvas = exitButton.GetComponentInParent<Canvas>();
            if (exitLabel == null || exitLabel.transform.childCount == 0 || canvas == null)
                return;

            GameObject entry = new GameObject("CMS18 Mods", typeof(RectTransform), typeof(NonDrawingGraphic),
                typeof(Button), typeof(MainMenuDownMenuItem));
            RectTransform rect = (RectTransform)entry.transform;
            rect.SetParent(exit.parent, false);
            int exitIndex = exit.GetSiblingIndex();
            rect.SetSiblingIndex(exitIndex);
            rect.anchorMin = exit.anchorMin;
            rect.anchorMax = exit.anchorMax;
            rect.pivot = exit.pivot;
            rect.sizeDelta = exit.sizeDelta;
            rect.anchoredPosition = exit.anchoredPosition;
            Text label = MenuPanel.TextElement(rect, "Mods", exitLabel.font, exitLabel.fontSize, exitLabel.alignment, exitLabel.color);
            label.fontStyle = exitLabel.fontStyle;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            MenuPanel.Fill((RectTransform)label.transform);
            GameObject underline = Instantiate(exitLabel.transform.GetChild(0).gameObject, label.transform, false);
            var underlineGraphic = underline.GetComponent<Graphic>();
            if (underlineGraphic != null)
                underlineGraphic.raycastTarget = false;
            underline.SetActive(false);

            float width = Mathf.Ceil(label.preferredWidth) + 2;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            if (exit.parent.GetComponent<LayoutGroup>() == null)
            {
                PositionBesideExit(rect, exit, canvas.rootCanvas);
            }
            else
            {
                var layout = entry.AddComponent<LayoutElement>();
                layout.preferredWidth = width;
                layout.preferredHeight = LayoutUtility.GetPreferredHeight(exit);
            }

            var navigation = HorizontalField == null ? null : HorizontalField.GetValue(menu) as HorizontalItemsMove;
            if (navigation != null && navigation.items != null && Array.IndexOf(navigation.items, exitButton.gameObject) == exitIndex)
            {
                navigation.items = navigation.items.Take(exitIndex).Concat(new[] { entry }).Concat(navigation.items.Skip(exitIndex)).ToArray();
                entry.AddComponent<DownMenuButton>();
            }

            modsButton = entry.GetComponent<Button>();
            modsButton.targetGraphic = entry.GetComponent<NonDrawingGraphic>();
            modsButton.onClick.AddListener(OpenPanel);
            if (panel != null)
                panel.Dispose();
            panel = new MenuPanel(menu.gameObject.scene, exitLabel.font, underlineGraphic as Image, ClosePanel, ReadPlugins, Logger);
            Logger.LogInfo("Added Mods to the left of " + exitButton.name + " on " + menu.gameObject.scene.name + ".");
        }

        private static void PositionBesideExit(RectTransform entry, RectTransform exit, Canvas canvas)
        {
            Canvas.ForceUpdateCanvases();
            Rect exitBounds = BoundsInParent(exit, exit.parent);
            Rect canvasBounds = BoundsInParent((RectTransform)canvas.transform, exit.parent);
            const float gap = 12;
            Vector2[] offsets =
            {
                new Vector2(-exitBounds.width - gap, 0),
                new Vector2(exitBounds.width + gap, 0),
                new Vector2(0, -exitBounds.height - gap),
                new Vector2(0, exitBounds.height + gap)
            };

            foreach (Vector2 offset in offsets)
            {
                Rect candidate = exitBounds;
                candidate.position += offset;
                if (!canvasBounds.Contains(candidate.min) || !canvasBounds.Contains(candidate.max))
                    continue;

                bool blocked = false;
                foreach (Transform sibling in exit.parent)
                {
                    var other = sibling as RectTransform;
                    if (other == entry || other == exit || other == null || !other.gameObject.activeSelf)
                        continue;
                    if (other.GetComponentInChildren<Selectable>() != null && candidate.Overlaps(BoundsInParent(other, exit.parent)))
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked)
                {
                    entry.anchoredPosition += offset;
                    return;
                }
            }

            entry.anchoredPosition += offsets[0];
        }

        private static Rect BoundsInParent(RectTransform rect, Transform parent)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = parent.InverseTransformPoint(corners[0]);
            Vector3 max = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static PluginInfo[] ReadPlugins()
        {
            return Chainloader.PluginInfos.Values
                .Where(plugin => plugin.Instance != null)
                .OrderBy(plugin => plugin.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void OpenPanel()
        {
            if (panel == null || panel.IsOpen)
                return;

            EventSystem events = EventSystem.current;
            previousSelection = events == null ? null : events.currentSelectedGameObject;
            horizontal = HorizontalField == null ? null : HorizontalField.GetValue(menu) as Behaviour;
            vertical = VerticalField == null ? null : VerticalField.GetValue(menu) as Behaviour;
            horizontalWasEnabled = Suspend(horizontal);
            verticalWasEnabled = Suspend(vertical);
            MenuPanel.PlayClick();
            panel.Open();
        }

        private static bool Suspend(Behaviour navigation)
        {
            if (navigation == null || !navigation.enabled)
                return false;
            navigation.enabled = false;
            return true;
        }

        private void ClosePanel()
        {
            if (panel == null || !panel.IsOpen)
                return;
            panel.Close();
            if (horizontal != null && horizontalWasEnabled)
                horizontal.enabled = true;
            if (vertical != null && verticalWasEnabled)
                vertical.enabled = true;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            ClosePanel();
            if (panel != null)
                panel.Dispose();
            if (modsButton != null)
                Destroy(modsButton.gameObject);
        }
    }
}
