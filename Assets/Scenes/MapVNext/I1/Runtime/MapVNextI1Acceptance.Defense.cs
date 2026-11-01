using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// This opt-in driver plays the existing Battle action. It never chooses a damage
// receiver, changes stats/AI, bypasses collision, or restores an unfinished walk.
public sealed partial class MapVNextI1Acceptance
{
    private bool autoDefense,defenseActive,defenseBattleQueued;
    private bool capturePlayerFrames=true;
    private Mouse defenseMouse;
    private float nextDefenseAt;
    private int defenseQueued,defensePerformed,defenseHits,defenseDefeats;
    private readonly List<BaseDamageable> defenseTargets=new List<BaseDamageable>();
    private readonly List<string> combatTrace=new List<string> {"phase,physicsTime,event,actor,target,health,stamina,generation,buildId,frame"};
    private PlayerAction observedDefenseAction;
    private FieldInfo observedDefenseWindup;
    private GameInput observedDefenseInput;
    private void RecordCombat(string kind,string target="")
    {
        var stats=GameSession.Instance.PlayerStats;
        combatTrace.Add(string.Join(",",trial,F(Time.fixedTime),kind,body.name.Replace(",","_"),target.Replace(",","_"),F(stats.CurrentHealth),F(stats.CurrentStamina),Context.ActiveGenerationId,MapHostBuildStamp.BuildId,Time.frameCount));
        File.WriteAllLines(Path.Combine(output,"combat-input.csv"),combatTrace);
    }
    private void OnDefenseBattle() {defenseBattleQueued=false;RecordCombat("GameInputBattle");}
    private void OnDefenseAttack(int direction) {defensePerformed++;RecordCombat("PlayerAttackPerformed-"+direction);}
    private void OnDefenseDamage(BaseDamageable target,DamageInfo info)
    {if(info.SourceTransform==body.transform) {defenseHits++;RecordCombat("OriginalDamage-health-"+F(target.CurrentHealth),target.name);}}
    private void OnDefenseDefeat(BaseDamageable target)
    {defenseDefeats++;RecordCombat("ObservedUnattributedDefeat",target.name);}
    private void BeginDefenseObservation()
    {
        Require(!defenseActive&&defenseMouse!=null,"DefenseInputMustBeExplicitAndBound");
        observedDefenseWindup=typeof(PlayerAction).GetField("pendingAttackCoroutine",BindingFlags.Instance|BindingFlags.NonPublic);
        Require(observedDefenseWindup!=null,"OriginalAttackCompletionObservationUnavailable");
        defenseActive=true;defenseBattleQueued=false;nextDefenseAt=0;defenseQueued=defensePerformed=defenseHits=defenseDefeats=0;previousGround.Clear();
        observedDefenseAction=action;observedDefenseInput=GameInput.Instance;
        observedDefenseAction.OnAttackPerformed+=OnDefenseAttack;observedDefenseInput.OnBattlePressed+=OnDefenseBattle;
        defenseTargets.AddRange(FindObjectsOfType<BaseDamageable>());
        foreach(var target in defenseTargets) {target.OnDamaged+=OnDefenseDamage;target.OnDefeated+=OnDefenseDefeat;}
        RecordCombat("PositiveWalkBegin");
    }
    private void ReleaseDefenseMouse()
    {
        if(defenseMouse!=null&&defenseMouse.added)InputSystem.QueueStateEvent(defenseMouse,new MouseState {position=new Vector2(Screen.width*.5f,Screen.height*.5f)});
    }
    private void FeedDefense()
    {
        if(!defenseActive||defenseBattleQueued||Time.time<nextDefenseAt||!GameInput.Instance.IsGameplayInputEnabled)return;
        if(observedDefenseWindup.GetValue(observedDefenseAction)!=null)return;
        bool nearby=defenseTargets.Any(t=>t!=null&&t.gameObject.activeInHierarchy&&!t.IsDefeated&&t.GetComponent<MonsterAIController>()!=null&&MapElevationRuntimeQuery.SameLevel(body.position,t.transform.position)&&Vector2.Distance(body.position,t.transform.position)<1.75f);
        if(!nearby)return;
        // Wait for the original attack owner to finish before the next normal click.
        // The minimum spacing also exceeds the original 0.24s hit delay.
        nextDefenseAt=Time.time+.30f;defenseQueued++;RecordCombat("OriginalWindupIdleBeforeMouse");
        defenseBattleQueued=true;
        InputSystem.QueueStateEvent(defenseMouse,new MouseState {position=new Vector2(Screen.width*.5f,Screen.height*.5f)}.WithButton(MouseButton.Left));
        keys+="+MouseLeft";RecordCombat("MouseLeftQueued");
    }
    private void ObserveDefenseStep()
    {if(defenseActive&&samples%8==0)AuditConsumers();}
    private IEnumerator DrainOriginalPositiveAttack()
    {
        trial="Positive-Original-Attack-Drain";Feed();yield return null;
        float until=Time.time+.32f;
        while(Time.time<until)
        {
            Vector2 before=FootPosition;Feed();yield return new WaitForFixedUpdate();Sample(before);ObserveDefenseStep();
        }
        RecordCombat("OriginalWindupNaturallyDrainedBeforeKeyboardOnlyNegative");
    }
    private void StopDefenseObservation()
    {
        if(!defenseActive)return;
        defenseActive=false;defenseBattleQueued=false;ReleaseDefenseMouse();
        if(observedDefenseAction!=null)observedDefenseAction.OnAttackPerformed-=OnDefenseAttack;
        if(observedDefenseInput!=null)observedDefenseInput.OnBattlePressed-=OnDefenseBattle;
        foreach(var target in defenseTargets)if(target!=null) {target.OnDamaged-=OnDefenseDamage;target.OnDefeated-=OnDefenseDefeat;}
        defenseTargets.Clear();observedDefenseAction=null;observedDefenseInput=null;observedDefenseWindup=null;
        evidence.checks.Add("Normal positive-walk self-defense queued="+defenseQueued+" performed="+defensePerformed+" actual nonlethal hits="+defenseHits+" observed defeats (source unavailable)="+defenseDefeats+"; original health/stamina/targeting/AI/collision unchanged.");
        RecordCombat("PositiveWalkObservationEnded");
    }
    private void RestoreCompletedWalkForConsumerFixture(int seed)
    {
        Require(evidence.inputs.Last().status=="READY_SAVED_REOPENED"&&!defenseActive,"ConsumerFixtureCannotRescueUnfinishedWalk");
        var completed=Context.ActiveGenerationId.Value;var read=session.ReadSlot(2);Require(read.state==TowerRunSlotState.Valid,"CompletedWalkEntranceFixtureMissing");
        session.ReturnToStartupChoice();session.BeginRestoreSlot(2,read.confirmationToken);Ready();BindPlayer();previousGround.Clear();
        Require(Context.ActiveGenerationId.Value!=completed,"ConsumerFixtureNeedsSeparateGeneration");ExportMap(seed);
        evidence.checks.Add("Only after completed continuous positive walk and separate A/D trials: restored original entrance into new Generation "+Context.ActiveGenerationId+" for independent full-population consumer fixture; completed movement Generation "+completed+" retained.");Persist();
    }
    public IEnumerator RunPilotAcceptanceForTests()
    {
        output=Environment.GetEnvironmentVariable("RPG_I1_EVIDENCE");Directory.CreateDirectory(output);phase="pilot";capturePlayerFrames=false;
        evidence=new Evidence {buildId=MapHostBuildStamp.BuildId,implementationDigest=MapHostBuildStamp.ImplementationDigest,phase="EDITOR_PILOT_REAL_BATTLE_INPUT",slotRoot=TowerRunRuntimeEnvironment.SlotRoot};
        var stack=new Stack<IEnumerator>();stack.Push(Run());Exception failure=null;
        try
        {
            while(stack.Count>0)
            {
                bool moved=false;object current=null;
                try {moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}catch(Exception error) {failure=error;break;}
                if(!moved) {(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(current is IEnumerator nested)stack.Push(nested);else yield return current;
            }
            if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
        finally
        {
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();FlushConsumerTrace();CleanupInput();
            evidence.status=failure==null?"EDITOR_PILOT_COMPLETE_SEE_XML":"FAIL";evidence.failure=failure?.ToString();Persist();
        }
    }
}
