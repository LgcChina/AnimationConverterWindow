#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class AnimationConverterWindow : EditorWindow
{
    private AnimationClip sourceClip;
    private GameObject targetAvatar;
    private Vector2 scrollPos;
    private string logMessage = "";
    private bool isConverting = false;
    private float progress = 0f;

    // 转换配置
    [SerializeField] private bool simplifyConversion = true;
    [SerializeField] private int frameSkip = 2;
    [SerializeField] private bool useCustomSuffix = false;
    [SerializeField] private string customSuffix = "Armature";

    // 缓存数据
    private Dictionary<string, HumanBodyBones> boneNameMapping;
    private Dictionary<HumanBodyBones, Transform> targetBonesCache;
    private Dictionary<string, string> bonePathCache;
    private Dictionary<Transform, TransformData> originalTransforms = new Dictionary<Transform, TransformData>();

    // 骨骼名称预设
    private Dictionary<string, HumanBodyBones> BoneNameMapping
    {
        get
        {
            if (boneNameMapping == null)
            {
                boneNameMapping = new Dictionary<string, HumanBodyBones>
                {
                    {"Hips", HumanBodyBones.Hips},
                    {"LeftUpperLeg", HumanBodyBones.LeftUpperLeg},
                    {"RightUpperLeg", HumanBodyBones.RightUpperLeg},
                    {"LeftLowerLeg", HumanBodyBones.LeftLowerLeg},
                    {"RightLowerLeg", HumanBodyBones.RightLowerLeg},
                    {"LeftFoot", HumanBodyBones.LeftFoot},
                    {"RightFoot", HumanBodyBones.RightFoot},
                    {"Spine", HumanBodyBones.Spine},
                    {"Chest", HumanBodyBones.Chest},
                    {"Neck", HumanBodyBones.Neck},
                    {"Head", HumanBodyBones.Head},
                    {"LeftShoulder", HumanBodyBones.LeftShoulder},
                    {"RightShoulder", HumanBodyBones.RightShoulder},
                    {"LeftUpperArm", HumanBodyBones.LeftUpperArm},
                    {"RightUpperArm", HumanBodyBones.RightUpperArm},
                    {"LeftLowerArm", HumanBodyBones.LeftLowerArm},
                    {"RightLowerArm", HumanBodyBones.RightLowerArm},
                    {"LeftHand", HumanBodyBones.LeftHand},
                    {"RightHand", HumanBodyBones.RightHand},
                    {"LeftToes", HumanBodyBones.LeftToes},
                    {"RightToes", HumanBodyBones.RightToes},
                    {"LeftThumbProximal", HumanBodyBones.LeftThumbProximal},
                    {"LeftThumbIntermediate", HumanBodyBones.LeftThumbIntermediate},
                    {"LeftThumbDistal", HumanBodyBones.LeftThumbDistal},
                    {"LeftIndexProximal", HumanBodyBones.LeftIndexProximal},
                    {"LeftIndexIntermediate", HumanBodyBones.LeftIndexIntermediate},
                    {"LeftIndexDistal", HumanBodyBones.LeftIndexDistal},
                    {"LeftMiddleProximal", HumanBodyBones.LeftMiddleProximal},
                    {"LeftMiddleIntermediate", HumanBodyBones.LeftMiddleIntermediate},
                    {"LeftMiddleDistal", HumanBodyBones.LeftMiddleDistal},
                    {"LeftRingProximal", HumanBodyBones.LeftRingProximal},
                    {"LeftRingIntermediate", HumanBodyBones.LeftRingIntermediate},
                    {"LeftRingDistal", HumanBodyBones.LeftRingDistal},
                    {"LeftLittleProximal", HumanBodyBones.LeftLittleProximal},
                    {"LeftLittleIntermediate", HumanBodyBones.LeftLittleIntermediate},
                    {"LeftLittleDistal", HumanBodyBones.LeftLittleDistal},
                    {"RightThumbProximal", HumanBodyBones.RightThumbProximal},
                    {"RightThumbIntermediate", HumanBodyBones.RightThumbIntermediate},
                    {"RightThumbDistal", HumanBodyBones.RightThumbDistal},
                    {"RightIndexProximal", HumanBodyBones.RightIndexProximal},
                    {"RightIndexIntermediate", HumanBodyBones.RightIndexIntermediate},
                    {"RightIndexDistal", HumanBodyBones.RightIndexDistal},
                    {"RightMiddleProximal", HumanBodyBones.RightMiddleProximal},
                    {"RightMiddleIntermediate", HumanBodyBones.RightMiddleIntermediate},
                    {"RightMiddleDistal", HumanBodyBones.RightMiddleDistal},
                    {"RightRingProximal", HumanBodyBones.RightRingProximal},
                    {"RightRingIntermediate", HumanBodyBones.RightRingIntermediate},
                    {"RightRingDistal", HumanBodyBones.RightRingDistal},
                    {"RightLittleProximal", HumanBodyBones.RightLittleProximal},
                    {"RightLittleIntermediate", HumanBodyBones.RightLittleIntermediate},
                    {"RightLittleDistal", HumanBodyBones.RightLittleDistal}
                };
            }
            return boneNameMapping;
        }
    }

    [MenuItem("LGC/动画转换器")]
    public static void ShowWindow()
    {
        GetWindow<AnimationConverterWindow>("骨骼动画转物体动画工具");
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // 工具标题
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("骨骼动画转物体动画工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 动画选择区域
        EditorGUILayout.LabelField("源骨骼动画", EditorStyles.boldLabel);
        var newSourceClip = (AnimationClip)EditorGUILayout.ObjectField("动画文件", sourceClip, typeof(AnimationClip), false);
        if (newSourceClip != sourceClip)
        {
            sourceClip = newSourceClip;
            ClearCaches();
        }

        // 目标人物选择区域
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("目标人物", EditorStyles.boldLabel);
        var newTargetAvatar = (GameObject)EditorGUILayout.ObjectField("目标模型", targetAvatar, typeof(GameObject), true);
        if (newTargetAvatar != targetAvatar)
        {
            targetAvatar = newTargetAvatar;
            ClearCaches();
        }

        // 转换选项
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("转换选项", EditorStyles.boldLabel);

        // 精简选项
        simplifyConversion = EditorGUILayout.Toggle("精简转换（提速）", simplifyConversion);
        if (simplifyConversion)
        {
            frameSkip = EditorGUILayout.IntSlider("跳过帧数", frameSkip, 1, 5);
            EditorGUILayout.HelpBox($"将跳过 {frameSkip} 帧中的 {frameSkip - 1} 帧，大幅提高转换速度但可能丢失细节", MessageType.Info);
        }

        // 路径后缀选项
        useCustomSuffix = EditorGUILayout.Toggle("自定义骨骼路径", useCustomSuffix);
        if (useCustomSuffix)
        {
            customSuffix = EditorGUILayout.TextField("路径名称", customSuffix);
            EditorGUILayout.HelpBox($"当前路径将变为{customSuffix}/Hips/Spine...", MessageType.Info);
        }

        // 转换按钮
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUI.enabled = !isConverting && sourceClip != null && targetAvatar != null;
        if (GUILayout.Button("转换为物体动画", GUILayout.Width(150), GUILayout.Height(30)))
        {
            StartConversion();
        }
        GUI.enabled = true;

        if (isConverting)
        {
            if (GUILayout.Button("取消转换", GUILayout.Width(120), GUILayout.Height(30)))
            {
                StopConversion();
            }
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 进度条
        if (isConverting)
        {
            EditorGUILayout.Space();
            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, progress, $"转换中... {progress * 100:F1}%");
            Repaint();
        }

        // 日志信息
        if (!string.IsNullOrEmpty(logMessage))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(logMessage, MessageType.Info);
        }

        // 使用说明
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("实验性工具，谨慎使用", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("使用说明", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("此工具专用于将人形骨骼动画(Humanoid)转换为通用物体动画(Generic)\n" +
                                "转换后的动画将包含所有骨骼的变换数据", MessageType.Info);

        EditorGUILayout.LabelField("1. 选择源骨骼动画文件", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("   - 必须是配置了人形骨骼的动画", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   - 动画类型应为Humanoid", EditorStyles.miniLabel);

        EditorGUILayout.LabelField("2. 选择目标人物模型", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("   - 模型需要包含Animator组件", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   - 建议使用与源动画匹配的模型", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   - 模型应正确配置Avatar", EditorStyles.miniLabel);

        EditorGUILayout.LabelField("3. 配置转换选项", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("   - 精简转换: 大幅提高转换速度，但可能丢失细节", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   - 自定义骨骼路径: 修改动画中骨骼的根路径", EditorStyles.miniLabel);

        EditorGUILayout.LabelField("4. 点击转换按钮", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("   - 转换过程可能需要一些时间，取决于动画长度", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   - 转换过程中可以随时取消", EditorStyles.miniLabel);

        EditorGUILayout.LabelField("5. 保存生成的动画", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("   - 转换完成后会自动弹出保存对话框", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   - 建议使用有意义的文件名以便识别", EditorStyles.miniLabel);

        EditorGUILayout.LabelField("注意事项", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("   - 转换过程不会修改原始模型的任何状态", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   - 如果目标模型骨骼结构与源动画不匹配，可能出现异常", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("   - 自定义路径功能用于适配不同模型的骨骼层级结构", EditorStyles.miniLabel);

        EditorGUILayout.EndScrollView();
    }

    private struct TransformData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public TransformData(Transform t)
        {
            position = t.localPosition;
            rotation = t.localRotation;
            scale = t.localScale;
        }

        public void ApplyTo(Transform t)
        {
            t.localPosition = position;
            t.localRotation = rotation;
            t.localScale = scale;
        }
    }

    private void ClearCaches()
    {
        targetBonesCache = null;
        bonePathCache = null;
        originalTransforms.Clear();
    }

    private void StartConversion()
    {
        if (sourceClip == null)
        {
            logMessage = "错误：请选择源动画文件";
            return;
        }

        if (targetAvatar == null)
        {
            logMessage = "错误：请选择目标人物";
            return;
        }

        logMessage = "开始转换：骨骼动画 → 物体动画";
        isConverting = true;
        progress = 0f;

        // 保存原始变换状态
        SaveOriginalTransforms();

        // 开始异步转换
        EditorCoroutineUtility.StartCoroutine(ConvertToGenericAnimationProcess(), this);
    }

    private void SaveOriginalTransforms()
    {
        originalTransforms.Clear();
        foreach (Transform bone in GetAllBones())
        {
            originalTransforms[bone] = new TransformData(bone);
        }
    }

    private void RestoreOriginalTransforms()
    {
        foreach (var kvp in originalTransforms)
        {
            if (kvp.Key != null)
            {
                kvp.Value.ApplyTo(kvp.Key);
            }
        }
    }

    private void StopConversion()
    {
        isConverting = false;
        logMessage = "转换已取消";
        RestoreOriginalTransforms();
    }

    // 获取所有骨骼变换
    private List<Transform> GetAllBones()
    {
        List<Transform> bones = new List<Transform>();
        Animator animator = targetAvatar.GetComponent<Animator>();

        if (animator != null && animator.isHuman)
        {
            foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                Transform boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform != null && !bones.Contains(boneTransform))
                {
                    bones.Add(boneTransform);
                }
            }
        }
        else
        {
            // 尝试根据预设名称查找骨骼
            foreach (var mapping in BoneNameMapping)
            {
                Transform boneTransform = FindDeepChild(targetAvatar.transform, mapping.Key);
                if (boneTransform != null && !bones.Contains(boneTransform))
                {
                    bones.Add(boneTransform);
                }
            }
        }

        // 如果没有找到特定骨骼，添加所有子孙变换
        if (bones.Count == 0)
        {
            bones.AddRange(targetAvatar.GetComponentsInChildren<Transform>());
        }

        return bones;
    }

    // 骨骼动画转物体动画
    private IEnumerator ConvertToGenericAnimationProcess()
    {
        try
        {
            // 获取目标人物的骨骼映射
            var targetBones = GetTargetBones();
            if (targetBones.Count == 0)
            {
                logMessage = "错误：无法获取目标人物的有效骨骼";
                isConverting = false;
                yield break;
            }

            // 创建新的动画剪辑
            AnimationClip newClip = new AnimationClip();
            newClip.name = $"{sourceClip.name}_{targetAvatar.name}_Generic";
            newClip.legacy = false;

            // 获取动画时间参数
            float length = sourceClip.length;
            float frameRate = sourceClip.frameRate;
            int totalFrames = Mathf.CeilToInt(length * frameRate);
            int step = simplifyConversion ? frameSkip : 1;

            // 创建临时动画控制器
            AnimatorController controller = new AnimatorController();
            controller.AddLayer("Base");
            AnimatorState state = controller.layers[0].stateMachine.AddState("ConvertState");
            state.motion = sourceClip;

            // 配置目标Animator
            Animator animator = targetAvatar.GetComponent<Animator>();
            if (animator == null)
            {
                logMessage = "错误：目标人物缺少Animator组件";
                isConverting = false;
                yield break;
            }

            RuntimeAnimatorController originalController = animator.runtimeAnimatorController;
            Avatar originalAvatar = animator.avatar;
            bool originalEnabled = targetAvatar.activeSelf;

            animator.runtimeAnimatorController = controller;
            animator.avatar = originalAvatar;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            targetAvatar.SetActive(true);

            // 强制更新一帧确保Animator正确初始化
            animator.Update(0.01f);
            yield return null;

            // 初始化动画采样
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();

            // 预计算所有骨骼路径
            var bonePaths = new Dictionary<HumanBodyBones, string>();
            foreach (var bonePair in targetBones)
            {
                string path = GetBonePath(bonePair.Value, targetAvatar.transform);

                // 应用自定义后缀
                if (useCustomSuffix && !string.IsNullOrEmpty(customSuffix))
                {
                    // 移除原有的Armature部分，添加自定义后缀
                    if (path.StartsWith("Armature/"))
                    {
                        path = path.Replace("Armature/", $"{customSuffix}/");
                    }
                    else if (!path.Contains("/"))
                    {
                        path = $"{customSuffix}/{path}";
                    }
                }

                bonePaths[bonePair.Key] = path;
            }

            // 创建临时对象存储关键帧数据
            List<KeyframeData> keyframeData = new List<KeyframeData>();

            // 采样每一帧
            for (int frame = 0; frame <= totalFrames; frame += step)
            {
                if (!isConverting) break;

                float time = Mathf.Clamp(frame / frameRate, 0, length);
                progress = (float)frame / totalFrames;

                // 强制更新Animator
                animator.Update(0.01f);
                yield return null;

                // 采样当前帧
                AnimationMode.SampleAnimationClip(targetAvatar, sourceClip, time);

                // 记录所有骨骼变换
                foreach (var bonePair in targetBones)
                {
                    Transform bone = bonePair.Value;
                    string path = bonePaths[bonePair.Key];

                    // 存储关键帧数据（不直接添加到clip）
                    keyframeData.Add(new KeyframeData
                    {
                        Path = path,
                        Time = time,
                        Position = bone.localPosition,
                        Rotation = bone.localRotation,
                        Scale = bone.localScale
                    });
                }

                // 每10帧更新一次UI
                if (frame % (step * 10) == 0)
                {
                    yield return null;
                }
            }

            // 结束采样
            AnimationMode.EndSampling();
            AnimationMode.StopAnimationMode();

            // 恢复原始状态
            animator.runtimeAnimatorController = originalController;
            animator.avatar = originalAvatar;
            targetAvatar.SetActive(originalEnabled);
            RestoreOriginalTransforms();

            // 保存生成的动画
            if (isConverting)
            {
                // 添加关键帧到新动画
                AddKeyframesToClip(newClip, keyframeData);

                SaveConvertedAnimation(newClip);
                logMessage = $"转换完成! 已保存为: {newClip.name}.anim";
            }
        }
        finally
        {
            // 确保原始状态恢复
            RestoreOriginalTransforms();
            isConverting = false;
            progress = 0f;
        }
    }

    private struct KeyframeData
    {
        public string Path;
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    private void AddKeyframesToClip(AnimationClip clip, List<KeyframeData> keyframeData)
    {
        // 按路径分组关键帧
        var groupedData = keyframeData.GroupBy(k => k.Path);

        foreach (var group in groupedData)
        {
            string path = group.Key;
            var positionX = new AnimationCurve();
            var positionY = new AnimationCurve();
            var positionZ = new AnimationCurve();
            var rotationX = new AnimationCurve();
            var rotationY = new AnimationCurve();
            var rotationZ = new AnimationCurve();
            var rotationW = new AnimationCurve();
            var scaleX = new AnimationCurve();
            var scaleY = new AnimationCurve();
            var scaleZ = new AnimationCurve();

            foreach (var data in group)
            {
                // 位置
                positionX.AddKey(data.Time, data.Position.x);
                positionY.AddKey(data.Time, data.Position.y);
                positionZ.AddKey(data.Time, data.Position.z);

                // 旋转
                rotationX.AddKey(data.Time, data.Rotation.x);
                rotationY.AddKey(data.Time, data.Rotation.y);
                rotationZ.AddKey(data.Time, data.Rotation.z);
                rotationW.AddKey(data.Time, data.Rotation.w);

                // 缩放
                scaleX.AddKey(data.Time, data.Scale.x);
                scaleY.AddKey(data.Time, data.Scale.y);
                scaleZ.AddKey(data.Time, data.Scale.z);
            }

            // 设置曲线
            SetCurve(clip, path, "m_LocalPosition.x", positionX);
            SetCurve(clip, path, "m_LocalPosition.y", positionY);
            SetCurve(clip, path, "m_LocalPosition.z", positionZ);

            SetCurve(clip, path, "m_LocalRotation.x", rotationX);
            SetCurve(clip, path, "m_LocalRotation.y", rotationY);
            SetCurve(clip, path, "m_LocalRotation.z", rotationZ);
            SetCurve(clip, path, "m_LocalRotation.w", rotationW);

            SetCurve(clip, path, "m_LocalScale.x", scaleX);
            SetCurve(clip, path, "m_LocalScale.y", scaleY);
            SetCurve(clip, path, "m_LocalScale.z", scaleZ);
        }
    }

    private void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
    {
        if (curve.keys.Length == 0) return;

        AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding
        {
            path = path,
            type = typeof(Transform),
            propertyName = property
        }, curve);
    }

    private Dictionary<HumanBodyBones, Transform> GetTargetBones()
    {
        if (targetBonesCache != null) return targetBonesCache;

        targetBonesCache = new Dictionary<HumanBodyBones, Transform>();

        if (targetAvatar == null) return targetBonesCache;

        Animator animator = targetAvatar.GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            // 尝试使用预设骨骼名称映射
            return GetBonesByNameMapping();
        }

        // 获取所有人形骨骼
        foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (bone == HumanBodyBones.LastBone) continue;

            Transform boneTransform = animator.GetBoneTransform(bone);
            if (boneTransform != null)
            {
                targetBonesCache[bone] = boneTransform;
            }
        }

        return targetBonesCache;
    }

    private Dictionary<HumanBodyBones, Transform> GetBonesByNameMapping()
    {
        Dictionary<HumanBodyBones, Transform> bones = new Dictionary<HumanBodyBones, Transform>();

        if (targetAvatar == null) return bones;

        // 尝试根据预设名称查找骨骼
        foreach (var mapping in BoneNameMapping)
        {
            Transform boneTransform = FindDeepChild(targetAvatar.transform, mapping.Key);
            if (boneTransform != null)
            {
                bones[mapping.Value] = boneTransform;
            }
        }

        return bones;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }

        return null;
    }

    private string GetBonePath(Transform bone, Transform root)
    {
        if (bone == null || root == null) return "";
        if (bone == root) return "";

        List<string> path = new List<string>();
        Transform current = bone;

        while (current != null && current != root)
        {
            path.Add(current.name);
            current = current.parent;
        }

        path.Reverse();
        return string.Join("/", path);
    }

    private void SaveConvertedAnimation(AnimationClip clip)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "保存转换的动画",
            clip.name,
            "anim",
            "请选择保存位置");

        if (string.IsNullOrEmpty(path)) return;

        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 选中新创建的动画文件
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }
}
#endif
