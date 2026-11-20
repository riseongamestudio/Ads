using System;
using System.Collections.Generic;
using System.Linq;
using RiseOn.Utils.Editor.SearchWindow;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using RiseOn.Utils.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// A format's mediations as a row of chips, best first, an arrow between each two side by side, wrapping onto more lines as text does when the row is too narrow; chips that move to another line slide there.<br/>
    /// A chip dragged takes the slot nearest to it while the others slide aside, x removes it, + adds one of the page's mediations the format lacks.
    /// </summary>
    internal sealed class AdPriorityListDrawer : OdinValueDrawer<AdPriorityList> {
        private const float Gap   = 4;
        // The arrow from a chip to the next: it leaves the chip's edge and stops just short of the next one.
        private const float Shaft   = 10;
        private const float Head    = 3.5f;
        private const float Short   = 3;
        // As heavy as the page's icons draw at their size.
        private const float Stroke  = 1.4f;
        private const float Between = Shaft + Short;
        private const float Cut   = 16;
        // Added to the mini button's own padding, so a name sits as far from the chip's sides as from its top and bottom.
        private const float Pad   = 1.5f;
        private const float Speed = 18;

        // Where each chip is drawn, from the row's top left, easing toward its slot; keyed by id, unique in a row.
        // From the row's corner, so a row that moves, as under sliding tabs, carries its chips with it.
        private readonly Dictionary<string, Vector2> drawn = new();
        private double lastRepaint;

        // The row's width at the last repaint: every event of the next frame breaks the lines by it.
        private float width;

        // Whether the row has had a repaint in this window yet, and whether the next frame sets the chips in their slots with no slide:
        // until the first repaint the row breaks its lines by the width it had last, in any window, and a window opening at another
        // width shows them in their places at once.
        private bool shown;
        private bool settle;

        // By property, the row's width at its last repaint in any window.
        private static readonly Dictionary<string, float> lastWidths = new();

        private int     dragged = -1;
        private Vector2 grab;
        private Vector2 mouse;

        // The window the row draws in, for what has to show without waiting for the next input: each step of a slide, and a change
        // picked in a drop-down after the window drew.
        private EditorWindow window;

        private static float LineStep => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        protected override void DrawPropertyLayout(GUIContent label) {
            window = GUIHelper.CurrentWindow;
            var settings = Property.Tree.WeakTargets.OfType<AdsSettings>().FirstOrDefault();
            var ids      = ValueEntry.SmartValue.EditableIds;
            if (!shown && width <= 0 && lastWidths.TryGetValue(Property.Path, out var last)) width = last;
            if (settle && Event.current.type is EventType.Layout) {
                drawn.Clear();
                settle = false;
            }

            var control  = GUIUtility.GetControlID(FocusType.Passive);
            // The + offers only the mediations that serve the row's format: the key of the format's entry, a few levels up.
            var format   = Format(Property);
            var missing  = settings ? settings.Mediations.Where(mediation => format is not { } served || served.IsServedBy(mediation)).Select(mediation => mediation.Id).Except(ids).ToList() : new List<string>();
            var off      = new GUIContent("Off", "No mediation serves this format, so the game never shows it.");

            var names = ids.Select(id => new GUIContent(AdMediationNames.Of(settings, id))).ToList();
            var chip  = ChipWidth(settings, names);
            // A row narrower than a chip narrows its chips to it, their names cut short, rather than letting them out of the section.
            if (width > 0) chip = Mathf.Max(Mathf.Min(chip, width), Cut * 2);
            if (dragged >= ids.Count) dragged = -1;

            // The lines as the last repaint's width breaks them, each as full as it fits, an arrow's room between two chips; the + keeps
            // its place while a chip is dragged, so nothing moves under it.
            var perLine = width > 0 ? Mathf.Max(1, Mathf.FloorToInt((width + Between) / (chip + Between))) : Mathf.Max(1, ids.Count);
            var tail    = Tail(ids.Count, perLine, chip, ids.Count == 0 ? EditorStyles.miniLabel.CalcSize(off).x : 0, missing.Count > 0);
            var rect    = EditorGUILayout.GetControlRect(label != null, tail.line * LineStep + EditorGUIUtility.singleLineHeight);
            var first   = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            var value   = label == null ? first : EditorGUI.PrefixLabel(first, label);

            var row    = Rect.MinMaxRect(value.x, rect.y, value.xMax, rect.yMax);
            if (Event.current.type is EventType.Repaint) {
                if (!Mathf.Approximately(width, row.width)) {
                    settle = !shown;
                    width  = lastWidths[Property.Path] = row.width;
                    Repaint();
                }

                shown = true;
            }

            // Under the mouse, inside the row.
            var held = mouse - grab - row.position;
            held = new Vector2(Mathf.Clamp(held.x, 0, Mathf.Max(0, row.width - chip)), Mathf.Clamp(held.y, 0, row.height - EditorGUIUtility.singleLineHeight));

            var order = Order(ids.Count, perLine, chip, held);
            Ease(ids, order, perLine, chip);
            if (dragged >= 0) drawn[ids[dragged]] = held;

            // Between two slots on one line, not the chips, so they stay put while chips slide and a dragged chip passes over them.
            if (Event.current.type is EventType.Repaint) {
                for (var slot = 1; slot < ids.Count; slot++) {
                    if (slot % perLine == 0) continue;

                    var at = row.position + Slot(slot, perLine, chip);
                    Arrow(at.x - Between, at.y + EditorGUIUtility.singleLineHeight / 2);
                }
            }

            // The dragged chip goes last, over the others.
            foreach (var i in order.Where(i => i != dragged).Append(dragged).Where(i => i >= 0)) {
                var at   = row.position + drawn[ids[i]];
                var body = new Rect(at.x, at.y, chip - Cut, EditorGUIUtility.singleLineHeight);
                var cut  = new Rect(body.xMax, at.y, Cut, EditorGUIUtility.singleLineHeight);

                if (Event.current.type is EventType.Repaint) {
                    EditorStyles.miniButtonLeft.Draw(body, names[i], false, false, i == dragged, false);
                    EditorStyles.miniButtonRight.Draw(cut, GUIContent.none, false, false, i == dragged, false);
                }

                // The page's one icon size, as on the bars and rows.
                using (new EditorGUI.DisabledScope(dragged >= 0)) {
                    if (Section.IconButton(Cell(cut), SdfIconType.X, "Remove the mediation from this format")) {
                        var index = i;
                        Change(list => list.RemoveAt(index));
                        GUIUtility.ExitGUI();
                    }
                }
                if (Event.current.type is EventType.MouseDown && Event.current.button == 0 && body.Contains(Event.current.mousePosition)) {
                    dragged = i;
                    mouse   = Event.current.mousePosition;
                    grab    = mouse - at;
                    GUIUtility.hotControl = control;
                    Event.current.Use();
                }
            }

            Drag(control, ids, order);

            var end = row.position + tail.at;
            if (ids.Count == 0) {
                var size = EditorStyles.miniLabel.CalcSize(off).x;
                // GUI's label, not EditorGUI's, which would push it right by an indented row's indent and out of its rect.
                GUI.Label(new Rect(end.x, end.y, size, EditorGUIUtility.singleLineHeight), off, EditorStyles.miniLabel);
                end.x += size + Gap;
            }

            if (missing.Count == 0 || dragged >= 0) return;

            var add = new Rect(end.x, end.y, Section.ButtonWidth, EditorGUIUtility.singleLineHeight);
            if (Section.IconButton(add, SdfIconType.Plus, "Add a mediation")) {
                ChoiceSearchWindow.Open(add, "", missing.Select(id => new SearchChoice<string>(AdMediationNames.Of(settings, id), id)), id => Change(list => list.Add(id)), search: false);
            }
        }

        // Where the chip in a slot goes, from the row's top left: perLine to a line, an arrow's room apart.
        private static Vector2 Slot(int slot, int perLine, float chip) {
            return new Vector2(slot % perLine * (chip + Between), slot / perLine * LineStep);
        }

        // Where what follows the chips starts, Off when there are none, and the line the + ends on: after the last chip, else on the
        // next line when the row has no room left for it.
        private (Vector2 at, int line) Tail(int count, int perLine, float chip, float offWidth, bool adds) {
            var at = count == 0 ? Vector2.zero : Slot(count - 1, perLine, chip) + new Vector2(chip + Gap, 0);
            var needs = offWidth + (offWidth > 0 ? Gap : 0) + (adds ? Section.ButtonWidth : 0);
            if (count > 0 && needs > 0 && width > 0 && at.x + needs > width) at = new Vector2(0, at.y + LineStep);
            return (at, Mathf.RoundToInt(at.y / LineStep));
        }

        // Strokes with sharp edges, as the icons' are, in the label's color as theirs is; the head's point reaches Shaft from the chip.
        private static void Arrow(float from, float center) {
            // Through the middle of a pixel, as the icons' strokes are: one full pixel and part of each beside it.
            var y   = PixelUtils.SnapY(center - PixelUtils.Pixel / 2) + PixelUtils.Pixel / 2;
            var tip = new Vector2(from + Shaft - Stroke * Mathf.Sqrt(.5f), y);
            var was = Handles.color;
            Handles.color = EditorStyles.label.normal.textColor;
            Line(new Vector2(from, y), tip - new Vector2(Stroke, 0));
            Line(tip + new Vector2(-Head, -Head), tip);
            Line(tip + new Vector2(-Head, Head), tip);
            Handles.color = was;

            // Square at both ends: at the point the head's two strokes overlap into a corner.
            static void Line(Vector2 start, Vector2 end) {
                var along  = (end - start).normalized * Stroke / 2;
                var across = new Vector2(-along.y, along.x);
                start -= along;
                end   += along;
                Handles.DrawAAConvexPolygon(start + across, end + across, end - across, start - across);
            }
        }

        // The x's cell as it shows: between the line the left button draws on the pixel 2 points inside its right edge and the right
        // button's border, on the last pixel inside its own.
        private static Rect Cell(Rect cut) {
            var left  = PixelUtils.SnapX(cut.x - 2) + PixelUtils.Pixel;
            var right = PixelUtils.SnapX(cut.xMax) - PixelUtils.Pixel;
            return new Rect(left, cut.y, right - left, cut.height);
        }

        // One width for every chip on the page, the longest name's, so the rows line up in columns.
        private static float ChipWidth(AdsSettings settings, IEnumerable<GUIContent> names) {
            var all = settings ? settings.Mediations.Select(mediation => new GUIContent(AdMediationNames.Of(mediation.GetType()))) : Enumerable.Empty<GUIContent>();
            return all.Concat(names).Select(name => EditorStyles.miniButtonLeft.CalcSize(name).x).DefaultIfEmpty(0).Max() + Pad * 2 + Cut;
        }

        // The chips in the order shown: a dragged chip takes the slot nearest to where it is drawn, its own until it moves.
        private List<int> Order(int count, int perLine, float chip, Vector2 held) {
            var order = Enumerable.Range(0, count).ToList();
            if (dragged < 0) return order;

            var nearest = order.OrderBy(slot => (Slot(slot, perLine, chip) - held).sqrMagnitude).First();
            order.Remove(dragged);
            order.Insert(nearest, dragged);
            return order;
        }

        // Moves each chip toward its slot and keeps repainting until all arrive.
        private void Ease(List<string> ids, List<int> order, int perLine, float chip) {
            var now  = EditorApplication.timeSinceStartup;
            var step = Event.current.type is EventType.Repaint ? 1 - Mathf.Exp(-(float) (now - lastRepaint) * Speed) : 0;
            if (Event.current.type is EventType.Repaint) lastRepaint = now;

            var moving = false;
            for (var slot = 0; slot < order.Count; slot++) {
                var id     = ids[order[slot]];
                var target = Slot(slot, perLine, chip);
                if (!drawn.TryGetValue(id, out var at)) at = target;
                at = (target - at).sqrMagnitude < .25f ? target : Vector2.Lerp(at, target, step);
                drawn[id] = at;
                moving |= at != target;
            }

            if (moving) Repaint();
        }

        private void Drag(int control, List<string> ids, List<int> order) {
            if (GUIUtility.hotControl != control) return;

            switch (Event.current.type) {
                case EventType.MouseDrag:
                    mouse = Event.current.mousePosition;
                    Event.current.Use();
                    Repaint();
                    break;
                case EventType.MouseUp:
                    GUIUtility.hotControl = 0;
                    dragged = -1;
                    Event.current.Use();
                    // One undo step for the whole drag.
                    if (!order.SequenceEqual(Enumerable.Range(0, ids.Count))) {
                        var reordered = order.Select(i => ids[i]).ToList();
                        Change(list => {
                            list.Clear();
                            list.AddRange(reordered);
                        });
                    }

                    break;
            }
        }

        private void Change(Action<List<string>> edit) {
            Property.RecordForUndo("Change ad priorities");
            edit(ValueEntry.SmartValue.EditableIds);
            ValueEntry.Values.ForceMarkDirty();
            Repaint();
        }

        private static AdFormat? Format(InspectorProperty property) {
            for (var parent = property.Parent; parent != null; parent = parent.Parent) {
                if (parent.Children["key"]?.ValueEntry?.WeakSmartValue is AdFormat format) return format;
            }

            return null;
        }

        // Odin's GUIHelper.RequestRepaint repaints Odin's own windows only, not a settings page.
        private void Repaint() {
            if (window) window.Repaint();
        }
    }
}