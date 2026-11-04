using System;
using BrickBreaker.Balls;
using UnityEngine;

namespace BrickBreaker.Launch
{
    // 표시용 공 오브젝트 하나의 이동을 맡는다. 피해와 특수효과는 기존 ApplyHit에 맡긴다.
    [DisallowMultipleComponent]
    public sealed class BallFlight : MonoBehaviour
    {
        public const float Radius = 0.10f;
        public const float InitialSpeed = 10f;
        public const float MaxSpeed = 24f;
        private const float SpeedPerHit = 0.3f;
        private const int MaxContactsPerStep = 16;

        public bool IsFlying { get; private set; }
        public float Speed { get; private set; }
        public Vector2 Direction { get; private set; }

        private BallEffectController effects;
        private Rect bounds;
        private int shotId;
        private int ballId;
        private Block previousBlock;
        private Action<BallFlight, float> onReturn;

        public void Launch(BallEffectController effects, int shotId, int ballId, Rect bounds,
            Vector2 origin, Vector2 direction, Action<BallFlight, float> onReturn)
        {
            if (effects == null || effects.Board == null || !effects.Board.IsAvailable ||
                !effects.IsShotActive || effects.CurrentShotId != shotId)
                throw new InvalidOperationException("공을 등록한 발사 구간과 보드가 필요합니다.");
            if (!BallFlightPath.IsValidBounds(bounds) || !BallFlightPath.IsFinite(origin) ||
                !BallFlightPath.IsFinite(direction) || direction.sqrMagnitude < 0.000001f)
                throw new ArgumentException("필드 범위, 발사 위치 또는 방향이 올바르지 않습니다.");

            this.effects = effects;
            this.shotId = shotId;
            this.ballId = ballId;
            this.bounds = bounds;
            this.onReturn = onReturn;
            previousBlock = null;
            Speed = InitialSpeed;
            Direction = direction.normalized;
            IsFlying = true;
            SetPosition(BallFlightPath.ClampCenter(bounds, origin));
            Physics2D.SyncTransforms(); // 발사 직전 바뀐 정적 블록 콜라이더를 반영한다.
        }

        private void FixedUpdate() => Advance(Time.fixedDeltaTime);

        // 프레임 끝점만 검사하지 않고 이동할 거리 전체를 훑는다. 한 스텝 안의 연속 충돌도 처리한다.
        public void Advance(float seconds)
        {
            if (!IsFlying || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
                return;
            if (!IsCurrentShot())
            {
                Cancel();
                return;
            }

            float remainingTime = seconds;
            Vector2 position = transform.position;
            for (int i = 0; i < MaxContactsPerStep && remainingTime > 0f; i++)
            {
                float distance = Speed * remainingTime;
                if (!BallFlightPath.TryFindContact(effects, bounds, position, Direction, distance,
                    previousBlock, out BallFlightPath.Contact hit))
                {
                    SetPosition(position + Direction * distance);
                    return;
                }

                // 충돌 전 속도로 접점까지 걸린 시간을 먼저 빼야 가속 후 남은 이동도 정확하다.
                position += Direction * hit.Distance;
                remainingTime = Mathf.Max(0f, remainingTime - hit.Distance / Speed);
                SetPosition(position);
                if (hit.IsFloor)
                {
                    var callback = onReturn;
                    Cancel();
                    // 자연 회수만 통지한다. 오브젝트 제거와 다음 발사 위치 결정은 발사 담당의 일이다.
                    callback?.Invoke(this, position.x);
                    return;
                }

                bool reflect = true;
                if (hit.Block != null)
                {
                    Block target = hit.Block;
                    previousBlock = target;
                    HitResult result = effects.ApplyHit(shotId, ballId, target);
                    // 피해 이벤트가 동기적으로 강제 회수/구간 종료를 요청할 수도 있다.
                    if (!IsFlying || !IsCurrentShot())
                    {
                        Cancel();
                        return;
                    }
                    if (result.IsValid)
                        Speed = Mathf.Min(MaxSpeed, Speed + SpeedPerHit);
                    // 게이지 능력이 피해 이벤트 안에서 블록을 추가 파괴한 경우도 관통한다.
                    reflect = result.IsValid && !result.DirectTargetDestroyed && target != null && !target.IsDestroyed;
                }

                if (reflect)
                    Direction = hit.Reflect(Direction);
                // 접점에 붙은 다음 질의가 같은 면을 다시 때리지 않게 실제 남은 거리 안에서만 전진한다.
                float separation = Mathf.Min(BallFlightPath.Skin, Speed * remainingTime);
                position += Direction * separation;
                remainingTime = Mathf.Max(0f, remainingTime - separation / Speed);
                SetPosition(position);
            }
            // 비정상적으로 좁은 틈에서 반복 한도를 채우면 관통시키지 않고 다음 물리 스텝을 기다린다.
        }

        public void Cancel()
        {
            IsFlying = false;
            onReturn = null;
            previousBlock = null;
        }

        private bool IsCurrentShot()
        {
            return effects != null && effects.IsShotActive && effects.CurrentShotId == shotId &&
                effects.Board != null && effects.Board.IsAvailable;
        }

        private void SetPosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        private void OnDisable() => Cancel();
    }
}
