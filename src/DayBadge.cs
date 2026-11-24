using System.Collections.Generic;
using HarmonyLib;
using SuperFantasyKingdom;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VisitorCalendar
{
    internal sealed class DayBadge : MonoBehaviour, IPointerClickHandler
    {
        private RectTransform panel;
        private TMP_FontAsset font;
        private float refresh;
        private bool shortcutVisible;
        private GameInputManager mapInput;
        private static readonly AccessTools.FieldRef<GameInputManager, Button> NativeCalendarButton =
            AccessTools.FieldRefAccess<GameInputManager, Button>("calendarButton");
        private string lastKey;
        private RectTransform foodDropdown;
        private readonly Vector3[] corners = new Vector3[4];

        internal static void Attach(Transform day)
        {
            // The old clock and the compact HUD each own a different day control.
            var rect = CalendarUi.Rect(day, "Visitor Calendar", new Vector2(122, 25), Vector2.zero);
            rect.pivot = new Vector2(1, 1);
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(0, -2);
            if (day.GetComponent<SuperFantasyKingdom.UI.UIClockButtonDay>() != null)
            {
                // The speed control and its binding prompt sit directly below Day.
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.anchoredPosition = new Vector2(-8, -2);
            }
            var self = rect.gameObject.AddComponent<DayBadge>();
            self.panel = rect;
            self.mapInput = FindObjectOfType<GameInputManager>();
            // The shipped HUD names its expandable food stock panel "Dropwdown".
            for (var parent = day.parent; parent != null && self.foodDropdown == null; parent = parent.parent)
                self.foodDropdown = parent.Find("HeaderNEW/PanelFood/Dropwdown") as RectTransform;
            self.font = day.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
            CalendarUi.Image(rect, CalendarUi.Panel, true);
        }

        private void LateUpdate()
        {
            if (foodDropdown == null) return;
            var day = (RectTransform)panel.parent;
            var position = panel.anchoredPosition;
            float top = -2;
            if (foodDropdown.gameObject.activeInHierarchy)
            {
                panel.GetWorldCorners(corners);
                float left = day.InverseTransformPoint(corners[0]).x;
                float right = day.InverseTransformPoint(corners[2]).x;
                foodDropdown.GetWorldCorners(corners);
                Vector3 min = day.InverseTransformPoint(corners[0]);
                Vector3 max = day.InverseTransformPoint(corners[2]);
                if (min.x < right && max.x > left)
                    top = Mathf.Min(top, min.y - day.rect.yMin - 6);
            }
            if (!Mathf.Approximately(position.y, top))
                panel.anchoredPosition = new Vector2(position.x, top);
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsGameLoaded() ||
                GameManager.Instance.IsUnloading() || DialogueManager.Instance == null ||
                DaytimeManager.Instance == null || WorldManager.Instance == null) return;
            if (!mapInput && Time.unscaledTime >= refresh) mapInput = FindObjectOfType<GameInputManager>();
            var nativeButton = mapInput ? NativeCalendarButton(mapInput) : null;
            bool showShortcut = CalendarShortcut.CanAdvertise(nativeButton && nativeButton.IsInteractable(),
                UIManager.Instance.IsWorldInteractable(), UIManager.Instance.CheatsEnabled(), Input.GetKey(KeyCode.LeftControl));
            if (Time.unscaledTime < refresh && shortcutVisible == showShortcut) return;
            refresh = Time.unscaledTime + .75f;
            shortcutVisible = showShortcut;
            var schedule = Visitors.Read();
            var visits = schedule.ForDay(schedule.Today);
            // The map action is unavailable in merchant, contract and calendar overlays.
            string prompt = showShortcut
                ? CalendarUi.WithBinding(CalendarShortcut.OpenAction, "Calendar") : "Calendar";
            string key = prompt;
            foreach (var actor in visits)
            {
                var sprite = Visitors.Portrait(actor);
                key += "|" + actor + ":" + (sprite != null ? sprite.GetInstanceID() : 0) +
                    ":" + schedule.Status(schedule.Today, actor, VisitState.IsClaimed(actor)) + ":" + Visitors.Title(actor);
            }
            if (key == lastKey) return;
            lastKey = key;
            Redraw(visits, prompt, schedule);
        }

        private void Redraw(List<Actors> visits, string prompt, Schedule schedule)
        {
            foreach (Transform child in panel) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            int width = visits.Count == 0 ? 126 : 158;
            panel.sizeDelta = new Vector2(width, 22 + visits.Count * 31);
            var title = Row("Calendar", 0, width - 4, 20, 0);
            CalendarUi.Label(title, font, 15).text = prompt;
            for (int i = 0; i < visits.Count; i++)
            {
                var actor = visits[i];
                int top = 22 + i * 31;
                var image = CalendarUi.Image(Row("Portrait", 4, 28, 28, top), Color.white);
                image.sprite = Visitors.Portrait(actor);
                image.enabled = image.sprite != null;
                image.preserveAspect = true;
                bool claimed = VisitState.IsClaimed(actor);
                var label = CalendarUi.Label(Row("Appointment", 36, width - 38, 30, top), font, 15);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.text = Visitors.Title(actor) + "\n" + schedule.Status(schedule.Today, actor, claimed);
                if (claimed) label.color = CalendarUi.Mint;
            }
        }

        private RectTransform Row(string name, int x, int width, int height, int top)
        {
            var row = CalendarUi.Rect(panel, name, new Vector2(width, height), Vector2.zero);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 1);
            row.anchoredPosition = new Vector2(x, -top);
            return row;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !UIManager.Instance.IsWorldInteractable()) return;
            if (OverlayManager.Instance.IsOpen()) return;
            var button = transform.parent.GetComponent<Button>();
            if (button != null && !button.IsInteractable()) return;
            if (button != null) Plugin.Focus(button.gameObject);
            OverlayManager.Instance.OpenCalendar();
        }
    }
}
