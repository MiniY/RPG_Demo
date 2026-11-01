using System.Reflection;
using UnityEngine;

public sealed class MapVNextI1ActivationWitness : MonoBehaviour
{
    public int ExpectedAmount;public float ExpectedCircleRadius=-1;public bool Valid;
    private void OnEnable()
    {
        var pickup=GetComponent<RewardPickup>();
        Valid=pickup!=null&&(int)typeof(RewardPickup).GetField("amount",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(pickup)==ExpectedAmount;
        if(ExpectedCircleRadius>=0)
        {var circle=GetComponent<CircleCollider2D>();Valid&=circle!=null&&circle.isTrigger&&circle.radius==ExpectedCircleRadius&&circle.offset==Vector2.zero;}
    }
}
