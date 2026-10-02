using System;
using System.Collections.Generic;
using UnityEngine;

namespace PschLib.Unity.UI
{
    [DisallowMultipleComponent]
    public sealed class PopupHost : MonoBehaviour
    {
        [SerializeField]
        private RectTransform popupRoot;

        private readonly Dictionary<Type, PopupBase> registeredPopups = new();
        private readonly Stack<PopupBase> popupStack = new();

        public int Count => popupStack.Count;

        public PopupBase Current => popupStack.Count > 0 ? popupStack.Peek() : null;

        public event Action BackRequestedWhenEmpty;

        private Transform PopupRoot => popupRoot != null ? popupRoot : transform;

        private void Awake()
        {
            RegisterChildPopups();
        }

        public bool TryGet<T>(out T popup) where T : PopupBase
        {
            if (registeredPopups.TryGetValue(typeof(T), out PopupBase registeredPopup))
            {
                popup = (T)registeredPopup;
                return true;
            }

            popup = null;
            return false;
        }

        public T Open<T>() where T : PopupBase
        {
            if (!TryGet(out T popup))
            {
                Debug.LogError($"Popup is not registered: {typeof(T).Name}", this);
                return null;
            }

            if (popup.IsOpen)
            {
                Debug.LogWarning($"Popup is already open: {typeof(T).Name}", popup);
                return null;
            }

            popup.SetVisibleInternal(true);
            popup.transform.SetAsLastSibling();

            try
            {
                popup.OpenInternal();
            }
            catch (Exception exception)
            {
                popup.SetVisibleInternal(false);
                Debug.LogException(exception, popup);
                ApplyStackVisibility();
                return null;
            }

            popupStack.Push(popup);
            ApplyStackVisibility();
            return popup;
        }

        public bool CloseTop()
        {
            if (popupStack.Count == 0)
            {
                return false;
            }

            return Close(popupStack.Peek());
        }

        public bool HandleBack()
        {
            if (Current != null)
            {
                return CloseTop();
            }

            Action handler = BackRequestedWhenEmpty;
            if (handler == null)
            {
                return false;
            }

            handler.Invoke();
            return true;
        }

        public bool Close<T>() where T : PopupBase
        {
            return TryGet(out T popup) && Close(popup);
        }

        public bool Close(PopupBase popup)
        {
            if (popup == null)
            {
                return false;
            }

            if (Current != popup)
            {
                Debug.LogWarning($"Only the top popup can be closed: {popup.GetType().Name}", popup);
                return false;
            }

            popupStack.Pop();

            try
            {
                popup.CloseInternal();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, popup);
            }
            finally
            {
                popup.SetVisibleInternal(false);
                ApplyStackVisibility();
            }

            return true;
        }

        public bool RefreshTop()
        {
            PopupBase popup = Current;
            if (popup == null)
            {
                return false;
            }

            try
            {
                popup.RefreshInternal();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, popup);
                Close(popup);
                return false;
            }
        }

        public bool Register(PopupBase popup)
        {
            if (popup == null)
            {
                Debug.LogError("Cannot register a null popup.", this);
                return false;
            }

            if (popup.transform.parent != PopupRoot)
            {
                Debug.LogError(
                    $"Popup must be a direct child of '{PopupRoot.name}': {popup.name}",
                    popup);
                return false;
            }

            Type popupType = popup.GetType();
            if (registeredPopups.TryGetValue(popupType, out PopupBase registeredPopup))
            {
                if (registeredPopup == popup)
                {
                    return true;
                }

                Debug.LogError($"Popup type is already registered: {popupType.Name}", popup);
                popup.SetVisibleInternal(false);
                return false;
            }

            registeredPopups.Add(popupType, popup);
            popup.SetVisibleInternal(false);
            return true;
        }

        public bool Unregister(PopupBase popup)
        {
            if (popup == null)
            {
                return false;
            }

            Type popupType = popup.GetType();
            if (!registeredPopups.TryGetValue(popupType, out PopupBase registeredPopup) ||
                registeredPopup != popup)
            {
                return false;
            }

            if (popup.IsOpen && !Close(popup))
            {
                return false;
            }

            registeredPopups.Remove(popupType);
            popup.SetVisibleInternal(false);
            return true;
        }

        private void RegisterChildPopups()
        {
            registeredPopups.Clear();
            popupStack.Clear();

            Transform root = PopupRoot;
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).TryGetComponent(out PopupBase popup))
                {
                    Register(popup);
                }
            }
        }

        private void ApplyStackVisibility()
        {
            bool keepPreviousVisible = true;

            foreach (PopupBase popup in popupStack)
            {
                bool visible = keepPreviousVisible;

                popup.SetVisibleInternal(visible);

                if (visible && popup.DisplayMode == PopupDisplayMode.ReplacePrevious)
                {
                    keepPreviousVisible = false;
                }
            }
        }
    }
}
