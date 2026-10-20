/// <summary>A collider or one of its parents may implement this to receive hitscan damage.</summary>
public interface IDamageable
{
    void TakeDamage(float amount);
}
