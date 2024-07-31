using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

[System.Serializable]
class BoneListJson
{
    public string[] Bones = new string[0];
}

[CustomEditor(typeof(SkinnedMeshRenderer))]
class AdvancedSkinnedMeshRendererEditor : SkinnedMeshRendererEditor
{
    // Styles
    internal static class Styles
    {
        public static GUIStyle lineStyle = "TV Line";

        public static GUIStyle indexLabel;

        static Styles()
        {
            indexLabel = new GUIStyle(lineStyle);
            indexLabel.alignment = TextAnchor.MiddleCenter;
            indexLabel.fontSize--;
        }
    }

    class BoneListView : TreeView
    {
        SkinnedMeshRenderer m_SkinnedMeshRenderer;

        public BoneListView(TreeViewState state, SkinnedMeshRenderer renderer)
            : base(state)
        {
            m_SkinnedMeshRenderer = renderer;
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem(-1, -1);
            root.children = new List<TreeViewItem>();

            var bones = m_SkinnedMeshRenderer.bones;

            for (var i = 0; i < bones.Length; ++i)
            {
                var boneItem = new TreeViewItem(i, 0, bones[i] != null ? bones[i].name : "None");
                root.children.Add(boneItem);
            }

            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (Event.current.rawType != EventType.Repaint)
            {
                return;
            }

            var indexRect = args.rowRect;
            indexRect.width = 30;

            Styles.indexLabel.Draw(indexRect, args.item.id.ToString(), false, false, args.selected, args.focused);

            var labelRect = args.rowRect;
            labelRect.xMin = indexRect.xMax + 4;

            Styles.lineStyle.Draw(labelRect, args.label, false, false, args.selected, args.focused);
        }
    }

    bool BoneListFoldoutState
    {
        get
        {
            return SessionState.GetBool(nameof(BoneListFoldoutState), false);
        }
        set
        {
            SessionState.SetBool(nameof(BoneListFoldoutState), value);
        }
    }

    bool DisplayBones
    {
        get
        {
            return SessionState.GetBool(nameof(DisplayBones), false);
        }
        set
        {
            SessionState.SetBool(nameof(DisplayBones), value);
        }
    }

    const float ButtonWidth = 60;

    TreeViewState m_BoneListState;
    BoneListView m_BoneList;
    SkinnedMeshRenderer m_SkinnedMeshRenderer;

    public override void OnEnable()
    {
        base.OnEnable();

        var targetArray = targets.Select(x => x as SkinnedMeshRenderer).ToArray();

        if (targetArray.Length == 1)
        {
            m_SkinnedMeshRenderer = targetArray[0];

            m_BoneListState = new TreeViewState();

            m_BoneList = new BoneListView(m_BoneListState, m_SkinnedMeshRenderer);
            m_BoneList.Reload();
        }
        else
        {
            m_SkinnedMeshRenderer = null;
            m_BoneList = null;
        }
    }

    static string GetBonePath(Transform rootBone, Transform bone)
    {
        var sb = new System.Text.StringBuilder(1000);

        while (bone != null && bone != rootBone)
        {
            if (0 < sb.Length)
            {
                sb.Insert(0, '/');
            }
            sb.Insert(0, bone.name);
            bone = bone.parent;
        }

        return sb.ToString();
    }

    void CopyBones()
    {
        var json = new BoneListJson();

        json.Bones = m_SkinnedMeshRenderer.bones.Select(x => x != null ? GetBonePath(m_SkinnedMeshRenderer.rootBone, x) : "").ToArray();

        GUIUtility.systemCopyBuffer = JsonUtility.ToJson(json, true);
    }

    void PasteBones()
    {
        var json = GetCopiedBoneList();

        var bones = new Transform[json.Bones.Length];

        for (var i = 0; i < json.Bones.Length; ++i)
        {
            bones[i] = string.IsNullOrEmpty(json.Bones[i]) ? m_SkinnedMeshRenderer.rootBone : m_SkinnedMeshRenderer.rootBone.Find(json.Bones[i]);
        }

        Undo.RecordObject(m_SkinnedMeshRenderer, "Paste Bones");

        m_SkinnedMeshRenderer.bones = bones;
    }

    BoneListJson GetCopiedBoneList()
    {
        var copyBuffer = GUIUtility.systemCopyBuffer;

        if (!string.IsNullOrEmpty(copyBuffer))
        {
            try
            {
                return JsonUtility.FromJson<BoneListJson>(copyBuffer);
            }
            catch (System.Exception)
            {
            }
        }

        return null;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (m_SkinnedMeshRenderer != null && m_BoneList != null)
        {
            EditorGUILayout.BeginHorizontal();
            BoneListFoldoutState = EditorGUILayout.BeginFoldoutHeaderGroup(BoneListFoldoutState, "Bones");
            GUILayout.Space(ButtonWidth * 2 + 2);
            EditorGUILayout.EndHorizontal();

            var headerRect = GUILayoutUtility.GetLastRect();

            var buttonRect = headerRect;
            buttonRect.xMin = buttonRect.xMax - ButtonWidth;

            EditorGUI.BeginDisabledGroup(m_SkinnedMeshRenderer.rootBone == null || GetCopiedBoneList() == null);

            if (GUI.Button(buttonRect, "Paste", EditorStyles.miniButton))
            {
                PasteBones();
            }

            EditorGUI.EndDisabledGroup();

            buttonRect.x -= buttonRect.width;

            EditorGUI.BeginDisabledGroup(m_SkinnedMeshRenderer.rootBone == null);

            if (GUI.Button(buttonRect, "Copy", EditorStyles.miniButton))
            {
                CopyBones();
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndFoldoutHeaderGroup();

            if (BoneListFoldoutState)
            {
                var rect = GUILayoutUtility.GetRect(1, 100);

                if (Event.current.type == EventType.Repaint)
                {
                    GUI.skin.box.Draw(rect, GUIContent.none, 0);
                }

                rect.xMin += 1;
                rect.yMin += 1;
                rect.xMax -= 1;
                rect.yMax -= 1;

                m_BoneList.OnGUI(rect);
            }

            EditorGUI.BeginChangeCheck();

            DisplayBones = EditorGUILayout.ToggleLeft("Display Bones", DisplayBones);

            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }
        }
    }

    new void OnSceneGUI()
    {
        base.OnSceneGUI();

        if (m_SkinnedMeshRenderer != null && DisplayBones && Event.current.type == EventType.Repaint)
        {
            var bones = m_SkinnedMeshRenderer.bones;

            Handles.color = Color.white;

            for (var i = 0; i < bones.Length; ++i)
            {
                if (bones[i] == m_SkinnedMeshRenderer.rootBone)
                {
                    continue;
                }

                Handles.DrawAAPolyLine(bones[i].position, bones[i].parent.position);
            }
        }
    }
}
