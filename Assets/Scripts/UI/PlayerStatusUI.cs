using UnityEngine;

/// <summary>
/// 단일 PlayerController의 런타임 자원을 플레이어 상태 UI에 전달한다.
/// 에너지는 직접 조회하고, 현재 HP가 아직 컨트롤러에 없으므로 체력은 외부 갱신도 받을 수 있게 둔다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStatusUI : MonoBehaviour
{
    [SerializeField] private PlayerController targetPlayer;
    [SerializeField] private AnimatedGaugeUI healthGauge;
    [SerializeField] private AnimatedGaugeUI energyGauge;
    [SerializeField] private bool findPlayerByTag = true;

    private void Start()
    {
        ResolvePlayer();
        Refresh(true);
    }

    private void LateUpdate()
    {
        if (targetPlayer == null)
            ResolvePlayer();

        Refresh(false);
    }

    public void Bind(PlayerController player)
    {
        targetPlayer = player;
        Refresh(true);
    }

    public void SetHealth(float current, float maximum, bool instant = false)
    {
        // PlayerController에 현재 HP 소유 구조가 생기기 전까지 외부 전투 시스템이 체력을 주입하는 진입점이다.
        if (healthGauge != null)
            healthGauge.SetValue(current, maximum, instant);
    }

    public void SetHealthNormalized(float normalized, bool instant = false)
    {
        if (healthGauge != null)
            healthGauge.SetNormalizedValue(normalized, instant);
    }

    public void Configure(AnimatedGaugeUI health, AnimatedGaugeUI energy)
    {
        healthGauge = health;
        energyGauge = energy;
    }

    private void ResolvePlayer()
    {
        if (targetPlayer != null || !findPlayerByTag)
            return;

        // 씬 참조가 비어 있을 때만 활성 Player 태그를 폴백으로 탐색한다.
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            targetPlayer = player.GetComponent<PlayerController>();
    }

    private void Refresh(bool instant)
    {
        if (targetPlayer == null || energyGauge == null)
            return;

        // 현재 PlayerController가 직접 소유하는 자원만 자동 갱신한다.
        energyGauge.SetValue(targetPlayer.CurrentEnergy, targetPlayer.MaxEnergy, instant);
    }
}
