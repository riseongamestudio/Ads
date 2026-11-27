using System;
using System.Collections.Generic;
using System.Linq;
using RiseOn.Utils.Editor.SearchWindow;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// A format's mediations on one line, best first, as "AdMob → MAX", Off with none; the tooltip holds the whole order. Where the names do not fit, each name gives way
    /// to its mediation's icon, the arrows kept; a mediation with no icon keeps the names, cut short with an ellipsis.<br/>
    /// A click opens the page's drop-down of choices to order them (<see cref="ChoiceSearchWindow.OpenOrder{T}"/>): the mediations on, ticked, in their order, then the
    /// page's others that serve the format; a click turns one on or off, a drag reorders the ones on.
    /// </summary>
    internal sealed class AdPriorityListDrawer : OdinValueDrawer<AdPriorityList> {
        // Where each mediation pack keeps its icon, from the pack's folder.
        private const string IconPath = "/Editor/MediationIcon.png";

        private const string Arrow = " → ";

        private static readonly Dictionary<Type, Texture2D> icons = new();

        private static GUIStyle summaryStyle;
        private static GUIStyle arrowStyle;

        private static GUIStyle SummaryStyle => summaryStyle ??= new GUIStyle(EditorStyles.popup) { clipping = TextClipping.Ellipsis };

        private static GUIStyle ArrowStyle => arrowStyle ??= new GUIStyle(EditorStyles.popup) {
            padding = new RectOffset(), margin = new RectOffset(), border = new RectOffset(), normal = { background = null }, alignment = TextAnchor.MiddleCenter
        };

        protected override void DrawPropertyLayout(GUIContent label) {
            var settings = Property.Tree.WeakTargets.OfType<AdsSettings>().FirstOrDefault();
            var rect     = EditorGUILayout.GetControlRect(label != null, EditorGUIUtility.singleLineHeight, GUILayout.MinWidth(0));
            Draw(label == null ? rect : EditorGUI.PrefixLabel(rect, label), settings, () => ValueEntry.SmartValue, Format(Property));
        }

        /// <summary>The line in <paramref name="rect"/>; <paramref name="list"/> gives the list it shows, asked again after each change, as an undo puts another in its place.</summary>
        public static void Draw(Rect rect, AdsSettings settings, Func<AdPriorityList> list, AdFormat? format) {
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var ids     = list().EditableIds;
            var summary = Summary(settings, ids);
            var marks   = ids.Count > 0 && SummaryStyle.CalcSize(summary).x > rect.width ? Icons(settings, ids) : null;
            var shown   = marks == null ? summary : new GUIContent(string.Empty, summary.tooltip);
            if (EditorGUI.DropdownButton(rect, shown, FocusType.Keyboard, SummaryStyle) && settings) Open(rect, settings, list, format);
            if (marks != null && Event.current.type == EventType.Repaint) DrawIcons(rect, marks);
            EditorGUI.indentLevel = indent;
        }

        // The mediations' icons in their order, or none when one has no icon.
        private static List<Texture2D> Icons(AdsSettings settings, List<string> ids) {
            if (!settings) return null;

            var found = new List<Texture2D>();
            foreach (var id in ids) {
                var mediation = settings.Mediations.FirstOrDefault(entry => entry.Id == id);
                if (mediation == null) return null;

                var type = mediation.GetType();
                if (!icons.TryGetValue(type, out var icon)) {
                    var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly);
                    icon        = package != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(package.assetPath + IconPath) : null;
                    icons[type] = icon;
                }

                if (!icon) return null;

                found.Add(icon);
            }

            return found;
        }

        // The icons line height, an arrow between each two, from the button's text inset; what runs past its own arrow is left out.
        private static void DrawIcons(Rect rect, List<Texture2D> marks) {
            var padding = SummaryStyle.padding;
            var size    = rect.height - 4;
            var arrow   = new GUIContent(Arrow);
            var gap     = ArrowStyle.CalcSize(arrow).x;
            var right   = rect.xMax - padding.right;
            var x       = rect.x + padding.left;
            for (var i = 0; i < marks.Count; i++) {
                if (i > 0) {
                    if (x + gap + size > right) break;

                    ArrowStyle.Draw(new Rect(x, rect.y, gap, rect.height), arrow, false, false, false, false);
                    x += gap;
                }

                GUI.DrawTexture(new Rect(x, rect.y + 2, size, size), marks[i], ScaleMode.ScaleToFit);
                x += size;
            }
        }

        private static void Open(Rect rect, AdsSettings settings, Func<AdPriorityList> list, AdFormat? format) {
            var serving = settings.Mediations.Where(mediation => format is not { } served || served.IsServedBy(mediation)).Select(mediation => mediation.Id);
            // A mediation on the list the page no longer has still shows, to be turned off.
            var choices = list().EditableIds.Union(serving).Select(id => new SearchChoice<string>(AdMediationNames.Of(settings, id), id));
            ChoiceSearchWindow.OpenOrder(rect, string.Empty, choices, () => list().EditableIds, order => {
                Undo.RecordObject(settings, "Change a format's mediations");
                var ids = list().EditableIds;
                ids.Clear();
                ids.AddRange(order);
                EditorUtility.SetDirty(settings);
                InternalEditorUtility.RepaintAllViews();
            });
        }

        private static GUIContent Summary(AdsSettings settings, List<string> ids) {
            if (ids.Count == 0) return new GUIContent("Off", "No mediation serves this format, so the game never shows it. Click to turn one on.");

            var names = string.Join(Arrow, ids.Select(id => AdMediationNames.Of(settings, id)));
            return new GUIContent(names, $"Tried left to right: {names}. Click to turn mediations on or off, or drag the ones on into another order.");
        }

        // The format whose list this is: the key of the format's entry, a few levels up.
        private static AdFormat? Format(InspectorProperty property) {
            for (var parent = property.Parent; parent != null; parent = parent.Parent) {
                if (parent.Children["key"]?.ValueEntry?.WeakSmartValue is AdFormat format) return format;
            }

            return null;
        }
    }
}