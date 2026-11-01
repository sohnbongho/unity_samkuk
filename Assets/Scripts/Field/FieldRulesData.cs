using UnityEngine;

namespace Samkuk.Field
{
    /// <summary>
    /// 야전 수치 에셋(15-5): <see cref="FieldRules"/>(병과 표·상성·장수 배율·적 AI 문턱)를 인스펙터에서 고칠 수 있게 감싼 것.
    /// 셋업 Step 15 가 <c>Assets/ScriptableObjects/Field/FieldRules.asset</c> 을 **없을 때만** 기본값으로 만들고 야전 씬의 컨트롤러에 연결한다.
    /// 컨트롤러가 야전을 시작할 때마다 이 객체를 모델에 넘기므로 고친 값은 다음 야전(결과창 [다시])부터 적용되고 에셋에 남는다.
    /// 기본값으로 되돌리려면 에셋을 지우고 Step 15 를 다시 돌린다. 설명은 docs/FIELD_BATTLE.md "병과 표".
    /// </summary>
    [CreateAssetMenu(menuName = "Samkuk/Field Rules", fileName = "FieldRules")]
    public class FieldRulesData : ScriptableObject
    {
        public FieldRules rules = new FieldRules();
    }
}
