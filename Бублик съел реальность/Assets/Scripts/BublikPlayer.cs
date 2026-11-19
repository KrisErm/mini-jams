using UnityEngine;

// The object's origin is exactly at its physical feet. The visible sprite lives on a child.
[RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
public sealed class BublikPlayer : MonoBehaviour
{
    public BublikGame Game;
    public Rigidbody2D Body;
    public BoxCollider2D Hitbox;
    public SpriteRenderer Visual;
    public Sprite Idle, Run1, Run2, Jump, Fall;
    public LayerMask GroundMask=1 << 8;
    public float MoveSpeed=6.0f;
    public float JumpSpeed=7.9f;
    float inputX, jumpUntil, coyoteUntil;
    bool grounded, faceRight=true;
    void Awake()
    {
        if(!Body)Body=GetComponent<Rigidbody2D>();
        if(!Hitbox)Hitbox=GetComponent<BoxCollider2D>();
        Body.freezeRotation=true;
        Body.interpolation=RigidbodyInterpolation2D.Interpolate;
        Body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        if(Hitbox){ Hitbox.size=new Vector2(.50f,1.34f); Hitbox.offset=new Vector2(0,.67f); Hitbox.edgeRadius=.05f; }
    }
    void Update()
    {
        if(Game==null)return;
        bool move=Game.AllowMovement;
        inputX=move?((BublikInput.Right()?1:0)-(BublikInput.Left()?1:0)):0;
        if(move&&BublikInput.JumpDown())jumpUntil=Time.time+.19f;
        if(!move)jumpUntil=-1;
        if(inputX!=0)faceRight=inputX>0;
        if(Visual)
        {
            Visual.flipX=!faceRight;
            if(!grounded && Body.linearVelocity.y>.2f)Visual.sprite=Jump;
            else if(!grounded&&Body.linearVelocity.y<-.15f)Visual.sprite=Fall;
            else if(Mathf.Abs(inputX)>.05f)Visual.sprite=Mathf.FloorToInt(Time.time*10)%2==0?Run1:Run2;
            else Visual.sprite=Idle;
        }
        if(transform.position.y < -7.1f)Game.Respawn();
    }
    void FixedUpdate()
    {
        if(Game==null||Body==null||Hitbox==null)return;
        // Never test the character's sprite: test the actual collider feet.
        Bounds b=Hitbox.bounds;
        Vector2 probe=new Vector2(b.center.x,b.min.y-.035f);
        Vector2 probeSize=new Vector2(b.size.x*.82f,.14f);
        grounded=Physics2D.OverlapBox(probe,probeSize,0f,GroundMask)!=null;
        if(grounded)coyoteUntil=Time.time+.12f;
        Body.gravityScale=Game.InLowGravity?1.45f:1.75f;
        Vector2 v=Body.linearVelocity;
        v.x=Game.AllowMovement?inputX*MoveSpeed:0;
        if(Game.AllowMovement&&Time.time<=jumpUntil&&Time.time<=coyoteUntil)
        {
            v.y=JumpSpeed;
            jumpUntil=-1;coyoteUntil=-1;grounded=false;
            Game.PlayJump();
        }
        Body.linearVelocity=v;
    }
    public void Teleport(float x,float floorTop)
    {
        Body.linearVelocity=Vector2.zero;
        // Keep the feet almost flush with the collider top after a respawn/door jump.
        Body.position=new Vector2(x,floorTop+.015f);
        transform.position=new Vector3(x,floorTop+.015f,0);
        jumpUntil=-1;coyoteUntil=Time.time+.08f;
    }
}
