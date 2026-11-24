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
        private Schedule schedule;
        private readonly Dictionary<int, Button> days = new Dictionary<int, Button>();
        private LocalizedString boss, dayTitle;
        private Transform grid;
        private GameObject last;
        private TMP_FontAsset font;
        private Sprite commissionIcon;
        private RectTransform details;
        private Image portrait;
        private TextMeshProUGUI heading, nameLabel, status;
        private Button back, previous, next;
        private int inspected, index;
        private List<Actors> choices;
        private List<CalendarNotice> notices, inspectedNotices;
        private RectTransform undated, focusTint;
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
                if (commissionIcon)
                    CalendarUi.Sprite(CalendarUi.Rect(strip, "Commission", new Vector2(26, 26), new Vector2(-198, 0)), commissionIcon);
                var line = CalendarUi.Label(CalendarUi.Rect(strip, "Label", new Vector2(commissionIcon ? 382 : 422, 34),
                    new Vector2(commissionIcon ? 18 : 0, 0)), font, 25);
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
                    if (!reminders[i].IsCommission)
                    {
                        CalendarUi.Image(CalendarUi.Rect(symbol, "Stroke", new Vector2(4, 9), new Vector2(0, 3)), CalendarUi.Gold);
                        CalendarUi.Image(CalendarUi.Rect(symbol, "Dot", new Vector2(4, 3), new Vector2(0, -5)), CalendarUi.Gold);
                    }
                    else if (commissionIcon) CalendarUi.Sprite(symbol, commissionIcon);
                    else
                    {
                        var label = CalendarUi.Label(symbol, font, 26);
                        label.fontStyle = FontStyles.Bold;
                        label.text = "+";
                        label.color = CalendarUi.Gold;
                    }
                }
            }
            if (reminders.Count > 0 || guests.Count > 0) button.onClick.AddListener(() => Inspect(day));
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
                if (marker != null)
                {
                    var label = CalendarUi.Label(slot, font, size);
                    label.text = marker;
                    label.color = CalendarUi.Gold;
                }
                else
                {
                    CalendarUi.Sprite(slot, sprite);
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
            if (action != null && action.WasPerformedThisFrame()) OverlayManager.Instance.Cancel();
        }

        private void LateUpdate()
        {
            var target = EventSystem.current?.currentSelectedGameObject;
            if (grid != null && target != null && (target.transform.parent == grid || target.transform.parent == last.transform))
            {
                if (focusTint == null)
                {
                    focusTint = CalendarUi.Rect(target.transform, "Focused date", Vector2.zero, Vector2.zero);
                    CalendarUi.Image(focusTint, new Color(1f, .85f, .48f, .25f));
                    focusTint.anchorMin = Vector2.zero;
                    focusTint.anchorMax = Vector2.one;
                }
                if (focusTint.parent != target.transform) focusTint.SetParent(target.transform, false);
                if (focusTint.GetSiblingIndex() != 0) focusTint.SetAsFirstSibling();
                if (!focusTint.gameObject.activeSelf) focusTint.gameObject.SetActive(true);
            }
            else if (focusTint != null && focusTint.gameObject.activeSelf) focusTint.gameObject.SetActive(false);
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
            if (target != null && target.transform.IsChildOf(transform) &&
                GamepadManager.Instance.GetGameObjectToSelect() != target)
                GamepadManager.Instance.SetGameObjectToSelect(target, force: true);
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
            CalendarUi.Image(details, CalendarUi.Panel, true);
            heading = Text("Day", 82, 24);
            portrait = CalendarUi.Image(CalendarUi.Rect(details, "Portrait", new Vector2(66, 66), new Vector2(0, 28)), Color.white);
            portrait.preserveAspect = true;
            nameLabel = Text("Visitor", -22, 30);
            status = Text("Status", -53, 23);
            var template = Resources.Load<GameObject>("UI/OverlayMessage")?.transform.Find("Buttons/BuyButton");
            previous = Control("Previous", -130, () => { index--; Display(); }, template);
            back = Control("Back", 0, () => Back(), template);
            next = Control("Next", 130, () => { index++; Display(); }, template);
        }

        private TextMeshProUGUI Text(string name, float y, int size) =>
            CalendarUi.Label(CalendarUi.Rect(details, name, new Vector2(404, 35), new Vector2(0, y)), font, size);

        private Button Control(string title, float x, UnityEngine.Events.UnityAction action, Transform template)
        {
            var rect = CalendarUi.Rect(details, title, new Vector2(118, 38), new Vector2(x, -93));
            var bg = CalendarUi.Image(rect, Color.white, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var native = template ? template.GetComponent<Image>() : null;
            var caption = template ? template.Find("Text")?.GetComponent<TMP_Text>() : null;
            var label = CalendarUi.Label(CalendarUi.Rect(rect, "Label", new Vector2(108, 34), Vector2.zero), font, 25);
            label.text = title;
            var colors = button.colors;
            if (native && native.sprite && caption)
            {
                bg.sprite = native.sprite;
                bg.type = native.type;
                bg.pixelsPerUnitMultiplier = native.pixelsPerUnitMultiplier;
                label.color = caption.color;
                colors.normalColor = Color.white;
                colors.selectedColor = colors.highlightedColor = CalendarUi.Gold;
            }
            else
            {
                colors.normalColor = new Color(.22f, .26f, .32f);
                colors.selectedColor = colors.highlightedColor = new Color(.46f, .36f, .18f);
            }
            button.colors = colors;
            button.onClick.AddListener(action);
            return button;
        }

        private void Display()
        {
            int count = choices.Count + inspectedNotices.Count;
            if (index < 0) index = count - 1;
            if (index >= count) index = 0;
            heading.text = dayTitle.GetLocalizedString(inspected.ToString()) +
                (count > 1 ? "  ·  " + (index + 1) + "/" + count : "");
            heading.color = CalendarUi.Gold;
            bool isVisitor = index < choices.Count;
            var notice = isVisitor ? null : inspectedNotices[index - choices.Count];
            portrait.sprite = isVisitor ? Visitors.Portrait(choices[index]) : notice.IsCommission ? commissionIcon : null;
            bool showArt = portrait.sprite != null;
            nameLabel.rectTransform.anchoredPosition = new Vector2(0, showArt ? -22 : 18);
            status.rectTransform.anchoredPosition = new Vector2(0, showArt ? -53 : -18);
            if (isVisitor)
            {
                var id = choices[index];
                nameLabel.text = Visitors.Title(id);
                bool claimed = inspected == schedule.Today && VisitState.IsClaimed(id);
                status.text = schedule.Status(inspected, id, claimed);
                status.color = claimed ? CalendarUi.Mint : Color.white;
            }
            else
            {
                nameLabel.text = notice.Title;
                status.text = notice.Detail;
                status.color = CalendarUi.Gold;
            }
            portrait.enabled = portrait.sprite != null;
            previous.gameObject.SetActive(count > 1);
            next.gameObject.SetActive(count > 1);
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
            return true;
        }
    }
}
