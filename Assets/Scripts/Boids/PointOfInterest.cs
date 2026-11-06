using UnityEngine;

public class PointOfInterest : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 30f;

    private float _health;

    public bool IsDestroyed { get; private set; }
    public float HealthRatio => _maxHealth > 0f ? _health / _maxHealth : 0f;

    private void Awake()
    {
        _health = _maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;

        _health = Mathf.Max(0f, _health - amount);

        if (_health > 0f) return;

        IsDestroyed = true;
        Destroy(gameObject);
    }
}
