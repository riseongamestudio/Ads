using System;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// A native ad in a rect the game places, a native MRec or an in-feed cell, which the game switches on and off; the pack fills it with an ad while it is on, and rotates it.<br/>
    /// Off, it holds no ad another placement could use.<br/>
    /// A handle made before a mediation serving the format started holds no cell yet and shows nothing; it gets its cell once one has, with the position and the on or off the game set meanwhile, so the game uses it the same way either way. With the format off, that never comes.<br/>
    /// While the format is suppressed (<see cref="AdsSdk.SetSuppressed"/>) the cell stays off, and comes back once that is lifted.
    /// </summary>
    public sealed class NativeRectHandle : IDisposable {
        private readonly Func<bool> canShow;

        // Whether the game has the cell on, and its onDisplayed until that comes.
        private bool   wanted;
        private bool   heldBack;
        private Action onDisplayed;

        // Where the game placed it, for a cell that comes later.
        private Vector2Int? positionPx;

        private Action<NativeRectHandle> disposed;
        private INativeRect              cell;

        internal NativeRectHandle(AdFormat format, Vector2Int sizePx, int roundCornerPx, Func<bool> canShow, Action<NativeRectHandle> disposed = null) {
            Format        = format;
            SizePx        = sizePx;
            RoundCornerPx = roundCornerPx;
            this.canShow  = canShow;
            this.disposed = disposed;
        }

        // What the cell is made with once a mediation serving the format started.
        internal AdFormat Format { get; }
        internal Vector2Int SizePx { get; }
        internal int RoundCornerPx { get; }
        internal bool IsDisposed { get; private set; }

        /// <summary>A mediation's cell backs the handle; false until one serving the format started, and for good while the format is off.</summary>
        public bool HasCell => cell != null;

        /// <summary>Where the cell's top-left corner goes, in pixels from the screen's top-left corner.</summary>
        public void SetPosition(Vector2Int positionPx) {
            this.positionPx = positionPx;
            cell?.SetPosition(positionPx);
        }

        /// <summary>Switches the cell on; <paramref name="onDisplayed"/> comes once an ad is on screen in it, which may be after an ad, or the cell itself, arrives.</summary>
        public void Show(Action onDisplayed = null) {
            if (IsDisposed) return;

            wanted           = true;
            this.onDisplayed = onDisplayed;
            if (cell == null) return;

            if (canShow?.Invoke() == false) {
                heldBack = true;
                return;
            }

            heldBack = false;
            ShowCell();
        }

        public void Hide() {
            wanted      = false;
            heldBack    = false;
            onDisplayed = null;
            cell?.Hide();
        }

        /// <summary>After canShow changes: hides the cell it now holds back, or brings back the one it held.</summary>
        internal void Refresh() {
            if (cell == null || !wanted) return;

            if (canShow?.Invoke() == false) {
                if (heldBack) return;

                heldBack = true;
                cell.Hide();
                return;
            }

            if (!heldBack) return;

            heldBack = false;
            ShowCell();
        }

        // The cell a mediation made once it started, set up as the game left the handle.
        internal void Bind(INativeRect cell) {
            this.cell = cell;
            if (positionPx is { } position) cell.SetPosition(position);
            if (!wanted) return;

            if (canShow?.Invoke() == false) {
                heldBack = true;
                return;
            }

            ShowCell();
        }

        public void Dispose() {
            cell?.Dispose();
            cell        = null;
            wanted      = false;
            heldBack    = false;
            onDisplayed = null;
            IsDisposed  = true;

            var callback = disposed;
            disposed = null;
            callback?.Invoke(this);
        }

        // The cell forgets a pending onDisplayed on hide, so each show hands it one that reads the game's from here.
        private void ShowCell() {
            cell.Show(() => {
                var callback = onDisplayed;
                onDisplayed = null;
                callback?.Invoke();
            });
        }
    }
}