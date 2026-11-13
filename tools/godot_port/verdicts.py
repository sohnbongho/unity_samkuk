# Godot 전환(ADR 0004): "확인 필요" 로 보류해 둔 테스트의 사유를 판정 결과로 바꾼다 (한 번 쓰는 도구).
# 판정 방법은 godot/PORTING.md "보류 테스트 판정": 유니티의 Mono 로 실행(mono_check.sh) + 테스트가 쓰인 커밋의 코드로 실행(old_snapshot.sh).
import glob, io, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
NL = chr(10)
PURE = '원본에서도 실패: 엔진과 무관한 계산이고, 유니티의 Mono 로 돌려도, 이 테스트가 쓰인 커밋의 코드로 돌려도 같은 결과다. 테스트 기대를 고칠 것 (godot/PORTING.md 보류 테스트 판정)'
SCENE = '원본에서도 실패로 판단: 이 테스트가 쓰인 커밋의 코드로도, 유니티의 Mono 로도 실패한다. 프레임 순서를 흉내 낸 장면 모형 위의 결과라 유니티 에디터에서 한 번 돌려 보면 확정된다 (godot/PORTING.md 보류 테스트 판정)'
SCENE_TESTS = {
    'Slash_HeldSprite_ShowsWhileSwinging_AndHidesAfter', 'Thrust_HeldSpear_MovesForwardAlongTheThrust_ThenHides',
    'Halberd_HeldSprite_LungesWhenThrusting_AndTurnsWhenSweeping', 'Halberd_AlternatesThrustAndSweep', 'Thrust_KnocksEnemiesAway',
    'Wader_InWater_MakesRipplesAndOneSplash_ThenStopsOnLand', 'Spawner_NoLights_WhenLightingDisabled', 'Spawner_ReusedPoolObjects_ResetRotationAndScale',
}


def main():
    changed = 0
    for path in sorted(glob.glob(os.path.join(ROOT, 'godot', 'tests', '*.cs'))):
        lines = io.open(path, encoding='utf-8').read().split(NL)
        for i, line in enumerate(lines):
            if '[Ignore("확인 필요' not in line:
                continue
            name = None
            for j in range(i + 1, min(i + 5, len(lines))):
                m = re.match(r'^\s+public (?:void|IEnumerator) (\w+)[(]', lines[j])
                if m:
                    name = m.group(1)
                    break
            reason = SCENE if name in SCENE_TESTS else PURE
            lines[i] = line[:line.index('[Ignore(')] + '[Ignore("%s")]' % reason
            changed += 1
        io.open(path, 'w', encoding='utf-8', newline=NL).write(NL.join(lines))
    print('사유를 바꾼 테스트 %d' % changed)


if __name__ == '__main__':
    main()
