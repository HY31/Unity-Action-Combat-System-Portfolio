using UnityEngine;

public class EnemyHitBoxTester : MonoBehaviour
{
    [SerializeField] private HitBox attackHitBox;
    [SerializeField] private KeyCode triggerKey = KeyCode.T;
    [SerializeField] private float activeDuration = 0.2f;

    private float timer;

    private void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            if (attackHitBox != null)
                attackHitBox.SetActive(true);

            timer = activeDuration;
        }

        if (timer > 0f)
        {
            timer -= Time.deltaTime;

            if (timer <= 0f && attackHitBox != null)
                attackHitBox.SetActive(false);
        }
    }
}