using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[System.Serializable]
public struct ElementIconEntry
{
    // 전투 속성과 인스펙터에서 지정한 표시용 Sprite를 연결한다.
    public CombatElement element;
    public Sprite sprite;
}

/// <summary>
/// 단일 적의 HP와 속성 이상 게이지를 월드 앵커 위에 표시하는 기본형 상태 UI다.
/// </summary>
public class EnemyStatusUI : MonoBehaviour
{
    [SerializeField] private EnemyController targetEnemy;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image anomalyFill;
    [SerializeField] private Image anomalyIcon;
    [SerializeField] private Transform worldAnchor;
    [SerializeField] private GameObject visualRoot;
    [FormerlySerializedAs("screenOffset")]
    [SerializeField] private Vector3 worldOffset;
    [SerializeField] private ElementIconEntry[] anomalyIcons;

    private void LateUpdate()
    {
        if (targetEnemy == null || hpFill == null || anomalyFill == null || worldAnchor == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 screenPos = cam.WorldToScreenPoint(worldAnchor.position + worldOffset);
        transform.position = screenPos;

        bool visible = screenPos.z > 0f;

        // 스크립트가 붙은 오브젝트는 켜 둬야 화면 안으로 돌아왔을 때 스스로 다시 표시할 수 있다.
        if (visualRoot != null)
            visualRoot.SetActive(visible);

        if (!visible)
            return;

        hpFill.fillAmount = targetEnemy.CurrentHpNormalized;
        anomalyFill.fillAmount = targetEnemy.CurrentAnomalyNormalized;
        UpdateAnomalyIcon(targetEnemy.DisplayAnomalyElement);
    }

    private void UpdateAnomalyIcon(CombatElement element)
    {
        if (anomalyIcon == null)
            return;

        // 게이지가 비었거나 속성이 없으면 이전 공격의 아이콘이 남지 않게 숨긴다.
        if (element == CombatElement.None || targetEnemy.CurrentAnomalyNormalized <= 0f)
        {
            anomalyIcon.enabled = false;
            return;
        }

        if (anomalyIcons != null)
        {
            for (int i = 0; i < anomalyIcons.Length; i++)
            {
                if (anomalyIcons[i].element != element || anomalyIcons[i].sprite == null)
                    continue;

                anomalyIcon.sprite = anomalyIcons[i].sprite;
                anomalyIcon.enabled = true;
                return;
            }
        }

        // 매핑이 빠진 속성에는 잘못된 이전 아이콘 대신 아무것도 표시하지 않는다.
        anomalyIcon.enabled = false;
    }
}
