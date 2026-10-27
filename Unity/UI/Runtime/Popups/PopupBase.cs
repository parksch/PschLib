using UnityEngine;

namespace PschLib.Unity.UI
{
    public abstract class PopupBase : MonoBehaviour
    {
        [SerializeField]
        private PopupDisplayMode displayMode = PopupDisplayMode.ReplacePrevious;

        public PopupDisplayMode DisplayMode => displayMode;

        public bool IsInitialized { get; private set; }

        public bool IsOpen { get; private set; }

        public bool IsVisible => this != null && gameObject.activeSelf;

        internal void SetVisibleInternal(bool visible)
        {
            if (this == null || gameObject.activeSelf == visible)
            {
                return;
            }

            gameObject.SetActive(visible);
        }

        internal void InitializeInternal()
        {
            if (IsInitialized)
            {
                return;
            }

            IsInitialized = true;

            try
            {
                OnInitialize();
            }
            catch
            {
                IsInitialized = false;
                SetVisibleInternal(false);
                throw;
            }
        }

        internal void OpenInternal()
        {
            if (IsOpen)
            {
                return;
            }

            InitializeInternal();
            if (this == null)
            {
                return;
            }

            IsOpen = true;

            try
            {
                OnOpen();
            }
            catch
            {
                IsOpen = false;
                SetVisibleInternal(false);
                throw;
            }
        }

        internal void RefreshInternal()
        {
            if (!IsOpen)
            {
                return;
            }

            OnRefresh();
        }

        internal void CloseInternal()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            OnClose();
        }

        protected virtual void OnInitialize()
        {
        }

        protected virtual void OnOpen()
        {
        }

        protected virtual void OnRefresh()
        {
        }

        protected virtual void OnClose()
        {
        }
    }
}
