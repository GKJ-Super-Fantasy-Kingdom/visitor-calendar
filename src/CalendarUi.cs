using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using SuperFantasyKingdom;

namespace VisitorCalendar
{
    internal static class CalendarUi
    {
        internal static readonly Color Panel = new Color(.11f, .13f, .17f, .97f);
        internal static readonly Color Gold = new Color(1f, .85f, .48f);
        internal static readonly Color Mint = new Color(.63f, .94f, .67f);
        internal static readonly Color Plum = new Color(.15f, .11f, .18f);
        internal static readonly Color Copper = new Color(.57f, .38f, .28f);
        internal static readonly Color BackRed = new Color(.43f, .10f, .07f);
        internal static FooterGraphic FooterSurface(RectTransform rect, Color color, bool edge = false)
        {
            var graphic = rect.gameObject.AddComponent<FooterGraphic>();
            graphic.Edge = edge;
            graphic.color = color;
            graphic.raycastTarget = !edge;
            return graphic;
        }

        internal static RectTransform ControlRim(RectTransform control, string name, Color color, float inset = 0)
        {
            var rim = Rect(control, name, Vector2.zero, Vector2.zero);
            rim.anchorMin = Vector2.zero;
            rim.anchorMax = Vector2.one;
            SetControlRimInset(rim, inset);
            FooterSurface(rim, color, edge: true);
            return rim;
        }

        internal static void SetControlRimInset(RectTransform rim, float inset)
        {
            rim.offsetMin = Vector2.one * inset;
            rim.offsetMax = -rim.offsetMin;
        }

        internal static RectTransform Border(RectTransform parent, string name, float inset, float width, Color color)
        {
            var border = Rect(parent, name, Vector2.zero, Vector2.zero);
            border.anchorMin = Vector2.zero;
            border.anchorMax = Vector2.one;
            border.offsetMin = Vector2.one * inset;
            border.offsetMax = -border.offsetMin;
            Edge(Vector2.zero, Vector2.right, new Vector2(0, width), new Vector2(.5f, 0));
            Edge(Vector2.up, Vector2.one, new Vector2(0, width), new Vector2(.5f, 1));
            Edge(Vector2.zero, Vector2.up, new Vector2(width, 0), new Vector2(0, .5f));
            Edge(Vector2.right, Vector2.one, new Vector2(width, 0), new Vector2(1, .5f));
            return border;

            void Edge(Vector2 min, Vector2 max, Vector2 size, Vector2 pivot)
            {
                var edge = Rect(border, "Edge", size, Vector2.zero);
                edge.anchorMin = min;
                edge.anchorMax = max;
                edge.pivot = pivot;
                Image(edge, color);
            }
        }

        // One icon family for the date tiles, undated reminder and appointment card.
        internal static Image AppointmentArt(RectTransform rect, Sprite sprite, bool deliveryDeadline)
        {
            var image = Image(rect, Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.enabled = sprite != null;
            if (sprite == null)
            {
                if (deliveryDeadline)
                {
                    Image(Rect(rect, "Stroke", rect.sizeDelta * new Vector2(.25f, .48f),
                        Vector2.up * (rect.sizeDelta.y * .15f)), Gold);
                    Image(Rect(rect, "Dot", rect.sizeDelta * .25f,
                        Vector2.down * (rect.sizeDelta.y * .27f)), Gold);
                }
                else
                {
                    var diamond = Rect(rect, "Appointment", rect.sizeDelta * .3f, Vector2.zero);
                    diamond.localRotation = Quaternion.Euler(0, 0, 45);
                    Image(diamond, Gold);
                }
            }
            return image;
        }

        internal static Color VisitColor(string status) =>
            status == "Forecast" ? Gold : status.Contains("Claimed") ? Mint : Color.white;

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

        internal static InputAction PagingHint(string actionName, GamepadManager pad, Image glyph, TMP_Text key)
        {
            var player = pad.GetPlayerInput();
            var action = player?.actions.FindAction(actionName);
            if (action != null && action.enabled && !string.IsNullOrEmpty(player.currentControlScheme))
            {
                foreach (var control in action.controls)
                {
                    if (!player.devices.Contains(control.device)) continue;
                    int i = action.GetBindingIndexForControl(control);
                    if (i < 0) continue;
                    var binding = action.bindings[i];
                    if (!string.IsNullOrEmpty(binding.groups) && !binding.groups.Split(';')
                        .Contains(player.currentControlScheme, System.StringComparer.OrdinalIgnoreCase)) continue;
                    bool nativeGlyph = control.device is DualShockGamepad ||
                        (control.device is Gamepad && (control.device.layout == "Gamepad" ||
                            InputSystem.IsFirstLayoutBasedOnSecond(control.device.layout, "XInputController")));
                    glyph.sprite = nativeGlyph ? pad.GetButtonPrompt(binding.effectivePath) : null;
                    glyph.enabled = glyph.sprite != null;
                    key.text = glyph.enabled ? "" : action.GetBindingDisplayString(i);
                    glyph.transform.parent.gameObject.SetActive(true);
                    return action;
                }
            }
            glyph.transform.parent.gameObject.SetActive(false);
            return null;
        }
    }

    internal sealed class FooterGraphic : MaskableGraphic
    {
        internal bool Edge;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var box = GetPixelAdjustedRect();
            if (box.width <= 4 || box.height <= 4) return;

            // The corner steps keep their pixel size as the footer widens.
            Row(box.yMin, box.yMin + 1, 2, cap: true);
            Row(box.yMin + 1, box.yMin + 2, 1);
            Row(box.yMin + 2, box.yMax - 2, 0);
            Row(box.yMax - 2, box.yMax - 1, 1);
            Row(box.yMax - 1, box.yMax, 2, cap: true);

            void Row(float bottom, float top, float inset, bool cap = false)
            {
                float left = box.xMin + inset, right = box.xMax - inset;
                if (!Edge || cap) AddQuad(left, right, bottom, top);
                else
                {
                    AddQuad(left, left + 1, bottom, top);
                    AddQuad(right - 1, right, bottom, top);
                }
            }

            void AddQuad(float left, float right, float bottom, float top)
            {
                int start = mesh.currentVertCount;
                mesh.AddVert(new Vector3(left, bottom), color, Vector2.zero);
                mesh.AddVert(new Vector3(left, top), color, Vector2.zero);
                mesh.AddVert(new Vector3(right, top), color, Vector2.zero);
                mesh.AddVert(new Vector3(right, bottom), color, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start + 2, start + 3, start);
            }
        }
    }
}
