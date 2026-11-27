using System.Collections.Generic;
using SuperFantasyKingdom;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace VisitorCalendar
{
    internal sealed class CalendarView : MonoBehaviour
    {
        // Leave a dark gap between the native white grid line and the active rim.
        private const float DateEdgeInset = 4;
        private Schedule schedule;
        private readonly Dictionary<int, Button> days = new Dictionary<int, Button>();
        private LocalizedString boss, dayTitle;
        private Transform grid;
        private GameObject last;
        private TMP_FontAsset font;
        private Sprite commissionIcon;
        private RectTransform details;
        private RectTransform artwork;
        private Image portrait, previousGlyph, nextGlyph;
        private TextMeshProUGUI heading, nameLabel, status, previousKey, nextKey;
        private Button back, previous, next;
        private int inspected, index;
        private List<Actors> choices;
        private List<CalendarNotice> notices, inspectedNotices;
        private RectTransform undated, focusOutline;
        private CalendarPointer pointer;
        private bool pointerMode;
        private bool lastWasVisible;
        private int consumedFrame = -1;
        private int openedFrame;
        private bool initialFocusPending;

        internal void Bind(Transform cells, GameObject finalDay, LocalizedString bossTitle, LocalizedString title)
        {
            if (grid != null) return;
            grid = cells;
            last = finalDay;
            dayTitle = title;
            boss = bossTitle;
            schedule = Visitors.Read();
            notices = CalendarNotice.Read(schedule, PartnerCalendar.Contract, PartnerCalendar.Commission, Plugin.LogWarning);
            if (notices.Exists(n => n.IsCommission) && PrefabsManager.Instance)
            {
                var chest = PrefabsManager.Instance.GetDropChest();
                if (chest) commissionIcon = chest.GetComponent<SpriteRenderer>()?.sprite;
            }
            for (int i = 0; i < Mathf.Min(28, cells.childCount); ++i) BindDay(i + 1, cells.GetChild(i));
            if (last.transform.childCount > 0) BindDay(29, last.transform.GetChild(0));
            var pending = notices.Find(n => !n.Day.HasValue);
            if (pending != null)
            {
                undated = CalendarUi.Rect(grid.parent, "Undated commission", new Vector2(436, 46), Vector2.zero);
                undated.SetSiblingIndex(grid.GetSiblingIndex());
                undated.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
                var strip = CalendarUi.Rect(undated, "Reminder", new Vector2(436, 34), Vector2.zero);
                CalendarUi.Image(strip, CalendarUi.Panel);
                CalendarUi.Border(strip, "Reminder frame", 1, 1, CalendarUi.Copper);
                CalendarUi.AppointmentArt(CalendarUi.Rect(strip, "Commission", new Vector2(24, 24),
                    new Vector2(-197, 0)), commissionIcon, false);
                var line = CalendarUi.Label(CalendarUi.Rect(strip, "Label", new Vector2(376, 30),
                    new Vector2(17, 0)), font, 23);
                line.text = pending.Title + "  ·  " + pending.Detail;
                line.color = CalendarUi.Gold;
            }
            // The native overlay can activate its container after OpenCalendar returns.
            SelectToday();
        }

        private void BindDay(int day, Transform cell)
        {
            var button = cell.GetComponent<Button>();
            if (button == null) return;
            days.Add(day, button);
            var input = cell.gameObject.AddComponent<CalendarPointer>();
            input.View = this;
            if (font == null) font = cell.Find("Day")?.GetComponent<TextMeshProUGUI>()?.font;
            var guests = schedule.ForDay(day);
            // Day X has its own native endgame art.
            var icon = cell.Find("Image");
            if (icon != null && day < 29) icon.gameObject.SetActive(false);
            var reminders = notices.FindAll(n => n.Day == day);
            if (reminders.Count != 0)
            {
                var corner = CalendarUi.Rect(cell, "Delivery marker", new Vector2(17 * reminders.Count, 18), Vector2.zero);
                corner.anchorMin = corner.anchorMax = corner.pivot = Vector2.one;
                corner.anchoredPosition = new Vector2(-2, -2);
                for (int i = 0; i < reminders.Count; i++)
                {
                    var symbol = CalendarUi.Rect(corner, reminders[i].Title, new Vector2(16, 16),
                        new Vector2((i - (reminders.Count - 1) / 2f) * 17, 0));
                    bool commission = reminders[i].IsCommission;
                    CalendarUi.AppointmentArt(symbol, commission ? commissionIcon : null, !commission);
                }
            }
            if (reminders.Count > 0 || guests.Count > 0)
            {
                input.Actionable = true;
                // Leave the shared grid line and the tile's native graphic in place.
                CalendarUi.ControlRim((RectTransform)cell, "Appointment edge", Color.black, DateEdgeInset);
                button.onClick.AddListener(() => Inspect(day));
            }
            if (guests.Count == 0) return;
            // Leave the native Day X artwork its own space on the final tile.
            bool finalArt = day == 29 && icon && icon.gameObject.activeSelf;
            int width = finalArt ? 32 : 56;
            if (finalArt && icon is RectTransform endgame)
            {
                endgame.sizeDelta = new Vector2(24, 24);
                endgame.anchoredPosition = new Vector2(-14, 17);
            }
            var row = CalendarUi.Rect(cell, "Visitor portraits", new Vector2(width, 32), Vector2.zero);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(.5f, 0);
            row.anchoredPosition = new Vector2(finalArt ? -13 : 0, 3);
            int slots = finalArt ? 2 : 3;
            int shown = Mathf.Min(guests.Count, slots);
            int size = Mathf.Min(32, width / shown);
            for (int i = 0; i < shown; ++i)
            {
                var slot = CalendarUi.Rect(row, guests[i].ToString(), new Vector2(size, size),
                    new Vector2((i - (shown - 1) / 2f) * size, 0));
                var sprite = Visitors.Portrait(guests[i]);
                string marker = Schedule.CellMarker(sprite != null, i, guests.Count, slots);
                if (marker != null && marker != "•")
                {
                    var label = CalendarUi.Label(slot, font, size);
                    label.text = marker;
                    label.color = CalendarUi.Gold;
                }
                else
                {
                    CalendarUi.AppointmentArt(slot, sprite, false);
                }
            }
        }

        internal void SelectToday()
        {
            openedFrame = Time.frameCount;
            initialFocusPending = true;
            if (days.TryGetValue(schedule.Today, out var button) && button.isActiveAndEnabled)
            {
                Plugin.Focus(button.gameObject);
                Hover(schedule.Today);
            }
        }

        internal void KeyboardToggle()
        {
            if (openedFrame == Time.frameCount || consumedFrame == Time.frameCount ||
                GamepadManager.Instance.IsGamepadInUse()) return;
            var action = GamepadManager.Instance.GetPlayerInput()?.actions.FindAction(CalendarShortcut.KeyboardBackAction);
            if (action != null && action.WasPerformedThisFrame())
            {
                UseNavigation();
                OverlayManager.Instance.Cancel();
            }
        }

        private void LateUpdate()
        {
            var pad = GamepadManager.Instance;
            var target = EventSystem.current?.currentSelectedGameObject;
            var active = pad.IsGamepadInUse() || !pointerMode ? target
                : pointer && pointer.isActiveAndEnabled ? pointer.gameObject : null;
            UpdateOutline(active);
            // The input event that opened the overlay can reselect the HUD button
            // after OnEnable. Recover once the native container is interactive.
            if ((initialFocusPending || target == null || Plugin.IsPreviousFocus(target)) && grid != null &&
                OverlayManager.Instance.IsCalendarOpen() && days.TryGetValue(schedule.Today, out var today))
            {
                if (details != null && details.gameObject.activeInHierarchy) Plugin.Focus(back.gameObject);
                else if (today.isActiveAndEnabled && today.IsInteractable())
                {
                    Plugin.Focus(today.gameObject);
                    initialFocusPending = false;
                }
                return;
            }
            if (target != null && target.transform.IsChildOf(transform) && pad.GetGameObjectToSelect() != target)
                pad.SetGameObjectToSelect(target, force: true);
            UpdatePaging(pad);
        }

        internal void Pointer(CalendarPointer cell, bool enter)
        {
            if (enter)
            {
                pointer = cell;
                UsePointer();
            }
            else if (pointer == cell) pointer = null;
        }

        internal void UsePointer() => pointerMode = true;
        internal void UseNavigation() => pointerMode = false;

        private void UpdateOutline(GameObject target)
        {
            if (target && target.activeInHierarchy &&
                target.TryGetComponent<CalendarPointer>(out var control) && control.View == this && control.Actionable &&
                target.TryGetComponent<Button>(out var button) && button.IsInteractable())
            {
                var rect = (RectTransform)target.transform;
                bool isDate = rect.parent != details;
                float inset = isDate ? DateEdgeInset - 1 : -1;
                if (focusOutline == null)
                {
                    focusOutline = CalendarUi.ControlRim(rect, "Appointment focus", Color.white, inset);
                }
                else if (focusOutline.parent != rect)
                {
                    focusOutline.SetParent(rect, false);
                    CalendarUi.SetControlRimInset(focusOutline, inset);
                }
                if (isDate && focusOutline.GetSiblingIndex() != 0) focusOutline.SetAsFirstSibling();
                focusOutline.gameObject.SetActive(true);
            }
            else if (focusOutline != null) focusOutline.gameObject.SetActive(false);
        }

        internal bool Hover(int day)
        {
            if (!days.ContainsKey(day)) return false;
            if (details != null && details.gameObject.activeSelf) return true;
            var text = dayTitle.GetLocalizedString(day.ToString());
            if (day <= 28 && day % 7 == 0) text += "\n" + boss.GetLocalizedString();
            var appointments = schedule.ForDay(day);
            var reminders = notices.FindAll(n => n.Day == day);
            if (appointments.Count == 0 && reminders.Count == 0) text += "\nNo Known Appointments";
            else
            {
                int shown = Mathf.Min(appointments.Count, 3);
                for (int i = 0; i < shown; i++)
                {
                    var id = appointments[i];
                    text += "\n" + Visitors.Title(id);
                    text += "  ·  " + schedule.Status(day, id, day == schedule.Today && VisitState.IsClaimed(id));
                }
                if (appointments.Count > shown)
                {
                    int more = appointments.Count - shown;
                    text += "\n+" + more + (more == 1 ? " more visitor" : " more visitors");
                }
                foreach (var notice in reminders) text += "\n" + notice.Title;
                text += "\nSelect day to inspect";
            }
            var bounds = (RectTransform)grid;
            var edge = bounds.TransformPoint(new Vector3(bounds.rect.xMin - 16, bounds.rect.center.y));
            var tip = TooltipManager.Instance;
            tip.StopFollowMouse();
            if (edge.x >= 200 * SettingsManager.Instance.GetUIScaling())
                tip.SetAnchor(TooltipManager.AnchorPresets.Right);
            else
            {
                tip.SetAnchor(TooltipManager.AnchorPresets.Top);
                edge = bounds.TransformPoint(new Vector3(bounds.rect.center.x, bounds.rect.yMin - 16));
            }
            tip.Display(text, edge, fix: false);
            return true;
        }

        private void Inspect(int day)
        {
            choices = schedule.ForDay(day);
            inspectedNotices = notices.FindAll(n => n.Day == day);
            if (choices.Count + inspectedNotices.Count == 0) return;
            inspected = day;
            index = 0;
            if (details == null) CreateDetails();
            lastWasVisible = last.activeSelf;
            grid.gameObject.SetActive(false);
            if (undated != null) undated.gameObject.SetActive(false);
            last.SetActive(false);
            details.gameObject.SetActive(true);
            TooltipManager.Instance.Hide();
            Display();
            Plugin.Focus(back.gameObject);
        }

        private void CreateDetails()
        {
            var source = (RectTransform)grid;
            details = CalendarUi.Rect(grid.parent, "Appointment", source.sizeDelta, source.anchoredPosition);
            // Replace the grid at its layout position so companion footers stay below the detail panel.
            details.SetSiblingIndex(grid.GetSiblingIndex() + 1);
            details.anchorMin = source.anchorMin;
            details.anchorMax = source.anchorMax;
            details.pivot = source.pivot;
            details.anchoredPosition = source.anchoredPosition;
            var layout = details.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = source.rect.width;
            layout.preferredHeight = source.rect.height;
            CalendarUi.Image(details, CalendarUi.Plum, true);
            CalendarUi.Border(details, "Appointment frame", 1, 2, CalendarUi.Copper);
            heading = Text("Day", .88f, 29, 24);
            heading.color = CalendarUi.Gold;
            artwork = Place("Artwork", new Vector2(68, 62), 0, .66f);
            CalendarUi.Image(artwork, CalendarUi.Panel);
            CalendarUi.Border(artwork, "Artwork frame", 0, 1, CalendarUi.Copper);
            nameLabel = Text("Name", .46f, 33, 32);
            status = Text("Status", .32f, 33, 24);
            previous = Control("Previous", -125, 108, false, () => Page(-1));
            back = Control("Back", 0, 94, true, () => Back());
            next = Control("Next", 125, 108, false, () => Page(1));
            (previousGlyph, previousKey) = CreateHint(-125);
            (nextGlyph, nextKey) = CreateHint(125);
        }

        private RectTransform Place(string name, Vector2 size, float x, float height)
        {
            var rect = CalendarUi.Rect(details, name, size, Vector2.zero);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, height);
            rect.anchoredPosition = new Vector2(x, 0);
            return rect;
        }

        private TextMeshProUGUI Text(string name, float height, float lineHeight, int size) =>
            CalendarUi.Label(Place(name, new Vector2(408, lineHeight), 0, height), font, size);

        private Button Control(string title, float x, float width, bool close, UnityEngine.Events.UnityAction action)
        {
            var rect = Place(title, new Vector2(width, 32), x, .155f);
            var bg = CalendarUi.FooterSurface(rect, Color.white);
            CalendarUi.ControlRim(rect, "Button frame", Color.black);
            var input = rect.gameObject.AddComponent<CalendarPointer>();
            input.View = this;
            input.Actionable = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            CalendarUi.Label(CalendarUi.Rect(rect, "Label", new Vector2(width - 12, 26), Vector2.zero),
                font, 24).text = title;
            var colors = button.colors;
            colors.normalColor = close ? CalendarUi.BackRed : new Color(.23f, .17f, .23f);
            colors.selectedColor = colors.highlightedColor = close ? new Color(.55f, .15f, .10f) : new Color(.35f, .26f, .28f);
            colors.pressedColor = CalendarUi.Panel;
            button.colors = colors;
            button.onClick.AddListener(action);
            return button;
        }

        private (Image glyph, TextMeshProUGUI key) CreateHint(float x)
        {
            var row = Place("Paging binding", new Vector2(108, 18), x, .044f);
            var glyph = CalendarUi.Image(CalendarUi.Rect(row, "Glyph", new Vector2(30, 18), Vector2.zero), Color.white);
            glyph.preserveAspect = true;
            var key = CalendarUi.Label(CalendarUi.Rect(row, "Binding", row.sizeDelta, Vector2.zero), font, 17);
            key.color = CalendarUi.Gold;
            row.gameObject.SetActive(false);
            return (glyph, key);
        }

        private void UpdatePaging(GamepadManager pad)
        {
            if (details == null || !details.gameObject.activeSelf || !previous.gameObject.activeSelf) return;
            var left = CalendarUi.PagingHint(CalendarShortcut.PreviousAction, pad, previousGlyph, previousKey);
            var right = CalendarUi.PagingHint(CalendarShortcut.NextAction, pad, nextGlyph, nextKey);
            if (!OverlayManager.Instance.IsCalendarOpen() || PauseMenuManger.Instance?.InMenu() == true) return;
            bool goLeft = left?.WasPerformedThisFrame() == true;
            bool goRight = right?.WasPerformedThisFrame() == true;
            if (goLeft != goRight)
            {
                UseNavigation();
                Page(goLeft ? -1 : 1);
            }
        }

        private void Page(int step)
        {
            index += step;
            Display();
        }

        private void Display()
        {
            int count = choices.Count + inspectedNotices.Count;
            if (index < 0) index = count - 1;
            if (index >= count) index = 0;
            heading.text = dayTitle.GetLocalizedString(inspected.ToString()) +
                (count > 1 ? "  ·  " + (index + 1) + "/" + count : "");
            bool isVisitor = index < choices.Count;
            var notice = isVisitor ? null : inspectedNotices[index - choices.Count];
            bool deadline = !isVisitor && !notice.IsCommission;
            var sprite = isVisitor ? Visitors.Portrait(choices[index]) : deadline ? null : commissionIcon;
            if (portrait != null)
            {
                portrait.gameObject.SetActive(false);
                Destroy(portrait.gameObject);
            }
            portrait = CalendarUi.AppointmentArt(CalendarUi.Rect(artwork, "Appointment icon",
                new Vector2(48, 48), Vector2.zero), sprite, deadline);
            if (isVisitor)
            {
                var id = choices[index];
                nameLabel.text = Visitors.Title(id);
                bool claimed = inspected == schedule.Today && VisitState.IsClaimed(id);
                status.text = schedule.Status(inspected, id, claimed);
                status.color = CalendarUi.VisitColor(status.text);
            }
            else
            {
                nameLabel.text = notice.Title;
                status.text = notice.Detail;
                status.color = notice.Title == "Commission Delivery" ? CalendarUi.Mint : CalendarUi.Gold;
            }
            bool canPage = count > 1;
            previous.gameObject.SetActive(canPage);
            next.gameObject.SetActive(canPage);
            if (!canPage)
            {
                previousGlyph.transform.parent.gameObject.SetActive(false);
                nextGlyph.transform.parent.gameObject.SetActive(false);
            }
            // Keep all navigation inside this page, including when only Back is present.
            Link(back, previous, next);
            Link(previous, next, back);
            Link(next, back, previous);
        }

        private static void Link(Button button, Button left, Button right)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = left.gameObject.activeSelf ? left : button;
            nav.selectOnRight = right.gameObject.activeSelf ? right : button;
            nav.selectOnUp = nav.selectOnDown = button;
            button.navigation = nav;
        }

        internal bool Back()
        {
            if (consumedFrame == Time.frameCount) return true;
            if (details == null || !details.gameObject.activeSelf) return false;
            consumedFrame = Time.frameCount;
            details.gameObject.SetActive(false);
            grid.gameObject.SetActive(true);
            if (undated != null) undated.gameObject.SetActive(true);
            last.SetActive(lastWasVisible);
            Plugin.Focus(days[inspected].gameObject);
            Hover(inspected);
            return true;
        }
    }

    internal sealed class CalendarPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IMoveHandler, ISubmitHandler
    {
        internal CalendarView View;
        internal bool Actionable;
        public void OnPointerEnter(PointerEventData data) => View.Pointer(this, true);
        public void OnPointerExit(PointerEventData data) => View.Pointer(this, false);
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) View.Pointer(this, true);
        }
        public void OnMove(AxisEventData data) => View.UseNavigation();
        public void OnSubmit(BaseEventData data) => View.UseNavigation();
        private void OnDisable() => View.Pointer(this, false);
    }
}
