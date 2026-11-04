using System.Collections.Generic;
using BrickBreaker.Balls;
using UnityEngine;

namespace BrickBreaker.Launch
{
    // 공 중심이 반지름을 유지한 채 도달할 첫 지점을 계산한다. 피해 적용은 BallFlight의 일이다.
    public static class BallFlightPath
    {
        internal const float Skin = 0.0005f;
        private const float TieTolerance = 0.00005f;
        // Unity의 물리 질의는 메인 스레드에서 끝난다. 공마다 매 프레임 배열을 만들지 않는다.
        private static readonly List<RaycastHit2D> castHits = new List<RaycastHit2D>(32);

        internal readonly struct Contact
        {
            public readonly float Distance;
            public readonly Vector2 Normal;
            public readonly Block Block;
            public readonly bool IsFloor;
            private readonly bool bothWalls;

            public Contact(float distance, Vector2 normal, Block block = null,
                bool isFloor = false, bool bothWalls = false)
            {
                Distance = distance;
                Normal = normal;
                Block = block;
                IsFloor = isFloor;
                this.bothWalls = bothWalls;
            }

            public Vector2 Reflect(Vector2 direction)
            {
                // 좌우 벽과 천장을 정확히 동시에 만나면 두 축 모두 반전한다.
                return bothWalls ? -direction : Vector2.Reflect(direction, Normal).normalized;
            }
        }

        // LineRenderer용 월드 좌표: 발사점, 첫 접점, 한 번 반사한 뒤의 다음 접점.
        // 미리보기는 블록이 살아 있다고 가정한다. 실제 파괴/관통은 피해를 적용한 뒤에만 확정한다.
        public static int GetPreviewPoints(BallEffectController effects, Rect bounds,
            Vector2 origin, Vector2 direction, Vector3[] points)
        {
            if (effects == null || effects.Board == null || !effects.Board.IsAvailable ||
                points == null || points.Length < 3 || !IsValidBounds(bounds) ||
                !IsFinite(origin) || !IsFinite(direction) || direction.sqrMagnitude < 0.000001f)
                return 0;

            Physics2D.SyncTransforms(); // 조준 단계에서 방금 생성되거나 하강한 블록도 포함한다.
            Vector2 position = ClampCenter(bounds, origin);
            Vector2 heading = direction.normalized;
            points[0] = position;
            int count = 1;
            Block previousBlock = null;
            // 보드 안의 한 선분은 대각선보다 길 수 없다.
            float maxDistance = bounds.size.magnitude + BallFlight.Radius;
            for (int segment = 0; segment < 2; segment++)
            {
                if (!TryFindContact(effects, bounds, position, heading, maxDistance, previousBlock, out Contact hit))
                    break;
                position += heading * hit.Distance;
                points[count++] = position;
                if (hit.IsFloor)
                    break;
                heading = hit.Reflect(heading);
                previousBlock = hit.Block;
                position += heading * Skin;
            }
            return count;
        }

        internal static bool TryFindContact(BallEffectController effects, Rect bounds, Vector2 origin,
            Vector2 heading, float maxDistance, Block previousBlock, out Contact contact)
        {
            Contact boundary = FirstBoundary(bounds, origin, heading);
            bool meetsBoundary = boundary.Distance <= maxDistance;
            float castDistance = Mathf.Min(maxDistance, boundary.Distance);
            var filter = ContactFilter2D.noFilter;
            filter.useTriggers = false;
            effects.gameObject.scene.GetPhysicsScene2D().CircleCast(origin, BallFlight.Radius, heading,
                castDistance, filter, castHits);

            Block firstBlock = null;
            float firstDistance = castDistance;
            int firstCell = int.MaxValue;
            Vector2 normalSum = Vector2.zero;
            foreach (RaycastHit2D hit in castHits)
            {
                if (hit.collider == null || hit.distance > firstDistance + TieTolerance ||
                    Vector2.Dot(heading, hit.normal) >= 0f)
                    continue;
                // 바닥과 동시에 접촉했다면 자연 회수가 우선이다.
                if (meetsBoundary && boundary.IsFloor && hit.distance >= boundary.Distance - TieTolerance)
                    continue;

                Block block = hit.collider.GetComponentInParent<Block>();
                if (block == null || block.IsDestroyed ||
                    (block == previousBlock && hit.distance <= Skin * 2f) ||
                    !effects.Board.TryGetCell(block, out int row, out int column))
                    continue;

                if (firstBlock == null || hit.distance < firstDistance - TieTolerance)
                {
                    firstBlock = null;
                    firstCell = int.MaxValue;
                    normalSum = Vector2.zero;
                }
                firstDistance = Mathf.Min(firstDistance, hit.distance);
                normalSum += hit.normal;
                // 이음새에 정확히 닿으면 한 블록에만 직접 피해를 주고 접촉면의 합으로 반사한다.
                // 물리 엔진이 반환한 순서 대신 행, 열 순으로 직접 적중 대상을 고른다.
                int cell = row * BallBoardLookup.Columns + column;
                if (cell < firstCell)
                {
                    firstCell = cell;
                    firstBlock = block;
                }
            }

            if (firstBlock != null)
            {
                Vector2 normal = normalSum.sqrMagnitude > 0.000001f ? normalSum.normalized : -heading;
                contact = new Contact(firstDistance, normal, firstBlock);
                return true;
            }
            contact = boundary;
            return meetsBoundary;
        }

        private static Contact FirstBoundary(Rect bounds, Vector2 origin, Vector2 heading)
        {
            float radius = BallFlight.Radius;
            float dx = float.PositiveInfinity, dy = float.PositiveInfinity;
            if (heading.x > 0f) dx = (bounds.xMax - radius - origin.x) / heading.x;
            if (heading.x < 0f) dx = (bounds.xMin + radius - origin.x) / heading.x;
            if (heading.y > 0f) dy = (bounds.yMax - radius - origin.y) / heading.y;
            if (heading.y < 0f) dy = (bounds.yMin + radius - origin.y) / heading.y;
            dx = Mathf.Max(0f, dx);
            dy = Mathf.Max(0f, dy);

            bool corner = Mathf.Abs(dx - dy) <= TieTolerance;
            if (dy < dx || corner)
                return new Contact(dy, heading.y > 0f ? Vector2.down : Vector2.up,
                    isFloor: heading.y < 0f, bothWalls: corner);
            return new Contact(dx, heading.x > 0f ? Vector2.left : Vector2.right);
        }

        internal static Vector2 ClampCenter(Rect bounds, Vector2 point)
        {
            float radius = BallFlight.Radius;
            return new Vector2(Mathf.Clamp(point.x, bounds.xMin + radius, bounds.xMax - radius),
                Mathf.Clamp(point.y, bounds.yMin + radius, bounds.yMax - radius));
        }

        internal static bool IsValidBounds(Rect bounds)
        {
            return IsFinite(bounds.min) && IsFinite(bounds.max) &&
                bounds.width > 2f * BallFlight.Radius && bounds.height > 2f * BallFlight.Radius;
        }

        internal static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) &&
                !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
        }
    }
}
