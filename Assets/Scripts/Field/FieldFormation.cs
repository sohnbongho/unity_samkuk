using System.Collections.Generic;

namespace Samkuk.Field
{
    /// <summary>
    /// 배치: 야전 시작 때 부대가 놓이는 자리. 네 가지 배치는 앞줄·가운데·뒷줄에 어느 병과를 두는지가 다르고,
    /// 좌우 반전은 행을 뒤집는다 (row → 8 - row). 자리는 "앞줄에서 몇 열 뒤인가(0 앞, 1 가운데, 2 뒤)"와 행으로 적어 두고
    /// 편에 따라 열로 바꾼다 (내 편은 앞줄 열 2 에서 왼쪽으로, 적은 앞줄 열 9 에서 오른쪽으로). 병과마다 자리 3개가 있어 부대 수 0~3 을 받는다.
    /// </summary>
    public static class FieldFormation
    {
        public struct Slot
        {
            public UnitClass cls;
            public bool isGeneral;
            public int col, row;
        }

        struct Offset
        {
            public int depth, row;
            public Offset(int depth, int row) { this.depth = depth; this.row = row; }
        }

        // 각 배치의 병과별 자리 3개 (순서대로 채움) + 장수 자리. 행은 가운데(4)를 중심으로 위아래 대칭이 되게 짰다.
        static readonly Offset[] BoGiGungInf = { new Offset(0, 3), new Offset(0, 5), new Offset(0, 4) };
        static readonly Offset[] BoGiGungCav = { new Offset(1, 2), new Offset(1, 6), new Offset(1, 1) };
        static readonly Offset[] BoGiGungArc = { new Offset(2, 3), new Offset(2, 5), new Offset(2, 4) };
        static readonly Offset BoGiGungGen = new Offset(1, 4);

        static readonly Offset[] GiBoGungCav = { new Offset(0, 3), new Offset(0, 5), new Offset(0, 4) };
        static readonly Offset[] GiBoGungInf = { new Offset(1, 2), new Offset(1, 6), new Offset(1, 1) };
        static readonly Offset[] GiBoGungArc = { new Offset(2, 3), new Offset(2, 5), new Offset(2, 4) };
        static readonly Offset GiBoGungGen = new Offset(1, 4);

        static readonly Offset[] GungBoGiArc = { new Offset(0, 3), new Offset(0, 5), new Offset(0, 4) };
        static readonly Offset[] GungBoGiInf = { new Offset(1, 3), new Offset(1, 5), new Offset(1, 4) };
        static readonly Offset[] GungBoGiCav = { new Offset(2, 2), new Offset(2, 6), new Offset(2, 1) };
        static readonly Offset GungBoGiGen = new Offset(2, 4);

        static readonly Offset[] WingsInf = { new Offset(0, 3), new Offset(0, 5), new Offset(0, 4) };
        static readonly Offset[] WingsArc = { new Offset(1, 1), new Offset(1, 7), new Offset(1, 2) };
        static readonly Offset[] WingsCav = { new Offset(2, 3), new Offset(2, 5), new Offset(2, 4) };
        static readonly Offset WingsGen = new Offset(1, 4);

        static void Table(FormationKind kind, out Offset[] inf, out Offset[] arc, out Offset[] cav, out Offset gen)
        {
            switch (kind)
            {
                case FormationKind.GiBoGung: inf = GiBoGungInf; arc = GiBoGungArc; cav = GiBoGungCav; gen = GiBoGungGen; break;
                case FormationKind.GungBoGi: inf = GungBoGiInf; arc = GungBoGiArc; cav = GungBoGiCav; gen = GungBoGiGen; break;
                case FormationKind.Wings: inf = WingsInf; arc = WingsArc; cav = WingsCav; gen = WingsGen; break;
                default: inf = BoGiGungInf; arc = BoGiGungArc; cav = BoGiGungCav; gen = BoGiGungGen; break;
            }
        }

        /// <summary>앞줄에서 depth 열 뒤의 실제 열.</summary>
        public static int ColFor(FieldSide side, int depth) =>
            side == FieldSide.Player ? FieldRules.PlayerFrontCol - depth : FieldRules.EnemyFrontCol + depth;

        /// <summary>
        /// 배치를 실제 칸으로 펼친다. 장수 부대가 맨 앞에 오고, 그다음 보병·궁병·기병 순서. 자리가 겹치지 않도록 표를 짰으므로
        /// 결과 칸은 모두 다르다 (테스트가 확인).
        /// </summary>
        public static List<Slot> Layout(FormationKind kind, bool mirror, FieldSide side, int infantry, int archers, int cavalry)
        {
            Table(kind, out var inf, out var arc, out var cav, out var gen);
            var list = new List<Slot>(10);
            list.Add(Make(side, gen, mirror, UnitClass.Cavalry, true));
            Fill(list, side, inf, mirror, UnitClass.Infantry, infantry);
            Fill(list, side, arc, mirror, UnitClass.Archer, archers);
            Fill(list, side, cav, mirror, UnitClass.Cavalry, cavalry);
            return list;
        }

        static void Fill(List<Slot> list, FieldSide side, Offset[] offsets, bool mirror, UnitClass cls, int count)
        {
            if (count > offsets.Length) count = offsets.Length;
            for (int i = 0; i < count; i++) list.Add(Make(side, offsets[i], mirror, cls, false));
        }

        static Slot Make(FieldSide side, Offset o, bool mirror, UnitClass cls, bool general)
        {
            int row = mirror ? FieldRules.Rows - 1 - o.row : o.row;
            return new Slot { cls = cls, isGeneral = general, col = ColFor(side, o.depth), row = row };
        }
    }
}
