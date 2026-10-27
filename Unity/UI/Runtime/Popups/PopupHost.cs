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
        private readonly List<Type> destroyedPopupTypes = new();
        private readonly List<PopupBase> survivingPopups = new();
        private bool isProcessingOperation;

        public int Count
        {
            get
            {
                RefreshDestroyedPopups();
                return popupStack.Count;
            }
        }

        public PopupBase Current
        {
            get
            {
                RefreshDestroyedPopups();
                return popupStack.Count > 0 ? popupStack.Peek() : null;
            }
        }

        public event Action BackRequestedWhenEmpty;

        private Transform PopupRoot => popupRoot != null ? popupRoot : transform;

        private void Awake()
        {
            RegisterChildPopups();
        }

        private void LateUpdate()
        {
            RefreshDestroyedPopups();
        }

        public bool TryGet<T>(out T popup) where T : PopupBase
        {
            RefreshDestroyedPopups();
            if (registeredPopups.TryGetValue(typeof(T), out PopupBase registeredPopup) && registeredPopup != null)
            {
                popup = (T)registeredPopup;
                return true;
            }

            popup = null;
            return false;
        }

        public T Open<T>() where T : PopupBase
        {
            if (!TryBeginOperation())
            {
                return null;
            }
            try
            {
                return OpenCore<T>();
            }
            finally
            {
                isProcessingOperation = false;
            }
        }

        private T OpenCore<T>() where T : PopupBase
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

            try
            {
                popup.SetVisibleInternal(true);
                if (popup == null)
                {
                    RemoveDestroyedPopups();
                    return null;
                }
                popup.transform.SetAsLastSibling();
                popup.OpenInternal();
            }
            catch (Exception exception)
            {
                if (popup != null)
                {
                    popup.SetVisibleInternal(false);
                }
                Debug.LogException(exception, popup);
                ApplyStackVisibility();
                return null;
            }

            if (popup == null)
            {
                RemoveDestroyedPopups();
                return null;
            }

            popupStack.Push(popup);
            ApplyStackVisibility();
            return popup;
        }

        public bool CloseTop()
        {
            return Close(Current);
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
            if (!TryBeginOperation())
            {
                return false;
            }
            try
            {
                return CloseCore(popup);
            }
            finally
            {
                isProcessingOperation = false;
            }
        }

        private bool CloseCore(PopupBase popup)
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
                if (popup != null)
                {
                    popup.SetVisibleInternal(false);
                }
                ApplyStackVisibility();
            }

            return true;
        }

        public bool RefreshTop()
        {
            if (!TryBeginOperation())
            {
                return false;
            }
            try
            {
                return RefreshTopCore();
            }
            finally
            {
                isProcessingOperation = false;
            }
        }

        private bool RefreshTopCore()
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
                CloseCore(popup);
                return false;
            }
        }

        public bool Register(PopupBase popup)
        {
            if (!TryBeginOperation())
            {
                return false;
            }
            try
            {
                return RegisterCore(popup);
            }
            finally
            {
                isProcessingOperation = false;
            }
        }

        private bool RegisterCore(PopupBase popup)
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
            if (!TryBeginOperation())
            {
                return false;
            }
            try
            {
                return UnregisterCore(popup);
            }
            finally
            {
                isProcessingOperation = false;
            }
        }

        private bool UnregisterCore(PopupBase popup)
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

            if (popup.IsOpen && !CloseCore(popup))
            {
                return false;
            }

            registeredPopups.Remove(popupType);
            popup.SetVisibleInternal(false);
            return true;
        }

        private bool TryBeginOperation()
        {
            if (isProcessingOperation)
            {
                Debug.LogWarning("PopupHost operations cannot be called from popup lifecycle or visibility callbacks.", this);
                return false;
            }

            isProcessingOperation = true;
            try
            {
                RemoveDestroyedPopups();
            }
            catch
            {
                isProcessingOperation = false;
                throw;
            }
            return true;
        }

        private void RefreshDestroyedPopups()
        {
            if (isProcessingOperation)
            {
                return;
            }

            isProcessingOperation = true;
            try
            {
                RemoveDestroyedPopups();
            }
            finally
            {
                isProcessingOperation = false;
            }
        }

        private void RemoveDestroyedPopups()
        {
            destroyedPopupTypes.Clear();
            foreach (var pair in registeredPopups)
            {
                if (pair.Value == null)
                {
                    destroyedPopupTypes.Add(pair.Key);
                }
            }
            foreach (var type in destroyedPopupTypes)
            {
                registeredPopups.Remove(type);
            }

            survivingPopups.Clear();
            bool removed = false;
            foreach (var popup in popupStack)
            {
                if (popup == null)
                {
                    removed = true;
                }
                else
                {
                    survivingPopups.Add(popup);
                }
            }

            if (removed)
            {
                popupStack.Clear();
                for (int i = survivingPopups.Count - 1; i >= 0; i--)
                {
                    popupStack.Push(survivingPopups[i]);
                }
                ApplyStackVisibility();
            }
            survivingPopups.Clear();
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
                if (popup == null)
                {
                    continue;
                }

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
