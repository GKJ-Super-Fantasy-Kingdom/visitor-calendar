using System.Collections.Generic;
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
        private string lastKey;

        internal static void Attach(Transform day)
        {
            // The old clock and the compact HUD each own a different day control.
            var rect = CalendarUi.Rect(day, "Visitor Calendar", new Vector2(122, 25), Vector2.zero);
            rect.pivot = new Vector2(1, 1);
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(0, -2);
            var self = rect.gameObject.AddComponent<DayBadge>();
            self.panel = rect;
            self.font = day.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
            CalendarUi.Image(rect, CalendarUi.Panel, true);
        }

        private void Update()
        {
            if (Time.unscaledTime < refresh) return;
            refresh = Time.unscaledTime + .75f;
            if (GameManager.Instance == null || !GameManager.Instance.IsGameLoaded() ||
                GameManager.Instance.IsUnloading() || DialogueManager.Instance == null ||
                DaytimeManager.Instance == null || WorldManager.Instance == null) return;
            var schedule = Visitors.Read();
            var visits = schedule.ForDay(schedule.Today);
            string prompt = CalendarUi.WithBinding("Game/Calendar", "Calendar");
            string key = prompt;
            foreach (var actor in visits) key += "|" + actor + VisitState.IsClaimed(actor) + Visitors.Title(actor);
            if (key == lastKey) return;
            lastKey = key;
            Redraw(visits, prompt);
        }

        private void Redraw(List<Actors> visits, string prompt)
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
                image.sprite = Visitors.Actor(actor)?.avatar;
                image.enabled = image.sprite != null;
                image.preserveAspect = true;
                bool claimed = VisitState.IsClaimed(actor);
                var label = CalendarUi.Label(Row("Appointment", 36, width - 38, 30, top), font, 15);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.text = Visitors.Title(actor) + (claimed ? "\n✓ Claimed today" : "");
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
