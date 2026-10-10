using UnityEngine;
using UnityEditor;

// Ligne du LevelCatalog : titre = nom de la scene (+ nom affiche s'il existe), scenePath en lecture seule
[CustomPropertyDrawer(typeof(LevelCatalogEntry))]
public class LevelCatalogEntryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, GetTitle(property), true);
        if (!property.isExpanded) return;

        EditorGUI.indentLevel++;
        float y = line.yMax + EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        bool enterChildren = true;
        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            enterChildren = false;
            float h = EditorGUI.GetPropertyHeight(child, true);
            using (new EditorGUI.DisabledScope(child.name == "scenePath"))
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), child, true);
            y += h + EditorGUIUtility.standardVerticalSpacing;
        }

        EditorGUI.indentLevel--;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded) return height;

        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        bool enterChildren = true;
        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            enterChildren = false;
            height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
        }
        return height;
    }

    private string GetTitle(SerializedProperty property)
    {
        SerializedProperty scene = property.FindPropertyRelative("scene");
        SerializedProperty levelName = property.FindPropertyRelative("levelName");

        string title = scene != null && scene.objectReferenceValue != null ? scene.objectReferenceValue.name : "(aucune scene)";
        if (levelName != null && !string.IsNullOrEmpty(levelName.stringValue))
            title += "   —   " + levelName.stringValue;
        return title;
    }
}
