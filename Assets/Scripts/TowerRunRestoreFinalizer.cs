using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TowerRunRestoreFinalizer : MonoBehaviour, IMapRuntimeReadyFinalizer
{
    public bool TryFinalizeBeforeReady(
        MapRuntimeContext context,
        Guid generationId,
        out string reason)
    {
        TowerRunCoordinator coordinator = GameSession.Instance == null
            ? null
            : GameSession.Instance.TowerRun;

        if (coordinator == null)
        {
            reason = "正式楼层 Ready 前缺少唯一的 TowerRunCoordinator。";
            return false;
        }

        if (!coordinator.HasStagedOperation)
        {
            reason = "正式楼层 Ready 前没有匹配的 staged 运行数据。";
            return false;
        }

        return coordinator.TryFinalizeStagedOperation(context, generationId, out reason);
    }
}
