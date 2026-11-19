using UnityEngine;
[RequireComponent(typeof(Rigidbody2D),typeof(BoxCollider2D))]
public sealed class BublikMovingPlatform : MonoBehaviour
{
    public int Stage;
    public float Amplitude=1.4f, Speed=1.4f, Phase, BaseX, BaseY, IdealX;
    public bool Frozen;
    float motionTime;
    Rigidbody2D body;
    BoxCollider2D solid;
    SpriteRenderer art;
    void Awake()
    {
        body=GetComponent<Rigidbody2D>();
        solid=GetComponent<BoxCollider2D>();
        art=GetComponentInChildren<SpriteRenderer>();
        body.bodyType=RigidbodyType2D.Kinematic;
        body.interpolation=RigidbodyInterpolation2D.Interpolate;
        body.useFullKinematicContacts=true;
        if(Mathf.Abs(BaseX)<.001f)BaseX=transform.position.x;
        if(Mathf.Abs(BaseY)<.001f)BaseY=transform.position.y;
        if(Mathf.Abs(IdealX)<.001f)IdealX=BaseX;
        motionTime=Time.fixedTime;
        Refresh();
    }
    void FixedUpdate()
    {
        if(Frozen)return;
        // Advance only while moving, so resuming never skips the paused interval.
        motionTime+=Time.fixedDeltaTime;
        float x=BaseX+Mathf.Sin(motionTime*Speed+Phase)*Amplitude;
        body.MovePosition(new Vector2(x,BaseY));
    }
    void Refresh()
    {
        // Before stopping time, the bridge is PHANTOM: no collision.
        if(solid)solid.enabled=Frozen;
        if(art)art.color=Frozen?Color.white:new Color(.75f,.88f,1f,.55f);
    }
    public bool TryFreeze()
    {
        Frozen=true;
        // Cancel motion immediately without snapping to IdealX or changing position.
        if(body)
        {
            body.linearVelocity=Vector2.zero;
            body.angularVelocity=0f;
        }
        Refresh();
        return true;
    }
    public void Unfreeze(){Frozen=false;Refresh();}
}
