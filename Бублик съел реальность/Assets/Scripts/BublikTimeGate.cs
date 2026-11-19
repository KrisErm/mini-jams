using UnityEngine;
/// <summary>Real 2D blocker across the bridge. Stops the player until time is frozen.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class BublikTimeGate : MonoBehaviour
{
    public int Stage;
    public SpriteRenderer Visual;
    BoxCollider2D physicalBarrier;
    void Awake(){physicalBarrier=GetComponent<BoxCollider2D>();SetOpen(false);}
    public void SetOpen(bool open)
    {
        if(!physicalBarrier)physicalBarrier=GetComponent<BoxCollider2D>();
        if(physicalBarrier)physicalBarrier.enabled=!open;
        if(Visual)Visual.enabled=!open;
    }
}
