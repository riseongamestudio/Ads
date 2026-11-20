using System;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// A native ad in a rect the game places, a native MRec or an in-feed cell, which the game switches on and off; the pack fills it with an ad while it is on, and rotates it.<br/>
    /// Off, it holds no ad another placement could use.<br/>
    /// A handle made while the format is off holds no cell and shows nothing, so the game uses it the same way either way.<br/>
    /// A cell the canShow it was made with turns down, such as the manager's remove-ads, stays off until its next show, or the manager's <see cref="AdsManagerBase.RefreshAdsRemoved"/>, finds it allowed again.
    /// </summary>
    public sealed class NativeRectHandle : IDisposable {
        private readonly Func<bool> canShow;

        // Whether the game has the cell on, and its onDisplayed until that comes.
        private bool   wanted;
        private bool   heldBack;
        private Action onDisplayed;

        private Action<NativeRectHandle> disposed;
        private INativeRect              cell;

        internal NativeRectHandle(INativeRect cell, Func<bool> canShow, Action<NativeRectHandle> disposed = null) {
            this.cell     = cell;
            this.canShow  = canShow;
            this.disposed = disposed;
        }

        public bool HasCell => cell != null;

        /// <summary>Where the cell's top-left corner goes, in pixels from the screen's top-left corner.</summary>
        public void SetPosition(Vector2Int positionPx) {
            cell?.SetPosition(positionPx);
        }

        /// <summary>Switches the cell on; <paramref name="onDisplayed"/> comes once an ad is on screen in it, which may be after an ad arrives.</summary>
        public void Show(Action onDisplayed = null) {
            if (cell == null) return;

            wanted           = true;
            this.onDisplayed = onDisplayed;
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

        public void Dispose() {
            cell?.Dispose();
            cell        = null;
            wanted      = false;
            heldBack    = false;
            onDisplayed = null;

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