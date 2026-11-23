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
        private RectTransform details;
        private Image portrait;
        private TextMeshProUGUI heading, nameLabel, status;
        private Button back, previous, next;
        private int inspected, index;
        private List<Actors> choices;
        private bool lastWasVisible;
        private int consumedFrame = -1;
        private int openedFrame;

        internal void Bind(Transform cells, GameObject finalDay, LocalizedString bossTitle, LocalizedString title)
        {
            if (grid != null) return;
            grid = cells;
            last = finalDay;
            dayTitle = title;
            boss = bossTitle;
            schedule = Visitors.Read();
            for (int i = 0; i < Mathf.Min(28, cells.childCount); ++i) BindDay(i + 1, cells.GetChild(i));
            if (last.transform.childCount > 0) BindDay(29, last.transform.GetChild(0));
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
            if (guests.Count == 0) return;
            var row = CalendarUi.Rect(cell, "Visitor portraits", new Vector2(44, 24), new Vector2(0, -6));
            int size = Mathf.Min(24, 44 / guests.Count);
            for (int i = 0; i < guests.Count; ++i)
            {
                var slot = CalendarUi.Rect(row, guests[i].ToString(), new Vector2(size, size),
                    new Vector2((i - (guests.Count - 1) / 2f) * size, 0));
                var image = CalendarUi.Image(slot, Color.white);
                image.sprite = Visitors.Actor(guests[i])?.avatar;
                image.enabled = image.sprite != null;
                image.preserveAspect = true;
            }
            button.onClick.AddListener(() => Inspect(day));
        }

        internal void SelectToday()
        {
            openedFrame = Time.frameCount;
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
            var action = GamepadManager.Instance.GetPlayerInput()?.actions.FindAction("Overlay/Calendar");
            if (action != null && action.WasPerformedThisFrame()) OverlayManager.Instance.Cancel();
        }

        private void LateUpdate()
        {
            var target = EventSystem.current?.currentSelectedGameObject;
            if (target != null && target.transform.IsChildOf(transform) &&
                GamepadManager.Instance.GetGameObjectToSelect() != target)
                GamepadManager.Instance.SetGameObjectToSelect(target, force: true);
        }

        internal bool Hover(int day)
        {
            if (!days.TryGetValue(day, out var cell)) return false;
            if (details != null && details.gameObject.activeSelf) return true;
            var text = dayTitle.GetLocalizedString(day.ToString());
            if (day <= 28 && day % 7 == 0) text += "\n" + boss.GetLocalizedString();
            var appointments = schedule.ForDay(day);
            if (appointments.Count == 0) text += "\nNo known appointments";
            else
            {
                foreach (var id in appointments)
                {
                    text += "\n" + Visitors.Title(id);
                    if (day == schedule.Today && VisitState.IsClaimed(id)) text += "  ·  Claimed today";
                }
                text += "\n" + CalendarUi.WithBinding("UI/Submit", "Inspect");
            }
            TooltipManager.Instance.SetAnchor(TooltipManager.AnchorPresets.Top);
            TooltipManager.Instance.Display(text, cell.transform.position);
            return true;
        }

        private void Inspect(int day)
        {
            choices = schedule.ForDay(day);
            if (choices.Count == 0) return;
            inspected = day;
            index = 0;
            if (details == null) CreateDetails();
            lastWasVisible = last.activeSelf;
            grid.gameObject.SetActive(false);
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
            nameLabel = Text("Visitor", -22, 24);
            status = Text("Status", -53, 18);
            previous = Control("Previous", -122, () => { index--; Display(); });
            back = Control("Back", 0, () => Back());
            next = Control("Next", 122, () => { index++; Display(); });
        }

        private TextMeshProUGUI Text(string name, float y, int size) =>
            CalendarUi.Label(CalendarUi.Rect(details, name, new Vector2(404, 35), new Vector2(0, y)), font, size);

        private Button Control(string title, float x, UnityEngine.Events.UnityAction action)
        {
            var rect = CalendarUi.Rect(details, title, new Vector2(104, 34), new Vector2(x, -93));
            var bg = CalendarUi.Image(rect, Color.white, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.normalColor = new Color(.22f, .26f, .32f);
            colors.selectedColor = colors.highlightedColor = new Color(.40f, .45f, .53f);
            button.colors = colors;
            CalendarUi.Label(CalendarUi.Rect(rect, "Label", rect.sizeDelta, Vector2.zero), font, 17).text = title;
            button.onClick.AddListener(action);
            return button;
        }

        private void Display()
        {
            if (index < 0) index = choices.Count - 1;
            if (index >= choices.Count) index = 0;
            var id = choices[index];
            heading.text = dayTitle.GetLocalizedString(inspected.ToString()) +
                (choices.Count > 1 ? "  ·  " + (index + 1) + "/" + choices.Count : "");
            heading.color = CalendarUi.Gold;
            portrait.sprite = Visitors.Actor(id)?.avatar;
            portrait.enabled = portrait.sprite != null;
            nameLabel.text = Visitors.Title(id);
            bool claimed = inspected == schedule.Today && VisitState.IsClaimed(id);
            status.text = claimed ? "Claimed today" : inspected == schedule.Today ? "Today's visit" : "Scheduled visit";
            status.color = claimed ? CalendarUi.Mint : Color.white;
            previous.gameObject.SetActive(choices.Count > 1);
            next.gameObject.SetActive(choices.Count > 1);
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
            last.SetActive(lastWasVisible);
            Plugin.Focus(days[inspected].gameObject);
            return true;
        }
    }
}
