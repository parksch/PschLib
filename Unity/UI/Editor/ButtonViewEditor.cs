using TMPro;
using PschLib.Unity.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PschLib.Unity.UI.Editor
{
    [CustomEditor(typeof(ButtonView))]
    public sealed class ButtonViewEditor : UnityEditor.Editor
    {
        private SerializedProperty labelType;
        private SerializedProperty text;
        private SerializedProperty tmpText;
        private SerializedProperty labelState;
        private SerializedProperty interactableTextColor;
        private SerializedProperty nonInteractableTextColor;
        private SerializedProperty objectMode;
        private SerializedProperty stateObject;
        private string labelSearchMessage;
        private MessageType labelSearchMessageType;

        private void OnEnable()
        {
            labelType = serializedObject.FindProperty("labelType");
            text = serializedObject.FindProperty("text");
            tmpText = serializedObject.FindProperty("tmpText");
            labelState = serializedObject.FindProperty("labelState");
            interactableTextColor = serializedObject.FindProperty("interactableTextColor");
            nonInteractableTextColor = serializedObject.FindProperty("nonInteractableTextColor");
            objectMode = serializedObject.FindProperty("objectMode");
            stateObject = serializedObject.FindProperty("stateObject");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Label", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(labelType, new GUIContent("Label Type"));

            if (EditorGUI.EndChangeCheck())
            {
                labelSearchMessage = null;
            }

            var selectedLabelType = (ButtonViewLabelType)labelType.enumValueIndex;
            var hasLabel = false;

            if (selectedLabelType == ButtonViewLabelType.Text)
            {
                EditorGUILayout.PropertyField(text, new GUIContent("Text"));
            }
            else if (selectedLabelType == ButtonViewLabelType.TextMeshPro)
            {
                EditorGUILayout.PropertyField(tmpText, new GUIContent("TMP Text"));
            }

            if (selectedLabelType != ButtonViewLabelType.None)
            {
                if (GUILayout.Button("Find Child Label"))
                {
                    FindChildLabel((ButtonView)target, selectedLabelType);
                }

                if (!string.IsNullOrEmpty(labelSearchMessage))
                {
                    EditorGUILayout.HelpBox(labelSearchMessage, labelSearchMessageType);
                }

                hasLabel = selectedLabelType == ButtonViewLabelType.Text
                    ? text.objectReferenceValue != null
                    : tmpText.objectReferenceValue != null;
            }

            if (hasLabel)
            {
                EditorGUILayout.PropertyField(labelState, new GUIContent("Interactable Label State"));

                var selectedLabelState = (ButtonViewLabelState)labelState.enumValueIndex;

                if (selectedLabelState == ButtonViewLabelState.ChangeColor)
                {
                    EditorGUILayout.PropertyField(interactableTextColor, new GUIContent("Interactable Color"));
                    EditorGUILayout.PropertyField(nonInteractableTextColor, new GUIContent("Non-Interactable Color"));
                }
                else if (selectedLabelState == ButtonViewLabelState.HideWhenNonInteractable)
                {
                    var labelComponent = selectedLabelType == ButtonViewLabelType.Text
                        ? text.objectReferenceValue as Component
                        : tmpText.objectReferenceValue as Component;

                    if (labelComponent != null &&
                        ((ButtonView)target).transform.IsChildOf(labelComponent.transform))
                    {
                        EditorGUILayout.HelpBox("The label must be on a child object. Hiding the button GameObject or an ancestor would disable ButtonView too.", MessageType.Warning);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Object State", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(objectMode, new GUIContent("Interactable Type"));

            var mode = (ButtonViewObjectMode)objectMode.enumValueIndex;

            if (mode != ButtonViewObjectMode.None)
            {
                var label = mode == ButtonViewObjectMode.HideObject ? "Object To Hide" : "Lock Object";
                EditorGUILayout.PropertyField(stateObject, new GUIContent(label));

                var selectedObject = stateObject.objectReferenceValue as GameObject;

                if (selectedObject != null && ((ButtonView)target).transform.IsChildOf(selectedObject.transform))
                {
                    EditorGUILayout.HelpBox("Choose a child or another object. The button cannot hide itself or an ancestor and still observe its state.", MessageType.Warning);
                }
            }

            var view = (ButtonView)target;

            if (view.GetComponentInChildren<Graphic>(true) == null)
            {
                EditorGUILayout.HelpBox("Add a raycastable UI Graphic, such as an Image, for pointer clicks.", MessageType.Info);
            }

            if (serializedObject.ApplyModifiedProperties() && Application.isPlaying)
            {
                view.Refresh();
            }
        }

        private void FindChildLabel(ButtonView view, ButtonViewLabelType type)
        {
            int count;

            if (type == ButtonViewLabelType.Text)
            {
                var found = FindUniqueChild<Text>(view, out count);

                if (count == 1)
                {
                    text.objectReferenceValue = found;
                }
            }
            else
            {
                var found = FindUniqueChild<TMP_Text>(view, out count);

                if (count == 1)
                {
                    tmpText.objectReferenceValue = found;
                }
            }

            labelSearchMessageType = count == 1 ? MessageType.Info : MessageType.Warning;
            labelSearchMessage = count == 1
                ? "Child label assigned."
                : count == 0
                    ? "No child label of the selected type was found."
                    : $"Found {count} child labels. Assign one manually.";
        }

        private static T FindUniqueChild<T>(ButtonView view, out int count) where T : Component
        {
            T found = null;
            count = 0;

            foreach (var candidate in view.GetComponentsInChildren<T>(true))
            {
                if (candidate.transform == view.transform)
                {
                    continue;
                }

                count++;
                found = candidate;
            }

            return count == 1 ? found : null;
        }
    }
}
