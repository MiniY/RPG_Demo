using System.Collections;
using UnityEngine;

// 视觉只消费已提交结果；根对象与拾取范围始终保持原位。
public class RewardVisualController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string spawnStateName = "Spawn";
    [SerializeField] private string isCollectedParameterName = "IsCollected";
    [SerializeField, Min(0)] private int animationLayer;
    [SerializeField] private bool showMissingStateWarning = true;
    private Vector3 restPosition;
    private bool initialized;
    private bool pausedAnimator;
    private bool animatorWasEnabled;
    private int presentationLease;

    private void Awake() => EnsureInitialized();
    private void OnEnable() => ResetForSpawn();
    private void OnDisable() => CancelPresentation();

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        restPosition = transform.localPosition;
        if (animator == null) animator = GetComponent<Animator>();
    }

    public void ResetForSpawn()
    {
        EnsureInitialized();
        CancelPresentation();
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(spawnStateName)) return;
        SetCollected(false);
        int state = Animator.StringToHash(spawnStateName);
        if (!animator.HasState(animationLayer, state))
        {
            if (showMissingStateWarning) Debug.LogWarning($"奖励动画状态不存在：{spawnStateName}。", this);
            return;
        }
        animator.Play(state, animationLayer, 0f);
        animator.Update(0f);
    }

    public void MarkCollected() => SetCollected(true);

    public IEnumerator PlayInventoryHop()
    {
        EnsureInitialized();
        CancelPresentation();
        int token = presentationLease;
        MarkCollected();
        if (animator != null)
        {
            animatorWasEnabled = animator.enabled;
            animator.enabled = false;
            pausedAnimator = true;
        }
        float elapsed = 0f;
        while (token == presentationLease && isActiveAndEnabled && elapsed < .28f)
        {
            transform.localPosition = restPosition + Vector3.up * (.28f * Mathf.Sin(Mathf.PI * elapsed / .28f));
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        if (token == presentationLease) CancelPresentation();
    }

    public void CancelPresentation()
    {
        EnsureInitialized();
        presentationLease++;
        transform.localPosition = restPosition;
        if (animator != null && pausedAnimator) animator.enabled = animatorWasEnabled;
        pausedAnimator = false;
    }

    private void SetCollected(bool value)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(isCollectedParameterName)) return;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.name == isCollectedParameterName && parameter.type == AnimatorControllerParameterType.Bool)
            { animator.SetBool(isCollectedParameterName, value); break; }
    }
}
