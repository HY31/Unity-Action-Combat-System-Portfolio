using UnityEngine;

/// <summary>
/// 단일 PlayerController의 런타임 자원을 플레이어 상태 UI에 전달한다.
/// 체력과 에너지는 컨트롤러를 직접 조회하고, 외부 시스템의 즉시 갱신 진입점도 제공한다.
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
        // 전투 연출처럼 즉시 표시해야 하는 외부 시스템도 같은 체력 게이지를 갱신할 수 있다.
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
        if (targetPlayer == null)
            return;

        // 단일 HUD도 PlayerController가 소유하는 현재 체력과 에너지를 같은 시점에 갱신한다.
        if (healthGauge != null)
            healthGauge.SetValue(targetPlayer.CurrentHp, targetPlayer.CurrentMaxHp, instant);

        if (energyGauge != null)
            energyGauge.SetValue(targetPlayer.CurrentEnergy, targetPlayer.MaxEnergy, instant);
    }
}
