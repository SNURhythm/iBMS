
public class LandmineNote: Note
{
    public float Damage { get; private set; }
    public LandmineNote(float damage) : base(0)
    {
        Damage = damage;
    }
    
    
}
