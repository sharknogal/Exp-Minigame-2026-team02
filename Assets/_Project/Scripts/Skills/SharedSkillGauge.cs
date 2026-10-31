using System;

namespace BrickBreaker.Skills
{
    // 공과 UI를 몰라도 동작하는 공유 게이지 규칙이다.
    public sealed class SharedSkillGauge
    {
        public const int Capacity = 600;

        private bool isResolving;
        private int shotVersion;

        public long Charge { get; private set; }
        public bool HasAbilities { get; private set; }
        public bool IsShotActive { get; private set; }
        public long ActivationCount { get; private set; }
        public float NormalizedCharge => (float)Math.Min(Charge, Capacity) / Capacity;

        public event Action Changed;
        public event Action AbilityActivationRequested;

        public void ResetGame()
        {
            // 발동 알림 도중 새 게임을 시작해도 이전 판의 발동은 이어지지 않는다.
            shotVersion++;
            Charge = 0;
            HasAbilities = false;
            IsShotActive = false;
            ActivationCount = 0;
            Changed?.Invoke();
        }

        public void SetHasAbilities(bool value)
        {
            // 능력 구매는 조준 단계에서만 가능하다. 전투 중의 변경은 받지 않는다.
            if (IsShotActive || HasAbilities == value)
                return;

            HasAbilities = value;
            if (!HasAbilities)
                Charge = Math.Min(Charge, Capacity);

            // 600을 채운 뒤 첫 능력을 사더라도 지금은 발동하지 않는다.
            Changed?.Invoke();
        }

        public void BeginShot()
        {
            if (IsShotActive)
                return;

            shotVersion++;
            IsShotActive = true;
            ResolveActivations();
        }

        public void EndShot()
        {
            if (!IsShotActive)
                return;

            shotVersion++;
            IsShotActive = false;
            // 턴이 끝나도 충전량과 보유 능력은 유지한다.
            Changed?.Invoke();
        }

        public void AddBallCharge(long amount)
        {
            // 능력 발동 콜백에서 다시 충전하는 연쇄도 막는다.
            if (!IsShotActive || amount <= 0 || isResolving)
                return;

            if (!HasAbilities)
            {
                // 먼저 남은 용량과 비교하면 큰 입력도 더하기 오버플로 없이 제한된다.
                Charge = amount >= Capacity - Charge ? Capacity : Charge + amount;
                Changed?.Invoke();
                return;
            }

            // 잘못된 값이 long 범위를 넘으면 음수로 바뀌지 않고 오류를 알린다.
            Charge = checked(Charge + amount);
            ResolveActivations();
        }

        private void ResolveActivations()
        {
            if (isResolving)
                return;

            int resolvingVersion = shotVersion;
            isResolving = true;
            try
            {
                while (IsShotActive && HasAbilities && Charge >= Capacity &&
                    shotVersion == resolvingVersion)
                {
                    // 먼저 소모하고 알린다. 수신자는 이 요청 한 번에 모든 보유 능력을 쓴다.
                    Charge -= Capacity;
                    ActivationCount = checked(ActivationCount + 1);
                    AbilityActivationRequested?.Invoke();
                }
            }
            finally
            {
                isResolving = false;
                Changed?.Invoke();
            }
        }
    }
}
