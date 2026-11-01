using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class MapVNextI1IntegrationTests
{
    [UnityTest] public IEnumerator FormalPilotUsesOriginalBattleInputAndKeepsFailedWalkFailures()
    {
        yield return Load("Pilot-real-battle-input");
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MapVNextI1Acceptance")).First(t=>t!=null);
        var probe=(MonoBehaviour)controller.gameObject.AddComponent(type);
        Application.LogCallback expectedBoundedRetry=(message,stack,kind)=>
        {
            if(kind==LogType.Error&&(message.StartsWith("Map generation failed [VNextFormalLayoutRejected]:",StringComparison.Ordinal)||message.StartsWith("Map generation failed [EcologyBudgetExhaustedOnFixedTerrain]:",StringComparison.Ordinal)))
                LogAssert.Expect(LogType.Error,message);
        };
        Application.logMessageReceived+=expectedBoundedRetry;
        IEnumerator pilot=null;
        try
        {
            pilot=(IEnumerator)type.GetMethod("RunPilotAcceptanceForTests").Invoke(probe,null);
            while(pilot.MoveNext())yield return pilot.Current;
        }
        finally
        {
            Application.logMessageReceived-=expectedBoundedRetry;
            try {(pilot as IDisposable)?.Dispose();}finally {UnityEngine.Object.Destroy(probe);}
        }
    }
}
