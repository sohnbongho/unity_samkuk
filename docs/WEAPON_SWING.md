# 무기 휘두르기 (Step 10-9)

베기 계열 무기가 "데미지 영역 표시"가 아니라 **무기를 휘두르는 동작**으로 보이게 하는 연출. 용어는 `CONTEXT.md` "전투"(휘두르기, 호 잔상).

## 무엇이 바뀌었나

| 항목 | 전 | 후 |
|---|---|---|
| 방향 | 좌/우를 번갈아 | **범위 안 가장 가까운 적 쪽**. 적이 없으면 좌/우 번갈아 |
| 보이는 것 | 원판 한 장을 범위 크기로 늘려 0.18초 | 무기 그림이 손 축(발에서 0.55유닛 위)을 중심으로 호를 그리며 돌고, 피해 원 자리에서 **반원 검기**가 앞으로 날아가며 사라진다(`SlashArcSprite`, 코드로 만든 그림) |
| 몸 방향 | 이동 방향만 | 휘두르는 동안 **휘두르는 쪽을 본다**(`ILookOverride`: 주인공 `PlayerAnimator`, 아군 `AllyController`). 달아나면서 등 뒤로 베는 어색함 방지 |
| 피해 시점 | 쿨다운이 차는 순간 | 호의 **40% 지점**(`SwingMotion.HitFraction`). 들어 올리는 동작이 보인 뒤 맞는다 |
| 판정 | 원(중심 range×0.5, 반지름 range×0.6) | 같음. 중심이 휘두르는 방향으로 옮겨졌을 뿐 |
| count ≥ 2 | 반대편도 동시에 | 같음(최대 2번, `SlashWeapon.MaxSwings`) |

해당 무기: 검 베기, 쌍고검, 청룡언월도, 참마도, 쌍룡자웅검, 청룡참월도(`WeaponType.Slash` 전부). 아군(동행 장수)도 같은 클래스라 자동으로 적용된다. 찌르기(`ThrustWeapon`)는 다음 Step.

## 구조

- `SwingMotion`(순수 규칙): 시작 각도·호 각도·시간을 받아 `Angle`(지금 무기가 가리키는 각도), `Advance(dt)`(타격 시점을 지나면 한 번 true), `FacesLeft`(왼쪽이면 그림 반전)를 준다. 느리게 시작해 빠르게 지나 느리게 끝나는 완화를 쓴다. 오른쪽을 향하든 왼쪽을 향하든 **위에서 아래로** 내려친다.
- `SlashWeapon`: 쿨다운이 차면 `EnemyManager.FindNearest` 로 방향을 정해 휘두르기를 시작하고, 매 틱 `SwingMotion` 을 밀어 타격 틱에 `Strike`(원 판정 + 넉백 + 잔상). 무기 그림은 `Held0/1` 자식 `SpriteRenderer`(Effect 정렬 레이어, 잔상보다 앞)이며 손 위치에서 `Angle - 90` 도로 돈다(그림은 날이 위를 향하므로). 그림이 없어도 타이밍/잔상은 같다. 오브젝트가 꺼지면(아군 쓰러짐) 동작을 끊고 숨긴다.
- `WeaponData`: `heldSprite`(들고 휘두르는 그림), `swingArcDegrees`(0 이면 120), `swingDuration`(0 이면 0.25). 0 이면 기본값이므로 기존 에셋도 그대로 동작한다. `duration` 은 검기가 보이는 시간(최소 0.22초).
- 검기(`SlashArcSprite`): 64x64 반원 띠(가운데 두껍고 끝이 얇음, 바깥 가장자리 밝음)를 실행 중에 한 번 만들어 공유한다. 반지름을 피해 원 반지름에 맞추고, 손 높이의 피해 원 자리에서 앞으로 range×0.35 만큼 나가며(빠르게 나가다 느려짐) 사라진다. 휘두르기마다 자기 검기 렌더러가 있어 반대편 베기도 따로 보인다. `WeaponData.sprite`(예전 원판)는 베기에서 더 쓰지 않는다.
- 바라보기(`ILookOverride.Look(방향, 초)`): 휘두르기 시작에 첫 번째(적 쪽) 휘두르기 방향으로 요청한다. 주인공은 이동 입력보다 우선하되 걷기 프레임은 계속 돌고(뒤로 걸으면서 적 쪽을 보는 모양), 휘두른 뒤 `SlashWeapon.LookHoldSeconds`(0.15초) 더 보다가 이동 방향으로 돌아간다. 걷기 시트가 없는 장수(기본 스프라이트 + 좌우 반전)는 영향 없음.

## 그림 규격

- 파일: `Assets/Sprites/Weapons/<무기 에셋 이름>_Held.png` (예: `Weapon_Sword_Held.png`).
- 도트 규격 PPU 32, 투명 배경, **손잡이가 아래·날이 위**, 피벗은 손잡이 끝(아래 가운데). 임포터(`HeldWeaponImporter`)가 자동으로 맞춘다(Sprite/Single, Point 필터, 압축 없음).
- 크기 기준: 검 12x26(약 0.8유닛), 쌍고검 12x24, 언월도 16x40(약 1.25유닛), 참마도 16x44, 쌍룡자웅검 12x26, 청룡참월도 18x44.
- 현재 6장은 **코드로 그린 임시 그림**(`tools/hero_art/WeaponSprites.cs`, `powershell -File tools\hero_art\generate.ps1 -Only weapon`, 미리보기 `%TEMP%\weapon_held_preview.png`). 직접 그린 그림으로 덮어쓰면 `-Only weapon` 을 실행하지 않는다.

## 에디터에서 할 일

1. 메뉴 `Samkuk > Step 10-9 - Link Held Weapon Sprites`(Run All 에 포함): 베기 무기의 빈 `heldSprite` 에 그림을 연결하고, 휘두르기 각도/시간이 0 인 무기에 무기별 값을 채운다(검 120°/0.25초, 쌍고검 100°/0.18초, 청룡언월도 150°/0.32초, 참마도 160°/0.35초, 쌍룡자웅검 110°/0.20초, 청룡참월도 170°/0.32초). 이미 값이 있으면 덮어쓰지 않는다.
2. Play: 유비/관우로 시작해 무기가 손에서 호를 그리며 돌고, 적이 위/아래에 있어도 그쪽으로 휘두르는지, 잔상이 피해 자리에 뜨는지.
3. Test Runner: `WeaponSwingTests`, `CombatTests`, `WeaponExpansionTests`, `AllyTests`, `BalanceTests`.

## 조정

- 느낌: `Weapon_*.asset` 의 `swingArcDegrees`/`swingDuration`(휘두름), `duration`(검기 시간). 타격 지점 비율은 `SwingMotion.HitFraction`, 손 높이 `SlashWeapon.HandHeight`, 검기 비거리 `SlashWeapon.TrailTravel`, 검기 모양은 `SlashArcSprite` 상수(두께 7px, 반각 75도).
- 밸런스: 판정이 적 쪽을 향해 실제 명중이 조금 늘 수 있다. `BalanceModel` 의 베기 가정(동시 2명)은 그대로이며 `BalanceTests` 가 범위를 벗어나면 베기 피해를 약간 내린다.

## 후속 후보

- 찌르기(`ThrustWeapon`) 같은 규격으로.
- 위로 벨 때 무기를 몸 뒤로(월드 정렬) 보내기, 쉬는 동안 무기를 들고 있는 자세.
- 장수별 공격 동작 시트(진짜 그림 교체 때).
