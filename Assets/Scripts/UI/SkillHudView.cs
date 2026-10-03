using Samkuk.Skills;
using UnityEngine;
using UnityEngine.UI;

namespace Samkuk.UI
{
    /// <summary>스킬 이름, 단축키, 쿨다운(채움 + 남은 초)을 표시한다.</summary>
    public class SkillHudView : MonoBehaviour
    {
        [SerializeField] SkillController skills;
        [SerializeField] Text label;
        [SerializeField, Tooltip("아래에서 위로 줄어드는 쿨다운 가림막 (피벗 아래쪽)")] RectTransform cooldownMask;
        [SerializeField] GameObject root;

        public SkillController Skills { get => skills; set => skills = value; }

        void Update()
        {
            if (skills == null) return;

            bool has = skills.HasSkill;
            if (root != null && root.activeSelf != has) root.SetActive(has);
            if (!has) return;

            if (cooldownMask != null)
                cooldownMask.localScale = new Vector3(1f, skills.CooldownRatio, 1f);

            if (label != null)
            {
                string name = skills.Skill.displayName;
                label.text = skills.IsReady
                    ? $"[Space]  {name}"
                    : $"{name}  {Mathf.CeilToInt(skills.CooldownLeft)}s";
            }
        }
    }
}
