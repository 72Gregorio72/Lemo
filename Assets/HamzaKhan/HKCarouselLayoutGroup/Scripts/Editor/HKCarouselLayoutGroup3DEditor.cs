using UnityEngine;
using UnityEditor;


namespace HKCarouselLayoutGroup
{
    [CustomEditor(typeof(HKCarouselLayoutGroup3D<>))]
    public class HKCarouselLayoutGroup3DEditor<T> : Editor where T : HKBaseCarouselData
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUIStyle textLargeStyle = new GUIStyle();

            textLargeStyle.fontSize = 20;
            textLargeStyle.wordWrap = true;
            textLargeStyle.alignment = TextAnchor.MiddleLeft;
            textLargeStyle.fontStyle = FontStyle.Bold;

            textLargeStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;

            HKCarouselLayoutGroup3D<T> clG = (HKCarouselLayoutGroup3D<T>)target;

            EditorGUILayout.Space(10);

            textLargeStyle.fontSize = 15;

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("Info", textLargeStyle);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Current Selected Index {clG.CurrentSelectedIndex}");
            EditorGUILayout.HelpBox("Current Selected Index is accessible from other scripts", MessageType.Info);
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("HK Carousel Layout Group 3D", textLargeStyle);
            EditorGUILayout.Space(10);
        }
    }
}