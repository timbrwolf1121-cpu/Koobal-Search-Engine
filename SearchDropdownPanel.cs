using System;

using System.Collections.Generic;

using KSP.UI.Screens;

using TMPro;

using UnityEngine;

using UnityEngine.EventSystems;

using UnityEngine.UI;



namespace PartSearchSuggest

{

    internal sealed class SearchDropdownPanel : MonoBehaviour

    {

        // Typography tuned for 1080p readability (v0.8.5.1).
        private const float PrimaryFontSize = 14f;
        private const float SubtitleFontSize = 12f;
        private const float HeaderFontSize = 13f;
        private const float HeaderIconFontSize = 10f;

        private const float RowHeight = 34f;
        private const float RowHeightWithSubtitle = 48f;
        private const float CategoryIconSize = 20f;
        private const float CategoryIconPadding = 4f;
        private const float TitleLineHeight = 20f;
        private const float SubtitleLineHeight = 16f;
        private const float HeaderHeight = 20f;
        private const float CloseButtonSize = 14f;
        private const float MaxPanelHeight = 420f;
        private const float PanelPadding = 4f;
        private const float GapBelowSearchField = 2f;

        private static readonly Color PrimaryTextColor = new Color(0.96f, 0.98f, 1f, 1f);
        private static readonly Color SubtitleTextColor = new Color(0.74f, 0.86f, 0.98f, 1f);
        private static readonly Color HeaderTextColor = new Color(0.88f, 0.92f, 0.98f, 1f);
        private static readonly Color HeaderIconTextColor = new Color(0.95f, 0.92f, 0.92f, 1f);
        private static readonly Color PanelBackgroundColor = new Color(0.08f, 0.11f, 0.15f, 1f);

        private static Sprite _whiteSprite;

        private static Sprite _trashcanSprite;



        private RectTransform _rootRect;

        private RectTransform _panelRect;

        private RectTransform _viewportRect;

        private RectTransform _rowsContentRect;

        private LayoutElement _scrollAreaLayout;

        private ScrollRect _scrollRect;

        private RectTransform _searchFieldRect;

        private Canvas _referenceCanvas;

        private TextMeshProUGUI _headerLabel;

        private GameObject _headerRow;

        private GameObject _brandingFooter;

        private LayoutElement _brandingFooterLayout;

        private GameObject _brandingContentHost;

        private GameObject _clearHistoryButton;

        private readonly List<GameObject> _rowObjects = new List<GameObject>();

        private bool _dropdownOpen;

        private bool _reserveCategoryIconRail;

        private float CategoryIconRailWidth => CategoryIconSize + CategoryIconPadding * 2f;



        public event Action<PartSuggestion> OnSuggestionChosen;

        public event Action OnDismissed;

        public event Action OnClearHistoryRequested;



        public bool IsVisible =>

            _rootRect != null && _rootRect.gameObject.activeSelf;



        public bool IsDropdownOpen => _dropdownOpen;



        public static SearchDropdownPanel Create(RectTransform searchFieldRect)

        {

            RectTransform overlayParent = FindOverlayParent(searchFieldRect);

            Canvas referenceCanvas = searchFieldRect.GetComponentInParent<Canvas>();



            GameObject root = new GameObject("KoobalSearchEngine_Overlay", typeof(RectTransform));

            root.transform.SetParent(overlayParent, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();

            StretchFull(rootRect);



            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();

            canvasGroup.blocksRaycasts = true;

            canvasGroup.interactable = true;

            canvasGroup.alpha = 1f;



            SearchDropdownPanel panel = root.AddComponent<SearchDropdownPanel>();

            panel.Build(searchFieldRect, rootRect, referenceCanvas);

            panel.Hide();

            return panel;

        }



        private void Build(RectTransform searchFieldRect, RectTransform rootRect, Canvas referenceCanvas)

        {

            _rootRect = rootRect;

            _searchFieldRect = searchFieldRect;

            _referenceCanvas = referenceCanvas;



            CreateDimBlocker();

            CreateDropdownPanel();

        }



        private void CreateDimBlocker()

        {

            GameObject blocker = new GameObject("DimBlocker", typeof(RectTransform));

            blocker.transform.SetParent(_rootRect, false);

            blocker.transform.SetSiblingIndex(0);

            StretchFull(blocker.GetComponent<RectTransform>());



            Image blockerImage = blocker.AddComponent<Image>();

            blockerImage.sprite = GetWhiteSprite();

            blockerImage.color = new Color(0f, 0f, 0f, 0f);

            blockerImage.raycastTarget = true;



            Button blockerButton = blocker.AddComponent<Button>();

            blockerButton.transition = Selectable.Transition.None;

            blockerButton.navigation = new Navigation { mode = Navigation.Mode.None };

            blockerButton.onClick.AddListener(RequestDismiss);

        }



        private void CreateDropdownPanel()

        {

            GameObject panelObject = new GameObject("DropdownPanel", typeof(RectTransform));

            panelObject.transform.SetParent(_rootRect, false);

            _panelRect = panelObject.GetComponent<RectTransform>();



            Image background = panelObject.AddComponent<Image>();

            background.sprite = GetWhiteSprite();

            background.color = PanelBackgroundColor;

            background.raycastTarget = true;



            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();

            layout.childAlignment = TextAnchor.UpperCenter;

            layout.childControlHeight = true;

            layout.childControlWidth = true;

            layout.childForceExpandHeight = false;

            layout.childForceExpandWidth = true;

            layout.spacing = 0f;

            layout.padding = new RectOffset(3, 3, 2, 2);



            CreateHeader(panelObject.transform);

            CreateScrollArea(panelObject.transform);

            CreateBrandingFooter(panelObject.transform);

            AttachScrollEvents(panelObject);



            PositionPanelBelowSearchField();

        }



        /// <summary>
        /// KSP stock pattern (CraftEntry / DirectoryController): EventTrigger Scroll on
        /// raycast targets forwards wheel input to ScrollRect.OnScroll because the editor
        /// does not reliably deliver IScrollHandler events while the search field is focused.
        /// </summary>
        private void AttachScrollEvents(GameObject panelObject)

        {

            AttachScrollEvent(panelObject);

            if (_viewportRect != null)

            {

                AttachScrollEvent(_viewportRect.gameObject);

            }



            Transform header = panelObject.transform.Find("Header");

            if (header != null)

            {

                AttachScrollEvent(header.gameObject);

            }

        }



        private void AttachScrollEvent(GameObject target)

        {

            if (target == null)

            {

                return;

            }



            EventTrigger trigger = target.GetComponent<EventTrigger>();

            if (trigger == null)

            {

                trigger = target.AddComponent<EventTrigger>();

            }



            for (int i = 0; i < trigger.triggers.Count; i++)

            {

                if (trigger.triggers[i].eventID == EventTriggerType.Scroll)

                {

                    return;

                }

            }



            EventTrigger.Entry scrollEntry = new EventTrigger.Entry

            {

                eventID = EventTriggerType.Scroll

            };

            scrollEntry.callback.AddListener(OnScrollEventTriggered);

            trigger.triggers.Add(scrollEntry);

        }



        private void OnScrollEventTriggered(BaseEventData baseEventData)

        {

            PointerEventData pointerData = baseEventData as PointerEventData;

            if (pointerData == null)

            {

                return;

            }



            if (ForwardScroll(pointerData))

            {

                pointerData.Use();

            }

        }



        private void CreateBrandingFooter(Transform parent)
        {
            GameObject footer = new GameObject("BrandingFooter", typeof(RectTransform));
            footer.transform.SetParent(parent, false);
            _brandingFooter = footer;

            _brandingFooterLayout = footer.AddComponent<LayoutElement>();
            _brandingFooterLayout.flexibleHeight = 0f;
            _brandingFooterLayout.flexibleWidth = 1f;

            Image footerBackground = footer.AddComponent<Image>();
            footerBackground.sprite = GetWhiteSprite();
            footerBackground.color = new Color(1f, 1f, 1f, 0f);
            footerBackground.raycastTarget = false;

            // Footer VLG keeps the wordmark host full-width and vertically centered
            // even when the panel width changes (category icon rail) or children rebuild.
            VerticalLayoutGroup footerLayout = footer.AddComponent<VerticalLayoutGroup>();
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = true;
            footerLayout.childForceExpandHeight = true;
            footerLayout.spacing = 0f;
            footerLayout.padding = new RectOffset(4, 4, 2, 2);

            GameObject contentHost = new GameObject("BrandingContent", typeof(RectTransform));
            contentHost.transform.SetParent(footer.transform, false);
            _brandingContentHost = contentHost;

            LayoutElement hostLayout = contentHost.AddComponent<LayoutElement>();
            hostLayout.flexibleWidth = 1f;
            hostLayout.flexibleHeight = 1f;
            hostLayout.minHeight = 0f;

            RectTransform hostRect = contentHost.GetComponent<RectTransform>();
            hostRect.anchorMin = new Vector2(0f, 0f);
            hostRect.anchorMax = new Vector2(1f, 1f);
            hostRect.pivot = new Vector2(0.5f, 0.5f);
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = contentHost.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 0f;

            UpdateBrandingFooterVisuals();
            footer.SetActive(false);
        }

        private void UpdateBrandingFooterVisuals()
        {
            if (_brandingFooterLayout == null || _brandingContentHost == null)
            {
                return;
            }

            float footerHeight = BrandingSettings.FooterHeight;
            _brandingFooterLayout.minHeight = footerHeight;
            _brandingFooterLayout.preferredHeight = footerHeight;

            // DestroyImmediate so deferred Destroy leftovers cannot leave a left-anchored
            // sibling in the layout pass that runs later in Show().
            for (int i = _brandingContentHost.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_brandingContentHost.transform.GetChild(i).gameObject);
            }

            TMP_FontAsset font = _headerLabel != null ? _headerLabel.font : null;
            if (BrandingSettings.DropdownBranding == DropdownBrandingVariant.FullTagline)
            {
                KoobalWordmarkBuilder.BuildFullTagline(_brandingContentHost.transform, font);
            }
            else
            {
                KoobalWordmarkBuilder.BuildWordmarkOnly(_brandingContentHost.transform, font);
            }

            RectTransform hostRect = _brandingContentHost.transform as RectTransform;
            if (hostRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(hostRect);
            }
        }

        private void RebuildBrandingFooterLayout()
        {
            if (_brandingFooter == null || !_brandingFooter.activeSelf)
            {
                return;
            }

            RectTransform footerRect = _brandingFooter.transform as RectTransform;
            if (footerRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(footerRect);
            }

            RectTransform hostRect = _brandingContentHost != null
                ? _brandingContentHost.transform as RectTransform
                : null;
            if (hostRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(hostRect);
            }
        }

        private void CreateHeader(Transform parent)

        {

            GameObject header = new GameObject("Header", typeof(RectTransform));

            header.transform.SetParent(parent, false);

            _headerRow = header;



            LayoutElement headerLayout = header.AddComponent<LayoutElement>();

            headerLayout.minHeight = HeaderHeight;

            headerLayout.preferredHeight = HeaderHeight;



            Image headerBackground = header.AddComponent<Image>();

            headerBackground.sprite = GetWhiteSprite();

            headerBackground.color = new Color(0.1f, 0.13f, 0.18f, 1f);

            headerBackground.raycastTarget = true;

            header.AddComponent<RectMask2D>();



            HorizontalLayoutGroup headerGroup = header.AddComponent<HorizontalLayoutGroup>();

            headerGroup.childAlignment = TextAnchor.MiddleCenter;

            headerGroup.spacing = 1f;

            headerGroup.padding = new RectOffset(3, 1, 1, 1);

            headerGroup.childControlWidth = true;

            headerGroup.childControlHeight = true;

            headerGroup.childForceExpandWidth = false;

            headerGroup.childForceExpandHeight = false;



            _clearHistoryButton = CreateHeaderIconButton(

                header.transform,

                "ClearHistoryButton",

                null,

                new Color(0.18f, 0.22f, 0.28f, 1f),

                new Color(0.35f, 0.28f, 0.18f, 1f),

                new Color(0.28f, 0.22f, 0.15f, 1f),

                RequestClearHistory,

                GetTrashcanSprite());

            _clearHistoryButton.SetActive(false);

            UiTooltipHelper.AttachTextTooltip(_clearHistoryButton, "Clear history");



            GameObject labelObject = new GameObject("HeaderLabel", typeof(RectTransform));

            labelObject.transform.SetParent(header.transform, false);



            LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();

            labelLayout.flexibleWidth = 1f;

            labelLayout.minHeight = 0f;

            labelLayout.preferredHeight = HeaderHeight;



            _headerLabel = labelObject.AddComponent<TextMeshProUGUI>();

            _headerLabel.fontSize = HeaderFontSize;

            _headerLabel.lineSpacing = -2f;

            _headerLabel.margin = Vector4.zero;

            _headerLabel.color = HeaderTextColor;

            _headerLabel.text = "Suggestions";

            _headerLabel.alignment = TextAlignmentOptions.Center;

            _headerLabel.enableWordWrapping = false;

            _headerLabel.overflowMode = TextOverflowModes.Overflow;

            _headerLabel.raycastTarget = false;



            CreateHeaderIconButton(

                header.transform,

                "CloseButton",

                "X",

                new Color(0.18f, 0.22f, 0.28f, 1f),

                new Color(0.35f, 0.2f, 0.2f, 1f),

                new Color(0.28f, 0.15f, 0.15f, 1f),

                RequestDismiss);

        }



        private GameObject CreateHeaderIconButton(

            Transform parent,

            string objectName,

            string labelText,

            Color normalColor,

            Color highlightedColor,

            Color pressedColor,

            UnityEngine.Events.UnityAction onClick,

            Sprite iconSprite = null)

        {

            GameObject buttonObject = new GameObject(objectName, typeof(RectTransform));

            buttonObject.transform.SetParent(parent, false);



            LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();

            buttonLayout.minWidth = CloseButtonSize;

            buttonLayout.preferredWidth = CloseButtonSize;

            buttonLayout.minHeight = CloseButtonSize;

            buttonLayout.preferredHeight = CloseButtonSize;



            Image buttonImage = buttonObject.AddComponent<Image>();

            buttonImage.sprite = GetWhiteSprite();

            buttonImage.color = normalColor;

            buttonImage.raycastTarget = true;



            Button button = buttonObject.AddComponent<Button>();

            ColorBlock colors = button.colors;

            colors.normalColor = normalColor;

            colors.highlightedColor = highlightedColor;

            colors.pressedColor = pressedColor;

            colors.selectedColor = highlightedColor;

            button.colors = colors;

            button.navigation = new Navigation { mode = Navigation.Mode.None };

            button.onClick.AddListener(onClick);



            if (iconSprite != null)

            {

                GameObject iconObject = new GameObject("Icon", typeof(RectTransform));

                iconObject.transform.SetParent(buttonObject.transform, false);



                RectTransform iconRect = iconObject.GetComponent<RectTransform>();

                iconRect.anchorMin = new Vector2(0.5f, 0.5f);

                iconRect.anchorMax = new Vector2(0.5f, 0.5f);

                iconRect.pivot = new Vector2(0.5f, 0.5f);

                iconRect.sizeDelta = new Vector2(10f, 10f);

                iconRect.anchoredPosition = Vector2.zero;



                Image iconImage = iconObject.AddComponent<Image>();

                iconImage.sprite = iconSprite;

                iconImage.color = HeaderIconTextColor;

                iconImage.raycastTarget = false;

                iconImage.preserveAspect = true;

            }

            else

            {

                GameObject labelObject = new GameObject("Label", typeof(RectTransform));

                labelObject.transform.SetParent(buttonObject.transform, false);

                StretchFull(labelObject.GetComponent<RectTransform>());



                TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();

                label.text = labelText;

                label.fontSize = HeaderIconFontSize;

                label.lineSpacing = -2f;

                label.margin = Vector4.zero;

                label.fontStyle = FontStyles.Bold;

                label.alignment = TextAlignmentOptions.Center;

                label.color = HeaderIconTextColor;

                label.raycastTarget = false;

            }



            return buttonObject;

        }



        private void CreateScrollArea(Transform parent)

        {

            // Height cap lives on a wrapper with LayoutElement only. ScrollRect also implements
            // ILayoutElement and reports full content height; on the same GameObject that wins
            // over our cap and the panel grows to fit every row (no internal scroll).
            GameObject scrollWrapper = new GameObject("ScrollAreaWrapper", typeof(RectTransform));

            scrollWrapper.transform.SetParent(parent, false);



            _scrollAreaLayout = scrollWrapper.AddComponent<LayoutElement>();

            _scrollAreaLayout.flexibleHeight = 0f;

            _scrollAreaLayout.minHeight = 0f;



            GameObject scrollArea = new GameObject("ScrollArea", typeof(RectTransform));

            scrollArea.transform.SetParent(scrollWrapper.transform, false);

            StretchFull(scrollArea.GetComponent<RectTransform>());



            Image scrollAreaBackground = scrollArea.AddComponent<Image>();

            scrollAreaBackground.sprite = GetWhiteSprite();

            scrollAreaBackground.color = PanelBackgroundColor;

            scrollAreaBackground.raycastTarget = false;



            GameObject viewport = new GameObject("Viewport", typeof(RectTransform));

            viewport.transform.SetParent(scrollArea.transform, false);

            _viewportRect = viewport.GetComponent<RectTransform>();

            StretchFull(_viewportRect);



            Image viewportImage = viewport.AddComponent<Image>();

            viewportImage.sprite = GetWhiteSprite();

            viewportImage.color = PanelBackgroundColor;

            viewportImage.raycastTarget = true;

            viewport.AddComponent<RectMask2D>();



            GameObject content = new GameObject("Content", typeof(RectTransform));

            content.transform.SetParent(viewport.transform, false);

            _rowsContentRect = content.GetComponent<RectTransform>();

            _rowsContentRect.anchorMin = new Vector2(0f, 1f);

            _rowsContentRect.anchorMax = new Vector2(1f, 1f);

            _rowsContentRect.pivot = new Vector2(0.5f, 1f);

            _rowsContentRect.anchoredPosition = Vector2.zero;

            _rowsContentRect.sizeDelta = new Vector2(0f, 0f);



            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();

            contentLayout.childAlignment = TextAnchor.UpperCenter;

            contentLayout.childControlHeight = true;

            contentLayout.childControlWidth = true;

            contentLayout.childForceExpandHeight = false;

            contentLayout.childForceExpandWidth = true;

            contentLayout.spacing = 0f;



            ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();

            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;



            _scrollRect = scrollArea.AddComponent<ScrollRect>();

            _scrollRect.viewport = _viewportRect;

            _scrollRect.content = _rowsContentRect;

            _scrollRect.horizontal = false;

            _scrollRect.vertical = true;

            _scrollRect.movementType = ScrollRect.MovementType.Clamped;

            _scrollRect.scrollSensitivity = 40f;

            _scrollRect.inertia = true;

        }



        private void Update()

        {

            if (!IsVisible || _scrollRect == null || !_scrollRect.enabled || !_scrollRect.vertical)

            {

                return;

            }



            float scrollDelta = Input.mouseScrollDelta.y;

            if (Mathf.Abs(scrollDelta) < 0.001f)

            {

                return;

            }



            if (!IsPointerOverScrollTarget())

            {

                return;

            }



            ForwardScrollFromMouseWheel(scrollDelta);

        }



        private void ForwardScrollFromMouseWheel(float scrollDelta)

        {

            PointerEventData eventData = new PointerEventData(EventSystem.current)

            {

                scrollDelta = new Vector2(0f, scrollDelta)

            };

            ForwardScroll(eventData);

        }



        private bool ForwardScroll(PointerEventData eventData)

        {

            if (_scrollRect == null || eventData == null || !_scrollRect.enabled || !_scrollRect.vertical)

            {

                return false;

            }



            if (!CanScrollContent())

            {

                return false;

            }



            _scrollRect.OnScroll(eventData);

            return true;

        }



        private bool CanScrollContent()

        {

            if (_rowsContentRect == null || _viewportRect == null)

            {

                return false;

            }



            return _rowsContentRect.rect.height > _viewportRect.rect.height + 0.5f;

        }



        private bool IsPointerOverScrollTarget()

        {

            return IsPointerOverRect(_panelRect);

        }



        private bool IsPointerOverRect(RectTransform rect)

        {

            if (rect == null)

            {

                return false;

            }



            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, GetEventCamera());

        }




        private static RectTransform FindOverlayParent(RectTransform searchFieldRect)

        {

            RectTransform best = searchFieldRect.parent as RectTransform;

            RectTransform current = best;

            while (current != null)

            {

                if (current.rect.height >= 150f)

                {

                    best = current;

                }



                current = current.parent as RectTransform;

            }



            return best ?? searchFieldRect;

        }



        private static void StretchFull(RectTransform rect)

        {

            rect.anchorMin = Vector2.zero;

            rect.anchorMax = Vector2.one;

            rect.offsetMin = Vector2.zero;

            rect.offsetMax = Vector2.zero;

            rect.pivot = new Vector2(0.5f, 0.5f);

        }



        private static Sprite GetWhiteSprite()

        {

            if (_whiteSprite != null)

            {

                return _whiteSprite;

            }



            Texture2D texture = Texture2D.whiteTexture;

            _whiteSprite = Sprite.Create(

                texture,

                new Rect(0f, 0f, texture.width, texture.height),

                new Vector2(0.5f, 0.5f));

            return _whiteSprite;

        }



        /// <summary>

        /// Programmatic 16x16 trashcan. Stock LiberationSans lacks U+1F5D1,

        /// and Squad GameData has no suitable delete/trash UI sprite at this size.

        /// </summary>

        private static Sprite GetTrashcanSprite()

        {

            if (_trashcanSprite != null)

            {

                return _trashcanSprite;

            }



            const int size = 16;

            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);

            texture.filterMode = FilterMode.Point;

            texture.wrapMode = TextureWrapMode.Clamp;

            texture.hideFlags = HideFlags.HideAndDontSave;



            Color clear = new Color(0f, 0f, 0f, 0f);

            Color solid = Color.white;

            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)

            {

                pixels[i] = clear;

            }



            // Pixel art (y=0 at bottom). Readable at ~10px on dark header buttons.

            System.Action<int, int> plot = (x, y) =>

            {

                if (x >= 0 && x < size && y >= 0 && y < size)

                {

                    pixels[y * size + x] = solid;

                }

            };



            System.Action<int, int, int> hLine = (x0, x1, y) =>

            {

                for (int x = x0; x <= x1; x++)

                {

                    plot(x, y);

                }

            };



            // Lid handle

            hLine(6, 9, 14);

            hLine(6, 9, 13);

            // Lid

            hLine(3, 12, 12);

            hLine(2, 13, 11);

            // Can body outline + vertical ribs

            for (int y = 3; y <= 10; y++)

            {

                plot(2, y);

                plot(13, y);

                plot(5, y);

                plot(8, y);

                plot(11, y);

            }



            hLine(2, 13, 10);

            hLine(3, 12, 3);

            hLine(4, 11, 2);



            texture.SetPixels(pixels);

            texture.Apply(false, true);



            _trashcanSprite = Sprite.Create(

                texture,

                new Rect(0f, 0f, size, size),

                new Vector2(0.5f, 0.5f),

                100f);

            _trashcanSprite.name = "KoobalTrashcan";

            return _trashcanSprite;

        }



        private void PositionPanelBelowSearchField()

        {

            if (_panelRect == null || _searchFieldRect == null || _rootRect == null)

            {

                return;

            }



            RectTransform searchParent = _searchFieldRect.parent as RectTransform;

            float railWidth = _reserveCategoryIconRail ? CategoryIconRailWidth : 0f;

            if (searchParent != null && searchParent == _rootRect)

            {

                float textWidth = Mathf.Max(_searchFieldRect.rect.width, 120f);

                float height = _panelRect.sizeDelta.y > 0f ? _panelRect.sizeDelta.y : MaxPanelHeight;

                _panelRect.anchorMin = _searchFieldRect.anchorMin;

                _panelRect.anchorMax = _searchFieldRect.anchorMax;

                if (railWidth > 0f)

                {

                    _panelRect.pivot = new Vector2(0f, 1f);

                    _panelRect.sizeDelta = new Vector2(textWidth + railWidth, height);

                    _panelRect.anchoredPosition = _searchFieldRect.anchoredPosition

                        + new Vector2(-textWidth * 0.5f, -_searchFieldRect.rect.height - GapBelowSearchField);

                }

                else

                {

                    _panelRect.pivot = new Vector2(0.5f, 1f);

                    _panelRect.sizeDelta = new Vector2(textWidth, height);

                    _panelRect.anchoredPosition = _searchFieldRect.anchoredPosition

                        + new Vector2(0f, -_searchFieldRect.rect.height - GapBelowSearchField);

                }

                return;

            }



            Vector3[] corners = new Vector3[4];

            _searchFieldRect.GetWorldCorners(corners);



            Camera eventCamera = GetEventCamera();



            Vector2 localBottomLeft;

            Vector2 localTopRight;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(

                _rootRect,

                RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]),

                eventCamera,

                out localBottomLeft);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(

                _rootRect,

                RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]),

                eventCamera,

                out localTopRight);



            float textWidthWorld = Mathf.Max(localTopRight.x - localBottomLeft.x, 120f);

            float panelTopY = localBottomLeft.y - GapBelowSearchField;

            float heightWorld = _panelRect.sizeDelta.y > 0f ? _panelRect.sizeDelta.y : MaxPanelHeight;



            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);

            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);

            if (railWidth > 0f)

            {

                _panelRect.pivot = new Vector2(0f, 1f);

                _panelRect.sizeDelta = new Vector2(textWidthWorld + railWidth, heightWorld);

                _panelRect.anchoredPosition = new Vector2(localBottomLeft.x, panelTopY);

            }

            else

            {

                float centerX = (localBottomLeft.x + localTopRight.x) * 0.5f;

                _panelRect.pivot = new Vector2(0.5f, 1f);

                _panelRect.sizeDelta = new Vector2(textWidthWorld, heightWorld);

                _panelRect.anchoredPosition = new Vector2(centerX, panelTopY);

            }

        }



        public void Show(
            IReadOnlyList<PartSuggestion> suggestions,
            bool historyMode,
            bool showClearHistory,
            string headerOverride = null,
            bool showBrandingFooter = false)

        {
            int suggestionCount = suggestions?.Count ?? 0;
            bool brandingOnly = showBrandingFooter && suggestionCount == 0;

            if (suggestionCount == 0 && !showBrandingFooter)
            {
                EditorBootstrap.Log("Show: suggestion count is 0 — not activating dropdown.");
                Hide();
                return;
            }

            _dropdownOpen = true;

            PartsPanelCollapseHelper.NotifyDropdownOpen(true);
            PartsPanelCollapseHelper.Collapse();



            ClearRows();

            _reserveCategoryIconRail = false;
            for (int i = 0; i < suggestionCount; i++)
            {
                if (StockCategorizerIconHelper.SupportsCategoryIconRail(suggestions[i]))
                {
                    _reserveCategoryIconRail = true;
                    break;
                }
            }

            if (_reserveCategoryIconRail)
            {
                StockCategorizerIconHelper.EnsureCacheWarmed(suggestions);
            }

            PositionPanelBelowSearchField();

            if (_brandingFooter != null)
            {
                if (showBrandingFooter)
                {
                    UpdateBrandingFooterVisuals();
                }

                _brandingFooter.SetActive(showBrandingFooter);
            }

            if (_headerRow != null)
            {
                _headerRow.SetActive(!brandingOnly);
            }

            UpdateClearHistoryVisibility(showClearHistory && !brandingOnly, historyMode, headerOverride);

            float totalRowHeight = 0f;

            for (int i = 0; i < suggestionCount; i++)
            {
                totalRowHeight += CreateRow(suggestions[i]);
            }

            float headerBlockHeight = brandingOnly ? 0f : HeaderHeight;
            float footerBlockHeight = showBrandingFooter ? BrandingSettings.FooterHeight : 0f;
            float contentHeight = totalRowHeight;
            float viewportHeight = Mathf.Max(
                0f,
                MaxPanelHeight - headerBlockHeight - footerBlockHeight - PanelPadding);
            float visibleContentHeight = brandingOnly
                ? 0f
                : Mathf.Min(contentHeight, viewportHeight);
            float panelHeight = headerBlockHeight
                + footerBlockHeight
                + PanelPadding
                + visibleContentHeight;
            if (brandingOnly)
            {
                panelHeight = footerBlockHeight + PanelPadding;
            }



            _panelRect.sizeDelta = new Vector2(_panelRect.sizeDelta.x, panelHeight);



            if (_scrollAreaLayout != null)
            {
                _scrollAreaLayout.gameObject.SetActive(!brandingOnly);
                _scrollAreaLayout.minHeight = visibleContentHeight;
                _scrollAreaLayout.preferredHeight = visibleContentHeight;
                _scrollAreaLayout.flexibleHeight = 0f;
            }



            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);



            if (_rowsContentRect != null)

            {

                LayoutRebuilder.ForceRebuildLayoutImmediate(_rowsContentRect);

            }

            if (showBrandingFooter)
            {
                RebuildBrandingFooterLayout();
            }



            Canvas.ForceUpdateCanvases();



            if (_scrollRect != null)

            {

                _scrollRect.enabled = true;

                _scrollRect.verticalNormalizedPosition = 1f;

            }



            PositionPanelBelowSearchField();

            // Width may change with category rail / search-field measure — re-center footer.
            if (showBrandingFooter)
            {
                RebuildBrandingFooterLayout();
            }



            _rootRect.gameObject.SetActive(true);

            _rootRect.SetAsLastSibling();

            _panelRect.transform.SetAsLastSibling();

            if (showBrandingFooter)
            {
                RebuildBrandingFooterLayout();
            }



            LogShowState(
                suggestionCount,
                historyMode,
                showClearHistory,
                contentHeight,
                viewportHeight,
                showBrandingFooter);

        }



        private void UpdateClearHistoryVisibility(bool showClearHistory, bool historyMode, string headerOverride = null)

        {

            if (_headerLabel != null)

            {

                if (!string.IsNullOrEmpty(headerOverride))

                {

                    _headerLabel.text = headerOverride;

                }

                else

                {

                    _headerLabel.text = historyMode ? "Recent searches" : "Suggestions";

                }

            }



            if (_clearHistoryButton != null)

            {

                _clearHistoryButton.SetActive(showClearHistory);

            }

        }



        private float GetRectHeight(RectTransform rect)

        {

            return rect != null ? rect.rect.height : 0f;

        }



        private void LogShowState(
            int suggestionCount,
            bool historyMode,
            bool showClearHistory,
            float contentHeight,
            float viewportHeight,
            bool showBrandingFooter)

        {

            Camera eventCamera = GetEventCamera();

            Vector3[] panelCorners = new Vector3[4];

            Vector3[] searchCorners = new Vector3[4];

            _panelRect.GetWorldCorners(panelCorners);

            _searchFieldRect.GetWorldCorners(searchCorners);



            float searchBottomY = searchCorners[0].y;

            float panelTopY = panelCorners[1].y;

            bool overlapsSearch = panelTopY > searchBottomY + 0.5f;



            float actualContentHeight = GetRectHeight(_rowsContentRect);

            float actualViewportHeight = GetRectHeight(_viewportRect);

            float actualScrollAreaHeight = _scrollAreaLayout != null ? GetRectHeight(_scrollAreaLayout.transform as RectTransform) : 0f;

            float actualPanelHeight = GetRectHeight(_panelRect);

            bool scrollable = actualContentHeight > actualViewportHeight + 0.5f;



            Vector2 screenPanelTop = RectTransformUtility.WorldToScreenPoint(eventCamera, panelCorners[1]);

            Vector2 screenPanelBottom = RectTransformUtility.WorldToScreenPoint(eventCamera, panelCorners[0]);



            EditorBootstrap.Log(

                "Show dropdown: count=" + suggestionCount

                + ", history=" + historyMode

                + ", clearHistory=" + showClearHistory

                + ", brandingFooter=" + showBrandingFooter

                + ", panelSize=" + _panelRect.sizeDelta

                + ", panelHeight=" + actualPanelHeight.ToString("F1")

                + ", scrollAreaHeight=" + actualScrollAreaHeight.ToString("F1")

                + ", contentHeight=" + contentHeight.ToString("F1")

                + ", actualContentHeight=" + actualContentHeight.ToString("F1")

                + ", viewportHeight=" + viewportHeight.ToString("F1")

                + ", actualViewportHeight=" + actualViewportHeight.ToString("F1")

                + ", scrollEnabled=" + (_scrollRect != null && _scrollRect.enabled)

                + ", scrollable=" + scrollable

                + ", panelPos=" + _panelRect.anchoredPosition

                + ", overlayParent=" + GetTransformPath(_rootRect != null ? _rootRect.parent as RectTransform : null)

                + ", overlayActive=" + _rootRect.gameObject.activeSelf

                + ", panelActive=" + _panelRect.gameObject.activeSelf

                + ", renderMode=" + (_referenceCanvas != null ? _referenceCanvas.renderMode.ToString() : "unknown")

                + ", worldCamera=" + (eventCamera != null ? eventCamera.name : "null")

                + ", overlaySibling=" + _rootRect.GetSiblingIndex()

                + ", partsPanelCollapsed=" + PartsPanelCollapseHelper.IsCollapsed

                + ", collapseApproach=" + PartsPanelCollapseHelper.Approach

                + ", searchBottomY=" + searchBottomY

                + ", panelTopY=" + panelTopY

                + ", panelScreenTop=" + screenPanelTop

                + ", panelScreenBottom=" + screenPanelBottom

                + ", overlapsSearch=" + overlapsSearch

                + ", panelWorldBL=" + panelCorners[0]

                + ", panelWorldTR=" + panelCorners[2]);

        }



        private static string GetTransformPath(RectTransform rect)

        {

            return rect != null ? rect.name : "null";

        }



        private Camera GetEventCamera()

        {

            if (_referenceCanvas == null || _referenceCanvas.renderMode == RenderMode.ScreenSpaceOverlay)

            {

                return null;

            }



            return _referenceCanvas.worldCamera;

        }



        public void HideWithoutHoldNotify()
        {
            _dropdownOpen = false;

            if (_rootRect != null)
            {
                _rootRect.gameObject.SetActive(false);
            }
        }

        public void Hide()

        {

            if (!_dropdownOpen && (_rootRect == null || !_rootRect.gameObject.activeSelf))

            {

                EditorBootstrap.Log("Hide dropdown: already hidden — skip Restore.");

                return;

            }



            bool wasOpen = _dropdownOpen;

            _dropdownOpen = false;

            PartsPanelCollapseHelper.NotifyDropdownOpen(false);



            if (wasOpen)

            {

                EditorBootstrap.Log(

                    "Hide dropdown: closed, collapsed="

                    + PartsPanelCollapseHelper.IsCollapsed

                    + ", approach="

                    + PartsPanelCollapseHelper.Approach

                    + ".");

                PartsPanelCollapseHelper.RestoreIfDropdownClosed(true);
            }

            else

            {

                EditorBootstrap.Log("Hide dropdown: overlay active without open flag — deactivating only.");

            }



            if (_rootRect != null)

            {

                _rootRect.gameObject.SetActive(false);

            }

        }



        private void RequestDismiss()

        {

            Hide();

            OnDismissed?.Invoke();

        }



        private void RequestClearHistory()

        {

            EditorBootstrap.Log("Clear history button clicked.");

            OnClearHistoryRequested?.Invoke();

        }



        private float CreateRow(PartSuggestion suggestion)

        {

            bool hasSubtitle = !suggestion.IsHistory && !string.IsNullOrEmpty(suggestion.MatchReason);

            bool isIconEligible = StockCategorizerIconHelper.SupportsCategoryIconRail(suggestion);

            float textInsetRight = _reserveCategoryIconRail ? CategoryIconRailWidth : 0f;

            float rowHeight = hasSubtitle ? RowHeightWithSubtitle : RowHeight;



            GameObject row = new GameObject("Row_" + _rowObjects.Count, typeof(RectTransform));

            row.transform.SetParent(_rowsContentRect, false);



            LayoutElement rowLayout = row.AddComponent<LayoutElement>();

            rowLayout.minHeight = rowHeight;

            rowLayout.preferredHeight = rowHeight;

            rowLayout.flexibleHeight = 0f;



            Image rowImage = row.AddComponent<Image>();

            rowImage.sprite = GetWhiteSprite();

            rowImage.color = suggestion.IsFirstClass

                ? new Color(0.12f, 0.22f, 0.30f, 1f)

                : new Color(0.14f, 0.18f, 0.24f, 1f);

            rowImage.raycastTarget = true;



            Button button = row.AddComponent<Button>();

            ColorBlock colors = button.colors;

            colors.normalColor = rowImage.color;

            colors.highlightedColor = suggestion.IsFirstClass

                ? new Color(0.20f, 0.36f, 0.50f, 1f)

                : new Color(0.22f, 0.34f, 0.48f, 1f);

            colors.pressedColor = suggestion.IsFirstClass

                ? new Color(0.16f, 0.30f, 0.42f, 1f)

                : new Color(0.18f, 0.28f, 0.4f, 1f);

            colors.selectedColor = colors.highlightedColor;

            button.colors = colors;

            button.navigation = new Navigation { mode = Navigation.Mode.None };

            string title = suggestion.DisplayText ?? suggestion.QueryText ?? string.Empty;

            CreateRowTextContent(row.transform, title, hasSubtitle, suggestion.MatchReason, textInsetRight);

            bool hasCategoryIcon = false;
            if (isIconEligible && _reserveCategoryIconRail)
            {
                hasCategoryIcon = TryCreateCategoryIconRight(row.transform, suggestion, false);
            }

            if (!hasCategoryIcon)
            {
                row.AddComponent<RectMask2D>();
            }



            PartSuggestion captured = suggestion;

            AddRowPointerDownHandler(row, captured);

            AttachScrollEvent(row);



            _rowObjects.Add(row);

            return rowHeight;

        }



        private static void ConfigureSuggestionLabel(

            TextMeshProUGUI label,

            float fontSize,

            Color color,

            TextAlignmentOptions alignment)

        {

            label.fontSize = fontSize;

            label.lineSpacing = -2f;

            label.margin = Vector4.zero;

            label.color = color;

            label.alignment = alignment;

            label.enableWordWrapping = false;

            label.enableAutoSizing = false;

            label.overflowMode = TextOverflowModes.Ellipsis;

            label.raycastTarget = false;

        }



        private static TextMeshProUGUI CreateRowLabel(

            Transform parent,

            string objectName,

            float height,

            float fontSize,

            Color color,

            string text,

            TextAlignmentOptions alignment = TextAlignmentOptions.Center)

        {

            GameObject labelObject = new GameObject(objectName, typeof(RectTransform));

            labelObject.transform.SetParent(parent, false);



            LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();

            labelLayout.minHeight = height;

            labelLayout.preferredHeight = height;

            labelLayout.flexibleHeight = 0f;



            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();

            ConfigureSuggestionLabel(label, fontSize, color, alignment);

            label.text = text ?? string.Empty;

            return label;

        }



        private static void CreateSingleLineRowContent(Transform rowTransform, string title)

        {

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));

            labelObject.transform.SetParent(rowTransform, false);

            StretchFull(labelObject.GetComponent<RectTransform>());



            LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();

            labelLayout.minHeight = RowHeight;

            labelLayout.preferredHeight = RowHeight;

            labelLayout.flexibleHeight = 0f;



            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();

            ConfigureSuggestionLabel(label, PrimaryFontSize, PrimaryTextColor, TextAlignmentOptions.Center);

            label.text = title ?? string.Empty;

        }



        private static void CreateRowTextContent(
            Transform rowTransform,
            string title,
            bool hasSubtitle,
            string subtitle,
            float insetRight)
        {
            GameObject textArea = new GameObject("TextArea", typeof(RectTransform));
            textArea.transform.SetParent(rowTransform, false);

            RectTransform textRect = textArea.GetComponent<RectTransform>();
            StretchFull(textRect);

            if (insetRight > 0f)
            {
                textRect.offsetMax = new Vector2(-insetRight, 0f);
            }

            if (hasSubtitle)
            {
                CreateSubtitleRowContent(textArea.transform, title, subtitle);
            }
            else
            {
                CreateSingleLineRowContent(textArea.transform, title);
            }
        }

        private static bool TryCreateCategoryIconRight(
            Transform rowTransform,
            PartSuggestion suggestion,
            bool showPlaceholderWhenMissing)
        {
            StockCategorizerIconHelper.TryResolveLiveCategory(suggestion, out object liveCategory);

            CustomCategoryIconHelper.CategoryIconSource iconSource = CustomCategoryIconHelper.GetIconSource(
                suggestion.IconName,
                suggestion.FilterKey,
                liveCategory);

            if (!iconSource.IsValid && !showPlaceholderWhenMissing)
            {
                return false;
            }

            CreateCategoryIconRight(rowTransform, suggestion, liveCategory, iconSource, showPlaceholderWhenMissing);
            return iconSource.IsValid || showPlaceholderWhenMissing;
        }

        private static void CreateCategoryIconRight(
            Transform rowTransform,
            PartSuggestion suggestion,
            object liveCategory,
            CustomCategoryIconHelper.CategoryIconSource iconSource,
            bool showPlaceholderWhenMissing)
        {
            GameObject iconObject = new GameObject("CategoryIcon", typeof(RectTransform));
            iconObject.transform.SetParent(rowTransform, false);

            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(1f, 0.5f);
            iconRect.anchorMax = new Vector2(1f, 0.5f);
            // Pivot on the right so negative X inset keeps the icon inside the row.
            // (pivot-left + positive X from a right anchor placed icons outside the viewport mask.)
            iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-CategoryIconPadding, 0f);
            iconRect.sizeDelta = new Vector2(CategoryIconSize, CategoryIconSize);

            RawImage iconImage = iconObject.AddComponent<RawImage>();
            iconImage.raycastTarget = false;
            iconImage.enabled = true;
            iconObject.transform.SetAsLastSibling();

            if (iconSource.IsValid)
            {
                iconImage.texture = iconSource.Texture;
                iconImage.uvRect = iconSource.UvRect;
                iconImage.color = Color.white;
            }
            else if (showPlaceholderWhenMissing)
            {
                iconImage.texture = Texture2D.whiteTexture;
                iconImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                iconImage.color = new Color(0.2f, 0.24f, 0.3f, 0.35f);
                EditorBootstrap.LogWarning(
                    "Category row icon missing for kind="
                    + suggestion.Kind
                    + " key='"
                    + (suggestion.FilterKey ?? string.Empty)
                    + "' icon='"
                    + (suggestion.IconName ?? string.Empty)
                    + "'.");
            }
        }

        private static void CreateSubtitleRowContent(Transform rowTransform, string title, string subtitle)

        {

            GameObject content = new GameObject("Content", typeof(RectTransform));

            content.transform.SetParent(rowTransform, false);

            StretchFull(content.GetComponent<RectTransform>());



            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();

            contentLayout.childAlignment = TextAnchor.MiddleCenter;

            contentLayout.childControlHeight = true;

            contentLayout.childControlWidth = true;

            contentLayout.childForceExpandHeight = false;

            contentLayout.childForceExpandWidth = true;

            contentLayout.spacing = 0f;

            contentLayout.padding = new RectOffset(4, 4, 2, 2);



            CreateRowLabel(content.transform, "Title", TitleLineHeight, PrimaryFontSize, PrimaryTextColor, title);

            CreateRowLabel(

                content.transform,

                "Subtitle",

                SubtitleLineHeight,

                SubtitleFontSize,

                SubtitleTextColor,

                subtitle,

                TextAlignmentOptions.Center);

        }



        private void AddRowPointerDownHandler(GameObject row, PartSuggestion suggestion)

        {

            EventTrigger trigger = row.AddComponent<EventTrigger>();



            EventTrigger.Entry pointerDown = new EventTrigger.Entry

            {

                eventID = EventTriggerType.PointerDown

            };

            pointerDown.callback.AddListener(_ => HandleRowPointerDown(suggestion));

            trigger.triggers.Add(pointerDown);

        }



        private void HandleRowPointerDown(PartSuggestion suggestion)

        {

            string text = suggestion.DisplayText ?? suggestion.QueryText ?? string.Empty;

            EditorBootstrap.Log(

                "Row clicked: '"

                + text

                + "' kind="

                + suggestion.Kind

                + " key='"

                + (suggestion.FilterKey ?? string.Empty)

                + "'");

            OnSuggestionChosen?.Invoke(suggestion);

        }



        private void ClearRows()

        {

            foreach (GameObject row in _rowObjects)

            {

                if (row != null)

                {

                    Destroy(row);

                }

            }



            _rowObjects.Clear();



            if (_scrollRect != null)

            {

                _scrollRect.verticalNormalizedPosition = 1f;

            }

        }

    }

}

