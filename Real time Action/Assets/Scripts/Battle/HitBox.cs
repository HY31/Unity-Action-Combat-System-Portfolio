using UnityEngine;

public class HitBox : MonoBehaviour
{
    private bool active;

    public void SetActive(bool value)
    {
        active = value;
        gameObject.SetActive(value);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!active) return;

        if (other.CompareTag("Enemy"))
        {
            Debug.Log("Àû ¸ÂÀ½!");
        }
    }
}
