using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PschLib.Unity.UI
{
    public enum ButtonViewLabelType
    {
        None,
        Text,
        TextMeshPro
    }

    public enum ButtonViewObjectMode
    {
        None,
        HideObject,
        LockObject
    }

    public enum ButtonViewLabelState
    {
        None,
        ChangeColor,
        HideWhenNonInteractable
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ButtonView : MonoBehaviour
    {
        [SerializeField] private ButtonViewLabelType labelType;
        [SerializeField] private Text text;
        [SerializeField] private TMP_Text tmpText;
        [SerializeField] private ButtonViewLabelState labelState;
        [SerializeField] private Color interactableTextColor = Color.white;
        [SerializeField] private Color nonInteractableTextColor = Color.gray;
        [SerializeField] private ButtonViewObjectMode objectMode;
        [SerializeField] private GameObject stateObject;

        private Button button;
        private bool appliedInteractable;
        private Text coloredText;
        private TMP_Text coloredTmpText;
        private Color originalTextColor;
        private Color originalTmpTextColor;
        private GameObject controlledLabelObject;
        private bool originalLabelActive;
        private GameObject controlledObject;
        private bool originalObjectActive;

        public Button Button
        {
            get
            {
                if (button == null)
                {
                    button = GetComponent<Button>();
                }

                return button;
            }
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            RestoreTextColor();
            RestoreLabelState();
            RestoreObjectState();
        }

        private void Update()
        {
            if (Button.interactable != appliedInteractable)
            {
                Refresh();
            }
        }

        public void SetText(string value)
        {
            if (labelType == ButtonViewLabelType.Text && text != null)
            {
                text.text = value;
            }

            if (labelType == ButtonViewLabelType.TextMeshPro && tmpText != null)
            {
                tmpText.text = value;
            }
        }

        public void SetInteractable(bool value)
        {
            Button.interactable = value;
            Refresh();
        }

        public void Refresh()
        {
            var interactable = Button.interactable;
            appliedInteractable = interactable;
            var selectedText = labelType == ButtonViewLabelType.Text ? text : null;
            var selectedTmpText = labelType == ButtonViewLabelType.TextMeshPro ? tmpText : null;

            if (labelState == ButtonViewLabelState.ChangeColor &&
                (selectedText != null || selectedTmpText != null))
            {
                if (coloredText != selectedText || coloredTmpText != selectedTmpText)
                {
                    RestoreTextColor();
                    coloredText = selectedText;
                    coloredTmpText = selectedTmpText;

                    if (coloredText != null)
                    {
                        originalTextColor = coloredText.color;
                    }

                    if (coloredTmpText != null)
                    {
                        originalTmpTextColor = coloredTmpText.color;
                    }
                }

                var color = interactable ? interactableTextColor : nonInteractableTextColor;

                if (coloredText != null)
                {
                    coloredText.color = color;
                }

                if (coloredTmpText != null)
                {
                    coloredTmpText.color = color;
                }
            }
            else
            {
                RestoreTextColor();
            }

            var labelObject = selectedText != null ? selectedText.gameObject :
                selectedTmpText != null ? selectedTmpText.gameObject : null;
            var hideLabel = labelState == ButtonViewLabelState.HideWhenNonInteractable &&
                labelObject != null && !transform.IsChildOf(labelObject.transform);

            if (hideLabel)
            {
                if (controlledLabelObject != labelObject)
                {
                    RestoreLabelState();
                    controlledLabelObject = labelObject;
                    originalLabelActive = labelObject.activeSelf;
                }

                controlledLabelObject.SetActive(interactable);
            }
            else
            {
                RestoreLabelState();
            }

            var validObject = objectMode != ButtonViewObjectMode.None &&
                stateObject != null && !transform.IsChildOf(stateObject.transform);

            if (!validObject)
            {
                RestoreObjectState();
                return;
            }

            if (controlledObject != stateObject)
            {
                RestoreObjectState();
                controlledObject = stateObject;
                originalObjectActive = stateObject.activeSelf;
            }

            var active = objectMode == ButtonViewObjectMode.HideObject
                ? interactable
                : !interactable;
            controlledObject.SetActive(active);
        }

        private void RestoreTextColor()
        {
            if (coloredText != null)
            {
                coloredText.color = originalTextColor;
            }

            if (coloredTmpText != null)
            {
                coloredTmpText.color = originalTmpTextColor;
            }

            coloredText = null;
            coloredTmpText = null;
        }

        private void RestoreObjectState()
        {
            if (controlledObject != null)
            {
                controlledObject.SetActive(originalObjectActive);
            }

            controlledObject = null;
        }

        private void RestoreLabelState()
        {
            if (controlledLabelObject != null)
            {
                controlledLabelObject.SetActive(originalLabelActive);
            }

            controlledLabelObject = null;
        }
    }
}
