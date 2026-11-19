using UnityEngine;
public sealed class BublikCollectible : MonoBehaviour
{
    public int Stage;
    public Vector3 RestPosition;
    public float BobPhase;
    public bool Taken;
    void Start(){RestPosition=transform.position;}
    void Update()
    {
        if(Taken)return;
        transform.position=RestPosition+new Vector3(0,Mathf.Sin(Time.time*2.7f+BobPhase)*.13f,0);
        transform.Rotate(0,0,Time.deltaTime*48f);
    }
}
