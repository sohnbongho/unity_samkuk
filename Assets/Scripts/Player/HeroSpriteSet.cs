using System.Collections.Generic;
using Samkuk.Core;
using UnityEngine;

namespace Samkuk.Player
{
    /// <summary>캐릭터가 바라보는 4방향. 값은 걷기 시트의 행 번호와 같다.</summary>
    public enum FacingDir
    {
        Down = 0,
        Up = 1,
        Left = 2,
        Right = 3
    }

    /// <summary>
    /// 걷기 스프라이트 시트를 잘라 둔 것. 시트는 4열 x 4행:
    /// 열 = 걷기 프레임(0 서 있기, 1 한쪽 발 앞, 2 서 있기, 3 반대쪽 발 앞),
    /// 행 = 방향(위에서부터 아래, 위, 왼쪽, 오른쪽). 칸 크기는 텍스처 크기 / 4.
    /// 에디터에서 슬라이스하지 않고 실행 중에 <see cref="Sprite.Create"/>로 자르므로 가져오기 설정이 단순하다.
    /// 피벗은 발(아래에서 <see cref="PixelArt.WalkFootPixels"/>px 위, 가로 가운데): 트랜스폼 위치 = 발 위치라서 월드 정렬(발 y 로 앞뒤)과
    /// 지형 충돌(밑동 원)과 드리운 그림자(발에서 시작)가 모두 같은 점을 쓴다 (Step 14-4).
    /// </summary>
    public class HeroSpriteSet
    {
        public const int Columns = 4;
        public const int Rows = 4;

        static readonly Dictionary<(Texture2D, float), HeroSpriteSet> Cache = new Dictionary<(Texture2D, float), HeroSpriteSet>();

        readonly Sprite[] sprites = new Sprite[Columns * Rows];

        /// <summary>같은 텍스처는 한 번만 잘라 재사용한다.</summary>
        public static HeroSpriteSet Get(Texture2D sheet, float pixelsPerUnit)
        {
            if (sheet == null) return null;
            if (Cache.TryGetValue((sheet, pixelsPerUnit), out var cached) && cached.sprites[0] != null) return cached;

            var set = new HeroSpriteSet();
            set.Slice(sheet, pixelsPerUnit);
            Cache[(sheet, pixelsPerUnit)] = set;
            return set;
        }

        void Slice(Texture2D sheet, float pixelsPerUnit)
        {
            int cw = sheet.width / Columns;
            int ch = sheet.height / Rows;
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    // 스프라이트 좌표는 아래에서 위로 센다 (시트는 위에서부터 행이 시작)
                    var rect = new Rect(col * cw, sheet.height - (row + 1) * ch, cw, ch);
                    var sprite = Sprite.Create(sheet, rect, FootPivot(ch), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = $"{sheet.name}_{(FacingDir)row}_{col}";
                    sprites[row * Columns + col] = sprite;
                }
            }
        }

        /// <summary>칸 높이에 대한 발 피벗 (0~1).</summary>
        public static Vector2 FootPivot(int cellHeight) => new Vector2(0.5f, cellHeight > 0 ? (float)PixelArt.WalkFootPixels / cellHeight : 0f);

        public Sprite Get(FacingDir dir, int frame)
        {
            frame = ((frame % Columns) + Columns) % Columns;
            return sprites[(int)dir * Columns + frame];
        }
    }
}
