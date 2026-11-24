using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using SuperFantasyKingdom;

namespace VisitorCalendar
{
    internal static class CalendarUi
    {
        internal static readonly Color Panel = new Color(.11f, .13f, .17f, .97f);
        internal static readonly Color Gold = new Color(1f, .85f, .48f);
        internal static readonly Color Mint = new Color(.63f, .94f, .67f);

        internal static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        internal static Image Image(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        internal static TextMeshProUGUI Label(RectTransform rect, TMP_FontAsset font, float size)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = size * .72f;
            label.fontSizeMax = size;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        internal static void Sprite(RectTransform rect, Sprite sprite)
        {
            var image = Image(rect, Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
        }

        internal static string Binding(string actionPath)
        {
            var player = GamepadManager.Instance?.GetPlayerInput();
            var action = player?.actions.FindAction(actionPath);
            if (action == null || !action.enabled) return "";
            int i = action.GetBindingIndex(group: player.currentControlScheme);
            if (i < 0) return "";
            var path = action.bindings[i].effectivePath;
            if (string.IsNullOrEmpty(path)) return "";
            if (path.StartsWith("*"))
            {
                foreach (var control in action.controls)
                    if (player.devices.Contains(control.device)) return control.shortDisplayName ?? control.displayName;
            }
            return action.GetBindingDisplayString(i);
        }

        internal static string WithBinding(string action, string label)
        {
            var binding = Binding(action);
            return string.IsNullOrEmpty(binding) ? label : label + "  [" + binding + "]";
        }
    }
}
